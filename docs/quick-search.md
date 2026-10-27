# Quick search (branch `feature/quick-search`)

Owner: "make a fast search: press e.g. Ctrl+F and search an item right away", then: a separate, full search over everything
the book knows, like a site's search box. Code: `RecipeBookSystem.QuickSearch.cs`, `BookQuickRank`, `BookEnglishNames`,
`BookMobCategoryPath`; the keys in `BookHotkeys.QuickSearch` and `BookPlayer` (`GuardHeldKeys`, `RestoreSmartCursor`).

## Opening

- **Ctrl+F anywhere in the game**, the book open (any tab, over How to Get or Help) or closed. With the book closed the
  popup stands where the book opens, over a dimmed screen; closing it leaves the book closed unless a pick opened it.
- **"Search Everything"** in Controls: the same on one key, unbound by default so it takes nobody's key.
- **Not** while chat, a sign, a chest name or another mod's text field takes the keyboard (`Main.drawingPlayerChat`,
  `editSign`, `editChest`, `CurrentInputTextTakerOverride`, `PlayerInput.WritingText` as the last frame left it,
  `Main.blockInput`), in the settings, a fancy UI, on the full map or the main menu. The book's own search field is
  fine: Ctrl+F there opens the quick search instead.
- Pressed again while open: the query is selected again.

## Keys never reach the game

- **Smart Cursor:** vanilla toggles it as Ctrl goes down (`Player.TryToToggleSmartCursor`), before F is pressed. Ctrl+F
  puts it back to what it was before Ctrl went down, and Ctrl then stays away from the game until let go, so its release
  toggles nothing. Ctrl alone and Ctrl with any other key outside the book stay vanilla.
- **Everything held while the popup is open**, and what is still held when it closes (the Esc or Enter that closed it,
  the clicking mouse button), reaches no game control until let go: no thrown item, no hotbar switch, no inventory on
  Esc or Tab, no swing on the click that closed it. The Enter that opens a pick does not open the chat (`chatRelease`).
- **The same rule now guards the book's own chords** (scout H1, H2): a key pressed while the book holds the keyboard
  (Ctrl or Alt down, a menu, the search field, How to Get) and a key the book acted on (F1, the page keys) is blocked by
  key, not by trigger name, so a mod's keybind is blocked too. Every binding is a trigger of one set
  (`TriggersSet.KeyStatus`, mods' as "Mod/Name"). With the owner's bindings: Ctrl+Tab switches the tab and no longer
  closes the book through Inventory on Tab; Alt+F no longer dashes through Calamity's F, Alt+T no longer flips gravity
  through its T. The book's own keybinds stay usable. This replaces the digit-with-modifier and F1 special cases.
- The game reads the keyboard an update late (`PlayerInput.GetPressedKeys` reads `Main.keyState`), so a key counts as
  held while either the live keyboard or the game's shows it.

## What it finds

One index per language and world (`EnsureQuickIndex`, 25-47 ms with Calamity, about 9,200 entries, built on the first
open):

| Group | Entries | Matched on | Opens |
|---|---|---|---|
| Items | every catalogue item | name, English name, internal name (as words), ID, mod | its card on Items (selected in the grid when the filters show it, else in the card) |
| Bestiary | every entry | name, English name, internal name, net ID, mod | its card on the Bestiary tab |
| Before a boss | every boss step | boss name, English name, its parts' names ("Ares" finds the Exo Mechs) | the stage filter "Before: X" (Storage and Tracking switch to Items) |
| Item categories | the sidebar's | label, key ("potions" works in Russian) | Items on that category |
| Bestiary categories | kinds, events, biomes | label, English label | the Bestiary on that category |
| Mods | mods with items or mobs | name | Items (or the Bestiary) with that mod's filter |

- **English names** while the game runs in another language: the game keeps only one language's texts, so
  `BookEnglishNames` reads the English ones again from vanilla's embedded `en_US` json and every mod's `en-US` hjson.
  The files are read in 2 ms slices on the main thread (15 ms in all with Calamity), parsed on a worker: 11,798 names.
  A row found by its English name shows it: "Террапризма · Terraprisma".
- **Ranking** (`BookQuickRank.Score`): exact > prefix > word-prefix > contains, each one step higher when a name matched
  rather than an internal name, ID or mod. Every term must match somewhere; a Russian word also starts a word
  without its last vowel or two (`BookQuickRank.Stem`: "лунный лорд" finds «Тело лунного лорда»). Equal matches: the weight (how many recipes
  make or use the item; a met mob, a boss; the next boss, then bosses still alive; a bigger category), then the shorter
  name. Groups are ordered by their best match, the table's order on a tie.
- **Caps:** Items 5, Bestiary 4, the others 3 (Mods 2). Items and Bestiary end with "All N on the tab" (the tab's own
  search, which also reads item texts, its other filters cleared as Ctrl+Backspace clears them); the others expand in
  place.
- **Nothing found:** "Search item descriptions on the Items tab", which runs the book's own search (it reads tooltips).
- **Empty field:** picks from this session, the latest crafts, the mobs seen last.

## Linked rows

Under a strong match (a name's words start with the query, or better; a Russian word's ending may differ), the book's
own links:

- **An item** (the first two): How to Get's own rows (`SourceEntries`, the Treasure Bag rule included): the bosses that
  drop it, the containers it is found in, other mobs; at most 3, then "All ways to get it: N · How to Get". Another
  world's rows are left to How to Get. Terraprisma → "Dropped by Empress of Light"; Kaleidoscope → the Empress and her
  Treasure Bag.
- **A mob** (the first): its card's own loot (its rules, the bag's in Expert): this world's drops, the rarest first, a
  material after the weapons and accessories of its rarity; at most 3, then "All loot: N · mob card".
