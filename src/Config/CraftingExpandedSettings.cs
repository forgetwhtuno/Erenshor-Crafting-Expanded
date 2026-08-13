using System;
using Lunaris.Config;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
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

        [Config("ShowCraftingToggle", "UI", "Show the Crafting on-screen launcher while a usable Suite Hub bridge is present. If Hub/bridge is unavailable, the standalone launcher is forced visible for recovery.")]
        public bool ShowCraftingToggle = true;

        [Config("PersistWindowPosition", "UI", "Remember the Crafting panel's dragged position between sessions.")]
        public bool PersistWindowPosition = true;

        [Config("PanelOffsetX", "UI", "Legacy Crafting panel horizontal offset retained for config compatibility; retained-uGUI position uses PanelX.")]
        public float PanelOffsetX = 0f;

        [Config("PanelOffsetY", "UI", "Legacy Crafting panel vertical offset retained for config compatibility; retained-uGUI position uses PanelY.")]
        public float PanelOffsetY = 0f;

        [Config("LauncherX", "UI", "Saved Crafting launcher horizontal position, normalized 0..1. Values outside that range recover to the safe default.")]
        public float LauncherX = -1f;

        [Config("LauncherY", "UI", "Saved Crafting launcher vertical position, normalized 0..1. Values outside that range recover to the safe default.")]
        public float LauncherY = -1f;

        [Config("PanelX", "UI", "Saved retained Crafting panel horizontal position, normalized 0..1. Values outside that range recover to the safe default.")]
        public float PanelX = -1f;

        [Config("PanelY", "UI", "Saved retained Crafting panel vertical position, normalized 0..1. Values outside that range recover to the safe default.")]
        public float PanelY = -1f;

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
