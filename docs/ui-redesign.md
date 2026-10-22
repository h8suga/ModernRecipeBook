# UI redesign — spec (branch `feature/ui-redesign`)

Owner feedback: texts are too long and not user-friendly; the interface must be more convenient and
predictable; both tracker overlays are inconvenient. This spec is the single source of truth for every
agent. Read it fully before editing. Screens of the current state: `<stand>\shots\a*.png`.

## 1. Copy rules (both languages, always through `T("ru", "en")`)

- Buttons, tabs, headers: 1–3 words. Actions are verbs ("Craft", "Track", "Clear"); views are nouns.
  EN Title Case for buttons/tabs/section headers, sentence case for hints. RU sentence case everywhere.
- Hint and status lines: one line, at most ~60 characters EN / ~70 RU. Never a paragraph in the main view.
- Tooltips: at most 2 short lines (~120 characters total): what it does + shortcut (`· Ctrl+F`). Rules,
  caveats and explanations move to Help (F1) or disappear.
- No internals: no "native", "uncertain stage", "projected leftovers", "API", "audit", "dynamic mod effects".
- One word per concept, same in every screen:

  | Concept | EN | RU |
  |---|---|---|
  | following a craft in the overlay | Track / Tracking / Untrack | Отслеживать / Отслеживание / Не отслеживать |
  | stages | Pre-Hardmode · Hardmode · Post-Moon Lord | Прехардмод · Хардмод · После Лунного лорда |
  | stage filter "all" | All stages | Все этапы |
  | material count | `have/need`, e.g. `12/10` | same |
  | crafting station | Station | Станция |
  | where to get an item | How to Get | Где взять |
  | crafting source | Inventory · Storage | Инвентарь · Хранилище |

  "Start crafting" (which only starts tracking) becomes Track. "Post-hardmode" becomes Post-Moon Lord.
- RU strings must fit the same boxes as EN: if a RU label does not fit, shorten the wording, do not let the
  text shrink to an unreadable size ("Предметы", "Хранилище" currently collapse in the tab bar).
- Labels never rely on truncation ("Движ." for Move). If it does not fit, change the layout or the word.

Owner (2nd round): the whole current layout is ugly and unintuitive — rearrange every tab boldly and consistently (same grid, same filter bar position, aligned columns, clear hierarchy, even spacing), not small tweaks. Use the design skills: ui-ux-pro-max, emil-design-eng, frontend-design, high-end-visual-design, minimalist-ui, design-taste-frontend, apple-design; review with web-design-guidelines.

Owner (3rd round, after the zones merged):
- Filters never reset by themselves. Search, mod, category, favorites and sort are kept per tab (saved,
  UI state v4); only the player clears them (search ✕, Ctrl+Backspace for the open tab, picking a value).
- The stage filter is global: a dropdown in the header next to the title, applied to every tab, saved, and
  untouched by Ctrl+Backspace or jumps.
- No Chains tab ("зачем для цепочек отдельная вкладка?"). The item card has a Recipe | Used in switch; Used in
  lists the recipes that take the item (earliest stage first, global stage filter + Craftable toggle only).
  Clicking an ingredient or a Used in row shows that item in the card; ‹ / Mouse4 / Alt+← go back,
  Mouse5 / Alt+→ forward. Shift+Click in the grid and Mouse3 on an inventory item open Used in directly;
  How to Get → a recipe shows that recipe in the card. Tabs: Items · Recipes · Storage · Recent · Tracking,
  Ctrl+N = the N-th visible tab.
- Track keeps the book open (toggle, footer confirms).

## 2. Visual system

- Keep the dark green skin and the existing palette (`Panel`, `Surface`, `Border`, `TextColor`, `Muted`,
  `Accent` in RecipeBookSystem.cs). Primary action: Accent fill. Secondary: outlined. Destructive: muted red text.
