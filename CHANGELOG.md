# Changelog

## 0.2.3 - Foraging gather interaction feedback / transaction hardening

- Replaced instant successful gather presentation with an explicit `Available -> Gathering -> GrantPending -> Depleted` transaction. Confirmed left-click starts a configurable 1.25-second channel (runtime-bounded to 1.0-1.5s); the existing red resource bar drains during the channel and returns full on cancellation.
- Added one-global-transaction/token authority, deterministic cancellation for movement, vertical jump/fall, range, occlusion, HP loss, typing, zone/character changes, gameplay disable, unload/runtime cleanup, same-node double-click suppression, and different-node cancel-without-auto-start behavior while unrelated world/UI clicks stay failure-open without cancelling the channel.
- Added Foraging-only strict inventory grants. Exactly one normal `AddItemToInv` overload is invoked, `ForceItemToInv` is never used, verified rejection rolls the node back, and only verified success commits discovery/XP and successful depletion.
- Added a separately persisted character-scoped quarantine for `UnknownAfterInvoke` outcomes so ambiguous native mutations cannot be automatically retried after zoning/restart without being misreported as successful depletion. Pre-invoke failures remain safely retryable.
- Added verified native `Misc.DropItem` success audio and ~0.15-second mod-owned completion scale/fade after authority commits. Presentation/sound failures cannot reopen or regrant a successful node.
- Added an OFF-by-default `StartLoot`/`EndLoot` animation adapter with guaranteed terminal cleanup; no animation event participates in reward authority.
- Normalized Crafting retained drag/resize ownership to left-button pointer-down acquisition, physical-button/focus/pause/disable/destroy/zone/unload release, process-local cross-mod baseline restoration, and a fail-closed runtime-verified monotonic `CameraController.UsingUI()` postfix for Modern camera containment.
- Added `/craftdiag` active gather state/elapsed/remaining/last cancel/grant diagnostics and deterministic/source coverage for state transitions, exactly-once authority, strict rollback/fail-closed outcomes, progress clamping, and camera-drag policies.

## 0.2.2 - Final playable-loop QA / recovery hardening

- Fixed production recipe activation so waiting for a live Smithing forge does not consume the ten-attempt compatibility budget; gameplay/production OFF→ON now receives a fresh bounded activation budget after Templates are neutralized.
- Replaced Foraging auto-placement's `Collider.ClosestPoint` scenery-clearance query with conservative collider-bounds clearance. The mod no longer invokes the warning-prone native closest-point path on arbitrary negative/non-uniform scene colliders.
- Added bounded one-second nameplate-facing diagnostics (camera identity/rect/depth/rotation, canvas rotation/forward, dot product, parent/local/world scale) for live 360° validation without changing the established camera-rotation billboard behavior.
- Added validated, durable `.tmp`/`.bak` recovery for Crafting progression, Foraging progression/depletion, permanent recipe knowledge, and installation-wide native recipe bindings. Newer complete temp state can recover after an interrupted write; malformed/truncated candidates cannot override valid state.
- Repaired the standalone build/install path so it no longer imports a missing project-root build module. Current game/Lunaris references are resolved explicitly, existing installed DLLs are backed up, and install/restore hashes are verified.
- Added final-loop deterministic/source checks for activation retry gating, crash-recovery persistence, exact-click/no-G Foraging, and the removal of `Collider.ClosestPoint` from placement. No crafting/foraging balance constants were changed from the merged progression workstream.

## Unreleased - four-workstream Crafting Expanded reconciliation

