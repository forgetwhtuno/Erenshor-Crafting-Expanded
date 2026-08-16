using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public static class ProductionRecipeDefinitionFactory
    {
        public static CustomRecipeDefinition Create(ProductionRecipePlanEntry plan, string outputItemId, string outputItemName,
            IList<string> donorIngredientIds, string extraNativeIngredientId)
        {
            if (plan == null || string.IsNullOrEmpty(outputItemId) || string.IsNullOrEmpty(outputItemName) || donorIngredientIds == null || donorIngredientIds.Count == 0) return null;

            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < donorIngredientIds.Count; i++)
            {
                string id = donorIngredientIds[i];
                if (string.IsNullOrEmpty(id)) return null;
                int current;
                counts.TryGetValue(id, out current);
                counts[id] = current + 1;
            }

            if (plan.WildHerbQuantity > 0)
            {
                int current;
                counts.TryGetValue(CraftingExpandedItemIds.WildHerbId, out current);
                counts[CraftingExpandedItemIds.WildHerbId] = current + plan.WildHerbQuantity;
            }
            if (plan.AddExtraNativeIngredient)
            {
                if (string.IsNullOrEmpty(extraNativeIngredientId) || CraftingExpandedItemIds.IsInOwnedRange(extraNativeIngredientId)) return null;
                int current;
                counts.TryGetValue(extraNativeIngredientId, out current);
                counts[extraNativeIngredientId] = current + 1;
            }

            CustomRecipeDefinition definition = new CustomRecipeDefinition();
            definition.RecipeKey = plan.RecipeKey;
            definition.TemplateItemId = plan.TemplateItemId;
            definition.DisplayName = plan.DisplayPrefix + ": " + outputItemName;
            definition.OutputItemId = outputItemId;
            definition.MinimumCraftingLevel = plan.MinimumCraftingLevel;
            List<string> ids = new List<string>(counts.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++) definition.Ingredients.Add(new CustomRecipeIngredient(ids[i], counts[ids[i]]));
            if (plan.RequiresWildHerbDiscovery) definition.RequiredDiscoveries.Add(RecipeDiscoveryKeys.WildHerb);
            return CustomRecipeCatalog.ValidateShape(definition) == CustomRecipeRejectReason.None ? definition : null;
        }

        internal static string RunSelfTests()
        {
            ProductionRecipePlanEntry herb = ProductionRecipePlan.Get("crafting.herbal_preparation");
            CustomRecipeDefinition herbDef = Create(herb, "native.out", "Native Output", new List<string> { "native.a", "native.a", "native.b" }, string.Empty);
            if (herbDef == null || herbDef.Ingredients.Count != 3 || herbDef.RequiredDiscoveries.Count != 1) return "FAIL herb definition factory";
            if (Quantity(herbDef, "native.a") != 2 || Quantity(herbDef, CraftingExpandedItemIds.WildHerbId) != 1) return "FAIL herb ingredient normalization";

            ProductionRecipePlanEntry foundation = ProductionRecipePlan.Get("crafting.basic_supply");
            CustomRecipeDefinition baseDef = Create(foundation, "native.out2", "Native Output 2", new List<string> { "native.a", "native.b" }, "native.b");
            if (baseDef == null || Quantity(baseDef, "native.b") != 2 || baseDef.RequiredDiscoveries.Count != 0) return "FAIL foundation extra ingredient";
            if (Create(foundation, "native.out2", "Native Output 2", new List<string> { "native.a" }, CraftingExpandedItemIds.WildHerbId) != null) return "FAIL foundation owned extra ingredient";
            return "PASS production recipe definition factory";
        }

        private static int Quantity(CustomRecipeDefinition definition, string id)
        {
            for (int i = 0; i < definition.Ingredients.Count; i++)
                if (string.Equals(definition.Ingredients[i].ItemId, id, StringComparison.Ordinal)) return definition.Ingredients[i].Quantity;
            return 0;
        }
    }
}
