using BepInEx.Configuration;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    internal static class CraftingConfig
    {
        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<KeyCode> CraftHotkey;
        internal static ConfigEntry<bool> EnableCraftingRequests;
        internal static ConfigEntry<bool> ShowCraftingToggle;
        internal static ConfigEntry<bool> PersistWindowPosition;
        internal static ConfigEntry<float> PanelOffsetX;
        internal static ConfigEntry<float> PanelOffsetY;

        internal static void Initialize(ConfigFile config)
        {
            EnableMod = config.Bind("General", "EnableMod", true,
                "Master switch. When false, no Harmony behavior beyond native crafting runs.");
            CraftHotkey = config.Bind("Crafting", "CraftHotkey", KeyCode.None,
                "Key that performs one Craft action while the forge window is open and chat is not focused. " +
                "Defaults to unbound (None) - set explicitly since no safe default that avoids every existing " +
                "keybind could be confirmed from native evidence.");
            EnableCraftingRequests = config.Bind("Commissions", "EnableCraftingRequests", false,
                "Experimental proof-of-concept: allow local SimPlayers to offer a single crafting commission. " +
                "Disabled by default until the final recipe-catalog/Sim-eligibility design is implemented.");
            ShowCraftingToggle = config.Bind("UI", "ShowCraftingToggle", true,
                "Show the small Crafting toggle button when crafting context is relevant.");
            PersistWindowPosition = config.Bind("UI", "PersistWindowPosition", true,
                "Remember the Crafting panel's dragged position between sessions.");
            PanelOffsetX = config.Bind("UI", "PanelOffsetX", 0f, "Persisted horizontal offset for the Crafting panel.");
            PanelOffsetY = config.Bind("UI", "PanelOffsetY", 0f, "Persisted vertical offset for the Crafting panel.");
        }
    }
}
