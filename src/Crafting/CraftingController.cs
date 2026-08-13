using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Static controller owning all mod state and per-frame ticking, matching the
    // BaseUnityPlugin-shell + static-controller pattern used by every other mod in this repo
    // (PvpController, NemesisDirector).
    internal static class CraftingController
    {
        internal static CraftingProgress Progress = new CraftingProgress();
        internal static CraftResult LastCraft;
        internal static string LastRejectionReason = string.Empty;
        internal static int LastCraftableCount;
        internal static int LastAutoFillMovedUnits;

        private static string _savePath;
        private static bool _initialized;

        internal static void Initialize(CraftingExpandedSettings settings, string pluginDataDir)
        {
            CraftingConfig.Initialize(settings);
            ForagingConfig.Initialize(settings);
            _savePath = Path.Combine(pluginDataDir, "smithing-progress.json");
            CraftingSaveData data = CraftingProgressionStore.Load(_savePath);
            Progress = data.Smithing ?? new CraftingProgress();

            CraftingWindow.ConfigurePosition(
                CraftingConfig.PanelOffsetX.Value, CraftingConfig.PanelOffsetY.Value,
                (x, y) =>
                {
                    if (!CraftingConfig.PersistWindowPosition.Value) return;
                    CraftingConfig.PanelOffsetX.Value = x;
                    CraftingConfig.PanelOffsetY.Value = y;
                });

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
            CraftingUiStateMachine.OnContextRelevant(CraftingConfig.ShowCraftingToggle.Value &&
                (forgeOpen || CommissionController.HasActiveCommission()));

            CraftHotkeyController.Tick();

            if (forgeOpen)
            {
                CraftRecipeSnapshot recipe = GameCraftingApi.TryGetActiveRecipe();
                if (recipe != null && !GameCraftingApi.IsSpecialCombineTemplate(recipe.TemplateItemId))
                {
                    int fuelUnits;
                    Dictionary<string, int> availability = CraftableCountPolicy.BuildAvailability(
                        GameCraftingApi.ReadTotalCraftingAvailability(out fuelUnits));
                    LastCraftableCount = CraftableCountPolicy.CalculateCraftableCount(recipe, availability, fuelUnits);
                }
                else
                {
                    LastCraftableCount = 0;
                }

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

        internal static void Draw()
        {
            if (!_initialized || !CraftingConfig.EnableMod.Value) return;
            CraftingWindow.DrawToggleButton();
            CraftingWindow.Draw();
        }

        internal static bool PointerIsOverUi(Vector2 screenPoint)
        {
            return CraftingWindow.PointerIsOverUi(screenPoint);
        }

        // Called from the Harmony postfix on Smithing.DoSuccess() using a recipe snapshot captured in
        // the prefix. This is the verified native craft-success boundary and awards exactly once
        // after the native success method has run (see CraftSuccessPatch.cs / findings doc).
        internal static void OnVerifiedCraftSuccess(CraftRecipeSnapshot recipe)
        {
            if (!_initialized) return;

            LastCraft = new CraftResult(recipe.TemplateItemId, recipe.TemplateItemName, recipe.OutputItemId, recipe.OutputItemName, DateTime.UtcNow);

            RecipeDifficulty difficulty = RecipeDifficultyCatalog.Classify(recipe.TemplateItemId);
            Progress.AwardXp(difficulty);

            bool commissionCompleted = CommissionController.TryCompleteFromCraft(recipe);
            if (commissionCompleted)
            {
                // Reward: Smithing XP only for v1, per the user's fallback instruction - no
                // verified safe native currency-award method was found (see findings doc).
                Progress.AwardXp(RecipeDifficulty.Appropriate);
            }

            Persist();
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
            CommissionController.SceneTransition();
            // Toggle/panel visibility state itself is intentionally NOT reset here - an
            // Open/PinnedOpen panel should survive a zone change without re-appearing as a
            // fresh instance (nothing is destroyed since it was never scene-owned).
            ForageNodeController.SceneTransition();
        }

        internal static void Persist()
        {
            if (!_initialized || string.IsNullOrEmpty(_savePath)) return;
            if (!CraftingProgressionStore.Save(_savePath, new CraftingSaveData { Smithing = Progress }))
                LogError(CraftingProgressionStore.LastError);
        }

        internal static void Shutdown()
        {
            Persist();
            ForageNodeController.Shutdown();
            CraftingWindow.ResetTransientState();
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
