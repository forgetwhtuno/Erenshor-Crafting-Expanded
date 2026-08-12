using System;
using System.Reflection;

namespace ErenshorCraftingExpanded
{
    // Same reflection-shim pattern as Erenshor-PvP's PvpCompatibility / Erenshor-Nemesis's
    // NemesisDirector: never take a hard reference on the ErenshorCoop assembly, just look
    // its types up by name at runtime so this mod loads fine whether or not COOP is installed.
    internal static class CoopCompatibility
    {
        internal static bool IsCoopSession()
        {
            try
            {
                Type networked = FindType("NetworkedPlayer");
                if (networked == null) return false;
                return UnityEngine.Object.FindObjectsOfType(networked).Length > 0;
            }
            catch { return true; }
        }

        // Fails closed: if identity cannot be established, treat the Sim as remote/human so
        // no commission or crafting-progression code ever mistakenly targets a real person.
        internal static bool IsRemoteHuman(SimPlayer sim)
        {
            if (sim == null) return true;
            try
            {
                Type networked = FindType("NetworkedPlayer");
                if (networked != null && sim.GetComponent(networked) != null) return true;
                Type networkedSim = FindType("NetworkedSim");
                if (networkedSim != null && sim.GetComponent(networkedSim) != null) return true;
            }
            catch { return true; }
            return false;
        }

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                try { Type type = assembly.GetType(name, false); if (type != null) return type; } catch { }
            return null;
        }
    }
}
