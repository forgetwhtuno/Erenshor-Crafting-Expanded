using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    [Serializable]
    internal sealed class CraftingSaveData
    {
        public const int CurrentSchemaVersion = 4;
        public int SchemaVersion = CurrentSchemaVersion;
        // Serialized field name remains Smithing for backward compatibility with the old sidecar.
        public CraftingProgress Smithing = new CraftingProgress();
        internal void Normalize()
        {
            if (Smithing == null) Smithing = new CraftingProgress();
            if (SchemaVersion < CurrentSchemaVersion)
            {
                int level = Smithing.Level;
                if (level < 1) level = 1;
                if (level > SmithingXpCurve.MaxLevel) level = SmithingXpCurve.MaxLevel;
                int oldNeed = SmithingXpCurve.LegacyXpToNextLevel(level);
                int newNeed = SmithingXpCurve.XpToNextLevel(level);
                Smithing.Xp = CraftingProgressionMigrationPolicy.PreserveInLevelProgress(level, Smithing.Xp, oldNeed, newNeed);
                SchemaVersion = CurrentSchemaVersion;
            }
            if (SchemaVersion < CurrentSchemaVersion) SchemaVersion = CurrentSchemaVersion;
            Smithing.Normalize();
        }
    }

    internal static class CraftingProgressionStore
    {
        private static string _lastError = string.Empty;
        private static string _lastRecovery = string.Empty;
        internal static string LastError { get { return _lastError; } private set { _lastError = value; } }
        internal static string LastRecovery { get { return _lastRecovery; } private set { _lastRecovery = value; } }

        internal static string CharacterDataPath(string root, string characterKey)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(characterKey)) return string.Empty;
            return Path.Combine(Path.Combine(Path.Combine(root, "Characters"), characterKey), "crafting-progress.json");
        }

        internal static string LegacyDataPath(string root) { return string.IsNullOrEmpty(root) ? string.Empty : Path.Combine(root, "smithing-progress.json"); }
        internal static string LegacyClaimMarkerPath(string root) { return string.IsNullOrEmpty(root) ? string.Empty : Path.Combine(root, "smithing-progress.migrated"); }

        internal static CraftingSaveData Load(string path)
        {
            LastError = string.Empty;
            LastRecovery = string.Empty;
            if (string.IsNullOrEmpty(path)) return NewData();

            // A future schema in the primary file is an explicit fail-closed condition. Do not
            // silently replace it with an older backup from this build.
            string primaryText;
            string primaryReadError;
            if (File.Exists(path) && AtomicTextSidecar.TryReadPrimary(path, out primaryText, out primaryReadError))
            {
                CraftingSaveData primary;
                if (TryParse(primaryText, out primary) && primary.SchemaVersion > CraftingSaveData.CurrentSchemaVersion)
                {
                    LastError = "Crafting progression sidecar is from a newer schema; refusing downgrade recovery.";
                    return NewData();
                }
            }

            string text;
            string recoveryError;
            SidecarRecoverySource source;
            if (!AtomicTextSidecar.TryLoadNewestValid(path, IsValidCurrentOrLegacyText, out text, out source, out recoveryError))
            {
                if (AtomicTextSidecar.HasAnyCandidate(path))
                    LastError = string.IsNullOrEmpty(recoveryError) ? "Crafting progression sidecar had no valid recovery candidate." : "Crafting progression recovery failed: " + recoveryError;
                return NewData();
            }

            CraftingSaveData data;
            if (!TryParse(text, out data))
            {
                LastError = "Crafting sidecar parsed as invalid; defaults loaded.";
                return NewData();
            }
            if (data.SchemaVersion > CraftingSaveData.CurrentSchemaVersion)
            {
                LastError = "Crafting progression sidecar is from a newer schema; refusing downgrade recovery.";
                return NewData();
            }
            data.Normalize();
            if (source != SidecarRecoverySource.Primary) LastRecovery = source.ToString().ToLowerInvariant();
            return data;
        }

        internal static CraftingSaveData LoadCharacterWithLegacyClaim(string root, string characterKey)
        {
            LastError = string.Empty;
            LastRecovery = string.Empty;
            string characterPath = CharacterDataPath(root, characterKey);
            if (string.IsNullOrEmpty(characterPath)) return NewData();
            // A valid .tmp/.bak from an interrupted character save belongs to this character and
            // must be considered before attempting one-time profile legacy migration.
            if (AtomicTextSidecar.HasAnyCandidate(characterPath)) return Load(characterPath);

            string legacyPath = LegacyDataPath(root);
            string markerPath = LegacyClaimMarkerPath(root);
            if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath) && TryAcquireLegacyClaim(markerPath, characterKey))
            {
                CraftingSaveData legacy = Load(legacyPath);
                // A corrupt/unreadable legacy sidecar must never be converted into a valid-looking
                // default character save. The marker remains owned by this same character so a
                // repaired legacy file can be retried without allowing a second character to claim it.
                if (!string.IsNullOrEmpty(LastError)) return NewData();
                if (Save(characterPath, legacy)) return legacy;
                // The marker intentionally remains owned by this character. A later load for the
                // same key can retry the copy; another character can never duplicate the legacy XP.
                return NewData();
            }
            return NewData();
        }

        internal static bool Save(string path, CraftingSaveData data)
        {
            LastError = string.Empty;
            LastRecovery = string.Empty;
            if (string.IsNullOrEmpty(path)) { LastError = "Crafting save path is unavailable."; return false; }
            try
            {
                if (data == null) data = NewData();
                data.Normalize();
                string text = JsonUtility.ToJson(data, true);
                string error;
                if (!AtomicTextSidecar.WriteAtomic(path, text, IsValidCurrentOrLegacyText, out error))
                {
                    LastError = "Crafting progression save failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Crafting progression save failed: " + ex.GetType().Name;
                return false;
            }
        }

        private static bool IsValidCurrentOrLegacyText(string text)
        {
            CraftingSaveData data;
            return TryParse(text, out data) && data.SchemaVersion <= CraftingSaveData.CurrentSchemaVersion;
        }

        private static bool TryParse(string text, out CraftingSaveData data)
        {
            data = null;
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return false;
                data = JsonUtility.FromJson<CraftingSaveData>(text);
                return data != null;
            }
            catch { data = null; return false; }
        }

        private static CraftingSaveData NewData()
        {
            CraftingSaveData data = new CraftingSaveData();
            data.Normalize();
            return data;
        }

        private static bool TryAcquireLegacyClaim(string path, string characterKey)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(characterKey)) return false;
                string parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                if (File.Exists(path))
                {
                    string owner = File.ReadAllText(path, Encoding.UTF8).Trim();
                    return string.Equals(owner, characterKey, StringComparison.Ordinal);
                }
                using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(characterKey);
                    writer.Flush();
                    stream.Flush(true);
                }
                return true;
            }
            catch
            {
                try
                {
                    if (!File.Exists(path)) return false;
                    string owner = File.ReadAllText(path, Encoding.UTF8).Trim();
                    return string.Equals(owner, characterKey, StringComparison.Ordinal);
                }
                catch { return false; }
            }
        }
    }
}
