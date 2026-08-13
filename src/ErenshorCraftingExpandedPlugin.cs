using System;
using System.Linq;
using Lunaris;
using Lunaris.Config;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorCraftingExpanded
{
    [LunarisPlugin("forgetwhtuno.erenshor.craftingexpanded", "0.2.0", "forgetwhtuno",
        "Horizontal-progression expansion to Erenshor's native crafting: forge quality-of-life, Smithing progression, an experimental commission PoC, and a mod-owned Foraging system.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorCraftingExpandedPlugin : LunarisPlugin
    {
        internal const string Version = "0.2.0";
        private Harmony _harmony;
        private CraftingExpandedSettings _settings;
        private CraftingSuiteAuraProvider _auraProvider;
        private bool _loggedStartupSummary;
        private bool _runtimeReady;

        private void Awake()
        {
            ErenshorCraftingExpandedPluginHolder.Instance = this;
            _settings = new CraftingExpandedSettings();
            Config.Register(ref _settings);
            string dataDir = System.IO.Path.Combine(System.IO.Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorCraftingExpanded");
            CraftingController.Initialize(_settings, dataDir);
            _harmony = new Harmony("forgetwhtuno.erenshor.craftingexpanded");

            // Optional Suite Hub transport adapter. Never assumed present; registration failure
            // must never block normal standalone crafting/foraging.
            try
            {
                _auraProvider = new CraftingSuiteAuraProvider();
                _auraProvider.Register(this);
            }
            catch (Exception ex) { Logging.LogError("Crafting Suite Aura provider setup failed: " + ex); }

            // A single missing Harmony patch target (e.g. a renamed native method after a game
            // update) would otherwise throw here and silently prevent the whole plugin from
            // loading, with no clue why - log the exact failure instead of letting the generic
            // loader error be the only trace.
            try
            {
                _harmony.PatchAll();
                _runtimeReady = true;
                Logging.LogInfo("Erenshor Crafting Expanded " + Version + ": Harmony patches applied OK (" + _harmony.GetPatchedMethods().Count() + " methods patched).");
            }
            catch (Exception ex)
            {
                Logging.LogError("Erenshor Crafting Expanded " + Version + ": Harmony PatchAll FAILED - " + ex);
                // PatchAll can fail after applying an earlier patch. Avoid leaving a partially
                // patched feature set running under the false assumption that every hook exists.
                try { _harmony.UnpatchSelf(); } catch { }
                _runtimeReady = false;
                Logging.LogError("Erenshor Crafting Expanded is fail-closed for this session because its verified patch set was incomplete.");
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Logging.LogInfo("Erenshor Crafting Expanded " + Version + " loaded.");
        }

        private void Update()
        {
            try
            {
                if (_runtimeReady) CraftingController.Tick();
                // The retained control UI remains available even when the gameplay patch set
                // fails closed, so the user can still inspect status/re-enable settings and is
                // never stranded without the standalone recovery surface.
                CraftingController.TickUi(_auraProvider != null && _auraProvider.Registered);
            }
            catch (Exception ex)
            {
                SuiteDragHandler.ForceReleaseIfOwned();
                Logging.LogError("Crafting update failed: " + ex);
            }
            if (!_loggedStartupSummary && CraftingExpandedItems.AttemptedThisSession)
            {
                _loggedStartupSummary = true;
                try { LogStartupSummary(); } catch (Exception ex) { Logging.LogError("Crafting startup summary failed: " + ex); }
            }
        }

        // Logged exactly once, as soon as the ItemDatabase.Start postfix has run (see
        // Items/CraftingExpandedItems.cs) - everything a tester needs to confirm at boot without
        // spamming LogOutput.log on every frame.
        private void LogStartupSummary()
        {
            Logging.LogInfo("Erenshor Crafting Expanded " + Version + " startup summary:");
            Logging.LogInfo("  Foraging enabled=" + ForagingConfig.EnableForaging.Value + " pocNodeEnabled=" + ForagingConfig.EnablePoCNode.Value);
            Logging.LogInfo("  Wild Herb id=" + CraftingExpandedItemIds.WildHerbId +
                " state=" + CraftingExpandedItems.WildHerbState() +
                " baseItem=" + (string.IsNullOrEmpty(GameItemRegistryApi.LastBaseItemName) ? "(none found)" : GameItemRegistryApi.LastBaseItemName) +
                " baseItemId=" + (string.IsNullOrEmpty(GameItemRegistryApi.LastBaseItemId) ? "(unknown)" : GameItemRegistryApi.LastBaseItemId));
            string conflict = CraftingExpandedItems.ConflictingItemName();
            string failure = CraftingExpandedItems.LastFailureReason();
            if (!string.IsNullOrEmpty(conflict)) Logging.LogWarning("  Wild Herb id collision with existing item: " + conflict);
            if (!string.IsNullOrEmpty(failure)) Logging.LogWarning("  Wild Herb registration issue: " + failure);
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { CraftingController.SceneTransition(); }
        private void OnSceneUnloaded(Scene scene) { CraftingController.SceneTransition(); }

        private void OnDestroy()
        {
            try { SuiteDragHandler.ForceReleaseIfOwned(); } catch { }
            try { CraftingController.Shutdown(); } catch { }
            SuiteUiPolicy.Reset();
            ErenshorCraftingExpandedPluginHolder.Instance = null;
            try { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneUnloaded -= OnSceneUnloaded; } catch { }
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _harmony = null;
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
        }

        internal bool RuntimeReady { get { return _runtimeReady; } }
        internal void SaveSettingsPublic() { try { Config.Save(); } catch { } }
        internal void LogErrorPublic(string message) { Logging.LogError(message); }
        internal void LogInfoPublic(string message) { Logging.LogInfo(message); }

        internal bool HandleCommand(TypeText typeText, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string command = raw.Trim();

            if (command.Equals("/craftdiag", StringComparison.OrdinalIgnoreCase))
            {
                ClearChatInput(typeText);
                CraftingDiagnostics.Report(Version);
                return true;
            }

            // Development/test-only subcommand, not a real gameplay command - grants exactly
            // one Wild Herb through the same verified native inventory-grant path used
            // everywhere else in this mod. See docs/NATIVE_ITEM_REGISTRY_FINDINGS.md.
            if (command.Equals("/craftdiag giveherb", StringComparison.OrdinalIgnoreCase))
            {
                ClearChatInput(typeText);
                CraftingDiagnostics.ReportGiveHerb();
                return true;
            }

            // Development-only asset survey subcommands - see docs/FORAGING_ASSET_SURVEY.md.
            // Not a general world-editing console: these only ever report read-only information
            // about the player's transform and nearby renderers, never modify game state.
            if (command.Equals("/craftdiag forage pos", StringComparison.OrdinalIgnoreCase))
            {
                ClearChatInput(typeText);
                ForagingSurvey.ReportPosition();
                return true;
            }
            const string scanPrefix = "/craftdiag forage scan";
            if (command.Equals(scanPrefix, StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith(scanPrefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                ClearChatInput(typeText);
                string filter = command.Length > scanPrefix.Length ? command.Substring(scanPrefix.Length).Trim() : string.Empty;
                ForagingSurvey.ReportScan(filter);
                return true;
            }

            return false;
        }

        private static void ClearChatInput(TypeText typeText)
        {
            try { if (typeText != null && typeText.typed != null) typeText.typed.text = string.Empty; } catch { }
        }
    }

    [HarmonyPatch(typeof(TypeText), "CheckCommands")]
    internal static class CraftingChatPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TypeText __instance)
        {
            try
            {
                if (ErenshorCraftingExpandedPluginHolder.Instance == null) return true;
                string text = __instance == null || __instance.typed == null ? string.Empty : __instance.typed.text;
                return !ErenshorCraftingExpandedPluginHolder.Instance.HandleCommand(__instance, text);
            }
            catch { return true; }
        }
    }

    internal static class ErenshorCraftingExpandedPluginHolder
    {
        internal static ErenshorCraftingExpandedPlugin Instance;
    }
}
