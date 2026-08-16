namespace ErenshorCraftingExpanded
{
    public static class ExperimentalRecipeDonorPolicy
    {
        public static bool IsEligible(bool isTemplate, bool isSpecialCombine, int distinctIngredientStacks,
            int componentSlots, bool hasReward, bool outputGeneral, bool outputStackable,
            bool outputUnique, bool outputRare, bool outputIsOwnedCustomItem)
        {
            if (!isTemplate || isSpecialCombine || distinctIngredientStacks <= 0 || !hasReward) return false;
            if (componentSlots <= 0 || distinctIngredientStacks + 1 > componentSlots) return false;
            if (!outputGeneral || !outputStackable || outputUnique || outputRare || outputIsOwnedCustomItem) return false;
            return true;
        }

        internal static string RunSelfTests()
        {
            if (!IsEligible(true, false, 2, 4, true, true, true, false, false, false)) return "FAIL valid experimental donor";
            if (IsEligible(true, true, 2, 4, true, true, true, false, false, false)) return "FAIL special combine donor";
            if (IsEligible(true, false, 4, 4, true, true, true, false, false, false)) return "FAIL component capacity donor";
            if (IsEligible(true, false, 2, 4, true, false, true, false, false, false)) return "FAIL non-general output donor";
            if (IsEligible(true, false, 2, 4, true, true, false, false, false, false)) return "FAIL nonstack output donor";
            if (IsEligible(true, false, 2, 4, true, true, true, true, false, false)) return "FAIL unique output donor";
            if (IsEligible(true, false, 2, 4, true, true, true, false, true, false)) return "FAIL rare output donor";
            if (IsEligible(true, false, 2, 4, true, true, true, false, false, true)) return "FAIL owned custom output donor";
            return "PASS experimental recipe donor policy";
        }
    }
}
