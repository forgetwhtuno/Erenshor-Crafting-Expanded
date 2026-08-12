# Runtime Regression Checklist — Erenshor Crafting Expanded

This is the human/local-agent checklist for the reviewed source package.

Previously **VERIFIED LIVE** on the pre-review build:

```text
Wild Herb registration
native inventory grant
stacking
zone leave/return
full game exit/restart + same-character reload
/craftdiag Wild Herb inventory count
```

Those tests should still be rerun as regression checks after compiling this reviewed source. Do not
upgrade any other item below to VERIFIED LIVE unless it is actually exercised in the running game.

Recommended test config:

```text
EnableMod = true
EnableForaging = true
EnablePoCNode = false
EnableCraftingRequests = false
ShowCraftingToggle = true
PersistWindowPosition = true
```

Keep `EnablePoCNode=false` until a real plant source/location has been authored.

---

## A. Build / install / boot

```powershell
.\BUILD.ps1 -BepInExRoot <exact profile root>
.\INSTALL_TEST.ps1 -BepInExRoot <same exact profile root>
```

1. Start Erenshor manually.
2. Reach normal gameplay.
3. Check `BepInEx/LogOutput.log` for plugin/Harmony errors.
4. Run `/craftdiag` and save the output.

STOP on `Collision`, `Unavailable`, failed Harmony patching, or startup exceptions.

---

## B. Wild Herb regression

Run `/craftdiag giveherb` several times.

Verify:

- name/lore/icon remain sane;
- no equipment/click/combat behavior appears;
- copies stack;
- `/craftdiag` inventory count matches the visible stack.

Then zone normally and confirm the stack survives. Finally fully exit Erenshor, restart the process,
load the same character, and confirm the item resolves correctly again.

Bank/vendor/trade/Auction House remain separate, optional tests; do not claim them safe based only
on inventory persistence.

---

## C. Forge stack QoL — important reviewed behavior

Use an ordinary **generic** Smithing recipe, not one of the special combine/blessing/decombine
paths.

1. Put one valid template and one valid fuel source into the forge using normal/native behavior.
2. Keep the required generic material(s) in inventory stacks rather than manually splitting exact
   quantities into forge component slots.
3. Press the native Craft button.
4. Verify the mod moves only the **missing required component units** through native
   `ItemIcon.QuickSmith()` calls, then native `Smithing.Combine()` validates/consumes/creates the
   result.
5. Record exact before/after quantities for template, fuel, every material, and output.
6. Repeat with some required component units already loaded in the forge; only the missing amount
   should move.
7. Put extra/unrelated material in a forge component slot and verify native invalid-recipe behavior
   remains authoritative rather than the mod deleting/rearranging it.

Also exercise at least one special-quality/special-combine path if safely available and verify the
reviewed auto-fill prefix **does not** interfere with it.

Run `/craftdiag` after attempts and note `autoFillMoved=`.

---

## D. Craft hotkey

Configure `CraftHotkey` to an unused key and restart/reload config as required.

Verify:

- outside forge: no craft;
- while typing chat: no craft;
- incomplete/invalid forge: no successful craft;
- valid generic recipe: one press -> one native craft attempt;
- holding/pressing does not create uncontrolled rapid-fire behavior.

The hotkey must reach the same `Smithing.Combine()` path as the Craft button.

---

## E. Smithing XP success boundary

Run `/craftdiag`, note Smithing XP, perform exactly one successful native craft, and run diagnostics
again.

Verify XP changes exactly once and only after success. Attempt an invalid recipe and confirm XP does
not change.

Restart/zone and verify the mod-owned sidecar still loads. Diagnostics currently report
`scope=profile-wide`; this is intentional until a stable per-character player identity is proven.
If `persistenceError=` appears, copy it verbatim.

---

## F. Craftable count

With a normal generic recipe loaded, compare the panel/diagnostic Can Craft value to real available:

- template copies in inventory + forge;
- fuel-source units in inventory + forge;
- repeated ingredient quantities in inventory + forge.

The minimum limiting requirement should win. Special combines should not pretend the generic count
formula applies.

---

## G. Crafting UI / pin semantics

Verify:

- toggle click opens panel;
- unpinned `Open` panel hides when crafting/request context goes away;
- `PinnedOpen` remains visible;
- dragging persists position when configured;
- clicking either the panel **or the small toggle** does not leak a world click or camera movement;
- zoning does not duplicate UI;
- disabling the mod stops feature UI cleanly.

---

## H. Commission PoC (optional, disabled by default)

Only enable `EnableCraftingRequests` if explicitly testing this experimental path.

Current limits are intentional:

- request source is the currently loaded generic recipe;
- no proven native class-usability check yet;
- Sim key is runtime/scene-local, not persistent;
- active offers/accepted requests are invalidated on zoning;
- closing/reopening the forge resets same-recipe offer suppression.

Do not treat this as the final commission system.

---

## I. Foraging scanner v2 — next required live test

The first live scanner build mostly returned local-player renderers. This reviewed source attempts to
fix that.

1. Stand directly beside a plant you like.
2. Run `/craftdiag forage scan`.
3. Confirm the chat top five no longer contain `Player/...` eyebrows/armor/effects.
4. In `LogOutput.log`, copy the rejection summary and the matching candidate block containing:

```text
scene=... path=...
objectPos=
boundsCenter=
boundsSize=
mesh=
material(s)=
shader(s)=
colorProperty(s)=
components=
```

5. If needed, rerun with an observed filter word.
6. Stand where the actual Wild Herb node should spawn and run `/craftdiag forage pos`.
7. Return both outputs to the development agent.

STOP if scanner output still mainly contains player/effect/other-actor objects, or if candidates
come from a Unity scene other than the player's current scene; fix diagnostics before authoring a
source.

---

## J. First real Forage node — blocked until I succeeds

Once a verified plant source and coordinate are authored:

1. enable `EnablePoCNode`;
2. verify one node appears in the exact location;
3. verify source native scale is preserved and the definition scale acts as a multiplier;
4. if tint enabled, confirm only the clone changes color;
5. press `G` in range and verify exactly one Wild Herb reward;
6. verify all renderers of the plant disappear together;
7. verify a second immediate press gives no reward;
8. verify respawn restores all visual parts;
9. make inventory intentionally unable to accept the reward only on a disposable test setup and
   verify a failed reward does not deplete the node unexpectedly;
10. zone away/return and confirm the node resets according to the vanilla-mining-like in-memory
    lifecycle.

---

## K. Reversible removal

```powershell
.\REMOVE_TEST.ps1 -BepInExRoot <same exact profile root>
```

The revised install/remove scripts bind backup sessions to their exact target profile and mark a
session restored after use. Confirm unrelated plugin/config files are untouched.

---

## Report format

For every reached section use:

```text
PASS
FAIL - exact behavior/error
NOT TESTED
```

Always include raw `/craftdiag` output, build/test output, and the Foraging scan log block when
relevant.
