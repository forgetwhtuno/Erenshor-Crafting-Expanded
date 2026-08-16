namespace ErenshorCraftingExpanded
{
    public enum CustomItemVisualKind
    {
        OrganicHerb = 0,
        OrganicFungus = 1,
        OrganicFlower = 2,
        OrganicMoss = 3,
        OrganicRoot = 4
    }

    // Plain data - no Unity/ScriptableObject reference. The actual Item clone is built at
    // runtime by GameItemRegistryApi from this definition; nothing here is persisted directly.
    public sealed class CustomItemDefinition
    {
        public string Id;
        public string Name;
        public string Lore;
        public int Value;
        public int DefaultGrantQuantity = 1;
        public CustomItemVisualKind VisualKind = CustomItemVisualKind.OrganicHerb;

        // Documents which base-item predicate produced the clone, for diagnostics only - not
        // used as authority for registration logic itself.
        public string BaseItemSelectionNote;
    }
}
