using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Reads local SimPlayer state for the experimental commission proof-of-concept. Isolated here
    // so CommissionPolicy (pure logic) never has to touch Unity types directly. IMPORTANT: this
    // adapter has not yet established a persistent Sim identity; RuntimeKey is deliberately only
    // a loaded-instance key and must not be carried across zoning.
    internal static class SimIdentityApi
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Local-only: filters out anything CoopCompatibility.IsRemoteHuman flags, per the
        // user's "never treat a remote human as an AI Sim" requirement.
        internal static List<SimIdentitySnapshot> GetEligibleLocalSims()
        {
            List<SimIdentitySnapshot> result = new List<SimIdentitySnapshot>();
            try
            {
                SimPlayerMngr mngr = GameData.SimMngr;
                if (mngr == null || mngr.ActiveSimInstances == null) return result;
                foreach (SimPlayer sim in mngr.ActiveSimInstances)
                {
                    if (sim == null) continue;
                    if (CoopCompatibility.IsRemoteHuman(sim)) continue;
                    string id = ReadRuntimeKey(sim);
                    if (string.IsNullOrEmpty(id)) continue;
                    result.Add(new SimIdentitySnapshot(id, ReadName(sim), ReadLevel(sim)));
                }
            }
            catch { }
            return result;
        }

        // Scene-local only. GetInstanceID is a Unity runtime identity and is not stable across
        // destroy/recreate or process restart. A future commission system must replace this with
        // a current-build-proven SimPlayerTracking/stable identity before persisting requests.
        private static string ReadRuntimeKey(SimPlayer sim)
        {
            try { return sim.gameObject != null ? sim.gameObject.GetInstanceID().ToString() + ":" + ReadName(sim) : string.Empty; }
            catch { return string.Empty; }
        }

        private static string ReadName(SimPlayer sim)
        {
            foreach (string candidate in new[] { "PlayerName", "MyName", "CharacterName", "CharName", "SimName", "Name" })
            {
                object value = ReadMember(sim, candidate);
                if (value is string && !string.IsNullOrWhiteSpace((string)value)) return ((string)value).Trim();
            }
            try { return sim.gameObject == null ? string.Empty : sim.gameObject.name; } catch { return string.Empty; }
        }

        private static int ReadLevel(SimPlayer sim)
        {
            try
            {
                object stats = ReadMember(sim, "MyStats") ?? ReadMember(sim, "Stats");
                object level = stats == null ? null : ReadMember(stats, "Level");
                if (level is int) return (int)level;
            }
            catch { }
            return 0;
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null) return null;
            try
            {
                Type type = instance.GetType();
                FieldInfo field = type.GetField(name, AllInstance);
                if (field != null) return field.GetValue(instance);
                PropertyInfo property = type.GetProperty(name, AllInstance);
                return property != null && property.CanRead ? property.GetValue(instance, null) : null;
            }
            catch { return null; }
        }
    }
}
