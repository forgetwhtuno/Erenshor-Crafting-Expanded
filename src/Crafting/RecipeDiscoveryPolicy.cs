using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public interface IRecipeDiscoverySource
    {
        bool HasDiscovery(string discoveryKey);
    }

    internal static class RecipeDiscoveryKeys
    {
        internal const string WildHerb = "wild_herb";
        internal const string CaveMushroom = "cave_mushroom";

        internal static string FromItemId(string itemId)
        {
            if (string.Equals(itemId, CraftingExpandedItemIds.WildHerbId, StringComparison.Ordinal)) return WildHerb;
            if (string.Equals(itemId, CraftingExpandedItemIds.CaveMushroomId, StringComparison.Ordinal)) return CaveMushroom;
            return string.Empty;
        }
    }

    // Pure deterministic helper used by policy tests. Runtime recipe discovery is authoritative
    // through ForagingKnowledge; this class is not persisted.
    internal sealed class MutableRecipeDiscoverySource : IRecipeDiscoverySource
    {
        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);

        public bool HasDiscovery(string discoveryKey)
        {
            return !string.IsNullOrEmpty(discoveryKey) && _known.Contains(discoveryKey);
        }

        internal bool MarkDiscovery(string discoveryKey)
        {
            return !string.IsNullOrEmpty(discoveryKey) && _known.Add(discoveryKey);
        }
    }
}