- Reconciled native-content, resource-gathering, progression/balance, and player-knowledge UI workstreams against the same authoritative local baseline.
- Added six stable runtime-bound production recipe slots (`910100010`, `011`, `012`, `013`, `015`, `016`) behind OFF-by-default `EnableProductionNativeRecipes`; native donor/output fingerprints and current-session registration must prove before use, and native `Smithing.Combine()` / `DoSuccess()` remain authoritative.
- Expanded the Foraging catalog to five meaningful families: Wild Herb (1), Cave Mushroom (8, experimental), Wild Bloom (14), Cave Moss (24, experimental), and regional Blightroot (36). New families fail closed without exact item/scene visual evidence; yields remain one.
- Retuned per-character Crafting 1-50 progression to the 29,324-XP curve, added bounded per-template practice/mastery, and retained exactly-once success awards with zero XP on failed native crafts.
- Added explicit recipe `MinimumForagingLevel` validation and combined Crafting + Foraging + discovery + physical Template access gates without creating another knowledge/save model.
- Reconciled the retained Crafting knowledge panel with the runtime catalog: it displays all currently enabled resource definitions, region-aware exploration hints, active forge materials, recipe lock reasons, and safe Template recovery.
- Kept the one-template native registration experiment separate from the production catalog and OFF by default. Cave Mushroom/Cave Moss remain separately controlled by the covered-resource experiment.
- Preserved exact-click Foraging, screen-aligned resource nameplates, persistent per-character depletion/progression, conservative placement, and standalone failure-closed behavior.

## 0.2.1 - Crafting knowledge panel

- Reworked the retained Crafting body into a compact player knowledge surface without changing
  crafting or foraging authority.
- Shows character-scoped Crafting and Foraging level/XP separately and avoids fake level data while
  character identity is unresolved.
- Added known-resource rows with `DISCOVERED` / `UNDISCOVERED`, concise Foraging skill gates, and
  an exploration-next hint sourced from the existing resource catalog. Experimental Cave Mushroom
  stays hidden unless its existing covered-resource experiment is enabled.
- Added a `FORGE NOW` section that reuses the controller's existing native availability scan to show
  the currently loaded native template/output, required materials, available/required counts, and the
  first concrete missing requirement. Native special combines remain delegated to native forge rules.
- Improved expanded recipe/template rows with concise `KNOWN` / `LOCKED` states and player-facing
  lock wording such as `Requires Crafting 8` and `Discover Cave Mushroom`.
- Empty expanded-recipe state now explicitly says that no expanded recipes are registered and that
  native Smithing recipes still work; no recipe content was fabricated to fill the panel.
- The outer masked ScrollRect remains the only overflow owner. Added pure layout coverage for compact,
  dense, commission, and small-screen bodies plus boolean state labels.
- Added `CraftingKnowledgePresentationPolicy` deterministic tests for resource state, exploration hints,
  active-recipe availability, missing materials, native special combines, and recipe summaries.

## 2026-08-14 — Live click interaction / 360 nameplate correction

- Replaced the production `ForageKey` gather path with exact left-click interaction. `G` is no longer read by Foraging, avoiding the native Guild-menu conflict; clicks must hit the mod-owned resource target and never fall back to a nearby node.
- Changed forage hover/target acquisition from a camera-center ray to `ScreenPointToRay(Input.mousePosition)`, with an immediate fresh raycast inside the native `PlayerControl.LeftClick` boundary and UI/chat pointer suppression.
- Changed the resource nameplate billboard from point-at-camera-position geometry to screen-aligned gameplay-camera rotation, so the integrated yellow-name/red-bar presentation follows camera orientation through a full orbit instead of behaving like a fixed world sign.
- Added a shared no-per-frame-allocation gameplay-camera resolver used by both click targeting and nameplate orientation, with current camera identity exposed in `/craftdiag` presentation diagnostics.

## 2026-08-14 — Foraging interaction / resource presentation / UI containment correction

