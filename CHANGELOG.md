# Changelog

All notable changes to Modern Recipe Book. Versions follow [Semantic Versioning](https://semver.org/):
patch for fixes, minor for features.

## 1.4.9 — 2026-10-22

### Fixed
- "Unknown stage" is gone from the book. An item whose source exists at another difficulty (a boss bag in
  Classic, Master-only relics, expert-only bag contents) says "Not available at this difficulty"; an item
  nothing yields in normal play says "No regular source". With Calamity in an Expert + Revengeance world
  no item is left without a stage.
- Banners and enemy-only drops of hardmode enemies (Lavabat, Derpling, Corruptor, Armored Bones...) were
  pre-hardmode. Enemies are now staged from the wikis' "Hardmode-only NPCs" / "Post-Moon Lord only NPCs"
  categories (terraria.wiki.gg, calamitymod.wiki.gg) matched to their internal names, plus drop inference
  for the few the wiki misses; a banner follows its own NPC instead of helpers sharing it (Vile Spit).
- Hover hints were drawn under panels drawn later and without a backdrop; they now come last in their
  layer, on a panel in the book's colours.
- The Astral Infection was treated as pre-hardmode; Calamity places it with the hardmode meteor, so its
  blocks, fish, fruit, crates and shrine loot (Starblight Soot, Aureus Cell) are hardmode.
- Recorded music boxes are as late as their track (Yharon's box after Moon Lord, Boss 4 = Golem/Cultist in
  hardmode); Calamity sells the blank box at the Merchant, so biome tracks stay pre-hardmode.
- Strange Plant dyes are hardmode (the plants only grow there) and follow the Dye Trader's gates.
- Gem Crawlers and Scuttler's Jewel are pre-hardmode (cavern spawns); only the Crystal Crawler is hardmode.
- Murasama is post-Moon Lord (it lies in the Hell lab but works only after the Devourer of Gods).

### Added
- More ways to obtain items: catching critters with a bug net, shimmer transmutations, Calamity world
  generation chests (Draedon labs, shrines, Brimstone Crag), Xyk's Blessing turning orange, recording
  music boxes, the Dye Trader's Strange Plant trade.
- Mining gates: a block from a modded tile comes no earlier than the first pickaxe with its MinPick
  (Voidstone and Pyre Mantle 180, Astral Ore 210, Exodium 225).
- Shop conditions that name another NPC or item: "NPC lives here" follows that NPC (Pirate, Wizard),
  "player carries item" waits for that item (Blood Rune needs Ice Barrage), bestiary gates from 70% need
  hardmode creatures. Gray junk counts as available from the start.

### Developer
- `BookProgression.NpcAudit()` lists NPCs with their stage; `BookProgression.Explain(item)` lists every
  source considered for an item (set `BookProgression.TraceSources` first). The audit now carries display
  names for matching against the wikis.

## 1.4.8 — 2026-10-22

### Fixed
- Stage filter misplaced most Calamity items. Non-craftable modded materials had no stage, so everything
  crafted from them was Unknown or wrong: Flying Carpet (Calamity recipe with Pearl Shard) never showed
  under Pre-hardmode. With Calamity 2.2.4 loaded, Unknown Calamity items went from 1690 of 2671 to 23.
- Calamity condition keys (`Mods.CalamityMod.Condition.Drops.Downed*`) were not recognised, so Calamity's
  own early recipes and shop entries counted as uncertain and its post-Moon Lord gates were missed.
- Boss trophies, masks, relics and bags took their stage from rarity (Plantera Trophy was pre-hardmode).
- Zenith-world ("getfixedboi") drops counted as normal loot: Aureus Cell showed as a King Slime drop and
  dozens of post-Moon Lord weapons as Astrum Aureus drops.
- Present contents inherited the Snow Globe's hardmode gate from vanilla's sequential drop report.
- Long hover hints ran off the screen; every hint in the book now wraps to a readable width.
- The footer and the UI error log showed a hard-coded "MRB 1.4.7"; they read the mod's version now.

### Changed
- The stage is the earliest point an item can be obtained, the best of:
  - recipes, through their ingredients, stations and conditions, all the way down the chain;
  - drops of bosses with a known stage: vanilla bosses and events, and every Calamity boss and miniboss
    staged by Calamity's own BossChecklist progression;
  - treasure bag, crate, present and lock box contents;
  - town NPC shops, with the NPC's arrival stage and the entry's conditions;
  - ordinary enemies, staged by what they always drop (a curated material, or two pieces of gear that
    agree), plus their banners.
  Rarity is only the last resort for items with no known source. Calamity's own rarities map to post-Moon
  Lord, except Draedon's DarkOrange, which spans every arsenal tier.
- Difficulty is read from the current world: Classic/Expert/Master/Journey gates and Calamity's
  Revengeance gates decide which drops exist. Items that cannot be obtained in that mode stay visible in
  All only. Special-seed (Zenith, drunk, remix...) content is left out of normal progression.
- Hardmode fishing crates (`IsFishingCrateHardmode`), Pearlwood, Lihzahrd bricks and altar, and Martian
  conduit plating count as hardmode. Draedon lab materials (Dubious Plating, Mysterious Circuitry, power
  cells) count as pre-hardmode. Giant Harpy Feather is no longer forced to hardmode: Harpies drop it from
  the start.
- Stage hints describe the new rules.

### Developer
- `BookProgression.Audit()` lists every item with its stage and the source that set it;
  `BookProgression.DropAudit(npc)` lists an NPC's reported drops with their condition floors.

## 1.4.7

Baseline recovered from the published build (decompiled; comments were lost).
