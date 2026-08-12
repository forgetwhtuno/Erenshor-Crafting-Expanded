using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // Pure helper for the development-only forage asset survey. Unity object ancestry checks
    // stay in ForagingAssetScanApi, but string/type filtering and size ranking live here so the
    // scanner's most important heuristics can be regression-tested without the game.
    public static class ForagingScanPolicy
    {
        public static bool IsEffectRendererType(string rendererTypeName)
        {
            return string.Equals(rendererTypeName, "ParticleSystemRenderer", StringComparison.Ordinal) ||
                   string.Equals(rendererTypeName, "TrailRenderer", StringComparison.Ordinal) ||
                   string.Equals(rendererTypeName, "LineRenderer", StringComparison.Ordinal);
        }

        // Smaller world meshes are more useful candidates for a herb/grass/bush visual than a
        // whole terrain chunk or building. This is a ranking preference, not a rejection gate:
        // large meshes still appear after smaller candidates if they are otherwise valid.
        public static int SizeRank(float largestBoundsDimension)
        {
            if (float.IsNaN(largestBoundsDimension) || float.IsInfinity(largestBoundsDimension) || largestBoundsDimension < 0f)
                return 4;
            if (largestBoundsDimension <= 4f) return 0;   // herb / flower / mushroom / small bush scale
            if (largestBoundsDimension <= 12f) return 1;  // larger shrub / small tree
            if (largestBoundsDimension <= 30f) return 2;  // tree / structure-sized, still inspectable
            return 3;
        }

        public static bool MatchesFilter(
            string hierarchyPath,
            string gameObjectName,
            string meshName,
            IEnumerable<string> materialNames,
            IEnumerable<string> shaderNames,
            string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            string needle = filter.Trim();
            if (ContainsIgnoreCase(hierarchyPath, needle) ||
                ContainsIgnoreCase(gameObjectName, needle) ||
                ContainsIgnoreCase(meshName, needle)) return true;

            if (ContainsAnyIgnoreCase(materialNames, needle)) return true;
            if (ContainsAnyIgnoreCase(shaderNames, needle)) return true;
            return false;
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack) &&
                haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAnyIgnoreCase(IEnumerable<string> haystacks, string needle)
        {
            if (haystacks == null) return false;
            foreach (string haystack in haystacks)
                if (ContainsIgnoreCase(haystack, needle)) return true;
            return false;
        }

        internal static string RunSelfTests()
        {
            if (!IsEffectRendererType("ParticleSystemRenderer")) return "FAIL particle renderer should be excluded";
            if (!IsEffectRendererType("TrailRenderer")) return "FAIL trail renderer should be excluded";
            if (IsEffectRendererType("MeshRenderer")) return "FAIL ordinary mesh renderer marked as effect";

            if (SizeRank(2f) != 0) return "FAIL small mesh should rank first";
            if (SizeRank(8f) != 1) return "FAIL shrub/small-tree mesh size rank";
            if (SizeRank(20f) != 2) return "FAIL medium/structure mesh size rank";
            if (SizeRank(50f) != 3) return "FAIL large mesh size rank";
            if (SizeRank(float.NaN) != 4) return "FAIL invalid bounds size rank";

            string[] materials = { "ForestPlant_Mat" };
            string[] shaders = { "Nature/Leaf" };
            if (!MatchesFilter("Environment/Vegetation/Fern_A", "Fern_A", "FernMesh", materials, shaders, "fern"))
                return "FAIL hierarchy/name/mesh filter";
            if (!MatchesFilter("Environment/Vegetation/Fern_A", "Fern_A", "FernMesh", materials, shaders, "forestplant"))
                return "FAIL material filter";
            if (!MatchesFilter("Environment/Vegetation/Fern_A", "Fern_A", "FernMesh", materials, shaders, "nature/leaf"))
                return "FAIL shader filter";
            if (MatchesFilter("Environment/Vegetation/Fern_A", "Fern_A", "FernMesh", materials, shaders, "eyebrow"))
                return "FAIL unrelated filter matched";
            if (!MatchesFilter("x", "y", "z", materials, shaders, "")) return "FAIL empty filter should accept";

            return "PASS foraging scan policy";
        }
    }
}
