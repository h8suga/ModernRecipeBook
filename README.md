<p align="center"><img src="icon.png" width="80" alt="Modern Recipe Book icon"></p>

<h1 align="center">Modern Recipe Book</h1>

<p align="center">
A recipe book and bestiary for every item and mob in Terraria and its mods, for tModLoader.<br>
English and Russian · single player and multiplayer · Calamity and Magic Storage aware
</p>

<p align="center"><a href="README.ru.md">Русская версия</a></p>

<p align="center"><img src="docs/screenshots/items.png" width="860" alt="Items tab with a weapon card"></p>

## What it does

Press **Mouse3** (middle click, rebindable in Controls) to open the book. Every item and mob of the game and of every
loaded mod is in it, read from the game itself: recipes, drops, shops, crates, fishing, world generation and spawn
rules. The book answers three questions without a wiki: *what is it*, *how do I get it*, and *can I make it right now*.

- **Six tabs**: Items, Recipes, Bestiary, Storage, Recent crafts and Tracking.
- **Search** by name or by the item's own text, a mod filter, favorites, categories and sorting (damage, defense,
  healing, tool power, rarity, sell price and more where the category has them). Each tab keeps its own filters.
- **Stage filter**: Pre-Hardmode, Hardmode, Post-Moon Lord, or "Before: &lt;boss&gt;" for any vanilla or Calamity boss.
  Stages are worked out from recipe chains, boss drops and bags, shops, crates, enemy drops, banners and world
  generation, for your world's difficulty.
- **Class buttons** (Melee, Ranged, Magic, Summoner, Rogue) and a **Traits** filter: homing, piercing, through walls,
  auto attack, minion, sentry.
- **Item card**: the recipe with have/need counts, batches (x1, x5, x10, Max), Craft, Track and Favorite, or Used in.
  Back and Forward (Mouse4/Mouse5, Alt+Left/Right) walk through everything you opened.
- **How to Get**: every source of an item, with the drop chances of your world's difficulty and seed.
- **Hover any row that stands for an item** to read that item's own tooltip.

<p align="center">
<img src="docs/screenshots/weapon-traits.png" width="49%" alt="Weapon traits filter">
<img src="docs/screenshots/station-filter.png" width="49%" alt="Crafting station filter">
</p>
<p align="center"><img src="docs/screenshots/traits-tooltip.png" width="70%" alt="Why a weapon counts as homing"></p>

## Nearby storage and Magic Storage

Chests, banks and every Magic Storage network around you (10–50 tiles, 30 by default) become one sorted, searchable
list, with how many of their cells are taken. A bound Portable Access in your inventory brings its far network in too,
within Magic Storage's own range rules.

Take items like from a chest (click, hold right-click, Shift+Click), put them away with Shift+Click or by dropping them
on the list, and craft straight from them: the book counts everything nearby and pulls the materials it needs. In
multiplayer the server moves the items.

<p align="center"><img src="docs/screenshots/storage.png" width="860" alt="Storage tab"></p>
<p align="center">
<img src="docs/screenshots/craft-from-storage.png" width="62%" alt="Crafting from nearby storage">
<img src="docs/screenshots/active-stations.png" width="36%" alt="Active crafting stations">
</p>

The footer chip shows the crafting stations in reach: nearby, in a Magic Storage Crafting Interface (even one opened
remotely), and surroundings such as water or snow.

## Bestiary

Every mob by kind, event and biome (Calamity's biomes included), with an animated portrait over its biome, its own
drops, where and after which boss it spawns (read from the mods' own spawn code too) and, once you have met it as the
vanilla Bestiary requires, its life, defense, damage, coins, kills and banner progress. Press Mouse3 on a mob in the
world to open its entry. The book remembers when you first and last met each mob.

<p align="center">
<img src="docs/screenshots/bestiary-spawns.png" width="66%" alt="Bestiary card with spawn rules">
<img src="docs/screenshots/mob-card.png" width="22%" alt="Mob card opened with Mouse3">
</p>
<p align="center"><img src="docs/screenshots/spawn-rules.png" width="60%" alt="Where and when a mob spawns"></p>

## Tracking, map and armor sets

- **Tracking**: follow up to five items or whole armor sets. A compact tracker under the minimap counts the materials.
- **Map**: Mouse3 on an explored spot of the full map teleports you there. Shared markers with a badge and an optional
  name show everyone who placed them and when. The host can turn nearby storage, map teleport and markers off in the
  server settings.
- **Armor cards** show the set bonus and every piece of the set.

<p align="center">
<img src="docs/screenshots/tracker.png" width="30%" alt="Tracker under the minimap">
<img src="docs/screenshots/marker-create.png" width="34%" alt="Placing a map marker">
<img src="docs/screenshots/marker-view.png" width="31%" alt="A placed marker">
</p>
<p align="center"><img src="docs/screenshots/set-bonus.png" width="70%" alt="Armor set bonus"></p>

## Keyboard

Every button's tooltip names its shortcut, and **F1** in the book lists them all.

<p align="center"><img src="docs/screenshots/shortcuts.png" width="70%" alt="Shortcuts help"></p>

## Russian

The whole book is translated, including what it writes about items and mobs.

<p align="center">
<img src="docs/screenshots/russian-items.png" width="49%" alt="Items tab in Russian">
<img src="docs/screenshots/russian-storage.png" width="49%" alt="Storage tab in Russian">
</p>

## Requirements

| | |
|---|---|
| Game | Terraria 1.4.4.9, tModLoader 2026.8.3.0 |
| Side | Both: install it on the server and on every client |
| Optional | Magic Storage 0.7.0.11 (storage networks, Crafting Interface, Portable Access) |
| Supported | Calamity Mod 2.2.4: bosses, biomes, events, Revengeance and Death drops |

## Install

1. Download `ModernRecipeBook.tmod` from the [latest release](../../releases/latest).
2. Put it into `Documents/My Games/Terraria/tModLoader/Mods`.
3. Enable **Modern Recipe Book** in Workshop → Manage Mods and reload.

## Build from source

1. Clone the repository into `Documents/My Games/Terraria/tModLoader/ModSources/ModernRecipeBook`. The folder must be
   named `ModernRecipeBook`: tModLoader takes the mod's name from it.
2. In tModLoader open Workshop → Develop Mods and press **Build** next to Modern Recipe Book. `dotnet build` from the
   folder works too.

## More

- [CHANGELOG.md](CHANGELOG.md): every release, newest first.
- [docs/](docs/): design notes for the larger features (nearby storage, boss progression, spawn gates, weapon traits
  and others).
