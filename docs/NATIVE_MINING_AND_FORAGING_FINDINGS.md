# Native Mining Evidence + Foraging Feasibility

Research originally performed by reflection + IL disassembly of the installed
`Assembly-CSharp.dll`. Exact game members below are versioned evidence, not a stable API.
Current source should still be rebuilt and rechecked against the user's installed game after any
Erenshor update.

## 1. Vanilla mining — current-build evidence

`MiningNode : MonoBehaviour` is a scene-authored component. Relevant fields found in the target
build include:

```text
List<Item> Common
List<Item> Rare
List<Item> Legend
Item guarantee
ParticleSystem Sparks
MeshRenderer MyRender
Collider MyCol
Character MyChar
float Respawn
float RespawnTime
```

Relevant methods:

```text
Awake()
Start()
Update()
Item Mine(int power)
```

`MiningNode.Start()` marks the attached `NPC` as a mining node. The object participates in native
`Character`/`Stats`/NPC targeting rather than being a lightweight reusable gathering base.

`PlayerCombat.TryMine(Character)` is the verified mining entry point. It:

1. checks native mining power/tool availability;
2. finds `MiningNode` on the target;
3. calls `MiningNode.Mine(power)`;
4. on an item reward, uses the normal inventory path (`AddItemToInv`, then the game's forced
   inventory fallback when required).

Foraging deliberately does **not** reuse the mining-power/tool gate.

### Depletion and respawn

On a successful mine, native mining disables the node's renderer and `Character` behaviour,
stores a countdown in the `Respawn` field, and later re-enables the same scene object from
`Update()`. No save/load persistence for this countdown was found. Scene reload naturally
reconstructs the authored node.

That directly supports the v0.1 Foraging persistence rule:

```text
Available -> Depleted -> timer -> Available
```

with depletion state kept in memory only. Crafting Expanded does not write node state into
Erenshor saves or its own sidecar.

## 2. What Foraging reuses — and what it does not

`MiningNode` is not used as a base class because doing so would drag in `NPC`/`Character`/`Stats`,
mining power, and mining-specific interaction semantics.

Crafting Expanded reuses only the safe design ideas:

- fixed authored world resources;
- native inventory grant semantics;
- visual depletion without destroying the controller;
- in-memory respawn/reset semantics.

It does **not** add:

- Foraging XP/levels;
- gathering stamina;
- gathering gear;
- a pickaxe requirement;
- Mining Power;
- procedural plant scattering;
- automatic movement/harvesting.

Vanilla Mining and Fishing remain untouched.

## 3. Wild Herb custom item — live status

The original feasibility concern about runtime-created items is resolved for the prototype.
`Wild Herb` is registered into the current native item database under:

```text
910000001
```

See `NATIVE_ITEM_REGISTRY_FINDINGS.md` for the full registry evidence and persistence matrix.
The human tester has verified live:

```text
registration
native inventory grant
stacking
zone leave/return
full game exit/restart + same-character reload
/craftdiag inventory count
```

Therefore the PoC Foraging reward is the real registered Wild Herb item, not a placeholder
vanilla reward or a mod-side fake inventory entry.

Bank/vendor/trade/Auction House behavior remains unverified and is not claimed safe.

## 4. Forage node definition and validation

Forage nodes are mod-owned plain-data definitions plus scene-bound runtime controllers. Definitions
contain no persisted Unity references. Current validation rejects:

- unset/non-finite position;
- non-finite rotation;
- missing scene;
- missing visual-source scene/path;
- visual source from a different scene than the node;
- invalid scale (non-finite, <= 0, or unreasonably large);
- invalid tint/color property data;
- invalid respawn interval;
- invalid reward id/quantity;
- duplicate IDs.

The first candidate definition remains intentionally unauthored, so `Catalog.Count == 0` and
`EnablePoCNode` defaults false. No real node is allowed to spawn until the human supplies a
runtime-verified scene, position, and visual source.

## 5. Runtime asset survey

The mod provides read-only development commands:

```text
/craftdiag forage pos
/craftdiag forage scan [filter]
```

