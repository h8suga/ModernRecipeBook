# Changelog

All notable changes to Modern Recipe Book. Versions follow [Semantic Versioning](https://semver.org/):
patch for fixes, minor for features.

## 1.18.0 — 2026-10-27

### Added
- The item card of anything with loot of its own (boss Treasure Bags, fishing crates, presents, Calamity's bags and
  geodes) gets a third mode, **Contents**: what it holds, with the same rows, chances, conditions and hover as the
  mob card's loot. A Treasure Bag on a mob card and a Container row in How to Get open it straight on its Contents.
- The item card's header names the item's category after its mod ("Terraria · Weapons › Whips"): what the item is,
  the most specific sidebar category, with its group when there is room (a long mod name shortens first). Clicking a part
  of the path opens Items on that category with the search cleared and every other filter kept, the item still selected;
  Back returns.
- Coins go to the Piggy Bank first: Deposit All, Shift+Click and dropping a stack on the Storage tab put coins into the
  Piggy Bank when it is open or in range (a Money Trough counts too), combined into the fewest stacks; only what does not
  fit goes on to nearby storage, and with the bank full the coins from the coin slots stay there. With the Piggy Bank in
  range Deposit All also empties the coin slots, works with no other storage around, and Magic Storage's own Deposit All
  sends coins to the Piggy Bank first.

### Fixed
- No drop condition reads "Special condition" any more:
  - a condition that tests the kill itself (Astrum Deus's split worms, the last Exo Mech, a supercharged Wulfrum
    robot) says nothing when a normal kill meets it, and otherwise what the kill needs ("While supercharged");
  - rules of another difficulty, Revengeance/Death or seed say which ("Not in this world · Death mode"), loot that
    drops only before a boss reads "Before: Eater of Worlds", a boss's own first-kill loot reads "Only the first
    kill", and gates the world meets say nothing.
- In Expert and Master worlds a boss's Classic-only loot is listed as coming from its Treasure Bag ("In Treasure Bag
  (Empress of Light)", with the bag's chance) on the mob card and in How to Get; the hover marks the boss's own
  Classic chance as not in this world.
- Drops of a rule's failed branch (Nova's Starblight Soot, the Caustic Croaker Staff before Aquatic Scourge) are
  listed under the right condition.
- Drops the game's Bestiary hides for good (Calamity's hidden drops such as the Coldheart Icicle) are hidden in the
  card, How to Get and stages too; Windy Balloon kites say they come from the slime they lift.
- In a drunk world the evil boss step reads "Eater of Worlds or Brain of Cthulhu", and both evils' loot counts as
  this world's.
- The mob card no longer shows the previous world's loot after you enter a world of another difficulty.
- Money Troughs, Chester and Void Lenses near the player count as their bank in the Storage tab.

## 1.17.1 — 2026-10-26

### Fixed
- How to Get names the boss behind a drop condition its mod left without a text: Starbuster Core from Stellar Culex
  reads "After Astrum Aureus · not met yet" instead of "Special condition", as do Hadarian Wings, the Eidolon Wyrm's
  Ectoplasm ("After Plantera") and the rest of Calamity's boss-gated drops. Conditions that wait for something other
  than a boss keep "Special condition".

## 1.17.0 — 2026-10-25

### Added
- How to Get knows the items the game hands out without a drop table: of the 436 items that read "No regular
  source", 4 are left (Bone Block and Lihzahrd Furnace, which nothing places, plus two leftovers).
  - The Dungeon, the Jungle Temple and the Underworld houses: their furniture, paintings, banners, lights and traps,
    the Biome Chest weapons, Gold and Shadow Chests, with the key, pickaxe power and boss each one waits for.
  - World generation everywhere else: surface, jungle, ice, desert, living tree, mushroom, spider cave and underwater
    chest finds, the chests themselves, Dead Man's Chests, traps, geysers and detonators, pyramid and sky island
    banners, the paintings of underground cabins and desert houses, The Dirtiest Block. Cave mosses come from
    scraping long moss with a Paint Scraper.
  - Angler quest rewards: the fixed ones by quest count (Calamity's replacements included) and the random ones with
    their threshold, hardmode gate and chance per quest.
  - Items the game gives in code: the rest of the Traveling Merchant's stock, right-click forms (Shellphone,
    Rubblemaker, the guide books), shimmering after Moon Lord, rare butterflies, the Torch God's Favor, Magic
    Storage's cores and more.
  - Calamity: the Brimstone Witch's Exhumed curse, Getfixedboi boss drops, rare critter catches, the Divine Swine,
    the Gluttony Blender, shaken astral and acid wood trees, Lilies of Finality, Abyssal Pots, the Abyss's locked
    chests, the starter bag and the Boss Rush rock; the Onyx Excavator Key, Crags Pylon and Astral Chest show their
    structures.
