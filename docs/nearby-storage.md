# Nearby storage: design (phase 1)

Date: 2026-10-23. Branch `feature/nearby-storage`, cut from `feature/ui-redesign` @ `ecc449d` (1.8.0).
Phase 1 was this design. Phase 2 (implementation) is under way: single player is built and verified on the
stand; the multiplayer part waits for `feature/server-side` (`side = Both`, `BookServerConfig`, MP stand commands).
The owner's decisions after review are folded in below; §12 has the phase 2 status, the measured numbers and the
deviations from this design.

Path roots:

| Root | Path |
|---|---|
| `MOD:` | this worktree at `ecc449d` |
| `TML:` | `<stand>\ref\tml` (decompiled tModLoader 2026.8) |
| `MS:` | `<stand>\ref\MagicStorage-0.7.0.11` |
| `CAL:` | `ModSources\.ref\CalamityMod-2.2.4` |
| prior | `<stand>\docs\chests-ms-research.md` (2026-10-22, client-only era) |

Evidence marks: **[stand]** = measured or seen on stand instance 13 (single player, Calamity 2.2.4 +
MS 0.7.0.11 loaded); everything else is **[code]**, read in the files cited.

---

## 0. The owner's intent and the decisions in one table

> «Чтобы айтемы на расстоянии, вот все storage и сундуков тоже, суммировались, чтобы показывало айтемы.
> Когда предметы не отсортированы, очень сложно лазить постоянно по сундукам, искать. А у нас уже сразу всё
> готово, всё отсортировано, мы просто используем фильтр, поиск и так далее.»

| Question | Decision |
|---|---|
| Source model | One aggregated **source set**: inventory, open container, carried Void Bag, banks in radius, unlocked world chests in radius, every accessible Magic Storage network in radius, plus the MS network the player has open. Every count, the Storage tab, take, craft and the tracker read the same set. |
| Old switch | `UseForCrafting`, `PreferInventory`, the craft-source chip, auto-connect, the network picker and Connect/Disconnect are deleted. No "inventory only" mode: `NearbyStorage = false` (host setting) is that mode. |
| Authority | The server (or the local game in single player) is the only place that removes items from chests and MS. The client never names a chest; it asks for `(type, prefix, amount)` and the server decides where it comes from. |
| Display in MP | World chests: the server pushes a per-chest mirror (deltas, only while the book is open or goals are tracked). MS: MS already syncs every storage unit to every client; the book reads those copies, like MS's own window does. |
| Delivery | Server → client `Give` packet → `Player.GetItem`, overflow to cursor or `QuickSpawnItem`. Client acknowledges; un-acknowledged deliveries go back to their chest/network when the player disconnects. |
| Crafting | Pull the missing ingredients into the inventory first, then the vanilla `Main.CraftItem` loop. Stations = the player's own `adjTile` plus the stations of every MS Crafting Interface in range, applied for the duration of the check and the craft. Results go to the inventory. |
| MS adapter | Rewritten: no MS UI session is ever opened by the book. It reads hearts, units and Crafting Interface stations and withdraws server-side through `TEStorageHeart.Withdraw`. |
| Vanilla crafting column | Not built. Section 6 says what it would take. |
| Radius | `BookServerConfig.NearbyRadius` (10–50, default 30) tiles, circle from the player centre to the container centre. `NearbyStorage` switches the whole feature. The host decides in MP. |
| Deposit (owner, after review) | The Storage tab is one big chest, as in vanilla: Shift+click an inventory item puts the whole stack in; an item on the cursor dropped onto the grid goes in; Deposit All works like a chest's. Placement: onto matching stacks first (nearest container that holds the item: chest or Magic Storage), then the nearest Magic Storage network, then the nearest chest with a free slot; the rest stays with the player. Banks are not targets. No deposit hotkeys (§4.9). |
| Results and stations (owner) | Crafted items always go to the inventory, and the room check covers them. Stations: vanilla adjacency plus Crafting Interface stations only; placed stations in the radius do not count. |

---

## 1. Facts this design rests on

### 1.1 Vanilla and tModLoader

| # | Fact | Evidence |
|---|---|---|
| V1 | `FindRecipes` counts inventory slots 0..57, the Void Vault when a Void Bag is carried and it is not the open container, the one open container (`player.chest` ≥ 0, −2..−5), and every `AddMaterialsForCrafting` result. A recipe is available if `adjTile` covers `requiredTile`, the item counts cover `requiredItem`, and `RecipeLoader.RecipeAvailable` (all `Conditions`) holds. | `TML:Terraria/Recipe.cs:484-525, 596-646`; `TML:Terraria.ModLoader/RecipeLoader.cs:194-197` |
| V2 | tML turns the old `needWater/needLava/needHoney/needSnowBiome/needGraveyardBiome` flags into `Condition`s at load, so conditions are the single environment check. `Condition.NearWater` reads `Main.LocalPlayer.adjWater \|\| adjTile[172]`. | `TML:Terraria/Recipe.cs:15833-15838`; `TML:Terraria/Condition.cs:11, 33, 87` |
| V3 | `Recipe.Create` consumes, per ingredient: inventory 0..57 → open container → Void Vault (bag) → modded providers. It never re-checks materials. In MP it sends msg 32 per touched slot of an open *world* chest only. Bank and inventory changes go to the server via `Main.TrySyncingMyPlayer` (msg 5 per changed slot) on the next update. Create ends with a full `FindRecipes()`. | `TML:Terraria/Recipe.cs:245-340, 297-300, 339`; `TML:Terraria/Main.cs:16846-16849, 17167-17197` |
| V4 | `Main.CraftItem` trusts the last `FindRecipes` result; the click path does not check materials. | `TML:Terraria/Main.cs:40532-40582, 40828-40852` |
| V5 | `ModPlayer.AddMaterialsForCrafting` runs on the local client only and must return live items, because Create edits them in place. | `TML:Terraria.ModLoader/ModPlayer.cs:1413-1424`; `TML:Terraria.ModLoader/PlayerLoader.cs:2093-2103` |
| V6 | A client is authoritative for its own inventory: it ignores msg 5 for itself unless SSC or `HasLockedInventory()`. The server cannot push an item into a normal client's inventory. | `TML:Terraria/MessageBuffer.cs:398-401` |
| V7 | Clients get only chest index/x/y/name from tile sections. Contents arrive on open (msg 31 → 40× msg 32 + msg 33). The server never relays msg 32, so a closed chest's client copy is stale. On clients, slots of a never-opened chest are `null`. | `TML:Terraria/NetMessage.cs:2288-2296, 2506-2523`; `TML:Terraria/MessageBuffer.cs:1894-1947`; `TML:Terraria/Chest.cs:137-142` |
| V8 | The server's msg 31 handler checks neither distance nor lock; only `FindChest` and `UsingChest == -1`. Opening a rigged chest fires wiring. | `TML:Terraria/MessageBuffer.cs:1894-1925` |
| V9 | `Chest.IsLocked(x, y)` covers vanilla locked frames and modded tiles via `TileLoader.IsLockedChest`. `UsingChest(i)` returns the first active player with `chest == i`. `Main.chest` has 8,000 entries; a chest has 40 slots. | `TML:Terraria/Chest.cs:324-352, 708-721, 82`; `TML:Terraria/Main.cs:1250, 1989`; `TML:Terraria.ModLoader/TileLoader.cs:1585-1588` |
| V10 | Vanilla quick stack: 600 px from the chest centre `(x*16+16, y*16+16)`; skips locked chests and chests in use; banks are tile 29 → −2, 97 → −3, 463 → −4, 491 → −5. On MP the server does it (msg 85 → `Chest.ServerPlaceItem`). | `TML:Terraria/Player.cs:20109-20234, 20148-20164`; `TML:Terraria/Chest.cs:368-458` |
| V11 | Money Trough / Chester open the Piggy Bank (−2), the Void Lens projectile opens the Void Vault (−5); they only count as the open container. The Void Bag counts while it is in slots 0..57. | `TML:Terraria/Projectile.cs:68392-68406`; `TML:Terraria/Player.cs:20097-20107` |
| V12 | `Player.GetItem` returns the leftover. Order: ammo slots, occupied slots, hotbar, empty slots, Void Vault. | `TML:Terraria/Player.cs:35589-35663` |
| V13 | `Item.IsTheSameAs` is internal (type + netID, prefix ignored); mods compare `type` themselves. `ItemLoader.CanStack` refuses different prefixes. | `TML:Terraria/Item.cs:51536-51543`; `TML:Terraria.ModLoader/ItemLoader.cs:1892-1907` |
| V14 | Server packets are processed on the main thread (`Netplay.UpdateServerInMainThread` → `CheckBytes`), so one handler call is atomic against other packets and world updates. | `TML:Terraria/Netplay.cs:82-91, 872` |
| V15 | Disconnect runs on the network thread: it sets `Main.player[i].active = false` and calls `PlayerDisconnect` there. Server code that touches `Main.chest` must react on the main thread, not in the hook. | `TML:Terraria/Netplay.cs:350-357, 394-398` |
| V16 | `Mod.HandlePacket` exceptions are swallowed by tML (empty `catch`). | `TML:Terraria.ModLoader/ModNet.cs:694-720` |
| V17 | ServerSide configs are sent to clients on join and on every accepted change, so a client can read `NearbyStorage` / `NearbyRadius`. | `TML:Terraria.ModLoader/ModNet.cs:202-216, 288, 404-411`; `TML:Terraria.ModLoader.Config/ConfigManager.cs:372-399` |
| V18 | `ModSystem.PostUpdateEverything` runs on the server after `UpdateServer`. `ModPacket` ≤ 65,535 bytes; `ItemIO.Send/Receive` carry type, prefix, stack and modded net data. | `TML:Terraria/Main.cs:17658`; `TML:Terraria.ModLoader/ModPacket.cs:81-84`; `TML:Terraria.ModLoader.IO/ItemIO.cs:229-264` |
| V19 | Vanilla stations: `AdjTiles()` scans ±4 × −3..+2 tiles around the feet, applies substitutions (77/302 → 17, 133 → 17+77, 134 → 16, 354/469/487 → 14, 355 → 13+14+alchemy) and `TileLoader.AdjTiles`. It runs only for the local player and only while the inventory is open. | `TML:Terraria/Player.cs:32184-32293, 22302-22305` |

