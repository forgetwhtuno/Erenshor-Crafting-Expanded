# Attribution

Tracks every external project that materially influenced Crafting Expanded's implementation.
Categories: **TECHNICAL REFERENCE** (behavior/architecture studied, no code copied),
**IMPLEMENTATION PATTERN** (a structural approach was consciously followed),
**DIRECTLY ADAPTED CODE** (source text was copied or closely paraphrased),
**DESIGN INSPIRATION** (concept only), **COMPATIBILITY REFERENCE** (studied to avoid conflicts,
e.g. id-range collisions).

---

## Arcanism — cammaron

- **Category**: TECHNICAL REFERENCE
- **Author/maintainer**: cammaron
- **Repository**: https://github.com/cammaron/Arcanism
- **License**: **Not found.** No `LICENSE` file exists in the repository (confirmed via GitHub's
  license-detection API returning 404, and by listing the repo root directly — no license file
  present), and the README states none. Per the user's instruction, this is treated as **no
  redistribution/reuse permission granted** — nothing from Arcanism is copied into this mod.
  Uncertain licensing is flagged here explicitly rather than assumed permissive.
- **Files/classes inspected**: `Patches/ItemDatabase.cs` (fetched in full via `gh api
  repos/cammaron/Arcanism/contents/...`, 1038 lines, current default branch at research time —
  not from memory of an older version). `ItemGenerator.cs` was referenced by name in the fetched
  file but not itself fetched/read this pass.
