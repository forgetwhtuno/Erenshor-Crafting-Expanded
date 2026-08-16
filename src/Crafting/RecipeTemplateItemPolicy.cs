namespace ErenshorCraftingExpanded
{
    public static class RecipeTemplateItemPolicy
    {
        public const int SafeVendorValue = 0;
        public const bool PlayerCannotSell = true;
        public const bool NoTradeNoDestroy = true;
        // Intentionally false as a POLICY DECISION, not a mutation value. Current evidence does
        // not establish native Unique semantics across inventory/bank/AH/trade, so this ownership
        // pass does not rely on or change Unique.
        public const bool RelyOnUnique = false;

        public static string FormatTemplateName(string recipeDisplayName)
        {
            string value = (recipeDisplayName ?? string.Empty).Trim();
            if (value.StartsWith("Recipe: ", System.StringComparison.OrdinalIgnoreCase)) value = value.Substring(8).Trim();
            return string.IsNullOrEmpty(value) ? "Recipe: Unknown" : "Recipe: " + value;
        }

        internal static string RunSelfTests()
        {
            if (FormatTemplateName("Herbal Preparation") != "Recipe: Herbal Preparation") return "FAIL recipe template naming";
            if (FormatTemplateName("Recipe: Herbal Preparation") != "Recipe: Herbal Preparation") return "FAIL recipe template double prefix";
            if (SafeVendorValue != 0 || !PlayerCannotSell || !NoTradeNoDestroy || RelyOnUnique) return "FAIL recipe template safety constants";
            return "PASS recipe template item policy";
        }
    }
}