### 1.2 Magic Storage 0.7.0.11

| # | Fact | Evidence |
|---|---|---|
| M1 | **Every client has every storage unit's contents.** A unit change calls `PostChangeContents` → `NetHelper.SendTEUpdate` → msg 86 to all clients. The unit's `NetSend` carries the queued deposit/withdraw ops or a full sync. A client whose count disagrees asks for a full sync. Opening the UI is not needed. | `MS:MagicStorage.Components/TEStorageUnit.cs:576-631, 633-727, 754-759`; `MS:MagicStorage/NetHelper.cs:247-279, 295-313` |
| M2 | A network centre's `NetReceive` deserialises its component list and asks the server for every map section that holds a component, so far-away units (Remote Access) reach the client too. | `MS:MagicStorage.Components/TEStorageCenter.cs:575-585, 729-740`; `MS:MagicStorage/NetHelper.cs:799-818` |
| M3 | `TEStorageHeart.GetStoredItems()` = all items of all units, including inactive units and remote units, and the Creative Storage Unit, which yields every item type at max stack. Module items are not included. | `MS:MagicStorage.Components/TEStorageHeart.cs:169-190`; `MS:MagicStorage.Components/TECreativeStorageUnit.cs:31-68` |
| M4 | Server withdraw = `TEStorageHeart.Withdraw(Item lookFor, bool keepOneIfFavorite)`. It is prefix-exact, broadcasts unit updates, and checks only `SecuritySystem.AccessibleFromContext` (no range check). MS's own client packet ends up in exactly this call. | `MS:MagicStorage.Components/TEStorageHeart.cs:238-379, 766-806`; `MS:MagicStorage.Components/TEStorageUnit.cs:181-258` |
| M5 | MS's client-side result handler assumes an open MS session: `heart = StoragePlayer.LocalPlayer.GetStorageHeart()`, then `heart.netcodeUpdate = true` throws when no session is open (swallowed, V16). Using MS's client withdraw packet without a session therefore relies on a swallowed exception. | `MS:MagicStorage/NetHelper.cs:393-474` (lines 429, 472) |
| M6 | Security: `SecuritySystem.CanPlayerAccessImmediately(Player, int networkID)` is public and works on the server. The access context is a `ref struct`, which reflection cannot use. A heart's network id is the internal field `TEStorageComponent.assignedNetwork`. | `MS:MagicStorage.Common.Systems/SecuritySystem.cs:104-131, 268-283, 1117-1134`; `MS:MagicStorage.Components/TEStorageComponent.cs:35, 49-52` |
| M7 | MS crafting stations live in `TECraftingAccess.stations` (≤ 45 items, synced to clients). MS turns them into `adjTile`, water, lava, honey, shimmer, alchemy and graveyard flags; Snow Biome Emulator and Biome Globe add zones. It applies them by swapping `player.adjTile` and the flags around its own checks and restoring them afterwards. | `MS:MagicStorage.Components/TECraftingAccess.cs:55, 315-335`; `MS:MagicStorage/CraftingGUI.cs:233-294, 2251-2355, 2694-2740` |
| M8 | The heart a client has open is known to the server: opening locks it (`LockOnCurrentClient` → msg `ClientLockStorageHeart`), and the server stores `clientUsingHeart[player]`. (The player byte comes from the packet, not from the sender.) | `MS:MagicStorage.Components/TEStorageHeart.cs:89, 157-167`; `MS:MagicStorage/NetHelper.cs:1308-1339` |
| M9 | A session stays open only while the player is within `lastTileRange` of the access point, the inventory is open and no chest/sign/NPC is open. Remote sessions (Portable Access, Remote Access) follow MS's own reach rule. | `MS:MagicStorage/StoragePlayer.cs:224-263`; `MS:MagicStorage.Items/PortableAccess.cs:101-127` |
| M10 | MS already extends vanilla quick stack to nearby networks (IL edit on `Player.QuickStackAllChests`). | `MS:MagicStorage.Edits/QuickStackILEdit.cs:40, 109-130` |
| M11 | `NetHelper.SendRefreshNetworkItems(Point16, bool, IEnumerable<int>)` and `AuditSystem.ReportItemWithdraw(int, TEStorageHeart, Item)` are public static, so a server-side withdraw can refresh viewers and be audited like MS's own. | `MS:MagicStorage/NetHelper.cs:476-501`; `MS:MagicStorage.Common.Systems.Auditing/AuditSystem.cs:339` |
| M12 | MS's "Use Inventory" module adds the player's inventory to MS crafting counts. The book must never add MS's crafting counts to its own; it reads units only (M3). | `MS:MagicStorage.Modules/UseInventoryModule.cs:15-18` |

### 1.3 Calamity 2.2.4

- Locked modded chests use `ModTile.IsLockedChest` (Astral, Abyss treasure), so `Chest.IsLocked` already excludes them (`CAL:CalamityMod.Tiles.Astral/AstralChestLocked.cs:61`, `CAL:CalamityMod.Tiles.Abyss/AbyssTreasureChest.cs:50`).
- Calamity furniture stations use `ModTile.AdjTiles`, which `TileLoader.AdjTiles` applies (`CAL:CalamityMod.Tiles/FurnitureCommon.cs:797-890`).
- Calamity has no `AddMaterialsForCrafting` provider.

### 1.4 What the current book does (to be replaced)

- **Either/or switch.** `UseForCrafting => CraftingConnected && !PreferInventory` (`MOD:BookStorage.cs:75-79`). It decides between MS counts and inventory counts in:
  - `Owned` (`MOD:RecipeBookSystem.cs:5298-5318`);
  - batch estimates (`:1252-1297`);
  - set tracking (`:1702`);
  - goals (`:1598, 1747-1765`);
  - the resource tracker pools (`:2437-2475, 2529-2536`);
  - craftable (`MOD:BookCrafting.cs:46-97`);
  - craft (`MOD:BookCrafting.cs:165-184`);
  - copy (`MOD:RecipeBookSystem.cs:5377-5381`);
  - the chip (`:4499-4504, 6681-6699`).
- **MS is reached through its UI session.** The book auto-opens an access point (`MOD:BookStorage.cs:1266-1309`), lets the player pick a network (`:1092-1158`), and crafts through `CraftingGUI` (`:707-793`).
- **Withdraw is session-bound.** It goes through `StorageGUI.DoWithdraw` on the session (`:869-911`).
- **The Storage tab exists only with MS installed** (`MOD:RecipeBookSystem.cs:5047`).

**[stand]** Screenshot of the 1.8.0 Storage tab on instance 13:
- a four-button toolbar (Quick Stack · Deposit All · Restock · Deposit Held);
- the details panel "MAGIC STORAGE · Not connected · Nothing in reach";
- the empty grid "Storage items show up here / Walk up to a storage access point";
- "Open Magic Storage" pinned at the bottom.

---

## 2. Source model (Q1)

### 2.1 The source set

| Kind | Where the data comes from (client) | Who removes items | Listed in Storage tab |
|---|---|---|---|
| Inventory 0..57 | `player.inventory` | Recipe.Create / the player | no (counts only) |
| Open container | `Main.chest[player.chest]` (fresh while open, V7) or `bank..bank4` | client, as vanilla (msg 32 for world chests, V3) | yes, "Open chest" or the bank's name |
| Void Vault via carried Void Bag | `bank4` when `useVoidBag() && chest != -5` | client | yes, "Void Vault" |
| Banks in radius | `bank` (29), `bank2` (97), `bank3` (463), `bank4` (491); tile scan on the client | client (player-owned data; synced by vanilla msg 5, V3) | yes, by item name |
| World chests and dressers in radius | single player: `Main.chest` via the local scan. MP: the server-pushed mirror (§4.3). | server | yes, "Chests ×N" |
| MS networks in radius | client copies of hearts/units (M1, M2) | server via `TEStorageHeart.Withdraw` (M4) | yes, "Magic Storage" (×N) |
| MS network open in MS's window | `StoragePlayer.LocalPlayer.GetStorageHeart()` | server (validated by `clientUsingHeart`, M8) | yes, same group |
| Modded `AddMaterialsForCrafting` providers | `PlayerLoader.GetModdedCraftingMaterials` | Recipe.Create | no (counts only, as today) |

**De-duplication**, applied once when the set is built:
- A bank counts once.
- `bank4` is skipped as a "bank in radius" when the Void Bag already counts it or when it is the open container.
- The open bank is skipped as a "bank in radius".
- A mirrored chest whose index is `player.chest` is skipped (the client copy is the fresher one).
- A heart counts once, however many of its access points are in range.

### 2.2 The aggregate

