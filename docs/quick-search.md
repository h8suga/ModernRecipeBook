# Quick search (branches `feature/quick-search`, `feature/quick-search-redesign`)

Owner: "make a fast search: press e.g. Ctrl+F and search an item right away", then: a separate, full search over everything
the book knows, like a site's search box. After the first version: "it doesn't look modern; rework the search window", plus
descriptions, concepts ("баф" lists the buff potions), every tab's data, and one-click actions on each result.

Code: `RecipeBookSystem.QuickSearch.cs` (index, ranking, scopes, keys, opening), `RecipeBookSystem.QuickPalette.cs` (drawing),
`RecipeBookSystem.QuickActions.cs` (actions, the ">" commands), `BookQuickRank`, `BookEnglishNames`, `BookMobCategoryPath`;
the keys in `BookHotkeys.QuickSearch` and `BookPlayer` (`GuardHeldKeys`, `RestoreSmartCursor`). Texts:
`Mods.ModernRecipeBook.QuickSearch` and `.QuickSearchDesign`.

## Opening

- **Ctrl+F with the inventory or the book open** (any tab, over How to Get or Help): in the world Ctrl is Smart Cursor, held while playing. With the book closed the
  popup stands where the book opens, over a dimmed screen; closing it leaves the book closed unless a pick opened it.
- **"Search Everything"** in Controls: the same on one key, unbound by default so it takes nobody's key.
- **Not** while chat, a sign, a chest name or another mod's text field takes the keyboard (`Main.drawingPlayerChat`,
  `editSign`, `editChest`, `CurrentInputTextTakerOverride`, `PlayerInput.WritingText` as the last frame left it,
  `Main.blockInput`), in the settings, a fancy UI, on the full map or the main menu. The book's own search field is
  fine: Ctrl+F there opens the quick search instead.
- Pressed again while open: the query is selected again.

## Keys never reach the game

- **Smart Cursor:** vanilla toggles it as Ctrl goes down (`Player.TryToToggleSmartCursor`), before F is pressed. Ctrl+F
  puts it back to what it was before Ctrl went down, and Ctrl then stays away from the game until let go.
- **Everything held while the popup is open**, and what is still held when it closes (the Esc or Enter that closed it,
  the clicking mouse button), reaches no game control until let go: no thrown item, no hotbar switch, no inventory on
  Esc or Tab, no swing on the click that closed it. The Enter that opens a pick does not open the chat (`chatRelease`).
- The same rule guards the book's own chords (a key pressed while the book holds the keyboard is blocked by key, not by
  trigger name, so a mod's keybind is blocked too).

## The window

A command palette in the book's panels (Raycast, Spotlight, Linear's Ctrl+K were the references), 1040 × 632 book units:

- **Field:** borderless, the query at 1.12, a larger magnifier, the scope as a chip at its start (Backspace in the empty
  field takes it off), "38 results" (the scope's count) and clear on the right. While English names are read it says so.
- **Scope chips:** All · Items · Storage · Recipes · Bestiary · Bosses · Tracking · Categories, each with its count; an
  empty one hides (All and the one in use stay). Tab / Shift+Tab or a click; the pill glides to the new chip.
- **Results (left):** light group headers (sentence case, the count dimmer), rows of 42 units: icon, the name with the
  typed words in mint (`BookQuickRank.Marks`: a term's first word-start, its stem's, else anywhere), then why it is listed
  (the English name, "ID 1234", the internal name, "gives Ironskin", "in Potions › Buff potions") or its category path
  (each step a link). A description match takes two lines: the name, then the line that matched. The selected row gets a
  fill and an accent bar; the bar glides to the next row in about 60 ms (critically damped, no overshoot).
- **Linked rows** hang under their match with an elbow: the relation muted, the name it points to brighter, the
  localized phrase whole ("Dropped by {0}" / «Падает с: {0}»: only its parts change colour).
- **Kind badges** (Item, Mob, Boss, Stage, Category, Mod) moved off the rows (the group header says it) to the preview,
  and to the rows of the one mixed group (Opened recently).
- **Preview (right):** the selected result in an inset card: a big icon, its name, kind, category path, mod and stage;
  what the player has ("You have 120", where: "Inventory: 2 · Magic Storage: 15"), "Can craft ×5", "Tracked · 40%",
  a favorite; the description (set bonus included) with the typed words marked; How to Get's first rows (the recipe's
  station, then the sources with their chance) or a mob's numbers, flavor text and loot; a category's first members as
  icons; a stage's boss numbers and loot. Below, every action as a button with its key. Sources and loot are read once
  the selection rests (8 updates), as the links are; a quiet placeholder of their shape stands in until then.
