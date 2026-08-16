namespace ErenshorCraftingExpanded
{
    // Pure policy separating authoritative evidence from Unity/native storage probing. A bank
    // reader may identify a banked copy, but only the stronger comprehensive absence probe may
    // turn "not visible here" into ConfirmedMissing.
    public static class RecipeTemplateStoragePolicy
    {
        public static bool IsKnownPresent(RecipeTemplateLocationState location)
        {
            return location == RecipeTemplateLocationState.Inventory ||
                location == RecipeTemplateLocationState.Forge ||
                location == RecipeTemplateLocationState.Bank ||
                location == RecipeTemplateLocationState.OtherStorage;
        }

        public static RecipeTemplateLocationState DetermineLocation(
            int inventoryQuantity,
            int forgeQuantity,
            bool bankInspectionAvailable,
            int bankQuantity,
            bool authoritativeAbsenceProbeAvailable,
            int allExternalQuantity)
        {
            if (inventoryQuantity > 0) return RecipeTemplateLocationState.Inventory;
            if (forgeQuantity > 0) return RecipeTemplateLocationState.Forge;
            if (bankInspectionAvailable && bankQuantity > 0) return RecipeTemplateLocationState.Bank;
            if (authoritativeAbsenceProbeAvailable && allExternalQuantity > 0) return RecipeTemplateLocationState.OtherStorage;
            if (authoritativeAbsenceProbeAvailable) return RecipeTemplateLocationState.ConfirmedMissing;
            return RecipeTemplateLocationState.Unknown;
        }

        internal static string RunSelfTests()
        {
            if (!IsKnownPresent(RecipeTemplateLocationState.Inventory) || !IsKnownPresent(RecipeTemplateLocationState.Forge) ||
                !IsKnownPresent(RecipeTemplateLocationState.Bank) || !IsKnownPresent(RecipeTemplateLocationState.OtherStorage) ||
                IsKnownPresent(RecipeTemplateLocationState.Unknown) || IsKnownPresent(RecipeTemplateLocationState.ConfirmedMissing))
                return "FAIL storage known-present classification";
            if (DetermineLocation(1, 0, false, 0, false, 0) != RecipeTemplateLocationState.Inventory) return "FAIL storage inventory";
            if (DetermineLocation(0, 1, false, 0, false, 0) != RecipeTemplateLocationState.Forge) return "FAIL storage forge";
            if (DetermineLocation(0, 0, true, 2, false, 0) != RecipeTemplateLocationState.Bank) return "FAIL storage bank";
            if (DetermineLocation(0, 0, true, 0, false, 0) != RecipeTemplateLocationState.Unknown) return "FAIL zero bank must not prove missing";
            if (DetermineLocation(0, 0, false, 0, true, 1) != RecipeTemplateLocationState.OtherStorage) return "FAIL storage elsewhere";
            if (DetermineLocation(0, 0, true, 0, true, 0) != RecipeTemplateLocationState.ConfirmedMissing) return "FAIL comprehensive missing";
            if (DetermineLocation(1, 0, true, 4, true, 5) != RecipeTemplateLocationState.Inventory) return "FAIL inventory precedence";
            if (DetermineLocation(0, 1, true, 4, true, 5) != RecipeTemplateLocationState.Forge) return "FAIL forge precedence";
            if (DetermineLocation(0, 0, true, 4, true, 5) != RecipeTemplateLocationState.Bank) return "FAIL bank specificity precedence";
            return "PASS recipe template storage policy";
        }
    }
}
