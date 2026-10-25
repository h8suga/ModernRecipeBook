# Boss progression and "Before: <boss>" (branch `feature/boss-progression`)

Owner: «возможно реализовать сортировку по боссам... если оружие не доступно потому что ресурс после такого-то босса
только появляется или моб, то не показывать его. я пикаю следующего босса и мне показывает то что я могу скрафтить еще
не убив его... желательно модовых боссов тоже учитывать».

Code: `BookSteps.cs` (the steps), `ModernRecipeBook.Call` (the bosses Calamity logs), `BookProgression.cs` / `BookProgressionSources.cs` / `BookProgressionRules.cs` (the
model), `RecipeBookSystem.Bosses.cs` (the filter, the menu, the card's hint), `BookSpawnGates.ProbeSteps` (mods'
opaque conditions).

## Steps

- **A step is a boss.** Step 0 is the start; step k means "boss k is down". Every item, recipe and mob gets the earliest
  step it can be had at. "Before: X" lists everything below X's step: what you can get with every boss before X down
  and X still alive.
- **Order: Boss Checklist's, without Boss Checklist.** The book never asks Boss Checklist; it reads the data the way
  Boss Checklist does (`ref\BossChecklist\src`):
  - Boss Checklist hardcodes the vanilla bosses with fixed values (`BossTracker`: King Slime 1, Eye of Cthulhu 2 ...
    Moon Lord 18). The book's table (`BookSteps.Table`) carries the same values.
  - Mods push their bosses to Boss Checklist themselves with `Call("LogBoss", hostMod, name, value, downed, npcIds,
    extra)`. Calamity 2.2.4 does it in `WeakReferenceSupport.AddCalamityBosses(Mod bossChecklist, Mod calamity)`, whose
    receiver is a parameter. The book calls that method by reflection with its own Mod as the receiver, and
    `ModernRecipeBook.Call` keeps the "LogBoss" calls it gets meanwhile (and accepts "LogMiniBoss" and "LogEvent",
    which are no steps; anything else, and any call outside that moment, is ignored). Each entry gives the key
    (`"CalamityMod " + name`), Calamity's value, its downed check, NPCs, display name and availability.
    `AddCalamityEvents` and `RegisterCalamityExtraInfo` are not called: events and collectibles are no steps.
  - Vanilla and Calamity entries are sorted by value: 44 steps. Checked against Boss Checklist itself on the same
    world: the same order, names and next boss under four sets of downed flags, and 0 differences over 8,317 items'
    steps, with Boss Checklist enabled and disabled.
  - If Calamity's method cannot be called (renamed, other arguments), the log says so in one line and Calamity's rows
    of the table stand in (Calamity's own values, 2.2.4); checked the same way, 0 differences.
  - Mods other than Calamity only tell Boss Checklist about their bosses, so without it they are no steps; their
    content keeps the stage model's places.
  - Keys are Boss Checklist's ("Terraria Golem", "CalamityMod AstrumAureus"), so a saved "Before" survives.
- **Downed.** Calamity's own check from its registration; vanilla's flag (`NPC.downed*`, `Main.hardMode`,
  `DD2Event.DownedInvasionT3` for Betsy), and in the table's fallback Calamity's `DownedBossSystem._downed*` fields,
  read through compiled getters.
- **Availability.** Calamity registers it: the Hive Mind needs a corruption world and the Perforators a crimson one
  (the table knows the same two). The other evil's boss is dimmed in the menu, skipped by "next boss", and an "either
  of them" requirement takes the one this world has.
- **Stages are buckets.** Below the Wall of Flesh's step is pre-hardmode, from it to Moon Lord's hardmode, from Moon
  Lord's on post-Moon Lord. Everything that read stages (the stage filter, Storage's filter, the Bestiary, the Station
  sort) keeps working.

## Where an item's step comes from

The model is the stage model's graph with steps instead of stages: seeds, floors and routes, least fixed point.

- **Bosses:** their loot and treasure bags, their step. A bag the world's difficulty rules out (Classic) stays
  "Not in this mode" and opens nothing.
