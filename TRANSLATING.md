# Translating Modern Recipe Book

Every text the book shows lives in `Localization/<culture>/`, one file per part of the book, the same way Calamity
lays out its own: `Mods.ModernRecipeBook.<Area>.hjson`. English (`en-US`) and Russian (`ru-RU`) ship with the mod;
any other language can be added without touching the code.

Item, mob, biome, NPC and mod names are not in these files: the game and the other mods give them in your language
already.

## Files

| File | What it holds |
|---|---|
| `Common` | The book's frame, the item card, crafting and tracking texts |
| `Categories`, `BestiaryCategories` | Category names in the sidebar |
| `Classes`, `Traits`, `VerifiedHoming` | Class buttons, weapon traits and why a weapon counts as homing |
| `Sorts`, `Stages`, `Stations`, `Craftable`, `Variants` | Sorting, the stage filter, the station filter, Craftable, recipe variants |
| `HowToGet` | The How to Get panel |
| `Storage`, `ActiveStations`, `Crafting` | Nearby storage, the active stations chip, crafting messages |
| `Bestiary`, `MobCard`, `MobPortrait` | The Bestiary tab and mob cards |
| `Markers`, `Badges`, `Map` | Map markers, their badges, map teleport |
| `Help`, `Time` | The F1 sheet (keycaps too), "5 min ago" and date patterns |
| `Configs`, `Keybinds` | Mod settings and control names |

## How to translate

1. Put the mod's source folder into `Documents/My Games/Terraria/tModLoader/ModSources/ModernRecipeBook`.
2. Create one empty file for your language, named as tModLoader knows it, for example
   `Localization/de-DE/Mods.ModernRecipeBook.Common.hjson` (`de-DE`, `fr-FR`, `es-ES`, `it-IT`, `pl-PL`, `pt-BR`,
   `zh-Hans`).
3. Build the mod in Workshop → Develop Mods. tModLoader writes every file for your language itself, with each line
   still to translate commented out in English (`// Recipe: "Recipe: {0}"`). `Localization/TranslationsNeeded.txt`
   shows how far each language is.
4. Uncomment a line and translate its value; keep the key as it is. Whatever stays commented out is shown in English,
   so a partial translation already works.
5. Every text is a whole phrase. Keep the placeholders: `{0}`, `{1}` are filled in by the book (`Recipe: {0}` becomes
   `Recipe: Wooden Sword`) and can move wherever your language needs them (`{0} or {1} more`).
6. Plurals use tModLoader's `{^0:...}`: the forms are separated by `;` and picked by the number in `{0}` with your
   language's own rule. English and most languages have two forms (`{^0:chest;chests}`); Russian and Polish have
   three (`{^0:сундук;сундука;сундуков}`), French treats 0 and 1 alike, Chinese uses the first form only.
7. `Time.DateShort` and `Time.DateLong` are .NET date patterns (`MMM d`, `d MMMM yyyy`); the month names come from
   the game's language. `Common.OrSeparator` (" or ") joins names in a list.

Build again and switch the game's language to see it. tModLoader rewrites the files into its own formatting when it
loads them; that is expected.

## Sending it

- **A pull request** adding your `Localization/<culture>` folder. The build checks that every key exists in English
  and that your values keep the English placeholders.
- **A translation mod** of your own, as Calamity's community translations do: a mod whose `Localization/<culture>/`
  files hold `Mods.ModernRecipeBook.*` keys. tModLoader applies them to Modern Recipe Book.
