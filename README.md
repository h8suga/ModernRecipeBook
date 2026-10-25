<p align="center"><img src="icon.png" width="96" alt="Modern Recipe Book icon"></p>

<h1 align="center">Modern Recipe Book</h1>

<p align="center">
A recipe book and bestiary for every item and mob in Terraria and its mods, for tModLoader.
</p>

<p align="center">
<img src="https://img.shields.io/badge/version-1.15.1-4c8f5a" alt="Version 1.15.1">
<img src="https://img.shields.io/badge/tModLoader-2026.08.3.0-1f6feb" alt="tModLoader 2026.08.3.0">
<img src="https://img.shields.io/badge/Terraria-1.4.4.9-2ea44f" alt="Terraria 1.4.4.9">
<img src="https://img.shields.io/badge/Calamity-2.2.4-b03a2e" alt="Calamity 2.2.4">
<img src="https://img.shields.io/badge/Magic%20Storage-0.7.0.11-7d3c98" alt="Magic Storage 0.7.0.11">
<img src="https://img.shields.io/badge/multiplayer-supported-2ea44f" alt="Multiplayer supported">
<img src="https://img.shields.io/badge/languages-EN%20%7C%20RU-555555" alt="English and Russian">
</p>

<p align="center"><b>English</b> · <a href="README.ru.md">Русский</a></p>

<p align="center"><img src="docs/screenshots/items.png" width="860" alt="Items tab with a weapon card"></p>

Press **Mouse3** (middle click) and the book opens over the game. Every item and mob of the game and of every loaded
mod is in it, read from the game itself: recipes, drops, shops, crates, fishing, world generation and spawn rules. It
answers three questions without a wiki: *what is it*, *how do I get it*, and *can I make it right now*.

## Contents

