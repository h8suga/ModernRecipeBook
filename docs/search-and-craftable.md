# Search by item text and the Craftable filter (branch `feature/search-and-craftable`)

Owner: search should also find an item by its description, and Items and Recipes get a "Craftable" filter with the meaning of the card's Used in "Craftable" toggle. Code: `BookItemTooltip.cs`, `RecipeBookSystem.Craftable.cs`, `ApplyFilters` and the description cache in `RecipeBookSystem.cs`.

## Search: name first, then text

- **The text** is the tooltip as the game shows it on hover: `Main.MouseText_DrawItemTooltip_GetLinesInfo`, then `ItemLoader.ModifyTooltips` with every mod's edits (`BookItemTooltip.Lines`). Calamity rewrites many vanilla lines: Monk's Shirt says "rogue" only after its ModifyTooltips.
- **Kept lines:**
  - the item's own lines (`Tooltip0..n`, as every mod left them);
  - every line a mod adds (Calamity's "Uses 1 minion slot", stealth strike lines).
- **Left out:** vanilla's stat and label lines.
  - "50 rogue damage", "22 defense", "Equipable", "Material", "Consumable", durations, prices, prefixes.
  - They read the same on hundreds of items. With the damage line, "rogue" found every rogue weapon and buried Monk's Shirt.
  - The class and category filters already cover them.
- **Chat tags** go: a colour tag keeps its text (`BookRules.PlainText`), an item or glyph icon (`[i:ID]`, `[g:4]`) has none (`BookRules.TagFree`).
- **The book's own tooltip code** stays out while it reads (`BookItemTooltip.Building`). It is not a hover, so the Storage lines and the Mouse3 shortcut do not fire.
- **Matching:** every term must be in the name fields (name, key, internal name, mod) or in the text. "flask rogue" finds Flask of Venom: "flask" is in the name, "rogue" in the text.
- **Ranking:** name, key and mod matches come first, then the ones found by text alone. Each group keeps the chosen sort. Recent keeps its newest-first order inside each group.
- **Where:** Items, Recipes, Storage and Recent use the same item text. The Bestiary searches as before and keeps its placeholder.

## Why an item matched

- The pager says how many the text alone found: "Items: 59 · text matches: 49" / «Предметов: 59 · по тексту: 49».
- The search box's tooltip says "Name matches first, then the item's text".
- No per-cell badge or matched-line hint: the hover tooltip already is the item's text with the line that matched. A marker on 49 cells would be noise.

## Cost and the cache

- **Building every description at once:** 8317 items in 61–70 ms with Calamity (7–8 µs each), 44 MB of garbage. Stand check: `c call p=ModernRecipeBook.RecipeBookSystem.DescriptionCost`.
- **That is one 4-frame stall, so the cache is built in 2 ms slices:**
  - one slice per update while the book is open, about 300 items each;
  - done in about 28 updates, half a second after the first open.
- **Until it is done** the search matches names only. The pager says so: "reading item text: 45%" / «читаю тексты предметов: 45%». A query typed meanwhile is filtered again once it is done.
- **The cache** is per item type with the name it was built from, like `ItemSearchTexts`.
  - A language change starts it over.
  - Leaving the world clears it: mods' tooltips can read the world.
- **`ApplyFilters` per pass** (`c bench p=ModernRecipeBook.RecipeBookSystem.ApplyFilters a=false n=10`, Items with Calamity):

  | | before | after |
  |---|---|---|
  | Items, empty query | 7.0 ms, 44 B | 7.1 ms, 44 B |
  | Items, "rogue" | 0.40 ms, 76 B | 0.70 ms, 76 B |
  | Recipes, empty query | 3.7 ms, 76 B | 3.6 ms, 76 B |
  | Recipes, "rogue" | 0.36 ms, 108 B | 0.51 ms, 108 B |

  No new garbage. The two groups reuse one list (`TextMatches`), and the sort delegate is cached by the compiler.

## Placeholder and the filter bar

- "Name, text, ID, mod" / «Имя, текст, ID, мод». The owner's "Name, text, ID or mod" is 152 book units, the RU one 171. They do not fit once Items and Recipes have two toggles.
- The empty search's placeholder also has the clear button's room (`BookGeometry.SearchPlaceholder`). At a large text size it shrinks instead of being cut.
- **Room for the toggle:** Items and Recipes now hold the station filter and Craftable, `FilterInset` 88.
  - The search gives up 48 units: 192 wide, the placeholder fits with 1.5 to spare.
  - The mod dropdown gives up 40: 112 wide, "All mods" / «Все моды» still fit.
  - A long mod name is cut ("Calamity...") and reads in full in its tooltip, as "Calamity Mod Music" already was.
  - The Bestiary keeps its layout (inset 44). Storage and Recent keep the full width.
- Checked at 100% and 70% book scale, EN and RU.
- A query cut short in the box now reads in full in the search box's tooltip.

## Craftable filter

- **Meaning:** only what the player can craft right now, as the Used in "Craftable" toggle means it (`BookCrafting.IsAvailable`).
  - Materials come from the inventory and nearby storage, and the stations must be in range.
  - **Items:** an item stays when any of its recipes is available. That includes every member of a merged "A or B" variant.
  - **Recipes:** a merged recipe stays when any of its members is available.
- **Updates:** `BookCrafting.AvailabilityVersion` changes only when the set of craftable recipes does.
  - With the filter on, `UpdateBookUI` filters again on that change (`AppliedAvailabilityVersion`), never every frame.
  - The card keeps its item even when the list drops it (`ApplyFilters(keepSelection: true)`). Crafting the last Work Bench leaves the card on the Work Bench, with Craft disabled. Otherwise the next Craft click would have made whatever moved into the card.
- **Order with the other filters:** search, mod, category, favourites, stage, class, then Craftable, then the station filter.
  - The station menu lists the stations of what Craftable leaves, like every other filter.
  - The empty grid names the first filter that hid something: class, then Craftable, then stations.
- **Button:** a hammer (vanilla's crafting window toggle is one) between the station filter's anvil and the favourites star.
  - The station filter is a menu like the dropdowns, so it sits by Sort; the two on/off toggles sit together.
  - When on, the button gets the accent frame.
- **Tooltip:**
  - Off: "Craftable / Only what you can craft right now / Craftable now: 6 of 8,310". The count comes from the listed entries, counted on hover and again only when the list or the craftable set changed.
  - On: "Craftable now: 6 · hidden: 8,304 / Click again to show everything".
- **Empty grid:** "Nothing to craft right now · Turn off Craftable to show everything" / «Сейчас ничего не создать · Выключите «Можно создать», чтобы показать всё».

## State

- Kept per tab in `BookUiState.TabFilters.CraftableOnly`. `BookUiState` is version 8; older saves have it off.
- Back/Forward carries it (`NavState.CraftableOnly`). Ctrl+Backspace clears it with the other filters.
- Leaving the world resets the live value, like Favorites; the player's save brings it back.
- Mouse: a click toggles it and ends typing in the search, as the star does. There is no key for it: the star has none either, and there is no vanilla gesture to follow.
