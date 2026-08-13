# Changelog

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
