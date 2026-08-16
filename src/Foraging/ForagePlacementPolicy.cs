using System;

namespace ErenshorCraftingExpanded
{
    public struct ForageGroundSample
    {
        public float OutwardDistance;
        public float LateralOffset;

        public ForageGroundSample(float outwardDistance, float lateralOffset)
        {
            OutwardDistance = outwardDistance;
            LateralOffset = lateralOffset;
        }
    }

    // Pure policy used by the runtime wall-anchor placement pass. Unity raycasts/NavMesh queries
    // stay in ForageAutoPlacementTrial; the conservative sample ordering and final-surface gates
    // are deterministic and testable here.
    public static class ForagePlacementPolicy
    {
        public const int DesiredClusterCount = 3;
        public const int MinimumUsefulClusterCount = 1;
        public const float ClusterMinimumSeparation = 18f;

        private static readonly ForageGroundSample[] Samples = new ForageGroundSample[]
        {
            new ForageGroundSample(1.35f, 0f),
            new ForageGroundSample(1.75f, 0f),
            new ForageGroundSample(2.15f, 0f),
            new ForageGroundSample(1.55f, 0.65f),
            new ForageGroundSample(1.55f, -0.65f),
            new ForageGroundSample(2.20f, 0.85f),
            new ForageGroundSample(2.20f, -0.85f),
            new ForageGroundSample(2.80f, 0f)
        };

        private static readonly string[] ForbiddenSurfaceTokens = new string[]
        {
            "road", "path", "trail", "bridge", "door", "gate", "portal", "zoneline", "zone line",
            "stair", "step", "forge", "smith", "bank", "auction", "vendor", "merchant", "chest"
        };

        private static readonly string[] RaisedObstacleTokens = new string[]
        {
            "boulder", "rock", "stone", "cliff", "wall", "pillar", "column", "statue", "stump", "log"
        };

        private static readonly string[] ForbiddenInteractionComponentTokens = new string[]
        {
            "NPC", "Character", "MiningNode", "Zoneline", "ZoneLine", "Door", "Portal",
            "Quest", "Merchant", "Vendor", "Chest", "Loot", "Forge", "Smith", "Bank", "Auction",
            "Interactable", "Interaction"
        };

        public static ForageGroundSample[] GetGroundSamples()
        {
            ForageGroundSample[] copy = new ForageGroundSample[Samples.Length];
            Array.Copy(Samples, copy, Samples.Length);
            return copy;
        }

        public static bool IsUsableClusterCount(int count)
        {
            return count >= MinimumUsefulClusterCount && count <= DesiredClusterCount;
        }


        public static bool IsClusterSeparated(float dx, float dy, float dz)
        {
            if (float.IsNaN(dx) || float.IsInfinity(dx) || float.IsNaN(dy) || float.IsInfinity(dy) || float.IsNaN(dz) || float.IsInfinity(dz)) return false;
            float distanceSquared = dx * dx + dy * dy + dz * dz;
            return distanceSquared >= ClusterMinimumSeparation * ClusterMinimumSeparation;
        }

        public static bool AcceptGroundCandidate(
            float slopeDegrees,
            float horizontalNavOffset,
            float verticalNavOffset,
            bool sameAnchorSurface,
            bool obviousRaisedObstacle,
            bool forbiddenSurface)
        {
            if (float.IsNaN(slopeDegrees) || float.IsInfinity(slopeDegrees) || slopeDegrees < 0f || slopeDegrees > 35f) return false;
            if (float.IsNaN(horizontalNavOffset) || float.IsInfinity(horizontalNavOffset) || horizontalNavOffset < 0f || horizontalNavOffset > 1.10f) return false;
            if (float.IsNaN(verticalNavOffset) || float.IsInfinity(verticalNavOffset) || verticalNavOffset < 0f || verticalNavOffset > 0.65f) return false;
            if (sameAnchorSurface || obviousRaisedObstacle || forbiddenSurface) return false;
            return true;
        }

        public static bool IsForbiddenSurfaceName(string value)
        {
            return ContainsAny(value, ForbiddenSurfaceTokens);
        }

