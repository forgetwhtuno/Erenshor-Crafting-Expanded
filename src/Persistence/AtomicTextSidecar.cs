using System;
using System.IO;
using System.Text;

namespace ErenshorCraftingExpanded
{
    internal enum SidecarRecoverySource
    {
        None = 0,
        Primary = 1,
        Temp = 2,
        Backup = 3
    }

    internal delegate bool SidecarTextValidator(string text);

    // Small shared persistence primitive for mod-owned text sidecars. A complete flushed .tmp is
    // retained on failed replacement so the next load can recover the newest validated state.
    // Load recovery never trusts malformed/truncated candidates and never logs file contents.
    internal static class AtomicTextSidecar
    {
        internal const long MaximumBytes = 8L * 1024L * 1024L;

        internal static bool HasAnyCandidate(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return File.Exists(path) || File.Exists(path + ".tmp") || File.Exists(path + ".bak");
        }

        internal static bool TryReadPrimary(string path, out string text, out string error)
        {
            return TryReadBounded(path, out text, out error);
        }

        internal static bool TryLoadNewestValid(
            string path,
            SidecarTextValidator validator,
            out string text,
            out SidecarRecoverySource source,
            out string error)
        {
            text = string.Empty;
            source = SidecarRecoverySource.None;
            error = string.Empty;
            if (string.IsNullOrEmpty(path)) return false;

            string[] paths = new string[] { path, path + ".tmp", path + ".bak" };
            SidecarRecoverySource[] sources = new SidecarRecoverySource[]
            {
                SidecarRecoverySource.Primary,
                SidecarRecoverySource.Temp,
                SidecarRecoverySource.Backup
            };

            bool anyFile = false;
            long bestTicks = long.MinValue;
            int bestPriority = -1;
            string bestText = string.Empty;
            SidecarRecoverySource bestSource = SidecarRecoverySource.None;
            string lastCandidateError = string.Empty;

            for (int i = 0; i < paths.Length; i++)
            {
                string candidatePath = paths[i];
                if (!File.Exists(candidatePath)) continue;
                anyFile = true;

                string candidateText;
                string readError;
                if (!TryReadBounded(candidatePath, out candidateText, out readError))
                {
                    lastCandidateError = readError;
                    continue;
                }

                bool valid = false;
                try { valid = validator == null || validator(candidateText); }
                catch (Exception ex) { lastCandidateError = "validation failed: " + ex.GetType().Name; }
                if (!valid) continue;

                long ticks;
                try { ticks = File.GetLastWriteTimeUtc(candidatePath).Ticks; }
                catch { ticks = 0L; }

                // On timestamp ties prefer the primary, then a complete temp, then backup. This
                // avoids making a stale backup win merely because a filesystem has coarse times.
                int priority = sources[i] == SidecarRecoverySource.Primary ? 3 :
                    (sources[i] == SidecarRecoverySource.Temp ? 2 : 1);
                if (bestSource == SidecarRecoverySource.None || ticks > bestTicks || (ticks == bestTicks && priority > bestPriority))
                {
                    bestTicks = ticks;
                    bestPriority = priority;
                    bestText = candidateText;
                    bestSource = sources[i];
                }
            }

            if (bestSource != SidecarRecoverySource.None)
            {
                text = bestText;
                source = bestSource;
                return true;
            }

            if (anyFile)
                error = string.IsNullOrEmpty(lastCandidateError) ? "no valid sidecar candidate" : lastCandidateError;
            return false;
        }

        internal static bool WriteAtomic(
            string path,
            string text,
            SidecarTextValidator validator,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(path)) { error = "sidecar path unavailable"; return false; }
            if (text == null) text = string.Empty;
            if (Encoding.UTF8.GetByteCount(text) > MaximumBytes) { error = "sidecar exceeds size limit"; return false; }