- Removed Foraging character identity's dependency on the generic Suite UI readiness gate. The old gate also required Sim/grouping managers unrelated to Foraging, so auto-placement could report available nodes while progression stayed `waiting` and rejected every gather. Foraging identity now waits only for native zoning/character-select transitions and the already-proven local character + save-slot fields.
- Added a mod-owned non-blocking trigger hit target to each forage resource. Native `MiningNode` gameplay is not copied; the current production interaction is exact pointer-click only.
- Added transition/transaction diagnostics for aimed node, eligibility/reason, gather attempt, grant/rollback, XP commit, depletion, and nameplate binding without per-frame log spam.
- Reduced Wild Herb from scenery-sized bush presentation to a compact three-clump resource patch, strongly preferring explicit herb/plant sources over ordinary bush fallbacks.
- Rebuilt the forage nameplate so the yellow resource name sits inside/over the red availability bar, with detached positive scale and one-sided camera-facing geometry to prevent mirrored text.
- Rebuilt the retained Crafting panel into a fixed header/footer plus a single masked scrolling body; recipe/recovery content can overflow by scrolling, while settings, Unpin/Close, and footer text remain inside the dark panel. Added an outer `RectMask2D` as a final containment boundary.
- Extended deterministic policies for interaction eligibility/selection/hit bounds, character-transition readiness, compact cluster/nameplate geometry, and retained panel containment.

## 2026-08-14 — Parallel profession workstreams reconciled

- Combined per-character Crafting progression, Foraging progression/discovery, recipe ownership/recovery, and the native Template verification path.
- Made Foraging the authoritative resource-discovery source for future recipe unlocks.
- Made recipe ownership the authoritative permanent known-recipe/physical-template recovery surface.
- Added safe intra-plugin adapters for character identity and Foraging progression; no sibling mod dependency was introduced.
- Preserved the fail-closed production recipe gate: no guessed production outputs are enabled.
- Preserved the covered-fungus false-positive fix: generic cave ferns are not valid fungus visuals.


## Unreleased - playable-state / release-readiness hardening

- Added per-character Crafting progression/recipe knowledge using the suite's strict slot-qualified character identity pattern; legacy profile-wide Smithing XP is claimable by one character only and permanent KnownRecipe knowledge is independent of physical Template ownership.
- Renamed the player-facing mod profession from Smithing to Crafting while leaving native Erenshor Smithing terminology/authority untouched; tuned the 1-50 curve and added recipe-relative successful-craft XP policy.
- Implemented an OFF-by-default `Crafting.Experimental/ExperimentalNativeRecipeRegistration` path. It reuses the verified ItemDB insertion transaction, scans conservative ordinary native Template donors, clones one owned verification Template (`910100001`), preserves the donor's exact native ingredients/output, adds one Wild Herb, and leaves `Smithing.Combine()` authoritative. Production custom-recipe catalog remains empty pending current live output/lifecycle proof.
- Hardened the native verification donor/registration path: packaged donor and output objects must match their live ItemDB entries, donor/output safety fields and ingredient ids must be readable, the donor must have exactly one reward, and both existing/recently inserted verification Templates must re-prove exact donor ingredients + exactly one Wild Herb + exact donor output before becoming usable. A failed committed post-insert proof stops automatic retries and requires restart rather than attempting unverified global removal.
- Added a fail-closed `Smithing.Combine()` gate for Crafting Expanded's reserved recipe-template range. The verification Template requires current-session registration as well as the config gate; `ForgeStackQolPatch` consults the same gate before QuickSmith autofill. Ordinary native recipe ids remain unaffected.
- Added a one-use `CraftSuccessAwardToken` around the native `DoSuccess()` observer so the same captured success state cannot award mod progression twice; failed success observations consume nothing. The verification Template also cannot satisfy the commission PoC or gain commission bonus XP.
- Added permanent discovery/unlock/template-delivery policy, inventory fallback discovery, a small `CraftingRecipeDiscoveryBridge` for the parallel Foraging workstream, and physical-template restore state that never rolls back learned knowledge on inventory failure.
- Added deterministic policies/tests for character identity, persistence migration, recipe knowledge, discovery gates, recipe catalog validity, 1-50 progression, experimental donor eligibility and template registration idempotence.
- Added `docs/CRAFTING_TEMPLATE_PROGRESSION_DESIGN.md` and root `MERGE_NOTES.md` documenting the evidence-pending 1-50 milestone catalog and all parallel-workstream assumptions.
- Added a bounded session-only forage depletion ledger keyed by scene + resource item. Successful gathers retain their remaining cooldown across zone-out/zone-in even when auto-placement regenerates transient node positions; failed grants record nothing. The ledger clears on plugin unload and remains session-only in this recipe/progression pass; the parallel Foraging workstream owns whether to adopt the newly available strict character scope for persisted depletion.
- Hardened disable/hot-unload behavior: disabling the master gameplay switch now clears any scene-local experimental commission immediately, plugin shutdown resets commission runtime state, and live custom-item ownership is accepted only from the explicit mod clone marker rather than a potentially stale static id cache.
- Deep playable-state pass: added an OFF-by-default `Cave Mushroom` covered-resource family with
  stable id `910000002`, strict fungal native Item visual selection, strict fungal current-scene
  mesh selection, 420-second production respawn, and shared one-pass herb/fungus renderer scanning.
  The feature fails closed independently at the item and world-visual gates and never falls back to
  debug geometry or a plant/rock palette swap.
