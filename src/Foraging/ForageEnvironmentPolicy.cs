namespace ErenshorCraftingExpanded
{
    public enum ForageEnvironmentKind
    {
        Open = 0,
        Covered = 1
    }

    // Resource pools describe visual/economy families, not world zones. Environment classification
    // remains deliberately small: runtime placement proves Open vs Covered, then regional/catalog
    // policy decides which resource families may compete for that point.
    public enum ForageResourcePool
    {
        OpenHerbs = 0,
        CoveredFungi = 1,
        OpenFlowers = 2,
        CoveredMoss = 3,
        OpenRoots = 4
    }

    public static class ForageEnvironmentPolicy
    {
        public static ForageEnvironmentKind Classify(bool covered)
        {
            return covered ? ForageEnvironmentKind.Covered : ForageEnvironmentKind.Open;
        }

        public static bool IsCoveredEvidence(bool overheadHit, float overheadDistance, string overheadName)
        {
            if (!overheadHit || float.IsNaN(overheadDistance) || float.IsInfinity(overheadDistance) || overheadDistance < 0f || overheadDistance > 10f) return false;
            string text = (overheadName ?? string.Empty).ToLowerInvariant();
            string[] openCanopyTokens = new string[] { "tree", "branch", "leaf", "leaves", "foliage", "canopy", "bush", "plant" };
            for (int i = 0; i < openCanopyTokens.Length; i++)
                if (text.IndexOf(openCanopyTokens[i], System.StringComparison.Ordinal) >= 0) return false;
            return true;
        }

        // Legacy primary-pool helper retained for callers/tests that only need the first family.
        public static ForageResourcePool ResourcePoolFor(ForageEnvironmentKind environment)
        {
            return environment == ForageEnvironmentKind.Covered
                ? ForageResourcePool.CoveredFungi
                : ForageResourcePool.OpenHerbs;
        }

        public static bool IsPoolCompatible(ForageEnvironmentKind environment, ForageResourcePool pool)
        {
            if (environment == ForageEnvironmentKind.Covered)
                return pool == ForageResourcePool.CoveredFungi || pool == ForageResourcePool.CoveredMoss;
            return pool == ForageResourcePool.OpenHerbs ||
                pool == ForageResourcePool.OpenFlowers ||
                pool == ForageResourcePool.OpenRoots;
        }

        public static ForageResourcePool[] AllPools()
        {
            return new ForageResourcePool[]
            {
                ForageResourcePool.OpenHerbs,
                ForageResourcePool.CoveredFungi,
                ForageResourcePool.OpenFlowers,
                ForageResourcePool.CoveredMoss,
                ForageResourcePool.OpenRoots
            };
        }

        internal static string RunSelfTests()
        {
            if (Classify(false) != ForageEnvironmentKind.Open) return "FAIL open environment classification";
            if (Classify(true) != ForageEnvironmentKind.Covered) return "FAIL covered environment classification";
            if (ResourcePoolFor(ForageEnvironmentKind.Open) != ForageResourcePool.OpenHerbs) return "FAIL open primary resource pool";
            if (ResourcePoolFor(ForageEnvironmentKind.Covered) != ForageResourcePool.CoveredFungi) return "FAIL covered primary resource pool";
            if (!IsPoolCompatible(ForageEnvironmentKind.Open, ForageResourcePool.OpenFlowers)) return "FAIL flowers should be open-compatible";
            if (!IsPoolCompatible(ForageEnvironmentKind.Open, ForageResourcePool.OpenRoots)) return "FAIL roots should be open-compatible";
            if (!IsPoolCompatible(ForageEnvironmentKind.Covered, ForageResourcePool.CoveredMoss)) return "FAIL moss should be covered-compatible";
            if (IsPoolCompatible(ForageEnvironmentKind.Covered, ForageResourcePool.OpenHerbs)) return "FAIL open herb leaked into covered point";
            if (IsPoolCompatible(ForageEnvironmentKind.Open, ForageResourcePool.CoveredFungi)) return "FAIL fungus leaked into open point";
            if (!IsCoveredEvidence(true, 3f, "Cave_Ceiling")) return "FAIL nearby cave ceiling should count as covered";
            if (IsCoveredEvidence(true, 3f, "Tree_Branch_Canopy")) return "FAIL tree canopy should not turn outdoor ground into cave resource ground";
            if (IsCoveredEvidence(false, 0f, "Cave_Ceiling")) return "FAIL missing overhead hit counted as covered";
            if (IsCoveredEvidence(true, 12f, "Cave_Ceiling")) return "FAIL distant overhead geometry counted as local cover";
            return "PASS forage environment policy";
        }
    }
}
