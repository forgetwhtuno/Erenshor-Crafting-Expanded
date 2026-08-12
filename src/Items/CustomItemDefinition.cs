namespace ErenshorCraftingExpanded
{
    // Plain data - no Unity/ScriptableObject reference. The actual Item clone is built at
    // runtime by GameItemRegistryApi from this definition; nothing here is persisted directly
    // (registration happens fresh every boot, from code, not from saved definition data).
    public sealed class CustomItemDefinition
    {
        public string Id;
        public string Name;
        public string Lore;
        public int Value;
        public int DefaultGrantQuantity = 1;

        // Documents which base-item predicate produced the clone, for diagnostics only - not
        // used for registration logic itself (see GameItemRegistryApi.FindSafeBaseItem).
        public string BaseItemSelectionNote;
    }
}