- Sixteen Calamity weapons whose text does not say it now carry Homing (Crushsaw Crasher, Poseidon, Void Edge,
  Essence Flayer...) or Homing stealth strike (The Old Reaper), each checked in the mod's code.

### Changed
- Items nothing can give are no longer listed: pickup icons, unnamed placeholders, banners no enemy counts toward,
  Magic Storage's UI icons, Calamity's cut fish, bricks, a wall and developer tools (43 items).
- Stages: Dungeon pieces wait for Skeletron, Biome Chest loot for Plantera, the Temple's traps and altar for a 210%
  pickaxe; the Dungeon's pre-hardmode enemies and their drops (Golden Key, Nazar, Tally Counter...) count from
  Skeletron too.

### Fixed
- The banners of the colored slimes (Green, Pinky, Black, Purple, Red, Yellow, Jungle) were not found.
- ru: the Astral Chest row says Темница (the Dungeon), not Подземелье.

## 1.16.2 — 2026-10-25

### Fixed
- Heavenfallen Stardisk had no source and passed the stage filter right after the Wall of Flesh. How to Get now
  names the Astral Chest that Calamity places in the Dungeon (one per world), and the item waits for Astrum Aureus,
  whose defeat opens that chest's lock.
- Murasama had no source either: it is in the chest of Draedon's lab in the Underworld from world creation, and
  How to Get says so; its stage is the Devourer of Gods, since it only works after that boss.

### Added
- How to Get knows machines that make an item by themselves: Calamity's Power Cell Factory is now a source of the
  Draedon Power Cell ("Machine · one every 15 s"; it keeps up to 9999 until you take them out), next to the Repair
  Unit's drop.
- Developer and patron sets have a source: How to Get names the treasure bags of hardmode bosses with the chance
  per bag (tModLoader's patron sets 1 in 14, one of them; its developer sets 1 in 50; vanilla's 1 in 16, one of 18),
  so items such as A Call Beyond no longer read "No regular source". Their stage is the first hardmode bag.

## 1.16.1 — 2026-10-25

