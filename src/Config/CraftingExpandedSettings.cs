using System;
using Lunaris.Config;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Loader-neutral ConfigEntry-style shim. Keeping the Value surface makes the Lunaris
    // migration mechanical and lets the existing call sites keep their proven access pattern.
    internal sealed class CraftingExpandedConfigEntry<T>
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal CraftingExpandedConfigEntry(Func<T> get, Action<T> set)
        {
            _get = get;
            _set = set;
        }

        internal T Value
        {
            get { return _get(); }
            set { _set(value); }
        }
    }

    internal sealed class CraftingExpandedSettings
    {
        public CraftingExpandedSettings() { }

        [Config("EnableMod", "General", "Master switch. When false, no Harmony behavior beyond native crafting runs.")]
        public bool EnableMod = true;

        [Config("CraftHotkey", "Crafting",
            "Key that performs one Craft action while the forge window is open and chat is not focused. " +
            "Defaults to unbound (None) - set explicitly since no safe default that avoids every existing " +
            "keybind could be confirmed from native evidence.")]
        public KeyCode CraftHotkey = KeyCode.None;

        [Config("EnableCraftingRequests", "Commissions",
            "Experimental proof-of-concept: allow local SimPlayers to offer a single crafting commission. " +
            "Disabled by default until the final recipe-catalog/Sim-eligibility design is implemented.")]
        public bool EnableCraftingRequests = false;

        [Config("ShowCraftingToggle", "UI", "Show the small Crafting toggle button when crafting context is relevant.")]
        public bool ShowCraftingToggle = true;

        [Config("PersistWindowPosition", "UI", "Remember the Crafting panel's dragged position between sessions.")]
        public bool PersistWindowPosition = true;

        [Config("PanelOffsetX", "UI", "Persisted horizontal offset for the Crafting panel.")]
        public float PanelOffsetX = 0f;

        [Config("PanelOffsetY", "UI", "Persisted vertical offset for the Crafting panel.")]
        public float PanelOffsetY = 0f;

        [Config("EnableForaging", "Foraging",
            "Enable the Foraging subsystem (registry, diagnostics). Does not by itself spawn a node - see EnablePoCNode.")]
        public bool EnableForaging = true;

        [Config("EnablePoCNode", "Foraging",
            "Spawn the authored Wild Herb node, once one has real survey-verified scene/position/visual-source " +
            "data (see docs/FORAGING_ASSET_SURVEY.md). A definition that still contains placeholder data refuses " +
            "to register/spawn regardless of this setting - it never falls back to spawning at an unverified location.")]
        public bool EnablePoCNode = false;

        [Config("ForageKey", "Foraging", "Key to gather an in-range, available Foraging node.")]
        public KeyCode ForageKey = KeyCode.G;

        [Config("ForagingInteractionRange", "Foraging",
            "Max distance (world units, ~2-4m per native interaction conventions) from the player to allow " +
            "gathering a node. Mod-owned value - no native interaction-distance constant was found (see findings doc).")]
        public float InteractionRange = 3.5f;

        [Config("ForagingScanRadius", "Foraging.Dev",
            "Radius (meters) /craftdiag forage scan searches for nearby renderers. Development diagnostic only.")]
        public float ScanRadius = 12f;

        [Config("ForagingDebugRespawnSeconds", "Foraging.Dev",
            "If > 0, overrides every node's authored RespawnSeconds with this shorter value for fast iteration " +
            "testing (e.g. 45). 0 disables the override and uses each definition's own RespawnSeconds. " +
            "Development setting - not meant for normal play.")]
        public float DebugRespawnSecondsOverride = 0f;

        [Config("AllowDebugPlaceholderVisual", "Foraging.Dev",
            "Development-only: if a node's real visual source fails to resolve, spawn a placeholder sphere " +
            "instead of refusing to spawn. Leave OFF for normal testing - when off, a node with an unresolved " +
            "visual source simply does not appear, and the reason is reported in /craftdiag.")]
        public bool AllowDebugPlaceholderVisual = false;
    }
}