- **Enemies:** their step, else what they always drop (as before), and never earlier than their spawn gate
  (`BookSpawnGates`, the modded mobs' probed `SpawnChance`). Vanilla has a curated table
  (`BookProgressionSources.VanillaNpcTerms`, from `NPC.SpawnNPC` and `Main.UpdateTime_SpawnTownNPCs`):
  - post-Plantera Dungeon (Dungeon Spirit, the armored bones, casters, Paladin...) and the Temple's Lihzahrds: Plantera;
  - Solar Eclipse: any mechanical boss; Reaper all three; Mothron, Butcher, Deadly Sphere, Dr. Man Fly, Nailhead and
    Psycho Plantera;
  - Pumpkin and Frost Moon: Plantera (their medallions need Ectoplasm); Martian Madness: Golem; the pillars and their
    enemies: the Lunatic Cultist; the Old One's Army tiers: the Eater of Worlds, a mechanical boss, Golem;
  - town NPCs: the Dryad after the Eye / Eater / Skeletron, the Tavernkeep after the Eater, the Witch Doctor after the
    Queen Bee, the Clothier and the Mechanic after Skeletron, the Steampunker after a mechanical boss, the Cyborg
    after Plantera.
  - Calamity: mini-bosses are no steps but are had once they spawn: the Giant Clam from the start, the Great Sand
    Shark after Plantera (`CalamityNaturalSpawnBossNPC`), Cragmaw Mire, Mauler and Nuclear Terror with their Acid Rain
    tier; Acid Rain enemies by their first tier (Eye of Cthulhu, Aquatic Scourge, Polterghast); the Archmage after
    Cryogen, the Sea King after the Desert Scourge, the Brimstone Witch after Supreme Calamitas; the Bohldohr after
    Plantera (it lives in the Jungle Temple).
- **Conditions** on drops, recipes and shop entries map to the boss they test: vanilla `Conditions.Downed*` and drop
  conditions (`DownedPlantera`, `DownedAllMechBosses`, the moons' wave gates...), Calamity's `CalamityConditions` keys
  (`Drops.DownedLevi` → the Leviathan, "Calamitas Clone or Plantera" → the earlier). The Primordial Wyrm is no step;
  its loot is put after Supreme Calamitas, where players meet it.
- **Mods' opaque conditions** (Calamity's `AddIf(() => NPC.downedGolemBoss, ...)`, `DropHelper` lambdas, mods' recipe
  conditions without a known key) are probed: the progression flags are opened step by step in `BookSpawnGates.Steps`
  order and the first step after which the condition holds is its step. A condition that also fails with everything
  open (a biome, the time, an event) keeps the rarity fallback. All flags are put back. 122 conditions are placed this
  way in a Classic world with Calamity, 174 in Expert with Revengeance.
- **A failed-condition branch** (`LeadingConditionRule` + `OnFailedConditions`, Calamity's `AddFail`) is reported by
  the game under the condition itself; the book reports it under its negation (the Caustic Croaker Staff drops before
  Aquatic Scourge too).
- **Mining.** An ore or other natural block is a route: no earlier than what its tile waits for
  (`BookWorldSources.TileGate`) and than the first pickaxe strong enough (`PickPower`: `ModTile.MinPick`, or
  `Player.GetPickaxeDamage`'s vanilla checks: Chlorophyte 200, Lihzahrd 210, the hardmode ores 100–150, ...). A route,
  so a pickaxe made of the ore cannot open it. Mined blocks are also floored at that pickaxe, because other sources
  only guess (Voidstone, Pyre Mantle). Tile gates:
  - Calamity's Early Hardmode Progression Rework (on by default, read from its server config): Mythril and Orichalcum
    after a mechanical boss, Adamantite and Titanium after two, Hallowed Ore after the third
    (`CalamityGlobalNPC.SpawnMechBossHardmodeOres`); its crate drops of those ores and bars wait the same
    (`DropHelper.MythrilCondition`, `AdamantiteCondition`, `HallowedBarsCondition`).
  - Calamity ores (`CalamityOreBosses`): Aerialite after the Hive Mind or the Perforators, Cryonic after Cryogen,
    Perennial after Plantera, Astral after Astrum Deus (`AstralOre.CanKillTile`; it lands with the hardmode meteor),
    Exodium after Moon Lord, Uelibloom after Providence, Auric after Yharon. Scoria has no gate but needs a 210 pickaxe
    and drops from the Chaotic Puffer after Golem.
  - Life Fruit grows after a mechanical boss.
- **Fishing** (`BookWorldSources` tables): from the start unless the row names a gate ("Hardmode", "boss:..."), the
  catch only comes from the Hallow or the Astral Infection (hardmode), or from the Dungeon (Skeletron); rod-only
  catches wait for the rod.
- **Recipes:** the latest of their ingredients' (each group's earliest member), the station's (the item that places
  it: Calamity's rework puts Mythril Anvil recipes after a mechanical boss) and their conditions'.
- **Curated stage lists** stay floors; they seed an item only when nothing else places it, so real sources set the
  step within the stage (Hallowed Bar: the mechanical bosses, not the Wall of Flesh).
- **Rarity** stays the last resort and places an item at its stage's first step. 1,445 items rely on it (1,126 at the
  start, 279 at the Wall of Flesh, 34 at Moon Lord, 6 elsewhere): furniture, vanity, mods' items without a readable
  source. They show from their stage's start in "Before".
- **Unknown:** anything the model cannot place. 133 items in a Classic world, all ruled out by the difficulty (boss
  bags, relics, expert and master items); 0 in Expert with Revengeance.

## The filter

- **Menu.** The stage dropdown opens one panel: the stages on the left (All, Pre-Hardmode, Hardmode, Post-Moon Lord,
  Unknown stage), "Next boss" under them, and every boss in three columns by the stage it closes (Wall of Flesh in
  Pre-Hardmode, Moon Lord in Hardmode), each with its map head (`NPC.GetBossHeadTextureIndex`, else its first frame).
  Downed bosses carry a tick and are dimmed, the next boss has an accent bar, the chosen one is filled; the hint says
  which of these it is. Clicks only, no hover levels.
- **Label.** "Before: Golem" / «До: Голем» with the boss's head. The dropdown is at least 205 units wide, which keeps
  both languages' tabs where they were; a long name is shrunk to fit, never moving the tabs.
- **Next boss** (Alt+5, or the panel's own row) means the first boss not yet down in this world and follows it as
  bosses fall (checked twice a second while the book is open). A picked boss stays as picked.
- **Applies to** Items, Recipes, the Bestiary (mobs by their step) and Storage (its own filter, as before), and to the
  card's Used in list. Saved per player with the other filters (UI state version 9: stage filter 4 and the boss's
  key; 5 is "Unknown stage").
- **Unknown is never hidden silently.** The footer says "unknown stage: 133" when the stage filter hid such items, and
  "Unknown stage" lists them; their card says "Not in this mode" or "No regular source".

## The card

The stage line names the step: "Pre-Hardmode", "After Plantera", "Hardmode" for the Wall of Flesh, "Post-Moon Lord"
for Moon Lord. Its hint gives the step with its stage and the chain that fixes it, from the item down to the source:

- "Spectre Bar ← Ectoplasm ← drop: Dungeon Spirit"
- "Chlorophyte Claymore ← Chlorophyte Bar ← Chlorophyte Ore ← pickaxe: Drax ← Hallowed Bar ← drop: Retinazer · after
  Skeletron Prime" (Calamity's Hallowed bars wait for all three mechanical bosses)
- "Hellkite ← drop: Renegade Warlock · after Plantera"

## Checks

```
c call p=ModernRecipeBook.BookProgression.StepAudit -Instance 5      # items and recipes per step, rarity, unknown
c call p=ModernRecipeBook.BookProgression.Audit out=x.tsv            # + step and its boss as the last two columns
c call p=ModernRecipeBook.BookProgression.Explain a=CalamityMod/ScoriaBar   # step, chain, seeds, routes (TraceSources)
c call p=ModernRecipeBook.BookSteps.Next                              # the next boss's step
```

Spot checks (Calamity 2.2.4, Classic and Expert+Rev):

| Item | Step | Why |
|---|---|---|
| Hallowed Bar | Skeletron Prime | Calamity's `HallowedBarsCondition` (all three mechs) on the mechs' drops |
| Chlorophyte Ore / Bar | Skeletron Prime | 200 pickaxe: Pickaxe Axe / Drax need the three mechs' souls |
| Spectre Bar, Ectoplasm | Plantera | Dungeon Spirit |
| Beetle Husk | Golem | Golem |
| Scoria Ore / Bar | Golem | Chaotic Puffer `AddIf(downedGolemBoss)`, 210 pickaxe |
| Leviathan Teeth | Leviathan | Anahita's loot |
| Perennial Bar | Plantera | Perennial Ore spawns on Plantera's death |
| Solar Veil | Calamitas Clone | eclipse enemies, but `DropHelper.PostCal()` (CalamityGlobalNPC 6119–6126) |
| Cryonic Bar | The Destroyer | Cryogen, and a 180 pickaxe: Adamantite after two mechs under the rework |
| Astral Bar | Astrum Deus | `AstralOre.CanKillTile`, Astral Slime `AddIf` |
| Uelibloom / Auric Bar | Providence / Yharon | their ores |

With real downed flags (everything before Golem down), "Before: Golem" lists 6,801 items (the 133 unknown are
counted in the footer). Of the 11 items named "Beetle" the 7 made of Beetle Husk are hidden (armor, wings,
minecart, the husk) and the banners and the Hercules Beetle shown; 24 of the 26 "Chlorophyte" items are shown (Magic
Storage's two Chlorophyte upgrades need later parts). The stage buckets differ from 1.14.0 for 84 of 8,317 items,
each reviewed: fishing tables now count (crates, Golden Carp, the Alchemy Table after Skeletron), Classic rules
out boss bags, the Hallow's and the Astral Infection's catches wait for hardmode, Abyss furniture needs Voidstone's
Void Condenser.

## Cost

- Built once per load with the rest of the book, in the world: 175–181 ms with Calamity (was 59–131 ms), including the
  condition probe. A multiplayer client rebuilds once when its spawn gates arrive.
- `ApplyFilters` (Items, all stages): 10.4 ms and 0.7 KB per call (1.14.0: 11.3 ms, 0.24 KB); "Before:
  Golem" 8.0 ms. Recipes: 3.8 ms. The name sort is most of it. `BookSteps.Next` 0.3 µs; the per-frame check 0.1 µs,
  no allocation.

## Limits

- One order for everyone: "Before: X" assumes every boss before X in Boss Checklist's order is down.
- Only Calamity's bosses beside the vanilla ones are steps (see Order).
- Rarity-placed items (see above) sit at their stage's first step.
- Events and mini-bosses are no steps; their content follows the curated tables.
- Calamity has no Russian boss names in 2.2.4, so Calamity bosses read in English in Russian.
