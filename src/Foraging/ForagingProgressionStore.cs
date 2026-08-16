using System;
using System.IO;

namespace ErenshorCraftingExpanded
{
    internal static class ForagingProgressionStore
    {
        private static string _lastError = string.Empty;
        private static string _lastRecovery = string.Empty;
        internal static string LastError { get { return _lastError; } }
        internal static string LastRecovery { get { return _lastRecovery; } }

        internal static string BuildPath(string dataRoot, string characterKey)
        {
            if (string.IsNullOrEmpty(dataRoot) || string.IsNullOrEmpty(characterKey)) return string.Empty;
            return Path.Combine(Path.Combine(dataRoot, "foraging-characters"), characterKey + ".dat");
        }

        internal static ForagingPersistentState Load(string path)
        {
            _lastError = string.Empty;
            _lastRecovery = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(path)) return new ForagingPersistentState();
                string text;
                string recoveryError;
                SidecarRecoverySource source;
                if (!AtomicTextSidecar.TryLoadNewestValid(path, IsValidText, out text, out source, out recoveryError))
                {
                    if (AtomicTextSidecar.HasAnyCandidate(path))
                        _lastError = string.IsNullOrEmpty(recoveryError)
                            ? "Foraging progression sidecar had no valid recovery candidate."
                            : "Foraging progression recovery failed: " + recoveryError + ".";
                    return new ForagingPersistentState();
                }

                bool valid;
                ForagingPersistentState state = ForagingProgressionCodec.Deserialize(text, out valid);
                if (!valid)
                {
                    _lastError = "Foraging progression sidecar format was invalid; safe defaults loaded.";
                    return new ForagingPersistentState();
                }
                state.Normalize();
                if (source != SidecarRecoverySource.Primary) _lastRecovery = source.ToString().ToLowerInvariant();
                return state;
            }
            catch (Exception ex)
            {
                _lastError = "Foraging progression load failed: " + ex.GetType().Name + ".";
                return new ForagingPersistentState();
            }
        }

        internal static bool Save(string path, ForagingPersistentState state)
        {
            _lastError = string.Empty;
            _lastRecovery = string.Empty;
            if (string.IsNullOrEmpty(path)) { _lastError = "Foraging progression save path unavailable."; return false; }
            try
            {
                string text = ForagingProgressionCodec.Serialize(state);
                string error;
                if (!AtomicTextSidecar.WriteAtomic(path, text, IsValidText, out error))
                {
                    _lastError = "Foraging progression save failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error) + ".";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _lastError = "Foraging progression save failed: " + ex.GetType().Name + ".";
                return false;
            }
        }

        private static bool IsValidText(string text)
        {
            bool valid;
            ForagingProgressionCodec.Deserialize(text, out valid);
            return valid;
        }

        internal static string RunSelfTests()
        {
            string a = BuildPath("root", ForagingCharacterKey.Compose("Aria", 0));
            string b = BuildPath("root", ForagingCharacterKey.Compose("Aria", 1));
            string c = BuildPath("root", ForagingCharacterKey.Compose("Borin", 0));
            if (string.IsNullOrEmpty(a) || string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || string.Equals(a, c, StringComparison.OrdinalIgnoreCase))
                return "FAIL foraging persistence character isolation";
            if (BuildPath("root", string.Empty).Length != 0 || BuildPath(string.Empty, "slot0_aria").Length != 0)
                return "FAIL unstable persistence path accepted";

            string root = Path.Combine(Path.GetTempPath(), "ece-foraging-store-tests-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "state.dat");
            try
            {
                ForagingPersistentState state = new ForagingPersistentState();
                state.Progress.Level = 7;
                state.Progress.Xp = 12;
                if (!Save(path, state)) return "FAIL foraging store save " + LastError;
                ForagingPersistentState loaded = Load(path);
                if (loaded.Progress.Level != 7 || loaded.Progress.Xp != 12) return "FAIL foraging store load";

                ForagingPersistentState newer = new ForagingPersistentState();
                newer.Progress.Level = 8;
                newer.Progress.Xp = 3;
                File.WriteAllText(path + ".tmp", ForagingProgressionCodec.Serialize(newer));
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(path + ".tmp", new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));
                ForagingPersistentState recovered = Load(path);
                if (recovered.Progress.Level != 8 || recovered.Progress.Xp != 3 || LastRecovery != "temp")
                    return "FAIL foraging newer temp recovery";

                return "PASS foraging progression store policy";
            }
            catch (Exception ex) { return "FAIL foraging progression store exception " + ex.GetType().Name; }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }
    }
}
