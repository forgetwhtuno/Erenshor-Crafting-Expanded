using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Separate from CraftingConfig since Foraging is its own subsystem with its own findings
    // doc and its own unresolved manual-verification items (see
    // docs/NATIVE_MINING_AND_FORAGING_FINDINGS.md). No Foraging XP/level settings exist here by
    // design - the user's spec explicitly excludes gathering progression in this pass.
    internal static class ForagingConfig
    {
        internal static CraftingExpandedConfigEntry<bool> EnableForaging;
        internal static CraftingExpandedConfigEntry<bool> EnablePoCNode;
        internal static CraftingExpandedConfigEntry<KeyCode> ForageKey;
        internal static CraftingExpandedConfigEntry<float> InteractionRange;
        internal static CraftingExpandedConfigEntry<float> ScanRadius;
        internal static CraftingExpandedConfigEntry<float> DebugRespawnSecondsOverride;
        internal static CraftingExpandedConfigEntry<bool> AllowDebugPlaceholderVisual;

        // Development-only label used by the intentionally unauthored candidate definition. It is
        // not itself trusted as placeholder detection; ForageNodeCatalog validation rejects the
        // unset position/missing visual source before this definition can ever register.
        internal const string UnsurveyedLabel = "UNVERIFIED_PENDING_MANUAL_SURVEY";

        internal static void Initialize(CraftingExpandedSettings settings)
        {
            EnableForaging = new CraftingExpandedConfigEntry<bool>(() => settings.EnableForaging, v => settings.EnableForaging = v);
            EnablePoCNode = new CraftingExpandedConfigEntry<bool>(() => settings.EnablePoCNode, v => settings.EnablePoCNode = v);
            ForageKey = new CraftingExpandedConfigEntry<KeyCode>(() => settings.ForageKey, v => settings.ForageKey = v);
            InteractionRange = new CraftingExpandedConfigEntry<float>(() => settings.InteractionRange, v => settings.InteractionRange = v);
            ScanRadius = new CraftingExpandedConfigEntry<float>(() => settings.ScanRadius, v => settings.ScanRadius = v);
            DebugRespawnSecondsOverride = new CraftingExpandedConfigEntry<float>(() => settings.DebugRespawnSecondsOverride, v => settings.DebugRespawnSecondsOverride = v);
            AllowDebugPlaceholderVisual = new CraftingExpandedConfigEntry<bool>(() => settings.AllowDebugPlaceholderVisual, v => settings.AllowDebugPlaceholderVisual = v);
        }
    }
}