- Restored the missing current typed Lunaris settings source and added
  `Foraging.Experimental/ExperimentalCoveredResources=false`; Basic Suite settings remain Crafting,
  Foraging, and Show Launcher, while covered resources and commission requests stay Advanced.
- Added a read-only current-runtime native crafting probe at `ItemDatabase.Start` that validates the
  historically documented Smithing recipe shape, ItemDB collection shape, ordinary native template
  count, and bounded ingredient→reward examples without inserting recipes or touching forge slots.
- Added pure custom-recipe definition validation, recipe/template dedupe, horizontal Smithing-level
  unlock policy, registration/removal ledger semantics, and a mutation gate that cannot open from
  field-shape evidence alone. Actual native recipe mutation remains disabled pending fresh installed
  assembly/lifecycle/output proof.
- Hardened cloned forage Items so a visual donor can never accidentally pass through native
  `Template`, `FuelSource`, `TemplateIngredients`, or `TemplateRewards` behavior. Base selection is
  per resource family, so an unavailable fungal template cannot block Wild Herb registration.
- Normal placement now accepts one safe cluster rather than clearing a good node merely because a
  scene could not produce two; the target remains 2–3. Covered classification rejects obvious
  foliage/tree canopy, and cluster spacing is a pure tested 18m policy.
- Upgraded Crafting launcher suppression to the current Suite Hub Aura presence contract
  (`Ready` + `uiAvailable=true`) instead of treating a loaded Hub component as proof that the player
  UI is usable. Failure always restores the standalone launcher.
- Made the retained Crafting panel compact when commissions are disabled (330px; 425px only when the
  experimental commission block is enabled), and standardized player toggle text to `[ON]/[OFF]`.
- Added commission decline/completion/scene-invalidation cooldowns so the opt-in PoC cannot immediately
  re-offer requests after the previous one ends.
- Item registration now retries safely when a late plugin load sees `GameData.ItemDB` before its
  native backing collection is populated, revalidates ownership markers after hot reload, and keeps
  owned item identities registered even when the master gameplay switch is OFF.
- The native crafting probe now also reports a bounded set of actual ordinary Smithing reward-item
  candidates (id/name plus slot/stack/click-effect shape) for live output vetting; it remains read-only.
- Forage source discovery now caches stable positive scene evidence, throttles missing-family rescans,
  and immediately reopens a missing fungal search if the covered-resource experiment is enabled
  later in the same scene.
- Successful forage gathers now survive zone-out/zone-in cooldown bypass through the session ledger;
  master gameplay OFF/ON preserves the ledger, while full plugin shutdown clears it.
- Smithing sidecar load now normalizes malformed profession/level/XP state to bounded Smithing values
  before use instead of accepting negative or impossible progression.
- Experimental commission candidate ordering is explicitly deterministic by scene-local runtime key
  then name, while requests remain local-Sim-only and OFF by default.

- Restored the typed native-Lunaris `CraftingExpandedSettings`/config-entry shim that current
  gameplay/UI source references but that was absent from this local snapshot. Defaults and config
  keys match the documented 0.2.0 Lunaris migration; local compile against the installed Lunaris
  build is still required.
- Hardened normal Wild Herb auto-placement without broadening content: final ground now rejects a
  wider set of raised rock/stump/log surfaces and nearby interactable/resource-style components,
  requires the existing conservative 2-3 cluster envelope, and grounds each mod-owned vegetation
  clone to its cluster plane before the root is placed.