### Changed
- Every text a translator sees is a whole phrase with placeholders ("Recipe: {0}", "{0} or {1} more", "Took {0} of
  {1}: the rest is gone") instead of pieces the code glued together, so a translation can put the words in its own
  order. The sort menu's rows are whole phrases too ("Damage: lowest first"). Nothing reads differently in English
  or Russian.
- [TRANSLATING.md](TRANSLATING.md) walks through tModLoader's own workflow: create one empty file of your language,
  build the mod, and every line appears in it commented out in English; `Localization/TranslationsNeeded.txt`
  shows how far each language is.

### Developer
- The sources are laid out by area under `src/` (UI, Catalogue, Sources, Progression, Mobs, Storage, Tracking,
  Recent, WorldMap, Common), each folder its own namespace; `GlobalUsings.cs` brings them in once.

## 1.16.0 — 2026-10-25

### Added
- The book can be translated into any language, the way Calamity is. Every text it shows (875 of them) now lives in
  `Localization/<culture>/Mods.ModernRecipeBook.<Area>.hjson`, one file per part of the book, instead of being built
  into the code as English/Russian pairs. A language without its own files, or a line they leave out, shows English,
  so a partial translation already works; a translation can come as a pull request or as a separate translation mod.
  [TRANSLATING.md](TRANSLATING.md) explains the files, placeholders and plurals.
- Counted texts use each language's own plural rules ("21 сундук", "22 сундука", "25 сундуков") through
  tModLoader's `{^0:...}` forms.
- The build checks the localization: every key the code names exists in English, Russian is complete, and a
  translation keeps the English placeholders.

### Changed
- Settings and control names moved into the same per-culture folders.
- Texts cached while drawing (station rows, trait labels, marker tooltips, the Changed sort's "5 min ago") follow
  any language change, not only one between English and Russian.
- Russian "days ago" reads "дн. назад" everywhere; the Bestiary wrote "дн назад".

## 1.15.1 — 2026-10-25

### Changed
- Hovering an ingredient or a Used in row in the item card shows that item's own tooltip at once, as a grid cell
  does, with the row's counts and the click hint under it. Before, it showed only the counts and needed a click to
  read the item.
- The same for every other row that stands for an item: Bestiary loot (a mob's own and Any enemy), the armor set's
  pieces in the card, How to Get sources that are items (a Treasure Bag, a crate, what shimmers into it) and the
  Tracking tab's materials. Under the item's own lines come only what the row cuts short: a loot row's full
  conditions for every rule, "Not in this world", the click hint.

### Fixed
- Drop chances follow the world you play in. A drop whose chance differs by difficulty or world seed (Classic vs
  Expert, Revengeance, the Constant or Remix seeds) showed "Varies by condition" in the Bestiary and in How to Get;
  it now shows this world's chance, e.g. Defective Sphere from a Deadly Sphere: 6.67% in Expert. Of 594 such loot
  lines, 22 still vary in an Expert Revengeance world, all by conditions that change as you play.
- Loot only another difficulty or seed drops (a boss's own weapons in Expert, which come in its Treasure Bag) reads
  "Not in this world · Classic" and is listed last, instead of a chance that never applies here.
- Vanilla's Classic / Expert / Master conditions no longer read "Special condition · not met yet": they have no text
  of their own, and the book now names them.

## 1.15.0 — 2026-10-25

### Added
- Storage sort "Changed: newest first / oldest first": what you just put in, took out or crafted with comes first,
  whoever moved it, you or another player. The selected stack says how long ago ("×27 · Terraria · 5 min ago") while
  this sort is on. The times are kept with your character, per world. What was already in storage before the update
  has no time yet and counts as oldest; walking to another base does not count as a change.
- The Storage tab's Quick Stack also reaches the Magic Storage the book counts, including the remote one through your
  Portable Access. As with any quick stack, an item goes only where the same item already is, what does not fit stays
  with you, and the hotbar, favorites, coins and your Portable Access are never touched. The book says what went where
  ("Stacked 18 items into remote storage").
- "Before: <boss>" in the stage filter: pick a boss and the book lists what you can get or craft with every boss
  before it down and that one still alive. The stage menu is one panel with the stages and every boss in three
  columns with its head, downed ones ticked; "Next boss" (Alt+5) is the first one not yet down in this world and moves
  on by itself. Works on Items, Recipes, the Bestiary, Storage's own filter and the card's Used in list, and is saved
  with the other filters.
- Every item, recipe and mob now has the boss it waits for, in Boss Checklist's order (the vanilla and Calamity
  bosses), not just one of three stages. Boss loot follows its boss;
  enemies their spawn (post-Plantera Dungeon, eclipse, moons, Martians, pillars, Acid Rain, mods' spawn gates); shops
  and drops their conditions, Calamity's lambdas too; ores the boss that spawns them and the pickaxe they need,
  Calamity's hardmode ore rework included; fishing its biome.
- The card's stage line names that boss ("After Plantera"), and its hint shows why: "Spectre Bar ← Ectoplasm ← drop:
  Dungeon Spirit".
- "Unknown stage" in the stage menu lists what the book cannot place, and the footer counts what the stage filter hid
  for that reason.
- Weapon traits filter on Items and Recipes: a Traits button under the class buttons opens a list of traits — Homing,
  Homing stealth strike (with Calamity), Pierces, Through walls, Auto attack, Minion, Sentry. Tick one or more and only
  the weapons and ammo that have all of them stay. It works with every other filter, is kept with your UI state and
  only changes when you change it. Each row shows how many listed items have that trait.
- The item card names the item's traits under its name; hover it to see where each one comes from: the description's
  own words (quoted), the projectile's stats, or a weapon checked by hand in the game's code. Homing is read from the
  description, in English too when the game speaks another language; a few vanilla weapons whose text does not say it
  (Razorblade Typhoon, Magic Missile, Wasp Gun and more) were checked in the code. Nothing is guessed: a trait the book
  cannot be sure of is left out.
- The boss order needs no Boss Checklist: the book takes Calamity's bosses the way Calamity hands them to Boss
  Checklist, with Calamity's own order and downed checks, and keeps its own table if that ever fails.

### Fixed
- How to Get said Hallowed Ore comes after The Destroyer; with Calamity it comes with the third mechanical boss,
  whichever it is. Astral Ore now says it waits for Astrum Deus, and Aerialite names both evil bosses.
- Fishing crates, the Golden Carp, the Alchemy Table and other catches had a hardmode stage from their rarity; they
  follow their fishing spot now. Catches only the Hallow or the Astral Infection give wait for hardmode.

## 1.14.0 — 2026-10-25

### Added
- A bound Portable Access in your inventory is enough: you no longer have to open Magic Storage's window. Its network's
  items show in the Storage tab and the cells line, count for recipes and tracking, are pulled for crafts, and take
  deposits when it is the nearest network. A Portable Crafting Access also brings its Crafting Interface's stations,
  listed in Active stations. Magic Storage's own rules decide whether it reaches: its range, pylons, a storage still in
  place, and the network's password. When it does not, the Storage tab's "Not counted" line says why. Works on a
  server, which checks the same rules itself.

### Fixed
- Deposit All in the Storage tab leaves Magic Storage locators (Portable Access, Portable Crafting Access, Locator)
  in your inventory: putting one away would cut off the network it reaches in the same click.
- Deposit All never touches the hotbar, coins in it included. Favorites stay, as before.
- A storage reached from afar (a carried Portable Access, or Magic Storage's own window) is named as such: "1 remote
  storage", "Remote via Portable Access", instead of "Within 30 tiles", which it is not.

## 1.13.0 — 2026-10-25

### Added
- The Storage tab says how full your storage is: "Cells: 1,234 / 3,200" under the radius line. It counts the cells of
  every Magic Storage network in range, as Magic Storage's own window counts them, plus the slots of nearby chests,
  banks, the open container and a carried Void Bag. At 90% full the line turns amber. Hover it for each network and
  the chests apart. Inactive storage units are left out, and so is the Creative Storage Unit, which has no limit; the
  tooltip says when either is there. Works the same on a server.
- Active stations: a small chip in the book's footer (Items, Recipes, Recent, Tracking) shows the strongest crafting
  station in reach and how many count. Hover it for their icons, grouped: placed nearby, in a Magic Storage Crafting
  Interface (near you or opened through Portable Crafting Access), and surroundings such as water or the snow biome.
  A station that only counts through another is dimmed with that station's icon on its corner (the Adamantite Forge's
  Furnace and Hellforge, the Alchemy Table's Bottle); hover an icon for its name and where it comes from. The list is
  what the book itself crafts with, so it always matches what it calls craftable.

### Changed
- The "Value" sort is called "Sell price", the game's own name for an item's price (it shows it while a shop is
  open). The order is the same.

## 1.12.1 — 2026-10-25

### Fixed
- Bestiary cards of modded mobs say after which boss they spawn in multiplayer too. On a server they kept the
  Bestiary's tags (Plaguebringer: "Spawns: Not listed" instead of "Jungle · after Golem"). The client now fetches the
  bit of the world the check reads from the server first, so its answer matches single player.
- The spawn check runs again for every world you enter, instead of once per game start.

## 1.12.0 — 2026-10-24

### Added
- Search looks at the item's own text too, as the game shows it with every mod's edits: "rogue" finds Monk's Shirt and
  the weapon flasks. Name matches come first, then the ones found in the text; the pager says how many came from the
  text. Stat lines ("50 rogue damage", "Material") are left out so they don't drown the rest. The texts are read in
  small steps while the book is open, so opening it never stalls; until then search uses names.
- Craftable filter on Items and Recipes (the hammer button): only what you can craft right now, from your inventory and
  nearby storage at the stations in range. It updates when your materials change and combines with the station, stage
  and class filters.
- Sorts that fit the category: weapons by damage, speed, crit chance or knockback, armor and accessories by defense,
  wings by flight time, potions and food by healing, mana or duration, tools by pickaxe, axe or hammer power, fishing
  poles and bait by their power; rarity and value everywhere. Each tab remembers its choice and goes back to it when
  the category fits again.
- Bestiary cards of modded mobs say after which boss they spawn, read from the mods' own spawn code: Calamity's plague
  enemies "Jungle · after Golem", Cryo Slime "Snow · after Cryogen · after any two mechanical bosses", Aero Slime
  "after The Hive Mind or The Perforator Hive". The bestiary's stage follows the gate too.

### Changed
- The search box reads "Name, text, ID, mod"; the mod dropdown on Items and Recipes is narrower to make room for the
  Craftable button, and a long mod name shows in full on hover.

## 1.11.2 — 2026-10-24

### Fixed
- Calamity's Acid Rain enemies read "Spawns: Not listed": Calamity spawns them from its event tables, not the
  Bestiary. The card now says where and when, e.g. Cragmaw Mire "in water · after Aquatic Scourge", and its hint
  gives the whole picture: "Acid Rain mini-boss in the Sulphurous Sea", how the event starts (on its own or with a
  Caustic Tear). Cragmaw Mire, Mauler and Nuclear Terror now count as mini-bosses.
- A mob card value cut short (a long Biome line, for example) could not be read: the card's general hint covered
  it. The full value now heads that hint.

## 1.11.1 — 2026-10-24

### Fixed
- The class filter missed Calamity's reworks of vanilla gear. Monk and Shinobi Infiltrator armor now count for
  summoner and rogue (not melee), Gladiator armor for rogue, weapon imbue flasks for melee, whips and rogue, and Ale
  and Sake for melee. 22 items changed; a check of every armor piece, accessory and potion against its full
  tooltip found no others.

## 1.11.0 — 2026-10-24

### Changed
- The Storage tab has its own stage filter. Picking Hardmode to browse the catalogue no longer hides the
  Pre-Hardmode materials in your chests; Items, Recipes, Bestiary, Recent and Tracking still share one. Both are
  remembered; Storage starts at All stages.

### Fixed
- Vanilla armor sets that Calamity reworks showed only the vanilla set bonus on the card. Monk's set now reads
  "+90 maximum stealth" before the sentry lines, as the game's own tooltip does; the same goes for the other
  reworked sets.

## 1.10.0 — 2026-10-23

### Added
- Station filter on the Items and Recipes tabs: the anvil button next to the sort lists the crafting stations of
  what the other filters show, with how many items need each. Untick the ones you lack, as many as you like, and
  whatever only they make is hidden; an item stays while one of its recipes needs none of them. The badge counts
  the unticked stations, the tooltip names them, Show all or Ctrl+Backspace brings everything back. Each tab
  remembers its own choice.
- Station sort on the Items and Recipes tabs: weakest first or strongest first, by the station the easiest recipe
  needs (by hand, Work Bench and Iron Anvil up to Mythril Anvil, Ancient Manipulator and Calamity's forges).

### Fixed
- A drop condition without a text (Calamity's Hadarian Wings after Astrum Aureus and many more) now says whether
  it holds in your world: "Special condition · met" or "not met yet".
- Filtering no longer churns memory: every search, category or sort change used to throw away about 8 MB on Items
  and 4.5 MB on Recipes. Typing in the search is also several times faster.

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
