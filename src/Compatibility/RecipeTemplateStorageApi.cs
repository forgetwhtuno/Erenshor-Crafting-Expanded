using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // Read-only physical-template presence bridge. Inventory and forge are proven by existing
    // project accessors. A bank provider may prove only "In Bank". Ordinary "Missing" requires the
    // stronger absence authority, whose zero result covers every supported holding surface outside
    // inventory+forge. Without that authority, absence remains Unknown and restore fails closed.
    internal static class RecipeTemplateStorageApi
    {
        internal static RecipeTemplateStorageSnapshot Probe(string templateItemId)
        {
            RecipeTemplateStorageSnapshot snapshot = new RecipeTemplateStorageSnapshot { TemplateItemId = templateItemId ?? string.Empty };
            if (string.IsNullOrEmpty(templateItemId)) return snapshot;

            try
            {
                List<InventoryAvailability> inventory = GameCraftingApi.ReadInventoryAvailability();
                long total = 0;
                for (int i = 0; i < inventory.Count; i++)
                {
                    InventoryAvailability line = inventory[i];
                    if (!string.Equals(line.ItemId, templateItemId, StringComparison.Ordinal) || line.Quantity <= 0) continue;
                    total += line.Quantity;
                    if (total >= int.MaxValue) { total = int.MaxValue; break; }
                }
                snapshot.InventoryQuantity = (int)total;
            }
            catch { snapshot.InventoryQuantity = 0; }

            try
            {
                CraftRecipeSnapshot forge = GameCraftingApi.TryGetActiveRecipe();
                if (forge != null && string.Equals(forge.TemplateItemId, templateItemId, StringComparison.Ordinal)) snapshot.ForgeQuantity = 1;
            }
            catch { snapshot.ForgeQuantity = 0; }

            int bankQuantity;
            snapshot.BankInspectionAvailable = RecipeOwnershipIntegration.TryCountBankTemplate(templateItemId, out bankQuantity);
            snapshot.BankQuantity = snapshot.BankInspectionAvailable ? bankQuantity : 0;
            int externalQuantity;
            snapshot.AuthoritativeAbsenceProbeAvailable = RecipeOwnershipIntegration.TryCountAllExternalTemplateStorage(templateItemId, out externalQuantity);
            snapshot.ExternalStorageQuantity = snapshot.AuthoritativeAbsenceProbeAvailable ? externalQuantity : 0;

            snapshot.Location = RecipeTemplateStoragePolicy.DetermineLocation(
                snapshot.InventoryQuantity,
                snapshot.ForgeQuantity,
                snapshot.BankInspectionAvailable,
                snapshot.BankQuantity,
                snapshot.AuthoritativeAbsenceProbeAvailable,
                snapshot.ExternalStorageQuantity);
            return snapshot;
        }
    }
}
