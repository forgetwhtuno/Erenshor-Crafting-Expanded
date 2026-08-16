using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public sealed class CraftingResourceDisplayModel
    {
        public string Key = string.Empty;
        public string DisplayName = string.Empty;
        public string StateText = string.Empty;
        public string DetailText = string.Empty;
        public bool Discovered;
        public bool SkillLocked;
    }

    public sealed class CraftingMaterialDisplayModel
    {
        public string ItemId = string.Empty;
        public string DisplayName = string.Empty;
        public int Required;
        public int Available;
        public bool Sufficient;
    }

    public sealed class CraftingActiveRecipeDisplayModel
    {
        public bool HasRecipe;
        public bool UsesNativeSpecialRules;
        public string Title = string.Empty;
        public string StatusText = string.Empty;
        public readonly List<CraftingMaterialDisplayModel> Materials = new List<CraftingMaterialDisplayModel>();
    }

    // Pure player-facing presentation policy. It consumes only already-authoritative progression,
    // discovery, recipe, and inventory snapshots; it never mutates gameplay or infers unproven
    // recipe/output data.
    public static class CraftingKnowledgePresentationPolicy
    {
        public static CraftingResourceDisplayModel BuildResourceRow(
            ForageResourceDefinition resource,
            int foragingLevel,
            bool discovered,
            bool experimentalCoveredResourcesEnabled)
        {
            if (resource == null) return null;
            if (resource.Experimental && !experimentalCoveredResourcesEnabled) return null;
            if (foragingLevel < 1) foragingLevel = 1;

            CraftingResourceDisplayModel row = new CraftingResourceDisplayModel();
            row.Key = resource.KnowledgeKey ?? string.Empty;
            row.DisplayName = string.IsNullOrEmpty(resource.DisplayName) ? "Resource" : resource.DisplayName;
            row.Discovered = discovered;
            row.SkillLocked = !discovered && foragingLevel < resource.MinimumSkill;

            if (discovered)
            {
                row.StateText = "DISCOVERED";
                row.DetailText = "Known resource";
                return row;
            }

            row.StateText = "UNDISCOVERED";
            if (row.SkillLocked)
            {
                row.DetailText = "Requires Foraging " + resource.MinimumSkill.ToString();
                return row;
            }

            row.DetailText = ExploreHintForResource(resource);
            return row;
        }

        public static string BuildNextExplorationHint(IList<CraftingResourceDisplayModel> rows)
        {
            if (rows == null || rows.Count == 0) return "No foraging resources are currently available.";

            for (int i = 0; i < rows.Count; i++)
            {
                CraftingResourceDisplayModel row = rows[i];
                if (row == null || row.Discovered || row.SkillLocked) continue;
                return row.DetailText + " for " + row.DisplayName + ".";
            }

            for (int i = 0; i < rows.Count; i++)
            {
                CraftingResourceDisplayModel row = rows[i];
                if (row == null || row.Discovered || !row.SkillLocked) continue;
                return row.DisplayName + " unlocks at " + row.DetailText.Replace("Requires ", string.Empty) + ".";
            }

            return "All currently available resources discovered.";
        }

        public static CraftingActiveRecipeDisplayModel BuildActiveRecipe(
            CraftRecipeSnapshot recipe,
            IDictionary<string, int> availableByItemId,
            int fuelSourceUnits,
            int craftableCount,
            bool usesNativeSpecialRules)
        {
            CraftingActiveRecipeDisplayModel model = new CraftingActiveRecipeDisplayModel();
            if (recipe == null)
            {
                model.Title = "No recipe loaded";
                model.StatusText = "Open a forge and load a template to inspect materials.";
                return model;
            }

            model.HasRecipe = true;
            model.UsesNativeSpecialRules = usesNativeSpecialRules;
            model.Title = !string.IsNullOrEmpty(recipe.OutputItemName)
                ? recipe.OutputItemName
                : (!string.IsNullOrEmpty(recipe.TemplateItemName) ? recipe.TemplateItemName : "Loaded recipe");

            if (usesNativeSpecialRules)
            {
                model.StatusText = "Native special combine — use the forge's normal rules.";
                return model;
            }

            int templateUnits = CountAvailable(availableByItemId, recipe.TemplateItemId);
            AddMaterial(model, recipe.TemplateItemId, "Template", 1, templateUnits);
            AddMaterial(model, string.Empty, "Fuel Source", 1, fuelSourceUnits);
            if (recipe.Requirements != null)
            {
                for (int i = 0; i < recipe.Requirements.Count; i++)
                {
                    RequirementLine requirement = recipe.Requirements[i];
                    if (requirement.Quantity <= 0) continue;
                    string name = string.IsNullOrEmpty(requirement.ItemName) ? "Material" : requirement.ItemName;
                    AddMaterial(model, requirement.ItemId, name, requirement.Quantity, CountAvailable(availableByItemId, requirement.ItemId));
                }
            }

            if (craftableCount > 0)
            {
                model.StatusText = "CRAFTABLE NOW  •  " + craftableCount.ToString() + " available";
                return model;
            }

            for (int i = 0; i < model.Materials.Count; i++)
            {
                CraftingMaterialDisplayModel material = model.Materials[i];
                if (material == null || material.Sufficient) continue;
                model.StatusText = "Missing " + material.DisplayName;
                return model;
            }

            model.StatusText = "Not craftable yet — native forge validation still applies.";
            return model;
        }

        public static string BuildRecipeSummary(int knownCount, int totalCount)
        {
            if (knownCount < 0) knownCount = 0;
            if (totalCount < 0) totalCount = 0;
            if (knownCount > totalCount) knownCount = totalCount;
            return "RECIPES / TEMPLATES  •  Known " + knownCount.ToString() + "  •  Locked " + (totalCount - knownCount).ToString();
        }

        private static string ExploreHintForResource(ForageResourceDefinition resource)
        {
            if (resource == null) return "Explore suitable areas";
            if (resource.RegionalRule == ForageRegionalRule.ExplicitScenes &&
                resource.EligibleScenes != null && resource.EligibleScenes.Length > 0)
            {
                if (resource.EligibleScenes.Length == 1)
                    return "Explore " + resource.EligibleScenes[0];
                return "Explore " + string.Join(", ", resource.EligibleScenes);
            }

            if (resource.Pool == ForageResourcePool.CoveredFungi ||
                resource.Pool == ForageResourcePool.CoveredMoss)
                return "Explore covered or cave areas";
            return "Explore open areas";
        }

        private static int CountAvailable(IDictionary<string, int> availableByItemId, string itemId)
        {
            if (availableByItemId == null || string.IsNullOrEmpty(itemId)) return 0;
            int value;
            if (!availableByItemId.TryGetValue(itemId, out value) || value < 0) return 0;
            return value;
        }

        private static void AddMaterial(CraftingActiveRecipeDisplayModel model, string itemId, string displayName, int required, int available)
        {
            if (model == null || required <= 0) return;
            if (available < 0) available = 0;
            model.Materials.Add(new CraftingMaterialDisplayModel
            {
                ItemId = itemId ?? string.Empty,
                DisplayName = string.IsNullOrEmpty(displayName) ? "Material" : displayName,
                Required = required,
                Available = available,
                Sufficient = available >= required
            });
        }

        internal static string RunSelfTests()
        {
            ForageResourceDefinition herb = ForageResourceCatalog.FindByKnowledgeKey("wild_herb");
            CraftingResourceDisplayModel herbUnknown = BuildResourceRow(herb, 1, false, false);
            if (herbUnknown == null || herbUnknown.StateText != "UNDISCOVERED" || herbUnknown.DetailText != "Explore open areas")
                return "FAIL resource undiscovered exploration state";
            CraftingResourceDisplayModel herbKnown = BuildResourceRow(herb, 3, true, false);
            if (herbKnown == null || !herbKnown.Discovered || herbKnown.StateText != "DISCOVERED")
                return "FAIL resource discovered state";

            ForageResourceDefinition mushroom = ForageResourceCatalog.FindByKnowledgeKey("cave_mushroom");
            if (BuildResourceRow(mushroom, 20, false, false) != null) return "FAIL disabled experimental resource should stay out of player list";
            CraftingResourceDisplayModel mushroomLocked = BuildResourceRow(mushroom, 7, false, true);
            if (mushroomLocked == null || !mushroomLocked.SkillLocked || mushroomLocked.DetailText != "Requires Foraging 8")
                return "FAIL resource skill lock state";
            CraftingResourceDisplayModel mushroomExplore = BuildResourceRow(mushroom, 8, false, true);
            if (mushroomExplore == null || mushroomExplore.DetailText != "Explore covered or cave areas")
                return "FAIL covered resource exploration hint";

            ForageResourceDefinition caveMoss = ForageResourceCatalog.FindByKnowledgeKey("cave_moss");
            CraftingResourceDisplayModel mossExplore = BuildResourceRow(caveMoss, 24, false, true);
            if (mossExplore == null || mossExplore.DetailText != "Explore covered or cave areas")
                return "FAIL covered moss exploration hint";

            ForageResourceDefinition blightroot = ForageResourceCatalog.FindByKnowledgeKey("blightroot");
            CraftingResourceDisplayModel rootExplore = BuildResourceRow(blightroot, 36, false, false);
            if (rootExplore == null || rootExplore.DetailText != "Explore The Blight")
                return "FAIL explicit regional exploration hint";

            List<CraftingResourceDisplayModel> next = new List<CraftingResourceDisplayModel>();
            next.Add(herbKnown);
            next.Add(mushroomExplore);
            if (BuildNextExplorationHint(next) != "Explore covered or cave areas for Cave Mushroom.")
                return "FAIL next exploration hint";

            CraftRecipeSnapshot recipe = new CraftRecipeSnapshot();
            recipe.TemplateItemId = "template";
            recipe.TemplateItemName = "Recipe: Test";
            recipe.OutputItemId = "output";
            recipe.OutputItemName = "Test Output";
            recipe.Requirements.Add(new RequirementLine("ore", "Iron Ore", 3));
            Dictionary<string, int> available = new Dictionary<string, int>();
            available["template"] = 1;
            available["ore"] = 2;
            CraftingActiveRecipeDisplayModel missing = BuildActiveRecipe(recipe, available, 1, 0, false);
            if (!missing.HasRecipe || missing.StatusText != "Missing Iron Ore" || missing.Materials.Count != 3)
                return "FAIL active recipe missing-material presentation";
            available["ore"] = 7;
            CraftingActiveRecipeDisplayModel craftable = BuildActiveRecipe(recipe, available, 4, 1, false);
            if (craftable.StatusText != "CRAFTABLE NOW  •  1 available") return "FAIL active recipe craftable presentation";
            CraftingActiveRecipeDisplayModel special = BuildActiveRecipe(recipe, available, 4, 0, true);
            if (!special.UsesNativeSpecialRules || special.Materials.Count != 0 || special.StatusText.IndexOf("Native special combine", StringComparison.Ordinal) != 0)
                return "FAIL native special recipe presentation";
            CraftingActiveRecipeDisplayModel empty = BuildActiveRecipe(null, null, 0, 0, false);
            if (empty.HasRecipe || empty.Title != "No recipe loaded") return "FAIL no-recipe presentation";

            if (BuildRecipeSummary(2, 5) != "RECIPES / TEMPLATES  •  Known 2  •  Locked 3") return "FAIL recipe summary presentation";
            if (BuildRecipeSummary(9, 4) != "RECIPES / TEMPLATES  •  Known 4  •  Locked 0") return "FAIL recipe summary normalization";
            return "PASS crafting knowledge presentation policy";
        }
    }
}
