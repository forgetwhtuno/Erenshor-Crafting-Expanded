namespace ErenshorCraftingExpanded
{
    public sealed class CraftingControlState
    {
        public bool GameplayReady;
        public bool RuntimeReady;
        public bool Enabled;
        public bool PanelOpen;
        public int SmithingLevel;
        public int SmithingXp;
        public int SmithingXpToNext;
        public int CraftableCount;
        public bool ForagingEnabled;
        public int ForageSpawned;
        public int ForageAvailable;
        public int ForageDepleted;
        public string PrimaryForageNode;
    }

    public static class CraftingControlApi
    {
        public const int ApiVersion = 1;
        public const string ModuleId = "crafting";
        public static bool HasDedicatedPanel { get { return true; } }
        public static bool IsPanelOpen { get { return CraftingController.PanelOpen; } }
        public static CraftingControlState GetBasicState()
        {
            CraftingProgress progress = CraftingController.Progress ?? new CraftingProgress();
            return new CraftingControlState
            {
                GameplayReady = SuiteUiPolicy.IsGameplayReady(),
                RuntimeReady = ErenshorCraftingExpandedPluginHolder.Instance != null && ErenshorCraftingExpandedPluginHolder.Instance.RuntimeReady,
                Enabled = CraftingConfig.EnableMod != null && CraftingConfig.EnableMod.Value,
                PanelOpen = CraftingController.PanelOpen, SmithingLevel = progress.Level, SmithingXp = progress.Xp,
                SmithingXpToNext = SmithingXpCurve.XpToNextLevel(progress.Level), CraftableCount = CraftingController.LastCraftableCount,
                ForagingEnabled = ForagingConfig.EnableForaging != null && ForagingConfig.EnableForaging.Value,
                ForageSpawned = ForageNodeController.SpawnedCount, ForageAvailable = ForageNodeController.AvailableCount(), ForageDepleted = ForageNodeController.DepletedCount(),
                PrimaryForageNode = ForageNodeController.DescribePrimaryNode()
            };
        }
        public static string GetStatus()
        {
            CraftingControlState s = GetBasicState();
            if (!s.RuntimeReady) return "Crafting runtime unavailable (patch set failed closed)";
            return !s.Enabled ? "Crafting Expanded disabled" : "Smithing Lv" + s.SmithingLevel + " | Foraging " + (s.ForagingEnabled ? "on" : "off");
        }
        public static bool GetShowLauncher() { return CraftingController.ShowStandaloneLauncher; }
        public static bool SetShowLauncher(bool value) { CraftingController.SetShowStandaloneLauncher(value); return true; }
        public static bool OpenPanel() { if (!GetBasicState().GameplayReady) return false; CraftingController.RequestOpenPanel(); return true; }
        public static bool ClosePanel() { CraftingController.RequestClosePanel(); return true; }
        public static void ResetPanelPosition() { CraftingController.ResetPanelPosition(); }
        public static void ResetLauncherPosition() { CraftingController.ResetLauncherPosition(); }
        public static bool SetEnabled(bool enabled) { CraftingController.SetEnabled(enabled); return true; }
        public static bool SetForagingEnabled(bool enabled) { CraftingController.SetForagingEnabled(enabled); return true; }
    }
}
