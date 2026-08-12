using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Mod-owned sidecar persistence. Never touches Erenshor's own save files. The current v0.1
    // file is profile-wide because a stable local-player character identity has NOT yet been
    // established from current Assembly-CSharp evidence. Do not silently invent a character key;
    // local assembly research must prove one before progression is split per character.
    [Serializable]
    internal sealed class CraftingSaveData
    {
        public CraftingProgress Smithing = new CraftingProgress();
    }

    internal static class CraftingProgressionStore
    {
        private static string _lastError = string.Empty;
        internal static string LastError { get { return _lastError; } private set { _lastError = value; } }

        internal static CraftingSaveData Load(string path)
        {
            LastError = string.Empty;
            try
            {
                if (!File.Exists(path)) return new CraftingSaveData();
                string text = File.ReadAllText(path, Encoding.UTF8);
                CraftingSaveData data = JsonUtility.FromJson<CraftingSaveData>(text);
                if (data == null)
                {
                    LastError = "Progression sidecar parsed as null; defaults loaded.";
                    return new CraftingSaveData();
                }
                return data;
            }
            catch (Exception ex)
            {
                LastError = "Progression load failed: " + ex.GetType().Name + ": " + ex.Message;
                return new CraftingSaveData();
            }
        }

        // Atomic write: write to a temp file then replace, so a crash mid-write never corrupts
        // the previous good save. Returns false and records LastError instead of allowing a
        // sidecar I/O failure to break native gameplay.
        internal static bool Save(string path, CraftingSaveData data)
        {
            LastError = string.Empty;
            string tempPath = path + ".tmp";
            try
            {
                string parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                string text = JsonUtility.ToJson(data ?? new CraftingSaveData(), true);
                File.WriteAllText(tempPath, text, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try { File.Replace(tempPath, path, null); }
                    catch
                    {
                        File.Copy(tempPath, path, true);
                        File.Delete(tempPath);
                    }
                }
                else
                {
                    File.Move(tempPath, path);
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Progression save failed: " + ex.GetType().Name + ": " + ex.Message;
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                return false;
            }
        }
    }
}
