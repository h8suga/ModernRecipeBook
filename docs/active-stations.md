# Active stations (branch `feature/active-stations`)

Owner: «нужно где-то в книге выводить активные станции если можно. при наведении желательно куда-то с иконочками чтобы не сильно мешало. но полезно». Code: `RecipeBookSystem.ActiveStations.cs`.

## What counts

The list is read from the very state the book checks recipes against, so it cannot disagree with what the book calls craftable.

- `BookCrafting.Collect` runs `player.AdjTiles()` and then `BookMagicStorage.BeginStations`, which merges in the stations of the counted Crafting Interfaces: those near the player and the one open through Portable Crafting Access. Inside that scope it calls `CaptureActiveStations`.
- **Stations** are the tiles some enabled recipe requires (`RankedStations`, the station sort's list) that are set in the merged `adjTile`. Stations that only stand for others are added too (an Alchemy Table is no recipe's station, it is a Bottle and a Table).
- **Surroundings** are vanilla's own recipe conditions, evaluated in the same scope: `Condition.NearWater` (water also counts at a Sink), `NearHoney`, `NearLava`, `NearShimmer`, `InSnow`, `InGraveyard`. Their names are the game's condition texts, the same as in a recipe's conditions.
- Stand check: `stand.ps1 c call p=ModernRecipeBook.RecipeBookSystem.ActiveStationsAudit` prints the list and checks that every recipe the book calls craftable has its stations and surroundings in it (`missing=0`).

## Groups and "via"

- **Nearby**: set in the player's own `adjTile`.
- **Crafting Interface** (named with Magic Storage's own item name): set only after the merge.
- **Surroundings**: each tooltip says where it comes from: next to you, the biome, or the Crafting Interface (a bucket or a Biome Globe in it).
- A station is **placed** when it stands in the box `Player.AdjTiles` reads (Artisan Loaf widens it), or is an item in a counted interface. Any other station counts through another one. To find which, every placed tile is run through `BookMagicStorage.AddStation`, the crafting rule itself: vanilla's forges, anvils and tables, `TileLoader.AdjTiles` for mods, and MS's rule that a table or work bench is also a chair. An interface's group also asks the placed tiles nearby, so a chair from the interface can read "via Work Bench". The box scan only tells placed from counted-through; whether a station counts is always `adjTile`'s answer.
- Placed first, then counted-through, each in station order (weakest to strongest).

## Look

- **Chip**: in the footer, left of the Help button, on Items, Recipes, Recent and Tracking. It shows the strongest placed station's icon and how many things count. With nothing in reach it shows a dimmed Work Bench and 0.
- **Panel**: opens on hover, above the chip, right edges aligned. It stays open while the mouse is on the chip, on the panel or in the gap between them. It is a popover like the dropdowns: a shadow, no blur. Clicks and the wheel over it do not reach what is under it, and nothing under it hovers.
  - Title "Active Stations" / «Активные станции» with the count, then a section label per group.
  - Placed: a filled cell. Counted through another: a hollow cell, the icon dimmed, and the source station's small icon on its corner, so the state never rests on colour alone.
  - Hovering an icon names it with one more line: "Placed nearby" / «Стоит рядом», "Counts via Adamantite Forge" / «Считается через: Адамантитовая кузня», and so on.
  - Five to eight columns, as many as the largest group needs; wider when a title or group name needs it.
  - Empty: "No stations in reach" / «Станций рядом нет», with a hint.
  - Mouse3 on an icon opens that item, like any icon in the book.

## Cost

- The capture runs at the book's crafting refresh (every 20 ticks while the book is open, and at once when the sources change). It folds the inputs into a key: the used stations' merged and own bits, the surroundings, the interface items and the tile types in the box. The list is rebuilt only when the key changes.
- Bench on the stand (`bench p=ModernRecipeBook.RecipeBookSystem.ActiveStationsBench a=false|true n=1000`, MP client, 37 used stations, 716 tile types): inputs unchanged 0.4–0.5 µs, forced rebuild 0.5–0.7 µs, 0 B per call. For scale, the crafting refresh it rides on costs 74 µs.
- Drawing builds no strings per frame: the count and the hovered icon's tooltip are cached.
