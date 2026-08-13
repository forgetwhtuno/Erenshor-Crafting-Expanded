# Erenshor Crafting Expanded

A horizontal-progression expansion to Erenshor's native crafting: forge quality-of-life, Smithing
progression, a crafting-commission proof-of-concept, and a new Foraging gathering system built on
mod-owned nodes (vanilla Mining and Fishing are untouched). See `docs/` for the full native-API
research this mod is built against, and the top-level implementation plan for scope boundaries.

## Status: Experimental / development (0.2.0)

- **Native Lunaris plugin.** This version has been migrated off BepInEx 5 onto native Lunaris
  (`LunarisPlugin`/`[LunarisPlugin]`/`[LunarisPermission]`, native `Config`/`Logging`). This is a
  loader/config/logging/lifecycle migration only — no gameplay behavior, recipe logic, Foraging
  scope, or command syntax changed. The migration has been statically verified (real compile
  against the installed game + Lunaris assemblies, zero BepInEx references in the compiled
  output, hot-unload event-cleanup audit, full deterministic test suite) but **has not yet been
  live-tested in a running game with Lunaris**. A legacy BepInEx release remains available in this
  repository's Git history/tags for anyone still on BepInEx.
- **Pre-UI-migration baseline:** compiled cleanly and passed its full deterministic test suite against the actual installed
  Erenshor/Lunaris assemblies. The retained-uGUI candidate in this working handoff has **not** been
  recompiled in this execution environment because the handoff omitted the native reference DLLs.
  Gameplay Harmony targets remain `Smithing.Combine`, `Smithing.DoSuccess`, `ItemDatabase.Start`,
  and `TypeText.CheckCommands`; the former UI-only `PlayerControl.LeftClick` / `csMouseOrbit.LateUpdate`
  patches were removed with the IMGUI panel. Native member assumptions include
  (`ItemDatabase.itemDict`/`ItemDB`/`ItemDBList`, `Smithing.Template`/`Components`/`FuelSource`,
  `Item.TemplateIngredients`/`TemplateRewards`, `ItemIcon.QuickSmith`, `PlayerControl.myTransform`,
  `GameData.PlayerTyping`) have been independently re-verified against the currently installed
  game assembly. This is a static/compile-time verification pass, not a live-game one.
- **Partially runtime verified — under the prior BepInEx build, not yet under native Lunaris.**
  Wild Herb custom-item registration, native inventory grant, stacking, zone leave/return, a full
  game exit/restart with the same character, and the `/craftdiag` inventory count were all
  confirmed live by the human tester before this migration. See
  `docs/NATIVE_ITEM_REGISTRY_FINDINGS.md` for that evidence matrix. None of the underlying
  gameplay code changed in the Lunaris migration, but a fresh live pass under Lunaris is still
  pending. Bank/vendor/trade/Auction House behavior remains unverified and is not claimed safe.
- **Smithing stack QoL and the Foraging scanner rewrite still need a live regression pass.** The
  code keeps native `Smithing.Combine()` authoritative, captures the recipe before `DoSuccess()`
  and awards progression only in the postfix after native success, and wires stack QoL to the
  normal Craft/Combine path by repeatedly invoking native `ItemIcon.QuickSmith()` only for the
  exact missing generic recipe ingredients (the three known special-combine template IDs are
  excluded from auto-fill). None of this has been exercised in a running game yet — see
  `docs/FIRST_RUNTIME_TEST.md` for the checklist.
- **Foraging visual placement is still intentionally unauthored.** No real plant asset or fixed
  world coordinate is shipped yet; `EnablePoCNode = false` by default and invalid placeholder
  definitions are rejected. The rewritten scanner (`src/Foraging/ForagingScanPolicy.cs`) now
  excludes the local player hierarchy, other actor-owned renderers, other loaded Unity scenes,
  Crafting Expanded's own clones, effect renderers, and non-cloneable meshes before ranking
  environmental candidates, and source lookup is scene-bounded — but it still needs a live re-scan
  before a real node is authored.
- **Smithing progression is currently profile-wide**, stored in the mod's own
  `smithing-progress.json`. A stable local-player character identity has not yet been proven from
  the current game assembly, so the mod deliberately does not invent a per-character key. This
  is a known pre-release research item.