- Cached spawned-node renderers once and now updates visual/label availability presentation only
  when node state changes, removing per-node hierarchy scans from the ordinary per-frame tick.
- Hardened exact authored visual lookup to prefer the logical loaded gameplay scene identified by
  `GameData.SceneName` instead of assuming the persistent local player belongs to the active Unity
  scene.
- Hardened the one-sided forage billboard for camera loss/reacquisition and expanded pure geometry
  coverage for opposite approaches and invalid vectors. Walk-around readability remains a required
  live acceptance test.
- Environment classification now distinguishes open vs covered placement. Wild Herb remains the default active auto-placement resource; the new Cave Mushroom covered-resource family is isolated behind `ExperimentalCoveredResources=false` and requires strict fungus item/world visual evidence before it can spawn.
- `/craftdiag` now reports current normalized retained-UI coordinates, keeps production auto
  placement separate from the legacy PoC switch, and explicitly reports that no Wild-Herb native
  Smithing template is registered.
- Disabled the experimental commission block in the player panel unless the existing
  `EnableCraftingRequests` opt-in is enabled. Commission gameplay remains an off-by-default PoC.
- Added the standard Suite `ui.state` Aura endpoint beside the existing `closePanel` action so the
  shared layer can identify/close the retained panel. No independent Escape/input hook was added.
- Added `UnityEngine`, `UnityEngine.UIModule`, and `UnityEngine.TerrainPhysicsModule` to the IDE
  project references so the project file matches the modules already required by `BUILD.ps1`.
- No Wild-Herb Smithing recipe was added: this handoff does not include the current installed
  `Assembly-CSharp.dll`, so the template-registration/database-rebuild lifecycle cannot be
  re-verified without inventing native behavior.

## Unreleased - content / utility viability

- Foraging presentation polish: replaced camera-rotation copying with a true one-sided world-space
  Canvas/TMP billboard that geometrically faces the active camera and applies the required local
  180-degree Y front-face correction; the label safely retains its last orientation while
  `Camera.main` is temporarily unavailable and recreates nothing per frame.
- Increased Wild Herb resource readability without changing gather/placement authority: larger
  yellow nameplate, thicker/wider full red presentation bar, slightly higher label clearance, and
  pure presentation-state policy that hides the plant/nameplate while depleted and restores both
  on respawn.
- Increased the auto Wild Herb cluster from four to five mildly varied clumps, raised normalized
  target vegetation size from 0.78m to 1.00m, and widened the bounded clump footprint while leaving
  auto-placement, wall/ground probes, slope/obstacle rejection, NavMesh checks, reward, and respawn
  logic unchanged.
- Added pure deterministic policy coverage for front/back/left/right/diagonal/above/below billboard
  facing, available/depleted presentation state, label/bar world-size bounds, and cluster
  normalization/footprint bounds. `/craftdiag` primary-node output now reports billboard and
  label/bar scale evidence without per-frame logging.

- Promoted the successful Wild Herb edge-placement prototype into the first usable auto-placement slice for scenes without curated forage entries: wall/rock hits are context anchors only, final placement probes outward/down to reachable ground, rejects anchor/boulder tops, steep/raised/forbidden surfaces, and accepts fewer nodes rather than forcing ugly placement.
- Replaced normal auto-node debug spheres with a compact native-vegetation multi-clump visual assembled from a safe plant-like `MeshRenderer + MeshFilter` source discovered in the current gameplay scene; no native gameplay components/colliders are cloned and no shared material is mutated. The presentation-polish pass above expands the original four-clump prototype to five clumps.
- Reworked the world resource nameplate to a detached world-space TMP billboard with yellow resource text and a red availability bar inspired by native Mineral Deposit presentation; the later presentation-polish pass above replaces the original camera-rotation assumption with explicit geometric one-sided facing.
- Wild Herb base-item selection now refuses Coral/rock/ore-style fallbacks and chooses the best safe plant/organic-looking live `ItemDB` template with a real inherited `ItemIcon`; registration remains clone-based, collision-safe, and preserves id `910000001`.
- `/craftdiag` now reports Foraging `enabled`, `autoPlacement`, scene, spawned, available, and depleted counts; deeper placement counters remain available in the primary-node diagnostics without per-frame logging.
- Added deterministic pure-policy coverage for final-ground acceptance/surface rejection, runtime forage visual ranking, and organic Wild Herb base-item selection.

