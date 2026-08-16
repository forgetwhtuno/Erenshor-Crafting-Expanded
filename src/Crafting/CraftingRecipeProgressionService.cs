using System;
using System.Collections.Generic;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Runtime glue for recipe unlocks. Foraging owns resource discovery, RecipeOwnership owns
    // permanent known-recipe state and physical Template recovery, and native Smithing remains
    // authoritative for ingredient validation/consumption/output.
    internal static class CraftingRecipeProgressionService
    {
        private static float _nextEvaluation;
        private static readonly HashSet<string> RegisteredOwnershipKeys = new HashSet<string>(StringComparer.Ordinal);
        private static readonly IRecipeDiscoverySource ForagingDiscoveries = new ForagingRecipeDiscoverySource();

        internal static void Initialize()
        {
            _nextEvaluation = 0f;
            RegisteredOwnershipKeys.Clear();
            EnsureOwnershipDefinitions();
        }

        internal static void OnCharacterChanged()
        {
            _nextEvaluation = 0f;
        }

        internal static void Tick(CraftingProgress progress, string characterKey)
        {
            if (!CraftingController.CharacterScopeResolved || progress == null || string.IsNullOrEmpty(characterKey)) return;
            if (Time.unscaledTime < _nextEvaluation) return;
            _nextEvaluation = Time.unscaledTime + 1f;
            EnsureOwnershipDefinitions();
            EvaluateUnlocks(progress);
        }

        internal static bool NotifyDiscovery(string discoveryKey)
        {
            if (!CraftingController.CharacterScopeResolved || string.IsNullOrEmpty(discoveryKey)) return false;
            if (!ForagingKnowledge.IsReady || !ForagingKnowledge.HasDiscovered(discoveryKey)) return false;
            return EvaluateUnlocks(CraftingController.Progress);
        }

        internal static bool EvaluateUnlocks(CraftingProgress progress)
        {
            if (progress == null || !CraftingController.CharacterScopeResolved) return false;
            EnsureOwnershipDefinitions();
            bool changed = false;
            IList<CustomRecipeDefinition> recipes = CraftingRecipeCatalog.Production.All;
            for (int i = 0; i < recipes.Count; i++)
            {
                CustomRecipeDefinition recipe = recipes[i];
                if (recipe == null || RecipeOwnershipApi.IsKnown(recipe.RecipeKey)) continue;
// Learning/granting a new physical template waits until this exact Template has
                // passed current-session native registration. Existing permanent knowledge is
                // retained even while a saved identity is temporarily inert.
                if (!ProductionNativeRecipeRegistry.IsRegisteredCurrentSession(recipe.TemplateItemId)) continue;
                int foragingLevel = ForagingKnowledge.IsReady ? ForagingKnowledge.CurrentLevel : 1;
                if (!RecipeAccessPolicy.CanLearn(progress.Level, foragingLevel, recipe, ForagingDiscoveries)) continue;

                RecipeOwnershipActionResult learned = RecipeOwnershipApi.LearnRecipe(recipe.RecipeKey);
                if (!learned.Success) continue;
                changed = true;
                ShowLearned(recipe.DisplayName, learned.TemplateGranted);
            }
            return changed;
        }

        internal static bool TryRestoreTemplate(string recipeKey)
        {
            RecipeOwnershipActionResult result = RecipeOwnershipApi.RestoreTemplate(recipeKey);
            return result != null && result.Success;
        }

        internal static bool IsRecipeAllowedForCurrentCharacter(CustomRecipeDefinition recipe)
        {
            return recipe != null &&
                ProductionNativeRecipeRegistry.IsRegisteredCurrentSession(recipe.TemplateItemId) &&
                CraftingController.CharacterScopeResolved &&
                RecipeAccessPolicy.CanUse(RecipeOwnershipApi.IsKnown(recipe.RecipeKey), true, CraftingController.Progress.Level,
                    ForagingKnowledge.IsReady ? ForagingKnowledge.CurrentLevel : 1, recipe, ForagingDiscoveries);
        }

        private static void EnsureOwnershipDefinitions()
        {
            IList<CustomRecipeDefinition> recipes = CraftingRecipeCatalog.Production.All;
            for (int i = 0; i < recipes.Count; i++)
            {
                CustomRecipeDefinition recipe = recipes[i];
                if (recipe == null || RegisteredOwnershipKeys.Contains(recipe.RecipeKey)) continue;
                RecipeOwnershipDefinitionRejectReason result = RecipeOwnershipApi.RegisterRecipe(new RecipeOwnershipDefinition
                {
                    StableRecipeId = recipe.RecipeKey,
                    TemplateItemId = recipe.TemplateItemId,
                    DisplayName = recipe.DisplayName,
                    MinimumCraftingLevel = recipe.MinimumCraftingLevel,
                    AdditionalLockReason = BuildProgressionLockReason(recipe),
                    Deprecated = false
                });
                if (result == RecipeOwnershipDefinitionRejectReason.None ||
                    result == RecipeOwnershipDefinitionRejectReason.DuplicateStableRecipeId ||
                    result == RecipeOwnershipDefinitionRejectReason.DuplicateTemplateItemId)
                    RegisteredOwnershipKeys.Add(recipe.RecipeKey);
            }
        }

        private static string BuildProgressionLockReason(CustomRecipeDefinition recipe)
        {
            if (recipe == null) return string.Empty;

            string discoveryReason = string.Empty;
            if (recipe.RequiredDiscoveries != null && recipe.RequiredDiscoveries.Count == 1)
            {
                string key = recipe.RequiredDiscoveries[0];
                ForageResourceDefinition resource = ForageResourceCatalog.FindByKnowledgeKey(key);
                discoveryReason = resource != null && !string.IsNullOrEmpty(resource.DisplayName)
                    ? "Discover " + resource.DisplayName
                    : "Discover required resource";
            }
            else if (recipe.RequiredDiscoveries != null && recipe.RequiredDiscoveries.Count > 1)
            {
                discoveryReason = "Discover required resources";
            }

            string forageReason = recipe.MinimumForagingLevel > 1
                ? "Requires Foraging " + recipe.MinimumForagingLevel.ToString()
                : string.Empty;
            if (forageReason.Length > 0 && discoveryReason.Length > 0) return forageReason + " • " + discoveryReason;
            if (forageReason.Length > 0) return forageReason;
            return discoveryReason;
        }

        private static void ShowLearned(string displayName, bool templateGranted)
        {
            try
            {
                string text = "[Crafting] Learned Recipe: " + displayName;
                if (!templateGranted) text += ". Recipe learned. Restore its template from Crafting.";
                UpdateSocialLog.LogAdd(text, "yellow");
            }
            catch { }
        }

        private sealed class ForagingRecipeDiscoverySource : IRecipeDiscoverySource
        {
            public bool HasDiscovery(string discoveryKey)
            {
                return ForagingKnowledge.IsReady && ForagingKnowledge.HasDiscovered(discoveryKey);
            }
        }
    }

    public static class CraftingRecipeDiscoveryBridge
    {
        // Called by Foraging only after a successful gather transaction has committed discovery.
        // The permanent discovery authority remains ForagingKnowledge.
        public static bool NotifyResourceDiscovered(string itemId)
        {
            string key = RecipeDiscoveryKeys.FromItemId(itemId);
            return !string.IsNullOrEmpty(key) && CraftingRecipeProgressionService.NotifyDiscovery(key);
        }
    }
}
