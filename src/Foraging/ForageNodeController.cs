using System.Collections.Generic;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // One live spawned node: definition + pure runtime state + scene-bound visual metadata.
    // Nothing here is persisted; scene transitions destroy this whole runtime layer just like
    // vanilla MiningNode depletion resets when its authored scene object is recreated.
    internal sealed class SpawnedForageNode
    {
        internal ForageNodeDefinition Definition;
        internal ForageNodeRuntimeState State;
        internal GameObject Visual;
        internal bool IsDebugPlaceholder;
        internal string ResolvedMeshName = string.Empty;
        internal string ResolvedShaderName = string.Empty;
        internal bool TintApplied;
        internal Vector3 AppliedScale = Vector3.one;
    }

    internal static class ForageNodeController
    {
        internal static readonly ForageNodeCatalog Catalog = new ForageNodeCatalog();
        private static readonly List<SpawnedForageNode> _spawned = new List<SpawnedForageNode>();
        private static string _spawnedScene = string.Empty;

        // The one Wild Herb node this mod tracks. It intentionally remains invalid until a human
        // supplies real /craftdiag forage scan + forage pos evidence. Catalog.Validate is the
        // authoritative gate that prevents placeholder data from ever spawning.
        private static readonly ForageNodeDefinition _candidateDefinition = BuildCandidateDefinition();
        private static readonly ForageDefinitionRejectReason _candidateRejectReason;

        internal static string LastSpawnFailureReason = string.Empty;
        internal static string LastGatherSummary = string.Empty;
        internal static string LastFailureReason = string.Empty;

        static ForageNodeController()
        {
            _candidateRejectReason = Catalog.TryRegister(_candidateDefinition);
        }

        private static ForageNodeDefinition BuildCandidateDefinition()
        {
            return new ForageNodeDefinition
            {
                Id = "WildHerb_001",
                DisplayName = "Wild Herb",
                Scene = ForagingConfig.UnsurveyedLabel,
                PositionSet = false,
                VisualSourceScene = string.Empty,
                VisualSourceHierarchyPath = string.Empty,
                Scale = 1.3f,
                TintEnabled = false,
                RespawnSeconds = 300f,
                RewardItemId = CraftingExpandedItemIds.WildHerbId,
                RewardQuantity = 1
            };
        }

        internal static void Tick(float deltaSeconds)
        {
            if (!ForagingConfig.EnableForaging.Value)
            {
                if (_spawned.Count > 0) DespawnAll();
                return;
            }

            if (!ForagingConfig.EnablePoCNode.Value)
            {
                if (_spawned.Count > 0) DespawnAll();
                return;
            }

            string scene = SafeSceneName();
            if (!string.Equals(scene, _spawnedScene, System.StringComparison.OrdinalIgnoreCase))
                RespawnForScene(scene);

            foreach (SpawnedForageNode node in _spawned)
            {
                node.State.Tick(deltaSeconds);
                UpdateVisualForState(node);
            }

            KeyCode key = ForagingConfig.ForageKey.Value;
            if (key == KeyCode.None || !Input.GetKeyDown(key)) return;
            if (IsChatFocused()) return;

            float interactionRange = ForagingConfig.InteractionRange.Value;
            if (!ForagingRuntimeConfigValidation.IsValidInteractionRange(interactionRange))
            {
                LastFailureReason = "Configured ForagingInteractionRange is invalid; gather suppressed.";
                return;
            }

            Vector3 playerPos;
            if (!GameForagingApi.TryGetPlayerPosition(out playerPos))
            {
                LastFailureReason = "Could not resolve player position.";
                return;
            }

            // One key press may gather exactly one node. The old per-node Input.GetKeyDown loop
            // would have gathered every overlapping node in range on the same frame; selecting
            // the nearest eligible node makes interaction deterministic and prevents multi-node
            // reward duplication once the catalog contains more than one plant.
            SpawnedForageNode nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (SpawnedForageNode node in _spawned)
            {
                if (node.State.Availability != ForageAvailability.Available || node.Visual == null) continue;
                float distance = Vector3.Distance(playerPos, node.Visual.transform.position);
                if (distance > interactionRange || distance >= nearestDistance) continue;
                nearest = node;
                nearestDistance = distance;
            }

            if (nearest == null)
            {
                LastFailureReason = "No available Foraging node within " + interactionRange.ToString("F1") + "m.";
                return;
            }

            TryGather(nearest);
        }

        private static bool IsChatFocused()
        {
            try { return GameData.PlayerTyping; }
            catch { return true; }
        }

        private static void TryGather(SpawnedForageNode node)
        {
            if (node == null || node.State.Availability != ForageAvailability.Available)
            {
                LastFailureReason = "Node already depleted.";
                return;
            }

            // Grant first, then transition to Depleted. A failed inventory/custom-item grant must
            // leave the plant available so the player never loses the node without receiving the
            // reward (the previous ordering depleted before grant and could strand a failed node).
            bool granted;
            if (CraftingExpandedItemIds.IsInOwnedRange(node.Definition.RewardItemId))
            {
                granted = GameItemRegistryApi.GrantRegisteredItem(node.Definition.RewardItemId, node.Definition.RewardQuantity);
                if (!granted)
                {
                    LastFailureReason = "Custom item '" + node.Definition.RewardItemId + "' not available (state=" + CraftingExpandedItems.WildHerbState() + ").";
                    return;
                }
            }
            else
            {
                object item = GameForagingApi.TryGetVanillaItemById(node.Definition.RewardItemId);
                if (item == null)
                {
                    LastFailureReason = "Reward item id '" + node.Definition.RewardItemId + "' did not resolve via ItemDatabase.";
                    return;
                }
                granted = GameForagingApi.GrantVanillaItem(item, node.Definition.RewardQuantity);
                if (!granted)
                {
                    LastFailureReason = "Inventory grant failed for '" + node.Definition.RewardItemId + "'.";
                    return;
                }
            }

            float effectiveRespawn = node.Definition.RespawnSeconds;
            float debugOverride = ForagingConfig.DebugRespawnSecondsOverride.Value;
            if (ForagingRuntimeConfigValidation.IsValidDebugRespawnOverride(debugOverride) && debugOverride > 0f)
                effectiveRespawn = debugOverride;

            if (!node.State.TryGather(effectiveRespawn))
            {
                // This should be unreachable on Unity's main thread after the availability check
                // above. Log loudly because the item has already been granted and duplicating it
                // on a retry would be worse than leaving the visual available.
                LastFailureReason = "Reward granted but node state transition unexpectedly failed; do not retry until diagnostics are reviewed.";
                CraftingController.LogError("Foraging: " + LastFailureReason + " node=" + node.Definition.Id);
                return;
            }

            LastGatherSummary = node.Definition.DisplayName + " x" + node.Definition.RewardQuantity;
            LastFailureReason = string.Empty;
            UpdateVisualForState(node);
        }

        private static void UpdateVisualForState(SpawnedForageNode node)
        {
            if (node == null || node.Visual == null) return;
            bool visible = node.State.Availability == ForageAvailability.Available;
            try
            {
                Renderer[] renderers = node.Visual.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                    if (renderer != null) renderer.enabled = visible;
            }
            catch { }
        }

        private static void RespawnForScene(string scene)
        {
            DespawnAll();
            _spawnedScene = scene;
            if (string.IsNullOrEmpty(scene)) return;

            foreach (ForageNodeDefinition def in Catalog.GetForScene(scene))
            {
                GameForagingApi.VisualResolution resolution = GameForagingApi.TryResolveVisualSource(def);
                GameObject visual;
                bool isDebugPlaceholder = false;

                if (resolution.Source != null)
                {
                    visual = GameForagingApi.BuildVisualClone(resolution.Source, def.Id);
                }
                else if (ForagingConfig.AllowDebugPlaceholderVisual.Value)
                {
                    visual = GameForagingApi.BuildDebugPlaceholderVisual(def.Id);
                    isDebugPlaceholder = true;
                    CraftingController.LogInfo("Foraging: '" + def.Id + "' visual source unresolved (" + resolution.FailureReason + ") - using DEBUG placeholder because AllowDebugPlaceholderVisual=true.");
                }
                else
                {
                    LastSpawnFailureReason = def.Id + ": " + (resolution.FailureReason ?? "visual source did not resolve");
                    CraftingController.LogError("Foraging: '" + def.Id + "' did not spawn - " + LastSpawnFailureReason);
                    continue;
                }

                if (visual == null)
                {
                    LastSpawnFailureReason = def.Id + ": visual clone produced no renderable geometry.";
                    continue;
                }

                visual.transform.position = new Vector3(def.Position.X, def.Position.Y, def.Position.Z);
                visual.transform.rotation = Quaternion.Euler(0f, def.RotationY, 0f);

                // Definition.Scale is a multiplier, not an absolute replacement. The regular
                // clone's VisualRoot already preserves the native source lossy scale; the outer
                // mod-owned root applies only the authored multiplier. Debug placeholders have no
                // native scale, so the same multiplier naturally applies to their unit sphere.
                visual.transform.localScale = Vector3.one * def.Scale;
                Vector3 baseScale = isDebugPlaceholder ? Vector3.one : resolution.SourceLossyScale;
                Vector3 appliedScale = new Vector3(baseScale.x * def.Scale, baseScale.y * def.Scale, baseScale.z * def.Scale);

                bool tintApplied = false;
                if (def.TintEnabled)
                    tintApplied = GameForagingApi.TryApplyTint(visual, new Color(def.TintR, def.TintG, def.TintB), def.TintColorProperty);

                _spawned.Add(new SpawnedForageNode
                {
                    Definition = def,
                    State = new ForageNodeRuntimeState(),
                    Visual = visual,
                    IsDebugPlaceholder = isDebugPlaceholder,
                    ResolvedMeshName = string.IsNullOrEmpty(resolution.MeshName) ? (isDebugPlaceholder ? "(debug placeholder)" : "(none)") : resolution.MeshName,
                    ResolvedShaderName = string.IsNullOrEmpty(resolution.ShaderName) ? (isDebugPlaceholder ? "(debug placeholder)" : "(none)") : resolution.ShaderName,
                    TintApplied = tintApplied,
                    AppliedScale = appliedScale
                });
                LastSpawnFailureReason = string.Empty;
            }
        }

        internal static void SceneTransition()
        {
            DespawnAll();
        }

        internal static void Shutdown()
        {
            DespawnAll();
        }

        private static void DespawnAll()
        {
            foreach (SpawnedForageNode node in _spawned)
                try { if (node.Visual != null) UnityEngine.Object.Destroy(node.Visual); } catch { }
            _spawned.Clear();
            _spawnedScene = string.Empty;
        }

        internal static string SafeSceneName()
        {
            return GameForagingApi.SafeSceneName();
        }

        internal static int AvailableCount()
        {
            int count = 0;
            foreach (SpawnedForageNode node in _spawned) if (node.State.Availability == ForageAvailability.Available) count++;
            return count;
        }

        internal static int DepletedCount()
        {
            int count = 0;
            foreach (SpawnedForageNode node in _spawned) if (node.State.Availability == ForageAvailability.Depleted) count++;
            return count;
        }

        internal static int SpawnedCount { get { return _spawned.Count; } }

        internal static string DescribePrimaryNode()
        {
            if (Catalog.Count == 0)
            {
                return "id=" + _candidateDefinition.Id +
                    " valid=false reason=" + _candidateRejectReason +
                    " (see docs/FORAGING_ASSET_SURVEY.md to supply real scene/position/visual-source data)";
            }

            if (_spawned.Count > 0)
            {
                SpawnedForageNode node = _spawned[0];
                return "id=" + node.Definition.Id + " valid=true" +
                    " pos=(" + node.Definition.Position.X + "," + node.Definition.Position.Y + "," + node.Definition.Position.Z + ")" +
                    " source=" + node.Definition.VisualSourceScene + ":" + node.Definition.VisualSourceHierarchyPath +
                    " sourceResolved=" + (!node.IsDebugPlaceholder) +
                    " mesh=" + node.ResolvedMeshName +
                    " shader=" + node.ResolvedShaderName +
                    " scaleMultiplier=" + node.Definition.Scale +
                    " appliedScale=(" + node.AppliedScale.x.ToString("F2") + "," + node.AppliedScale.y.ToString("F2") + "," + node.AppliedScale.z.ToString("F2") + ")" +
                    " tintProperty=" + (node.Definition.TintEnabled ? node.Definition.TintColorProperty : "(off)") +
                    " tintApplied=" + node.TintApplied +
                    " state=" + node.State.Availability +
                    " respawnRemaining=" + node.State.RemainingRespawnSeconds.ToString("F0") + "s";
            }

            return "id=" + _candidateDefinition.Id + " valid=true spawned=false" +
                (string.IsNullOrEmpty(LastSpawnFailureReason) ? " (not in this scene)" : " reason=" + LastSpawnFailureReason);
        }
    }
}