- **Footer:** the keys for the selected row, and what the last action said ("Crafted: Torch") or "> commands".
- **States:** the empty field lists tracked goals, picks from this session, recent crafts and mobs seen, then what the
  field searches. Nothing found: the tabs' own searches (they read item texts), "Search everything" when a scope hid the
  rest, the query and a hint; the preview explains the four ways to search.
- **Motion:** the panel comes in with the backdrop as the book's modals do (150 ms, ease-out, from 98 %); over the game
  the same curve. The selection and the scope pill glide; nothing else moves, and closing is instant (it is opened tens
  of times a play session).

## What it finds

One index per language and world (`EnsureQuickIndex`, 21-28 ms with Calamity, 9191 entries and 450 concepts, built on the
first open):

| Group | Entries | Matched on | Opens |
|---|---|---|---|
| Items | every catalogue item | name, English name, internal name (as words), ID, mod; what it is; its description | its card on Items |
| Bestiary | every entry | name, English name, internal name, net ID, mod; its category; its flavor text | its card on the Bestiary tab |
| Before a boss | every boss step | boss name, English name, its parts' names ("Ares" finds the Exo Mechs) | the stage filter "Before: X" |
| Item categories | the sidebar's | label, English label, key, short forms ("buff", «баф») | Items on that category |
| Bestiary categories | kinds, events, biomes | label, English label | the Bestiary on that category |
| Mods | mods with items or mobs | name | Items (or the Bestiary) with that mod's filter |
| Tracking | tracked goals | the goal's item or set | the Tracking tab on that goal |

- **What an item is (concepts, `EnsureQuickConcepts`):** a query that names a concept strongly (every term starts a word
  of it) lists its members:
  - an item or Bestiary category (the sidebar's own lists, with short forms in both languages: `QuickSynonyms`, e.g.
    "buff, buffs" / «баф, бафф, бафы»), the reason "in Potions › Buff potions" (`BookCategoryPath.Path`);
  - the buff an item gives (`Item.buffType`'s name, and the English one), and the stations vanilla gives a buff for a
    right click (`Player.TileInteractionsUse`: Crystal Ball, Slice of Cake, War Table, Sharpening Station, Bewitching
    Table, Ammo Box): "Clairvoyance" finds the Crystal Ball, "gives Clairvoyance";
  - a trait (`BookItemTraits`: homing, pierces, auto attack, minion, sentry...) and a class (`BookItemClasses`).
  The concepts follow the traits (read after the world loads) and the English labels, a few ms, never the whole index.
- **Descriptions:** three letters or more also read the Items tab's own text cache (`ItemDescriptions`, now an `ItemText`
  record per type: the description as searched and as shown, the armor piece's set bonus, the item's own English text
  in another language) and each Bestiary entry's flavor text (`FlavorTextBestiaryInfoElement`'s key). Every term must
  start a word of it (its stem may): "rage" finds "the rage meter", not "storage". A Latin query in another language
  reads the English text too. The cache is built in 2 ms slices while the book or the quick search is open; the quick
  search runs again when it is done. Calamity's buff tags ([cbuffplr:Frostburn]) read as the buff's name, as the game
  draws them (`ResolveTags`).