- Links are read once the query rests (8 updates), one match per update: reading a match's sources the first time
  probes conditions and took up to 37 ms; "w", "wo", "woo" need none of them.

## Rows

- Icon, name, why it matched when not by name (English name, "ID 1234", internal name), then where it sits: an item's
  path as its card names it (`BookCategoryPath`: "Weapons › Whips"), a mob's kind and biome (`BookMobCategoryPath`:
  "Enemies · Forest", an event only when no biome lists it). Each step of the path is a link to that category.
- A badge on the right says what the row is and where it opens: Item, Mob, Boss, Stage, Category, Mod.
- Linked rows hang under their match with an elbow, smaller and muted.

## Keys in the popup

| Key | Does |
|---|---|
| ↑ ↓, PgUp PgDn | Select (across groups and linked rows; held keys repeat) |
| Enter | Open the selected row |
| Shift+Enter | An item on its Used in list (as Shift+Click in the grid) |
| Ctrl+Enter | An item's How to Get (the card behind it, so Back returns to the card) |
| Esc | Close; the query stays and comes back selected, so typing replaces it (the book's Esc closes the top layer, never clears a field) |
| Ctrl+A, Backspace | Select the query; clear it |

The mouse: hover selects, a click opens, the wheel scrolls, a click outside closes. Every pick goes through the book's
history: Back returns to where the book was.

## Cost (stand, Calamity, Russian names loaded)

- Ranking a keystroke: 0.7-2.6 ms (single letters match thousands; only the shown rows are kept in order).
- The slowest popup frame while typing from a cold start: 12 ms (the first rows' new strings); typing "moon lord" after
  that: 2.6 ms. Links after the pause: 11-13 ms once.
- Before the fixes the first query's frames took 52-93 ms: `BookCategoryPath` counting a category the first time an
  item in it asks (now warmed in 2 ms slices while the popup is open), the world's tile sources (read on the update after
  the popup opens) and vanilla textures loaded on the frame that asks (now requested in the background, the icon shows a
  frame later).
- Stand probes: `c call p=ModernRecipeBook.UI.Book.RecipeBookSystem.QuickSearchCost a=<query>`, `QuickFrameMaxMs`,
  `QuickSearchMaxMs`; `c call p=ModernRecipeBook.Catalogue.Search.BookQuickRank.SelfCheck`.
