using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum MaterialSourceKind
    {
        Foraging = 0,
        NativeMining = 1,
        NativeExistingItem = 2
    }

    public sealed class MaterialObtainabilityDefinition
    {
        public string MaterialKey;
        public string DisplayName;
        public string ItemId;
        public MaterialSourceKind Source;
        public string LevelOrEnvironment;
        public string RarityOrDensity;
        public bool Renewable;
        public bool CraftingExpandedShouldCreate;
        public string Evidence;
        public string FutureCraftingPurpose;
    }

    // Cross-workstream read-only map. Foraging-owned materials are generated from the resource
    // catalog. Native entries are evidence notes only: this mod does not reroute native mining,
    // vendors, loot, or existing ItemDB items just to make a recipe convenient.
    public static class ResourceObtainabilityCatalog
    {
        public static List<MaterialObtainabilityDefinition> All()
        {
            List<MaterialObtainabilityDefinition> result = new List<MaterialObtainabilityDefinition>();
            List<ForageResourceDefinition> forage = ForageResourceCatalog.All();
            for (int i = 0; i < forage.Count; i++)
            {
                ForageResourceDefinition resource = forage[i];
                result.Add(new MaterialObtainabilityDefinition
                {
                    MaterialKey = "forage." + resource.KnowledgeKey,
                    DisplayName = resource.DisplayName,
                    ItemId = resource.RewardItemId,
                    Source = MaterialSourceKind.Foraging,
                    LevelOrEnvironment = "Foraging " + resource.MinimumSkill + " / " + resource.Pool,
                    RarityOrDensity = resource.Rarity + " / weight " + resource.DensityWeight +
                        " / scene cap " + resource.MaxAutoNodesPerScene,
                    Renewable = true,
                    CraftingExpandedShouldCreate = true,
                    Evidence = "world={" + resource.VisualEvidenceRequirement + "}; itemDonor={" + resource.ItemDonorEvidenceRequirement + "}; discovery=" + resource.DiscoveryRule,
                    FutureCraftingPurpose = resource.FutureCraftingPurpose
                });
            }

            result.Add(new MaterialObtainabilityDefinition
            {
                MaterialKey = "native.mining_materials",
                DisplayName = "Native mining materials",
                ItemId = string.Empty,
                Source = MaterialSourceKind.NativeMining,
                LevelOrEnvironment = "Native MiningNode/PlayerCombat.TryMine authority",
                RarityOrDensity = "Native-defined",
                Renewable = true,
                CraftingExpandedShouldCreate = false,
                Evidence = "Installed-IL notes prove MiningNode.Mine returns native Item rewards and owns its respawn; this export does not enumerate exact ore tables.",
                FutureCraftingPurpose = "Recipes should consume proven native mining items by their existing IDs instead of cloning them into Foraging."
            });

            result.Add(new MaterialObtainabilityDefinition
            {
                MaterialKey = "native.fernallan_willow_seed",
                DisplayName = "Fernallan Willow Seed",
                ItemId = string.Empty,
                Source = MaterialSourceKind.NativeExistingItem,
                LevelOrEnvironment = "Existing live ItemDB item; acquisition source not proven in this export",
                RarityOrDensity = "Native-defined",
                Renewable = false,
                CraftingExpandedShouldCreate = false,
                Evidence = "Live Wild Herb diagnostics selected it as the safe organic Item visual donor. Donor status is not evidence that Foraging should grant the native item.",
                FutureCraftingPurpose = "May be considered by recipe work only after its real native obtainability is established."
            });

            result.Add(new MaterialObtainabilityDefinition
            {
                MaterialKey = "native.planar_stone",
                DisplayName = "Planar Stone",
                ItemId = "46289586",
                Source = MaterialSourceKind.NativeExistingItem,
                LevelOrEnvironment = "Existing native Smithing/special-combine item",
                RarityOrDensity = "Native-defined",
                Renewable = false,
                CraftingExpandedShouldCreate = false,
                Evidence = "Current repository IL notes identify native item Id 46289586 as Planar Stone in a special Smithing path; acquisition source was not re-investigated here.",
                FutureCraftingPurpose = "Leave native. Do not duplicate as a Foraging material."
            });

            return result;
        }

        public static MaterialObtainabilityDefinition Find(string materialKey)
        {
            if (string.IsNullOrWhiteSpace(materialKey)) return null;
            List<MaterialObtainabilityDefinition> all = All();
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].MaterialKey, materialKey, StringComparison.OrdinalIgnoreCase)) return all[i];
            return null;
        }

        internal static string RunSelfTests()
        {
            List<MaterialObtainabilityDefinition> all = All();
            if (all.Count != ForageResourceCatalog.All().Count + 3) return "FAIL obtainability map size";
            MaterialObtainabilityDefinition herb = Find("forage.wild_herb");
            if (herb == null || herb.Source != MaterialSourceKind.Foraging || !herb.Renewable || !herb.CraftingExpandedShouldCreate)
                return "FAIL Wild Herb obtainability";
            MaterialObtainabilityDefinition mining = Find("native.mining_materials");
            if (mining == null || mining.Source != MaterialSourceKind.NativeMining || mining.CraftingExpandedShouldCreate)
                return "FAIL native mining boundary";
            MaterialObtainabilityDefinition seed = Find("native.fernallan_willow_seed");
            if (seed == null || seed.Source != MaterialSourceKind.NativeExistingItem || seed.CraftingExpandedShouldCreate)
                return "FAIL native donor boundary";
            MaterialObtainabilityDefinition planar = Find("native.planar_stone");
            if (planar == null || planar.ItemId != "46289586" || planar.CraftingExpandedShouldCreate)
                return "FAIL native Planar Stone boundary";
            return "PASS resource obtainability catalog";
        }
    }
}
