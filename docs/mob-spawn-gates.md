# Mob spawn gates

Many mods gate their mobs inside `ModNPC.SpawnChance`, where no Bestiary tag can show it. Calamity's Plague
Charger: `!NPC.downedGolemBoss → 0`, else `SpawnCondition.HardmodeJungle.Chance * 0.12f`. Its card read
"Spawns: Not listed", so players looked for it in the underground jungle before Golem.

`BookSpawnGates` asks `SpawnChance` itself, the way `BookItemClasses` runs items on a throwaway player.

## The probe

- **Who.** Every `ModNPC` whose type overrides `SpawnChance`, town NPCs excluded. Vanilla NPCs are not asked.
- **Where.** A throwaway `Player` (whoAmI = `Main.maxPlayers`, never `Main.LocalPlayer`) per place in
  `BookSpawnGates.Places`: forest, underground, caverns, jungle, underground jungle, jungle temple, snow,
  underground snow, desert, underground desert, ocean, dungeon, underworld, corruption, crimson, hallow, sky, plus
  Calamity's Sulphurous Sea, Sunken Sea, the four Abyss layers, the Astral Infection (surface, underground, desert)
  and the Brimstone Crags. Vanilla zones are the `Player.Zone*` flags. Mods' biomes are bits of the player's
  `modBiomeFlags` (what `Player.InModBiome` reads, so `CalamityPlayer.ZoneSunkenSea` and the rest follow). Each
  place has its height band and a tile under the spawn point, so `NPCSpawnHelper.DoChecks`, which
  `NPCLoader.ChooseSpawn` runs before asking the mods, gives `SpawnCondition.X.Chance` the values the game would.
- **Variants.** Day and night, rain or not, and in water or not where a place has water. A gated mob reports
  «днём»/«ночью», «в дождь» or «в воде» when only that variant worked.
- **Progression.** `Steps` lists the bosses in Boss Checklist order: vanilla values, Calamity's from
  `WeakReferenceSupport.BossChecklistProgressionValues` (2.2.4). First every flag is set (`Main.hardMode`, every
  `NPC.downed*`, `DD2Event.DownedInvasion*`, Calamity's `DownedBossSystem._downed*`) and every place and variant is
  asked twice: a mob whose answers differ is inconsistent and left out. Then all flags are cleared and opened step by
  step. The first step after which one of the mob's working variants returns more than 0 is its gate. Open with
  nothing downed = no gate. Events and pillars only open in the everything-on state, so a mob only they gate keeps
  its card.
- **Restore.** All flags, `Main.dayTime`, `raining`, `cloudAlpha`, `eclipse`, `bloodMoon`, `pumpkinMoon`,
  `snowMoon` and `invasionType` are saved first and put back in a `finally`. Events are off while it runs. Calamity's
  flags are written through their backing fields: the property setters call `NPC.SetEventFlagCleared`, which would
  also start a Lantern Night.
- **When.** Once per load, the first time the Bestiary is needed (`BookBestiary.EnsureBuilt`), on the main thread
  between two updates. Single player only. The flags are local statics and nothing is sent, but a mod's SpawnChance
  never runs on a multiplayer client in the game itself. A client skips the probe, logs one note and keeps the
  Bestiary tags. A later single-player world runs it.
- **Errors.** A SpawnChance that throws is left out and counted. Any missing piece (tModLoader's spawn helper, a
  progression field) logs one warning and leaves every card as before.

Audit line (client log): `Spawn gate audit (N ms): probed 124 NPCs in 27 places over 47 steps; 33 got a gate, 84
open from the start, 0 errors (0 inconsistent).` Calamity 2.2.4: about 20 ms on the first run (JIT), 9–13 ms after.

## On the card

- **Spawns.** The places, the gate, then the conditions, merged with the Bestiary's own spawn tags without
  duplicates. A time or rain the tags already state is not repeated. The card's value is short: the first biome and
  how many more ("Forest +3"; Underground Jungle counts as Jungle). A mod's biome the Biome line already names is
  left out, so the gate stays in sight: Plague Charger "Jungle · after Golem", Anthozoan Crab "after Aquatic Scourge".
  Hovering shows every place and "From the mod's own spawn code".
- **Gate text.** «после: <boss>» / "after <boss>" with the game's NPC name, «Хардмод»/"Hardmode" for the Wall of
  Flesh step. The world's evil picks Eater of Worlds or Brain of Cthulhu, and The Hive Mind or The Perforators.
- **Stage.** `BookProgression.NpcStage` takes the later of the curated stage and the gate's stage. The stage
  filter and the Stage sort read the same function. BookStage has no step finer than Hardmode, so the gate text
  carries the detail. For Calamity 2.2.4 the curated tables already agree with all 33 gates.
- Acid Rain mobs keep their own line (`AcidRainText`).

## Limits

- `GlobalNPC.EditSpawnPool` is not run. Calamity's only removes vanilla mobs in its biomes and adds Acid Rain and
  vanilla ones.
- Tile counts are not modelled. A place is its zone flags, height and one tile.
- `NPC.AnyNPCs(type)` checks read the live world: a Giant Clam already alive reads as "does not spawn" during the
  probe, and the card stays as before.
- Other mods' biomes have no place of their own. Their mobs are probed in the places above and usually stay as before.

## Stand checks

```
c call p=ModernRecipeBook.BookBestiary.EnsureBuilt -Instance 8     # runs the probe
c get  p=ModernRecipeBook.BookSpawnGates.Summary -Instance 8
c call p=ModernRecipeBook.BookSpawnGates.Audit -Instance 8         # type, name, gate, stage, places, day, rain, water
c call p=ModernRecipeBook.BookSpawnGates.Clear -Instance 8         # then EnsureBuilt again to re-probe
```
