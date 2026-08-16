using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ErenshorCraftingExpanded
{
    [Serializable]
    public sealed class ForagingPersistentState
    {
        public ForagingProgress Progress = new ForagingProgress();
        public ForagingKnowledgeState Knowledge = new ForagingKnowledgeState();
        public List<ForageDepletionSnapshot> Depletions = new List<ForageDepletionSnapshot>();
        public List<ForageDepletionSnapshot> AmbiguousGrants = new List<ForageDepletionSnapshot>();

        public void Normalize()
        {
            if (Progress == null) Progress = new ForagingProgress();
            if (Knowledge == null) Knowledge = new ForagingKnowledgeState();
            if (Depletions == null) Depletions = new List<ForageDepletionSnapshot>();
            if (AmbiguousGrants == null) AmbiguousGrants = new List<ForageDepletionSnapshot>();
            Progress.Normalize();
            Knowledge.Normalize();
            List<ForageDepletionSnapshot> clean = new List<ForageDepletionSnapshot>();
            for (int i = 0; i < Depletions.Count && clean.Count < 64; i++)
            {
                ForageDepletionSnapshot d = Depletions[i];
                if (d == null || string.IsNullOrWhiteSpace(d.Scene) || string.IsNullOrWhiteSpace(d.ItemId)) continue;
                if (float.IsNaN(d.RemainingSeconds) || float.IsInfinity(d.RemainingSeconds) || d.RemainingSeconds <= 0f) continue;
                if (d.RemainingSeconds > 86400f) d.RemainingSeconds = 86400f;
                clean.Add(new ForageDepletionSnapshot(d.Scene.Trim(), d.ItemId.Trim(), d.RemainingSeconds));
            }
            Depletions = clean;
            List<ForageDepletionSnapshot> ambiguous = new List<ForageDepletionSnapshot>();
            for (int i = 0; i < AmbiguousGrants.Count && ambiguous.Count < 16; i++)
            {
                ForageDepletionSnapshot d = AmbiguousGrants[i];
                if (d == null || string.IsNullOrWhiteSpace(d.Scene) || string.IsNullOrWhiteSpace(d.ItemId)) continue;
                if (float.IsNaN(d.RemainingSeconds) || float.IsInfinity(d.RemainingSeconds) || d.RemainingSeconds <= 0f) continue;
                if (d.RemainingSeconds > 86400f) d.RemainingSeconds = 86400f;
                ambiguous.Add(new ForageDepletionSnapshot(d.Scene.Trim(), d.ItemId.Trim(), d.RemainingSeconds));
            }
            AmbiguousGrants = ambiguous;
        }
    }

    public static class ForagingProgressionCodec
    {
        private const string Header = "ERENSHOR_FORAGING_V1";

        public static string Serialize(ForagingPersistentState state)
        {
            state = state ?? new ForagingPersistentState();
            state.Normalize();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Header);
            sb.AppendLine("level=" + state.Progress.Level.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("xp=" + state.Progress.Xp.ToString(CultureInfo.InvariantCulture));
            List<string> discovered = new List<string>(state.Knowledge.DiscoveredResourceKeys);
            discovered.Sort(StringComparer.Ordinal);
            for (int i = 0; i < discovered.Count; i++) sb.AppendLine("discovered=" + discovered[i]);

            List<ForageDepletionSnapshot> depletions = new List<ForageDepletionSnapshot>(state.Depletions);
            depletions.Sort(CompareDepletion);
            for (int i = 0; i < depletions.Count; i++)
            {
                ForageDepletionSnapshot d = depletions[i];
                sb.Append("depletion=");
                sb.Append(Encode(d.Scene)); sb.Append('|');
                sb.Append(Encode(d.ItemId)); sb.Append('|');
                sb.AppendLine(d.RemainingSeconds.ToString("R", CultureInfo.InvariantCulture));
            }
            List<ForageDepletionSnapshot> ambiguous = new List<ForageDepletionSnapshot>(state.AmbiguousGrants);
            ambiguous.Sort(CompareDepletion);
            for (int i = 0; i < ambiguous.Count; i++)
            {
                ForageDepletionSnapshot d = ambiguous[i];
                sb.Append("quarantine=");
                sb.Append(Encode(d.Scene)); sb.Append('|');
                sb.Append(Encode(d.ItemId)); sb.Append('|');
                sb.AppendLine(d.RemainingSeconds.ToString("R", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static ForagingPersistentState Deserialize(string text, out bool valid)
        {
            valid = false;
            ForagingPersistentState state = new ForagingPersistentState();
            if (string.IsNullOrWhiteSpace(text)) return state;
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            if (lines.Length == 0 || !string.Equals(lines[0].Trim(), Header, StringComparison.Ordinal)) return state;
            valid = true;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("level=", StringComparison.Ordinal))
                {
                    int value;
                    if (int.TryParse(line.Substring(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) state.Progress.Level = value;
                }
                else if (line.StartsWith("xp=", StringComparison.Ordinal))
                {
                    int value;
                    if (int.TryParse(line.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) state.Progress.Xp = value;
                }
                else if (line.StartsWith("discovered=", StringComparison.Ordinal))
                {
                    state.Knowledge.DiscoveredResourceKeys.Add(line.Substring(11));
                }
                else if (line.StartsWith("depletion=", StringComparison.Ordinal))
                {
                    string[] parts = line.Substring(10).Split('|');
                    if (parts.Length != 3) continue;
                    string scene;
                    string itemId;
                    float remaining;
                    if (!TryDecode(parts[0], out scene) || !TryDecode(parts[1], out itemId)) continue;
                    if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out remaining)) continue;
                    state.Depletions.Add(new ForageDepletionSnapshot(scene, itemId, remaining));
                }
                else if (line.StartsWith("quarantine=", StringComparison.Ordinal))
                {
                    string[] parts = line.Substring(11).Split('|');
                    if (parts.Length != 3) continue;
                    string scene;
                    string itemId;
                    float remaining;
                    if (!TryDecode(parts[0], out scene) || !TryDecode(parts[1], out itemId)) continue;
                    if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out remaining)) continue;
                    state.AmbiguousGrants.Add(new ForageDepletionSnapshot(scene, itemId, remaining));
                }
            }
            state.Normalize();
            return state;
        }

        private static int CompareDepletion(ForageDepletionSnapshot left, ForageDepletionSnapshot right)
        {
            int scene = string.Compare(left == null ? string.Empty : left.Scene, right == null ? string.Empty : right.Scene, StringComparison.OrdinalIgnoreCase);
            if (scene != 0) return scene;
            int item = string.Compare(left == null ? string.Empty : left.ItemId, right == null ? string.Empty : right.ItemId, StringComparison.Ordinal);
            if (item != 0) return item;
            float a = left == null ? 0f : left.RemainingSeconds;
            float b = right == null ? 0f : right.RemainingSeconds;
            return a.CompareTo(b);
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static bool TryDecode(string value, out string decoded)
        {
            decoded = string.Empty;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
                return true;
            }
            catch { return false; }
        }

        internal static string RunSelfTests()
        {
            ForagingPersistentState state = new ForagingPersistentState();
            state.Progress.Level = 8;
            state.Progress.Xp = 42;
            state.Knowledge.Discover("wild_herb");
            state.Knowledge.Discover("cave_mushroom");
            state.Depletions.Add(new ForageDepletionSnapshot("Hidden Hills", CraftingExpandedItemIds.WildHerbId, 123.5f));
            state.AmbiguousGrants.Add(new ForageDepletionSnapshot("Hidden Hills", CraftingExpandedItemIds.CaveMushroomId, 44f));
            string encoded = Serialize(state);
            bool valid;
            ForagingPersistentState copy = Deserialize(encoded, out valid);
            if (!valid || copy.Progress.Level != 8 || copy.Progress.Xp != 42) return "FAIL progression codec level/xp roundtrip";
            if (!copy.Knowledge.HasDiscovered("wild_herb") || !copy.Knowledge.HasDiscovered("cave_mushroom")) return "FAIL progression codec discovery roundtrip";
            if (copy.Depletions.Count != 1 || Math.Abs(copy.Depletions[0].RemainingSeconds - 123.5f) > 0.01f) return "FAIL progression codec depletion roundtrip";
            if (copy.AmbiguousGrants.Count != 1 || Math.Abs(copy.AmbiguousGrants[0].RemainingSeconds - 44f) > 0.01f) return "FAIL progression codec quarantine roundtrip";
            if (!string.Equals(encoded, Serialize(copy), StringComparison.Ordinal)) return "FAIL progression codec deterministic serialization";

            ForagingPersistentState malformed = Deserialize("ERENSHOR_FORAGING_V1\nlevel=-7\nxp=-9\ndiscovered=Wild Herb\ndiscovered=wild-herb\ndepletion=bad|data|oops\n", out valid);
            if (!valid || malformed.Progress.Level != 1 || malformed.Progress.Xp != 0) return "FAIL malformed progression recovery";
            if (malformed.Knowledge.DiscoveredResourceKeys.Count != 1 || !malformed.Knowledge.HasDiscovered("wild_herb")) return "FAIL malformed discovery recovery";
            ForagingPersistentState wrong = Deserialize("UNKNOWN\nlevel=50\n", out valid);
            if (valid || wrong.Progress.Level != 1) return "FAIL unknown progression format accepted";
            return "PASS foraging progression codec";
        }
    }
}
