using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ErenshorCraftingExpanded
{
    public enum KnownRecipeDocumentLoadState
    {
        NewDocument = 0,
        Loaded = 1,
        MigratedLegacyV0 = 2,
        UnsupportedVersion = 3,
        Malformed = 4,
        IoFailure = 5
    }

    public sealed class KnownRecipeDocument
    {
        public const int CurrentVersion = 1;
        private readonly Dictionary<string, KnownRecipeLedger> _characters = new Dictionary<string, KnownRecipeLedger>(StringComparer.Ordinal);

        public KnownRecipeDocumentLoadState LoadState = KnownRecipeDocumentLoadState.NewDocument;
        public bool HasWarnings;
        public string Warning = string.Empty;

        public KnownRecipeLedger GetOrCreateCharacter(string hashedCharacterKey)
        {
            if (string.IsNullOrEmpty(hashedCharacterKey)) return null;
            KnownRecipeLedger ledger;
            if (!_characters.TryGetValue(hashedCharacterKey, out ledger))
            {
                ledger = new KnownRecipeLedger();
                _characters.Add(hashedCharacterKey, ledger);
            }
            return ledger;
        }

        public KnownRecipeLedger GetCharacter(string hashedCharacterKey)
        {
            KnownRecipeLedger ledger;
            return !string.IsNullOrEmpty(hashedCharacterKey) && _characters.TryGetValue(hashedCharacterKey, out ledger) ? ledger : null;
        }

        public List<string> CharacterKeys()
        {
            List<string> keys = new List<string>(_characters.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }
    }

    public static class RecipeCharacterIdentityKey
    {
        public static string HashStableIdentity(string stableIdentity)
        {
            if (string.IsNullOrEmpty(stableIdentity)) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(stableIdentity);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        internal static string RunSelfTests()
        {
            string a = HashStableIdentity("character-a");
            string b = HashStableIdentity("character-b");
            if (a.Length != 64 || b.Length != 64 || a == b) return "FAIL character identity hashing";
            if (HashStableIdentity("character-a") != a) return "FAIL character identity hash stability";
            if (HashStableIdentity(string.Empty) != string.Empty) return "FAIL empty identity hash";
            return "PASS recipe character identity key";
        }
    }

    public static class KnownRecipeLedgerCodec
    {
        private const string Header = "ECE_RECIPE_KNOWLEDGE|1";
        private const string LegacyHeader = "ECE_RECIPE_KNOWLEDGE|0";

        public static string Serialize(KnownRecipeDocument document)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine(Header);
            if (document == null) return output.ToString();
            List<string> characters = document.CharacterKeys();
            for (int i = 0; i < characters.Count; i++)
            {
                string key = characters[i];
                KnownRecipeLedger ledger = document.GetCharacter(key);
                if (ledger == null) continue;
                output.Append("C|").Append(key).AppendLine();
                List<KnownRecipeRecord> records = ledger.Snapshot();
                for (int j = 0; j < records.Count; j++)
                {
                    KnownRecipeRecord record = records[j];
                    output.Append("R|")
                        .Append(Encode(record.StableRecipeId)).Append('|')
                        .Append(record.LearnedUtcTicks.ToString()).Append('|')
                        .Append(record.PendingTemplateEntitlements.ToString()).Append('|')
                        .Append(record.LastManualRestoreUtcTicks.ToString()).AppendLine();
                }
                output.AppendLine("E");
            }
            return output.ToString();
        }

        public static KnownRecipeDocument Deserialize(string text)
        {
            KnownRecipeDocument document = new KnownRecipeDocument();
            if (string.IsNullOrWhiteSpace(text)) return document;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length == 0) return document;
            string first = (lines[0] ?? string.Empty).Trim();
            if (first == LegacyHeader) return DeserializeLegacyV0(lines);
            if (first != Header)
            {
                document.LoadState = KnownRecipeDocumentLoadState.UnsupportedVersion;
                document.Warning = "Known recipe sidecar version is not supported; file left untouched.";
                return document;
            }

            document.LoadState = KnownRecipeDocumentLoadState.Loaded;
            KnownRecipeLedger current = null;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = (lines[i] ?? string.Empty).Trim();
                if (line.Length == 0) continue;
                if (line == "E") { current = null; continue; }
                if (line.StartsWith("C|", StringComparison.Ordinal))
                {
                    string key = line.Substring(2);
                    if (!IsHashedCharacterKey(key))
                    {
                        MarkWarning(document, "Malformed character record skipped.");
                        current = null;
                        continue;
                    }
                    current = document.GetOrCreateCharacter(key);
                    continue;
                }
                if (line.StartsWith("R|", StringComparison.Ordinal))
                {
                    if (current == null)
                    {
                        MarkWarning(document, "Recipe record outside character section skipped.");
                        continue;
                    }
                    string[] parts = line.Split('|');
                    if (parts.Length != 5)
                    {
                        MarkWarning(document, "Malformed recipe record skipped.");
                        continue;
                    }
                    string recipeId = Decode(parts[1]);
                    long learned;
                    int pending;
                    long lastRestore;
                    if (string.IsNullOrEmpty(recipeId) || !long.TryParse(parts[2], out learned) || !int.TryParse(parts[3], out pending) || !long.TryParse(parts[4], out lastRestore))
                    {
                        MarkWarning(document, "Malformed recipe fields skipped.");
                        continue;
                    }
                    KnownRecipeRecord record = new KnownRecipeRecord
                    {
                        StableRecipeId = recipeId,
                        LearnedUtcTicks = learned,
                        PendingTemplateEntitlements = pending,
                        LastManualRestoreUtcTicks = lastRestore
                    };
                    record.Normalize();
                    if (current.IsKnown(recipeId))
                    {
                        MarkWarning(document, "Duplicate recipe record skipped.");
                        continue;
                    }
                    current.ReplaceWith(Append(current.Snapshot(), record));
                    continue;
                }
                MarkWarning(document, "Unknown sidecar record skipped.");
            }
            return document;
        }

        private static KnownRecipeDocument DeserializeLegacyV0(string[] lines)
        {
            KnownRecipeDocument document = new KnownRecipeDocument();
            document.LoadState = KnownRecipeDocumentLoadState.MigratedLegacyV0;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = (lines[i] ?? string.Empty).Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split('|');
                if (parts.Length != 3 || parts[0] != "K" || !IsHashedCharacterKey(parts[1]))
                {
                    MarkWarning(document, "Malformed legacy recipe record skipped.");
                    continue;
                }
                string recipeId = Decode(parts[2]);
                if (string.IsNullOrEmpty(recipeId))
                {
                    MarkWarning(document, "Malformed legacy recipe id skipped.");
                    continue;
                }
                KnownRecipeLedger ledger = document.GetOrCreateCharacter(parts[1]);
                if (ledger != null) ledger.ImportKnown(recipeId, 0);
            }
            return document;
        }

        private static List<KnownRecipeRecord> Append(List<KnownRecipeRecord> records, KnownRecipeRecord value)
        {
            records.Add(value);
            return records;
        }

        private static void MarkWarning(KnownRecipeDocument document, string warning)
        {
            document.HasWarnings = true;
            if (string.IsNullOrEmpty(document.Warning)) document.Warning = warning;
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty)); }
            catch { return string.Empty; }
        }

        private static bool IsHashedCharacterKey(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        internal static string RunSelfTests()
        {
            string a = RecipeCharacterIdentityKey.HashStableIdentity("alpha");
            string b = RecipeCharacterIdentityKey.HashStableIdentity("beta");
            KnownRecipeDocument document = new KnownRecipeDocument();
            KnownRecipeLedger alpha = document.GetOrCreateCharacter(a);
            KnownRecipeLedger beta = document.GetOrCreateCharacter(b);
            alpha.LearnNew("recipe.one", 100);
            alpha.ConsumeTemplateEntitlement("recipe.one");
            beta.ImportKnown("recipe.two", 200);
            beta.MarkVerifiedTemplateConsumed("recipe.two", 210);

            string encoded = Serialize(document);
            if (encoded.IndexOf("alpha", StringComparison.Ordinal) >= 0 || encoded.IndexOf("beta", StringComparison.Ordinal) >= 0) return "FAIL raw identity leaked to sidecar";
            KnownRecipeDocument loaded = Deserialize(encoded);
            if (loaded.LoadState != KnownRecipeDocumentLoadState.Loaded) return "FAIL v1 load state";
            if (loaded.GetCharacter(a) == null || !loaded.GetCharacter(a).IsKnown("recipe.one")) return "FAIL character A isolation";
            if (loaded.GetCharacter(a).IsKnown("recipe.two")) return "FAIL character A cross-talk";
            if (loaded.GetCharacter(b) == null || !loaded.GetCharacter(b).IsKnown("recipe.two")) return "FAIL character B isolation";
            if (loaded.GetCharacter(b).Get("recipe.two").PendingTemplateEntitlements != 1) return "FAIL entitlement persistence";

            string legacy = LegacyHeader + "\nK|" + a + "|" + Encode("recipe.legacy") + "\n";
            KnownRecipeDocument migrated = Deserialize(legacy);
            if (migrated.LoadState != KnownRecipeDocumentLoadState.MigratedLegacyV0 || !migrated.GetCharacter(a).IsKnown("recipe.legacy")) return "FAIL v0 migration";
            if (migrated.GetCharacter(a).Get("recipe.legacy").PendingTemplateEntitlements != 0) return "FAIL migration created blind template entitlement";

            KnownRecipeDocument malformed = Deserialize(Header + "\nC|not-a-hash\nR|bad|x|y|z\nC|" + a + "\nR|%%%|0|0|0\nR|" + Encode("recipe.good") + "|1|999|2\nE\n");
            if (!malformed.HasWarnings) return "FAIL malformed file warning";
            KnownRecipeRecord good = malformed.GetCharacter(a) == null ? null : malformed.GetCharacter(a).Get("recipe.good");
            if (good == null || good.PendingTemplateEntitlements != KnownRecipeLedger.MaximumPendingTemplateEntitlements) return "FAIL malformed safe normalization";

            KnownRecipeDocument unsupported = Deserialize("ECE_RECIPE_KNOWLEDGE|99\n");
            if (unsupported.LoadState != KnownRecipeDocumentLoadState.UnsupportedVersion) return "FAIL future version fail closed";
            return "PASS known recipe persistence codec";
        }
    }

    internal static class KnownRecipeStore
    {
        internal static string LastError = string.Empty;
        internal static string LastRecovery = string.Empty;

        internal static KnownRecipeDocument Load(string path)
        {
            LastError = string.Empty;
            LastRecovery = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(path)) return new KnownRecipeDocument();

                // Newer-version primaries are authority. Do not "recover" an older backup and
                // accidentally downgrade/overwrite knowledge produced by a newer plugin build.
                string primaryText;
                string primaryError;
                if (File.Exists(path) && AtomicTextSidecar.TryReadPrimary(path, out primaryText, out primaryError))
                {
                    KnownRecipeDocument primary = KnownRecipeLedgerCodec.Deserialize(primaryText);
                    if (primary.LoadState == KnownRecipeDocumentLoadState.UnsupportedVersion) return primary;
                }

                string text;
                string recoveryError;
                SidecarRecoverySource source;
                if (!AtomicTextSidecar.TryLoadNewestValid(path, IsRecoverableText, out text, out source, out recoveryError))
                {
                    if (!AtomicTextSidecar.HasAnyCandidate(path)) return new KnownRecipeDocument();
                    LastError = string.IsNullOrEmpty(recoveryError)
                        ? "Known recipe sidecar had no valid recovery candidate."
                        : "Known recipe recovery failed: " + recoveryError;
                    KnownRecipeDocument failed = new KnownRecipeDocument();
                    failed.LoadState = KnownRecipeDocumentLoadState.IoFailure;
                    failed.Warning = LastError;
                    return failed;
                }

                KnownRecipeDocument document = KnownRecipeLedgerCodec.Deserialize(text);
                if (source != SidecarRecoverySource.Primary) LastRecovery = source.ToString().ToLowerInvariant();
                return document;
            }
            catch (Exception ex)
            {
                LastError = "Known recipe load failed: " + ex.GetType().Name + ": " + ex.Message;
                KnownRecipeDocument document = new KnownRecipeDocument();
                document.LoadState = KnownRecipeDocumentLoadState.IoFailure;
                document.Warning = LastError;
                return document;
            }
        }

        internal static bool Save(string path, KnownRecipeDocument document)
        {
            LastError = string.Empty;
            LastRecovery = string.Empty;
            if (document != null && document.LoadState == KnownRecipeDocumentLoadState.UnsupportedVersion)
            {
                LastError = "Known recipe sidecar has a newer unsupported version; refusing to overwrite it.";
                return false;
            }
            if (string.IsNullOrEmpty(path)) { LastError = "Known recipe save path unavailable."; return false; }
            try
            {
                string error;
                if (!AtomicTextSidecar.WriteAtomic(path, KnownRecipeLedgerCodec.Serialize(document), IsRecoverableText, out error))
                {
                    LastError = "Known recipe save failed: " + (string.IsNullOrEmpty(error) ? "unknown error" : error);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Known recipe save failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool IsRecoverableText(string text)
        {
            KnownRecipeDocument document = KnownRecipeLedgerCodec.Deserialize(text);
            return document.LoadState == KnownRecipeDocumentLoadState.Loaded ||
                document.LoadState == KnownRecipeDocumentLoadState.MigratedLegacyV0;
        }

        internal static string RunSelfTests()
        {
            string root = Path.Combine(Path.GetTempPath(), "ece-known-recipe-tests-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(root, "known-recipes.v1.txt");
            try
            {
                KnownRecipeDocument document = new KnownRecipeDocument();
                string character = RecipeCharacterIdentityKey.HashStableIdentity("store-test-character");
                document.GetOrCreateCharacter(character).LearnNew("recipe.saved", 123);
                if (!Save(path, document)) return "FAIL known recipe atomic save: " + LastError;
                KnownRecipeDocument loaded = Load(path);
                KnownRecipeLedger ledger = loaded.GetCharacter(character);
                if (ledger == null || !ledger.IsKnown("recipe.saved")) return "FAIL known recipe file load";

                // Crash-after-temp-write recovery must preserve newly learned knowledge.
                KnownRecipeDocument newer = KnownRecipeLedgerCodec.Deserialize(KnownRecipeLedgerCodec.Serialize(document));
                newer.GetOrCreateCharacter(character).LearnNew("recipe.temp", 124);
                File.WriteAllText(path + ".tmp", KnownRecipeLedgerCodec.Serialize(newer), Encoding.UTF8);
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(path + ".tmp", new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));
                KnownRecipeDocument recovered = Load(path);
                KnownRecipeLedger recoveredLedger = recovered.GetCharacter(character);
                if (recoveredLedger == null || !recoveredLedger.IsKnown("recipe.temp") || LastRecovery != "temp")
                    return "FAIL known recipe temp recovery";

                File.WriteAllText(path, "ECE_RECIPE_KNOWLEDGE|99\n", Encoding.UTF8);
                KnownRecipeDocument future = Load(path);
                if (future.LoadState != KnownRecipeDocumentLoadState.UnsupportedVersion) return "FAIL future sidecar load";
                if (Save(path, future)) return "FAIL future sidecar overwritten";
                string untouched = File.ReadAllText(path, Encoding.UTF8);
                if (untouched.IndexOf("|99", StringComparison.Ordinal) < 0) return "FAIL future sidecar changed";
                return "PASS known recipe store";
            }
            catch (Exception ex) { return "FAIL known recipe store exception " + ex.GetType().Name; }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }
    }
}
