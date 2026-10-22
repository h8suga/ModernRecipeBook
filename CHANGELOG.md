# Changelog

All notable changes to Modern Recipe Book. Versions follow [Semantic Versioning](https://semver.org/):
patch for fixes, minor for features.

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