- **Crafting commissions remain an experimental PoC and are disabled by default.** Their current
  Sim runtime key is scene-local, not a persistent identity; active requests are invalidated on
  zoning. The final commission system still needs a verified recipe catalog and persistent Sim
  identity contract.
- Use `INSTALL_TEST.ps1` for a reversible test install. It records target-bound backup metadata
  so `REMOVE_TEST.ps1` cannot restore a backup belonging to another profile. The one-shot
  `BUILD_AND_INSTALL.ps1` intentionally has no backup and is not the preferred development path.

## Installation

This is a **native Lunaris plugin** — BepInEx is no longer required for this version. Requires
Lunaris installed in your Erenshor install. The compiled DLL is placed directly in
`<Erenshor>\plugins\ErenshorCraftingExpanded.dll`; Lunaris manages enable/disable.

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD.ps1
powershell -ExecutionPolicy Bypass -File .\INSTALL_TEST.ps1
```

`BUILD.ps1` auto-detects your Erenshor install and a Lunaris developer reference folder
(`Lunaris.dll`/`0Harmony.dll`) and compiles to this mod's own `bin\`; it never installs anything.
`INSTALL_TEST.ps1` performs the actual install into `<Erenshor>\plugins\` and records a
target-bound, timestamped backup so `REMOVE_TEST.ps1` can cleanly restore whatever was there
before. `BUILD_AND_INSTALL.ps1` is a one-shot alternative with no backup — prefer the
test/remove pair above during development.

A legacy BepInEx build of this mod remains available in this repository's Git history for anyone
who hasn't moved to Lunaris yet.

## Configuration

Settings are managed by Lunaris's native config system (typed `[Config]` fields, no separate
`.cfg` file to hand-edit).

| Section | Key | Default | What it does |
|---|---|---|---|
| General | EnableMod | true | Master switch. When false, no Harmony behavior beyond native crafting runs. |
| Crafting | CraftHotkey | None (unbound) | Key that performs one Craft action while the forge window is open and chat isn't focused. |
| Commissions | EnableCraftingRequests | false | Experimental PoC: let local Sims offer a single crafting commission. |
| UI | ShowCraftingToggle | true | Show the on-screen Crafting launcher while the Suite Hub bridge is usable. Hub/bridge failure forces the fallback launcher visible regardless. |
| UI | PersistWindowPosition | true | Remember the retained Crafting panel's normalized dragged position between sessions. |
| UI | LauncherX / LauncherY | -1 | Normalized launcher position; invalid/legacy values recover to a safe default. |
| UI | PanelX / PanelY | -1 | Normalized retained-panel position; invalid/legacy values recover to a safe default. |
| Foraging | EnableForaging | true | Enable the Foraging subsystem (registry, diagnostics). Does not by itself spawn a node. |
| Foraging | EnablePoCNode | false | Spawn the authored node once it has real survey-verified position/visual data. Leave off. |
| Foraging | ForageKey | G | Gather an in-range, available Foraging node. |
| Foraging | ForagingInteractionRange | 3.5 | Max distance (world units) to gather a node. Mod-owned value; no native constant found. |
| Foraging.Dev | ForagingScanRadius | 12 | Radius `/craftdiag forage scan` searches. Development diagnostic only. |
| Foraging.Dev | ForagingDebugRespawnSeconds | 0 | If > 0, overrides node respawn time for fast iteration testing. Leave 0 for normal play. |
| Foraging.Dev | AllowDebugPlaceholderVisual | false | Development-only placeholder-sphere fallback. Leave off. |

## Commands

- `/craftdiag` — concise diagnostic summary (mod state, persistence errors, forage survey pointer).
- `/craftdiag giveherb` — development helper to grant a Wild Herb for testing.
- `/craftdiag forage pos` — reports the player's current position/scene for authoring a node location.
- `/craftdiag forage scan [filter]` — scans nearby renderers as forage-node visual-source candidates; see `docs/FORAGING_ASSET_SURVEY.md`.

## Compatibility

- **Required:** native Lunaris, a matching installed Erenshor build (the mod compiles against and
  disassembles the currently installed `Assembly-CSharp.dll` — see `docs/` for the exact findings).
- **Optional integration:** none currently. Adapts UI/input-protection *code patterns* from this
  author's own `Erenshor-PvP` (see Acknowledgements) but has no runtime dependency on it or any
  other mod in the suite.
- Vanilla Mining and Fishing are untouched by design; this mod only adds a new, separate Foraging
  system.

## Known limitations

See the Status section above for the full detail. In short: Bank/vendor/trade/Auction House
interaction with custom items is unverified; Foraging has no authored node location yet; Smithing
progression is profile-wide, not per-character; crafting commissions are an off-by-default PoC.

## Troubleshooting

- If the mod doesn't load: verify Lunaris itself loads, verify `ErenshorCraftingExpanded.dll` is
  under `<Erenshor>\plugins\`, and check the Lunaris log for a Harmony patch-target error — a
  missing target fails closed with a log line rather than silently running partial features.
- Rebuild against the current `Assembly-CSharp.dll` after any Erenshor update; native method/field
  shapes are re-verified each pass but can change with a patch.
- Run `/craftdiag` for a quick state summary, including any progression `persistenceError`.

## Development / build information

This project has been developed with substantial AI-assisted coding, guided through design,
testing, playtesting, and audits against the actual installed game assembly. Bug reports, code
review, corrections, and contributions from experienced Erenshor modders are welcome.

Build/test procedure and architecture boundaries for contributors (human or AI) are documented in
[`AGENTS.md`](AGENTS.md).

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by
the game's developer.

## License

Apache License 2.0 — see [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).

## Acknowledgements

Crafting Expanded builds on research and examples from the Erenshor modding community.

- **Arcanism — cammaron**
  Referenced as an existing, currently-maintained Erenshor implementation of custom item
  creation and `ItemDatabase` registration. No source was copied (no license file is published
  in that repository); the registration technique was independently reimplemented against this
  build's own disassembled `Assembly-CSharp.dll`.

- **Erenshor-PvP — forgetwhtuno**
  This mod's own sibling project (same author, Apache-2.0-licensed, independent checkout in this
  development environment). Its earlier panel/input-protection patterns informed development; the current player-facing Crafting panel is retained Unity uGUI and no longer uses UI-only `PlayerControl.LeftClick` / `csMouseOrbit.LateUpdate` Harmony prefixes. Gameplay Harmony hooks remain limited to the actual crafting/item/command surfaces described above and were adapted for Crafting Expanded's own panel.

- **Erenshor-Party-Tools — forgetwhtuno**
  Same author/license as above. Established the panel offset-anchoring convention `Erenshor-PvP`
  (and in turn this mod) follows, and its `RUN_TESTS.ps1` shape is the template for this mod's
  own pure-logic test runner.

Detailed technical attribution, including exactly which files were inspected and what was
copied versus independently reimplemented, is in [`docs/ATTRIBUTION.md`](docs/ATTRIBUTION.md).

No endorsement by any credited project or author is implied.


## Optional Suite Hub integration

Erenshor Suite Hub is **optional**. When it is installed, this mod can expose its normal player-facing controls there through the versioned public `CraftingControlApi` surface. The mod remains independently usable without Suite Hub and does not compile against Hub types or assume Hub load order.

Crafting keeps its dedicated retained-uGUI Crafting panel and fallback launcher. `/craftdiag` remains a developer/diagnostic surface rather than a normal-player requirement.

Hub can show Smithing/Foraging status, enable or disable the mod-owned features, and open the Crafting panel. Forage scan, asset survey, debug placeholders, and spawn probes remain developer-only.

The shared control/API and fully-in-world UI policy in this handoff are source-validated but **not yet live-tested under Lunaris hot reload**.

### Content/UI migration candidate

The current source replaces the Crafting player panel with retained Unity uGUI, adds a Suite-style fallback launcher plus normalized position persistence/reset, and keeps `/craftdiag` developer-only. Existing `Crafting Expanded` and `Foraging` bool descriptors remain valid, with a new **Show Crafting launcher** descriptor and Open/Close/Reset actions. Recipe, item-registration, persistence, foraging spawn/gathering, clone, and diagnostic gameplay paths were not redesigned. Native compile and live Lunaris UI/reload verification remain required for this candidate.
