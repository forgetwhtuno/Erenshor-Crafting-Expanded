# Custom Item Registration Findings (ItemDatabase)

Same methodology as the other findings docs: reflection + IL disassembly of the installed
`<Erenshor install>\Erenshor_Data\Managed\Assembly-CSharp.dll` via a throwaway
`ildump.exe` scratch tool, plus a revalidation pass against current public mod source (fetched
live via `gh api`/`gh search`, not from memory of older versions). `[CODE]` = disassembled this
pass. `[PUBLIC MOD, CURRENT]` = fetched from the mod's repo at research time, not assumed from
older recollection.

Repo state at research time: branch `main`, HEAD `5f18be1f755a051b2a11dd25d7e452059611ef1b`,
working tree has one untracked directory (`Erenshor-Crafting-Expanded/`, this mod itself).

## 1. `ItemDatabase` — exact current layout `[CODE]`

`ItemDatabase.Start()` (fully disassembled):

```
GameData.ItemDB = this;                                  // static back-reference, set first
this.ItemDB = Resources.LoadAll("Items", typeof(Item)).Cast<Item>().ToArray();
foreach item in ItemDB:
    if (!item.Unique && item.RequiredSlot == General && item.ItemValue > 0
        && item.Id != "46289586" && item.Id != "23431650")
        this.GenericItems.Add(item);
this.itemDict = new Dictionary<string, Item>(this.ItemDB.Length);
foreach item in ItemDB:
    if (!string.IsNullOrEmpty(item.Id)) this.itemDict[item.Id] = item;   // last-write-wins, no collision check
GetComponent<KnowledgeDatabase>().BuildItemSearchDB();
```

Key facts:
- **`ItemDB` (`Item[]`) is authoritative and is the *only* source loaded from disk** — every
  `Item` asset under a `Resources/Items` folder path, discovered via
  `Resources.LoadAll("Items", typeof(Item))`. This confirms items are plain Unity
  `ScriptableObject` assets, not database rows.
- **`itemDict` (`Dictionary<string, Item>`) is derived from `ItemDB`**, built fresh every
  `Start()`, with **no duplicate-key guard** — a later entry with the same `Id` silently
  overwrites an earlier one. This is exactly the gap a Harmony postfix can safely use to add
  entries (see §7), but also exactly why this mod must run its own collision check before
  inserting, since the game itself won't stop us from colliding with an existing id.
- `ItemDatabase.GetItemByID(string id)` (fully disassembled): `itemDict.TryGetValue(id, out item)`,
  and on a miss **returns `GameData.PlayerInv.Empty`** (a sentinel `Item`), never `null`, never an
  exception. This is the single most important fact for missing-mod safety (§ Persistence table).
- `ItemDatabase.ItemDBList` is **not touched anywhere in `Start()`** — grep of the disassembled
  method found no reference. Cross-referencing current Arcanism source (§3) confirms it is treated
  as a **derived view, rebuilt as `new List<Item>(ItemDB)`** by mods that extend the database, not
  as independent authoritative storage. This mod follows the same convention.

## 2. Save/load ID resolution — exact current layout `[CODE]`

