using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    internal static class CraftingController
    {
        internal static CraftingProgress Progress = new CraftingProgress();
        internal static CraftResult LastCraft;
        internal static string LastRejectionReason = string.Empty;
        internal static int LastCraftableCount;
        internal static int LastAutoFillMovedUnits;

        private static string _savePath;
        private static bool _initialized;
        private static bool _pendingExternalOpen;
        private static bool _pendingExternalClose;
        private static bool _pendingLauncherToggle;
        private static CraftingLauncher _launcher;

        internal static void Initialize(CraftingExpandedSettings settings, string pluginDataDir)
        {
            CraftingConfig.Initialize(settings);
            ForagingConfig.Initialize(settings);
            _savePath = Path.Combine(pluginDataDir, "smithing-progress.json");
            CraftingSaveData data = CraftingProgressionStore.Load(_savePath);
            Progress = data.Smithing ?? new CraftingProgress();

            CraftingWindow.Initialize(CraftingConfig.PanelX.Value, CraftingConfig.PanelY.Value, PersistPanelPosition);
            _launcher = new CraftingLauncher();
            _launcher.Initialize(CraftingConfig.LauncherX.Value, CraftingConfig.LauncherY.Value, PersistLauncherPosition,
                delegate { _pendingLauncherToggle = true; });
            _initialized = true;
        }

        internal static void Tick()
        {
            if (!_initialized) return;

            if (!CraftingConfig.EnableMod.Value)
            {
                CraftingUiStateMachine.OnContextRelevant(false);
                ForageNodeController.Shutdown();
                return;
            }

            bool forgeOpen = GameCraftingApi.IsForgeOpen();
            CraftingUiStateMachine.OnContextRelevant(forgeOpen || CommissionController.HasActiveCommission());

            CraftHotkeyController.Tick();
            if (forgeOpen)
            {
                CraftRecipeSnapshot recipe = GameCraftingApi.TryGetActiveRecipe();
                if (recipe != null && !GameCraftingApi.IsSpecialCombineTemplate(recipe.TemplateItemId))
                {
                    int fuelUnits;
                    Dictionary<string, int> availability = CraftableCountPolicy.BuildAvailability(GameCraftingApi.ReadTotalCraftingAvailability(out fuelUnits));
                    LastCraftableCount = CraftableCountPolicy.CalculateCraftableCount(recipe, availability, fuelUnits);
                }
                else LastCraftableCount = 0;
                CommissionController.TryOfferFromCurrentRecipe();
            }
            else
            {
                LastCraftableCount = 0;
                LastAutoFillMovedUnits = 0;
                CommissionController.OnForgeClosed();
            }

            CommissionController.RevalidateAgainstLiveSims();
            ForageNodeController.Tick(Time.deltaTime);
        }

        internal static void TickUi(bool bridgeRegistered)
        {
            if (!_initialized) return;
            ProcessPendingUiActions();
            bool gameplayReady = SuiteUiPolicy.IsGameplayReady();
            if (!gameplayReady) SuiteDragHandler.ForceReleaseIfOwned();
            bool showLauncher = SuiteUiPolicy.ShouldShowStandaloneLauncher(
                bridgeRegistered,
                CraftingConfig.ShowCraftingToggle != null && CraftingConfig.ShowCraftingToggle.Value);
            if (_launcher != null) _launcher.Tick(showLauncher, CraftingUiStateMachine.IsPanelVisible());
            CraftingWindow.Tick(gameplayReady && CraftingUiStateMachine.IsPanelVisible());
        }

        private static void ProcessPendingUiActions()
        {
            if (_pendingExternalClose)
            {
                _pendingExternalClose = false;
                _pendingExternalOpen = false;
                CraftingUiStateMachine.Close();
            }
            else if (_pendingExternalOpen)
            {
                _pendingExternalOpen = false;
                CraftingUiStateMachine.OpenPersistent();
            }

            if (_pendingLauncherToggle)
            {
                _pendingLauncherToggle = false;
                if (CraftingUiStateMachine.IsPanelVisible()) CraftingUiStateMachine.Close();
                else CraftingUiStateMachine.OpenPersistent();
            }
        }

        internal static void OnVerifiedCraftSuccess(CraftRecipeSnapshot recipe)
        {
            if (!_initialized) return;
            LastCraft = new CraftResult(recipe.TemplateItemId, recipe.TemplateItemName, recipe.OutputItemId, recipe.OutputItemName, DateTime.UtcNow);
            RecipeDifficulty difficulty = RecipeDifficultyCatalog.Classify(recipe.TemplateItemId);
            Progress.AwardXp(difficulty);
            if (CommissionController.TryCompleteFromCraft(recipe)) Progress.AwardXp(RecipeDifficulty.Appropriate);
            Persist();
            LastRejectionReason = string.Empty;
        }

        internal static void OnHotkeyCraftAttempt(bool invoked)
        {
            LastRejectionReason = invoked ? string.Empty : "Hotkey craft could not be invoked (forge/native path unavailable).";
        }
        internal static void OnAutoFillAttempt(int movedUnits) { LastAutoFillMovedUnits = movedUnits < 0 ? 0 : movedUnits; }

        internal static void SceneTransition()
        {
            SuiteDragHandler.ForceReleaseIfOwned();
            CommissionController.SceneTransition();
            ForageNodeController.SceneTransition();
        }

        internal static void Persist()
        {
            if (!_initialized || string.IsNullOrEmpty(_savePath)) return;
            if (!CraftingProgressionStore.Save(_savePath, new CraftingSaveData { Smithing = Progress })) LogError(CraftingProgressionStore.LastError);
        }

        internal static void Shutdown()
        {
            Persist();
            ForageNodeController.Shutdown();
            _pendingExternalOpen = _pendingExternalClose = _pendingLauncherToggle = false;
            CraftingWindow.ResetTransientState();
            CraftingWindow.Dispose();
            if (_launcher != null) _launcher.Dispose();
            _launcher = null;
            SuiteDragHandler.ForceReleaseIfOwned();
            _initialized = false;
        }

        internal static bool PanelOpen { get { return CraftingUiStateMachine.IsPanelVisible(); } }
        internal static bool ShowStandaloneLauncher { get { return CraftingConfig.ShowCraftingToggle != null && CraftingConfig.ShowCraftingToggle.Value; } }
        internal static void RequestOpenPanel() { _pendingExternalOpen = true; _pendingExternalClose = false; }
        internal static void RequestClosePanel() { _pendingExternalClose = true; _pendingExternalOpen = false; }
        internal static void ResetPanelPosition() { CraftingWindow.ResetPosition(); }
        internal static void ResetLauncherPosition() { if (_launcher != null) _launcher.ResetPosition(); }
        internal static void SetShowStandaloneLauncher(bool value) { if (CraftingConfig.ShowCraftingToggle != null) CraftingConfig.ShowCraftingToggle.Value = value; SaveSettings(); }
        internal static void SetEnabled(bool enabled) { CraftingConfig.EnableMod.Value = enabled; SaveSettings(); }
        internal static void SetForagingEnabled(bool enabled) { ForagingConfig.EnableForaging.Value = enabled; SaveSettings(); }

        private static void PersistPanelPosition(float x, float y)
        {
            if (!CraftingConfig.PersistWindowPosition.Value) return;
            CraftingConfig.PanelX.Value = x; CraftingConfig.PanelY.Value = y; SaveSettings();
        }
        private static void PersistLauncherPosition(float x, float y)
        {
            CraftingConfig.LauncherX.Value = x; CraftingConfig.LauncherY.Value = y; SaveSettings();
        }
        private static void SaveSettings()
        {
            try { if (ErenshorCraftingExpandedPluginHolder.Instance != null) ErenshorCraftingExpandedPluginHolder.Instance.SaveSettingsPublic(); } catch { }
        }
        internal static void LogError(string message) { try { if (ErenshorCraftingExpandedPluginHolder.Instance != null) ErenshorCraftingExpandedPluginHolder.Instance.LogErrorPublic(message); } catch { } }
        internal static void LogInfo(string message) { try { if (ErenshorCraftingExpandedPluginHolder.Instance != null) ErenshorCraftingExpandedPluginHolder.Instance.LogInfoPublic(message); } catch { } }
    }
}
