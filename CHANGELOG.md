# Changelog

All notable changes to Modern Recipe Book. Versions follow [Semantic Versioning](https://semver.org/):
patch for fixes, minor for features.

## 1.9.4 — 2026-10-23

### Fixed
- The inventory key closes the book too. With the book open it used to do nothing. An open layer (help, a menu,
  typing, How to Get) still takes the press first, and with the inventory closed the key only closes the book
  instead of opening the inventory.
- A boss card opened before a friend joined kept the one-player numbers. In Expert and Master the card now reads
  them again whenever someone joins or leaves, and its tooltip says how many players they are for.

## 1.9.3 — 2026-10-23

### Fixed
- The Demon/Crimson Altar showed as "Tile 26" on recipe cards. Stations that no item places now use the game's own
  name for them, as the crafting list does ("Crimson Altar", Calamity's "Altar of the Accursed").
- A recipe condition another mod left untranslated showed its raw key; Magic Storage's campfire condition now reads
  "Near a campfire (Magic Storage)".
- Tooltips of station, condition and "By hand" rows no longer have an empty line under the name.

## 1.9.2 — 2026-10-23

### Changed
- Storage tab cells work like a vanilla chest slot. A click takes the stack onto the cursor, or adds to the same
  item already on the cursor. Holding right-click takes more and more with the game's own speed-up. Shift+Click
  sends the stack to the inventory, Ctrl+Click to the trash (when quick trash is on), Alt+Click marks a favorite.
  A click with an item on the cursor puts it into storage. In multiplayer the repeats are batched, so holding
  right-click never runs into the server's rate limit.
- The details panel follows the hovered cell instead of a selection click. Ctrl+Enter takes the selected stack
  to the inventory.
- The cell tooltip, the footer and F1 list the take gestures.

## 1.9.1 — 2026-10-23

### Fixed
- Map markers: typing the name works. The game switched keyboard text input off every frame while the full map
  was open, so typed letters never reached the field.
- Map markers: the name is optional. A marker without one shows its badge's name, in the current language.
- Map markers: clicking Cancel or Save no longer drags the map.

## 1.9.0 — 2026-10-23

### Added
- Nearby storage: every unlocked chest and dresser, the Piggy Bank, Safe, Defender's Forge and Void Vault, and
  every Magic Storage network within the radius (10–50 tiles, default 30, set by the host) are one list on the
  Storage tab, with search, filters and sorting. Right-click takes one, Shift+Click takes a stack. Shift+Click an
  inventory item or drop it from the cursor onto the grid to put it away: onto matching stacks first, then into
  the nearest Magic Storage, then into the nearest chest with room. Card counts, Craftable, Max and the tracker
  count all of it; Craft pulls the missing materials into the inventory and crafts at your stations plus those of
  Magic Storage Crafting Interfaces in range. Locked chests and chests another player has open are skipped and
  listed as not counted. Works in multiplayer: the server moves the items and checks every request.
- Map Teleport: Mouse3 (rebindable in Controls) on an explored spot of the full map teleports you there.
  Unexplored spots refuse; inside blocks or next to lava it lands on the nearest safe floor.
- Shared map markers: right-click the full map to place a marker with a name and one of 12 badges. Everyone on
  the server sees it on the full map, the minimap and the overlay, with who placed it and how long ago. Only the
  author or the host can edit or delete it. Saved with the world.
- Server settings, Quality of Life (only the host can change them): Nearby Storage, Nearby Storage Radius, Map
  Teleport, Shared Map Markers.

### Changed
- The mod is now needed on the server as well as on every client; players joining download it. On a server
  without it, the book is off for that session.
- Magic Storage: no connecting or picking a network any more, every network in range counts. The craft source
  chip and the auto-connect option are gone, and crafted items always go to the inventory. Deposit All, Quick
  Stack and Restock work with chests too; the Ctrl+D and Ctrl+Shift+D shortcuts are gone.
- The Storage tab is always shown, since chests count without Magic Storage, so for players without Magic
  Storage Ctrl+4 now opens Storage instead of Recent.

### Fixed
- Clicks on the full map no longer press hidden book or tracker buttons under it.

## 1.8.0 — 2026-10-22

### Added
- How to Get shows what the world gives: where a plant grows (Vile Mushroom: the Corruption, on grass), what
  breaking blocks, plants and trees drops (the game's own drop code, so chances and modded tiles are right), where
  to mine ores and gems and with which pickaxe, fishing and quest fish, shimmer, the bug net, every shop including
  the Travelling Merchant. Items with no source on the card went from 1,624 to 600.
- Recipes of one item that differ in a single ingredient are one recipe: the row reads "Shadow Scale / or Tissue
  Sample", counts the alternative you have most of, and Craft, Max and Track use the one you can make. The
  Recipes tab and How to Get list it once (386 recipes merged into 142 with Calamity).
- Armor cards show the set bonus of the shown combination, in the game's own words.

### Fixed
- Mouse3 on an item in the crafting list or grid opened the book instead of the item.

## 1.7.0 — 2026-10-22

### Added
- Bestiary: the book records when you first and last met each mob (it came near you; town NPCs excepted).
  "Recently seen" lists the last 24, newest first; the sort menu gets "Met: oldest" and "Met: newest"; the mob
  card shows a Met line with both dates in its tooltip. Saved per character.

### Changed
- A locked mob's grid tooltip reads "Not in the Bestiary yet".

## 1.6.2 — 2026-10-22

### Fixed
- The mod filter lists only mods that have something on the open tab: items on Items, recipes on Recipes, mobs
  on the Bestiary, what is stored on Storage, what was crafted on Recent. A chosen mod stays listed even when the
  tab has nothing from it right now, so a saved filter never disappears by itself.
- The window moves only by the grip (the dots left of the title); the frame and the empty header no longer drag
  it or light up.

## 1.6.1 — 2026-10-22

### Fixed
- With the inventory closed, a missed Mouse3 (no mob under the cursor) opened the book. In the world the book's
  mouse button now only opens a mob's entry; with the inventory or the book open it toggles the book as before,
  and a keyboard key bound to the book always toggles it.

## 1.6.0 — 2026-10-22

### Added
- Bestiary tab: every mob of the game and its mods, by kind (bosses, mini-bosses, enemies, town NPCs, critters),
  event and biome (Calamity's biomes included), with search, mod, sort, favorites, "encountered only" and the
  stage filter. The mob card shows an animated portrait over its biome, its own drops first (drops any enemy can
  give are folded under "Any enemy"), and its life, defense, damage, knockback resistance, coins, kills, banner
  progress and Calamity damage reduction once the Bestiary would show them (enemies after a kill, town NPCs after a
  chat, critters once seen). Mouse3 on a mob in the world opens its entry; How to Get's enemy rows lead there.
- Class buttons above the categories: Melee, Ranged, Magic, Summoner and Rogue (with Calamity) for weapons, ammo,
  armor, accessories and buff potions; armor and accessories by what they actually boost, set bonuses included.
- Popups (How to Get, Help) open over the blurred book instead of hiding it, with a short fade.
- Magic Storage: the book connects to the nearest network by itself (a Crafting Interface first) and follows you;
  a network picker appears when several are in reach; the card shows whether Craft takes from the inventory or
  the storage. "Auto-connect Magic Storage" in the config brings back the manual button when turned off.
- Optional Double-Tap Dash (Calamity): double-tapping left/right dashes again while Calamity's dash key keeps
  working. Off by default.

### Changed
- Tabs: Items · Recipes · Bestiary · Storage · Recent · Tracking (Ctrl+1…6).
- Categories are item types only; the per-class and per-damage-type entries moved to the class buttons.
- Help (F1) lists the new shortcuts.

### Fixed
- Magic Storage: holding a Locator or Portable Access near a network re-linked or used it up and spammed the chat;
  the storage status and the Craft button blinked every two seconds; ×5/×10/Max crafted one batch with recursive
  crafting on; the native Magic Storage window popped up after closing the book; any Magic Storage update turned
  the integration off. The adapter costs a tenth of what it did.
- Ctrl+Enter crafted and also opened the chat.
- A mob's name under the cursor was drawn over the open book; slimes were grey in mob portraits.
- Station names stayed in the previous language after switching it.
- Calamity armor sets with one helmet per class are recognised as sets.

## 1.5.2 — 2026-10-22

### Fixed
- Compact tracker lines match the game's info lines exactly: item icons sit on the same left edge as the
  watch and depth meter icons, and a long line is squeezed like the game's own instead of cut with "...".

## 1.5.1 — 2026-10-22

### Fixed
- Compact tracker lines in Russian sat 50 px left of the minimap. They now stay under the minimap's edge and
  follow the game's own info lines only when those are shown (the game moves its lines left in Russian).

## 1.5.0 — 2026-10-22

### Changed
- The whole book is redesigned: short, consistent texts in English and Russian, one layout for every tab
  (filter bar on top, categories on the left with icons, the item card on the right), clear hover tips with
  their shortcuts, and labels that never shrink or get cut off.
- Tabs are Items · Recipes · Storage · Recent · Tracking; Ctrl+1…5 open them by position. The Storage tab
  appears only with Magic Storage.
- The stage filter (Pre-Hardmode · Hardmode · Post-Moon Lord) is one dropdown in the header for every tab
  and is saved with the character.
- Search, mod, category, favorites and sort are kept per tab and never reset by themselves — not when the
  book closes, not when you jump to another item. Ctrl+Backspace clears the open tab's filters.
- The Chains tab is folded into the item card: switch between Recipe and Used in. Clicking an ingredient or a
  Used in row opens that item in the card.
- The item card shows the item's own mod (a vanilla item says Terraria even when Calamity changed its
  recipe) and names the recipe's mod separately.
- The window moves by its frame or the empty part of the header; the Move button is gone.
- Track keeps the book open, so several goals can be added in a row.
- Tracker overlays: the compact style is drawn like vanilla info-accessory lines under the minimap; the
  cards style is a compact list with progress bars.
- The F1 sheet lists every shortcut in three columns: moving around, the item card, finding things.

### Added
- Back / Forward for everything in the book (Mouse4 / Mouse5, Alt+← / Alt+→, the card's back button):
  tabs, filters, selected items, card steps, How to Get and enemy cards.
- Shortcuts: arrows move the selection, Alt+T track, Alt+F favorite, Alt+G How to Get, Alt+U Recipe / Used
  in, Alt+1…4 stage, Ctrl+Shift+Enter crafts as many as materials allow.
- Shift+Click in the grid and Mouse3 on an inventory item open the item's Used in list.

### Fixed
- The trackers showed counts capped at the need (2/2 with 10 in the inventory); they show what you have.
- Ctrl+digit and Alt+digit no longer switch the hotbar slot; F1 no longer switches to Loadout 1 while the book
  is open; quick buff no longer fires under the book's Ctrl/Alt shortcuts.
- Item tooltips over the book use the book's colours instead of the blue box.
- The How to Get / enemy cards and Help stay open when the book is closed and reopened.
- How to Get → a recipe landed on the wrong page.
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
