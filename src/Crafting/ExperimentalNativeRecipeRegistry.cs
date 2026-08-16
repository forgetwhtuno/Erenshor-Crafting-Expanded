using System;
using System.Collections.Generic;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    internal static class ExperimentalNativeRecipeRegistry
    {
        internal static bool Attempted;
        internal static bool Registered;
        internal static int AttemptCount;
        internal static bool CanAutoRetry { get { return !Registered && AttemptCount < 10; } }
        internal static string LastFailure = string.Empty;
        internal static string DonorTemplateId = string.Empty;
        internal static string DonorTemplateName = string.Empty;
        internal static string OutputItemId = string.Empty;
        internal static string OutputItemName = string.Empty;

        internal static void BeginSession()
        {
            Attempted = false; Registered = false; AttemptCount = 0; LastFailure = string.Empty;
            DonorTemplateId = DonorTemplateName = OutputItemId = OutputItemName = string.Empty;
        }

        internal static bool TryRegisterFromLiveDatabase()
        {
            if (CraftingConfig.ExperimentalNativeRecipeRegistration == null || !CraftingConfig.ExperimentalNativeRecipeRegistration.Value)
            { LastFailure = "experimental registration gate is OFF"; return false; }
            object db = GameItemRegistryApi.TryGetLiveItemDatabase();
            return TryRegister(db);
        }

        internal static bool TryRegister(object itemDatabaseInstance)
        {
            Attempted = true;
            AttemptCount++;
            LastFailure = string.Empty;
            if (CraftingConfig.EnableMod == null || !CraftingConfig.EnableMod.Value)
            { LastFailure = "Crafting Expanded master switch is OFF"; return false; }
            if (CraftingConfig.ExperimentalNativeRecipeRegistration == null || !CraftingConfig.ExperimentalNativeRecipeRegistration.Value)
            { LastFailure = "experimental registration gate is OFF"; return false; }
            if (itemDatabaseInstance == null) { LastFailure = "live ItemDatabase unavailable"; return false; }
            if (!NativeCraftingRuntimeProbe.Last.ShapeSupported) { LastFailure = "current runtime recipe shape not proven"; return false; }
            object herb = GameItemRegistryApi.TryResolveCustomItem(CraftingExpandedItemIds.WildHerbId);
            if (herb == null) { LastFailure = "Wild Herb is not registered in the live ItemDB"; return false; }

            List<ExperimentalRecipeCandidate> candidates = GameNativeRecipeRegistryApi.FindCandidates(itemDatabaseInstance);
            object existing = GameItemRegistryApi.TryGetLiveItem(itemDatabaseInstance, CraftingRecipeCatalog.ExperimentalHerbalTemplateId);
            if (existing != null)
            {
                if (!GameItemRegistryApi.HasOwnedMarker(existing, CraftingRecipeCatalog.ExperimentalHerbalTemplateId))
                { LastFailure = "experimental template id collision with foreign item"; return false; }
                ExperimentalRecipeCandidate matched = null;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (GameNativeRecipeRegistryApi.MatchesVerificationDonor(existing, candidates[i], CraftingRecipeCatalog.ExperimentalHerbalTemplateId))
                    { matched = candidates[i]; break; }
                }
                if (matched == null)
                { LastFailure = "existing owned experimental template cannot be re-proven against a packaged native donor; restart before more recipe testing"; return false; }
                CaptureDonor(matched);
                Registered = true;
                return true;
            }

            if (candidates.Count == 0) { LastFailure = "no conservative native verification donor found"; return false; }
            ExperimentalRecipeCandidate donor = candidates[0];
            string failure;
            object clone = GameNativeRecipeRegistryApi.CloneVerificationTemplate(donor.TemplateItem, herb,
                CraftingRecipeCatalog.ExperimentalHerbalTemplateId, "Recipe: Herbal Preparation (Verification)", out failure);
            if (clone == null) { LastFailure = failure; return false; }

            object live;
            if (!GameItemRegistryApi.TryInsertOwnedItem(itemDatabaseInstance, CraftingRecipeCatalog.ExperimentalHerbalTemplateId, clone, out live))
            {
                try { UnityEngine.Object unityClone = clone as UnityEngine.Object; if (unityClone != null) UnityEngine.Object.Destroy(unityClone); } catch { }
                LastFailure = "ItemDB insertion rejected or collided";
                return false;
            }
            if (!GameNativeRecipeRegistryApi.MatchesVerificationDonor(live, donor, CraftingRecipeCatalog.ExperimentalHerbalTemplateId))
            {
                AttemptCount = 10;
                LastFailure = "inserted template failed exact donor post-registration validation; restart before more recipe testing";
                return false;
            }

            CaptureDonor(donor);
            Registered = true;
            return true;
        }

        private static void CaptureDonor(ExperimentalRecipeCandidate donor)
        {
            if (donor == null) return;
            DonorTemplateId = donor.TemplateId;
            DonorTemplateName = donor.TemplateName;
            OutputItemId = donor.OutputId;
            OutputItemName = donor.OutputName;
        }

        internal static bool GrantVerificationTemplate()
        {
            if (CraftingConfig.ExperimentalNativeRecipeRegistration == null || !CraftingConfig.ExperimentalNativeRecipeRegistration.Value || !Registered) return false;
            return GameItemRegistryApi.GrantRegisteredItemStrict(CraftingRecipeCatalog.ExperimentalHerbalTemplateId, 1);
        }

        internal static string Describe()
        {
            string state = Registered ? "REGISTERED" : (Attempted ? "NOT REGISTERED" : "not attempted");
            string text = "gate=" + (CraftingConfig.ExperimentalNativeRecipeRegistration != null && CraftingConfig.ExperimentalNativeRecipeRegistration.Value ? "ON" : "OFF") + " state=" + state + " attempts=" + AttemptCount;
            if (Registered) text += " donor=" + DonorTemplateName + "#" + DonorTemplateId + " output=" + OutputItemName + "#" + OutputItemId;
            if (!string.IsNullOrEmpty(LastFailure)) text += " reason={" + LastFailure + "}";
            return text;
        }

        internal static string DescribeCandidates()
        {
            object db = GameItemRegistryApi.TryGetLiveItemDatabase();
            List<ExperimentalRecipeCandidate> candidates = GameNativeRecipeRegistryApi.FindCandidates(db);
            if (candidates.Count == 0) return "(none)";
            System.Text.StringBuilder sb = new System.Text.StringBuilder(480);
            int limit = candidates.Count < 5 ? candidates.Count : 5;
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) sb.Append(" || ");
                ExperimentalRecipeCandidate c = candidates[i];
                sb.Append(c.TemplateName).Append('#').Append(c.TemplateId).Append(" -> ").Append(c.OutputName).Append('#').Append(c.OutputId)
                    .Append(" value=").Append(c.OutputValue).Append(" ingredientEntries=").Append(c.IngredientEntries).Append(" stacks=").Append(c.DistinctIngredients).Append("+WildHerb");
            }
            return sb.ToString();
        }
    }
}
