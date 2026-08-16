using System.Globalization;
using System.Text;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Handles /craftdiag forage pos and /craftdiag forage scan - the development workflow that
    // lets a human capture real runtime evidence (position, hierarchy path, mesh, shader,
    // components) for an authored Wild Herb node instead of this mod guessing native asset
    // names. See docs/FORAGING_ASSET_SURVEY.md for the full workflow this feeds into.
    internal static class ForagingSurvey
    {
        private const int MaxChatEntries = 5;
        private const int MaxScanResults = 25;

        internal static void ReportPosition()
        {
            Vector3 position;
            float yaw;
            bool havePosition = GameForagingApi.TryGetPlayerPosition(out position);
            bool haveYaw = GameForagingApi.TryGetPlayerYawDegrees(out yaw);

            if (!havePosition)
            {
                ChatLine("Forage position: could not read player transform (not in a live scene?).");
                return;
            }

            string scene = GameForagingApi.SafeSceneName();
            if (!haveYaw)
            {
                ChatLine("Forage position: position resolved, but player yaw could not be read. Move/retry before authoring this node; no RotationY value was guessed.");
                return;
            }

            string yawLiteral = FloatLiteral(yaw);
            string snippet = BuildDefinitionSnippet(scene, position, yawLiteral);

            ChatLine("Forage position: scene=" + scene);
            ChatLine("  position=(" + FloatLiteral(position.x) + ", " + FloatLiteral(position.y) + ", " + FloatLiteral(position.z) + ") yaw=" + yawLiteral);
            ChatLine("Paste-ready ForageNodeDefinition skeleton follows (visual source intentionally blank):");
            string[] snippetLines = snippet.Split(new char[] { '\n' });
            for (int i = 0; i < snippetLines.Length; i++) ChatLine(snippetLines[i]);
            CraftingController.LogInfo("=== /craftdiag forage pos paste-ready definition ===\n" + snippet);
        }

        internal static void ReportScan(string filter)
        {
            Vector3 position;
            if (!GameForagingApi.TryGetPlayerPosition(out position))
            {
                ChatLine("Forage scan: could not read player transform (not in a live scene?).");
                return;
            }

            float radius = ForagingConfig.ScanRadius.Value;
            if (!ForagingRuntimeConfigValidation.IsValidScanRadius(radius))
            {
                ChatLine("Forage scan: configured ForagingScanRadius (" + radius + ") is invalid - must be > 0 and <= 100. Using 12m for this scan.");
                radius = 12f;
            }

            ForagingScanReport report = ForagingAssetScanApi.ScanNearby(position, radius, MaxScanResults, filter);
            string scene = GameForagingApi.SafeSceneName();
            string filterNote = string.IsNullOrEmpty(filter) ? string.Empty : " matching \"" + filter + "\"";

            ChatLine("Forage scan: " + report.Results.Count + " environmental mesh candidate(s)" + filterNote + " within " + radius.ToString("F0") + "m in " + scene + ".");

            CraftingController.LogInfo("=== /craftdiag forage scan: " + report.Results.Count + " environmental result(s), radius=" + radius + "m, scene=" + scene + (string.IsNullOrEmpty(filter) ? string.Empty : ", filter=" + filter) + " ===");
            CraftingController.LogInfo(ForagingAssetScanApi.FormatRejectionSummary(report.Rejected));

            if (report.Results.Count == 0)
            {
                ChatLine("No clone-compatible environmental meshes found. Move closer to the object, remove the filter, or increase ForagingScanRadius.");
                return;
            }

            int shown = System.Math.Min(MaxChatEntries, report.Results.Count);
            ChatLine("Top " + shown + " useful candidate(s), ranked by plant-scale bounds then distance (full details in the Lunaris log):");
            for (int i = 0; i < report.Results.Count; i++)
            {
                RendererScanResult entry = report.Results[i];
                CraftingController.LogInfo(ForagingAssetScanApi.FormatEntry(i + 1, entry));
                if (i < shown)
                {
                    ChatLine("  #" + (i + 1) + " dist=" + entry.Distance.ToString("F1") + "m " + entry.HierarchyPath + " (mesh=" + entry.MeshName + ")");
                }
            }

            RendererScanResult closest = report.Results[0];
            string colorProps = closest.ColorPropertyNames.Count == 0
                ? "(none found)"
                : string.Join(", ", closest.ColorPropertyNames.ToArray());
            ChatLine("Top-ranked environmental candidate: mesh=" + closest.MeshName + " shader=" +
                (closest.ShaderNames.Count == 0 ? "(none)" : string.Join(", ", closest.ShaderNames.ToArray())));
            ChatLine("  tint properties: " + colorProps + " (see log for path/material/components)");
        }

        private static string BuildDefinitionSnippet(string scene, Vector3 position, string yawLiteral)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("catalog.TryRegister(new ForageNodeDefinition");
            sb.AppendLine("{");
            sb.AppendLine("    Id = \"REPLACE_ID\",");
            sb.AppendLine("    DisplayName = \"REPLACE_NAME\",");
            sb.AppendLine("    Scene = \"" + EscapeCSharp(scene) + "\",");
            sb.AppendLine("    Position = new ForagePosition(" + FloatLiteral(position.x) + "f, " + FloatLiteral(position.y) + "f, " + FloatLiteral(position.z) + "f),");
            sb.AppendLine("    PositionSet = true,");
            sb.AppendLine("    RotationY = " + yawLiteral + "f,");
            sb.AppendLine("    // Fill both values only from /craftdiag forage scan [filter].");
            sb.AppendLine("    VisualSourceScene = \"\",");
            sb.AppendLine("    VisualSourceHierarchyPath = \"\",");
            sb.AppendLine("    RewardItemId = CraftingExpandedItemIds.WildHerbId,");
            sb.AppendLine("    RewardQuantity = 1,");
            sb.AppendLine("    RespawnSeconds = 300f");
            sb.Append("});");
            return sb.ToString();
        }

        private static string FloatLiteral(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string EscapeCSharp(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void ChatLine(string text)
        {
            try { UpdateSocialLog.LogAdd(text, "yellow"); } catch { }
        }
    }
}