            string temp = path + ".tmp";
            string backup = path + ".bak";
            try
            {
                string parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                WriteFlushed(temp, text);

                string tempText;
                string tempReadError;
                if (!TryReadBounded(temp, out tempText, out tempReadError) || (validator != null && !validator(tempText)))
                {
                    error = string.IsNullOrEmpty(tempReadError) ? "temporary sidecar failed validation" : tempReadError;
                    return false;
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, backup);
                    }
                    catch
                    {
                        // Some filesystems do not support Replace. Preserve the previous readable
                        // primary first, then copy the complete temp. Keep temp until validation.
                        File.Copy(path, backup, true);
                        File.Copy(temp, path, true);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }

                string committed;
                string committedReadError;
                bool committedValid = TryReadBounded(path, out committed, out committedReadError) &&
                    (validator == null || validator(committed));
                if (!committedValid)
                {
                    // Best-effort rollback if the fallback copy produced a damaged primary.
                    string backupText;
                    string backupError;
                    if (TryReadBounded(backup, out backupText, out backupError) &&
                        (validator == null || validator(backupText)))
                    {
                        try { File.Copy(backup, path, true); } catch { }
                    }
                    error = string.IsNullOrEmpty(committedReadError) ? "committed sidecar failed validation" : committedReadError;
                    return false;
                }

                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                // Deliberately retain a complete .tmp on failure. TryLoadNewestValid will only use
                // it if its codec validates, giving first-save and interrupted-copy recovery.
                error = ex.GetType().Name;
                return false;
            }
        }

        internal static string RunSelfTests()
        {
            string root = Path.Combine(Path.GetTempPath(), "ece-atomic-sidecar-tests-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "state.txt");
            SidecarTextValidator validator = delegate(string value) { return value != null && value.StartsWith("OK|", StringComparison.Ordinal); };
            try
            {
                string error;
                if (!WriteAtomic(path, "OK|one", validator, out error)) return "FAIL atomic initial write " + error;
                string text; SidecarRecoverySource source;
                if (!TryLoadNewestValid(path, validator, out text, out source, out error) || text != "OK|one" || source != SidecarRecoverySource.Primary)
                    return "FAIL atomic primary load";

                // A newer complete temp represents a crash after durable temp write but before
                // replacement and must win over the older still-readable primary.
                WriteFlushed(path + ".tmp", "OK|two");
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(path + ".tmp", new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));
                if (!TryLoadNewestValid(path, validator, out text, out source, out error) || text != "OK|two" || source != SidecarRecoverySource.Temp)
                    return "FAIL atomic newer temp recovery";

                // A newer malformed temp must never override a good primary.
                WriteFlushed(path + ".tmp", "BROKEN");
                File.SetLastWriteTimeUtc(path + ".tmp", new DateTime(2026, 1, 1, 0, 0, 4, DateTimeKind.Utc));
                if (!TryLoadNewestValid(path, validator, out text, out source, out error) || text != "OK|one" || source != SidecarRecoverySource.Primary)
                    return "FAIL atomic malformed temp rejection";

                // When the primary is corrupt, recover the newest valid backup/temp candidate.
                WriteFlushed(path + ".bak", "OK|backup");
                File.SetLastWriteTimeUtc(path + ".bak", new DateTime(2026, 1, 1, 0, 0, 5, DateTimeKind.Utc));
                WriteFlushed(path, "BROKEN");
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 6, DateTimeKind.Utc));
                if (!TryLoadNewestValid(path, validator, out text, out source, out error) || text != "OK|backup" || source != SidecarRecoverySource.Backup)
                    return "FAIL atomic backup recovery";

                return "PASS atomic text sidecar";
            }
            catch (Exception ex) { return "FAIL atomic sidecar exception " + ex.GetType().Name; }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        private static bool TryReadBounded(string path, out string text, out string error)
        {
            text = string.Empty;
            error = string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length < 0 || info.Length > MaximumBytes)
                {
                    error = "sidecar exceeds size limit";
                    return false;
                }
                text = File.ReadAllText(path, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                error = "sidecar read failed: " + ex.GetType().Name;
                return false;
            }
        }

        private static void WriteFlushed(string path, string text)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(text ?? string.Empty);
                writer.Flush();
                stream.Flush(true);
            }
        }
    }
}