- **English names** while the game runs in another language: `BookEnglishNames` reads vanilla's embedded `en_US` json
  (items, NPCs, buffs, Bestiary tags) and every mod's `en-US` hjson (display names, and the book's own category, class,
  trait and command labels).
- **Ranking:** a strong name match (a name's words start with the query, or better; an ID, an internal name, the mod)
  first, then what an item is (a concept), then a name that only contains the query, then the description; within a
  tier `BookQuickRank.Score` (exact > prefix > word-prefix > contains, a name above an internal name), then the weight
  (how many recipes use the item, a met mob, a boss), a little for a recent craft or sighting and for an item this world
  can get before its next boss, then the shorter name. Groups are ordered by their best match.
- **Russian stems:** up to two final vowels go while three letters stay; a word ending in ь loses it while four letters
  stay ("ярость" finds «ярости»).
- **Caps:** All shows Items 5, Bestiary 4, the others 3 (Mods 2); a scope lists 60, then "All N on the tab" (the tab's own
  search). Categories, stages and mods expand in place.

## Every tab in the search

- **Storage:** an item the player has (inventory, nearby chests, banks, Magic Storage: `BookSources.Quantity`) shows
  "×120" on its row (the tooltip and the preview say where); the Storage scope lists them; Enter there opens the Storage
  tab with its biggest stack selected. Elsewhere Enter opens the item's card on Items, as before: the scope says which
  tab, and the footer names it ("Enter Open on Storage").
- **Recipes:** an item that can be crafted now (the card's own logic: `BookCrafting.IsAvailable`, nearby storage and the
  Crafting Interface stations included) shows "Can craft ×N" and its row button crafts; the Recipes scope lists the
  items with a recipe, and Enter there opens the Recipes tab on it, with Craft.
- **Tracking:** goals whose item matches list with their progress bar; Enter opens the Tracking tab on the goal.
- **Recent:** the empty field lists recent crafts and sightings; they rank a little higher.
- The per-item counts and craftable recipes are cached by `BookSources.Version`, `ItemsVersion` and
  `BookCrafting.AvailabilityVersion`; when any of them changes (a craft, a take, a scan) the same query is ranked again
  with the selection kept.

## Actions

Every action runs the book's own code and gives its feedback (the card's `BookCrafting.TryCraft` and its message, Storage's
`TakeToInventory` / `TakeOne` and their sound, Deposit All's rules, `ToggleTrack`, `ToggleFavorite`, the stage filter,
How to Get). The ones that show something open the book there, through its history; the others stay in the field.

- **One click on each row:** the row's own button at its right end, picked the same way everywhere: an item that can be
  crafted now → **Craft ×1** (Shift+click: the most), one nearby storage holds → **Take** (a stack; right click: one), one
  with a recipe → **Track** / **Untrack**, everything else → **Open** (shown only on the row in hand: a click on the row
  opens it already). The row's other actions appear as small buttons while it is hovered (Track, Favorite, How to Get,
  Used in, Shimmer with the Decrafting Interface's icon; a mob's Stats and Favorite; a goal's Untrack and How to Get). A
  click on the row's body opens it.
