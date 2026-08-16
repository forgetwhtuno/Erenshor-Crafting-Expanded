using System;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    internal static class ForagingProgressionController
    {
        private const float IdentityProbeSeconds = 0.25f;
        private const float DepletionCheckpointSeconds = 30f;
        private const float RequirementMessageCooldownSeconds = 1.25f;

        private static string _dataRoot = string.Empty;
        private static string _characterKey = string.Empty;
        private static string _playerName = string.Empty;
        private static int _slotIndex = -1;
        private static string _savePath = string.Empty;
        private static ForagingPersistentState _state = new ForagingPersistentState();
        private static bool _initialized;
        private static bool _loaded;
        private static bool _characterChanged;
        private static float _nextIdentityProbe;
        private static float _nextDepletionCheckpoint;
        private static string _lastRequirementKey = string.Empty;
        private static float _nextRequirementMessage;

        internal static void Initialize(string dataRoot)
        {
            _dataRoot = dataRoot ?? string.Empty;
            _characterKey = string.Empty;
            _playerName = string.Empty;
            _slotIndex = -1;
            _savePath = string.Empty;
            _state = new ForagingPersistentState();
            _state.Normalize();
            _loaded = false;
            _characterChanged = false;
            _nextIdentityProbe = 0f;
            _nextDepletionCheckpoint = 0f;
            _lastRequirementKey = string.Empty;
            _nextRequirementMessage = 0f;
            ForageDepletionLedger.Clear();
            ForageAmbiguousGrantQuarantine.Clear();
            _initialized = true;
        }

        internal static void Tick()
        {
            if (!_initialized) return;
            float now = Time.unscaledTime;
            if (now >= _nextIdentityProbe)
            {
                _nextIdentityProbe = now + IdentityProbeSeconds;
                RefreshIdentity(now);
            }
            if (_loaded && (ForageDepletionLedger.Count > 0 || ForageAmbiguousGrantQuarantine.Count > 0) && now >= _nextDepletionCheckpoint)
            {
                _nextDepletionCheckpoint = now + DepletionCheckpointSeconds;
                Persist(now);
            }
        }

        private static void RefreshIdentity(float now)
        {
            string key;
            string playerName;
            int slotIndex;
            if (!ForagingCharacterIdentity.TryResolve(out key, out playerName, out slotIndex))
            {
                if (_loaded)
                {
                    Persist(now);
                    UnloadCurrent();
                    _characterChanged = true;
                }
                return;
            }
            if (_loaded && string.Equals(_characterKey, key, StringComparison.Ordinal)) return;

            if (_loaded) Persist(now);
            ForageDepletionLedger.Clear();
            ForageAmbiguousGrantQuarantine.Clear();
            _characterKey = key;
            _playerName = playerName;
            _slotIndex = slotIndex;
            _savePath = ForagingProgressionStore.BuildPath(_dataRoot, key);
            _state = ForagingProgressionStore.Load(_savePath);
            _state.Normalize();
            ForageDepletionLedger.ImportRemaining(_state.Depletions, now);
            ForageAmbiguousGrantQuarantine.ImportRemaining(_state.AmbiguousGrants, now);
            _loaded = true;
            _characterChanged = true;
            _nextDepletionCheckpoint = now + DepletionCheckpointSeconds;
            if (!string.IsNullOrEmpty(ForagingProgressionStore.LastError)) CraftingController.LogError(ForagingProgressionStore.LastError);
        }

        private static void UnloadCurrent()
        {
            ForageDepletionLedger.Clear();
            ForageAmbiguousGrantQuarantine.Clear();
            _characterKey = string.Empty;
            _playerName = string.Empty;
            _slotIndex = -1;
            _savePath = string.Empty;
            _state = new ForagingPersistentState();
            _state.Normalize();
            _loaded = false;
            _nextDepletionCheckpoint = 0f;
        }

        internal static bool ConsumeCharacterChanged()
        {
            bool value = _characterChanged;
            _characterChanged = false;
            return value;
        }

        internal static ForagingGatherProgressionResult OnSuccessfulGather(ForageResourceDefinition resource)
        {
            ForagingGatherProgressionResult empty = new ForagingGatherProgressionResult();
            if (!_loaded || resource == null) return empty;
            ForagingGatherProgressionResult result = ForagingProgressionEngine.ApplySuccessfulGather(_state.Progress, _state.Knowledge, resource);
            if (!result.Applied) return result;
            Persist(Time.unscaledTime);
            if (result.NewlyDiscovered)
            {
                // Recipe progression observes the already-committed Foraging discovery. The bridge
                // owns no Foraging persistence and cannot award a discovery before the item grant.
                CraftingRecipeDiscoveryBridge.NotifyResourceDiscovered(resource.RewardItemId);
                NotifyPlayer("Discovered: " + resource.DisplayName + ".");
            }
            if (result.XpAward != null && result.XpAward.LeveledUp) NotifyPlayer("Foraging increased to " + result.XpAward.NewLevel + ".");
            return result;
        }

        internal static bool CanGather(ForageResourceDefinition resource)
        {
            if (!_loaded || resource == null) return false;
            return _state.Progress.Level >= resource.MinimumSkill;
        }

        internal static void NotifyRequirement(ForageResourceDefinition resource)
        {
            if (resource == null) return;
            float now = Time.unscaledTime;
            if (string.Equals(_lastRequirementKey, resource.KnowledgeKey, StringComparison.Ordinal) && now < _nextRequirementMessage) return;
            _lastRequirementKey = resource.KnowledgeKey;
            _nextRequirementMessage = now + RequirementMessageCooldownSeconds;
            if (!_loaded) NotifyPlayer("Foraging progression is waiting for the active character save slot.");
            else NotifyPlayer(resource.DisplayName + " requires Foraging " + resource.MinimumSkill + ".");
        }

        internal static bool HasDiscovered(string resourceKey)
        {
            return _loaded && _state != null && _state.Knowledge != null && _state.Knowledge.HasDiscovered(resourceKey);
        }

        internal static int CurrentLevel { get { return _loaded && _state != null && _state.Progress != null ? _state.Progress.Level : 1; } }
        internal static int CurrentXp { get { return _loaded && _state != null && _state.Progress != null ? _state.Progress.Xp : 0; } }
        internal static int DiscoveredCount { get { return _loaded && _state != null && _state.Knowledge != null ? _state.Knowledge.DiscoveredResourceKeys.Count : 0; } }
        internal static string CurrentCharacterKey { get { return _loaded ? _characterKey : string.Empty; } }
        internal static bool IsReady { get { return _loaded; } }
        internal static string PersistenceState { get { return _loaded ? (string.IsNullOrEmpty(ForagingProgressionStore.LastError) ? "ready" : "error") : "waiting"; } }

        internal static bool RecordAmbiguousGrantQuarantine(string scene, string itemId, float cooldownSeconds)
        {
            if (!_loaded || string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(itemId) || cooldownSeconds <= 0f) return false;
            float now = Time.unscaledTime;
            ForageAmbiguousGrantQuarantine.Record(scene, itemId, now, cooldownSeconds);
            return Persist(now);
        }

        internal static void SceneTransition()
        {
            if (_loaded) Persist(Time.unscaledTime);
        }

        internal static void Shutdown()
        {
            if (_loaded) Persist(Time.unscaledTime);
            UnloadCurrent();
            _initialized = false;
            _characterChanged = false;
        }

        private static bool Persist(float now)
        {
            if (!_loaded || string.IsNullOrEmpty(_savePath) || _state == null) return false;
            _state.Depletions = ForageDepletionLedger.ExportActive(now);
            _state.AmbiguousGrants = ForageAmbiguousGrantQuarantine.ExportActive(now);
            _state.Normalize();
            bool saved = ForagingProgressionStore.Save(_savePath, _state);
            if (!saved) CraftingController.LogError(ForagingProgressionStore.LastError);
            return saved;
        }

        internal static void NotifyGatherFeedback(string message)
        {
            NotifyPlayer(message);
        }

        private static void NotifyPlayer(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            try { UpdateSocialLog.LogAdd("[Foraging] " + message, "yellow"); }
            catch { CraftingController.LogInfo("Foraging: " + message); }
        }
    }
}
