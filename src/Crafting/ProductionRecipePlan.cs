using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum ProductionRecipeContentKind
    {
        Foundation = 0,
        ActivatedUtility = 1
    }

    public sealed class ProductionRecipePlanEntry
    {
        public string RecipeKey = string.Empty;
        public string TemplateItemId = string.Empty;
        public string DisplayPrefix = string.Empty;
        public int MinimumCraftingLevel = 1;
        public ProductionRecipeContentKind ContentKind;
        public int WildHerbQuantity;
        public bool AddExtraNativeIngredient;
        public bool RequiresWildHerbDiscovery;
        public int TierOrdinal;
    }

    // Stable player-facing recipe slots. Native donor/output identities are bound separately and
    // persisted only after the current ItemDB proves them. This gives saves stable mod recipe ids
    // without hard-coding an output that this source snapshot cannot verify.
    public static class ProductionRecipePlan
    {
        private static readonly List<ProductionRecipePlanEntry> Entries = Build();
        private static readonly IList<ProductionRecipePlanEntry> ReadOnlyEntries = Entries.AsReadOnly();

        public static IList<ProductionRecipePlanEntry> All { get { return ReadOnlyEntries; } }

        public static ProductionRecipePlanEntry Get(string recipeKey)
        {
            if (string.IsNullOrEmpty(recipeKey)) return null;
            for (int i = 0; i < Entries.Count; i++)
                if (string.Equals(Entries[i].RecipeKey, recipeKey, StringComparison.Ordinal)) return Entries[i];
            return null;
        }

        public static ProductionRecipePlanEntry GetByTemplateId(string templateItemId)
        {
            if (string.IsNullOrEmpty(templateItemId)) return null;
            for (int i = 0; i < Entries.Count; i++)
                if (string.Equals(Entries[i].TemplateItemId, templateItemId, StringComparison.Ordinal)) return Entries[i];
            return null;
        }

        private static List<ProductionRecipePlanEntry> Build()
        {
            List<ProductionRecipePlanEntry> result = new List<ProductionRecipePlanEntry>();
            // Keep the established progression workstream milestone levels wherever possible.
            // Cave-Mushroom-dependent slots (910100014/017/018) remain reserved and untouched.
            result.Add(Entry("crafting.basic_supply", "910100010", "Basic Workshop Supply", 1, ProductionRecipeContentKind.Foundation, 0, true, false, 0));
            result.Add(Entry("crafting.herbal_preparation", "910100011", "Herbal Preparation", 3, ProductionRecipeContentKind.ActivatedUtility, 1, false, true, 0));
            result.Add(Entry("crafting.trail_preparation", "910100012", "Trail Preparation", 5, ProductionRecipeContentKind.ActivatedUtility, 1, false, true, 1));
            result.Add(Entry("crafting.field_preparation", "910100013", "Field Preparation", 8, ProductionRecipeContentKind.ActivatedUtility, 2, false, true, 2));
            result.Add(Entry("crafting.journeyman_refinement", "910100015", "Journeyman Workshop Supply", 18, ProductionRecipeContentKind.Foundation, 0, true, false, 1));
            result.Add(Entry("crafting.traveler_supply", "910100016", "Traveler Workshop Supply", 25, ProductionRecipeContentKind.Foundation, 0, true, false, 2));
            return result;
        }

        private static ProductionRecipePlanEntry Entry(string key, string templateId, string prefix, int level,
            ProductionRecipeContentKind kind, int herbQuantity, bool extraNative, bool herbDiscovery, int tierOrdinal)
        {
            ProductionRecipePlanEntry entry = new ProductionRecipePlanEntry();
            entry.RecipeKey = key;
            entry.TemplateItemId = templateId;
            entry.DisplayPrefix = prefix;
            entry.MinimumCraftingLevel = level;
            entry.ContentKind = kind;
            entry.WildHerbQuantity = herbQuantity;
            entry.AddExtraNativeIngredient = extraNative;
            entry.RequiresWildHerbDiscovery = herbDiscovery;
            entry.TierOrdinal = tierOrdinal;
            return entry;
        }

        internal static string RunSelfTests()
        {
            if (Entries.Count != 6) return "FAIL production recipe plan count";
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int herb = 0;
            int foundation = 0;
            int lastLevel = 0;
            for (int i = 0; i < Entries.Count; i++)
            {
                ProductionRecipePlanEntry entry = Entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.RecipeKey) || string.IsNullOrEmpty(entry.DisplayPrefix)) return "FAIL production plan required fields";
                if (!CraftingExpandedItemIds.IsInRecipeTemplateRange(entry.TemplateItemId)) return "FAIL production plan template range";
                if (!keys.Add(entry.RecipeKey) || !ids.Add(entry.TemplateItemId)) return "FAIL production plan dedupe";
                if (entry.MinimumCraftingLevel < lastLevel) return "FAIL production plan level ordering";
                lastLevel = entry.MinimumCraftingLevel;
                if (entry.ContentKind == ProductionRecipeContentKind.ActivatedUtility)
                {
                    herb++;
                    if (!entry.RequiresWildHerbDiscovery || entry.WildHerbQuantity <= 0 || entry.AddExtraNativeIngredient) return "FAIL herbal plan requirements";
                }
                else
                {
                    foundation++;
                    if (entry.RequiresWildHerbDiscovery || entry.WildHerbQuantity != 0 || !entry.AddExtraNativeIngredient) return "FAIL foundation plan requirements";
                }
            }
            if (herb != 3 || foundation != 3) return "FAIL production plan category balance";
            if (Get("crafting.herbal_preparation") == null || GetByTemplateId("910100016") == null) return "FAIL production plan lookup";
            return "PASS production recipe plan";
        }
    }
}
