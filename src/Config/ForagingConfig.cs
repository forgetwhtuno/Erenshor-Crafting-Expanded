using BepInEx.Configuration;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Separate from CraftingConfig since Foraging is its own subsystem with its own findings
    // doc and its own unresolved manual-verification items (see
    // docs/NATIVE_MINING_AND_FORAGING_FINDINGS.md). No Foraging XP/level settings exist here by
    // design - the user's spec explicitly excludes gathering progression in this pass.
    internal static class ForagingConfig
    {
        internal static ConfigEntry<bool> EnableForaging;
        internal static ConfigEntry<bool> EnablePoCNode;
        internal static ConfigEntry<KeyCode> ForageKey;
        internal static ConfigEntry<float> InteractionRange;
        internal static ConfigEntry<float> ScanRadius;
        internal static ConfigEntry<float> DebugRespawnSecondsOverride;
        internal static ConfigEntry<bool> AllowDebugPlaceholderVisual;

        // Development-only label used by the intentionally unauthored candidate definition. It is
        // not itself trusted as placeholder detection; ForageNodeCatalog validation rejects the
        // unset position/missing visual source before this definition can ever register.
        internal const string UnsurveyedLabel = "UNVERIFIED_PENDING_MANUAL_SURVEY";

        internal static void Initialize(ConfigFile config)
        {
            EnableForaging = config.Bind("Foraging", "EnableForaging", true,
                "Enable the Foraging subsystem (registry, diagnostics). Does not by itself spawn " +
                "a node - see EnablePoCNode.");
            EnablePoCNode = config.Bind("Foraging", "EnablePoCNode", false,
                "Spawn the authored Wild Herb node, once one has real survey-verified scene/" +
                "position/visual-source data (see docs/FORAGING_ASSET_SURVEY.md). A definition " +
                "that still contains placeholder data refuses to register/spawn regardless of " +
                "this setting - it never falls back to spawning at an unverified location.");
            ForageKey = config.Bind("Foraging", "ForageKey", KeyCode.G,
                "Key to gather an in-range, available Foraging node.");
            InteractionRange = config.Bind("Foraging", "ForagingInteractionRange", 3.5f,
                "Max distance (world units, ~2-4m per native interaction conventions) from the " +
                "player to allow gathering a node. Mod-owned value - no native interaction-" +
                "distance constant was found (see findings doc).");
            ScanRadius = config.Bind("Foraging.Dev", "ForagingScanRadius", 12f,
                "Radius (meters) /craftdiag forage scan searches for nearby renderers. Development diagnostic only.");
            DebugRespawnSecondsOverride = config.Bind("Foraging.Dev", "ForagingDebugRespawnSeconds", 0f,
                "If > 0, overrides every node's authored RespawnSeconds with this shorter value " +
                "for fast iteration testing (e.g. 45). 0 disables the override and uses each " +
                "definition's own RespawnSeconds. Development setting - not meant for normal play.");
            AllowDebugPlaceholderVisual = config.Bind("Foraging.Dev", "AllowDebugPlaceholderVisual", false,
                "Development-only: if a node's real visual source fails to resolve, spawn a " +
                "placeholder sphere instead of refusing to spawn. Leave OFF for normal testing - " +
                "when off, a node with an unresolved visual source simply does not appear, and " +
                "the reason is reported in /craftdiag.");
        }
    }
}
