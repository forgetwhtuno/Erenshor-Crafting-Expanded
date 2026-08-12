using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // One scanned environmental renderer's worth of runtime survey evidence. The scanner is
    // development-only and invoked explicitly from /craftdiag forage scan; none of these live
    // details are persisted across scene transitions.
    internal sealed class RendererScanResult
    {
        internal float Distance;
        internal int SortRank;
        internal string SceneName;
        internal string HierarchyPath;
        internal string GameObjectName;
        internal Vector3 WorldPosition;
        internal Vector3 LocalScale;
        internal Vector3 BoundsCenter;
        internal Vector3 BoundsSize;
        internal string RendererType;
        internal string MeshName;
        internal List<string> MaterialNames = new List<string>();
        internal List<string> ShaderNames = new List<string>();
        internal List<string> ColorPropertyNames = new List<string>();
        internal bool ActiveInHierarchy;
        internal List<string> ComponentTypeNames = new List<string>();
    }

    internal sealed class ForagingScanRejectionSummary
    {
        internal int TotalRenderers;
        internal int OutsideRadius;
        internal int PlayerOwned;
        internal int ModOwned;
        internal int ActorOwned;
        internal int OtherScene;
        internal int EffectRenderer;
        internal int NoCloneableMesh;
        internal int FilterMismatch;
    }

    internal sealed class ForagingScanReport
    {
        internal readonly List<RendererScanResult> Results = new List<RendererScanResult>();
        internal readonly ForagingScanRejectionSummary Rejected = new ForagingScanRejectionSummary();
    }

    // Development-only asset survey. The first live scan showed why positive environmental
    // filtering matters: the nearest renderers were the player's eyebrows/armor/effects. This
    // implementation excludes the current player hierarchy and Crafting Expanded's own clones,
    // rejects effect/no-mesh renderers, then ranks clone-compatible environmental meshes by
    // approximate size and distance. It never runs from Update().
    internal static class ForagingAssetScanApi
    {
        internal static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null) return "(null)";
            List<string> segments = new List<string>();
            Transform current = transform;
            int guard = 0;
            while (current != null && guard < 64)
            {
                segments.Add(current.name);
                current = current.parent;
                guard++;
            }
            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        internal static ForagingScanReport ScanNearby(Vector3 origin, float radiusMeters, int maxResults, string nameFilter)
        {
            ForagingScanReport report = new ForagingScanReport();
            Transform playerTransform;
            Transform playerRoot = GameForagingApi.TryGetPlayerTransform(out playerTransform) && playerTransform != null
                ? playerTransform.root
                : null;
            int playerSceneHandle = -1;
            try
            {
                if (playerTransform != null && playerTransform.gameObject != null && playerTransform.gameObject.scene.IsValid())
                    playerSceneHandle = playerTransform.gameObject.scene.handle;
            }
            catch { playerSceneHandle = -1; }

            Renderer[] all = UnityEngine.Object.FindObjectsOfType<Renderer>();
            report.Rejected.TotalRenderers = all == null ? 0 : all.Length;
            if (all == null) return report;

            foreach (Renderer renderer in all)
            {
                if (renderer == null) continue;

                if (IsSameOrChildOf(renderer.transform, playerRoot))
                {
                    report.Rejected.PlayerOwned++;
                    continue;
                }
                if (IsCraftingExpandedOwned(renderer.transform))
                {
                    report.Rejected.ModOwned++;
                    continue;
                }
                if (playerSceneHandle >= 0)
                {
                    try
                    {
                        if (!renderer.gameObject.scene.IsValid() || renderer.gameObject.scene.handle != playerSceneHandle)
                        {
                            report.Rejected.OtherScene++;
                            continue;
                        }
                    }
                    catch
                    {
                        report.Rejected.OtherScene++;
                        continue;
                    }
                }
                if (IsActorOwned(renderer.transform))
                {
                    report.Rejected.ActorOwned++;
                    continue;
                }

                string rendererType = renderer.GetType().Name;
                if (ForagingScanPolicy.IsEffectRendererType(rendererType))
                {
                    report.Rejected.EffectRenderer++;
                    continue;
                }

                // The current visual-clone path intentionally supports MeshFilter+MeshRenderer.
                // Surveying renderers we cannot safely clone just creates false candidates, so
                // the primary asset scan positively requires that exact shape.
                MeshRenderer meshRenderer = renderer as MeshRenderer;
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshRenderer == null || meshFilter == null || meshFilter.sharedMesh == null)
                {
                    report.Rejected.NoCloneableMesh++;
                    continue;
                }

                Bounds bounds = renderer.bounds;
                float distance = Vector3.Distance(origin, bounds.center);
                if (distance > radiusMeters)
                {
                    report.Rejected.OutsideRadius++;
                    continue;
                }

                string hierarchyPath = BuildHierarchyPath(renderer.transform);
                string meshName = meshFilter.sharedMesh.name ?? "(unnamed mesh)";

                List<string> materialNames = new List<string>();
                List<string> shaderNames = new List<string>();
                List<string> colorPropertyNames = new List<string>();
                Material[] materials = renderer.sharedMaterials;
                if (materials != null)
                {
                    foreach (Material material in materials)
                    {
                        if (material == null) continue;
                        if (!string.IsNullOrEmpty(material.name) && !materialNames.Contains(material.name))
                            materialNames.Add(material.name);
                        if (material.shader != null && !string.IsNullOrEmpty(material.shader.name) && !shaderNames.Contains(material.shader.name))
                            shaderNames.Add(material.shader.name);

                        foreach (string property in GameForagingApi.GetColorShaderProperties(material))
                            if (!colorPropertyNames.Contains(property)) colorPropertyNames.Add(property);
                    }
                }

                if (!ForagingScanPolicy.MatchesFilter(
                    hierarchyPath,
                    renderer.gameObject.name,
                    meshName,
                    materialNames,
                    shaderNames,
                    nameFilter))
                {
                    report.Rejected.FilterMismatch++;
                    continue;
                }

                float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                RendererScanResult entry = new RendererScanResult
                {
                    Distance = distance,
                    SortRank = ForagingScanPolicy.SizeRank(largestDimension),
                    SceneName = SafeSceneName(renderer),
                    HierarchyPath = hierarchyPath,
                    GameObjectName = renderer.gameObject.name,
                    WorldPosition = renderer.transform.position,
                    LocalScale = renderer.transform.localScale,
                    BoundsCenter = bounds.center,
                    BoundsSize = bounds.size,
                    RendererType = rendererType,
                    MeshName = meshName,
                    MaterialNames = materialNames,
                    ShaderNames = shaderNames,
                    ColorPropertyNames = colorPropertyNames,
                    ActiveInHierarchy = renderer.gameObject.activeInHierarchy
                };

                foreach (Component component in renderer.GetComponents<Component>())
                    if (component != null) entry.ComponentTypeNames.Add(component.GetType().Name);

                report.Results.Add(entry);
            }

            report.Results.Sort(CompareCandidates);
            if (maxResults > 0 && report.Results.Count > maxResults)
                report.Results.RemoveRange(maxResults, report.Results.Count - maxResults);
            return report;
        }

        internal static string FormatEntry(int index, RendererScanResult entry)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("#").Append(index)
              .Append(" rank=").Append(entry.SortRank)
              .Append(" dist=").Append(entry.Distance.ToString("F1")).Append("m\n");
            sb.Append("  scene=").Append(entry.SceneName).Append(" path=").Append(entry.HierarchyPath).Append("\n");
            sb.Append("  objectPos=(")
              .Append(entry.WorldPosition.x.ToString("F2")).Append(",")
              .Append(entry.WorldPosition.y.ToString("F2")).Append(",")
              .Append(entry.WorldPosition.z.ToString("F2")).Append(")\n");
            sb.Append("  boundsCenter=(")
              .Append(entry.BoundsCenter.x.ToString("F2")).Append(",")
              .Append(entry.BoundsCenter.y.ToString("F2")).Append(",")
              .Append(entry.BoundsCenter.z.ToString("F2")).Append(")")
              .Append(" boundsSize=(")
              .Append(entry.BoundsSize.x.ToString("F2")).Append(",")
              .Append(entry.BoundsSize.y.ToString("F2")).Append(",")
              .Append(entry.BoundsSize.z.ToString("F2")).Append(")\n");
            sb.Append("  mesh=").Append(entry.MeshName).Append(" renderer=").Append(entry.RendererType).Append("\n");
            sb.Append("  scale=(")
              .Append(entry.LocalScale.x.ToString("F2")).Append(",")
              .Append(entry.LocalScale.y.ToString("F2")).Append(",")
              .Append(entry.LocalScale.z.ToString("F2")).Append(")\n");
            sb.Append("  material(s)=").Append(string.Join(", ", entry.MaterialNames.ToArray())).Append("\n");
            sb.Append("  shader(s)=").Append(string.Join(", ", entry.ShaderNames.ToArray())).Append("\n");
            sb.Append("  colorProperty(s)=").Append(entry.ColorPropertyNames.Count == 0 ? "(none)" : string.Join(", ", entry.ColorPropertyNames.ToArray())).Append("\n");
            sb.Append("  active=").Append(entry.ActiveInHierarchy).Append("\n");
            sb.Append("  components=").Append(string.Join(",", entry.ComponentTypeNames.ToArray()));
            return sb.ToString();
        }

        internal static string FormatRejectionSummary(ForagingScanRejectionSummary rejected)
        {
            if (rejected == null) return "Rejected: (unavailable)";
            return "Rejected: total=" + rejected.TotalRenderers +
                " player=" + rejected.PlayerOwned +
                " modOwned=" + rejected.ModOwned +
                " actorOwned=" + rejected.ActorOwned +
                " otherScene=" + rejected.OtherScene +
                " effects=" + rejected.EffectRenderer +
                " noCloneableMesh=" + rejected.NoCloneableMesh +
                " outsideRadius=" + rejected.OutsideRadius +
                " filterMismatch=" + rejected.FilterMismatch;
        }

        private static int CompareCandidates(RendererScanResult a, RendererScanResult b)
        {
            int rank = a.SortRank.CompareTo(b.SortRank);
            if (rank != 0) return rank;
            int distance = a.Distance.CompareTo(b.Distance);
            if (distance != 0) return distance;
            return string.Compare(a.HierarchyPath, b.HierarchyPath, StringComparison.OrdinalIgnoreCase);
        }


        private static string SafeSceneName(Renderer renderer)
        {
            try
            {
                return renderer != null && renderer.gameObject != null && renderer.gameObject.scene.IsValid()
                    ? renderer.gameObject.scene.name
                    : "(invalid)";
            }
            catch { return "(unavailable)"; }
        }

        private static bool IsActorOwned(Transform transform)
        {
            Transform current = transform;
            int guard = 0;
            while (current != null && guard < 64)
            {
                try
                {
                    if (current.GetComponent<Character>() != null ||
                        current.GetComponent<NPC>() != null ||
                        current.GetComponent<SimPlayer>() != null ||
                        current.GetComponent<PlayerControl>() != null)
                        return true;
                }
                catch { }
                current = current.parent;
                guard++;
            }
            return false;
        }

        private static bool IsSameOrChildOf(Transform candidate, Transform root)
        {
            if (candidate == null || root == null) return false;
            try { return candidate == root || candidate.IsChildOf(root); }
            catch { return false; }
        }

        private static bool IsCraftingExpandedOwned(Transform transform)
        {
            Transform current = transform;
            int guard = 0;
            while (current != null && guard < 64)
            {
                string name = current.name ?? string.Empty;
                if (name.StartsWith("ForageNode_", StringComparison.Ordinal) ||
                    name.StartsWith("ErenshorCraftingExpanded", StringComparison.OrdinalIgnoreCase))
                    return true;
                current = current.parent;
                guard++;
            }
            return false;
        }
    }
}
