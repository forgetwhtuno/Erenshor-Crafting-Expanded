namespace ErenshorCraftingExpanded
{
    // Pure final admission gate for an auto-placed resource family. Placement coordinates are
    // established elsewhere. This policy exists so missing ItemDB donor/scene-mesh evidence and
    // environment/regional gates are deterministic testable reasons instead of controller-only
    // branches.
    public static class ForageResourceAvailabilityPolicy
    {
        public static bool CanAutoSpawn(
            ForageResourceDefinition resource,
            ForageEnvironmentKind environment,
            string scene,
            bool coveredResourcesEnabled,
            bool itemAvailable,
            bool visualAvailable,
            out string reason)
        {
            reason = string.Empty;
            if (resource == null) { reason = "resource-missing"; return false; }
            if (!ForageResourceCatalog.IsRuntimeEnabled(resource, coveredResourcesEnabled))
            { reason = "resource-disabled"; return false; }
            if (!ForageEnvironmentPolicy.IsPoolCompatible(environment, resource.Pool))
            { reason = "wrong-environment"; return false; }
            if (!ForageRegionalPolicy.IsEligible(resource, scene))
            { reason = "wrong-region"; return false; }
            if (!itemAvailable)
            { reason = "item-donor-unavailable"; return false; }
            if (!visualAvailable)
            { reason = "scene-visual-unavailable"; return false; }
            return true;
        }

        internal static string RunSelfTests()
        {
            ForageResourceDefinition herb = ForageResourceCatalog.FindByKnowledgeKey("wild_herb");
            ForageResourceDefinition fungus = ForageResourceCatalog.FindByKnowledgeKey("cave_mushroom");
            ForageResourceDefinition bloom = ForageResourceCatalog.FindByKnowledgeKey("wild_bloom");
            ForageResourceDefinition root = ForageResourceCatalog.FindByKnowledgeKey("blightroot");
            if (herb == null || fungus == null || bloom == null || root == null)
                return "FAIL availability test catalog";

            string reason;
            if (!CanAutoSpawn(herb, ForageEnvironmentKind.Open, "Hidden Hills", false, true, true, out reason))
                return "FAIL proven Wild Herb should be spawnable: " + reason;
            if (CanAutoSpawn(bloom, ForageEnvironmentKind.Open, "Hidden Hills", false, false, true, out reason) || reason != "item-donor-unavailable")
                return "FAIL missing ItemDB donor did not fail closed";
            if (CanAutoSpawn(bloom, ForageEnvironmentKind.Open, "Hidden Hills", false, true, false, out reason) || reason != "scene-visual-unavailable")
                return "FAIL missing scene mesh did not fail closed";
            if (CanAutoSpawn(bloom, ForageEnvironmentKind.Covered, "Hidden Hills", false, true, true, out reason) || reason != "wrong-environment")
                return "FAIL flower admitted to covered point";
            if (CanAutoSpawn(root, ForageEnvironmentKind.Open, "Hidden Hills", false, true, true, out reason) || reason != "wrong-region")
                return "FAIL Blightroot admitted outside The Blight";
            if (!CanAutoSpawn(root, ForageEnvironmentKind.Open, "The Blight", false, true, true, out reason))
                return "FAIL proven Blightroot rejected in The Blight: " + reason;
            if (CanAutoSpawn(fungus, ForageEnvironmentKind.Covered, "Some Cave", false, true, true, out reason) || reason != "resource-disabled")
                return "FAIL covered experiment leaked while disabled";
            if (!CanAutoSpawn(fungus, ForageEnvironmentKind.Covered, "Some Cave", true, true, true, out reason))
                return "FAIL proven covered fungus rejected while experiment enabled: " + reason;

            return "PASS forage resource availability policy";
        }
    }
}
