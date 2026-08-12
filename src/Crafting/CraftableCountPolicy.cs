using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // Pure, game-independent math: how many times can this recipe be crafted from the given
    // inventory totals. No native "how many can I craft" method exists (see findings doc), so
    // this is mod-owned logic, not a wrapper around a hidden native query.
    public static class CraftableCountPolicy
    {
        public static int CalculateCraftableCount(IEnumerable<RequirementLine> requirements, IDictionary<string, int> availableByItemId)
        {
            if (requirements == null) return 0;
            long best = long.MaxValue;
            bool any = false;
            foreach (RequirementLine line in requirements)
            {
                any = true;
                if (line.Quantity <= 0) continue;
                int available = 0;
                if (availableByItemId != null) availableByItemId.TryGetValue(line.ItemId, out available);
                long possible = (long)available / line.Quantity;
                if (possible < best) best = possible;
            }
            if (!any) return 0;
            if (best == long.MaxValue) return 0;
            if (best < 0) return 0;
            if (best > int.MaxValue) return int.MaxValue;
            return (int)best;
        }


        // Full generic-forge count: component materials plus one copy of the loaded template and
        // one native FuelSource per craft. The caller supplies totals across both inventory and
        // currently occupied forge slots, so moving a stack/unit into the forge does not make
        // the displayed count incorrectly drop. Special quality/merge templates are excluded by
        // the live caller because their native rules are not TemplateIngredients-based.
        public static int CalculateCraftableCount(
            CraftRecipeSnapshot recipe,
            IDictionary<string, int> availableByItemId,
            int availableFuelSourceUnits)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.TemplateItemId)) return 0;
            int materialCount = CalculateCraftableCount(recipe.Requirements, availableByItemId);
            if (materialCount <= 0) return 0;

            int templateUnits = 0;
            if (availableByItemId != null) availableByItemId.TryGetValue(recipe.TemplateItemId, out templateUnits);
            if (templateUnits <= 0 || availableFuelSourceUnits <= 0) return 0;

            int result = materialCount;
            if (templateUnits < result) result = templateUnits;
            if (availableFuelSourceUnits < result) result = availableFuelSourceUnits;
            return result < 0 ? 0 : result;
        }

        public static Dictionary<string, int> BuildAvailability(IEnumerable<InventoryAvailability> slots)
        {
            Dictionary<string, int> totals = new Dictionary<string, int>();
            if (slots == null) return totals;
            foreach (InventoryAvailability slot in slots)
            {
                if (string.IsNullOrEmpty(slot.ItemId) || slot.Quantity <= 0) continue;
                int existing;
                totals.TryGetValue(slot.ItemId, out existing);
                long sum = (long)existing + slot.Quantity;
                totals[slot.ItemId] = sum > int.MaxValue ? int.MaxValue : (int)sum;
            }
            return totals;
        }

        internal static string RunSelfTests()
        {
            List<RequirementLine> req = new List<RequirementLine>
            {
                new RequirementLine("coal", "Coal", 1),
                new RequirementLine("template", "Template", 1),
                new RequirementLine("ore", "Iron Ore", 3)
            };
            Dictionary<string, int> inv = new Dictionary<string, int> { { "coal", 10 }, { "template", 5 }, { "ore", 17 } };
            if (CalculateCraftableCount(req, inv) != 5) return "FAIL basic count";

            if (CalculateCraftableCount(req, new Dictionary<string, int>()) != 0) return "FAIL zero inventory";

            Dictionary<string, int> exact = new Dictionary<string, int> { { "coal", 1 }, { "template", 1 }, { "ore", 3 } };
            if (CalculateCraftableCount(req, exact) != 1) return "FAIL exact quantity";

            Dictionary<string, int> missingOne = new Dictionary<string, int> { { "coal", 10 }, { "template", 5 } };
            if (CalculateCraftableCount(req, missingOne) != 0) return "FAIL insufficient one component";

            if (CalculateCraftableCount(new List<RequirementLine>(), inv) != 0) return "FAIL invalid recipe (no requirements)";

            List<RequirementLine> overflow = new List<RequirementLine> { new RequirementLine("x", "X", 1) };
            Dictionary<string, int> huge = new Dictionary<string, int> { { "x", int.MaxValue } };
            if (CalculateCraftableCount(overflow, huge) != int.MaxValue) return "FAIL overflow safety";

            List<InventoryAvailability> stacks = new List<InventoryAvailability>
            {
                new InventoryAvailability("ore", 20),
                new InventoryAvailability("ore", 11),
                new InventoryAvailability("ore", -50) // corrupt/invalid slot input must not reduce totals
            };
            Dictionary<string, int> combined = BuildAvailability(stacks);
            if (combined["ore"] != 31) return "FAIL multiple stack totals";

            CraftRecipeSnapshot fullRecipe = new CraftRecipeSnapshot { TemplateItemId = "template", TemplateItemName = "Template" };
            fullRecipe.Requirements.Add(new RequirementLine("ore", "Iron Ore", 3));
            Dictionary<string, int> fullAvailable = new Dictionary<string, int>
            {
                { "template", 4 },
                { "ore", 20 }
            };
            if (CalculateCraftableCount(fullRecipe, fullAvailable, 10) != 4) return "FAIL template count should limit full craftable count";
            if (CalculateCraftableCount(fullRecipe, fullAvailable, 2) != 2) return "FAIL fuel count should limit full craftable count";
            if (CalculateCraftableCount(fullRecipe, new Dictionary<string, int> { { "ore", 20 } }, 10) != 0) return "FAIL missing template should make full count zero";
            if (CalculateCraftableCount(fullRecipe, fullAvailable, 0) != 0) return "FAIL missing fuel should make full count zero";

            return "PASS craftable count";
        }
    }
}
