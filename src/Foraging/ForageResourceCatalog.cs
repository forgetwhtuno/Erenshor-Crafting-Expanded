using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum ForageResourceKind
    {
        WildHerb = 0,
        CaveMushroom = 1,
        WildBloom = 2,
        CaveMoss = 3,
        Blightroot = 4
    }

    public enum ForageResourceRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2
    }

    public enum ForageDiscoveryRule
    {
        FirstSuccessfulGather = 0
    }

    public sealed class ForageResourceDefinition
    {
        public ForageResourceKind Kind;
        public ForageResourcePool Pool;
        public string KnowledgeKey;
        public string NodeIdPrefix;
        public string DisplayName;
        public string RewardItemId;
        public float RespawnSeconds;
        public int MinimumSkill;
        public int GatherXp;
        public int BaseYield;
        public ForageResourceRarity Rarity;

        // Cave/covered experimentation remains explicitly opt-in until live covered placement has
        // its own evidence. Open evidence-gated families are normal catalog entries, but still
        // require an exact matching native Item donor and exact matching current-scene mesh.
        public bool Experimental;
        public bool EnabledByDefault;

        public ForageRegionalRule RegionalRule;
        public string[] EligibleScenes;

        // Small economy controls. Total scene placement remains bounded to 1-3 nodes by the
        // existing placement policy; these values only decide which proven family occupies them.
        public int DensityWeight;
        public int MaxAutoNodesPerScene;

        // Presentation remains one mechanical node. These only vary the bounded cloned clump
        // presentation; Wild Herb values preserve the already-tested baseline.
        public int VisualClumpCount;
        public float VisualScaleMultiplier;
        public float VisualSpreadMultiplier;

        public string VisualEvidenceRequirement;
        public string ItemDonorEvidenceRequirement;
        public ForageDiscoveryRule DiscoveryRule;
        public string FutureCraftingPurpose;
    }

    // Compact gathering economy. A definition is not enough to make a world node: runtime
    // placement additionally requires environment + regional eligibility, the registered custom
    // item, and an explicitly matching current-scene visual family.
    public static class ForageResourceCatalog
    {
        private static readonly ForageResourceDefinition WildHerb = new ForageResourceDefinition
        {
            Kind = ForageResourceKind.WildHerb,
            Pool = ForageResourcePool.OpenHerbs,
            KnowledgeKey = "wild_herb",
            NodeIdPrefix = "AutoHerb_",
            DisplayName = "Wild Herb",
            RewardItemId = CraftingExpandedItemIds.WildHerbId,
            RespawnSeconds = 300f,
            MinimumSkill = 1,
            GatherXp = 20,
            BaseYield = 1,
            Rarity = ForageResourceRarity.Common,
            Experimental = false,
            EnabledByDefault = true,
            RegionalRule = ForageRegionalRule.AnyScene,
            EligibleScenes = new string[0],
            DensityWeight = 10,
            MaxAutoNodesPerScene = 3,
            VisualClumpCount = 3,
            VisualScaleMultiplier = 1.00f,
            VisualSpreadMultiplier = 1.00f,
            VisualEvidenceRequirement = "safe plant/herb/fern/bush scene mesh; TFF_Bush_01A is live-proven",
            ItemDonorEvidenceRequirement = "safe organic ItemDB donor; Fernallan Willow Seed is live-proven",
            DiscoveryRule = ForageDiscoveryRule.FirstSuccessfulGather,
            FutureCraftingPurpose = "baseline herbal preparations and simple restorative recipes"
        };

        private static readonly ForageResourceDefinition CaveMushroom = new ForageResourceDefinition
        {
            Kind = ForageResourceKind.CaveMushroom,
            Pool = ForageResourcePool.CoveredFungi,
            KnowledgeKey = "cave_mushroom",
            NodeIdPrefix = "AutoFungus_",
            DisplayName = "Cave Mushroom",
            RewardItemId = CraftingExpandedItemIds.CaveMushroomId,
            RespawnSeconds = 420f,
            MinimumSkill = 8,
            GatherXp = 32,
            BaseYield = 1,
            Rarity = ForageResourceRarity.Uncommon,
            Experimental = true,
            EnabledByDefault = false,
            RegionalRule = ForageRegionalRule.AnyScene,
            EligibleScenes = new string[0],
            DensityWeight = 6,
            MaxAutoNodesPerScene = 2,
            VisualClumpCount = 3,
            VisualScaleMultiplier = 0.85f,
            VisualSpreadMultiplier = 0.80f,
            VisualEvidenceRequirement = "explicit mushroom/toadstool/fungus/fungi/spore scene mesh",
            ItemDonorEvidenceRequirement = "safe ItemDB donor with explicit mushroom/toadstool/fungus/fungi/spore/truffle name evidence",
            DiscoveryRule = ForageDiscoveryRule.FirstSuccessfulGather,
            FutureCraftingPurpose = "cave tonics, alchemical reagents, and dungeon-focused consumables"
        };

        private static readonly ForageResourceDefinition WildBloom = new ForageResourceDefinition
        {
            Kind = ForageResourceKind.WildBloom,
            Pool = ForageResourcePool.OpenFlowers,
            KnowledgeKey = "wild_bloom",
            NodeIdPrefix = "AutoBloom_",
            DisplayName = "Wild Bloom",
            RewardItemId = CraftingExpandedItemIds.WildBloomId,
            RespawnSeconds = 360f,
            MinimumSkill = 14,
            GatherXp = 38,
            BaseYield = 1,
            Rarity = ForageResourceRarity.Uncommon,
            Experimental = false,
            EnabledByDefault = true,
            RegionalRule = ForageRegionalRule.AnyScene,
            EligibleScenes = new string[0],
            DensityWeight = 4,
            MaxAutoNodesPerScene = 1,
            VisualClumpCount = 3,
            VisualScaleMultiplier = 0.95f,
            VisualSpreadMultiplier = 0.90f,
            VisualEvidenceRequirement = "explicit flower/blossom/bloom/petal current-scene mesh",
            ItemDonorEvidenceRequirement = "safe ItemDB donor with explicit flower/blossom/bloom/petal name evidence",
            DiscoveryRule = ForageDiscoveryRule.FirstSuccessfulGather,
            FutureCraftingPurpose = "pigments, restorative preparations, and light utility elixirs"
        };

        private static readonly ForageResourceDefinition CaveMoss = new ForageResourceDefinition
        {
            Kind = ForageResourceKind.CaveMoss,
            Pool = ForageResourcePool.CoveredMoss,
            KnowledgeKey = "cave_moss",
            NodeIdPrefix = "AutoMoss_",
            DisplayName = "Cave Moss",
            RewardItemId = CraftingExpandedItemIds.CaveMossId,
            RespawnSeconds = 540f,
            MinimumSkill = 24,
            GatherXp = 52,
            BaseYield = 1,
            Rarity = ForageResourceRarity.Rare,
            Experimental = true,
            EnabledByDefault = false,
            RegionalRule = ForageRegionalRule.AnyScene,
            EligibleScenes = new string[0],
            DensityWeight = 3,
            MaxAutoNodesPerScene = 1,
            VisualClumpCount = 3,
            VisualScaleMultiplier = 0.75f,
            VisualSpreadMultiplier = 0.85f,
            VisualEvidenceRequirement = "explicit moss/lichen current-scene mesh",
            ItemDonorEvidenceRequirement = "safe ItemDB donor with explicit moss/lichen name evidence",
            DiscoveryRule = ForageDiscoveryRule.FirstSuccessfulGather,
            FutureCraftingPurpose = "protective salves, binding agents, and higher-tier dungeon remedies"
        };

        private static readonly ForageResourceDefinition Blightroot = new ForageResourceDefinition
        {
            Kind = ForageResourceKind.Blightroot,
            Pool = ForageResourcePool.OpenRoots,
            KnowledgeKey = "blightroot",
            NodeIdPrefix = "AutoBlightroot_",
            DisplayName = "Blightroot",
            RewardItemId = CraftingExpandedItemIds.BlightrootId,
            RespawnSeconds = 660f,
            MinimumSkill = 36,
            GatherXp = 68,
            BaseYield = 1,
            Rarity = ForageResourceRarity.Rare,
            Experimental = false,
            EnabledByDefault = true,
            RegionalRule = ForageRegionalRule.ExplicitScenes,
            EligibleScenes = new string[] { "The Blight" },
            DensityWeight = 2,
            MaxAutoNodesPerScene = 1,
            VisualClumpCount = 3,
            VisualScaleMultiplier = 1.00f,
            VisualSpreadMultiplier = 0.95f,
            VisualEvidenceRequirement = "explicit root/rhizome/briar/bramble/vine/thorn current-scene mesh in The Blight",
            ItemDonorEvidenceRequirement = "safe ItemDB donor with explicit root/rhizome/briar/bramble/vine/thorn name evidence",
            DiscoveryRule = ForageDiscoveryRule.FirstSuccessfulGather,
            FutureCraftingPurpose = "high-tier regional reagents and difficult late-progression preparations"
        };

        public static ForageResourceDefinition ForEnvironment(ForageEnvironmentKind environment, bool coveredResourcesEnabled)
        {
            if (environment == ForageEnvironmentKind.Covered)
                return coveredResourcesEnabled ? CaveMushroom : null;
            return WildHerb;
        }

        public static List<ForageResourceDefinition> ForEnvironmentAll(
            ForageEnvironmentKind environment,
            string scene,
            bool coveredResourcesEnabled)
        {
            List<ForageResourceDefinition> result = new List<ForageResourceDefinition>();
            List<ForageResourceDefinition> all = All();
            for (int i = 0; i < all.Count; i++)
            {
                ForageResourceDefinition resource = all[i];
                if (!IsRuntimeEnabled(resource, coveredResourcesEnabled)) continue;
                if (!ForageEnvironmentPolicy.IsPoolCompatible(environment, resource.Pool)) continue;
                if (!ForageRegionalPolicy.IsEligible(resource, scene)) continue;
                result.Add(resource);
            }
            return result;
        }

        public static ForageResourceDefinition ForPool(ForageResourcePool pool, bool coveredResourcesEnabled)
        {
            List<ForageResourceDefinition> all = All();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Pool == pool && IsRuntimeEnabled(all[i], coveredResourcesEnabled)) return all[i];
            return null;
        }

        public static bool IsRuntimeEnabled(ForageResourceDefinition resource, bool coveredResourcesEnabled)
        {
            if (resource == null) return false;
            if (resource.Experimental) return coveredResourcesEnabled;
            return resource.EnabledByDefault;
        }

        public static ForageResourceDefinition FindByRewardItemId(string itemId)
        {
            List<ForageResourceDefinition> all = All();
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(itemId, all[i].RewardItemId, StringComparison.Ordinal)) return all[i];
            return null;
        }

        public static ForageResourceDefinition FindByKnowledgeKey(string resourceKey)
        {
            string key = ForagingKnowledgeState.NormalizeKey(resourceKey);
            List<ForageResourceDefinition> all = All();
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(key, all[i].KnowledgeKey, StringComparison.Ordinal)) return all[i];
            return null;
        }

        public static List<ForageResourceDefinition> All()
        {
            return new List<ForageResourceDefinition>
            {
                WildHerb,
                CaveMushroom,
                WildBloom,
                CaveMoss,
                Blightroot
            };
        }

        public static bool IsGatherableAtSkill(ForageResourceDefinition resource, int foragingLevel)
        {
            if (resource == null) return false;
            if (foragingLevel < 1) foragingLevel = 1;
            return foragingLevel >= resource.MinimumSkill;
        }

        public static bool ValidateDefinition(ForageResourceDefinition resource, out string reason)
        {
            reason = string.Empty;
            if (resource == null) { reason = "resource=null"; return false; }
            if (string.IsNullOrWhiteSpace(resource.KnowledgeKey)) { reason = "knowledge key missing"; return false; }
            if (string.IsNullOrWhiteSpace(resource.NodeIdPrefix)) { reason = "node id prefix missing"; return false; }
            if (string.IsNullOrWhiteSpace(resource.DisplayName)) { reason = "display name missing"; return false; }
            if (string.IsNullOrWhiteSpace(resource.RewardItemId) || !CraftingExpandedItemIds.IsInOwnedRange(resource.RewardItemId))
            { reason = "reward item id outside owned range"; return false; }
            if (resource.MinimumSkill < 1 || resource.MinimumSkill > 50) { reason = "minimum skill outside 1-50"; return false; }
            if (resource.GatherXp <= 0 || resource.GatherXp > 500) { reason = "gather xp invalid"; return false; }
            if (resource.BaseYield < 1 || resource.BaseYield > 3) { reason = "base yield invalid"; return false; }
            if (float.IsNaN(resource.RespawnSeconds) || float.IsInfinity(resource.RespawnSeconds) ||
                resource.RespawnSeconds < 30f || resource.RespawnSeconds > 7200f)
            { reason = "respawn invalid"; return false; }
            if (resource.DensityWeight < 1 || resource.DensityWeight > 100) { reason = "density weight invalid"; return false; }
            if (resource.MaxAutoNodesPerScene < 1 || resource.MaxAutoNodesPerScene > ForagePlacementPolicy.DesiredClusterCount)
            { reason = "auto-node cap invalid"; return false; }
            if (resource.VisualClumpCount < 2 || resource.VisualClumpCount > ForagePresentationPolicy.PreferredClusterClumpCount)
            { reason = "visual clump count invalid"; return false; }
            if (float.IsNaN(resource.VisualScaleMultiplier) || float.IsInfinity(resource.VisualScaleMultiplier) ||
                resource.VisualScaleMultiplier < 0.5f || resource.VisualScaleMultiplier > 1.5f)
            { reason = "visual scale invalid"; return false; }
            if (float.IsNaN(resource.VisualSpreadMultiplier) || float.IsInfinity(resource.VisualSpreadMultiplier) ||
                resource.VisualSpreadMultiplier < 0.5f || resource.VisualSpreadMultiplier > 1.5f)
            { reason = "visual spread invalid"; return false; }
            if (resource.RegionalRule == ForageRegionalRule.ExplicitScenes &&
                (resource.EligibleScenes == null || resource.EligibleScenes.Length == 0))
            { reason = "explicit regional rule has no scenes"; return false; }
            if (string.IsNullOrWhiteSpace(resource.VisualEvidenceRequirement)) { reason = "visual evidence requirement missing"; return false; }
            if (string.IsNullOrWhiteSpace(resource.ItemDonorEvidenceRequirement)) { reason = "item donor evidence requirement missing"; return false; }
            if (resource.DiscoveryRule != ForageDiscoveryRule.FirstSuccessfulGather) { reason = "unsupported discovery rule"; return false; }
            return true;
        }

        internal static string RunSelfTests()
        {
            List<ForageResourceDefinition> all = All();
            if (all.Count != 5) return "FAIL expected compact five-resource catalog";

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < all.Count; i++)
            {
                string reason;
                if (!ValidateDefinition(all[i], out reason)) return "FAIL catalog definition " + all[i].DisplayName + ": " + reason;
                if (!keys.Add(all[i].KnowledgeKey)) return "FAIL duplicate resource key";
                if (!ids.Add(all[i].RewardItemId)) return "FAIL duplicate resource item id";
                if (all[i].BaseYield != 1) return "FAIL first economy should keep every gather yield at one";
                if (all[i].DiscoveryRule != ForageDiscoveryRule.FirstSuccessfulGather) return "FAIL discovery must remain first successful gather";
            }

            ForageResourceDefinition open = ForEnvironment(ForageEnvironmentKind.Open, false);
            if (open == null || open.Kind != ForageResourceKind.WildHerb || open.RewardItemId != CraftingExpandedItemIds.WildHerbId)
                return "FAIL open baseline resource definition";
            if (open.MinimumSkill != 1 || open.GatherXp != 20 || open.KnowledgeKey != "wild_herb")
                return "FAIL Wild Herb progression metadata";
            if (ForEnvironment(ForageEnvironmentKind.Covered, false) != null)
                return "FAIL covered experimental baseline should fail closed while disabled";
            ForageResourceDefinition covered = ForEnvironment(ForageEnvironmentKind.Covered, true);
            if (covered == null || covered.Kind != ForageResourceKind.CaveMushroom)
                return "FAIL Cave Mushroom experimental baseline";

            ForageResourceDefinition bloom = FindByKnowledgeKey("Wild Bloom");
            ForageResourceDefinition moss = FindByRewardItemId(CraftingExpandedItemIds.CaveMossId);
            ForageResourceDefinition root = FindByKnowledgeKey("blightroot");
            if (bloom == null || bloom.MinimumSkill != 14 || bloom.Pool != ForageResourcePool.OpenFlowers)
                return "FAIL Wild Bloom catalog";
            if (moss == null || moss.MinimumSkill != 24 || !moss.Experimental || moss.Pool != ForageResourcePool.CoveredMoss)
                return "FAIL Cave Moss catalog";
            if (root == null || root.MinimumSkill != 36 || root.RegionalRule != ForageRegionalRule.ExplicitScenes)
                return "FAIL Blightroot catalog";
            if (!ForageRegionalPolicy.IsEligible(root, "The Blight") || ForageRegionalPolicy.IsEligible(root, "Hidden Hills"))
                return "FAIL Blightroot regional boundary";

            List<ForageResourceDefinition> hidden = ForEnvironmentAll(ForageEnvironmentKind.Open, "Hidden Hills", false);
            if (hidden.Count != 2) return "FAIL Hidden Hills open catalog should contain Herb + evidence-gated Bloom only";
            List<ForageResourceDefinition> blight = ForEnvironmentAll(ForageEnvironmentKind.Open, "The Blight", false);
            if (blight.Count != 3) return "FAIL The Blight should additionally admit Blightroot";
            if (ForEnvironmentAll(ForageEnvironmentKind.Covered, "Cave", false).Count != 0)
                return "FAIL covered resources leaked while experimental gate off";
            if (ForEnvironmentAll(ForageEnvironmentKind.Covered, "Cave", true).Count != 2)
                return "FAIL covered experimental pool should contain Mushroom + Moss";

            if (!IsGatherableAtSkill(open, 1) || IsGatherableAtSkill(bloom, 13) || !IsGatherableAtSkill(bloom, 14))
                return "FAIL resource skill gates";

            ForageResourceDefinition malformed = new ForageResourceDefinition();
            string malformedReason;
            if (ValidateDefinition(malformed, out malformedReason)) return "FAIL malformed catalog entry accepted";
            if (string.IsNullOrEmpty(malformedReason)) return "FAIL malformed catalog rejection lacks reason";

            return "PASS forage resource catalog";
        }
    }
}
