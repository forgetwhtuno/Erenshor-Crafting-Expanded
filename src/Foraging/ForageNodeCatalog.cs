using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum ForageDefinitionRejectReason
    {
        None = 0,
        MissingId,
        DuplicateId,
        InvalidScene,
        InvalidRespawn,
        InvalidRewardQuantity,
        MissingRewardItem,
        InvalidScale,
        PositionNotSet,
        InvalidPosition,
        InvalidRotation,
        MissingVisualSource,
        VisualSourceSceneMismatch,
        InvalidTint
    }

    // Pure registry/validation - no Unity calls. Definitions are authorable data, not code, per
    // the plan's "authorable without changing node-controller logic" requirement.
    //
    // Validate() deliberately refuses anything that still looks like placeholder/unauthored
    // data (unset position, missing visual-source locator) - a node definition can only reach
    // "valid" once real runtime survey evidence has been filled in (see
    // docs/FORAGING_ASSET_SURVEY.md). This is the single choke point that keeps an unauthored
    // definition from ever spawning, even if EnablePoCNode is turned on.
    public sealed class ForageNodeCatalog
    {
        private readonly Dictionary<string, ForageNodeDefinition> _byId = new Dictionary<string, ForageNodeDefinition>();

        public static ForageDefinitionRejectReason Validate(ForageNodeDefinition definition, ForageNodeCatalog existing)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id)) return ForageDefinitionRejectReason.MissingId;
            if (existing != null && existing._byId.ContainsKey(definition.Id)) return ForageDefinitionRejectReason.DuplicateId;
            if (string.IsNullOrEmpty(definition.Scene)) return ForageDefinitionRejectReason.InvalidScene;
            if (!definition.PositionSet) return ForageDefinitionRejectReason.PositionNotSet;
            if (!IsFinite(definition.Position.X) || !IsFinite(definition.Position.Y) || !IsFinite(definition.Position.Z))
                return ForageDefinitionRejectReason.InvalidPosition;
            if (!IsFinite(definition.RotationY)) return ForageDefinitionRejectReason.InvalidRotation;
            if (string.IsNullOrEmpty(definition.VisualSourceScene) || string.IsNullOrEmpty(definition.VisualSourceHierarchyPath))
                return ForageDefinitionRejectReason.MissingVisualSource;
            if (!string.Equals(definition.Scene, definition.VisualSourceScene, System.StringComparison.OrdinalIgnoreCase))
                return ForageDefinitionRejectReason.VisualSourceSceneMismatch;
            if (!IsFinite(definition.Scale) || definition.Scale <= 0f || definition.Scale > 10f)
                return ForageDefinitionRejectReason.InvalidScale;
            if (definition.TintEnabled && (string.IsNullOrEmpty(definition.TintColorProperty) ||
                !IsUnitColor(definition.TintR) || !IsUnitColor(definition.TintG) || !IsUnitColor(definition.TintB)))
                return ForageDefinitionRejectReason.InvalidTint;
            if (!IsFinite(definition.RespawnSeconds) || definition.RespawnSeconds <= 0f) return ForageDefinitionRejectReason.InvalidRespawn;
            if (definition.RewardQuantity <= 0) return ForageDefinitionRejectReason.InvalidRewardQuantity;
            if (string.IsNullOrEmpty(definition.RewardItemId)) return ForageDefinitionRejectReason.MissingRewardItem;
            return ForageDefinitionRejectReason.None;
        }


        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsUnitColor(float value)
        {
            return IsFinite(value) && value >= 0f && value <= 1f;
        }

        public ForageDefinitionRejectReason TryRegister(ForageNodeDefinition definition)
        {
            ForageDefinitionRejectReason reason = Validate(definition, this);
            if (reason != ForageDefinitionRejectReason.None) return reason;
            _byId[definition.Id] = definition;
            return ForageDefinitionRejectReason.None;
        }

        public IEnumerable<ForageNodeDefinition> GetForScene(string scene)
        {
            foreach (KeyValuePair<string, ForageNodeDefinition> entry in _byId)
                if (string.Equals(entry.Value.Scene, scene, System.StringComparison.OrdinalIgnoreCase))
                    yield return entry.Value;
        }

        public ForageNodeDefinition Get(string id)
        {
            ForageNodeDefinition def;
            return _byId.TryGetValue(id, out def) ? def : null;
        }

        public int Count { get { return _byId.Count; } }

        private static ForageNodeDefinition MakeValid(string id)
        {
            return new ForageNodeDefinition
            {
                Id = id,
                Scene = "PortAzure",
                Position = new ForagePosition(1f, 2f, 3f),
                PositionSet = true,
                VisualSourceScene = "PortAzure",
                VisualSourceHierarchyPath = "Environment/Plants/Fern_A",
                Scale = 1.3f,
                RespawnSeconds = 300f,
                RewardItemId = "some-item-id",
                RewardQuantity = 1
            };
        }

        internal static string RunSelfTests()
        {
            ForageNodeCatalog catalog = new ForageNodeCatalog();
            ForageNodeDefinition valid = MakeValid("WildHerb_PortAzure_01");
            if (catalog.TryRegister(valid) != ForageDefinitionRejectReason.None) return "FAIL valid definition rejected";

            ForageNodeDefinition duplicate = MakeValid("WildHerb_PortAzure_01");
            if (catalog.TryRegister(duplicate) != ForageDefinitionRejectReason.DuplicateId) return "FAIL duplicate id accepted";

            ForageNodeDefinition noScene = MakeValid("X1"); noScene.Scene = "";
            if (catalog.TryRegister(noScene) != ForageDefinitionRejectReason.InvalidScene) return "FAIL invalid scene accepted";

            ForageNodeDefinition badRespawn = MakeValid("X2"); badRespawn.RespawnSeconds = 0f;
            if (catalog.TryRegister(badRespawn) != ForageDefinitionRejectReason.InvalidRespawn) return "FAIL invalid respawn accepted";

            ForageNodeDefinition badQty = MakeValid("X3"); badQty.RewardQuantity = 0;
            if (catalog.TryRegister(badQty) != ForageDefinitionRejectReason.InvalidRewardQuantity) return "FAIL invalid reward quantity accepted";

            ForageNodeDefinition badScale = MakeValid("X4"); badScale.Scale = 0f;
            if (catalog.TryRegister(badScale) != ForageDefinitionRejectReason.InvalidScale) return "FAIL invalid (zero) scale accepted";
            ForageNodeDefinition negativeScale = MakeValid("X4b"); negativeScale.Scale = -1f;
            if (catalog.TryRegister(negativeScale) != ForageDefinitionRejectReason.InvalidScale) return "FAIL invalid (negative) scale accepted";

            // The core "placeholder data" cases this pass adds: a definition that still has an
            // unset position or a missing visual-source locator must never register, even though
            // every other field is otherwise well-formed - this is the check that keeps an
            // unauthored PoC node from ever spawning.
            ForageNodeDefinition noPosition = MakeValid("X5"); noPosition.PositionSet = false;
            if (catalog.TryRegister(noPosition) != ForageDefinitionRejectReason.PositionNotSet) return "FAIL unset position accepted";

            ForageNodeDefinition noVisualSource = MakeValid("X6"); noVisualSource.VisualSourceHierarchyPath = "";
            if (catalog.TryRegister(noVisualSource) != ForageDefinitionRejectReason.MissingVisualSource) return "FAIL missing visual source accepted";

            ForageNodeDefinition wrongSourceScene = MakeValid("X7"); wrongSourceScene.VisualSourceScene = "HiddenHills";
            if (catalog.TryRegister(wrongSourceScene) != ForageDefinitionRejectReason.VisualSourceSceneMismatch) return "FAIL cross-scene visual source accepted";

            ForageNodeDefinition invalidPosition = MakeValid("X8"); invalidPosition.Position = new ForagePosition(float.NaN, 2f, 3f);
            if (catalog.TryRegister(invalidPosition) != ForageDefinitionRejectReason.InvalidPosition) return "FAIL non-finite position accepted";

            ForageNodeDefinition hugeScale = MakeValid("X9"); hugeScale.Scale = 1000f;
            if (catalog.TryRegister(hugeScale) != ForageDefinitionRejectReason.InvalidScale) return "FAIL unreasonable scale accepted";

            ForageNodeDefinition badTint = MakeValid("X10"); badTint.TintEnabled = true; badTint.TintColorProperty = "_Color"; badTint.TintR = 1.2f; badTint.TintG = 0.5f; badTint.TintB = 0.5f;
            if (catalog.TryRegister(badTint) != ForageDefinitionRejectReason.InvalidTint) return "FAIL out-of-range tint accepted";
            ForageNodeDefinition missingTintProperty = MakeValid("X11"); missingTintProperty.TintEnabled = true; missingTintProperty.TintR = 0.5f; missingTintProperty.TintG = 0.5f; missingTintProperty.TintB = 0.5f;
            if (catalog.TryRegister(missingTintProperty) != ForageDefinitionRejectReason.InvalidTint) return "FAIL tint without shader property accepted";

            if (catalog.Count != 1) return "FAIL only the valid definition should have registered";

            return "PASS forage node catalog";
        }
    }
}
