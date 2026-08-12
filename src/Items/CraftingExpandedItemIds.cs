namespace ErenshorCraftingExpanded
{
    // Reserved id namespace for this mod's custom items - see
    // docs/NATIVE_ITEM_REGISTRY_FINDINGS.md section 4 for why this specific range was chosen
    // (deliberately not cammaron/Arcanism's 90000000+ block, and well above any vanilla id
    // observed in this or the crafting research pass).
    public static class CraftingExpandedItemIds
    {
        public const long RangeStart = 910000000L;
        public const long RangeEnd = 910999999L;

        public const string WildHerbId = "910000001";

        public static bool IsInOwnedRange(string id)
        {
            long numeric;
            if (string.IsNullOrEmpty(id) || !long.TryParse(id, out numeric)) return false;
            return numeric >= RangeStart && numeric <= RangeEnd;
        }
    }
}
