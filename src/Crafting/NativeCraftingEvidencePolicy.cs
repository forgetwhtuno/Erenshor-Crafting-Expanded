namespace ErenshorCraftingExpanded
{
    // Pure policy for deciding whether the runtime native-recipe shape probe is strong enough to
    // justify further recipe-registration experiments. It does NOT authorize mutation by itself.
    public static class NativeCraftingEvidencePolicy
    {
        public static bool IsRecipeShapeSupported(
            bool smithingCombine,
            bool smithingDoSuccess,
            bool smithingTemplateSlot,
            bool smithingFuelSlot,
            bool smithingComponents,
            bool itemTemplateFlag,
            bool itemTemplateIngredients,
            bool itemTemplateRewards,
            bool itemIconQuickSmith,
            int ordinaryTemplateCount)
        {
            return smithingCombine && smithingDoSuccess && smithingTemplateSlot && smithingFuelSlot &&
                smithingComponents && itemTemplateFlag && itemTemplateIngredients && itemTemplateRewards &&
                itemIconQuickSmith && ordinaryTemplateCount > 0;
        }

        internal static string RunSelfTests()
        {
            if (!IsRecipeShapeSupported(true, true, true, true, true, true, true, true, true, 1))
                return "FAIL complete native recipe shape rejected";
            if (IsRecipeShapeSupported(false, true, true, true, true, true, true, true, true, 1))
                return "FAIL missing Combine accepted";
            if (IsRecipeShapeSupported(true, true, true, true, true, true, true, true, true, 0))
                return "FAIL recipe shape accepted without any ordinary native template exemplar";
            return "PASS native crafting evidence policy";
        }
    }
}
