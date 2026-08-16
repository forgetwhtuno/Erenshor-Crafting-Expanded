namespace ErenshorCraftingExpanded
{
    // Reserved id namespace for this mod's custom items - see
    // docs/NATIVE_ITEM_REGISTRY_FINDINGS.md section 4 for the collision-avoidance rationale.
    public static class CraftingExpandedItemIds
    {
        public const long RangeStart = 910000000L;
        public const long RangeEnd = 910999999L;
        // Reserved subrange for mod-owned native Smithing Template Items. Production slots use
        // stable ids from ProductionRecipePlan; keeping resource and recipe-template ids disjoint
        // prevents gathered materials from ever being reused as recipe identities.
        public const long RecipeTemplateRangeStart = 910100000L;
        public const long RecipeTemplateRangeEnd = 910199999L;

        public const string WildHerbId = "910000001";
        public const string CaveMushroomId = "910000002";
        public const string WildBloomId = "910000003";
        public const string CaveMossId = "910000004";
        public const string BlightrootId = "910000005";

        public static bool IsInOwnedRange(string id)
        {
            long numeric;
            if (string.IsNullOrEmpty(id) || !long.TryParse(id, out numeric)) return false;
            return numeric >= RangeStart && numeric <= RangeEnd;
        }

        public static bool IsInRecipeTemplateRange(string id)
        {
            long numeric;
            if (string.IsNullOrEmpty(id) || !long.TryParse(id, out numeric)) return false;
            return numeric >= RecipeTemplateRangeStart && numeric <= RecipeTemplateRangeEnd;
        }
    }
}
