# Foraging Asset Survey Workflow

Crafting Expanded deliberately does not guess native Erenshor vegetation names, meshes, shader
properties, or world coordinates. The first Wild Herb node is authored only from runtime evidence
captured by these read-only commands:

```text
/craftdiag forage scan [optional filter]
/craftdiag forage pos
```

## Why there is no automatic "nearest plant" feature

The first live survey build demonstrated that a naïve renderer scan mostly saw the player's own
character hierarchy (eyebrows, armor, spell effects). That is exactly why the mod will not simply
clone "the nearest renderer". A human chooses a visually appropriate plant; diagnostics provide
repeatable evidence for the exact source and location.

The reviewed scanner now filters before ranking:

- local player root + all descendants excluded by Transform ancestry;
- Crafting Expanded's own node/clone hierarchy excluded;
- renderers from any Unity scene other than the player's current loaded scene excluded;
- renderers under NPC/Character/SimPlayer/PlayerControl actor hierarchies excluded;
- particle/trail/line renderers excluded;
- only `MeshRenderer + MeshFilter + sharedMesh` candidates accepted because that is the currently
  supported safe clone shape;
- distance measured from renderer bounds center;
- reasonable-size environmental meshes ranked before huge world geometry;
- optional text filter applied only after those safety exclusions.

The scanner writes a rejected-count summary to the BepInEx log so it is obvious why candidates did
not appear.

## Step 1 — stand beside a plant you like

Choose something ordinary enough to represent Wild Herb rather than a unique quest prop. Stand
close to it and run:

```text
/craftdiag forage scan
```

Chat shows the top five filtered environmental candidates, ranked by plant-scale bounds then distance. The fuller bounded result list is
written to `BepInEx/LogOutput.log` with data like:

```text
path=
objectPos=
boundsCenter=
boundsSize=
mesh=
renderer=
scale=
material(s)=
shader(s)=
colorProperty(s)=
components=
```

If the list is still crowded, use an observed runtime word as a filter, for example:

```text
/craftdiag forage scan fern
```

The filter matches hierarchy path, GameObject name, mesh, material, and shader names. It never
overrides the player/mod/effect exclusions.

## Step 2 — send the candidate evidence back

For the plant you actually mean, capture its matching block from `LogOutput.log`, especially:

```text
path
mesh
material(s)
shader(s)
colorProperty(s)
components
boundsSize
```

Do not infer a source from the old screenshot where `_Color` belonged to a player renderer. Only a
candidate that survives the environmental filter counts as plant-shader evidence.

## Step 3 — capture the authored spawn point

Stand exactly where you want the gatherable node to appear and run:

```text
/craftdiag forage pos
```

It reports the current scene, world position, and yaw. This is the source of truth for the fixed
node coordinate; the mod does not procedurally scatter plants.

## Step 4 — author the first node

With both pieces of evidence, fill the candidate definition approximately as:

```csharp
Id = "WildHerb_<Scene>_001",
Scene = "<verified scene>",
Position = new ForagePosition(x, y, z),
PositionSet = true,
RotationY = 0f, // optional yaw offset on top of the source visual's native orientation
VisualSourceScene = "<verified Unity scene from the scan>",
VisualSourceHierarchyPath = "<exact runtime path>",
Scale = 1.30f,
RewardItemId = CraftingExpandedItemIds.WildHerbId,
RewardQuantity = 1,
RespawnSeconds = ...
```

`Scale` is a multiplier over the source plant's native scale; `1.30` means roughly 30% larger.
`RotationY` is an optional yaw **offset** applied on top of the source visual's existing world
orientation. Start at `0` for ordinary vegetation and adjust only if the clone needs turning.
`ForageNodeCatalog.Validate()` refuses missing/placeholder data, non-finite coordinates, wrong
source scene, invalid scale/tint, invalid reward, or duplicate IDs. Source lookup is limited to
the player's loaded Unity scene and fails closed if the configured hierarchy path is ambiguous.

## Optional tint

The scan reports **actual shader Color properties** for each surviving candidate. If the selected
plant exposes a suitable property, tint is authored on the node itself:

```csharp
TintEnabled = true,
TintColorProperty = "<exact runtime color property>",
TintR = ...,
TintG = ...,
TintB = ...
```

There is intentionally no global `TintColorProperty` config anymore because different plant
shaders may use different properties. `_Color` is never assumed.

At runtime tint uses `MaterialPropertyBlock` only on the clone. Native `sharedMaterial` is never
modified. If the property is unsupported, the node keeps its original color.

## Step 5 — rebuild and test one vertical slice

After authoring real data:

1. rebuild with `BUILD.ps1`;
2. reinstall with `INSTALL_TEST.ps1`;
3. set `EnablePoCNode = true`;
4. verify the node appears in the expected location;
5. test `G` (or configured `ForageKey`) within `ForagingInteractionRange`;
6. verify exactly one Wild Herb is granted;
7. verify the whole plant visual depletes;
8. verify immediate repeated input does not duplicate the reward;
9. verify it respawns after the configured timer (or validated debug override);
10. zone away and return; the in-memory depletion state is expected to reset like vanilla mining.

## Debug placeholder

`AllowDebugPlaceholderVisual` exists only as an explicit developer escape hatch. It defaults
false. Normal gameplay never silently substitutes a sphere when a real source fails to resolve;
that node simply does not spawn and `/craftdiag` reports the reason.

## Not part of this survey

Do not add Wild Grain, mushrooms, berries, Cooking, Foraging levels, procedural scattering, or
automatic harvesting as part of the first asset survey. Prove one real Wild Herb node first.
