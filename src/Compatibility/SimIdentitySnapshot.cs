namespace ErenshorCraftingExpanded
{
    // Plain data, deliberately kept in its own file with no Unity/game dependency so
    // CommissionPolicy (pure logic) and its tests can reference it without pulling in
    // UnityEngine/Assembly-CSharp - see tests/RUN_TESTS.ps1. RuntimeKey is scene-local, not a
    // persistent Sim identity contract.
    public struct SimIdentitySnapshot
    {
        public readonly string RuntimeKey;
        public readonly string Name;
        public readonly int Level;

        public SimIdentitySnapshot(string runtimeKey, string name, int level)
        {
            RuntimeKey = runtimeKey;
            Name = name;
            Level = level;
        }
    }
}