- [Highlights](#highlights)
- [Features](#features)
  - [Recipe book](#recipe-book)
  - [Magic Storage integration](#magic-storage-integration)
  - [Calamity integration](#calamity-integration)
  - [Bestiary](#bestiary)
  - [Tracking, map and armor sets](#tracking-map-and-armor-sets)
  - [Languages](#languages)
- [Compatibility](#compatibility)
- [Installation](#installation)
- [Controls](#controls)
- [Configuration](#configuration)
- [Multiplayer](#multiplayer)
- [Building from source](#building-from-source)
- [Contributing](#contributing)
- [Changelog](#changelog)
- [Credits](#credits)

## Highlights

- **Everything in one window**: Items, Recipes, Bestiary, Storage, Recent crafts and Tracking tabs.
- **How to Get** for any item: recipes, drops, bags, crates, shops, fishing, shimmer and the world itself, with the
  drop chances of *your* world's difficulty and seed.
- **Craft from everything nearby**: chests, banks and Magic Storage networks, even a remote one.
- **Full Magic Storage and Calamity integration**, read from their own code: networks, Portable Access, Crafting
  Interface; Calamity bosses, biomes, events, Revengeance, the Rogue class.
- **Bestiary** with animated portraits, drops, spawn rules and the stats you have earned.
- **Multiplayer-ready**, in English and Russian, every action on a keyboard shortcut.

## Features

### Recipe book

- **Search** by name or by the item's own text, a mod filter, favorites, categories and sorting: name, ID, mod,
  crafting station, rarity, sell price, and damage, defense, healing, tool power and more where the category has them.
  Each tab keeps its own filters.
- **Stage filter**: Pre-Hardmode, Hardmode, Post-Moon Lord, or "Before: &lt;boss&gt;" for any vanilla or Calamity boss
  ("Next boss" by default). The stage of each item and mob is worked out from recipe chains, boss drops and bags,
  shops, crates, enemy drops, banners and world generation, for your world's difficulty.
- **Class buttons** (Melee, Ranged, Magic, Summoner, Rogue): weapons, armor and accessories are classified by what they
  actually boost. The **Traits** filter narrows weapons to homing, piercing, through walls, auto attack, minion or
  sentry, and says why a weapon counts.
- **Station filter**: untick the crafting stations you lack to hide what only they make. Items and Recipes can also
  show only what you can craft right now.
- **Item card**: the recipe with have/need counts, batches (x1, x5, x10, Max), Craft, Track and Favorite, or Used in.
  Recipes that differ in one ingredient show as one, "A or B". Back and Forward walk through everything you opened.
- **How to Get** lists every source of an item, with this world's drop chances. Loot only another difficulty or seed
  drops is marked "Not in this world".
- **Hover any row that stands for an item** to read that item's own tooltip, no click needed.

<p align="center">
<img src="docs/screenshots/weapon-traits.png" width="49%" alt="Weapon traits filter">
<img src="docs/screenshots/station-filter.png" width="49%" alt="Crafting station filter">
</p>
<p align="center">
<img src="docs/screenshots/traits-tooltip.png" width="56%" alt="Why a weapon counts as homing">
<img src="docs/screenshots/sort-menu.png" width="42%" alt="Sort menu">
</p>

### Magic Storage integration

Magic Storage is fully integrated: the book uses Magic Storage's own members and rules, not a copy of them.

- Every network in reach is part of the **Storage** tab, next to your chests and banks: its items, how many cells of
  its storage units are taken, and the stations of its Crafting Interface.
- **Craft from it directly**: the book counts what the network holds and takes the materials out itself.
- A bound **Portable Access** or **Portable Crafting Access** you carry brings its network in from afar, by Magic
  Storage's own range rules. A network opened through the Magic Storage window counts too; both read "Remote".
- **Quick Stack, Deposit All and Restock** reach the network, the remote one included. Deposit All leaves your hotbar,
  favorites and Magic Storage locators alone.
- Locked and password networks are named ("Not counted: 1 locked · 1 password"), never silently skipped.
- The **Changed** sort puts what was just stored, taken or crafted with first, whoever moved it.

<p align="center"><img src="docs/screenshots/storage.png" width="860" alt="Storage tab"></p>
<p align="center">
<img src="docs/screenshots/craft-from-storage.png" width="62%" alt="Crafting from nearby storage">
<img src="docs/screenshots/active-stations.png" width="36%" alt="Active crafting stations">
</p>

The footer chip shows the crafting stations in reach: nearby, in a Crafting Interface (near you or through a Portable
Crafting Access), and surroundings such as water or snow.

### Calamity integration

Calamity is fully integrated too: bosses, biomes, events and drop rules come from Calamity's own code.

- **Every Calamity boss is a progression step.** The stage filter, "Before: &lt;boss&gt;" and each item's "After
  &lt;boss&gt;" follow Calamity's order. Boss Checklist is not needed.
- **Biomes and events**: the Sulphurous Sea, the Abyss layers, the Sunken Sea, the Astral Infection and the Brimstone
  Crag sort the Bestiary, and so do the Acid Rain tiers. Where and after which boss a mob spawns is read from
  Calamity's spawn code.
- **Drop rules**: chances follow your world's difficulty, Revengeance included. Ores that appear on a boss's first
  kill (Cryonic, Perennial, Uelibloom, Auric and others) say which boss and where, and Calamity's early-hardmode ore
  order is taken into account.
- **Rogue** has its own class button, and the Traits filter knows homing stealth strikes.

### Bestiary

Every mob by kind, event and biome, with an animated portrait over its biome, its own drops, where and after which
boss it spawns (read from the mods' own spawn code too) and, once you have met it as the vanilla Bestiary requires,
its life, defense, damage, coins, kills and banner progress. Press Mouse3 on a mob in the world to open its entry.
The book remembers when you first and last met each mob; "Recently seen" lists the last ones.

<p align="center">
<img src="docs/screenshots/bestiary-spawns.png" width="66%" alt="Bestiary card with spawn rules">
<img src="docs/screenshots/mob-card.png" width="22%" alt="Mob card opened with Mouse3">
</p>
<p align="center"><img src="docs/screenshots/spawn-rules.png" width="60%" alt="Where and when a mob spawns"></p>

### Tracking, map and armor sets

- **Tracking**: follow up to five items or whole armor sets. Cards or compact lines under the minimap count the
  materials you still need.
- **Map**: Mouse3 on an explored spot of the full map teleports you there. Shared markers with a badge and an optional
  name show everyone who placed them and when.
- **Armor cards** show the set bonus and every piece of the set.

<p align="center">
<img src="docs/screenshots/tracker.png" width="30%" alt="Tracker under the minimap">
<img src="docs/screenshots/marker-create.png" width="34%" alt="Placing a map marker">
<img src="docs/screenshots/marker-view.png" width="31%" alt="A placed marker">
</p>
<p align="center"><img src="docs/screenshots/set-bonus.png" width="70%" alt="Armor set bonus"></p>

### Languages

The book is fully translated into Russian, including what it writes about items, sources and mobs. It follows the
game's language setting.

<p align="center">
<img src="docs/screenshots/russian-items.png" width="49%" alt="Items tab in Russian">
<img src="docs/screenshots/russian-storage.png" width="49%" alt="Storage tab in Russian">
</p>

## Compatibility

Tested with:

| | Version | |
|---|---|---|
| Terraria | 1.4.4.9 | |
| tModLoader | 2026.08.3.0 (stable) | required |
| Calamity Mod | 2.2.4 | optional, fully supported |
| Magic Storage | 0.7.0.11, with SerousCommonLib 1.0.6.2 | optional, fully supported |

Without Calamity or Magic Storage the book works with whatever the game has. Other mods' items, recipes, drops and
mobs are read the same way as vanilla ones.

## Installation

1. Download `ModernRecipeBook.tmod` from the [latest release](../../releases/latest).
2. Put it into `Documents/My Games/Terraria/tModLoader/Mods`.
3. Enable **Modern Recipe Book** in Workshop → Manage Mods and reload the mods.

On a server, install the same file on the server and on every client.

## Controls

Open the book with **Mouse3** (rebindable in Controls). Every button's tooltip names its shortcut, and **F1** in the
book shows them all.

<details>
<summary><b>All shortcuts</b></summary>

| Moving around | |
|---|---|
| Ctrl+1…6 | Go to a tab |
| Ctrl+Tab | Next tab |
| Mouse4 · Alt+← | Back |
| Mouse5 · Alt+→ | Forward |
| ← ↑ → ↓ | Move the selection |
| PgUp · PgDn | Turn pages |
| Home · End | First · last page |
| Esc | Close the top window |
| F1 | Help |

| The item card | |
|---|---|
| Ctrl+Enter | Craft |
| Ctrl+Shift+Enter | Craft the most |
| Alt+T | Track |
| Alt+F | Favorite |
| Alt+G | How to Get |
| Alt+U | Recipe · Used in (on a mob: Loot · Stats) |
| LMB | Open an ingredient |
| Shift+LMB | Where it's used |
| Alt+LMB | Favorite in the grid |
| Mouse3 | Look up the item or mob under the cursor |

| Finding things | |
|---|---|
| Ctrl+F | Search |
| Ctrl+Backspace | Reset filters |
| Alt+1…5 | Stage (5: before the next boss) |
| F5 | Refresh |

| Storage | |
|---|---|
| LMB | Take a stack |
| RMB, hold | One at a time |
| Shift+LMB | Stack to inventory |
| Ctrl+LMB | To the trash |
| Ctrl+Enter | Selected to inventory |
| Ctrl+Q · Ctrl+R | Quick stack · restock |
| Shift+LMB in inventory | Put a stack in |
| LMB holding an item | Put the held item in |

| Full map | |
|---|---|
| Mouse3 | Teleport there |
| RMB | Place a marker |
| RMB on a marker | Edit or delete it |

</details>

<p align="center"><img src="docs/screenshots/shortcuts.png" width="70%" alt="Shortcuts help"></p>

## Configuration

Mod Configuration → Recipe Book (yours) and Recipe Book: Server (the host's).

| Option | Default | |
|---|---|---|
| Window Size | 90% | Shrinks the book to fit small screens |
| Text Size | 100% | Text only, not the window |
| Middle-Click Item Search | on | Mouse3 on an item in any slot opens it in the book |
| Tracking Cards Opacity / Size | 90% / 100% | The tracking overlay's panel |
| Tracking Overlay: Compact Lines | off | Material lines under the minimap instead of cards |
| Double-Tap Dash (Calamity) | off | Double-tap left or right to dash |
| **Server:** Nearby Storage | on | Show, count and craft from chests and Magic Storage in the radius |
| **Server:** Nearby Storage Radius | 30 tiles | 10–50 tiles |
| **Server:** Map Teleport | on | Mouse3 on the full map |
| **Server:** Shared Map Markers | on | Markers everyone on the server sees, kept with the world |

## Multiplayer

The mod is Both-sided and built for servers, Host & Play included. Taking from and putting into storage is carried out
by the server; map markers are shared and kept with the world; spawn rules and world sources are read on clients the
same way as in single player. The host decides in Recipe Book: Server whether
nearby storage, map teleport and markers are on.

## Building from source

1. Clone the repository into `Documents/My Games/Terraria/tModLoader/ModSources/ModernRecipeBook`. The folder must be
   named `ModernRecipeBook`: tModLoader takes the mod's name from it.
2. In tModLoader open Workshop → Develop Mods and press **Build** next to Modern Recipe Book, or run `dotnet build` in
   the folder.

The [docs](docs/) folder has the design notes of the larger features (nearby storage, boss progression, spawn gates,
weapon traits and others); it is kept out of the built `.tmod`.

## Contributing

Bugs and ideas go to [Issues](../../issues). For a bug, say what you did, what you expected and what happened, with
your tModLoader, Calamity and Magic Storage versions and `tModLoader-Logs/client.log`. Pull requests are welcome;
user-facing text comes in both English and Russian.

## Changelog

Every release is in [CHANGELOG.md](CHANGELOG.md), newest first. The versions follow
[Semantic Versioning](https://semver.org/).

## Credits

- Author: 2k17clubbb.
- Built on [tModLoader](https://github.com/tModLoader/tModLoader).
- Integrates with [Calamity Mod](https://calamitymod.wiki.gg/) and
  [Magic Storage](https://github.com/absoluteAquarian/MagicStorage); both belong to their authors.