- `ItemSaveData { int Quality; string ID; }` — a player inventory slot round-trips as an `Id`
  string (looked up via `GetItemByID` on load — same graceful-fallback-to-`Empty` path) plus an
  int historically observed to double as either stack quantity or a quality-tier marker depending
  on the item (see `NATIVE_CRAFTING_FINDINGS.md` §4's `QuickSmith` quantity-as-quality evidence).
- `BankSaveData { List<string> BankData; List<int> BankCount; int GEc; List<string> TabNames; }`
  — parallel `Id` + count lists, same resolution model, confirming the **shared bank is also
  purely `Id`-string based**, not character-local object references.
- `AHItemSaveData { string itemID; int itemQual; int itemPrice; }` — Auction House entries are
  likewise `Id`-string based.

**All three persistence surfaces (inventory, bank, auction) resolve items exclusively through the
same `Id`-keyed lookup family, and all three degrade to a harmless empty-item sentinel on a miss
rather than corrupting, throwing, or refusing to load.** This is real, verified-from-IL evidence,
not an assumption — it directly answers the plan's Stop Conditions concern about "current item
lookup structure."

## 3. Public mod comparison — revalidated against current source, not memory

**`cammaron/Arcanism`** (fetched `Patches/ItemDatabase.cs`, 1038 lines, current default branch):
- Registration mechanism: `[HarmonyPatch(typeof(ItemDatabase), "Start")]` class
  `ItemDatabase_Start` with a plain synchronous `[HarmonyPostfix] static void Postfix(ItemDatabase
  __instance)` that calls `UpdateItemDatabase(__instance)` then sets a `static bool isFinished =
  true` other patches can poll (`QuestDB` patch coroutine-waits on `IsFinished()` before touching
  quest items that reference custom ids — evidence that **postfixing `Start()` is late enough for
  every other system this mod's own author trusted**, corroborating this pass's choice of hook).
- Item cloning: fetches an existing vanilla `Item` via `GetItemByID`, mutates copies (their own
  `ItemGenerator` helper, not fetched in full this pass) into new items, sets `.Id`, adds to a
  local `List<Item> itemsToAdd`.
- Collision handling — **exactly the required policy**: before insertion, `itemsToAdd.RemoveAll(i
  => itemDictionary.ContainsKey(i.Id))`, logging the conflicting id and the existing item's name
  and **skipping** the new item rather than overwriting. This is the same fail-closed,
  never-overwrite behavior this mod's `CustomItemRegistrationPolicy` implements independently
  (confirmed convention, not copied code).
- Insertion mechanics: `Array.Resize(ref ItemDB, ItemDB.Length + itemsToAdd.Count)`, appends each
  new item, `itemDictionary.Add(item.Id, item)`, then **`ItemDBList = new List<Item>(ItemDB)`** —
  directly confirms §1's "`ItemDBList` is a derived rebuild" conclusion.
- `itemDict` access uses **`HarmonyLib.Traverse`** (`Traverse.Create(__instance).Field<...>
  ("itemDict")`), confirming the field is not safely assumed public — this mod's own
  `GameItemRegistryApi` uses plain reflection to the same effect (no `Traverse` dependency added
  just for this).
- **ID range**: Arcanism's *new* items (skill books awarded by their own content, not reused
  vanilla equipment) use a **sequential `90000000`+ block** (`EXPERT_CONTROL = 90000000`,
  `TWIN_SPELL = 90000002`, … up to `90000026` seen). Its *renamed/re-skinned vanilla equipment*
  entries use scattered 6–8 digit values matching vanilla's own id style (not a clean block) —
  those are edits to existing items, not new-id registrations, and are not directly comparable.
  **Per the user's explicit instruction, this mod does not use `90000000+`.**

**`xJeris/ErenshorDoom`** — checked (`gh search code "ItemDatabase" --owner xJeris` against this
repo): no matches. It does not touch `ItemDatabase`/items at all (it's a Doom-in-Erenshor
minigame), so it contributes no ID-range or registration evidence.

**Other public mods surveyed for ID-range collision risk** (`gh search repos "Erenshor"`,
manually reviewed the list): no other repo surfaced item-registration code in a code search this
pass; only Arcanism does. This is a real but incomplete survey — a future pass should re-check
before shipping wider than the single prototype item, since new mods appear regularly.

## 4. Chosen ID namespace

Vanilla ids observed across this and the crafting research pass top out in the low tens of
millions (`"84620118"` is the highest seen, from Arcanism's *vanilla* base-item references, not
even a Crafting Expanded id). Arcanism claims `90,000,000`–`90,999,999`-ish for its own new items.
To leave clear separation from both, **Crafting Expanded's custom item ids occupy
`910,000,000`–`910,999,999`** (a full 9-digit block, `9` + `1` prefix, non-adjacent to Arcanism's
`9,0000000` block and far above any observed vanilla id). This is documented in code as
`CraftingExpandedItemIds` and enforced at registration time — a definition whose id falls outside
that range fails validation before any native call happens (`CustomItemRegistrationPolicy` /
`CustomItemDefinitionValidator`, both pure/tested). `WildHerbId = "910000001"`.

**Startup collision detection**: regardless of range reservation, `GameItemRegistryApi`
re-verifies at registration time that the exact id is not already present in the live `itemDict`
(same check Arcanism performs) before inserting — never a range assumption alone. On a real
collision (an id inside our own reserved range that somehow already exists, or — defensively —
any id at all that's occupied by something we didn't insert ourselves this session), registration
is refused and the conflicting id + existing item name are logged; the existing item is never
touched or overwritten.

