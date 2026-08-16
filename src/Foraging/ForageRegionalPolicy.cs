using System;

namespace ErenshorCraftingExpanded
{
    public enum ForageRegionalRule
    {
        AnyScene = 0,
        ExplicitScenes = 1
    }

    public static class ForageRegionalPolicy
    {
        public static bool IsEligible(ForageResourceDefinition resource, string scene)
        {
            if (resource == null) return false;
            if (resource.RegionalRule == ForageRegionalRule.AnyScene) return true;
            if (resource.EligibleScenes == null || resource.EligibleScenes.Length == 0 || string.IsNullOrWhiteSpace(scene)) return false;
            for (int i = 0; i < resource.EligibleScenes.Length; i++)
                if (string.Equals(resource.EligibleScenes[i], scene, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal static string RunSelfTests()
        {
            ForageResourceDefinition any = new ForageResourceDefinition();
            any.RegionalRule = ForageRegionalRule.AnyScene;
            if (!IsEligible(any, "Hidden Hills")) return "FAIL any-scene regional rule";
            ForageResourceDefinition regional = new ForageResourceDefinition();
            regional.RegionalRule = ForageRegionalRule.ExplicitScenes;
            regional.EligibleScenes = new string[] { "Hidden Hills", "Test Cave" };
            if (!IsEligible(regional, "hidden hills")) return "FAIL explicit regional match";
            if (IsEligible(regional, "Other")) return "FAIL explicit regional leak";
            return "PASS forage regional policy";
        }
    }
}
