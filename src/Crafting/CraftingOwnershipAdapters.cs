using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // Bridges the recipe-ownership layer to already-proven Crafting/Foraging character state.
    // These are intra-plugin adapters only; they do not create cross-mod dependencies.
    internal static class CraftingOwnershipAdapters
    {
        private static readonly IRecipeCharacterIdentityProvider CharacterIdentity = new CharacterIdentityAdapter();
        private static readonly IForagingProgressionSource ForagingProgression = new ForagingProgressionAdapter();

        internal static void Register()
        {
            RecipeOwnershipIntegration.RegisterCharacterIdentityProvider(CharacterIdentity);
            RecipeOwnershipIntegration.RegisterForagingProgressionSource(ForagingProgression);
        }

        private sealed class CharacterIdentityAdapter : IRecipeCharacterIdentityProvider
        {
            public bool TryGetStableCharacterIdentity(out string stableIdentity)
            {
                stableIdentity = string.Empty;
                if (!CraftingCharacterIdentity.IsReady()) return false;
                string value = CraftingCharacterIdentity.ResolveCharacterKey();
                if (string.IsNullOrEmpty(value)) return false;
                stableIdentity = value;
                return true;
            }

            public string DescribeCapability()
            {
                return "Crafting character slot/name identity";
            }
        }

        private sealed class ForagingProgressionAdapter : IForagingProgressionSource
        {
            public bool TryGetProgression(out ForagingProgressionSnapshot snapshot)
            {
                snapshot = null;
                if (!ForagingKnowledge.IsReady) return false;
                ForagingKnowledgeSnapshot source = ForagingKnowledge.GetSnapshot();
                ForagingProgressionSnapshot value = new ForagingProgressionSnapshot();
                value.Level = source.Level;
                value.Xp = source.Xp;
                value.XpToNextLevel = source.XpToNext;

                List<string> known = new List<string>();
                for (int i = 0; i < source.Resources.Count; i++)
                {
                    ForagingResourceKnowledgeSnapshot resource = source.Resources[i];
                    if (resource != null && resource.Discovered && !string.IsNullOrEmpty(resource.DisplayName))
                        known.Add(resource.DisplayName);
                }
                value.KnownResources = known.ToArray();
                snapshot = value;
                return true;
            }

            public string DescribeCapability()
            {
                return "Foraging progression/knowledge";
            }
        }
    }
}