- Added a mod-owned world-space TMP forage resource label that follows the node lifetime, hides while depleted, returns on respawn, and is destroyed with scene/unload cleanup.
- `/craftdiag forage pos` now emits round-trip float scene/position/yaw values plus a paste-ready `ForageNodeDefinition` skeleton; visual-source fields remain intentionally blank for `/craftdiag forage scan` evidence.
- Split curated production forage entries into `ForageAuthoredNodes`; the developer `EnablePoCNode` candidate no longer gates real authored production content.
- Added compact normal-player forage availability status to the retained Crafting panel and require authored nodes to have a display name.
- Documented the exact unresolved native Smithing-template registration question instead of inventing a Wild-Herb recipe path.
- `closePanel` remains available through Suite Aura. A later shared-contract pass added the
  standard read-only `ui.state` endpoint without adding any independent Escape handler.

## 0.2.0 - Native Lunaris migration

- Migrated off BepInEx 5 onto native Lunaris: `LunarisPlugin`/`[LunarisPlugin]`/
  `[LunarisPermission(FileAccess | Reflection | Harmony)]` replace `BaseUnityPlugin`/
  `[BepInPlugin]`; `ManualLogSource` replaced by native `Logging`; `Config.Bind`/`ConfigEntry<T>`
  replaced by a typed `CraftingExpandedSettings` class (`[Config]` fields) plus a small
  `CraftingExpandedConfigEntry<T>` compatibility shim so existing `.Value` call sites needed no
  changes. All 14 existing settings (7 Crafting, 7 Foraging) preserved with identical
  section/key/default/description.
- This is a loader/config/logging/lifecycle migration only: no recipe logic, Foraging scope,
  Harmony patch targets, `/craftdiag` command syntax, or config defaults changed. Every Harmony
  patch target was re-verified against the currently installed `Assembly-CSharp.dll`.
- `BUILD.ps1`, `BUILD_AND_INSTALL.ps1`, `INSTALL_TEST.ps1`, and `REMOVE_TEST.ps1` rewritten for
  Lunaris: install target is now `<Erenshor>\plugins\ErenshorCraftingExpanded.dll` (a single file,
  no per-mod subfolder or separate `.cfg` file to back up); reference resolution now looks for a
  Lunaris developer folder (`Lunaris.dll`/`0Harmony.dll`) instead of a BepInEx profile root; all
  r2modman/BepInEx-profile auto-detection removed. `INSTALL_TEST.ps1`/`REMOVE_TEST.ps1` keep the
  same target-bound, timestamped backup/restore session semantics as before.
- Verified: real compile against the installed Erenshor + Lunaris assemblies, zero `BepInEx`
  references in the compiled output, full existing deterministic test suite still passes
  (10/10 pure-logic groups), and a static hot-unload audit (every `SceneManager.sceneLoaded`/
  `sceneUnloaded` subscription has a matching unsubscribe in `OnDestroy`; the only
  `AppDomain.CurrentDomain.GetAssemblies()` usages are fresh per-call scans with no
  `AssemblyLoad` event subscription to leak).
- Not yet done: live in-game verification under Lunaris. The mod has not been loaded, unloaded, or
  exercised in a running game since this migration.

## 0.1.1 - Source review pass

- Foraging scanner rewritten to exclude the local player hierarchy, other actor-owned renderers
  (`Character`/`NPC`/`SimPlayer`/`PlayerControl`), renderers outside the player's current loaded
  Unity scene, the mod's own clones, and particle/trail/line renderers; requires
  `MeshRenderer + MeshFilter + sharedMesh`; ranks small/medium environmental geometry ahead of
  giant world meshes; records rejection counts and per-candidate shader/material evidence.
  New pure-logic helper: `src/Foraging/ForagingScanPolicy.cs`.