- **The preview** lists them all: Open, Craft ×1, Craft ×Max, Take, Deposit (its stacks from slots 10-49, as Deposit All
  takes them), Shimmer / Shimmer All, Track / Track Set (armor), Favorite, How to Get, Used in, Contents (containers),
  "Before: <boss>" (the stage that first lists it), Link in chat (`ItemTagHandler.GenerateTag`); for a mob Open card,
  Stats, Favorite, "Before: <this boss>", and the boss's summon item's Craft / How to Get / Track (`BookBossInfo`: Boss
  Checklist, else Calamity's registrations). The grid holds 11; what is past that (Link in chat first) is left out.
- **Shimmer / Shimmer All** (`BookShimmer`, through a Magic Storage Decrafting Interface in reach): offered only
  when the item can be shimmered from the book now (`BookShimmer.Reason` is ""), as Craft is offered only for an item that
  can be crafted now; without an interface in reach, with none in its network, before the boss that unlocks it or with
  fewer than one step's worth (a decraft of a recipe that makes 10 takes 10) the row and the preview look as before, and
  the card says why. Shimmer takes one step, Shimmer All `BookShimmer.Max` (Shift+click on Shimmer does the same). The
  tooltip says what it becomes ("Iron Bar ×50 → Iron Ore ×150", "→ coin luck", "→ Faeling released", the first three
  ingredients) and the network it takes from with its count; names there are plain, as the Craft tooltip's sources are.
  The result line (`BookCrafting.LastMessage`) is the footer's, as a craft's. No key: the card's chords have none for it.
- **Keys are the card's:** Ctrl+Enter the row's action (craft as in the card; take as on Storage for an item that cannot
  be crafted now), Ctrl+Shift+Enter craft the most, Alt+T track, Alt+F favorite, Alt+G How to Get, Alt+U Used in (a mob's
  stats), Shift+Enter Used in (as Shift+click in the grid). Ctrl+Enter used to open How to Get here; that is Alt+G now,
  as on the card.
- **Commands (">"):** Deposit All, Quick Stack, Restock (the Storage tab's buttons), Refresh (F5), Clear filters
  (Ctrl+Backspace), Open <tab> (Ctrl+1..6), Switch tracker style (the config's compact lines, saved), Help, Settings (the
  mod's config page), Untrack all (Enter twice, as the Tracking tab asks). Five more for a player working toward a boss:
  Before the next boss (Alt+5's stage filter: what can be had for the fight), Next boss's card (its loot, life and
  summon), Craftable now (Items with the Craftable filter), Favorites, Summoner items (the class filter).

## Keys in the popup

| Key | Does |
|---|---|
| ↑ ↓, PgUp PgDn | Select (across groups and linked rows; held keys repeat) |
| Tab, Shift+Tab | The next or previous scope chip |
| Enter | Open the selected row (on the scope's tab) |
| Ctrl+Enter, Ctrl+Shift+Enter | The row's action (craft or take), craft the most |
| Alt+T, Alt+F, Alt+G, Alt+U | Track, favorite, How to Get, Used in (a mob's stats) |
| Shift+Enter | An item on its Used in list |
| Esc | Close; the query and the scope stay and come back selected |
| Ctrl+A, Backspace | Select the query; clear it; in the empty field, take the scope off |

The mouse: hover selects, a click opens, a row's buttons act, the wheel scrolls, a click outside closes.

## Cost (stand, Calamity, Russian names loaded)

- Ranking a keystroke: 0.7-1.4 ms; the slowest passes are a single letter (a full scan, 2.2-2.5 ms) and the third letter
  (descriptions join, a full scan once, about 2.3 ms). Typing on narrows the last pass's matches (`QuickExtends`): the
  same terms with the last one longer, or one more.
- A popup frame while typing for real (`stand.ps1 type`): 1.2-1.9 ms at most, ranking included; idle 0.3-0.5 ms.
- Links and the preview's sources after the pause: read once per match, as before (11-13 ms the first time in a world).
- The text cache with set bonuses and English texts: 48 slices of 2 ms after the first open (the descriptions alone took
  72 ms, about 36 slices); the slowest single tooltip (Apoctolith, 2.2 ms) bounds a slice's overrun, as before.
- Stand probes: `c call p=ModernRecipeBook.UI.Book.RecipeBookSystem.QuickSearchCost a=<query> a=<scope>`,
  `QuickTypingCost a=<query>`, `QuickFrameMaxMs`, `QuickSearchMaxMs`;
  `c call p=ModernRecipeBook.Catalogue.Search.BookQuickRank.SelfCheck`.
