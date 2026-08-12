using System;

namespace ErenshorCraftingExpanded
{
    // Mod-owned classification of how appropriate a recipe is for the player's current level.
    // Recipes this mod hasn't classified default to Unclassified, which awards minimal XP -
    // per the user's "fail gracefully" instruction, never zero-and-never-crashing.
    public enum RecipeDifficulty
    {
        Unclassified = 0,
        Trivial = 1,
        Easy = 2,
        Appropriate = 3,
        Challenging = 4
    }

    // Deterministic, mod-owned XP curve. Kept as plain data/formula in one place rather than
    // scattered across UI code, per the user's architecture instruction.
    public static class SmithingXpCurve
    {
        // Level N requires this much cumulative XP to complete (index 0 unused, level 1 starts at 0).
        public const int MaxLevel = 50;

        public static int XpToNextLevel(int level)
        {
            if (level < 1) level = 1;
            if (level >= MaxLevel) return 0;
            return 100 + (level - 1) * 50;
        }

        public static int XpForDifficulty(RecipeDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RecipeDifficulty.Trivial: return 0;
                case RecipeDifficulty.Easy: return 5;
                case RecipeDifficulty.Appropriate: return 15;
                case RecipeDifficulty.Challenging: return 30;
                case RecipeDifficulty.Unclassified: return 5;
                default: return 5;
            }
        }
    }

    [Serializable]
    public sealed class CraftingProgress
    {
        public string Profession = "Smithing";
        public int Level = 1;
        public int Xp = 0;

        // Returns the amount of XP actually applied (0 if the craft was too trivial for the
        // player's level, per the "cheapest item spam" mitigation). Deterministic and pure -
        // no game calls, safe to unit test.
        public int AwardXp(RecipeDifficulty difficulty)
        {
            int amount = SmithingXpCurve.XpForDifficulty(difficulty);
            if (amount <= 0) return 0;
            if (Level >= SmithingXpCurve.MaxLevel) return 0;

            Xp += amount;
            while (Level < SmithingXpCurve.MaxLevel)
            {
                int need = SmithingXpCurve.XpToNextLevel(Level);
                if (need <= 0 || Xp < need) break;
                Xp -= need;
                Level++;
            }
            return amount;
        }

        internal static string RunSelfTests()
        {
            CraftingProgress p = new CraftingProgress();
            int gained = p.AwardXp(RecipeDifficulty.Appropriate);
            if (gained != 15 || p.Xp != 15 || p.Level != 1) return "FAIL single successful craft";

            CraftingProgress trivial = new CraftingProgress();
            int trivialGain = trivial.AwardXp(RecipeDifficulty.Trivial);
            if (trivialGain != 0 || trivial.Xp != 0) return "FAIL trivial reduction";

            CraftingProgress boundary = new CraftingProgress { Xp = 95 };
            int boundaryGain = boundary.AwardXp(RecipeDifficulty.Appropriate); // +15 crosses 100 needed for level 1->2
            if (boundaryGain != 15 || boundary.Level != 2 || boundary.Xp != 10) return "FAIL level-up boundary";

            CraftingProgress multi = new CraftingProgress { Xp = 0, Level = 1 };
            multi.AwardXp(RecipeDifficulty.Challenging); // +30, need 100, no level
            for (int i = 0; i < 10; i++) multi.AwardXp(RecipeDifficulty.Challenging);
            if (multi.Level <= 1) return "FAIL multiple-level gain";

            CraftingProgress capped = new CraftingProgress { Level = SmithingXpCurve.MaxLevel, Xp = 0 };
            if (capped.AwardXp(RecipeDifficulty.Challenging) != 0) return "FAIL max level gate";

            return "PASS crafting progression";
        }
    }
}
