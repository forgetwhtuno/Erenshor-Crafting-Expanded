using System;
using Lunaris.Config;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Thin ConfigEntry-style shim over Lunaris typed settings. The current source expects .Value
    // semantics throughout runtime code, while Lunaris persists the fixed-key public fields below.
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
        [Config("EnableMod", "General", "Enable Crafting Expanded gameplay. Installed custom item identities remain registered while the plugin is loaded so existing saves can still resolve them safely.")]
        public bool EnableMod = true;

        [Config("CraftHotkey", "Crafting", "Optional key that performs one craft through the native forge path while the forge is open. None leaves it unbound.")]
        public KeyCode CraftHotkey = KeyCode.None;

        [Config("ExperimentalNativeRecipeRegistration", "Crafting.Experimental", "Developer-only single-template lifecycle experiment. Leave OFF for normal play.")]
        public bool ExperimentalNativeRecipeRegistration = false;

        [Config("EnableProductionNativeRecipes", "Crafting.Experimental", "Enable the runtime-bound production native recipe catalog. OFF by default until the current installed build passes the documented native Smithing lifecycle test.")]
        public bool EnableProductionNativeRecipes = false;

        [Config("EnableCraftingRequests", "Commissions", "Enable the experimental local-Sim crafting commission proof of concept.")]
        public bool EnableCraftingRequests = false;

        [Config("ShowCraftingToggle", "UI", "Show the standalone Crafting launcher while Suite Hub is usable. Hub failure still forces a recovery launcher.")]
        public bool ShowCraftingToggle = true;

        [Config("PersistWindowPosition", "UI", "Remember retained Crafting launcher/panel positions.")]
        public bool PersistWindowPosition = true;

        [Config("LauncherX", "UI", "Normalized standalone launcher X position. Invalid values recover to a safe default.")]
        public float LauncherX = -1f;

        [Config("LauncherY", "UI", "Normalized standalone launcher Y position. Invalid values recover to a safe default.")]
        public float LauncherY = -1f;

        [Config("PanelX", "UI", "Normalized retained Crafting panel X position. Invalid values recover to a safe default.")]
        public float PanelX = -1f;

        [Config("PanelY", "UI", "Normalized retained Crafting panel Y position. Invalid values recover to a safe default.")]
        public float PanelY = -1f;

        [Config("EnableForaging", "Foraging", "Enable the mod-owned Foraging gathering system.")]
        public bool EnableForaging = true;

        [Config("EnablePoCNode", "Foraging", "Developer-only survey/PoC authored-node switch. Normal auto-placement does not depend on it.")]
        public bool EnablePoCNode = false;

        [Config("ForageKey", "Foraging", "Legacy compatibility field. Foraging is click-to-gather; this key is not read by production interaction code.")]
        public KeyCode ForageKey = KeyCode.None;

        [Config("ForagingInteractionRange", "Foraging", "Maximum player distance in world units for an eligible gather.")]
        public float ForagingInteractionRange = 3.5f;

        [Config("GatherDurationSeconds", "Foraging", "Short mod-owned gather channel duration in seconds. Production values are clamped to 1.0-1.5 seconds.")]
        public float GatherDurationSeconds = 1.25f;

        [Config("ExperimentalCoveredResources", "Foraging.Experimental", "OFF-by-default covered/cave resource family. Requires explicit fungus item and scene-mesh evidence.")]
        public bool ExperimentalCoveredResources = false;

        [Config("ForagingScanRadius", "Foraging.Dev", "Radius used only by explicit forage asset diagnostics.")]
        public float ForagingScanRadius = 12f;

        [Config("ForagingDebugRespawnSeconds", "Foraging.Dev", "If greater than zero, override production forage respawn seconds for testing.")]
        public float ForagingDebugRespawnSeconds = 0f;

        [Config("AllowDebugPlaceholderVisual", "Foraging.Dev", "Allow the development-only placeholder visual if a surveyed source cannot resolve. Leave OFF for normal play.")]
        public bool AllowDebugPlaceholderVisual = false;

        [Config("UseNativeGatherAnimation", "Foraging.Experimental", "Optional StartLoot/EndLoot animation adapter. OFF by default until live player-rig/equipment audition proves the pose is appropriate.")]
        public bool UseNativeGatherAnimation = false;
    }

    internal static class CraftingConfig
    {
        internal static CraftingExpandedConfigEntry<bool> EnableMod;
        internal static CraftingExpandedConfigEntry<KeyCode> CraftHotkey;
        internal static CraftingExpandedConfigEntry<bool> ExperimentalNativeRecipeRegistration;
        internal static CraftingExpandedConfigEntry<bool> EnableProductionNativeRecipes;
        internal static CraftingExpandedConfigEntry<bool> EnableCraftingRequests;
        internal static CraftingExpandedConfigEntry<bool> ShowCraftingToggle;
        internal static CraftingExpandedConfigEntry<bool> PersistWindowPosition;
        internal static CraftingExpandedConfigEntry<float> LauncherX;
        internal static CraftingExpandedConfigEntry<float> LauncherY;
        internal static CraftingExpandedConfigEntry<float> PanelX;
        internal static CraftingExpandedConfigEntry<float> PanelY;

        internal static void Initialize(CraftingExpandedSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            EnableMod = new CraftingExpandedConfigEntry<bool>(delegate { return settings.EnableMod; }, delegate(bool value) { settings.EnableMod = value; });
            CraftHotkey = new CraftingExpandedConfigEntry<KeyCode>(delegate { return settings.CraftHotkey; }, delegate(KeyCode value) { settings.CraftHotkey = value; });
            ExperimentalNativeRecipeRegistration = new CraftingExpandedConfigEntry<bool>(delegate { return settings.ExperimentalNativeRecipeRegistration; }, delegate(bool value) { settings.ExperimentalNativeRecipeRegistration = value; });
            EnableProductionNativeRecipes = new CraftingExpandedConfigEntry<bool>(delegate { return settings.EnableProductionNativeRecipes; }, delegate(bool value) { settings.EnableProductionNativeRecipes = value; });
            EnableCraftingRequests = new CraftingExpandedConfigEntry<bool>(delegate { return settings.EnableCraftingRequests; }, delegate(bool value) { settings.EnableCraftingRequests = value; });
            ShowCraftingToggle = new CraftingExpandedConfigEntry<bool>(delegate { return settings.ShowCraftingToggle; }, delegate(bool value) { settings.ShowCraftingToggle = value; });
            PersistWindowPosition = new CraftingExpandedConfigEntry<bool>(delegate { return settings.PersistWindowPosition; }, delegate(bool value) { settings.PersistWindowPosition = value; });
            LauncherX = new CraftingExpandedConfigEntry<float>(delegate { return settings.LauncherX; }, delegate(float value) { settings.LauncherX = value; });
            LauncherY = new CraftingExpandedConfigEntry<float>(delegate { return settings.LauncherY; }, delegate(float value) { settings.LauncherY = value; });
            PanelX = new CraftingExpandedConfigEntry<float>(delegate { return settings.PanelX; }, delegate(float value) { settings.PanelX = value; });
            PanelY = new CraftingExpandedConfigEntry<float>(delegate { return settings.PanelY; }, delegate(float value) { settings.PanelY = value; });
        }
    }

    internal static class ForagingConfig
    {
        internal const string UnsurveyedLabel = "<UNSURVEYED>";

        internal static CraftingExpandedConfigEntry<bool> EnableForaging;
        internal static CraftingExpandedConfigEntry<bool> EnablePoCNode;
        internal static CraftingExpandedConfigEntry<KeyCode> ForageKey;
        internal static CraftingExpandedConfigEntry<float> InteractionRange;
        internal static CraftingExpandedConfigEntry<float> GatherDurationSeconds;
        internal static CraftingExpandedConfigEntry<bool> ExperimentalCoveredResources;
        internal static CraftingExpandedConfigEntry<float> ScanRadius;
        internal static CraftingExpandedConfigEntry<float> DebugRespawnSecondsOverride;
        internal static CraftingExpandedConfigEntry<bool> AllowDebugPlaceholderVisual;
        internal static CraftingExpandedConfigEntry<bool> UseNativeGatherAnimation;

        internal static void Initialize(CraftingExpandedSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            EnableForaging = new CraftingExpandedConfigEntry<bool>(delegate { return settings.EnableForaging; }, delegate(bool value) { settings.EnableForaging = value; });
            EnablePoCNode = new CraftingExpandedConfigEntry<bool>(delegate { return settings.EnablePoCNode; }, delegate(bool value) { settings.EnablePoCNode = value; });
            ForageKey = new CraftingExpandedConfigEntry<KeyCode>(delegate { return settings.ForageKey; }, delegate(KeyCode value) { settings.ForageKey = value; });
            InteractionRange = new CraftingExpandedConfigEntry<float>(delegate { return settings.ForagingInteractionRange; }, delegate(float value) { settings.ForagingInteractionRange = value; });
            GatherDurationSeconds = new CraftingExpandedConfigEntry<float>(delegate { return settings.GatherDurationSeconds; }, delegate(float value) { settings.GatherDurationSeconds = value; });
            ExperimentalCoveredResources = new CraftingExpandedConfigEntry<bool>(delegate { return settings.ExperimentalCoveredResources; }, delegate(bool value) { settings.ExperimentalCoveredResources = value; });
            ScanRadius = new CraftingExpandedConfigEntry<float>(delegate { return settings.ForagingScanRadius; }, delegate(float value) { settings.ForagingScanRadius = value; });
            DebugRespawnSecondsOverride = new CraftingExpandedConfigEntry<float>(delegate { return settings.ForagingDebugRespawnSeconds; }, delegate(float value) { settings.ForagingDebugRespawnSeconds = value; });
            AllowDebugPlaceholderVisual = new CraftingExpandedConfigEntry<bool>(delegate { return settings.AllowDebugPlaceholderVisual; }, delegate(bool value) { settings.AllowDebugPlaceholderVisual = value; });
            UseNativeGatherAnimation = new CraftingExpandedConfigEntry<bool>(delegate { return settings.UseNativeGatherAnimation; }, delegate(bool value) { settings.UseNativeGatherAnimation = value; });
        }
    }
}