        public static bool IsRaisedObstacleSurfaceName(string value)
        {
            return ContainsAny(value, RaisedObstacleTokens);
        }

        public static bool IsForbiddenInteractionComponentName(string value)
        {
            return ContainsAny(value, ForbiddenInteractionComponentTokens);
        }

        private static bool ContainsAny(string value, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null) return false;
            for (int i = 0; i < tokens.Length; i++)
                if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        internal static string RunSelfTests()
        {
            ForageGroundSample[] samples = GetGroundSamples();
            if (samples.Length < 5) return "FAIL forage placement needs multiple ground samples";
            if (samples[0].OutwardDistance <= 0.7f) return "FAIL first ground sample must move beyond the old wall-surface offset";
            if (samples[samples.Length - 1].OutwardDistance <= samples[0].OutwardDistance) return "FAIL ground samples should fan farther outward";
            bool hasPositiveLateral = false;
            bool hasNegativeLateral = false;
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i].LateralOffset > 0f) hasPositiveLateral = true;
                if (samples[i].LateralOffset < 0f) hasNegativeLateral = true;
            }
            if (!hasPositiveLateral || !hasNegativeLateral) return "FAIL ground samples should probe both sides of the wall normal";

            if (!IsUsableClusterCount(1) || !IsUsableClusterCount(2) || !IsUsableClusterCount(3)) return "FAIL 1-3 safe clusters should be accepted without forcing bad placement";
            if (IsUsableClusterCount(0) || IsUsableClusterCount(4)) return "FAIL cluster count policy should remain bounded to at most three";
            if (!IsClusterSeparated(18f, 0f, 0f)) return "FAIL exact minimum cluster separation rejected";
            if (IsClusterSeparated(17.99f, 0f, 0f)) return "FAIL overlapping forage clusters accepted";
            if (IsClusterSeparated(float.NaN, 0f, 0f)) return "FAIL non-finite cluster separation accepted";

            if (!AcceptGroundCandidate(12f, 0.25f, 0.10f, false, false, false)) return "FAIL ordinary nearby ground rejected";
            if (AcceptGroundCandidate(12f, 0.25f, 0.10f, true, false, false)) return "FAIL top of anchor boulder accepted";
            if (AcceptGroundCandidate(12f, 0.25f, 0.10f, false, true, false)) return "FAIL obvious raised obstacle accepted as ground";
            if (AcceptGroundCandidate(50f, 0.25f, 0.10f, false, false, false)) return "FAIL steep ground accepted";
            if (AcceptGroundCandidate(12f, 2f, 0.10f, false, false, false)) return "FAIL surface too far from reachable NavMesh accepted";
            if (AcceptGroundCandidate(12f, 0.25f, 1.5f, false, false, false)) return "FAIL vertically separated ledge accepted";
            if (!IsForbiddenSurfaceName("Environment/Road_Main")) return "FAIL road surface name should be rejected";
            if (!IsForbiddenSurfaceName("North ZoneLine Portal")) return "FAIL zoneline/portal surface name should be rejected";
            if (IsForbiddenSurfaceName("Terrain_Grass_A")) return "FAIL ordinary terrain surface name rejected";
            if (!IsRaisedObstacleSurfaceName("Stone_Boulder_Large")) return "FAIL stone/boulder ground should be rejected";
            if (!IsRaisedObstacleSurfaceName("Forest_Stump_02")) return "FAIL stump ground should be rejected";
            if (IsRaisedObstacleSurfaceName("Terrain_Grass_A")) return "FAIL ordinary terrain marked as raised obstacle";
            if (!IsForbiddenInteractionComponentName("QuestInteractable")) return "FAIL interactable component should force clearance";
            if (!IsForbiddenInteractionComponentName("MiningNode")) return "FAIL native resource component should force clearance";
            if (IsForbiddenInteractionComponentName("MeshRenderer")) return "FAIL rendering component incorrectly treated as interactable";

            return "PASS forage placement policy";
        }
    }
}