- Spacing on the 1120×700 book canvas: multiples of 4, prefer 8 / 12 / 16 / 24. Equal gaps between siblings.
- Text: nothing readable below scale 0.8 (today's 0.6–0.7 sub-hints go away or grow). Section labels:
  small uppercase `Muted`. Values: `TextColor`.
- States for every control: hover (brighter fill), selected (Accent border + fill), disabled (Muted, no hover).
- Colour never carries meaning alone: counts stay numeric (`3/5`), status also has a word or icon.
- Tooltips only through `MouseTip(...)` (deferred, wrapped, with a panel). One tooltip per hover.

## 3. Components (use these everywhere instead of ad-hoc widgets)

- **Segmented control** — every option visible, the selected one highlighted. For 2–4 options: stage filter,
  craft source. Replaces click-to-cycle buttons (cycling hides the options and is not predictable).
- **Dropdown** — closed state shows the current value + `▾`; click opens a list inside the book; click outside
  or Esc closes. For more than 4 options: mod filter, sort (field and direction in one list).
- **Icon button** — square, same size in a row, always with a `MouseTip` label (+ shortcut).
- **Empty state** — one title line + one action hint, centered in its panel.

## 4. Zones and ownership (one agent per zone, own git worktree and branch)

Merge safety: only edit the methods/rects listed for your zone. If you need a helper, add a NEW method
(or a new file) instead of changing an existing helper's signature. Do not reformat untouched code. Keep
CRLF line endings. Never `git stash`. Commit on your own branch with clear messages.

### Zone A — book core (`ui/book`)
Header, tabs, filter bar, categories, item grid, chain sample slot, detail panel, footer; Items, Recipes,
Chains and Recent views. Owns `BookGeometry.cs` (all rects except those listed for B and C), and in
`RecipeBookSystem.cs`: `UpdateUI`, `HandleKeys`, `Draw`, `DrawStageFilter`, `StageExplanation`, `StageName`,
`DrawSample`, `Details`, `DrawChains`, `DrawRecentToolbar`, `DrawBatchChoices`, `HandleBatchChoiceClick`,
`DrawArmorSet`, `DrawArmorDefense`, `HandleArmorSetClick`, `ApplyFilters`/`SortLabel`/`CompareFilteredIndexes`,
the shared helpers (`Button`, `Box`, `Label`, `Text`, `CenterLabel`, `Icon`, `MouseTip`, `FlushTip`, colours);
`BookCategories.cs`, `BookTypography.cs`, `BookSkin.cs`, `BookUiState.cs`.
- Header: title + tabs + close. Remove "Recipes: N · Items: N" from the header (the result count stays in the
  grid footer). Storage tab only when Magic Storage is loaded.
- Filter bar under the header, same place on every catalogue tab: Search (with clear), Stage segmented
  control, Mod dropdown, Sort dropdown, Favorites toggle (star). Find out what the "R" button does: fold it
  into a dropdown/toggle with a clear label, or drop it.
- Categories: left sidebar as a vertical list of `icon + name` rows, wheel-scrollable, "All" first, selected
  row highlighted. No paged, unlabeled 3-column icon grid.
- Chain sample slot ("Place an item here") only on the Chains tab, in the top-left corner of the content area, compact (slot + one-line label). Owner: in the middle it looks "очень убого".
- Detail panel hierarchy: big icon + name; subtitle `<item's own mod> · Stage` (a vanilla item says Terraria even when Calamity changed its recipe — owner asked about Flying Carpet showing "Calamity Mod"); when the shown recipe comes from another mod than the item, say so in the recipe section (`Recipe: Calamity`); "Ingredients" rows (icon, name, `have/need`
  green when enough, red when not, "?" opens How to Get); "Station" rows; variant pager only when more than one
  recipe; batch chips ×1 ×5 ×10 Max; actions: primary Craft, secondary Track, Favorite as a star icon button.
- Footer: one short contextual hint + version. Window size controls stay but compact; drag the window by its
  header (no separate Move mode). Craft source as the segmented control (only when Magic Storage is present).

### Zone B — overlays and tracking (`ui/overlays`)
Owns the tracker code in `RecipeBookSystem.cs`: `StopTracker` … `DrawGoalMaterials`, `UpdateTrackerMaterials`,
`PostUpdatePlayers`, `ResetResourceTracker` … `DrawResourceTracker`, `SelectedTrackingGoal` …
`TrackingDetails`, `DrawTrackButton`, `HandleTrackButton`, `InsertTrackerLayer`; `BookTrackerRules.cs`,
`BookResourceTrackerRules.cs`, `BookTrackerDismiss.cs`, `BookTrackerSignal.cs`, `BookTrackingSelectionRules.cs`,
`BookResourceNeed.cs`, `BookResourceProgress.cs`; tracker settings in `BookConfig.cs`; the `Tracking*` and
`Track*Action` rects in `BookGeometry.cs`.
- **Mini overlay** (resources-only style) looks like vanilla info-accessory lines: under the minimap, stacked
  below whatever vanilla info lines are shown; each line = item icon + `Name have/need` in vanilla info text
  style (white text with shadow; complete lines tinted green). No panel, no header, no X, no drag, not
  interactive — like an equipped info accessory. Owner: "без всяких крестиков, просто как оверлей, как у
  аксессуара".
- **Bug**: the overlay clamps have to need (shows `2/2` while the inventory holds 10). Always show the real
  amount available (`10/2`), in both overlays and the Tracking tab.
- **Big overlay** (cards): compact list. Header `Tracking · n` (drag the window by it). One row per goal: icon +
  name + `crafts possible/wanted` + thin progress bar; clicking a row expands its materials (missing first).
  Remove goals from the book or by right-click on a row; no X on every card, no long hover tooltips.
- Tracking tab: same header controls as the other tabs; short empty state; `have/need` everywhere.
- Config labels/tooltips for tracker settings follow the copy rules.

### Zone C — secondary surfaces (`ui/secondary`)
Owns `BookStorage*.cs` texts and `DrawStorageToolbar`, `DrawStorageDetails`; the How to Get card
(`CloseAcquisition` … `DrawAcquisition`); the enemy card (`CanOpenEnemy` … `DrawEnemyCard`); Help
(`DrawHelp`, `HelpKeycap`, `BookHelp.cs`) and the mod menu; `Localization/*.hjson` (config labels and
tooltips, keybind names); chat/status messages; the `Storage*`, `Acquisition*`, `Enemy*`, `Help*`, `ModMenu*`
rects in `BookGeometry.cs`.
- Shorten all copy per §1, align controls with §3.
- Help (F1): compact cheat sheet, two columns, keycaps aligned; Esc and F1 both close it (Esc does not today).
- Config tooltips: one or two short lines each (today some are a paragraph).

## 5. Test stand (each zone has its own instance)

`<stand>\stand.ps1`, PowerShell. Instance A = 1, B = 2, C = 3. Always pass both
`-Instance <n>` and `-Project <your worktree>\ModernRecipeBook.csproj`.

```
$s = '<stand>\stand.ps1'; $p = '<worktree>\ModernRecipeBook.csproj'
& $s restart -Instance 1 -Project $p          # stop + build + start, ~25-45 s, prints state when in world
& $s c -Instance 1 click x=960 y=400 b=middle # open the book (middle click)
& $s c -Instance 1 shot name=a-items          # -> TerrariaStand\shots-1\a-items.png (Read it to look)
& $s c -Instance 1 mouse x=800 y=265          # hover (tooltips)
& $s c -Instance 1 key k=LeftControl+F        # keys; text input: c text s="wooden sword" (needs focused field)
& $s c -Instance 1 give item=Wood stack=50    # modded items: item=CalamityMod/PearlShard
& $s c -Instance 1 config type=BookConfig field=ResourceOnlyTracker v=true
& $s c -Instance 1 call p=Terraria.Localization.LanguageManager.Instance.SetLanguage a=ru-RU   # RU texts
& $s c -Instance 1 get p=ModernRecipeBook.RecipeBookSystem.Open                                 # read state
& $s stop -Instance 1
```
Coordinates are screenshot pixels (1920×1080, UI scale 1.1). Escape closes the book; Enter opens chat —
avoid both while scripting. The window opens behind other windows and does not take focus.

Worktrees: A `ModSources\.wt\book\ModernRecipeBook`, B `ModSources\.wt\overlays\ModernRecipeBook`, C `ModSources\.wt\secondary\ModernRecipeBook` (the folder must be named ModernRecipeBook — tModLoader takes the mod name from it). First build of an instance takes ~90 s.

Verify every screen you touched in EN and RU with screenshots, and check that nothing overlaps, clips or
shrinks to unreadable size. Build must have 0 errors. Report: what changed, screenshots (paths), open issues.
