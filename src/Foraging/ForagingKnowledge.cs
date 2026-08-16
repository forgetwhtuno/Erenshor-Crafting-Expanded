using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public sealed class ForagingResourceKnowledgeSnapshot
    {
        public string Key;
        public string DisplayName;
        public int MinimumSkill;
        public int GatherXp;
        public bool Discovered;
        public bool MeetsSkillRequirement;
        public bool Experimental;
        public string Environment;
        public string Rarity;
        public string RegionalRequirement;
        public string FutureCraftingPurpose;
        public string VisualEvidenceRequirement;
        public string ItemDonorEvidenceRequirement;
        public string DiscoveryRule;
    }

    public sealed class ForagingKnowledgeSnapshot
    {
        public int Level;
        public int Xp;
        public int XpToNext;
        public List<ForagingResourceKnowledgeSnapshot> Resources = new List<ForagingResourceKnowledgeSnapshot>();
    }

    // Clean mod-owned integration seam for the parallel recipe/template and panel workstreams.
    // It intentionally exposes knowledge/skill only - no ItemDatabase or native recipe mutation.
    public static class ForagingKnowledge
    {
        public static int CurrentLevel { get { return ForagingProgressionController.CurrentLevel; } }
        public static int CurrentXp { get { return ForagingProgressionController.CurrentXp; } }
        public static int XpToNext { get { return ForagingXpCurve.XpToNextLevel(CurrentLevel); } }
        public static bool IsReady { get { return ForagingProgressionController.IsReady; } }

        public static bool HasDiscovered(string resourceKey)
        {
            return ForagingProgressionController.HasDiscovered(resourceKey);
        }

        public static bool MeetsTemplateRequirement(string resourceKey, int minimumForagingLevel)
        {
            if (!IsReady) return false;
            if (minimumForagingLevel < 1) minimumForagingLevel = 1;
            return CurrentLevel >= minimumForagingLevel && HasDiscovered(resourceKey);
        }

        public static ForagingKnowledgeSnapshot GetSnapshot()
        {
            ForagingKnowledgeSnapshot snapshot = new ForagingKnowledgeSnapshot();
            snapshot.Level = CurrentLevel;
            snapshot.Xp = CurrentXp;
            snapshot.XpToNext = XpToNext;
            List<ForageResourceDefinition> resources = ForageResourceCatalog.All();
            for (int i = 0; i < resources.Count; i++)
            {
                ForageResourceDefinition resource = resources[i];
                snapshot.Resources.Add(new ForagingResourceKnowledgeSnapshot
                {
                    Key = resource.KnowledgeKey,
                    DisplayName = resource.DisplayName,
                    MinimumSkill = resource.MinimumSkill,
                    GatherXp = resource.GatherXp,
                    Discovered = HasDiscovered(resource.KnowledgeKey),
                    MeetsSkillRequirement = IsReady && CurrentLevel >= resource.MinimumSkill,
                    Experimental = resource.Experimental,
                    Environment = resource.Pool.ToString(),
                    Rarity = resource.Rarity.ToString(),
                    RegionalRequirement = resource.RegionalRule == ForageRegionalRule.ExplicitScenes
                        ? string.Join(", ", resource.EligibleScenes ?? new string[0])
                        : "Any eligible scene",
                    FutureCraftingPurpose = resource.FutureCraftingPurpose ?? string.Empty,
                    VisualEvidenceRequirement = resource.VisualEvidenceRequirement ?? string.Empty,
                    ItemDonorEvidenceRequirement = resource.ItemDonorEvidenceRequirement ?? string.Empty,
                    DiscoveryRule = resource.DiscoveryRule.ToString()
                });
            }
            return snapshot;
        }
    }
}
