using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum RecipeDifficulty
    {
        Unclassified = 0,
        Trivial = 1,
        Easy = 2,
        Appropriate = 3,
        Challenging = 4
    }

    // Player-facing profession is Crafting; the authoritative native activity/station remains
    // Smithing. This curve is deliberately action-heavier than the previous linear curve because
    // forge interactions are much faster than world gathering. Levels stay tangible early, while
    // the quadratic term makes 31-50 require sustained material acquisition and recipe variety.
    public static class SmithingXpCurve
    {
        public const int MaxLevel = 50;
        public const int CurrentCurveVersion = 2;

        public static int XpToNextLevel(int level)
        {
            if (level < 1) level = 1;
            if (level >= MaxLevel) return 0;
            int n = level - 1;
            return 100 + (10 * n) + ((n * n) / 3);
        }

        // Schema <=3 used this linear curve. Kept only for deterministic sidecar migration.
        public static int LegacyXpToNextLevel(int level)
        {
            if (level < 1) level = 1;
            if (level >= MaxLevel) return 0;
            return 80 + (level - 1) * 15;
        }

        public static int TotalXpToLevel(int level)
        {
            if (level <= 1) return 0;
            if (level > MaxLevel) level = MaxLevel;
            int total = 0;
            for (int i = 1; i < level; i++) total += XpToNextLevel(i);
            return total;
        }

        // Used for bounded commission/native classifications. Custom recipes use
        // RecipeProgressionPolicy because their explicit minimum level provides better evidence.
        public static int XpForDifficulty(RecipeDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RecipeDifficulty.Trivial: return 0;
                case RecipeDifficulty.Easy: return 4;
                case RecipeDifficulty.Appropriate: return 8;
                case RecipeDifficulty.Challenging: return 12;
                case RecipeDifficulty.Unclassified: return 6;
                default: return 6;
            }
        }
    }

    [Serializable]
    public sealed class CraftingRecipePracticeRecord
    {
        public string TemplateItemId = string.Empty;
        public int SuccessfulCrafts;
    }

    public static class CraftingProgressionMigrationPolicy
    {
        public static int PreserveInLevelProgress(int level, int oldXp, int oldNeed, int newNeed)
        {
            if (level >= SmithingXpCurve.MaxLevel || newNeed <= 0) return 0;
            if (oldXp <= 0 || oldNeed <= 0) return 0;
            if (oldXp >= oldNeed) oldXp = oldNeed - 1;
            long scaled = ((long)oldXp * (long)newNeed + (oldNeed / 2)) / oldNeed;
            if (scaled < 0) return 0;
            if (scaled >= newNeed) return newNeed - 1;
            return (int)scaled;
        }

        internal static string RunSelfTests()
        {
            if (PreserveInLevelProgress(10, 107, 215, 217) != 108) return "FAIL crafting migration ratio";
            if (PreserveInLevelProgress(50, 999, 800, 0) != 0) return "FAIL crafting migration cap";
            if (PreserveInLevelProgress(8, -5, 185, 196) != 0) return "FAIL crafting migration malformed low xp";
            if (PreserveInLevelProgress(8, 9999, 185, 196) != 195) return "FAIL crafting migration malformed high xp";
            return "PASS crafting progression migration policy";
        }
    }

    [Serializable]
    public sealed class CraftingProgress
    {
        private const int MaxPracticeRecords = 256;
        private const int MaxRecordedCraftsPerTemplate = 1000000;

        public string Profession = "Crafting";
        public int Level = 1;
        public int Xp = 0;
        public List<CraftingRecipePracticeRecord> RecipePractice = new List<CraftingRecipePracticeRecord>();

        public void Normalize()
        {
            Profession = "Crafting";
            if (Level < 1) Level = 1;
            if (Level > SmithingXpCurve.MaxLevel) Level = SmithingXpCurve.MaxLevel;
            if (Level >= SmithingXpCurve.MaxLevel) { Xp = 0; }
            else
            {
                int next = SmithingXpCurve.XpToNextLevel(Level);
                if (Xp < 0) Xp = 0;
                if (next > 0 && Xp >= next) Xp = next - 1;
            }

            if (RecipePractice == null) RecipePractice = new List<CraftingRecipePracticeRecord>();
            List<CraftingRecipePracticeRecord> clean = new List<CraftingRecipePracticeRecord>();
            Dictionary<string, int> indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < RecipePractice.Count && clean.Count < MaxPracticeRecords; i++)
            {
                CraftingRecipePracticeRecord record = RecipePractice[i];
                if (record == null) continue;
                string id = (record.TemplateItemId ?? string.Empty).Trim();
                if (id.Length == 0) continue;
                int count = record.SuccessfulCrafts;
                if (count < 0) count = 0;
                if (count > MaxRecordedCraftsPerTemplate) count = MaxRecordedCraftsPerTemplate;
                int existingIndex;
                if (indexById.TryGetValue(id, out existingIndex))
                {
                    long combined = (long)clean[existingIndex].SuccessfulCrafts + count;
                    clean[existingIndex].SuccessfulCrafts = combined > MaxRecordedCraftsPerTemplate ? MaxRecordedCraftsPerTemplate : (int)combined;
                }
                else
                {
                    indexById.Add(id, clean.Count);
                    clean.Add(new CraftingRecipePracticeRecord { TemplateItemId = id, SuccessfulCrafts = count });
                }
            }
            RecipePractice = clean;
        }

        public int GetSuccessfulCraftCount(string templateItemId)
        {
            if (string.IsNullOrEmpty(templateItemId) || RecipePractice == null) return 0;
            for (int i = 0; i < RecipePractice.Count; i++)
            {
                CraftingRecipePracticeRecord record = RecipePractice[i];
                if (record != null && string.Equals(record.TemplateItemId, templateItemId, StringComparison.Ordinal))
                    return record.SuccessfulCrafts < 0 ? 0 : record.SuccessfulCrafts;
            }
            // Fail closed once the bounded ledger is full: an untracked template must not regain
            // first-craft/native-mastery XP forever simply because the sidecar reached its cap.
            return RecipePractice.Count >= MaxPracticeRecords ? 100 : 0;
        }

        public int RecordSuccessfulCraft(string templateItemId)
        {
            if (string.IsNullOrEmpty(templateItemId)) return 0;
            Normalize();
            for (int i = 0; i < RecipePractice.Count; i++)
            {
                CraftingRecipePracticeRecord record = RecipePractice[i];
                if (record == null || !string.Equals(record.TemplateItemId, templateItemId, StringComparison.Ordinal)) continue;
                if (record.SuccessfulCrafts < MaxRecordedCraftsPerTemplate) record.SuccessfulCrafts++;
                return record.SuccessfulCrafts;
            }
            if (RecipePractice.Count >= MaxPracticeRecords) return 0;
            CraftingRecipePracticeRecord created = new CraftingRecipePracticeRecord { TemplateItemId = templateItemId, SuccessfulCrafts = 1 };
            RecipePractice.Add(created);
            return created.SuccessfulCrafts;
        }

        public int AwardXp(RecipeDifficulty difficulty) { return AwardRawXp(SmithingXpCurve.XpForDifficulty(difficulty)); }

        public int AwardRawXp(int amount)
        {
            Normalize();
            if (amount <= 0 || Level >= SmithingXpCurve.MaxLevel) return 0;
            Xp += amount;
            while (Level < SmithingXpCurve.MaxLevel)
            {
                int need = SmithingXpCurve.XpToNextLevel(Level);
                if (need <= 0 || Xp < need) break;
                Xp -= need;
                Level++;
            }
            if (Level >= SmithingXpCurve.MaxLevel) Xp = 0;
            return amount;
        }

        internal static string RunSelfTests()
        {
            for (int level = 1; level < SmithingXpCurve.MaxLevel - 1; level++)
                if (SmithingXpCurve.XpToNextLevel(level + 1) <= SmithingXpCurve.XpToNextLevel(level)) return "FAIL crafting curve monotonicity";
            if (SmithingXpCurve.XpToNextLevel(1) != 100 || SmithingXpCurve.XpToNextLevel(10) != 217) return "FAIL crafting curve anchors";
            if (SmithingXpCurve.TotalXpToLevel(50) != 29324) return "FAIL 1-50 curve total";
            if (SmithingXpCurve.XpToNextLevel(50) != 0) return "FAIL crafting max curve";

            CraftingProgress p = new CraftingProgress();
            if (p.AwardRawXp(14) != 14 || p.Xp != 14 || p.Level != 1) return "FAIL single successful craft";
            CraftingProgress boundary = new CraftingProgress { Xp = 95 };
            if (boundary.AwardRawXp(14) != 14 || boundary.Level != 2 || boundary.Xp != 9) return "FAIL level-up carry";
            CraftingProgress multi = new CraftingProgress();
            if (multi.AwardRawXp(250) != 250 || multi.Level != 3 || multi.Xp != 40) return "FAIL multi-level carry";
            CraftingProgress capped = new CraftingProgress { Level = SmithingXpCurve.MaxLevel, Xp = 0 };
            if (capped.AwardRawXp(30) != 0) return "FAIL max level gate";

            CraftingProgress corrupt = new CraftingProgress { Profession = "Other", Level = -7, Xp = -20 };
            corrupt.RecipePractice.Add(new CraftingRecipePracticeRecord { TemplateItemId = " template.a ", SuccessfulCrafts = -4 });
            corrupt.RecipePractice.Add(new CraftingRecipePracticeRecord { TemplateItemId = "template.a", SuccessfulCrafts = 3 });
            corrupt.Normalize();
            if (corrupt.Profession != "Crafting" || corrupt.Level != 1 || corrupt.Xp != 0) return "FAIL progression normalization";
            if (corrupt.RecipePractice.Count != 1 || corrupt.RecipePractice[0].SuccessfulCrafts != 3) return "FAIL practice normalization";
            if (corrupt.GetSuccessfulCraftCount("template.a") != 3) return "FAIL practice lookup";
            if (corrupt.RecordSuccessfulCraft("template.a") != 4 || corrupt.GetSuccessfulCraftCount("template.a") != 4) return "FAIL practice increment";
            if (corrupt.RecordSuccessfulCraft("template.b") != 1) return "FAIL practice new template";

            CraftingProgress fullLedger = new CraftingProgress();
            for (int i = 0; i < MaxPracticeRecords; i++)
                fullLedger.RecipePractice.Add(new CraftingRecipePracticeRecord { TemplateItemId = "template." + i.ToString(), SuccessfulCrafts = 1 });
            fullLedger.Normalize();
            if (fullLedger.GetSuccessfulCraftCount("overflow.template") != 100) return "FAIL full practice ledger did not fail closed";

            return "PASS crafting progression";
        }
    }
}
