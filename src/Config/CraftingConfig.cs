using UnityEngine;

namespace ErenshorCraftingExpanded
{
    internal static class CraftingConfig
    {
        internal static CraftingExpandedConfigEntry<bool> EnableMod;
        internal static CraftingExpandedConfigEntry<KeyCode> CraftHotkey;
        internal static CraftingExpandedConfigEntry<bool> EnableCraftingRequests;
        internal static CraftingExpandedConfigEntry<bool> ShowCraftingToggle;
        internal static CraftingExpandedConfigEntry<bool> PersistWindowPosition;
        internal static CraftingExpandedConfigEntry<float> PanelOffsetX;
        internal static CraftingExpandedConfigEntry<float> PanelOffsetY;
        internal static CraftingExpandedConfigEntry<float> LauncherX;
        internal static CraftingExpandedConfigEntry<float> LauncherY;
        internal static CraftingExpandedConfigEntry<float> PanelX;
        internal static CraftingExpandedConfigEntry<float> PanelY;

        internal static void Initialize(CraftingExpandedSettings settings)
        {
            EnableMod = new CraftingExpandedConfigEntry<bool>(() => settings.EnableMod, v => settings.EnableMod = v);
            CraftHotkey = new CraftingExpandedConfigEntry<KeyCode>(() => settings.CraftHotkey, v => settings.CraftHotkey = v);
            EnableCraftingRequests = new CraftingExpandedConfigEntry<bool>(() => settings.EnableCraftingRequests, v => settings.EnableCraftingRequests = v);
            ShowCraftingToggle = new CraftingExpandedConfigEntry<bool>(() => settings.ShowCraftingToggle, v => settings.ShowCraftingToggle = v);
            PersistWindowPosition = new CraftingExpandedConfigEntry<bool>(() => settings.PersistWindowPosition, v => settings.PersistWindowPosition = v);
            PanelOffsetX = new CraftingExpandedConfigEntry<float>(() => settings.PanelOffsetX, v => settings.PanelOffsetX = v);
            PanelOffsetY = new CraftingExpandedConfigEntry<float>(() => settings.PanelOffsetY, v => settings.PanelOffsetY = v);
            LauncherX = new CraftingExpandedConfigEntry<float>(() => settings.LauncherX, v => settings.LauncherX = v);
            LauncherY = new CraftingExpandedConfigEntry<float>(() => settings.LauncherY, v => settings.LauncherY = v);
            PanelX = new CraftingExpandedConfigEntry<float>(() => settings.PanelX, v => settings.PanelX = v);
            PanelY = new CraftingExpandedConfigEntry<float>(() => settings.PanelY, v => settings.PanelY = v);
        }
    }
}