- Visual-source resolution for forage nodes is now scene-bounded and fails closed on an
  ambiguous or cross-scene hierarchy path instead of using an unscoped `GameObject.Find`.
- Forage node hardening: single nearest-node gather input, `GameData.PlayerTyping` suppresses
  gather input, reward is granted before depletion, per-node tint via `MaterialPropertyBlock`
  only (never edits `sharedMaterial`), clone recursion capped by total transform count, and
  node/config values reject NaN/Infinity/invalid ranges.
- `ForgeStackQolPatch` now actually wires stack quality-of-life into `Smithing.Combine()`:
  moves only missing generic component units via repeated native `ItemIcon.QuickSmith()` calls,
  refuses to move anything when the forge already holds an unrelated or excess component, and
  excludes the three known special-combine template IDs (`31377423`, `2298018`, `2265228`) from
  auto-fill. Movement is detected from before/after slot state rather than assumed from a bool
  return.
- `CraftSuccessPatch` captures the recipe in a `Smithing.DoSuccess()` prefix and awards
  progression only in the postfix, after native success; `Smithing.Combine()` remains
  authoritative.
- Custom item registration hardened: a runtime ownership marker makes repeated
  registration/hot-reload more idempotent, database insertion is transactional/rollback-capable,
  a failed clone insertion destroys the orphan clone, and multi-quantity grants fail closed
  rather than partially granting and reporting success.
- Progression sidecar I/O now exposes a readable `LastError` instead of silently swallowing
  failures; `/craftdiag` reports it when present.
- Crafting commissions (disabled by default): the same recipe is no longer immediately
  re-offered after decline/completion until the forge reopens; the current Sim identity field is
  explicitly named `RuntimeKey` (scene-local, not persistent) rather than implying a stable ID.
- Partial Harmony `PatchAll` failure now unpatches itself and leaves the affected feature
  fail-closed instead of running with a partial patch set.
- UI: unpinned panel state now hides when its context ends (pin remains meaningful), and pointer
  protection covers the small Crafting toggle in addition to the main panel.
- Build/test scripts hardened: `BUILD.ps1` refuses an ambiguous BepInEx reference root instead of
  guessing; `INSTALL_TEST.ps1` records a target-bound, millisecond-timestamped backup session;
  `REMOVE_TEST.ps1` restores only the backup belonging to the exact selected target and marks a
  restored session so it cannot be replayed; `BUILD_AND_INSTALL.ps1` now delegates compilation to
  `BUILD.ps1`.
- Fixed a build break in this pass: `CraftingProgressionStore.LastError` used a C# 6
  auto-property initializer, which the project's legacy `csc.exe` toolchain (effectively C# 5)
  rejects; replaced with an explicit backing field.

## 0.1.0 - Initial development preview

- Forge quality-of-life groundwork, Smithing progression sidecar, a crafting-commission
  proof-of-concept, and an initial mod-owned Foraging system (vanilla Mining and Fishing
  untouched).
- Wild Herb custom item: registration, native inventory grant, stacking, zone leave/return, and a
  full game exit/restart with the same character — confirmed live by the human tester.
- `/craftdiag` diagnostic command.


## Unreleased - Suite UI/API coherence handoff

- Added optional, versioned `CraftingControlApi` discovery/control surface for Suite Hub without a hard Hub dependency.
- Kept standalone commands and core gameplay authority intact.
- Documented the retained panel/launcher policy and Lunaris live-test requirement.
- Standardized the Crafting panel on a runtime-Rect/header-only drag model, added Reset Position, persisted runtime config mutations, and kept diagnostic/asset-survey features developer-only.
## 0.2.4 - Forgotten Roads launcher/header chrome

- Standardized the standalone retained-uGUI launcher at 154x32 with programmatic grip marks and collection hover/pressed colors.
- Replaced font-dependent collapse triangles with mod-owned Image chevrons while preserving panel behavior.
