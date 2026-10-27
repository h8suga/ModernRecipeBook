# Shimmer from the book (branch `feature/shimmer`)

Owner: «я скрафтил шиммер акцесс ячейку и не могу юзать в книге шиммер) а ты говорил фулл адаптировано меджик сторейдж».
Asked where: «Везде» — the item card, How to Get and the quick search.

Magic Storage's decrafting interface, the **Storage Aether Interface** (`MagicStorage/DecraftingAccess`, RU «Интерфейс
Хранения Эфира», made by shimmering a Crafting Interface) is a
network component whose window shimmers items stored in the network. The book does the same from its own UI: the item
comes out of the network, shimmers as the liquid would, and the results go back into that network.

## What an item turns into (`BookShimmer.For`)

The plan follows vanilla's own decision, in its order (`Item.CanShimmer` / `Item.GetShimmered`,
TML:Terraria/Item.cs:49561-49850):

1. Coins (`ItemID.Sets.CoinLuckValue`): gone, coin luck for the player.
2. Rod of Discord, Clentaminator, Bottomless Water / Shimmer Bucket: their pair, after the Moon Lord.
3. Luminite Brick: one of eight bricks by the moon phase.
4. A music box: the blank Music Box.
5. `ItemID.Sets.ShimmerTransformToItem`: that item.
6. Sparkle Slime Balloon (4986): the rainbow Town Slime arrives, once; after that it does nothing.
7. A critter item (`makeNPC`): the critter, or its shimmer form (`NPCID.Sets.ShimmerTransformToNPC`), let go.
8. A decraft recipe (`ShimmerTransforms.GetDecraftingRecipeIndex`): its ingredients, or the recipe's own
   `customShimmerResults`, one craft's worth (`Unit` = `createItem.stack`) at a time, less what the recipe's
   `RecipeLoader.ConsumeIngredient(isDecrafting: true)` keeps back.

`ItemID.Sets.ShimmerCountsAsItem` is applied first, as vanilla does. A lock (`ShimmerTransforms.IsItemTransformLocked`)
holds the whole item back; the plan names the boss when the recipe tells it (Skeletron for a Bone ingredient, Golem for
a Lihzahrd one). The running game's lock goes through mods' hooks, so the plan does too.

Calamity (CalamityMod.ILEditing.ILChanges):
- `AdjustShimmerRequirements`: the Rod of Discord stays locked until Supreme Calamitas and then the Exo Mechs; the plan
  names the next of the two.
- `ShimmerEffectEdits`: a Plagued Containment Brick before Golem is destroyed and lets three Plague Nanodroids out. The
  book will not do that to a stack in storage: the plan waits for Golem.

Magic Storage's own `ShimmerMetrics` is **not** used: it checks `Item.CanShimmer` on a one-item sample, which says no to
every decraft whose recipe makes several (arrows, bullets), and its server packet applies the results a client sends.

## Where it can be done

- A counted network (`BookSources`, the Storage tab's networks: an access tile within the radius, the window open in
  MS, a carried Portable Access) that has a Decrafting Interface (`TEStorageCenter.ComponentManager
  .GetDecraftingAccesses()`, `BookMagicStorage.HasDecrafting`). A Decrafting Interface within the radius also reaches its
  network, like the other access tiles.
- The one that holds most of the item does it (`BookShimmer.Best`); `Max` is what it holds, in whole units.
- Not when nearby storage is off on the server, without Magic Storage, or when the plan is not ready.

## Doing it (`BookNearbyServer.Shimmer`)

Where the items are true (the server, or the single-player game), on the client's request `NearbyShimmer` (heart
position, item, amount):

1. The same admission as a pull (nearby storage on, the player alive, the rate limit).
2. The plan again, on the server's world; the network again: one the player reaches by the pull rules, with a
   Decrafting Interface.
3. Whole units come out of that heart (`TakeFromHeart`, plain items first).
4. `ShimmerItems` does what `GetShimmered` does, at the player: coin luck (`AddCoinLuck` in single player, the game's
   `ShimmerActions` message to clients), item results, the Town Slime (`NPC.unlockedSlimeRainbowSpawn`, `NewNPC`), critters
   (`NPC.ReleaseNPC`, as many as the NPC slots allow, 50 at most).
5. What was taken and not used goes back where it came from (`ReturnUnused`, the last pieces first).
6. The results go into the same network (`DepositInto`); what does not fit drops at the player's feet (`spilled`).
7. MS's refresh and audit (`SafeReport`). Any exception before the results exist puts everything back.

The answer `NearbyShimmered` (status, item, used, spilled, results by type) becomes the card's status line:
"Shimmered 7 Iron Bar: 21 Iron Ore".

## UI

- **Item card** (`RecipeBookSystem.Shimmer.cs`): a Shimmer mode beside Recipe / Used in / Contents, offered for every
  item with a plan. A lead line per kind, the results as the card's ingredient rows (links, item tooltips), the batch
  bar (x1/x5/x10/Max in units) and the Shimmer button in the Craft button's place; Shift+click shimmers all. When it
  cannot, the reason takes the batch bar's place, the boss and the Storage Aether Interface as links. Without a network
  with that interface in reach the mode still shows what the item turns into.
- **How to Get**: a compact Shimmer button on a "shimmer from X" row with a single source item (click one unit,
  Shift+click all); a row that stands for several items ("N items" decraft) has none, the button would shimmer what the
  row does not name.
- **Quick search**: a Shimmer action on item rows and in the preview, offered only when it can be done now (as Craft is);
  Shift+click shimmers all. One button, so an item that can also be crafted keeps Link in Chat in the preview's 11 places.
- Reasons come from `BookShimmer.WhyNot` (an enum) and `Reason` (its words, the interface by Magic Storage's own name).

## Stand checks

- `stand.ps1 c call p=ModernRecipeBook.Storage.BookShimmer.Describe a=<type>`: the plan, the network, Max and the reason.
- `stand.ps1 c call p=ModernRecipeBook.Storage.BookShimmer.Test a=<type> a=<items>`: shimmer and the result line.
- Fixture: `scratchpad\shimmer\setup.ps1 -I <N>` (a network with a Decrafting Interface and an item of every kind);
  multiplayer: `scratchpad\shimmer\mp-setup.ps1` (stand 8 serves, stand 6 joins).