- **What was learned**: that `ItemDatabase.Start()` can safely be extended via a Harmony
  `[HarmonyPostfix]`; that the private `itemDict` field needs reflection (`HarmonyLib.Traverse`
  in Arcanism's case) rather than direct access; that new items should be checked against
  `itemDictionary.ContainsKey(id)` before insertion and skipped (never overwritten) on a
  collision, logging the conflicting id and existing item's name; that `ItemDB` must be resized
  and appended to directly (`Array.Resize`); and that `ItemDBList` is a **derived** view rebuilt
  as `new List<Item>(ItemDB)` after any `ItemDB` change, not independent authoritative storage.
- **What Crafting Expanded uses**: the same *shape* of solution — postfix on `ItemDatabase.Start`,
  reflection to `itemDict`, collision-check-before-insert, `Array.Resize` + `ItemDBList` rebuild —
  implemented independently in `src/Compatibility/GameItemRegistryApi.cs` against **this repo's
  own IL disassembly of the currently installed `Assembly-CSharp.dll`** (see
  `docs/NATIVE_ITEM_REGISTRY_FINDINGS.md`), not against Arcanism's field/method names taken on
  faith. Field names (`itemDict`, `ItemDB`, `ItemDBList`, `Id`) were independently re-confirmed by
  disassembling `ItemDatabase.Start()` directly before any registration code was written.
- **Copied, adapted, or independently reimplemented**: **independently reimplemented**. No source
  text from Arcanism appears in this mod. The *technique* (which method to patch, in what order,
  with what collision policy) is the same because it is the only architecturally sound approach
  given how the current `ItemDatabase` actually works — Arcanism is valuable evidence that the
  technique holds up in a real, currently-maintained mod against the current game, which is why it
  is credited as a technical reference rather than left uncredited.
- **ID range note**: Arcanism's own new-item ids use a `90000000`+ block. Crafting Expanded
  deliberately does not use that range (see `docs/NATIVE_ITEM_REGISTRY_FINDINGS.md` section 4) —
  this entry doubles as the compatibility-reference record for that decision.

---

## Erenshor-PvP — forgetwhtuno (this author's own sibling project)

- **Category**: IMPLEMENTATION PATTERN
- **Author/maintainer**: forgetwhtuno (same author/organization as this repository)
- **Repository**: `Erenshor-PvP/`, an independent local checkout alongside this repo (see the
  parent repo's `.gitignore`, which excludes `/Erenshor-PvP/` because it is managed as its own
  project, not as source that lives inside `DeepSim-erenshor`'s own git history).
- **License**: Apache License 2.0 (`Erenshor-PvP/LICENSE`), `NOTICE` states
  "Erenshor PvP, Copyright 2026 forgetwhtuno. This product includes software developed by
  forgetwhtuno." Same rights-holder as this mod — reuse carries no third-party attribution
  obligation in the ordinary sense, but is documented here anyway for an accurate provenance
  record, and Apache 2.0's NOTICE-preservation expectation is honored by this very entry.
- **Correction of an inaccurate premise in this pass's request**: the request asked for a required
  attribution entry for **"RecksPvP / Reckimus"** — a *different*, unrelated public repository
  (`Reckimus/ErenshorPvP` on GitHub). **That repository was never fetched, read, or referenced at
  any point in this project.** The actual source consulted for draggable/toggleable-panel and
  input-passthrough-prevention patterns was this author's own `Erenshor-PvP/src/PvpPanel.cs`,
  `PvpPanelPositioning.cs`, and `ErenshorPvPPlugin.cs` (the `PlayerControl.LeftClick` and
  `csMouseOrbit.LateUpdate` prefixes), read directly from the local checkout during this mod's
  earlier planning pass. Crediting Reckimus for work never inspected would be inaccurate
  attribution, not more thorough attribution, so no `Reckimus/RecksPvP` entry is included here.
  See "Remaining Risks" in the final report for how to correct this if the request intended
  something else.
- **Files/classes inspected**: `src/PvpPanel.cs`, `src/PvpPanelPositioning.cs`,
  `src/ErenshorPvPPlugin.cs`.
- **What was learned**: IMGUI-only panels (no `Canvas`/prefab) drawn from `OnGUI()` avoid any
  duplication-across-scene-reload risk since nothing is scene-owned; manual drag via
  `Event.current`/header-rect hit-testing (rather than `GUI.DragWindow`) plays well with a
  position that's recomputed from a persisted offset every frame; `PlayerControl.LeftClick` and
  `csMouseOrbit.LateUpdate` Harmony prefixes are the two hooks needed to stop a UI click/drag from
  also affecting the world (dropping target, spinning the camera).
- **What Crafting Expanded uses**: the same three techniques, reimplemented as
  `src/UI/CraftingWindow.cs`, `src/UI/CraftingPanelPositioning.cs`, and the
  `CraftingPanelLeftClickPatch`/`CraftingCameraLookPatch` classes in
  `src/ErenshorCraftingExpandedPlugin.cs` — adapted to this mod's own panel content and layout,
  not a byte-for-byte copy, but the drag/positioning math and Harmony-patch structure closely
  follow the `Erenshor-PvP` originals given they solve the identical problem the same way.
- **Copied, adapted, or independently reimplemented**: **adapted** — closer to direct adaptation
  than the Arcanism entry above, since the positioning math (`Resolve`/`Clamp`/`ToOffsets`) and
  the two Harmony prefix bodies are structurally near-identical to the `Erenshor-PvP` source, with
  renamed types/constants for this mod's own namespace and panel dimensions. This is explicitly
  not undersold as "independent reimplementation" — it is adaptation of same-author source.

---

## Erenshor-Party-Tools — forgetwhtuno (same author, indirectly)

- **Category**: DESIGN INSPIRATION
- **Repository**: `Erenshor-Party-Tools/` (same independent-checkout arrangement as `Erenshor-PvP`,
  same `.gitignore` entry, same author).
- **License**: Apache License 2.0 (`Erenshor-Party-Tools/LICENSE`/`NOTICE`, same author).
- **What was learned**: `Erenshor-PvP`'s own panel-positioning code comment states it deliberately
  mirrors `Erenshor-Party-Tools/src/PanelPositioning.cs`'s offset-anchoring convention "so both
  mods anchor to the same upper-right area" — Party Tools established that convention first.
  Crafting Expanded's panel was not fetched/read directly against Party Tools' source; the
  convention arrived secondhand through `Erenshor-PvP`. Also referenced for the repo-wide
  `RUN_TESTS.ps1` csc-based pure-logic test runner convention (`Erenshor-Party-Tools/RUN_TESTS.ps1`
  was read directly and used as the template for `Erenshor-Crafting-Expanded/tests/RUN_TESTS.ps1`).
- **What Crafting Expanded uses**: the left-anchored (rather than right-anchored, to avoid
  overlapping PvP/Party Tools) offset convention in `CraftingPanelPositioning.cs`, and the
  `csc`-compiled standalone test-runner shape in `tests/RUN_TESTS.ps1`.
- **Copied, adapted, or independently reimplemented**: `RUN_TESTS.ps1` structure adapted directly
  (same author, same license); panel-positioning convention independently reimplemented from the
  description, not fetched from Party Tools source itself.

---

## Other repositories surveyed but not materially used

- **`xJeris/ErenshorDoom`** — checked via `gh search code "ItemDatabase" --owner xJeris` for
  item-registration precedent; no matches, does not touch `ItemDatabase`. No material influence,
  not attributed further.
- **`Brumdail/ErenshorQoL`** — reviewed the public repo listing for forge/command-related work
  that might have informed this mod's forge QoL or `/craftdiag` command design; no code was
  fetched or read, and no evidence was found in this pass that it materially influenced any
  implementation decision here. Not added as an attribution entry per the user's own instruction
  not to add projects "merely because they exist" — if a future pass actually reads its source and
  reuses something, add an entry then.
- **General `gh search repos "Erenshor"` survey** (~28 repos, reviewed by name/description only,
  not fetched) — used solely for the id-range collision-avoidance survey in
  `docs/NATIVE_ITEM_REGISTRY_FINDINGS.md` section 3, which is a COMPATIBILITY REFERENCE use, not
  an implementation-pattern one; no single other repo stood out as relevant beyond Arcanism.

---

## License audit summary

| Project | License found | Redistribution obligation | Action taken |
|---|---|---|---|
| Arcanism (cammaron) | **None found** | Unknown/none granted — treat as all-rights-reserved | No source copied; independently reimplemented against this repo's own IL evidence |
| Erenshor-PvP (forgetwhtuno) | Apache 2.0, same author | NOTICE preservation (satisfied by this file) | Adapted with attribution recorded here |
| Erenshor-Party-Tools (forgetwhtuno) | Apache 2.0, same author | NOTICE preservation (satisfied by this file) | Convention/test-runner adapted with attribution recorded here |

**Uncertain licensing flag**: Arcanism's missing license is the only open item. If this mod is
ever distributed publicly, do not add an Arcanism source excerpt without first getting explicit
permission from cammaron, regardless of how small — current status is "no permission on file."

---

## Similar-mod reference survey (later pass)

A separate, research-only survey specifically to (a) avoid unknowingly duplicating another mod,
(b) credit genuine inspiration, (c) flag compatibility concerns, (d) note technical precedent.
Uses the four-way classification requested for this survey: **USED AS IMPLEMENTATION REFERENCE**,
**USED AS TECHNICAL PRECEDENT**, **DESIGN INSPIRATION**, **SURVEYED BUT NOT USED**. This is a
finer-grained pass over the same ground as the categories above — where they overlap, both are
noted rather than one silently superseding the other.

### `Reckimus/ErenshorPvP`

- **Classification: SURVEYED BUT NOT USED** (explicitly *not* IMPLEMENTATION SOURCE).
- **Repo exists currently**: yes — `https://github.com/Reckimus/ErenshorPvP`, last pushed
  2025-05-02.
- **License**: **none** (`gh api repos/Reckimus/ErenshorPvP` reports `"license": null`; no
  `LICENSE` file in the repo).
- **Contents**: the repository root contains exactly `README.md` and a single compiled binary,
  `Reckss-PvP-Mod.dll`. **No source code is published.** There is nothing to read, study, or
  adapt from — this is a binary-only release repo.
- **Overlap with Crafting Expanded**: none identified, and none is possible to identify further
  without decompiling a third party's unlicensed compiled DLL, which was not done and would not
  be an appropriate way to "study" it regardless.
- **Whether it materially influenced anything**: **no.** This closes out the correction already
  recorded in the "Erenshor-PvP" entry above: the actual UI/input-handling reference used during
  development was this author's own local `Erenshor-PvP/` project (same author, Apache 2.0), not
  `Reckimus/ErenshorPvP`. That entry stands as written; this one exists so the survey explicitly
  covers the repo by name rather than leaving it unaddressed.

### `cammaron/Arcanism` (re-confirmed this pass)

- **Classification: USED AS TECHNICAL PRECEDENT** (matches the "TECHNICAL REFERENCE" entry
  above — same conclusion, cross-referenced here for the survey's own taxonomy).
- Re-verified still current/public at survey time; findings unchanged from the main entry above.

### `Brumdail/ErenshorQoL`

- **Classification: SURVEYED BUT NOT USED.**
- **Repo**: `https://github.com/Brumdail/ErenshorQoL`, "Erenshor Quality of Life Modpack based on
  BepInEx", last pushed 2026-02-27.
- **License**: **MIT** (`LICENSE.txt` present) — permissive, source *could* be adapted with
  attribution if something material were found.
- **What was checked**: fetched `ErenshorQoL/ErenshorQoLMod.cs` directly and searched it (and the
  whole repo) for `Smithing`, `ItemDatabase`, and `Forge` — **zero matches**. The mod is a single
  `BaseUnityPlugin` file plus a `Utilities.cs`; its Harmony patches target `TypeText.CheckCommands`
  (a chat-command dispatcher, same general pattern this repo's other mods already use
  independently) and `AuctionHouseUI.OpenListItem` (an auto-pricing feature). Neither overlaps
  with anything Crafting Expanded does.
- **Conclusion**: no material influence found. Not added to the concise README credits, per the
  instruction not to add projects "merely because they exist" — the MIT license means this could
  change cheaply if a future pass finds a genuine reason to adapt something from it.

### Other crafting/gathering/cooking-content mod search

`gh search repos` for `"Erenshor cooking"`, `"Erenshor gathering OR foraging OR harvest"`, and
`"Erenshor craft"` each returned **zero results** this pass. No other Erenshor mod implementing
overlapping crafting/gathering/cooking content was found to exist publicly at survey time. This is
a real but time-bound result — GitHub's search index and mod visibility can change; it is not a
permanent guarantee of non-duplication.