## 5–6. Wild Herb prototype

**Base item selection: chosen at runtime, not hardcoded**, by scanning the live `ItemDB` (once
populated, inside this mod's own `ItemDatabase.Start` postfix) for the first entry satisfying all
of: `Stackable == true`, `RequiredSlot == General`, `Unique == false`, `TeachSpell == null`,
`TeachSkill == null`, `ItemEffectOnClick == null`, `AssignQuestOnRead == null`, `CompleteOnRead ==
null`, `Disposable == false`, `MustBeEquippedToClick == false`, `WornEffect == null`, `Aura ==
null`. This avoids hardcoding a specific vanilla id this pass has no live way to visually confirm
(no running game session — same limitation noted in the Foraging findings doc), while still being
fully deterministic and IL-justified: every field in that predicate is a confirmed real `Item`
field (see `NATIVE_CRAFTING_FINDINGS.md` §2's full `Item` field dump). **If no such item exists in
the live `ItemDB`, registration fails closed** (`CustomItemRegistrationState.Unavailable`) rather
than falling back to a guessed id — consistent with the plan's Stop Conditions.

Cloning mechanism: `UnityEngine.Object.Instantiate(baseItem)` — the standard, safe Unity pattern
for producing an independent `ScriptableObject` copy without mutating the source asset (Item has
no custom `OnEnable`/constructor side effects observed in its member list, just plain data
fields).

Fields explicitly overridden on the clone: `Id` (→ `910000001`), `ItemName` (→ `"Wild Herb"`),
`Lore` (→ `"A useful wild herb gathered while foraging."`), `ItemValue` (→ a small fixed positive
value), `Classes` (→ cleared to an empty list — no class restriction), and, as a **deterministic
vendor/trade/AH safeguard** rather than a guess about what's currently safe to enable:
`PlayerCannotSell = true` and `NoTradeNoDestroy = true` — both real, confirmed `Item` fields the
game itself already uses to gate exactly these interactions, so this reuses native behavior rather
than inventing new blocking logic. Combat/utility fields (`WeaponDmg`, `AC`, `HP`, all six
attributes, `WeaponProcOnHit`, `ItemEffectOnClick`, `TeachSpell`, `TeachSkill`, `Aura`,
`WornEffect`, `WandEffect`, `BowEffect`) are explicitly zeroed/nulled on the clone as defense in
depth even though the base-item predicate above should already exclude anything with them set.

Icon: **inherited unchanged from the cloned base item** (no new art shipped, no `Sprite`
mutation — the clone gets its own `Item.ItemIcon` reference copied by `Instantiate`, which is safe
since `Sprite` objects aren't touched by this mod at all, only referenced).

## Persistence classification

**Update (human tester, live build):** the tester confirmed the following directly in the
running game, character save intact throughout, no manual save-file edits: Wild Herb registered
successfully (`registered=Registered`, `wildHerbId=910000001`, `baseItem=Coral`, no collision),
was granted through the normal inventory path, stacked correctly across repeated grants, survived
leaving and returning to a zone, and **survived a full game exit and restart** with the same
character reloaded and Wild Herb still present and correct. Observed diagnostic output:
```
Custom items:
range=910000000-910999999
wildHerbId=910000001
registered=Registered
baseItem=Coral
inventoryCount=2
```
Rows below are updated accordingly; anything not actually exercised in that session remains
whatever it was before — no result is fabricated for a surface that wasn't tested.

| Surface | Classification | Basis |
|---|---|---|
| Registration (id 910000001, no collision) | **VERIFIED LIVE** | Human tester, `/craftdiag` output above. |
| Inventory grant | **VERIFIED LIVE** | Human tester — `/craftdiag giveherb` granted the item through the normal `AddItemToInv`/`ForceItemToInv` path. |
| Stacking | **VERIFIED LIVE** | Human tester — repeated grants stacked into the base item's native stack behavior. |
| Zoning (leave/return) | **VERIFIED LIVE** | Human tester — item survived a zone transition. |
| Player save | **VERIFIED LIVE** | Implied by the restart/reload result below (a restart round-trip cannot succeed without the save actually persisting the item). |
| Disconnect | NOT TESTED | Not distinguished from the restart sequence in the tester's report. |
| Restart/reload | **VERIFIED LIVE** | Human tester — full game exit, process restart, same character reloaded, Wild Herb present with correct identity/quantity. This is the one that matters most and it passed. |
| Inventory count diagnostic | **VERIFIED LIVE** | `/craftdiag` correctly reported `inventoryCount=2` matching the actual held quantity. |
| Bank | VERIFIED FROM CURRENT IL (mechanism) / NOT TESTED (live) | Still optional/manual per this pass's instructions — `BankSaveData` uses the identical `Id`-string + `GetItemByID` pattern as inventory, same mechanism, not yet live-exercised. Kept in the manual checklist, not blocking. |
| Vendor sell | NOT TESTED — deliberately blocked | `PlayerCannotSell = true` set on the clone; native vendor code was not disassembled this pass to confirm it actually reads that flag before allowing a sale, so this is a defensive lever, not a proven guarantee. |
| Buyback | NOT TESTED | Depends on vendor sell being blocked in the first place. |
| Auction House | NOT TESTED — deliberately discouraged | `AHItemSaveData` uses the same `Id`-string pattern (mechanism plausible), but v0.7's AH rewrite + known duplication-fix history makes this explicitly high-risk per the user's instruction; `NoTradeNoDestroy = true` is set as a defensive lever but AH listing code itself was not disassembled to confirm it's honored there. **Not claimed safe.** |
| Trade | NOT TESTED — local-only assumption held | No COOP-specific item-sync code was found or assumed; per instruction, custom items are treated as local-only unless COOP APIs are explicitly proven otherwise (they were not investigated this pass — out of scope, no COOP evidence needed for a local single-player registration mechanism). |
| Missing-mod load | VERIFIED FROM CURRENT IL | `GetItemByID` returns `PlayerInv.Empty` (not null, not a throw) on any unresolved id — confirmed by direct IL disassembly, not inferred. A removed-mod's stored Wild Herb id would silently become an empty slot on load. Not separately live-tested (would require actually uninstalling the mod with a Wild Herb in inventory), but the mechanism is now doubly credible given the restart/reload result above exercised the same `GetItemByID`-based resolution path successfully. |

## 7. Initialization order — timing requirement

Registration **must** happen via a Harmony **postfix on `ItemDatabase.Start()`**, not before
(the `Resources.LoadAll` + `itemDict` build must have already run, or there's nothing to check
against / append to) and not appreciably later (Arcanism's own `QuestDB` patch coroutine-blocks on
`IsFinished()` before doing anything id-dependent, i.e. the current mod ecosystem already treats
"immediately after `ItemDatabase.Start()` returns" as the correct, safe moment). This mod's
`CraftingExpandedItemRegistrationPatch` follows the identical pattern: `[HarmonyPatch(typeof(
ItemDatabase), "Start")] [HarmonyPostfix]`.

## Stop-condition check

None of the plan's stop conditions were triggered: `ItemDatabase` *can* safely be extended after
`Start()` (proven by a real, currently-maintained mod doing exactly this against the same class),
id resolution does not require modifying save files, and no evidence surfaced of an unsafe
architecture. The prototype proceeds. Anything still uncertain is listed as NOT TESTED above, not
silently assumed safe.

## Reviewed implementation hardening

The current reviewed source preserves the already human-verified Wild Herb registration behavior
while tightening failure handling:

- custom clones receive a runtime-only Unity Object ownership marker (`ErenshorCraftingExpanded::<id>`)
  so repeated registration/hot reload in the same process can distinguish this mod's prior entry
  from a foreign collision without changing the user-facing `ItemName`;
- `ItemDB`, `itemDict`, and `ItemDBList` insertion is staged and rollback-capable rather than
  leaving an obviously partial registry state if a setter/add throws;
- an orphan clone is destroyed if insertion fails;
- multi-quantity grants fail closed when the native quantity overload cannot complete the whole
  request, rather than silently granting one item and returning success. Current Wild Herb
  Foraging rewards remain quantity 1, so this does not change the verified live path.

These hardening changes require a local compile and a Wild Herb grant/stack/zone/restart regression
test before publication. They do not change the reserved id (`910000001`) or save-id format.
