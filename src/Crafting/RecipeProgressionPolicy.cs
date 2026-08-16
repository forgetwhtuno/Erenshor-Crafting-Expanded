namespace ErenshorCraftingExpanded
{
    public static class RecipeProgressionPolicy
    {
        // Explicit-level recipes naturally become trivial as Crafting outlevels them. This is the
        // preferred advancement path because it needs no guesses about native item value or power.
        public static int XpForSuccessfulRecipe(int playerLevel, int recipeLevel)
        {
            return XpForSuccessfulRecipe(playerLevel, recipeLevel, 0);
        }

        public static int XpForSuccessfulRecipe(int playerLevel, int recipeLevel, int priorSuccessfulCrafts)
        {
            if (playerLevel < 1) playerLevel = 1;
            if (playerLevel > SmithingXpCurve.MaxLevel) playerLevel = SmithingXpCurve.MaxLevel;
            if (recipeLevel < 1) recipeLevel = 1;
            if (recipeLevel > SmithingXpCurve.MaxLevel) recipeLevel = SmithingXpCurve.MaxLevel;
            if (playerLevel >= SmithingXpCurve.MaxLevel) return 0;

            int delta = recipeLevel - playerLevel;
            int baseXp;
            if (delta >= 5) baseXp = 20;
            else if (delta >= 1) baseXp = 17;
            else if (delta >= -2) baseXp = 14;
            else if (delta >= -5) baseXp = 7;
            else if (delta >= -8) baseXp = 2;
            else baseXp = 0;

            // First successful use of a newly learned explicit recipe receives a small knowledge
            // bonus. There is no lifetime repeat cap here because the level delta itself decays to
            // zero as the player outgrows the recipe.
            if (baseXp > 0 && priorSuccessfulCrafts <= 0) baseXp += FirstCraftBonus(baseXp);
            return baseXp;
        }

        // Native recipes currently have no verified profession-level metadata in the supplied
        // evidence. Static difficulty therefore receives a bounded per-template mastery budget:
        // repeated crafting tapers to zero after 100 successes. This prevents one cheap unknown
        // native template from training Crafting forever without inventing native item power.
        public static int XpForNativeRecipe(int playerLevel, RecipeDifficulty difficulty, int priorSuccessfulCrafts)
        {
            if (playerLevel < 1) playerLevel = 1;
            if (playerLevel >= SmithingXpCurve.MaxLevel) return 0;
            if (priorSuccessfulCrafts < 0) priorSuccessfulCrafts = 0;
            int baseXp = SmithingXpCurve.XpForDifficulty(difficulty);
            if (baseXp <= 0 || priorSuccessfulCrafts >= 100) return 0;

            if (priorSuccessfulCrafts == 0) return baseXp + FirstCraftBonus(baseXp);
            if (priorSuccessfulCrafts < 10) return baseXp;
            if (priorSuccessfulCrafts < 25) return ScaleCeiling(baseXp, 3, 4);
            if (priorSuccessfulCrafts < 50) return ScaleCeiling(baseXp, 1, 2);
            return ScaleCeiling(baseXp, 1, 4);
        }

        private static int FirstCraftBonus(int baseXp)
        {
            return ScaleCeiling(baseXp, 1, 2);
        }

        private static int ScaleCeiling(int value, int numerator, int denominator)
        {
            if (value <= 0 || numerator <= 0 || denominator <= 0) return 0;
            return (value * numerator + denominator - 1) / denominator;
        }

        internal static string RunSelfTests()
        {
            if (XpForSuccessfulRecipe(10, 15, 1) != 20) return "FAIL challenging recipe XP";
            if (XpForSuccessfulRecipe(10, 11, 1) != 17) return "FAIL higher recipe XP";
            if (XpForSuccessfulRecipe(10, 10, 1) != 14) return "FAIL at-level recipe XP";
            if (XpForSuccessfulRecipe(10, 5, 1) != 7) return "FAIL easy recipe XP";
            if (XpForSuccessfulRecipe(10, 2, 1) != 2) return "FAIL very easy recipe XP";
            if (XpForSuccessfulRecipe(10, 1, 1) != 0) return "FAIL trivial recipe XP";
            if (XpForSuccessfulRecipe(10, 10, 0) != 21) return "FAIL first craft knowledge bonus";
            if (XpForSuccessfulRecipe(50, 50, 0) != 0) return "FAIL level cap XP";

            if (XpForNativeRecipe(10, RecipeDifficulty.Unclassified, 0) != 9) return "FAIL unclassified first craft XP";
            if (XpForNativeRecipe(10, RecipeDifficulty.Unclassified, 1) != 6) return "FAIL unclassified normal XP";
            if (XpForNativeRecipe(10, RecipeDifficulty.Unclassified, 10) != 5) return "FAIL native mastery 75 percent";
            if (XpForNativeRecipe(10, RecipeDifficulty.Unclassified, 25) != 3) return "FAIL native mastery 50 percent";
            if (XpForNativeRecipe(10, RecipeDifficulty.Unclassified, 50) != 2) return "FAIL native mastery 25 percent";
            if (XpForNativeRecipe(10, RecipeDifficulty.Unclassified, 100) != 0) return "FAIL native mastery cap";
            if (XpForNativeRecipe(10, RecipeDifficulty.Trivial, 0) != 0) return "FAIL native trivial XP";
            return "PASS recipe progression policy";
        }
    }
}
