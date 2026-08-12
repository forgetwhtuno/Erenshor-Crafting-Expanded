using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // Mod-owned recipe classification, keyed by Item.Id (the native BaseScriptableObject
    // identity, confirmed in docs/NATIVE_CRAFTING_FINDINGS.md). Deliberately empty by default -
    // the user's spec explicitly says not every native recipe needs manual classification up
    // front, and unknown recipes must fail gracefully rather than crash or guess wildly.
    internal static class RecipeDifficultyCatalog
    {
        private static readonly Dictionary<string, RecipeDifficulty> ById = new Dictionary<string, RecipeDifficulty>();

        internal static RecipeDifficulty Classify(string templateItemId)
        {
            if (string.IsNullOrEmpty(templateItemId)) return RecipeDifficulty.Unclassified;
            RecipeDifficulty difficulty;
            return ById.TryGetValue(templateItemId, out difficulty) ? difficulty : RecipeDifficulty.Unclassified;
        }

        // Exposed for a future data-driven pass (e.g. loaded from a sidecar JSON file) -
        // in-process registration only for v1.
        internal static void Register(string templateItemId, RecipeDifficulty difficulty)
        {
            if (string.IsNullOrEmpty(templateItemId)) return;
            ById[templateItemId] = difficulty;
        }
    }
}
