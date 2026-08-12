namespace ErenshorCraftingExpanded
{
    // Plain data, no UnityEngine dependency so it and ForageNodeCatalog stay unit-testable
    // without the game (see tests/RUN_TESTS.ps1). Mirrors the plan's ForageNodeDefinition shape.
    public struct ForagePosition
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public ForagePosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    // Authored, fixed-location node definition. No Unity object reference is ever stored here.
    // The visual source is a (scene, hierarchy path) locator resolved at runtime via
    // GameForagingApi.TryResolveVisualSource - never a live GameObject/Transform reference, so
    // this stays plain data safe to hold across scene transitions.
    public sealed class ForageNodeDefinition
    {
        public string Id;
        public string DisplayName;
        public string Scene;

        public ForagePosition Position;
        // Explicit flag rather than treating (0,0,0) as "unset" - (0,0,0) could in principle be
        // a real authored coordinate, so placeholder detection must not rely on guessing from
        // the value alone (see ForageNodeCatalog.Validate).
        public bool PositionSet;

        public float RotationY;

        // Scene + Unity hierarchy path (e.g. "Environment/Vegetation/Plant_04/Fern_A") of the
        // native GameObject this node's visual is cloned from. Both must be supplied from actual
        // runtime survey evidence (see docs/FORAGING_ASSET_SURVEY.md) - never guessed.
        public string VisualSourceScene;
        public string VisualSourceHierarchyPath;

        public float Scale = 1.3f;
        public bool TintEnabled;
        public string TintColorProperty;
        public float TintR;
        public float TintG;
        public float TintB;

        public float RespawnSeconds = 300f;
        public string RewardItemId;
        public int RewardQuantity = 1;
    }
}
