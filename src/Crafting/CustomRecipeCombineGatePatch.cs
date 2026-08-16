using System;
using HarmonyLib;

namespace ErenshorCraftingExpanded
{
    // Future production mod templates are permanently learned recipes represented by physical
    // native Template items. This prefix gates ONLY mod-owned recipe Templates; ordinary native
    // recipes pass through untouched. If allowed, Smithing.Combine remains the sole authority for
    // ingredients, consumption and output.
    [HarmonyPatch(typeof(Smithing), "Combine")]
    internal static class CustomRecipeCombineGatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            CraftRecipeSnapshot recipe = null;
            try
            {
                recipe = GameCraftingApi.TryGetActiveRecipe();
                return IsRecipeAllowed(recipe, true);
            }
            catch (Exception ex)
            {
                CraftingController.LogError("Custom recipe gate failed: " + ex.GetType().Name);
                if (recipe != null && CraftingExpandedItemIds.IsInRecipeTemplateRange(recipe.TemplateItemId)) return false;
                return true;
            }
        }

        internal static bool IsRecipeAllowed(CraftRecipeSnapshot recipe, bool showMessage)
        {
            if (recipe == null) return true;

            if (string.Equals(recipe.TemplateItemId, CraftingRecipeCatalog.ExperimentalHerbalTemplateId, StringComparison.Ordinal))
            {
                bool experimentEnabled = CraftingConfig.EnableMod != null && CraftingConfig.EnableMod.Value &&
                    CraftingConfig.ExperimentalNativeRecipeRegistration != null && CraftingConfig.ExperimentalNativeRecipeRegistration.Value &&
                    ExperimentalNativeRecipeRegistry.Registered;
                if (experimentEnabled) return true;
                if (showMessage) try { UpdateSocialLog.LogAdd("[Crafting] Experimental recipe use is unavailable for this session. Verify registration or restart after an experimental session.", "yellow"); } catch { }
                return false;
            }

            CustomRecipeDefinition custom = CraftingRecipeCatalog.Production.GetByTemplateId(recipe.TemplateItemId);
            if (custom == null)
            {
                if (!CraftingExpandedItemIds.IsInRecipeTemplateRange(recipe.TemplateItemId)) return true;
                if (showMessage) try { UpdateSocialLog.LogAdd("[Crafting] This Crafting Expanded recipe is not registered for this build.", "yellow"); } catch { }
                return false;
            }
            if (CraftingConfig.EnableMod == null || !CraftingConfig.EnableMod.Value)
            {
                if (showMessage) try { UpdateSocialLog.LogAdd("[Crafting] Crafting Expanded recipes are disabled.", "yellow"); } catch { }
                return false;
            }
            if (!ProductionNativeRecipeRegistry.IsRegisteredCurrentSession(recipe.TemplateItemId))
            {
                if (showMessage) try { UpdateSocialLog.LogAdd("[Crafting] This recipe is known but its native Template is not verified for the current session.", "yellow"); } catch { }
                return false;
            }

            bool allowed = CraftingRecipeProgressionService.IsRecipeAllowedForCurrentCharacter(custom);
            if (allowed) return true;
            if (showMessage) try { UpdateSocialLog.LogAdd("[Crafting] This recipe is not learned for the current character.", "yellow"); } catch { }
            return false;
        }
    }
}
