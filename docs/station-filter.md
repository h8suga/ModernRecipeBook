# Station filter and Station sort (branch `feature/station-filter`)

Owner: «добавь возможность сортировки по станции (только те, что у предметов, отображаемых в фильтре), чтобы можно было сразу станции пикнуть … Должна быть возможность выбирать несколько станций, а не одну» and «сортировку по станции добавь ещё, типа от слабой к сильной». Code: `RecipeBookSystem.Stations.cs`.

## Filter: exclusion

The player unticks the stations he does not have.

- **Items tab.** An item stays while at least one of its recipes needs none of the unticked stations. An item without a recipe (drops, shops, world) never goes.
- **Recipes tab.** A recipe goes when any station it needs is unticked. Merged "A or B" variants share their stations (`BookRecipeVariants.Common` includes `requiredTile`), so a variant counts as one recipe.
- **Matching is by `requiredTile`.** A tile that only counts as a station through `adjTile` (a Hellforge works as a Furnace) is not matched to the station it stands in for.
- **The menu lists** the stations of what the other filters list. That is search, mod, category, class, stage and favourites, with the station filter itself left out, so the list never shrinks as you tick.
  - Unticked stations stay listed, at 0 if nothing listed uses them.
  - A row's count is how many listed items (or recipes) have a recipe at that station.
  - Rows are sorted by count, then by name. "By hand" is not a station.
- **Where it is offered:**
  - Items and Recipes: yes.
  - Recent: no. It lists only what was just crafted, so the stations are at hand.
  - Bestiary, Storage and Tracking: no; there are no recipes there.

## Button and menu

- **Button.** The filter row has no room for a text dropdown. "Without 12 stations" measures 128 book units, plus 40 for the dropdown frame, so the search would drop below its placeholder width.
  - The filter is therefore an icon button, an anvil, in the slot the Bestiary gives its Encountered toggle. The search narrows by the same 44 units there.
  - While stations are unticked, the button has an accent frame and a count badge.
  - The button's tooltip names the unticked stations. The menu header carries the label: "Stations" / «Станции», or "Without 2 stations" / «Без 2 станций» with RU plurals.
- **Menu.** It uses the book's menu style. Clicking a row toggles it and the menu stays open; "Show all" / «Показать все» clears every unticked station.
  - A ticked row shows a check box.
  - An unticked row shows a crossed box, a dimmed name and a line through the name, so the state never rests on colour alone.
  - A row's tooltip shows the full name, the count, and what a click will do.
- **Keyboard.** Up/Down moves the focus, Enter or Space toggles the focused row, and Esc or the inventory key closes the menu as the top layer (it is part of `MenuOpen` and `LayerOpen`).
  - While the menu is open it captures the keyboard: Space does not jump, and Enter does not open the chat.
- **Empty grid:** "Hidden by the station filter" / «Скрыто фильтром станций».

## State

- Kept per tab in `BookUiState.TabFilters.ExcludedStations`. `BookUiState` is version 6; older saves have none.
- Saved per player. Back/Forward restores it, and Ctrl+Backspace clears it with the other filters.
- Keys are stable: `Terraria/<tile id>` for vanilla, `ModTile.FullName` for modded tiles. A key whose mod is not loaded is kept but has no effect and is not counted.

## Station sort

The sort menu gains "Station: weakest first" / «Станция: от слабой» and "Station: strongest first" / «Станция: от сильной». It appears on Items and Recipes only. The closed dropdown shows "Station ↑/↓" / «Станция ↑/↓», like the Met sort.

**Station rank,** computed once with the recipe index, uses these keys in order:
1. The stage of the item that places the station.
2. That item's rarity.
3. The median rarity of what the station makes. A Hellforge and an anvil are both common and cheap, but the Hellforge makes Hellstone.
4. The item's value.

A station that no item places (the altars) takes the earliest stage of what its recipes make, and comes last in that stage. "By hand" ranks 0.

**Sort key:**
- An item's key is the minimum, over the recipes the station filter leaves, of the strongest station each recipe needs. That is the easiest way to make it.
- A recipe's key is its own strongest station.
- Items without a recipe come after every crafted item in both directions.
- The name breaks ties in both directions.