The first live scan proved the command worked, but also exposed that naïve distance sorting mostly
returned the local player's eyebrows, armor, and effects. That result is important negative
evidence: the `_Color` property reported by that first build was **not plant-shader evidence**.

The reviewed scanner now:

- excludes the current local player root and all descendants by Transform ancestry;
- excludes Crafting Expanded's own node/visual hierarchy;
- excludes renderers from loaded Unity scenes other than the player's current scene;
- excludes renderer hierarchies owned by `Character`, `NPC`, `SimPlayer`, or `PlayerControl`;
- excludes particle/trail/line effect renderers;
- positively requires the `MeshRenderer + MeshFilter + sharedMesh` shape supported by the safe
  clone path;
- measures distance to renderer bounds center;
- ranks reasonable-size environmental meshes before very large geometry;
- applies optional name/mesh/material/shader filtering only after safety exclusions;
- reports a rejection-count summary;
- reports color shader properties and actual Unity scene name for the surviving environmental candidate.

This scanner revision still requires local compile + live re-test. See `FORAGING_ASSET_SURVEY.md`.

## 6. Safe visual cloning

Normal gameplay never blindly instantiates an entire native scenery GameObject. Source resolution
requires the configured source scene/path and a clone-compatible mesh renderer. The hierarchy path
is walked only inside the player's verified loaded Unity scene; zero matches or duplicate full-path
matches fail closed instead of relying on global `GameObject.Find` selection.

The clone builder copies a bounded visual hierarchy only:

```text
Transform
MeshFilter (shared mesh reference)
MeshRenderer (shared material references + safe renderer presentation fields)
```

It intentionally does not copy arbitrary MonoBehaviours, `NPC`, `Character`, quest scripts,
triggers, navigation, gameplay colliders, or other scene logic.

The clone traversal has a hard transform-count cap and prunes empty branches. Scene-bound source
references are discarded on zoning.

### Scale

`ForageNodeDefinition.Scale` is a **multiplier** over the resolved source object's native world
scale, so `1.3` means roughly 30% larger than the native plant instead of forcing the clone to an
absolute `(1.3,1.3,1.3)` scale.

### Tint

Tint is optional and per node because different candidate shaders can expose different properties.
The actual color property must come from runtime shader-property enumeration; `_Color` is never
assumed.

Tint uses `MaterialPropertyBlock` across all child renderers whose shared materials expose the
configured property. It never mutates native `sharedMaterial` and does not fall back to allocating
new material instances merely to force a color. If no safe supported property exists, the node
keeps its native color.

A debug sphere fallback still exists only behind the explicit development flag
`AllowDebugPlaceholderVisual=false` by default. Normal unresolved visuals do not spawn.

## 7. Interaction and reward semantics

Foraging currently uses a small mod-owned proximity interaction rather than impersonating a
native mining NPC:

- configurable `ForageKey` (default `G`);
- configurable interaction range (default 3.5 world units);
- input ignored while `GameData.PlayerTyping` is true;
- one key press chooses only the nearest available in-range node;
- reward is granted first through the verified item/inventory path;
- depletion happens only after grant succeeds;
- failed reward leaves the plant available, preventing "lost node, no item" failure;
- repeated input while depleted cannot duplicate the reward;
- all renderers in the cloned plant hierarchy are hidden/shown together.

A debug respawn override can shorten the timer for iteration, but only when validated and > 0.

## 8. Remaining live work

Before the first real node can be enabled, the human/local agent must still:

1. rebuild this reviewed source against the installed game;
2. re-run `/craftdiag forage scan` beside a desired plant and confirm player/effect clutter is
   gone;
3. capture the candidate's real path/mesh/material/shader/color properties from `LogOutput.log`;
4. run `/craftdiag forage pos` at the desired authored node location;
5. put those verified values into the Wild Herb definition;
6. enable the PoC node and test spawn -> gather -> deplete -> respawn -> zone-away/return.

Do not author Wild Grain, mushrooms, berries, or Cooking until this first visual/resource loop has
passed live testing.
