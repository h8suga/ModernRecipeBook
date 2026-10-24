# Storage cells (branch `feature/storage-cells`)

Owner: «в книге в сторейдже выводить макс кол-во ячеек и сколько занято». Code: `BookSources.BuildCells`, `BookMagicStorage.Cells`, the Storage details panel in `RecipeBookSystem.DrawStorageDetails`.

## The line

The Nearby block on the Storage tab has one more line, under the radius: "Cells: 1,234 / 3,200" / «Ячейки: 1 234 из 3 200» (`N0` in the game's culture, like every count in the book). It counts the containers the tab itself reads, besides the inventory:

- **Magic Storage**, every counted network. Per heart, the same sum MS's own window shows at its bottom ("365 / 880 items", `BaseStorageUIAccessPage`): `NumItems` / `Capacity` over the heart's `TEStorageUnit`s. One cell holds one stack.
  - An **inactive** unit takes no deposits, so neither its capacity nor its stacks count. Here the book differs from MS's window, which counts inactive units too. The tooltip says how many were left out: "Not counted: 1 inactive storage unit".
  - The **Creative Storage Unit** is not a `TEStorageUnit`, so MS leaves it out of its number. The book leaves it out of the total too, and the tooltip says "Creative Storage Unit: unlimited, not in the total", with the item name in the game's language.
- **World chests** in the server's mirror: 40 slots each (`Chest.maxItems`), and the slots holding something.
- **Banks** in range, the **open container** and a carried **Void Bag**: their arrays' length and non-empty slots.

The line is drawn in Caution at 90% full or more, and is hidden when nothing is counted. Hovering it shows one row per source: each network ("Magic Storage · Base" or "Network N", the names the summary's tooltip uses), then "Chests ×N", the open chest and each bank. A cut line repeats itself above the rows.

## When it is computed

Never while drawing. `BookSources.Scan` (every 30 ticks while the book is open or a goal is tracked) reads each network's cells next to its items and folds them, and the network's name, into the scan signature. A unit placed, upgraded or switched off therefore rebuilds the aggregate even when no item changed. `Rebuild` counts chest and bank cells, and the texts are built once per rebuild with the other Nearby texts. The draw path reads cached strings.

Measured on stand 16 (1 network with 4 units and 365 stacks, 2 chests, a Piggy Bank):

| Call | Time | Allocated |
| --- | --- | --- |
| `BookSources.Scan` with cells / without (`Countable` off) | 22.5 / 21.0 µs | 2,880 / 1,608 B |
| `BookSources.BuildCells`, once per rebuild | 1.2 µs | 1,072 B |
| `get_Cells`, `get_CellsTip`, per frame | 0.1 µs | 0 B |

The MS reads go through reflection, so each unit boxes three values. That is about 1.3 KB every half second while the book is open.

## Multiplayer

The client reads what it already has:

- **Magic Storage.** A storage unit's `NetSend` carries its items and `Inactive`, and MS sends every change to all clients (message 86 through `NetHelper.SendTEUpdate`). `Capacity` comes from the unit's tile. Verified on stands 14 and 15: the client's numbers equal the server's after deposits and withdrawals.
- **Chests.** The chest mirror merges stacks per kind, so a client could not tell slots apart. `NearbyChestSync` now carries one more byte per chest, the occupied slot count (`docs/nearby-storage.md` §4.2). Both sides are the same build (the mod is side Both), so nothing older ever reads it.
- **Banks** are the player's own and live on the client.

A network reached only through a Remote Access, with its heart far away, is not counted on a client whose game has not loaded the heart's area. Its items are not counted there either; this predates the cells.

## Binding

`TEStorageUnit.Capacity` and `NumItems` bind as their own group ("cells"). If MS renames them, only the cells' MS part goes quiet, and reading items keeps working.
