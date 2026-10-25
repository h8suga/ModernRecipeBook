# Storage sort: Changed (branch `feature/storage-changed-sort`)

Owner: «в сторейдж добавить сортировку по времени добавления или обновления стака. это было бы очень удобно». Code: `BookSources.TrackChanges`, `SnapshotChanged` / `LoadChanged`, `BookPlayer` (save), `RecipeBookSystem.Sorts.cs` (sort), `RecipeBookSystem.ChangedSuffix` (details panel).

## What is recorded

Per kind as the Storage tab lists it, `(type, prefix)`, the key `BookSources.Listed` aggregates by: the UTC time the kind appeared or its total changed, up or down.

- **Detection.** No source is hooked. Every aggregate rebuild compares each kind's total with the previous rebuild's, so a deposit, a take, a craft, another player or Magic Storage itself all count the same. Rebuilds run only when an input changed (the existing scan cadence), and only while the book is open or a goal is tracked. A change made while the book was closed is stamped when the book next sees it.
- **A different set of containers is a new baseline, not a change.** That covers walking to another base, a network finishing loading through a Portable Access, the first chest mirror snapshot, opening a chest, and a chest another player opens. `SourceSet` hashes the banks, mirror chests and network positions, order-free. On such a rebuild nothing is stamped and nothing is dropped. Kinds out of reach keep their time for when they are back.
- **The first rebuild after entering a world is also a baseline.** Kinds already there have no time. They sort as the oldest (key 0) and show no age; nothing gets "now".
- **A kind that leaves** while the set of containers stays the same (taken out completely) drops its entry.

## Saved

With the player, in `BookPlayer` as `StorageChanged`: a list of `{World, Kinds}`.
- `World` is the world's unique id (`Main.ActiveWorldFileData.UniqueId`, which a multiplayer client gets from the server), so worlds never mix.
- `Kinds` are lines `item|prefix|ticks`, newest first, at most 2,000 per world. Items and prefixes are stored by stable name: vanilla `ItemID` / `PrefixID` names, a mod's `FullName`. A kind whose mod is gone is dropped on load. At most 10 worlds are kept, the most recently changed.
- The current world's times go into the player data on every player save and when the world unloads, and are read back on entering the world.

## Sort

"Changed" / «Изменено» on the Storage tab, "newest first" / «сначала новые» and "oldest first" / «сначала старые». It is appended to `BookSortMode`, which UI state stores by name, and to `StorageSorts`. It goes through the rank path (`PrepareRanks`, `CompareRank`): seconds since 2000, ties by name. Kinds without a time lead "oldest first" and trail "newest first".

## Details panel

While this sort is in effect, the selected stack's meta line ends with its age: "×27 · Terraria · 5 min ago" / «×27 · Terraria · 5 мин назад» (`BookMobEncounters.Ago`). Nothing is shown for a kind without a time. It is not shown under other sorts. The time explains the order, and elsewhere it would only add noise to a full line. No tooltip line was added: the stack's item tooltip already covers the panel when hovered. The suffix is cached per kind, time, minute and language.

## Multiplayer

Tracking runs on the client against what it already receives: the chest mirror and Magic Storage's unit sync. Verified on stands 14 and 15: a take made on the server and a deposit made by the client both re-sort the client's list.

## Cost

Measured on stand 16 with 491 kinds (`bench`): `TrackChanges` takes 28 µs per rebuild and allocates 0 B, so about 45 µs at 800 kinds, and only on rebuilds. `ApplyFilters` under the Changed sort takes 249 µs against 226 µs for Name. Nothing runs per frame besides the cached suffix.
