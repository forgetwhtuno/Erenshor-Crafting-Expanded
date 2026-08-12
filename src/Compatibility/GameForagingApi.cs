using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorCraftingExpanded
{
    // Compatibility layer for Foraging, isolated from Crafting's GameCraftingApi even though
    // both ultimately touch GameData/Inventory - kept separate because the two systems' native
    // evidence and failure modes are unrelated (see docs/NATIVE_MINING_AND_FORAGING_FINDINGS.md).
    internal static class GameForagingApi
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // A cloned visual's hierarchy is capped so a pathological source object (deeply nested,
        // huge child count) can't produce an unbounded clone - see BuildVisualClone.
        private const int MaxClonedNodes = 64;

        // PlayerControl.myTransform is a confirmed field (see findings doc). Reflection is still
        // used for the read so a visibility change doesn't hard-break compilation, consistent
        // with GameCraftingApi's approach.
        internal static bool TryGetPlayerTransform(out Transform transform)
        {
            transform = null;
            try
            {
                PlayerControl control = GameData.PlayerControl;
                if (control == null) return false;
                FieldInfo field = control.GetType().GetField("myTransform", AllInstance);
                transform = field == null ? null : field.GetValue(control) as Transform;
                return transform != null;
            }
            catch { return false; }
        }

        internal static bool TryGetPlayerPosition(out Vector3 position)
        {
            position = Vector3.zero;
            Transform transform;
            if (!TryGetPlayerTransform(out transform)) return false;
            position = transform.position;
            return true;
        }

        internal static bool TryGetPlayerYawDegrees(out float yaw)
        {
            yaw = 0f;
            Transform transform;
            if (!TryGetPlayerTransform(out transform)) return false;
            yaw = transform.eulerAngles.y;
            return true;
        }

        // Resolves an existing vanilla Item by id through the same ItemDatabase.GetItemByID
        // path the game itself uses - see findings doc section 10.A. Returns null (not a thrown
        // exception) if the id doesn't resolve, so callers can fail gracefully.
        internal static object TryGetVanillaItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            try
            {
                object db = GetStaticField("GameData", "ItemDB");
                if (db == null) return null;
                MethodInfo method = db.GetType().GetMethod("GetItemByID", AllInstance);
                return method == null ? null : method.Invoke(db, new object[] { itemId });
            }
            catch { return null; }
        }

        // Same verified-safe reward path as vanilla mining: AddItemToInv, falling back to
        // ForceItemToInv on a full inventory (see findings doc section 1, PlayerCombat.TryMine).
        internal static bool GrantVanillaItem(object item, int quantity)
        {
            if (item == null || quantity <= 0) return false;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return false;
                Type invType = playerInv.GetType();

                MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
                bool added = false;
                if (addWithQty != null)
                {
                    object result = addWithQty.Invoke(playerInv, new object[] { item, quantity });
                    added = result is bool && (bool)result;
                }
                if (!added)
                {
                    // The one-item native overload cannot truthfully satisfy a multi-item grant.
                    // Fail closed rather than partially grant N=1 and then let a caller retry the
                    // node/command and accidentally duplicate. Current Wild Herb nodes request 1;
                    // future >1 rewards must prove the native quantity overload in the live build.
                    if (quantity != 1) return false;
                    MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
                    if (add != null)
                    {
                        object result = add.Invoke(playerInv, new object[] { item });
                        added = result is bool && (bool)result;
                    }
                }
                if (added) return true;

                if (quantity != 1) return false;
                MethodInfo force = FindMethod(invType, "ForceItemToInv", item.GetType());
                if (force == null) return false;
                force.Invoke(playerInv, new object[] { item });
                return true;
            }
            catch { return false; }
        }

        internal sealed class VisualResolution
        {
            internal GameObject Source;
            internal string FailureReason;
            internal string MeshName = string.Empty;
            internal string ShaderName = string.Empty;
            internal Vector3 SourceLossyScale = Vector3.one;
        }

        // Resolves a definition's (VisualSourceScene, VisualSourceHierarchyPath) locator against
        // the currently loaded scene. Never returns a "close enough" guess - either the exact
        // scene+path resolves to a GameObject carrying a real renderer, or resolution fails with
        // an explicit reason. Resolution is restricted to the player's loaded Unity scene and
        // rejects duplicate hierarchy paths instead of letting GameObject.Find silently choose one.
        internal static VisualResolution TryResolveVisualSource(ForageNodeDefinition definition)
        {
            VisualResolution result = new VisualResolution();
            try
            {
                string currentScene = SafeSceneName();
                if (!string.Equals(currentScene, definition.VisualSourceScene, StringComparison.OrdinalIgnoreCase))
                {
                    result.FailureReason = "Current scene '" + currentScene + "' does not match VisualSourceScene '" + definition.VisualSourceScene + "'.";
                    return result;
                }

                Scene playerScene;
                if (!TryGetPlayerScene(out playerScene))
                {
                    result.FailureReason = "Could not resolve the player's loaded Unity scene.";
                    return result;
                }
                if (!string.Equals(playerScene.name, definition.VisualSourceScene, StringComparison.OrdinalIgnoreCase))
                {
                    result.FailureReason = "Player Unity scene '" + playerScene.name + "' does not match VisualSourceScene '" + definition.VisualSourceScene + "'.";
                    return result;
                }

                int pathMatches;
                GameObject candidate = FindExactHierarchyPathInScene(playerScene, definition.VisualSourceHierarchyPath, out pathMatches);
                if (candidate == null)
                {
                    result.FailureReason = pathMatches > 1
                        ? ("Hierarchy path '" + definition.VisualSourceHierarchyPath + "' is ambiguous in scene '" + playerScene.name + "' (" + pathMatches + " matches). Choose a uniquely named source path.")
                        : ("Hierarchy path '" + definition.VisualSourceHierarchyPath + "' was not found in scene '" + playerScene.name + "'.");
                    return result;
                }

                MeshRenderer renderer = null;
                MeshFilter filter = null;
                MeshRenderer[] renderers = candidate.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer possible in renderers)
                {
                    if (possible == null) continue;
                    MeshFilter possibleFilter = possible.GetComponent<MeshFilter>();
                    if (possibleFilter == null || possibleFilter.sharedMesh == null) continue;
                    renderer = possible;
                    filter = possibleFilter;
                    break;
                }
                if (renderer == null || filter == null)
                {
                    result.FailureReason = "Resolved object '" + definition.VisualSourceHierarchyPath + "' has no clone-compatible MeshRenderer + MeshFilter/sharedMesh in itself or its children.";
                    return result;
                }

                result.Source = candidate;
                result.SourceLossyScale = candidate.transform.lossyScale;
                result.MeshName = filter.sharedMesh.name ?? "(unnamed mesh)";
                result.ShaderName = renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null ? renderer.sharedMaterial.shader.name : "(none)";
                return result;
            }
            catch (Exception ex)
            {
                result.FailureReason = "Resolution threw: " + ex.Message;
                return result;
            }
        }

        // Builds a mod-owned visual clone containing only the visual Transform hierarchy plus
        // MeshFilter/MeshRenderer data. The cap applies to *all cloned transforms*, not merely
        // renderers, so a pathological deep/empty hierarchy cannot allocate without bound.
        // Empty branches are pruned and no gameplay MonoBehaviours/colliders/triggers are copied.
        internal static GameObject BuildVisualClone(GameObject source, string nodeId)
        {
            if (source == null) return null;

            // Keep the mod-owned node transform separate from the native visual transform. The
            // outer root owns authored position/yaw + scale multiplier; VisualRoot preserves the
            // selected source object's current world rotation and lossy scale so a mesh that only
            // looks correct because of parent scaling/rotation does not get flattened to an
            // arbitrary identity transform when cloned elsewhere.
            GameObject clone = new GameObject("ForageNode_" + nodeId);
            clone.layer = source.layer;
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.layer = source.layer;
            visualRoot.transform.SetParent(clone.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = source.transform.rotation;
            visualRoot.transform.localScale = source.transform.lossyScale;

            int clonedNodeCount = 2;
            int copiedRendererCount = 0;
            bool hasVisual = CloneVisualBranch(source, visualRoot.transform, ref clonedNodeCount, ref copiedRendererCount);
            if (!hasVisual || copiedRendererCount == 0)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }
            return clone;
        }

        // Explicit development-only fallback - never used unless the caller (ForageNodeController,
        // gated on ForagingConfig.AllowDebugPlaceholderVisual) asks for it directly.
        internal static GameObject BuildDebugPlaceholderVisual(string nodeId)
        {
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            placeholder.name = "ForageNode_DebugPlaceholder_" + nodeId;
            Collider collider = placeholder.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            return placeholder;
        }

        private static bool CloneVisualBranch(GameObject source, Transform target, ref int clonedNodeCount, ref int copiedRendererCount)
        {
            if (source == null || target == null) return false;
            bool hasVisual = false;
            try
            {
                target.gameObject.layer = source.layer;

                MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
                MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
                if (sourceFilter != null && sourceRenderer != null && sourceFilter.sharedMesh != null)
                {
                    MeshFilter filter = target.gameObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = sourceFilter.sharedMesh;

                    MeshRenderer renderer = target.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                    renderer.enabled = sourceRenderer.enabled;
                    renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                    renderer.receiveShadows = sourceRenderer.receiveShadows;
                    renderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                    renderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                    renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                    renderer.sortingOrder = sourceRenderer.sortingOrder;
                    renderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;

                    copiedRendererCount++;
                    hasVisual = true;
                }

                foreach (Transform child in source.transform)
                {
                    if (clonedNodeCount >= MaxClonedNodes) break;
                    GameObject childClone = new GameObject(child.name);
                    clonedNodeCount++;
                    childClone.layer = child.gameObject.layer;
                    childClone.transform.SetParent(target, false);
                    childClone.transform.localPosition = child.localPosition;
                    childClone.transform.localRotation = child.localRotation;
                    childClone.transform.localScale = child.localScale;

                    bool childHasVisual = CloneVisualBranch(child.gameObject, childClone.transform, ref clonedNodeCount, ref copiedRendererCount);
                    if (!childHasVisual)
                    {
                        UnityEngine.Object.Destroy(childClone);
                    }
                    else
                    {
                        hasVisual = true;
                    }
                }
            }
            catch { }
            return hasVisual;
        }

        // Tint only the mod-owned renderers via MaterialPropertyBlock. The shader/property has
        // already been discovered from the live source; if no shared material on a renderer
        // exposes that property, that renderer is left untouched. We intentionally do not call
        // renderer.material as a fallback because doing so allocates per-instance Material
        // objects that then require explicit lifetime management. Safe failure is simply no tint.
        internal static bool TryApplyTint(GameObject node, Color color, string colorPropertyName)
        {
            if (node == null || string.IsNullOrEmpty(colorPropertyName)) return false;
            bool anyApplied = false;
            try
            {
                Renderer[] renderers = node.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    bool supportsProperty = false;
                    Material[] materials = renderer.sharedMaterials;
                    if (materials != null)
                    {
                        foreach (Material material in materials)
                        {
                            if (material != null && material.HasProperty(colorPropertyName))
                            {
                                supportsProperty = true;
                                break;
                            }
                        }
                    }
                    if (!supportsProperty) continue;

                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(colorPropertyName, color);
                    renderer.SetPropertyBlock(block);
                    anyApplied = true;
                }
            }
            catch { return anyApplied; }
            return anyApplied;
        }

        // Shader.GetPropertyCount/GetPropertyName/GetPropertyType are real runtime-safe members
        // of UnityEngine.Shader in this build (confirmed via IL disassembly of
        // UnityEngine.CoreModule.dll before use - not UnityEditor-only reflection). Used only by
        // the explicit /craftdiag forage scan command, never per-frame.
        internal static List<string> GetColorShaderProperties(Material material)
        {
            List<string> names = new List<string>();
            try
            {
                if (material == null || material.shader == null) return names;
                Shader shader = material.shader;
                int count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color)
                        names.Add(shader.GetPropertyName(i));
                }
            }
            catch { }
            return names;
        }

        internal static string SafeSceneName()
        {
            Scene scene;
            if (TryGetPlayerScene(out scene)) return scene.name ?? string.Empty;
            try { return GameData.SceneName ?? string.Empty; } catch { return string.Empty; }
        }

        private static bool TryGetPlayerScene(out Scene scene)
        {
            scene = default(Scene);
            try
            {
                Transform player;
                if (!TryGetPlayerTransform(out player) || player == null || player.gameObject == null) return false;
                scene = player.gameObject.scene;
                return scene.IsValid() && scene.isLoaded;
            }
            catch { return false; }
        }

        // Resolve only inside the player's verified loaded scene. Unity's GameObject.Find can
        // cross loaded scenes and silently choose the first duplicate path; authored resource
        // nodes need fail-closed, deterministic source resolution instead. Inactive objects are
        // included because Scene.GetRootGameObjects()/Transform children remain enumerable.
        private static GameObject FindExactHierarchyPathInScene(Scene scene, string hierarchyPath, out int matchCount)
        {
            matchCount = 0;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(hierarchyPath)) return null;
            string[] rawSegments = hierarchyPath.Split('/');
            List<string> segments = new List<string>();
            foreach (string raw in rawSegments)
            {
                string segment = raw == null ? string.Empty : raw.Trim();
                if (!string.IsNullOrEmpty(segment)) segments.Add(segment);
            }
            if (segments.Count == 0 || segments.Count > 64) return null;

            List<Transform> current = new List<Transform>();
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root != null && string.Equals(root.name, segments[0], StringComparison.Ordinal)) current.Add(root.transform);

                for (int segmentIndex = 1; segmentIndex < segments.Count && current.Count > 0; segmentIndex++)
                {
                    List<Transform> next = new List<Transform>();
                    foreach (Transform parent in current)
                    {
                        if (parent == null) continue;
                        foreach (Transform child in parent)
                            if (child != null && string.Equals(child.name, segments[segmentIndex], StringComparison.Ordinal)) next.Add(child);
                    }
                    current = next;
                }
            }
            catch { return null; }

            matchCount = current.Count;
            return current.Count == 1 && current[0] != null ? current[0].gameObject : null;
        }

        private static MethodInfo FindMethod(Type declaringType, string name, params Type[] argTypes)
        {
            foreach (MethodInfo candidate in declaringType.GetMethods(AllInstance))
            {
                if (candidate.Name != name) continue;
                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length != argTypes.Length) continue;
                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i])) { match = false; break; }
                }
                if (match) return candidate;
            }
            return null;
        }

        private static object GetStaticField(string typeName, string fieldName)
        {
            Type type = FindType(typeName);
            if (type == null) return null;
            FieldInfo field = type.GetField(fieldName, AllStatic);
            return field == null ? null : field.GetValue(null);
        }

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                try { Type type = assembly.GetType(name, false); if (type != null) return type; } catch { }
            return null;
        }
    }
}
