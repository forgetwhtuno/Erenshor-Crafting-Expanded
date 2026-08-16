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
        internal static CraftRecipeSnapshot ActiveRecipeSnapshot;
        internal static Dictionary<string, int> ActiveCraftingAvailability = new Dictionary<string, int>();
        internal static int ActiveFuelSourceUnits;

        private static string _pluginDataRoot;
        private static string _savePath;
        private static string _characterKey = string.Empty;
        private static bool _characterScopeReady;
        private static string _lastCharacterScopeLoadFailureKey = string.Empty;
        private static float _nextCharacterScopeRetryAt;
        private static CraftingSaveData _saveData = new CraftingSaveData();
        private static bool _initialized;
        private static bool _pendingExternalOpen;
        private static bool _pendingExternalClose;
        private static bool _pendingLauncherToggle;
        private static bool _lastPanelOpen;
        private static double _panelActivatedAt;
        private static CraftingLauncher _launcher;

        internal static void Initialize(CraftingExpandedSettings settings, string pluginDataDir)
        {
            CraftingConfig.Initialize(settings);
            ForagingConfig.Initialize(settings);

            _pluginDataRoot = pluginDataDir;
            _savePath = string.Empty;
            _characterKey = string.Empty;
            _characterScopeReady = false;
            _lastCharacterScopeLoadFailureKey = string.Empty;
            _nextCharacterScopeRetryAt = 0f;
            _saveData = new CraftingSaveData();
            _saveData.Normalize();
            Progress = _saveData.Smithing;
            ClearActiveForgeSnapshot();

            ForagingProgressionController.Initialize(pluginDataDir);
            CraftingOwnershipAdapters.Register();
            RecipeOwnershipController.Initialize(pluginDataDir);
            ProductionNativeRecipeRegistry.Initialize(pluginDataDir);
            CraftingRecipeProgressionService.Initialize();

            CraftingWindow.Initialize(CraftingConfig.PanelX.Value, CraftingConfig.PanelY.Value, PersistPanelPosition);
            _launcher = new CraftingLauncher();
            _launcher.Initialize(CraftingConfig.LauncherX.Value, CraftingConfig.LauncherY.Value, PersistLauncherPosition,
                delegate { _pendingLauncherToggle = true; });
            _initialized = true;
        }

        internal static void Tick()
        {
            if (!_initialized) return;

            // Character-owned progression stays synchronized even when gameplay is disabled.
            EnsureCharacterScope();
            ForagingProgressionController.Tick();
            if (ForagingProgressionController.ConsumeCharacterChanged())
            {
                ForageNodeController.SceneTransition();
                CraftingRecipeProgressionService.OnCharacterChanged();
            }
            RecipeOwnershipController.Tick();
            // Production recipe identities may remain inert while the mod is disabled, but native
            // Template activation is owned by this gameplay tick and is removed again on disable.
            ProductionNativeRecipeRegistry.Tick(CraftingConfig.EnableMod.Value && CraftingConfig.EnableProductionNativeRecipes != null && CraftingConfig.EnableProductionNativeRecipes.Value);

            if (!CraftingConfig.EnableMod.Value)
            {
                CraftingUiStateMachine.OnContextRelevant(false);
                CommissionController.OnGameplayDisabled();
                ForageNodeController.DisableGameplay();
                ClearActiveForgeSnapshot();
                return;
            }

            bool forgeOpen = GameCraftingApi.IsForgeOpen();
            CraftingUiStateMachine.OnContextRelevant(forgeOpen || CommissionController.HasActiveCommission());

            CraftHotkeyController.Tick();
            if (forgeOpen)
            {
                CraftRecipeSnapshot recipe = GameCraftingApi.TryGetActiveRecipe();
                ActiveRecipeSnapshot = recipe;
                if (recipe != null && !GameCraftingApi.IsSpecialCombineTemplate(recipe.TemplateItemId))
                {
                    int fuelUnits;
                    Dictionary<string, int> availability = CraftableCountPolicy.BuildAvailability(GameCraftingApi.ReadTotalCraftingAvailability(out fuelUnits));
                    ActiveCraftingAvailability = availability;
                    ActiveFuelSourceUnits = fuelUnits;
                    LastCraftableCount = CraftableCountPolicy.CalculateCraftableCount(recipe, availability, fuelUnits);
                }
                else
                {
                    ActiveCraftingAvailability.Clear();
                    ActiveFuelSourceUnits = 0;
                    LastCraftableCount = 0;
                }
                CommissionController.TryOfferFromCurrentRecipe();
            }
            else
            {
                ClearActiveForgeSnapshot();
                LastAutoFillMovedUnits = 0;
                CommissionController.OnForgeClosed();
            }

            CommissionController.RevalidateAgainstLiveSims();
            CraftingRecipeProgressionService.Tick(Progress, _characterKey);
            ForageNodeController.Tick(Time.deltaTime);
        }

        internal static void TickUi(bool bridgeRegistered)
        {
            if (!_initialized) return;
            ProcessPendingUiActions();
            bool panelOpen = CraftingUiStateMachine.IsPanelVisible();
            if (panelOpen && !_lastPanelOpen) _panelActivatedAt = (double)Time.unscaledTime;
            _lastPanelOpen = panelOpen;
            bool gameplayReady = SuiteUiPolicy.IsGameplayReady();
            if (!gameplayReady) SuiteDragHandler.ForceReleaseIfOwned();
            bool showLauncher = SuiteUiPolicy.ShouldShowStandaloneLauncher(
                bridgeRegistered,
                CraftingConfig.ShowCraftingToggle != null && CraftingConfig.ShowCraftingToggle.Value);
            if (_launcher != null) _launcher.Tick(showLauncher, panelOpen);
            CraftingWindow.Tick(gameplayReady && panelOpen);
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
            if (!_initialized || recipe == null) return;
            EnsureCharacterScope();
            LastCraft = new CraftResult(recipe.TemplateItemId, recipe.TemplateItemName, recipe.OutputItemId, recipe.OutputItemName, DateTime.UtcNow);

            if (CharacterScopeResolved)
            {
                bool verificationRecipe = string.Equals(recipe.TemplateItemId, CraftingRecipeCatalog.ExperimentalHerbalTemplateId, StringComparison.Ordinal);
                CustomRecipeDefinition custom = CraftingRecipeCatalog.Production.GetByTemplateId(recipe.TemplateItemId);

if (custom != null)
                {
                    // Runtime-bound Crafting Expanded recipes award progression only while the
                    // exact native Template binding is active in this session. A persisted inert
                    // identity can remain in ItemDB for save safety but must never train Crafting.
                    if (ProductionNativeRecipeRegistry.IsRegisteredCurrentSession(recipe.TemplateItemId))
                    {
                        int priorCrafts = Progress.GetSuccessfulCraftCount(recipe.TemplateItemId);
                        int xp = RecipeProgressionPolicy.XpForSuccessfulRecipe(Progress.Level, custom.MinimumCraftingLevel, priorCrafts);
                        Progress.RecordSuccessfulCraft(recipe.TemplateItemId);
                        Progress.AwardRawXp(xp);
                    }
                }
                else if (!verificationRecipe)
                {
                    int priorCrafts = Progress.GetSuccessfulCraftCount(recipe.TemplateItemId);
                    RecipeDifficulty difficulty = RecipeDifficultyCatalog.Classify(recipe.TemplateItemId);
                    int xp = RecipeProgressionPolicy.XpForNativeRecipe(Progress.Level, difficulty, priorCrafts);
                    Progress.RecordSuccessfulCraft(recipe.TemplateItemId);
                    Progress.AwardRawXp(xp);
                }

                if (!verificationRecipe && CommissionController.TryCompleteFromCraft(recipe))
                    Progress.AwardXp(RecipeDifficulty.Appropriate);

                // Native Smithing may consume the physical Template on success. Ownership tracks
                // the replacement entitlement without ever changing permanent recipe knowledge.
                RecipeOwnershipController.OnVerifiedCraftSuccess(recipe.TemplateItemId);
                CraftingRecipeProgressionService.EvaluateUnlocks(Progress);
                Persist();
            }

            LastRejectionReason = string.Empty;
        }

        internal static void OnHotkeyCraftAttempt(bool invoked)
        {
            LastRejectionReason = invoked ? string.Empty : "Hotkey craft could not be invoked (forge/native path unavailable).";
        }

        internal static void OnAutoFillAttempt(int movedUnits)
        {
            LastAutoFillMovedUnits = movedUnits < 0 ? 0 : movedUnits;
        }

        internal static void SceneTransition()
        {
            _characterScopeReady = false;
            ClearActiveForgeSnapshot();
            SuiteDragHandler.ForceReleaseIfOwned();

            RecipeOwnershipController.SceneTransition();
            CommissionController.SceneTransition();

            // Persist Foraging progression and remaining cooldowns before scene-local node
            // teardown can clear the depletion ledger.
            ForagingProgressionController.SceneTransition();
            ForageNodeController.SceneTransition();
        }

        internal static void Persist()
        {
            if (!_initialized) return;

            if (!string.IsNullOrEmpty(_savePath))
            {
                _saveData.Smithing = Progress;
                if (!CraftingProgressionStore.Save(_savePath, _saveData))
                    LogError(CraftingProgressionStore.LastError);
            }

            RecipeOwnershipController.Persist();
        }

        private static void EnsureCharacterScope()
        {
            if (!CraftingCharacterIdentity.IsReady())
            {
                _characterScopeReady = false;
                return;
            }

            string resolved = CraftingCharacterIdentity.ResolveCharacterKey();
            if (string.IsNullOrEmpty(resolved))
            {
                _characterScopeReady = false;
                return;
            }

            if (string.Equals(resolved, _characterKey, StringComparison.Ordinal))
            {
                _characterScopeReady = true;
                _lastCharacterScopeLoadFailureKey = string.Empty;
                _nextCharacterScopeRetryAt = 0f;
                return;
            }

            if (string.Equals(resolved, _lastCharacterScopeLoadFailureKey, StringComparison.Ordinal) &&
                Time.unscaledTime < _nextCharacterScopeRetryAt)
            {
                _characterScopeReady = false;
                return;
            }

            if (!string.IsNullOrEmpty(_characterKey)) Persist();

            string candidatePath = CraftingProgressionStore.CharacterDataPath(_pluginDataRoot, resolved);
            CraftingSaveData candidateData = CraftingProgressionStore.LoadCharacterWithLegacyClaim(_pluginDataRoot, resolved);
            if (!string.IsNullOrEmpty(CraftingProgressionStore.LastError))
            {
                _characterScopeReady = false;
                _lastCharacterScopeLoadFailureKey = resolved;
                _nextCharacterScopeRetryAt = Time.unscaledTime + 2f;
                LogError(CraftingProgressionStore.LastError);
                return;
            }

            _characterKey = resolved;
            _savePath = candidatePath;
            _saveData = candidateData;
            _saveData.Normalize();
            Progress = _saveData.Smithing;
            CraftingRecipeProgressionService.OnCharacterChanged();

            _lastCharacterScopeLoadFailureKey = string.Empty;
            _nextCharacterScopeRetryAt = 0f;
            _characterScopeReady = true;
        }

        internal static void Shutdown()
        {
            Persist();
            CommissionController.Shutdown();

            // Save Foraging state before resource/node shutdown clears runtime depletion state.
            ForagingProgressionController.Shutdown();
            ForageNodeController.Shutdown();
            ProductionNativeRecipeRegistry.Shutdown();
            RecipeOwnershipController.Shutdown();

            _pendingExternalOpen = _pendingExternalClose = _pendingLauncherToggle = false;
            _lastPanelOpen = false;
            _panelActivatedAt = 0d;
            CraftingWindow.ResetTransientState();
            CraftingWindow.Dispose();
            if (_launcher != null) _launcher.Dispose();
            _launcher = null;
            SuiteDragHandler.ForceReleaseIfOwned();

            _characterKey = string.Empty;
            _characterScopeReady = false;
            _lastCharacterScopeLoadFailureKey = string.Empty;
            _nextCharacterScopeRetryAt = 0f;
            _savePath = string.Empty;
            _pluginDataRoot = null;
            _saveData = new CraftingSaveData();
            ClearActiveForgeSnapshot();
            CraftingRecipeProgressionService.OnCharacterChanged();
            _initialized = false;
        }


        private static void ClearActiveForgeSnapshot()
        {
            ActiveRecipeSnapshot = null;
            if (ActiveCraftingAvailability == null) ActiveCraftingAvailability = new Dictionary<string, int>();
            else ActiveCraftingAvailability.Clear();
            ActiveFuelSourceUnits = 0;
            LastCraftableCount = 0;
        }

        internal static bool CharacterScopeResolved
        {
            get { return _characterScopeReady && !string.IsNullOrEmpty(_characterKey); }
        }

        internal static bool PanelOpen { get { return CraftingUiStateMachine.IsPanelVisible(); } }
        internal static double PanelActivatedAt { get { return _panelActivatedAt; } }
        internal static bool ShowStandaloneLauncher { get { return CraftingConfig.ShowCraftingToggle != null && CraftingConfig.ShowCraftingToggle.Value; } }

        internal static void RequestOpenPanel()
        {
            _pendingExternalOpen = true;
            _pendingExternalClose = false;
        }

        internal static void RequestClosePanel()
        {
            _pendingExternalClose = true;
            _pendingExternalOpen = false;
        }

        internal static void ResetPanelPosition() { CraftingWindow.ResetPosition(); }
        internal static void ResetLauncherPosition() { if (_launcher != null) _launcher.ResetPosition(); }
        internal static void SetShowStandaloneLauncher(bool value) { if (CraftingConfig.ShowCraftingToggle != null) CraftingConfig.ShowCraftingToggle.Value = value; SaveSettings(); }
        internal static void SetEnabled(bool enabled) { CraftingConfig.EnableMod.Value = enabled; SaveSettings(); }
        internal static void SetForagingEnabled(bool enabled) { ForagingConfig.EnableForaging.Value = enabled; SaveSettings(); }
        internal static void SetCraftingRequestsEnabled(bool enabled) { CraftingConfig.EnableCraftingRequests.Value = enabled; SaveSettings(); }
        internal static void SetExperimentalCoveredResources(bool enabled) { ForagingConfig.ExperimentalCoveredResources.Value = enabled; SaveSettings(); }

        private static void PersistPanelPosition(float x, float y)
        {
            if (!CraftingConfig.PersistWindowPosition.Value) return;
            CraftingConfig.PanelX.Value = x;
            CraftingConfig.PanelY.Value = y;
            SaveSettings();
        }

        private static void PersistLauncherPosition(float x, float y)
        {
            CraftingConfig.LauncherX.Value = x;
            CraftingConfig.LauncherY.Value = y;
            SaveSettings();
        }

        private static void SaveSettings()
        {
            try
            {
                if (ErenshorCraftingExpandedPluginHolder.Instance != null)
                    ErenshorCraftingExpandedPluginHolder.Instance.SaveSettingsPublic();
            }
            catch { }
        }

        internal static void LogError(string message)
        {
            try
            {
                if (ErenshorCraftingExpandedPluginHolder.Instance != null)
                    ErenshorCraftingExpandedPluginHolder.Instance.LogErrorPublic(message);
            }
            catch { }
        }

        internal static void LogInfo(string message)
        {
            try
            {
                if (ErenshorCraftingExpandedPluginHolder.Instance != null)
                    ErenshorCraftingExpandedPluginHolder.Instance.LogInfoPublic(message);
            }
            catch { }
        }
    }
}