`BookSources` (client, replaces `BookStorage`'s role) builds one aggregate when its inputs change:

- `Counts`: `type → total` (ingredients ignore prefix, V3 `ConsumeForCraft`). `long` totals, clamped to `int.MaxValue` for display.
- `Infinite`: true for every type when an active Creative Storage Unit is in a counted network (M3). Shown as `∞`.
- `ByKind[type]`: per-kind totals and the number of chests holding it, for the breakdown.
- `Items`: one display `Item` per `(type, prefix)` outside the inventory, with `stack` = total. This is the Storage tab list. The Creative Storage Unit is not expanded into 5,000+ cells.
- `Version`: bumped only when the aggregate really changes (signature compare, §8).

### 2.3 Who reads it

| Consumer | Today | New |
|---|---|---|
| Card rows have/need, overlap, ∞ | `Owned` switch (`MOD:RecipeBookSystem.cs:5298`), enumerates `Materials()` per row per frame | `BookSources.Quantity(types)` / `IsInfinite(types)`: dictionary reads |
| Max batches, set tracking | `PreviewCounts` vs `CountPlayerMaterials` (`:1297, 1702`) | `BookSources.Counts` |
| Craftable flag, filters, Used in "craftable" | `FindRecipes` every 20 ticks or MS `IsAvailable` (`MOD:BookCrafting.cs:46-97`) | `BookCrafting.Evaluate()` (§5.4): own pass over `Main.recipe` with the aggregate and the merged stations |
| Tracker cards, compact tracker, ready sound | `goal.Storage` picks a pool; the resource tracker keeps two pools (`:1598, 1752, 2445-2470`) | one pool; `goal.Storage`, `BookResourceNeed.Storage` and the "· storage / · inventory" suffix are deleted |
| Storage tab grid | `BookStorage.Items` (one MS network) | `BookSources.Items` |
| Take | `StorageGUI.DoWithdraw` on the session | §4.5 |
| Craft | inventory loop or `CraftingGUI.Craft` | pull → vanilla loop (§5.5) |

### 2.4 What replaces the switch

Nothing does: there is one set.
- The craft row goes back to `[★][Craft]`, using `BookGeometry.CraftAction`. `CraftSourceChip` and `CraftActionBesideSource` are deleted.
- The Craft tooltip says where materials will come from (§7.6).

**"Inventory only" mode: not needed.** The cases:
- Off for everyone: `NearbyStorage = false`. The book then counts exactly what vanilla counts (V1).
- A co-op partner who wants it off just for themselves: not requested. A client toggle can be added later; it would only filter the set.

---

## 3. Which containers count (Q2)

| Container | Counts? | Rule |
|---|---|---|
| World chests and dressers, including modded chest tiles (`Main.chest` entries) | yes | in radius, not locked, not open by anyone |
| Locked chests (`Chest.IsLocked`, includes `TileLoader.IsLockedChest`) | **no**, not shown | V9; Calamity's locked chests included in the rule |
| Chest open by another player | **no**, not shown, never modified | its owner's client is authoritative for it while open (V7, msg 32 is client→server). It is counted in "Not counted: 1 in use" so the player knows why it is missing. |
| Chest the player has open | yes, as the open container | client data, counted once (§2.1) |
| Piggy Bank, Safe, Defender's Forge, Void Vault tiles | yes, when any tile of it is in radius | player-owned arrays; no server involvement |
| Money Trough, Chester, Void Lens projectiles | only while opened | vanilla rule (V11) |
| Void Bag | while carried | vanilla rule (V1) |
| MS network | yes, when its Storage Heart, Storage Access, Crafting Interface or Remote Access tile is in radius, or it is the network the player has open in MS | the player must have access (M6); password networks are "Not counted: 1 password" |

**Radius.**
- `BookServerConfig.NearbyRadius` tiles (10..50, default 30): a circle, `|playerCenter − containerCenter| ≤ R × 16` px.
- Container centre:
  - chest: `(x*16 + 16, y*16 + 16)`, as vanilla (`TML:Terraria/Chest.cs:407-408`);
  - dresser: `(x*16 + 24, y*16 + 16)` (3×2);
  - MS 2×2 tile: `(x*16 + 16, y*16 + 16)`;
  - bank: any of its tiles' centres `(i*16 + 8, j*16 + 8)` (vanilla `Player.cs:20164`).
- The client uses its own position for banks and MS. The server uses its copy of the player position for chests, and `R + 8` tiles of slack when validating a pull (8 tiles ≈ 6 ticks of 20 px/tick movement).
- Default 30 tiles is inside vanilla quick stack's 37.5 tiles (V10).

---

## 4. Multiplayer protocol (Q3)

### 4.1 Prior recommendation M1, challenged

The prior research's M1:
- the book's Craft sends `Pull(recipe, batches, needs)`;
- the server removes from chests and sends `Give`;
- the client crafts;
- an optional display mirror.

It is kept, with six changes:

1. **Generic pull, not recipe-shaped.** Take, Restock and Craft all send `Pull(id, [(type, prefix|any, amount)])`. The server needs no recipe logic and runs none of `Recipe`.
2. **The server picks the sources.** The client never names a chest index. Its view may be stale, and indices are reused after a chest is destroyed (prior research A.3).
3. **MS goes through the same server operation.** The server calls `TEStorageHeart.Withdraw` itself (M4). MS's own client withdraw packet is not used:
   - its result handler needs an open MS session (M5);
   - one handler call can then take from chests and several networks atomically (V14) and answer once.
4. **Acknowledged delivery.** The server keeps every `Give` until the client's `Ack`. A disconnect before the ack returns the items to their source (§4.6).
5. **Display mirror is required, not optional.** Counts must be right before a craft.
   - Chests use the server mirror.
   - MS needs no mirror: MS keeps every client's unit copies current itself (M1, M2).
6. **Single player runs the same code.** The "server" functions run locally and the mirror is filled without packets. The same code path is exercised in SP tests.

Rejected:
- **M2** (optimistic consumption with callbacks): a shortfall cannot be undone once Create has run (V3, V4).
- **M3** (peeking with msg 31): it fires wiring and marks chests in use (V8).
- **A client-requested chest list**: the client has no contents to ask about.

### 4.2 Packets

One `byte` message id after tML's mod header, handled in `ModernRecipeBook.HandlePacket(reader, whoAmI)` (`TML:Terraria.ModLoader/Mod.cs:540`). "7b" = `Write7BitEncodedInt`.

| Id | Name | Dir | Fields |
|---|---|---|---|
| 0 | `Subscribe` | C→S | `byte on` (1 = send me nearby chests, 0 = stop) |
| 1 | `ChestSync` | S→C | `byte flags` (bit0 Reset = clear mirror first, bit1 MsOk = server's MS adapter works, bit2 PullsOff = SSC server); `ushort skippedLocked`; `ushort skippedInUse`; `ushort upserts` → per chest `short index, short x, short y, byte n`, then n × (`7b type`, `7b prefix`, `7b count`) merged per (type, prefix); `ushort removes` → `short index` each |
| 2 | `Pull` | C→S | `int id`; `byte n` (1..32); n × (`7b type`, `7b prefixPlusOne` (0 = any prefix), `7b amount` (1..999,999)) |
| 3 | `Give` | S→C | `int id`; `byte status` (0 Done, 1 Partial, 2 Nothing, 3 Off, 4 Busy, 5 Invalid); `ushort n` (≤ 256); n × `ItemIO.Send(item, w, writeStack: true)` (V18, keeps modded data) |
| 4 | `Ack` | C→S | `int id` |
| 5 | `Deposit` | C→S | `int id`; `byte n` (1..60); n × `ItemIO.Send(item, w, writeStack: true)`. The server places them (§4.9) and answers with a `Give` holding what did not fit. |

The config change needs no packet: tML broadcasts it (V17). Clients clear the mirror and unsubscribe when `NearbyStorage` turns off; the server stops scanning.

### 4.3 The chest mirror

**Client.**
- `Dictionary<short, MirrorChest>`, where `MirrorChest = (short X, short Y, (int Type, int Prefix, int Count)[] Entries)`.
- Read only by `BookSources`. Never written into `Main.chest`, never returned to vanilla or to `AddMaterialsForCrafting`.
- Cleared on unsubscribe, world unload, feature off, and before a `Reset` sync.

**Subscription.**
- The client subscribes when all of these hold:
  - `NearbyStorage` is on;
  - the player is in a world and active;
  - the book is open, or tracked goals exist (the tracker shows counts while the book is closed).
- It unsubscribes 120 ticks after the last of those stops being true (the hysteresis avoids a snapshot every time the book is toggled).
- Until the first `Reset` sync arrives, `BookSources.Syncing = true`.

**Server, every 30 ticks, only if anyone is subscribed.**
1. One pass over active players builds the set of chest indices that are open (`player.chest ≥ 0`). This replaces 255-player `UsingChest` loops per chest.
2. One pass over `Main.chest`. For each non-null chest, test the distance to each subscriber. A chest in some subscriber's radius gets a content signature: 64-bit fold of x, y and (slot, type, prefix, stack) over 40 slots, computed once per cycle and shared.
3. Per subscriber: eligible = in radius ∧ ¬locked ∧ ¬open. Locked and open ones only increase `skippedLocked/InUse`. Diff against the subscriber's `Sent` map (index → signature): upsert new or changed chests, remove gone ones. Send one `ChestSync` only if something changed, chunked under 60 KB.
4. A new subscriber gets a `Reset` snapshot in the next cycle.

Idle cost on the wire is zero bytes. The signature only drives display: the pull reads the real chest, so a collision can at worst delay a display update.

### 4.4 Server validation of a `Pull`

In order, all on the main thread (V14):

1. `NearbyStorage` on, and `!Main.ServerSideCharacter`; otherwise `Off`.
   - Delivery relies on client-authoritative inventories (V6); an SSC server would discard the client-side `GetItem`.
2. Player active, not dead, not a ghost.
3. Rate limit: token bucket of 16, refilling at 8 per second; ≤ 8 un-acked `Give`s. Otherwise `Busy`.
4. Well-formed:
   - 1..32 entries;
   - `0 < type < ItemLoader.ItemCount`;
   - prefix within `PrefixLoader` range or "any";
   - amount 1..999,999.

   Otherwise `Invalid` (nothing taken; the client is not kicked).
5. Sources, in this order:
   - (a) world chests with centre within `R + 8` tiles of the **server's** copy of the player position, `!IsLocked`, `UsingChest == -1` (this also excludes the requester's own open chest, which the client handles), nearest first;
   - (b) MS hearts with an access tile within `R + 8` tiles, or `clientUsingHeart[sender]` (M8), and `CanPlayerAccessImmediately(player, heart.assignedNetwork)` (M6), nearest access first.
6. Per entry, take up to `amount`:
   - Chests: exact prefix, or for "any" prefix 0 first, then the lowest `item.value` (spares reforged gear when a plain one exists). The slot's stack is decremented, `TurnToAir` at 0. No msg 32, because nobody has the chest open.
   - MS: for "any", read the prefixes present via `GetStoredItems`, then call `heart.Withdraw(new Item(type){prefix, stack}, false)` per prefix until done (M4). Afterwards call `SendRefreshNetworkItems(heart.Position, false, types)` and `AuditSystem.ReportItemWithdraw(sender, heart, item)` (M11).
7. Stop at 256 delivered stacks. Reply `Give` (`Done` / `Partial` / `Nothing`) and record it in flight with each stack's origin (chest index + x, y, or heart position).

**Security relative to vanilla.**
- Vanilla's server lets any client open any chest without range or lock checks (V8). MS's server withdraws for any client without range checks (M4).
- This path checks range, lock, in-use and MS access, so it opens nothing new.
- Chest-protection mods that hook only chest *opening* are bypassed, as the prior research noted. The host has `NearbyStorage` for that case.

### 4.5 Concurrency and delivery

**Two players pulling from the same chest.** Handlers run one after another (V14). The second sees what the first left and gets `Partial` or `Nothing`. No locks are needed.

**A chest opened by someone mid-flight.**
- The check happens at removal time on the server.
- If the open request is processed after the removal, the opener receives the fresh contents (V7).

**Client on `Give`:**
1. For each item: `Main.LocalPlayer.GetItem(myPlayer, item, InventoryUIToInventorySettingsShowAsNew)` (V12).
2. Leftover: onto the cursor if it is empty, else `QuickSpawnItem` (synced world item, picked up again).
3. Send `Ack(id)` in a `finally`, so it goes out even if a hook throws mid-way.
4. If `id` is the pending craft, continue the craft (§5.5).

**Inventory room is checked before asking** (§5.5 step 3), so overflow only happens if the inventory changed during the round trip.

### 4.6 Disconnect mid-request: no loss, no duplicate

| Moment | Outcome |
|---|---|
| Client leaves before the server handles the `Pull` | Nothing was taken. |
| Server took the items; client leaves before applying `Give` | No `Ack` arrives. In `PostUpdateEverything` (main thread; V15 forbids doing it in `PlayerDisconnect`) the server sees `!Main.player[p].active` with entries in flight. Each stack goes back to its origin: the same chest if it still exists at x,y (stack onto an equal item, else an empty slot), or the heart via `heart.DepositItem`. Leftover is dropped as a world item at the origin with `Item.NewItem` (`TML:Terraria/Item.cs:51785-51832`). |
| Client applied `Give` and the `Ack` reached the server | The entry is dropped; the items are the client's. |
| Client applied `Give`, then crashed before the `Ack` was processed | Restored, and the client's unsaved inventory is lost with the crash, so there is still exactly one copy. |
| Client applied `Give` and quit cleanly within one server frame (≈ RTT/2 + 16 ms) of receiving it | Theoretical duplicate. A human Save & Exit takes longer than that, so this is accepted. |
| Server shuts down with entries in flight | On `OnWorldUnload` the server restores them first (worst case is the duplicate case above). |

A malicious client could withhold `Ack` and leave to duplicate a pull. Without SSC a client can already edit its own inventory freely (V6), so this adds no new capability; with SSC pulls are off.

### 4.7 Interaction with vanilla messages

- **msg 31 is never sent.** No open sound, no in-use marking, no rigged-chest wiring (V8).
- **msg 32 is not needed for server-side removals.** For the requester's own open chest, the client edits the slot and sends msg 32 like `Recipe.Create` does (V3).
- **msg 33 / msg 80** keep `player.chest` current on the server, which `UsingChest` reads (`TML:Terraria/MessageBuffer.cs:1994-2006`).
- **msg 34 (chest placed or killed)** shows up as a null or moved entry in the next cycle: the mirror entry is removed or replaced (its key includes x,y).
- **Quick stack (msg 85)** keeps working and simply changes chest contents, which the next cycle reports.

### 4.8 Rate limits and sizes

- **Pulls and deposits:** 32 burst, 16/s (vanilla lets a player Shift+click as fast as they like), ≤ 8 unacknowledged replies with items, ≤ 32 pull entries, ≤ 256 stacks per reply, ≤ 60 items per deposit.
- **Mirror:** cadence fixed at 30 ticks; a subscribe toggle costs one snapshot.
- **Client side:** one pull per gesture. Craft and take buttons are disabled while a pull is pending (no double submission).

### 4.9 Deposit: the Storage tab as one big chest (owner, after review)

> «давать ещё возможность закидывать предметы, так же, как и в сундук… Shift — весь стак сразу, курсором
> переносишь и складываешь в хранилище, а оно само решает, в какой сундук закинуть»

**Gestures**, copied from vanilla's open-chest behaviour (`TML:Terraria.UI/ItemSlot.cs`):

| Gesture | Vanilla with a chest open | Storage tab |
|---|---|---|
| Shift+click an inventory, coin or ammo slot | the whole stack into the chest; never a favorite (`ItemSlot.cs:387-424` sets cursor 9 only for `!favorited`; `OverrideLeftClick` runs `PlayerLoader.ShiftClickSlot` first, `:572`) | the whole stack into nearby storage, through `ModPlayer.ShiftClickSlot`; same contexts (0, 1, 2), same favorite rule, the same "into the chest" cursor (`HoverSlot` sets `Main.cursorOverride = 9`) |
| Left click a chest slot holding an item | the cursor stack goes into the slot; a favorite loses its mark (`ItemSlot.cs:958-961`) | the cursor stack dropped anywhere on the grid goes in; the mark is cleared the same way |
| Right click a chest slot with an item held | takes one from the slot to the cursor, never puts one in (`ItemSlot.cs:1383-1420`) | right click stays "take one"; nothing put in |
| Deposit All | slots 10..49 except favorites; coins from every slot only into a container that already has coins (`Terraria.UI/ChestUI.cs:456-500, 1025-1110`) | the same slot set and the same coin condition (coins are not exchanged into higher coins) |

A player with a chest of their own open uses that chest (vanilla's path); the book's Shift+click steps aside.
With a Magic Storage window open, MS's own `ShiftClickSlot` runs first (it loads first) and deposits into that
network. There are no deposit hotkeys (owner: «Ctrl+D отдельной комбинацией супер неудобно»).

**Placement rule** (server or single player, per item, `BookNearbyServer.Deposit`):
1. Onto stacks of the same item, nearest container first: a chest that holds it (vanilla's quick-stack rule, `Chest.PutItemInNearbyChest`, `TML:Terraria/Chest.cs:392-458`: fill its stacks, then one of its free slots), or a Magic Storage network that holds the type (`heart.DepositItem`).
2. The rest into the nearest Magic Storage network.
3. Else the nearest chest with a free slot.
4. What does not fit comes back: "Nearby storage is full: 50 left" / «Рядом нет места: осталось 50».

Chests follow the pull rules: in range (server position + slack), not locked, open by nobody. Banks are not
targets: a player does not expect items to land in a Piggy Bank they did not open.

**Multiplayer.** As in MS's own deposit (`MS:MagicStorage.Components/TEStorageHeart.cs:693-703`), the items leave
the client's slots when it sends them; the server places them and returns the leftover in a `Give`. One handler
call does it all (V14). An exception keeps what was already placed (it is in storage) and returns the rest. A
leftover the client never acknowledges drops where the player stood when the player leaves (§4.6). The chest mirror
and Magic Storage's own sync carry the change to every client.

---

## 5. Magic Storage, single player and multiplayer (Q4)

### 5.1 Enumerating networks

**Client: one tile pass per refresh (§8) over the `(2R+1)²` box around the player.** One pass serves both:
- bank tiles (29/97/463/491);
- MS access tiles: `CraftingAccess`, `StorageAccess`, `StorageHeart`, `RemoteAccess` top-left frames, the same filter as `MOD:BookStorage.cs:957-988`.

For each access tile in radius:
- `heart = StorageAccess.GetHeart(x, y)` (virtual; Remote Access resolves its locator, `MS:MagicStorage.Components/RemoteAccess.cs:22`);
- keep it if `heart.CanLocalClientAccess()` (M6), else count it as "password";
- a Crafting Interface also contributes its `stations` (§5.4).

Add `StoragePlayer.LocalPlayer.GetStorageHeart()` (and its `GetCraftingAccess()` stations) when the player has MS open. This covers Portable Access, Portable Crafting Access and far Remote Access sessions. MS keeps them only while its reach rule holds (M9).

**No picker and no connect state.** A network is in the set or not.

**Server:** the same tile pass around the server's player position with `R + 8`, done per `Pull`, plus `clientUsingHeart[sender]`.

### 5.2 Reading contents

| Mode | Source |
|---|---|
| SP | `heart.GetStoredItems()` (M3) |
| MP client | the same call on the client's copies, which MS keeps current for every client (M1, M2). This is exactly what MS's own storage window reads (`MS:MagicStorage/StorageGUI.cs:637-649`). |

Change detection is a per-heart signature over its units' items, computed every 30 ticks while the book needs counts.

### 5.3 Withdraw

Server or SP only: `TEStorageHeart.Withdraw` (§4.4 step 6). This is the same function MS's packet handler calls (M4). The book adds the range and access checks that MS's handler does not have. Deposits: §4.9.

### 5.4 Crafting with MS stations

**Stations = the player's own `adjTile` and flags ∪ the stations of every counted Crafting Interface.** A Crafting Interface counts when it is in radius on an accessible network, or belongs to the open session.

`BookMagicStorage.CraftingEnvironment()` folds the station items the way MS does (M7, `MS:MagicStorage/CraftingGUI.cs:2251-2355`):
- `createTile` → `adjTile`, with vanilla's substitutions (V19) and `TileLoader.AdjTiles`;
- `CountsAsWater/Lava/Honey/ShimmerSource`;
- buckets 206/3031, 207/4820, 1128/5302;
- Alchemy Table → `alchemyTable`;
- tile 85 → graveyard;
- MS's Snow Biome Emulator and Biome Globe → zones;
- `adjTile[CraftingAccess]`.

**Applying it:** a disposable scope swaps `player.adjTile` to the merged array and ORs `adjWater/adjLava/adjHoney/adjShimmer/alchemyTable/ZoneSnow/ZoneGraveyard`, then restores them. This is MS's own pattern (`CraftingGUI.cs:2694-2740`). It is used around:
- the availability pass (conditions read those player fields, V2);
- the craft loop, where alchemy's ingredient saving reads `alchemyTable` (`TML:Terraria/Recipe.cs:41-46`).

After the loop, one plain `Recipe.FindRecipes()` restores vanilla's own list (V3).

**MS features left out:**
- *Third-party `EnvironmentModule.ModifyCraftingZones`:* no built-in module uses it.
- *MS recursive crafting:* MS default is off (`MS:MagicStorage/MagicStorageConfig.cs:57, 141`), and the book's rows only count direct ingredients.
- *Journey "infinite items" module:* it is a module, not storage (M3).

### 5.5 A craft with ingredients spread over everything: the exact sequence

Example: ×5 batches, ingredients partly in the inventory, a bank, two chests and two networks.

1. **Preconditions** (as today, `MOD:BookCrafting.cs:244-252`): player ready, cursor and guide slot empty, no pull pending. The recipe is available per `Evaluate()` (§2.3).
2. **Plan** (`BookSourceRules.Plan`, pure, extends `BookBatchPlanner`'s max-flow so it returns the per-type flow):
   - Needs are `ingredient.stack × batches` per requirement, with the accepted types (type ∪ its recipe groups, as `MOD:RecipeBookSystem.cs:1271-1293`).
   - Allocate in layers:
     - L0 Create-visible: inventory, open container, Void Bag, modded providers. Nothing moves.
     - L1 banks in radius: moved locally.
     - L2 remote: mirror chests + MS. Pulled.
   - Each layer gets only what the previous layers could not cover.
   - L2 entries use "any prefix" (vanilla consumes any prefix, V3).
3. **Room check** (`BookSourceRules.SlotsNeeded`): free room for L1 + L2 per type = capacity left in partial stacks of that type, then empty slots 0..49 (plus Void Vault slots if the bag is carried, V12). This is conservative, because ammo and coin slots are ignored. If it does not fit, no pull is made and the message is "Inventory full: free N slots".
4. **L1:** move bank stacks into the inventory locally (`GetItem`). Vanilla msg 5 syncs both arrays (V3).
5. **L2 empty:** go to step 7.
6. **L2:** send `Pull`, remember `(id, recipe, batches)`. After 30 ticks the button shows "Gathering materials…" (no flash for fast replies). A 10 s timeout clears the pending state; items arriving later still land in the inventory.
7. **On `Give`** (immediately in SP): run the vanilla loop for up to `batches` inside the station scope:
   - before each batch, check the layer-0 counts and the conditions;
   - `Main.CraftItem(recipe)` (V3, V4);
   - move the result from the cursor into the inventory (the existing loop, `MOD:BookCrafting.cs:188-232`).
8. **Stop early** with "Crafted 3/5 · Torch · Stopped: out of materials" if a partial reply left too little.
9. **After the loop:** close the scope, call `Recipe.FindRecipes()` once, and set the message.

**Results always go to the inventory.** Before this change, crafting through MS put them into storage; this is open question 2.

**Stations that exist only in a Crafting Interface** are covered by the scope in step 7, for both availability and the craft. The capability of today's MS crafting (MS stations, MS environment items) is kept. It is extended to ingredients from every source.

### 5.6 Hiding a session the player opened

If the player opened MS's window and then opens the book, the book keeps doing what it does today:
- removes MS's interface layers;
- quiets its mouse input;
- unfocuses its text input.

This is `MOD:BookStorage.cs:482-526, 588-598` and `MOD:RecipeBookSystem.cs:4848-4855`, gated by "book open ∧ MS window open". The book never opens or closes MS sessions any more:
- `Open`, `ConnectNearby`, `AutoConnect` and `PostUpdateEverything` close are deleted;
- the `BookOpened` / `HandOff` bookkeeping is deleted.

---

## 6. Vanilla crafting column (Q5): not built

What it would take: implement `ModPlayer.AddMaterialsForCrafting`. Vanilla's menu, `FindRecipes` and `Recipe.Create` would then see the items (V1, V3, V5).

| Source | Possible? |
|---|---|
| Banks in radius | yes, in SP and MP: player-owned arrays returned live; vanilla msg 5 syncs the consumption (V3) |
| World chests, SP | yes: return the live `Main.chest[i].item[k]` objects |
| World chests, MP | no: the client copy is stale (V7) and Create consumes before any packet could reach the server (V4). Duplicates. |
| Magic Storage | no in both modes: consuming MS items in place bypasses each unit's metadata, its net op queue and its sync (`MS:MagicStorage.Components/TEStorageUnit.cs:43-49, 754-759`). |

Recommendation: do not build it.
- Inconsistent: SP and MP would show different numbers in vanilla's column.
- Unnecessary: the book's Craft button already covers every source.

If ever wanted, banks only (both modes), in a separate change.

---

## 7. UI (Q6)

The book's visual language stays: palette, `Box`/`CardButton`/`Dropdown`/`EmptyState`, `MouseTip`, sizes (`MOD:docs/ui-redesign.md` §2–3).

**UX rules applied** (ui-ux-pro-max `ux` domain results):

| Guideline | Applied as |
|---|---|
| Empty states give a message and an action | every empty grid has a title and one hint |
| Loading feedback without flashing for near-instant work | "Syncing", "Taking…" and "Gathering materials…" appear only after 30 ticks, like today's `ShowSyncAfter` (`MOD:BookStorage.cs:63-67`) |
| Loading buttons prevent double submission | Craft and take are disabled while a pull is pending |
| Error clarity: cause + fix | "Inventory full: free 2 slots" |
| Colour not only | the pill keeps its dot plus a word |
| Progressive disclosure | the per-source breakdown appears only for the selected item and in the cell tooltip; the "Not counted" line appears only when non-zero |

### 7.1 Storage tab layout

- **Tab.** Always visible: `StorageTabVisible` becomes `true`, because chests count without MS. Players without MS will see Ctrl+4 move from Recent to Storage.
- **Toolbar.** Vanilla's chest buttons in vanilla's order, minus Loot All: `[Deposit All] [Quick Stack] [Restock]` (owner). Deposit Held is gone: dropping from the cursor replaces it.
- **Grid.** `BookSources.Items` with the existing search, mod filter, categories, class filter, stage filter, sort and favourites (`MOD:RecipeBookSystem.cs:3747-3826`).
- **Details panel**, top to bottom:
  1. Section label + pill.
  2. Summary line, with a tooltip.
  3. Radius line.
  4. The "Not counted" line, only when non-zero, with a tooltip.
  5. Divider.
  6. The selected stack (icon, name, `×total · Mod`) and its breakdown rows.
  7. The last action message.
- **Removed:** the network dropdown, the Connect/Disconnect/Enter Password button and "Open Magic Storage".

### 7.2 Gestures (unchanged, `MOD:BookStorageGesture.cs`)

| Gesture | Action |
|---|---|
| Left click | select |
| Right click | take one |
| Shift + left | take a stack: `min(maxStack, total)` |
| Alt + left | favourite |

**Take order:**
1. open container;
2. Void Vault (bag);
3. banks in radius;
4. the rest via `Pull` (exact prefix).

The result goes into the inventory, and the rest onto the cursor (as today, `MOD:BookStorage.cs:869-911`).

**Restock:**
- for every inventory stack 0..57 below its max stack, except coins, top it up from the same order;
- MS's Restock did the same but included coins (`MS:MagicStorage/StorageGUI.cs:489-517`);
- pulling every coin out of the Piggy Bank would surprise players, so coins are skipped.

**Quick Stack** calls `Main.LocalPlayer.QuickStackAllChests()`. That is the inventory's own button: vanilla's radius, server-side in MP, and extended to networks by MS (V10, M10).

**Putting items in:** §4.9 (Shift+click in the inventory, the cursor dropped onto the grid, Deposit All).

**Hotkeys:**
- Ctrl+Q and Ctrl+R stay;
- Ctrl+D and Ctrl+Shift+D are removed (`MOD:BookHotkeys.cs:120-137`); the owner wants no deposit keys.

### 7.3 Strings (EN Title Case for buttons; RU sentence case)

**Details panel**

| Where | EN | RU |
|---|---|---|
| Section label | NEARBY | РЯДОМ |
| Pill: ready | Ready | Готово |
| Pill: first snapshot > 0.5 s | Syncing | Синхронизация |
| Pill: off | Off | Выключено |
| Summary (kinds with 0 omitted) | 7 chests · 2 storages · 1 bank | 7 сундуков · 2 хранилища · 1 банк |
| Summary, nothing | Nothing nearby | Рядом ничего нет |
| Summary tooltip | Piggy Bank, Safe<br>Magic Storage: Base, Network 2 | Свинья-копилка, Сейф<br>Magic Storage: Base, Сеть 2 |
| Radius line, SP | Within 30 tiles | В радиусе 30 блоков |
| Radius line, MP | Within 30 tiles · set by the host | В радиусе 30 блоков · задаёт хост |
| Not counted | Not counted: 2 locked · 1 in use · 1 password | Не учтены: 2 заперты · 1 занят · 1 с паролем |
| Not counted tooltip | Locked chests and chests another player has open are skipped.<br>A storage with a password counts once you join it in Magic Storage. | Запертые сундуки и сундуки, открытые другим игроком, пропускаются.<br>Хранилище с паролем учтётся, когда вы войдёте в его сеть в Magic Storage. |
| MS adapter failed | Magic Storage support is off (see client.log) | Связь с Magic Storage отключена (client.log) |
| MS version unbindable | Magic Storage {v} is not supported yet | Magic Storage {v} пока не поддерживается |

Notes on the details panel:
- Bank names come from the game's item names (`Lang.GetItemNameValue`), so they are already localized.
- An unnamed network is labelled "Network N" / «Сеть N», numbered by heart position as today (`MOD:BookStorage.cs:1122-1133`).
- Plurals:
  - сундук / сундука / сундуков;
  - хранилище / хранилища / хранилищ;
  - банк / банка / банков;
  - слот / слота / слотов;
  - блок / блока / блоков (the existing rule, `MOD:BookStoragePresentation.cs:72-81`, moves to `BookSourceRules`).

**Selected item and tooltips**

| Where | EN | RU |
|---|---|---|
| Breakdown rows | Inventory: 12 · Open chest: 40 · Piggy Bank: 5 · Chests ×3: 120 · Magic Storage: 40 (×2 when two networks) · ∞ for creative | Инвентарь: 12 · Открытый сундук: 40 · Свинья-копилка: 5 · Сундуки ×3: 120 · Magic Storage: 40 |
| No selection (existing) | No item selected / Click an item in the grid | Предмет не выбран / Нажмите на предмет в сетке |
| Cell tooltip, extra line (via `BookTooltipShortcut.ModifyTooltips`) | Chests ×3: 120 · Magic Storage: 40 | Сундуки ×3: 120 · Magic Storage: 40 |
| Stack tooltip (existing) | Shift+Click: take a stack · Right-click: take one<br>Alt+Click: favorite | Shift+клик: взять стопку · ПКМ: взять одну<br>Alt+клик: в избранное |

**Toolbar**

| Where | EN | RU |
|---|---|---|
| Quick Stack button | Quick Stack | Быстрый стак |
| Quick Stack tooltip | Quick stack to nearby chests · Ctrl+Q (with MS: "…chests and storages…") | Сложить в сундуки рядом · Ctrl+Q (с MS: «…в сундуки и хранилища…») |
| Restock button | Restock | Пополнить |
| Restock tooltip | Top up inventory stacks from nearby · Ctrl+R | Дополнить стопки в инвентаре из того, что рядом · Ctrl+R |
| Restock disabled tooltip | Nearby storage is off | Хранилища рядом выключены |

### 7.4 Empty and off states (grid centre, `EmptyState`)

| State | Title EN / RU | Hint EN / RU |
|---|---|---|
| Off, single player | Nearby storage is off / Хранилища рядом выключены | Turn it on in the mod settings / Включите в настройках мода |
| Off, multiplayer | Nearby storage is off / Хранилища рядом выключены | The host turned it off / Хост выключил эту функцию |
| SSC server | Not available on this server / Недоступно на этом сервере | The server keeps inventories itself / Сервер сам хранит инвентари |
| Syncing (> 0.5 s) | Syncing… / Синхронизация… | — |
| Nothing in radius | Nothing nearby / Рядом ничего нет | Chests and storages within 30 tiles show up here / Здесь появятся сундуки и хранилища в радиусе 30 блоков |
| Containers empty | Nearby containers are empty / Рядом всё пусто | — (the "Not counted" line explains exclusions) |
| Filtered out (existing) | Nothing found / Ничего не найдено | Ctrl+Backspace resets filters / Ctrl+Backspace сбрасывает фильтры |

The off states name the config option. Its label belongs to `feature/server-side`; proposed "Nearby Storage" / «Хранилища рядом».

**Not carried over:**
- Deposit Held: dropping the cursor stack onto the grid does it (owner).
- Open Magic Storage: the player right-clicks the access point.

**Toolbar and grid strings** (owner):

| Where | EN | RU |
|---|---|---|
| Deposit All button | Deposit All | Сложить всё |
| Deposit All tooltip | Deposit to nearby storage, except hotbar and favorites<br>Onto matching stacks first, then Magic Storage, then a free chest | Сложить в хранилища рядом, кроме панели и избранного<br>Сначала к таким же, потом Magic Storage, потом сундук |
| without Magic Storage installed | …<br>Onto matching stacks first, then a free chest | …<br>Сначала к таким же, потом в свободный сундук |
| nothing to deposit into | Deposit to nearby storage<br>Nothing nearby to deposit into | Сложить в хранилища рядом<br>Рядом некуда складывать |
| Storage cell tooltip, last line | Shift+Click an inventory item or drop it here: put into storage | Shift+клик по предмету в инвентаре или перенос сюда — сложить в хранилище |

### 7.5 Messages (Storage tab message line)

| Event | EN | RU |
|---|---|---|
| taken (existing) | Taken to inventory | Забрано в инвентарь |
| overflow (existing) | Inventory full: rest on cursor | Инвентарь полон: остаток на курсоре |
| cursor busy (existing) | Clear the cursor first | Сначала уберите предмет с курсора |
| partial | Took 30 of 50: the rest is gone | Взято 30 из 50: остального уже нет |
| nothing | No longer nearby | Этого уже нет рядом |
| pending > 0.5 s | Taking… | Забираю… |
| 10 s timeout | No reply from the server: try again | Сервер не отвечает: повторите |
| Busy | Too fast: wait a moment | Слишком часто: подождите |
| restock done (existing) | Stacks topped up | Стопки пополнены |
| restock nothing | Nothing to top up | Нечего пополнять |
| quick stack | Stacked into nearby chests | Сложено в сундуки рядом |
| deposit done | Stored nearby | Сложено рядом |
| deposit, no room for all | Nearby storage is full: 50 left | Рядом нет места: осталось 50 |
| deposit, nothing to put into | Nothing nearby to deposit into | Рядом некуда складывать |
| Deposit All, nothing to move | Nothing to deposit | Нечего складывать |

Messages fade after five seconds.

### 7.6 Craft row, card rows and tracker

| Where | EN | RU |
|---|---|---|
| Craft tooltip line 1 (existing) | Ready to craft | Можно создать |
| Craft tooltip line 2, when anything besides the inventory is needed | From: inventory, 2 chests, Magic Storage | Откуда: инвентарь, 2 сундука, Magic Storage |
| pending > 0.5 s | Gathering materials… | Собираю материалы… |
| no room | Inventory full: free 2 slots | Инвентарь полон: освободите 2 слота |
| pulled but not crafted | Materials are in the inventory; craft stopped: {reason} | Материалы в инвентаре; крафт остановлен: {причина} |
| stopped early (existing pieces) | Crafted 3/5 · Torch · Stopped: out of materials | Создано 3/5 · Факел · Остановлено: кончились материалы |
| Material row tooltip (replaces "In storage" / "Have") | Have 132, need 10 (×5)<br>Inventory: 12 · Chests ×3: 120 | Есть 132, нужно 10 (×5)<br>Инвентарь: 12 · Сундуки ×3: 120 |
| Tracking status while syncing (replaces "Storage unavailable") | Syncing… | Синхронизация… |

Removed copy:
- "Ready · result goes to storage";
- "From storage / From inventory" (the chip);
- "Inventory / Storage" (`TrackingSource`);
- " · storage / · inventory" (compact tracker).

In `docs/ui-redesign.md` §1, the "crafting source" row becomes "nearby containers: Nearby / Рядом".

### 7.7 Help (F1)

The STORAGE section drops Ctrl+D and Ctrl+Shift+D and loses its `StorageOnly` flag (`MOD:BookHelp.cs:59-67`). It lists:
- Shift+LMB: take a stack
- RMB: take one
- Ctrl+Q: quick stack
- Ctrl+R: restock
- Shift+LMB in the inventory: put a stack in
- LMB holding an item: put the held item in

---

## 8. Performance budget (Q7)

**[stand] measurements**, SP, Calamity + MS loaded:

| Measurement | Result |
|---|---|
| `Recipe.numRecipes` | 5,015 |
| `Recipe.FindRecipes()` | 301.2 µs, 765 B per call (20 calls) |
| `Chest.FindChest` miss, a full pass over 8,000 `Main.chest` entries | 10.1 µs |
| `TileEntity.ByID.Count` in the stand world | 0, so MS costs below are estimates |

### Client

| Work | When | Estimate |
|---|---|---|
| Tile pass `(2R+1)²` = 10,201 tiles at R = 50 (banks + MS access tiles) | every 30 ticks while counts are needed | 30–60 µs |
| Local signature (≤ 58 + 40 + 40 + 160 slots) | every 10 ticks | < 5 µs |
| MS signature, per heart: iterate stored items (2,000 stacks); `GetStoredItems` is LINQ, a few KB garbage | every 30 ticks | 20–40 µs |
| Aggregate rebuild (reused dictionaries) | only when a signature or a `ChestSync` changed | ~100 µs for ~3,000 stacks |
| `Evaluate()` over `Main.recipe`; overlapping requirements, precomputed once per recipe, get the exact flow check | every 20 ticks while the book is open (same cadence as today's `FindRecipes`), plus on aggregate change | ≈ `FindRecipes` |
| Per frame | material rows, grid and tracker read the aggregate | dictionary reads only; no item enumeration. Today `Owned` enumerates `Materials()` per row per frame (`MOD:RecipeBookSystem.cs:5298-5318`). |
| Batch craft ×N | vanilla `Create` calls `FindRecipes` per batch (V3) | ×100 ≈ 30 ms once. The per-batch check drops today's extra `Refresh(force)` → `FindRecipes` (`MOD:BookCrafting.cs:200`), halving it. |

Evaluate fast path: per ingredient, the sum of accepted counts ≥ stack, as vanilla's `CollectedEnoughItemsToCraftRecipeNew` does (`TML:Terraria/Recipe.cs:564-583`). Only recipes with overlapping requirements get the flow check.

**Book open, nothing changing: ≈ 15–20 µs per tick, amortised.** Book closed with no goals: zero (no subscription, no scans).

### Server, per subscribed player

| Work | Estimate |
|---|---|
| Shared per 30-tick cycle: one `Main.chest` pass (≈ 10 µs) + 8,000 distance tests per subscriber (≈ 10 µs) + signatures of chests in radius (50 chests × 40 slots ≈ 10 µs) | ≈ 30 µs per player per 0.5 s ≈ 1 µs/tick |
| `Pull`: one tile pass (≈ 50 µs) + removals | ≤ 8/s |
| No subscribers | zero |

### Network

| Case | Size |
|---|---|
| Idle | 0 B/s |
| Snapshot on subscribe: 9 B per chest header + ~5 B per distinct item; 30 chests × 20 kinds | ≈ 3.3 KB once |
| One chest changing | ≈ 110 B per cycle, < 250 B/s even with someone shuffling items |
| Walking through a 100-chest room | ≈ 10 KB burst |
| `Pull` | 9 + ~6 B per entry |
| `Give` | ~10 B + modded data per stack |
| `Ack` | 5 B |
| MS | adds nothing beyond MS's own unit sync, which happens for every MS withdrawal anyway (M1) |

**Phase 2 measures and reports:**
- harness `bench` on `BookSources.Rebuild`, `BookCrafting.Evaluate` and the server cycle (client and server instances);
- bytes per message from tML's net diagnostics (`ModNetDiagnosticsUI.CountReadMessage`, `TML:Terraria.ModLoader/ModNet.cs:717-719`);
- GC bytes per call.

---

## 9. Migration (Q8)

### Delete

| File / item | Why |
|---|---|
| `BookStorageSnapshot.cs` | MS crafting-cache counts |
| `BookStorageConfig.cs` and its `Localization/*.hjson` block | `AutoConnect` has no meaning without connections. The old config file on disk is ignored by tML. |
| From `BookStorage.cs`, the session and crafting code: `CraftingGUI` / `StorageGUI` / `MagicUI` refresh bindings, `Tick`, `QueueCraft` / `FinishCraft`, `Deposit*`, `InventoryAction`, `Withdraw`, `RefreshStorage`, `Disconnect`, `Nearest`, `Networks` / `PickNetwork`, `ConnectNearby`, `AutoConnect`, `ChooseCraftSource`, `PreferInventory`, `UseForCrafting`, `CanChooseSource`, `BookOpened`, `HandOff` | replaced by `BookSources` and `BookMagicStorage` |
| From `BookStoragePresentation.cs`, all except the plural rule | the rule moves to `BookSourceRules` |
| From `BookRecentCraftHooks.cs`, `BeginStorageCraft` / `EndStorageCraft` and the achievement observer | every craft is now `Main.CraftItem`, which the existing hook records |

From `RecipeBookSystem.cs`:
- `NetworkMenu`, `NetworkPickerShown`, `StorageRows`, `StorageAction`, `CurrentNetworkName`, `NetworkOption`, `DrawNetworkMenu`;
- `CloseForStorage`, `SourceShown`, `DrawCraftSource`, the chip click;
- the Connect/Native clicks;
- `CountPlayerMaterials`, `goal.Storage`, `goal.Unknown` (it becomes Syncing).

Elsewhere:
- `BookResourceNeed.Storage` and the two-pool logic in `BookResourceTrackerRules`;
- `BookGeometry.CraftSourceChip`, `CraftActionBesideSource`, `StorageConnect`, `StorageNative`;
- `BookHelp.Section.StorageOnly`;
- `BookHotkeys` DepositCursor and DepositAll.

### Add (four files)

| File | Contents |
|---|---|
| `BookSources.cs` | client: aggregate, mirror, local containers, take / restock / quick stack, plan → pull → `Give` handling, `Syncing`, summary and breakdown strings |
| `BookSourceRules.cs` | pure rules: range, centres, eligibility, de-duplication, layered plan (on `BookBatchPlanner`), slots needed, prefix order, RU plurals, and `SelfCheck()` (callable from the harness) |
| `BookNearbyServer.cs` | `ModSystem`, server and SP: subscriptions, cycle, `Pull` handling, in-flight restore, packet codec |
| `BookMagicStorage.cs` | reflection adapter: access tiles → hearts, stored items, creative flag, Crafting Interface stations and the craft environment scope, session heart and its hiding; server side: `Withdraw`, `DepositItem` (restore), `SendRefreshNetworkItems`, `AuditSystem`, `clientUsingHeart`, `CanPlayerAccessImmediately`, `assignedNetwork`. It binds on the dedicated server too (today `PostSetupContent` returns on `dedServ`, `MOD:BookStorage.cs:375`). Delegates are cached. |

### Change

| File | Change |
|---|---|
| `BookCrafting.cs` | `Evaluate` / `IsAvailable` / `Reason` / `TryCraft` as in §5.5; `Materials()` stays (layer 0) |
| `RecipeBookSystem.cs` | all `BookStorage.*` call sites (§1.4 list) |
| `RecipeBookSystem.Variants.cs:100` | the same |
| `ModernRecipeBook.cs` | `HandlePacket` |
| `docs/ui-redesign.md` | concept row |

### Unchanged

- `BookStorageGesture.cs`.
- `BookUiState`: no version bump. The `storage` tab filters keep their key (`MOD:BookUiState.cs:47`). No MS state was ever saved there.
- Tracked goals are not saved (`MOD:BookPlayer.cs:193-215`), so `goal.Storage` needs no migration.

**`BookStorageConfig.AutoConnect`:** removed, not renamed. The new options are the host's `BookServerConfig` fields.

---

## 10. Test plan (Q9)

### Harness

- `stand.ps1 c get|call|bench` on clients.
- `server -Instance N` / `join -Instance M -Server N` and the server-side harness endpoint from `feature/server-side`. The exact syntax comes with that merge.
- Chests are placed with `c place tile=21` and filled through `call p=Terraria.Main.chest.<i>.item.<k>.SetDefaults a=<type>` plus `set ….stack`.
- MS networks are placed with `c place tile=MagicStorage/StorageHeart` / `StorageUnit` / `CraftingAccess`. Filling them may need a small harness helper in phase 2.
- Instance 13 for SP. MP uses instances agreed with the other agent.
- Every visible state gets a cropped screenshot in EN and RU.

### Probes

| Side | Probe |
|---|---|
| Client | `ModernRecipeBook.BookSources.Quantity`, `.Summary`, `.Version`, `.Syncing`, `.Items.Count`, `BookCrafting.LastMessage`, `Main.LocalPlayer.inventory` |
| Server | `BookNearbyServer.Subscribers`, `.InFlight`, `.LastPull` (a short text log), chest slots via `get` |
| Rules | `call p=ModernRecipeBook.BookSourceRules.SelfCheck` → `ok` or the first failing case |

### Single player

| # | Case | Expected |
|---|---|---|
| S1 | 3 chests with wood at 10 / 29 / 31 tiles, R = 30 | Summary "2 chests"; Wood count = the two chests + inventory; card row "Have" matches; tooltip "Chests ×2: …" |
| S2 | One chest locked (gold chest frame), one Calamity Astral locked chest | Not shown; "Not counted: 2 locked" |
| S3 | Open one of the chests | Counted once (as "Open chest"); no double count |
| S4 | Void Bag carried + Void Vault tile in radius | `bank4` counted once |
| S5 | Right-click a cell / Shift-click a cell | 1 / a full stack taken; the chest slot decremented in `Main.chest`; message "Taken to inventory" |
| S6 | Craft ×5 Wooden Chair with wood only in chests | Wood moves in, 5 chairs crafted, chest decreased by exactly 5 × need, inventory wood unchanged net, message "Crafted 5/5 · …" |
| S7 | Full inventory, craft needing 2 new kinds | "Inventory full: free 2 slots"; nothing moved |
| S8 | MS absent (instance save with MS disabled in `enabled.json`) | Storage tab works with chests and banks; no MS lines |
| S9 | MS present, two networks in radius, one with a password | Both summed except the password one; "Not counted: 1 password"; take from the far network works |
| S10 | Recipe needing an Anvil only inside a Crafting Interface in radius | Craftable in the book; the craft succeeds; vanilla's crafting column unaffected after the craft (`FindRecipes` restored) |
| S11 | Alchemy Table only in a Crafting Interface | Alchemy saving applies (`alchemyTable` within the scope) |
| S12 | Portable Access opened far away, then the book | That network counted; MS window hidden while the book is open and back after |
| S13 | `NearbyStorage = false` (`c config type=BookServerConfig field=NearbyStorage v=false`) | Off state with the SP hint; counts equal vanilla's (`FindRecipes` set == book set for inventory-only items) |
| S14 | Calamity recipe with a Calamity station and ingredients in chests | Craftable and crafted |
| S15 | Performance | bench numbers from §8 reported; zero work with the book closed and no goals |

### Multiplayer (server + clients A and B)

| # | Case | Client expectation | Server expectation |
|---|---|---|---|
| P1 | A opens the book | `Syncing` then counts; snapshot bytes logged | A subscribed; one `Reset` sync; nothing sent while idle |
| P2 | Server-side change: fill a chest via the server endpoint | A's count updates within 30 ticks | one upsert to A |
| P3 | A and B both Shift-take 40 from a 50-stack, same frame | One gets 40, the other 10 ("Took 10 of 40: the rest is gone") | chest ends at 0; two `Give`s; total delivered 50 |
| P4 | B has the chest open | A: chest not listed, "Not counted: 1 in use"; A's take never touches it | `UsingChest` excluded; chest unchanged |
| P5 | A pulls, then is killed (process kill) before applying | A reconnects: item not in inventory | entry restored into the same chest at the same x,y; logged |
| P6 | A pulls, applies, acks, disconnects cleanly | item in A's saved inventory | no restore |
| P7 | A's inventory fills during the round trip | rest on the cursor, then `QuickSpawnItem` near A | — |
| P8 | Radius edge: A stands at R − 0.5 and R + 0.5 tiles from a chest; then pulls at R + 5 (client stale) | listed / not listed | pull within R + 8 succeeds, beyond fails with `Nothing` |
| P9 | Locked chest via a forged `Pull` from the harness | — | `Nothing`; chest unchanged |
| P10 | 50 forged `Pull`s in one second | "Too fast: wait a moment" | ≤ 16 processed, the rest `Busy`; ≤ 8 in flight |
| P11 | MS present, two networks, craft needing items from both plus a chest | crafted once; counts drop by exactly the need | `Withdraw` on both hearts; `SendRefreshNetworkItems` sent; MS audit entries for A |
| P12 | B has MS's window open on network 1 while A pulls from it | B's MS window refreshes | refresh sent |
| P13 | Host turns `NearbyStorage` off mid-session | A: off state, mirror cleared, unsubscribed | no cycles; `Pull` → `Off` |
| P14 | Calamity enabled, Calamity locked chests near A | not listed | excluded |
| P15 | Bytes and CPU | idle 0 B/s; numbers reported | cycle µs per subscriber reported |

---

## 11. Risks and open questions (Q10)

### Risks (handled or accepted)

1. **Reflection drift with MS updates.** Binds throw and turn MS support off with a log line, as today (`MOD:BookStorage.cs:329-332, 469-480`). The server and clients run the same MS version (MS is `side = Both`).
2. **MS internals used:** `assignedNetwork`, `clientUsingHeart`, `AuditSystem`. They are internal or internal-class members and may change in any MS patch. They are listed in the adapter, with a fallback:
   - without `clientUsingHeart`, only the radius rule applies;
   - without audit, nothing is logged.
3. **Another mod adding nearby chests through `AddMaterialsForCrafting`** would be counted twice: its live items, plus our scan copies in SP. No such mod is in the owner's set (Calamity has none, §1.3).
4. **The duplicate window in §4.6** (clean quit within ~1 server frame of a delivery) is accepted.
5. **MS's "Use Inventory" and Journey infinite modules are not reflected** in the book's counts (M12, §5.4). The Creative Storage Unit is.
6. **SSC servers:** pulls are off, with a clear state (§7.4).

### Owner decisions (after review)

1. Deposit stays and works with chests too (§4.9); the deposit hotkeys and the Deposit Held button go.
2. Crafted items always go to the inventory; the room check covers the results (§5.5 step 3).
3. Stations: vanilla adjacency plus Crafting Interface stations only.

---

## 12. Phase 2 status

### 12.1 Built

| File | What |
|---|---|
| `BookSourceRules.cs` | pure rules and `SelfCheck()`: range, centres, banks, RU plurals, inventory room (`Shortage`); with `BookBatchPlanner.PlanLayers` (layered max-flow: inventory, then banks, then remote) |
| `BookSources.cs` | the client aggregate, the chest mirror, local containers, take / restock / quick stack / deposit gestures, craft pulls, `Give` handling, the Storage tab texts |
| `BookNearbyServer.cs` | the authority (server or single player): subscriptions, the mirror cycle, `Pull`, `Deposit`, in-flight restore, packets; `BookNearbyConfig`, the one accessor for the two settings |
| `BookMagicStorage.cs` | the rewritten MS adapter: three bind groups (read, write, hide), networks in range, stored items, Crafting Interface stations and the station scope, the hidden MS window |
| `BookCrafting.cs` | `Refresh` = vanilla's availability rule over the book's sources; pull-then-craft; `VanillaDiff` and `CraftRecipe` stand hooks |

Deleted: `BookStorage.cs`, `BookStorageSnapshot.cs`, `BookStorageConfig.cs` (and its localization),
`BookStoragePresentation.cs`, the craft-source chip, the network picker, Connect/Disconnect, Open Magic Storage,
Deposit Held, the deposit hotkeys, the tracker's two pools, `TrackingSource`.

### 12.2 Verified on stand 14 (single player, Calamity 2.2.4 + Magic Storage 0.7.0.11, 5,015 recipes)

| Case | Result |
|---|---|
| `BookSourceRules.SelfCheck` | `ok` (range edge to the pixel, centres, banks, plurals 1/2/5/11/21/22/111, layered plan incl. the overlap case, room) |
| Craftable equals vanilla, feature off, containers in range | `VanillaDiff`: diff=0 (vanilla=99, book=99; another run 114/114) |
| Craftable equals vanilla, feature on, nothing in range | diff=0 (114/114) |
| Feature on, Tin Bars only in a chest | diff=10, all of them the book's (tin tools at the anvil), none vanilla-only |
| Crafting Interface station | a Loom only in the Crafting Interface: Silk becomes craftable in the book only; crafted ×2; afterwards `adjTile[86]` is false and vanilla's list has no Silk (scope restored) |
| Craft from a chest | Tin Pickaxe ×2: chest tin 50 → 34, no tin left in the inventory |
| Craft from Magic Storage | ×3: network tin 65 → 41 |
| Room check covers results | 0 free slots: "Inventory full: free 2 slots"; 1 free: "free 1 slot"; 2 free: crafted |
| Take | right click 500 → 499; Shift+click took `min(maxStack, total)` = 750 from two chests; from MS 40 → 39 → 0 |
| Restock | torch stack 9,900 → 9,999 from a chest (500 → 401) |
| Deposit placement | stone went to the far chest that holds stone (999 → 1,099), not the near chest with a free slot; glass (held nowhere) to the nearest chest with a free slot; wood to the nearest chest holding wood; with MS: tin held nowhere → MS; tin again → MS (holds it); stone → its chest |
| Deposit leftover | chests full: "Nearby storage is full: 50 left", the cursor keeps 50; 9 of 30 fit: 21 come back |
| Shift+click (real UI click) | the stack went in; a favorite was not deposited (vanilla picks it up instead) |
| Deposit All | slots 10..49 except the favorite went in; hotbar and coin slots stayed (no coins nearby) |
| Cursor dropped on the grid (real UI click) | the stack went in, its favorite mark cleared |
| An MS window the player opened | hidden under the book (`HideNativeUI` true), its network counted |

### 12.3 Measured (harness `bench`)

| Call | Time | Allocated |
|---|---|---|
| `BookSources.Quantity(int)`, the per-frame read | 0.0 µs | 24 B: the harness boxes the `int` result; `Breakdown`, `BreakdownRows`, `Summary`, `NotCounted`, `RadiusLine` (cached) all read 0 B |
| `BookSources.Refresh(false)`, nothing changed | 0.4 µs | 3 B |
| `CheckLocal`, every 10 ticks | 0.5 µs | 218 B |
| `Scan`, every 30 ticks (R = 30, one network) | 15.8 µs | 1,456 B |
| `Rebuild`, only on change | 3.9 µs | 3,512 B |
| `BookCrafting.Refresh(true)`, every 20 ticks with the book open | 153.9 µs | 438 B |
| vanilla `Recipe.FindRecipes`, for comparison | 154.7 µs | 617 B |
| server mirror cycle, one subscriber, every 30 ticks | 5.4 µs | 9 B |

Amortised: about 8 µs per tick with the book open (7.7 of them the craftable pass, as before); about 0.2 µs per
tick per subscriber on the server; nothing with the book closed and no tracked goals.

Network bytes: not measured yet (multiplayer waits for `feature/server-side`).

### 12.4 Deviations from this design

1. **Deposit** (owner, after review): §4.9, the `Deposit` packet, the toolbar `[Deposit All] [Quick Stack] [Restock]`, no Deposit Held, no deposit hotkeys.
2. **Craftable is vanilla's exact rule.** Each ingredient is counted as its own type or as the last accepted group holding it, and all required tiles and conditions must hold (`Recipe.CreateRequiredItemQuickLookups`, `CollectedEnoughItemsToCraftRecipeNew`). The design's extra flow check for overlapping rows is dropped, so that with the feature off the set is vanilla's by construction (review fix d). The craft plan still uses the flow to split materials over the layers.
3. **Settings** are read through `BookNearbyConfig` (two stand-in fields) until `BookServerConfig` exists; multiplayer is gated by `BookNearbyConfig.MultiplayerReady = false` while the mod is `side = Client`.
4. **Rate limit** 32 burst / 16 per second instead of 16 / 8, and deposits may overlap, so Shift+click works as fast as in vanilla.
5. **Restock** tops up partial stacks (coins excepted) like vanilla's chest Restock, but does not move ammo into empty slots as vanilla does.
6. **Deposit All coins** follow vanilla's condition (only where coins already are) without exchanging them into higher coins.
7. **Messages fade** after five seconds; cell counts above 99,999 read "399K" / "12M".
8. **Long names** in the details panel shrink to 80 % and then cut; the stack's tooltip repeats the name.
9. **The Craft tooltip** counts only the chests that hold what the craft pulls ("From: inventory, 1 chest").

### 12.5 Screenshots (stand 14, cropped to the book)

`<stand>\shots-14\`, each in EN and RU (`-en-c.png`, `-ru-c.png`):
`nb-01-tab` (summary, not counted, breakdown rows), `nb-02-cell-tip` (where it is + how to put in),
`nb-03-long-name`, `nb-03b-long-name-tip`, `nb-04-toolbar-0/1/2-tip` (Deposit All, Quick Stack, Restock),
`nb-05-summary-tip`, `nb-06-notcounted-tip`, `nb-07-craft-tip` ("From:"), `nb-08-card`, `nb-09-material-tip`,
`nb-10-help`, `nb-11-off`, `nb-12-nothing`, `nb-13-empty`, `nb-14-full`, `nb-15-inventory-full`; and
`nb-16-ms-window-under-book-en.png` (full screen: a player-opened MS window hidden under the book).

### 12.6 Left for the multiplayer phase

- Merge `feature/server-side`; switch `BookNearbyConfig` to `BookServerConfig` and set `MultiplayerReady = true`.
- MP cases P1–P15 of §10 plus: deposit to a chest with no MS; two players depositing into the last free slot; a disconnect mid-deposit.
- Bytes per message and bytes/s idle and active; the "Syncing" state on screen.
- Magic Storage server-side withdraw and deposit on a dedicated server (`Writable` there), audit entries, viewers' refresh.
