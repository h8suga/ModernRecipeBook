using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ModernRecipeBook.UI.Book;

// Quick search: Ctrl+F anywhere (or the "Search Everything" key) opens one field over the game or the book that finds
// items, Bestiary entries, "Before: <boss>" stages, item and Bestiary categories, mods and tracked goals at once, grouped
// by kind, and opens a pick where it lives, through the book's history (docs/quick-search.md). Items are found by their
// names, then by what they are (a category, the buff they give, a trait, a class), then by their description; scopes
// narrow the list to one tab's view (Storage, Recipes, Tracking...). The index is built once per language and world; a
// keystroke ranks it (BookQuickRank). Drawing: RecipeBookSystem.QuickPalette.cs; the row and preview actions and the ">"
// commands: RecipeBookSystem.QuickActions.cs.
public sealed partial class RecipeBookSystem
{
	private enum QuickKind
	{
		Header,
		Item,
		Mob,
		Stage,
		ItemCategory,
		MobCategory,
		Mod,
		Goal,
		Command,
		ShowAll,
		TextSearch,
		AllScopes
	}

	// The order on a tie. Storage, Recipes and Bosses list only in their scope; the last four list while the field is empty.
	private enum QuickGroup
	{
		Items,
		Mobs,
		Stages,
		ItemCategories,
		MobCategories,
		Mods,
		Goals,
		Storage,
		Recipes,
		Bosses,
		Commands,
		Tracked,
		Opened,
		Crafted,
		Seen
	}

	// The chips under the field: All, then one per tab view of the results (Tab / Shift+Tab, a click).
	private enum QuickScope
	{
		All,
		Items,
		Storage,
		Recipes,
		Bestiary,
		Bosses,
		Tracking,
		Categories
	}

	// A row under a direct match.
	private enum QuickLink
	{
		None,
		DroppedBy,
		FoundIn,
		Drops,
		AllSources,
		AllLoot
	}

	// Why a row is listed: its name (Hint: the English name, an ID, the internal name when the name itself did not
	// match), what it is (Hint: "Gives: Ironskin", "In Potions › Buff potions") or its description (Hint: the line).
	private enum QuickWhy
	{
		Name,
		Concept,
		Description
	}

	// Id: item type, Bestiary index, step, category or tag index, QuickMods index, TrackedGoals index, QuickCommand. Count:
	// a header's, a "show all" row's or an "all sources" row's total.
	private readonly record struct QuickRow(QuickKind Kind, QuickGroup Group, int Id, QuickLink Link = QuickLink.None, int Count = 0, string Hint = "", QuickWhy Why = QuickWhy.Name);

	// Fields are normalized: the name, the English name ("" when the same or unknown), then an internal name, the ID and
	// the mod. All joins them, so most entries are passed over with one IndexOf. Weight ranks equal matches: how much the
	// item is used, a met mob, a big category.
	private sealed record QuickEntry(QuickKind Kind, int Id, string[] Fields, int Weight, string English, string Internal, string All);

	private enum QuickConceptKind
	{
		Category,
		MobCategory,
		Buff,
		Trait,
		Class
	}

	// What an item is, found by name: a sidebar category (with its short forms, QuickSynonyms), the buff it gives, a
	// trait, a class. Members: item types, mob indexes for a Bestiary category. Why: the row's reason.
	private sealed record QuickConcept(QuickConceptKind Kind, int Id, string[] Fields, int[] Members, string Why);

	internal static bool QuickSearchOpen { get; private set; }

	private static readonly object QuickSearchOwner = new object();

	private static string QuickQuery = "";

	// The kept query is selected when the field opens: typing replaces it, an arrow key keeps it.
	private static bool QuickAllSelected;

	private static bool QuickRequested;

	private static bool QuickEverOpened;

	private static KeyboardState QuickHotkeyKeys;

	private static KeyboardState QuickLastKeys;

	private static bool QuickLastLeft;

	private static bool QuickLastRight;

	private static int QuickLastWheel;

	private static Vector2 QuickLastMouse;

	private static Keys QuickRepeatKey;

	private static int QuickRepeatTicks;

	private static long QuickOpenedAt;

	private static QuickScope QuickScopeShown;

	// Matches per scope, counted on every pass whatever the scope.
	private static readonly int[] QuickScopeCounts = new int[8];

	private static readonly List<QuickRow> QuickRows = new List<QuickRow>();

	private static int QuickSelected = -1;

	// Updates the selection has rested: the preview reads its sources once it has (QuickLinkDelay).
	private static int QuickSelectedAge;

	private static int QuickScroll;

	private static readonly HashSet<QuickGroup> QuickExpanded = new HashSet<QuickGroup>();

	// The category names in item and mob rows, as drawn last frame: a click opens that category.
	private static readonly List<(BookRect Rect, int Category, bool Mobs)> QuickSegments = new List<(BookRect, int, bool)>();

	private static readonly List<QuickEntry> QuickIndex = new List<QuickEntry>();

	// The index position of every item type and Bestiary entry (-1: not listed), for concept members.
	private static int[] QuickItemEntry = Array.Empty<int>();

	private static int[] QuickMobEntry = Array.Empty<int>();

	// Per Bestiary index: its flavor text, normalized and as shown.
	private static (string Text, string Shown)[] QuickFlavor = Array.Empty<(string, string)>();

	private static readonly List<QuickConcept> QuickConcepts = new List<QuickConcept>();

	private static (string Culture, int Items, int Version, bool English) QuickConceptKey;

	private static readonly List<string> QuickMods = new List<string>();

	// What category and mod rows say after their name, counted with the index rather than every frame.
	private static int[] QuickCategorySizes = Array.Empty<int>();

	private static readonly Dictionary<string, (int Items, int Mobs)> QuickModSizes = new Dictionary<string, (int, int)>(StringComparer.Ordinal);

	private static (string Culture, int Items, int Mobs, int Steps, int Categories, bool English) QuickIndexKey;

	// Per item type: it has a recipe (the Recipes scope lists it).
	private static bool[] QuickHasRecipe = Array.Empty<bool>();

	private static readonly Dictionary<(QuickKind, int), List<QuickRow>> QuickLinks = new Dictionary<(QuickKind, int), List<QuickRow>>();

	// Links not read yet are read once the query rests (QuickLinkDelay updates), one match's per update: reading a match's
	// sources or loot the first time can take tens of ms (condition probes), and "w", "wo", "woo" need none of them.
	private static bool QuickLinksPending;

	private static int QuickQueryAge;

	private const int QuickLinkDelay = 8;

	// One pass's matches by index position: the rank (QuickTier), the pass that set it, the concept that listed it.
	private static int[] QuickRank = Array.Empty<int>();

	private static int[] QuickStamp = Array.Empty<int>();

	private static int[] QuickConceptOf = Array.Empty<int>();

	private static int QuickPass;

	private static readonly List<int> QuickHits = new List<int>();

	// The last pass's terms and what it read: typing on (the same terms, the last one longer, or one more) can only
	// narrow it, so the next pass reads only its matches.
	private static string[] QuickHitTerms = Array.Empty<string>();

	private static (bool Texts, bool English, int Index) QuickHitMode;

	private static readonly List<int> QuickCandidates = new List<int>();

	// Picks from the quick search this session, newest first: what the empty field offers.
	private static readonly List<(QuickKind Kind, int Id, string Key)> QuickOpened = new List<(QuickKind, int, string)>();

	// Read when the field opens: crafted or seen lately, and obtainable before the next boss, rank a little higher.
	private static readonly HashSet<int> QuickRecentItems = new HashSet<int>();

	private static readonly HashSet<int> QuickRecentMobs = new HashSet<int>();

	private static bool[] QuickStageNow = Array.Empty<bool>();

	private static readonly Dictionary<string, Asset<Texture2D>> QuickModIcons = new Dictionary<string, Asset<Texture2D>>(StringComparer.Ordinal);

	// Stand: the last index build and the last ranking pass, in milliseconds.
	internal static double QuickIndexMs { get; private set; }

	internal static double QuickSearchMs { get; private set; }

	// Stand: the popup's last frame and its slowest since it opened (drawing, with the ranking a keystroke runs), and the
	// slowest ranking pass since then, in milliseconds.
	internal static double QuickFrameMs { get; private set; }

	internal static double QuickFrameMaxMs { get; private set; }

	internal static double QuickSearchMaxMs { get; private set; }

	// Rows per group in All: Items, Bestiary, stages, item and Bestiary categories, mods, goals.
	private static readonly int[] QuickCaps = { 5, 4, 3, 3, 3, 2, 3 };

	// Rows a scope lists before "All N on the tab".
	private const int QuickScopeCap = 60;

	private const int QuickLinkCap = 3;

	private const int QuickRecentCap = 8;

	// Rank tiers (QuickRank): a name (a word of it starts with the query, or better; an ID, an internal name or a mod),
	// what the entry is, a name that only contains the query, then the description. BookQuickRank's score is added within.
	private const int QuickStrongName = 48;

	private const int QuickConceptTier = 32;

	private const int QuickWeakName = 16;

	private const int QuickDescribed = 1;

	// The tiles vanilla gives a buff for a right click (Player.TileInteractionsUse): Crystal Ball, Slice of Cake, War Table,
	// Sharpening Station, Bewitching Table, Ammo Box. Their items are found by the buff's name.
	private static readonly (int Tile, int Buff)[] QuickBuffTiles = { (TileID.CrystalBall, BuffID.Clairvoyance), (TileID.SliceOfCake, BuffID.SugarRush), (TileID.WarTable, BuffID.WarTable), (TileID.SharpeningStation, BuffID.Sharpened), (TileID.BewitchingTable, BuffID.Bewitched), (TileID.AmmoBox, BuffID.AmmoBox) };

	private static readonly FieldInfo QuickFlavorKey = typeof(FlavorTextBestiaryInfoElement).GetField("_key", BindingFlags.Instance | BindingFlags.NonPublic);

	// The "Search Everything" key (BookPlayer.ProcessTriggers): opens on the next UI update, as Ctrl+F does.
	internal static void RequestQuickSearch()
	{
		QuickRequested = true;
	}

	// Not over a text field someone else is typing in (chat, a sign, a chest's name, another mod's field), a menu, the
	// full map or the main menu. The book's own search is fine: Ctrl+F there opens the quick search instead.
	private static bool QuickSearchAllowed()
	{
		if (Main.gameMenu || Main.mapFullscreen || Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.ingameOptionsWindow || Main.inFancyUI || Main.LocalPlayer == null || !Main.LocalPlayer.active)
		{
			return false;
		}
		object taker = Main.CurrentInputTextTakerOverride;
		if (taker != null && taker != SearchOwner && taker != QuickSearchOwner)
		{
			return false;
		}
		// The UI update runs before the game reads input, so WritingText still says whether a field took text last frame.
		if (PlayerInput.WritingText && !Typing && !QuickSearchOpen)
		{
			return false;
		}
		return !Main.blockInput || OwnInputBlock;
	}

	// Every UI update: the shortcut, then the popup's own input. True while it is open: the book under it takes none.
	private static bool UpdateQuickSearch()
	{
		if (QuickEverOpened && BookEnglishNames.Pump() && QuickSearchOpen)
		{
			RunQuickSearch();
		}
		KeyboardState keys = Keyboard.GetState();
		bool pressed = BookHotkeys.QuickSearch(keys, QuickHotkeyKeys) || QuickRequested;
		QuickHotkeyKeys = keys;
		QuickRequested = false;
		if (pressed && QuickSearchAllowed())
		{
			OpenQuickSearch();
			return true;
		}
		if (!QuickSearchOpen)
		{
			return false;
		}
		if (Main.mapFullscreen || Main.drawingPlayerChat || Main.editSign || Main.editChest)
		{
			CloseQuickSearch();
			return false;
		}
		WarmQuickPaths();
		// The book's update does not run under the popup: what can be crafted now, the description texts and the
		// traits still follow, so the rows' marks and the description matches are those of this moment.
		BookCrafting.Refresh();
		bool texts = DescriptionsReady;
		WarmDescriptions();
		UpdateTraits();
		if (!texts && DescriptionsReady && QuickQuery.Length > 0)
		{
			RunQuickSearch();
		}
		if (QuickLinksPending && ++QuickQueryAge >= QuickLinkDelay)
		{
			RefreshQuickLinks();
		}
		if (++QuickSelectedAge >= QuickLinkDelay)
		{
			ReadQuickPreview();
		}
		HandleQuickInput(keys);
		return QuickSearchOpen;
	}

	// The same rows with one more match's links, the selection and the scroll kept.
	private static void RefreshQuickLinks()
	{
		QuickRow? selected = (QuickSelected >= 0 && QuickSelected < QuickRows.Count) ? QuickRows[QuickSelected] : null;
		int scroll = QuickScroll;
		RunQuickSearch(linkReads: 1);
		KeepQuickSelection(selected, scroll);
	}

	private static void KeepQuickSelection(QuickRow? selected, int scroll)
	{
		int at = selected.HasValue ? QuickRows.IndexOf(selected.Value) : (-1);
		if (at >= 0)
		{
			QuickSelected = at;
		}
		QuickScroll = Math.Min(scroll, QuickMaxScroll());
	}

	// BookCategoryPath counts a category the first time an item in it asks (a few ms each with Calamity): every item asks
	// in 2 ms slices while the field is open, so a row never waits for it.
	private static int QuickWarmNext;

	private static void WarmQuickPaths()
	{
		// The world's tile sources, which an item's links read (43 ms the first time in a world), on the update after the
		// field opens rather than on the first keystroke.
		BookWorldSources.For(0);
		long end = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 2 / 1000;
		while (QuickWarmNext < AllItems.Count && Stopwatch.GetTimestamp() < end)
		{
			BookCategoryPath.Of(AllItems[QuickWarmNext++]);
		}
	}

	private static void OpenQuickSearch()
	{
		// Pressed again while open: the query is selected again, as a browser's Ctrl+F does.
		if (QuickSearchOpen)
		{
			QuickAllSelected = QuickQuery.Length > 0;
			return;
		}
		if (Dirty || AllItems.Count == 0)
		{
			Rebuild();
		}
		EnsureBestiary();
		SetTyping(value: false);
		ModMenu = (SortMenu = (StageMenu = (StationMenu = (TraitMenu = false))));
		BookPlayer.RestoreSmartCursor();
		QuickSearchOpen = true;
		QuickEverOpened = true;
		QuickAllSelected = QuickQuery.Length > 0;
		QuickExpanded.Clear();
		Main.clrInput();
		HoldTextInput(QuickSearchOwner, value: true);
		QuickLastKeys = Keyboard.GetState();
		QuickLastLeft = Main.mouseLeft;
		QuickLastRight = Main.mouseRight;
		QuickLastWheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		QuickLastMouse = Main.MouseScreen;
		QuickRepeatKey = Keys.None;
		QuickOpenedAt = Stopwatch.GetTimestamp();
		QuickStatusAt = 0;
		ReadQuickBoosts();
		QuickHitTerms = Array.Empty<string>();
		RunQuickSearch();
		QuickFrameMaxMs = (QuickSearchMaxMs = 0);
	}

	private static void CloseQuickSearch()
	{
		if (!QuickSearchOpen)
		{
			return;
		}
		QuickSearchOpen = false;
		QuickArmed = -1;
		HoldTextInput(QuickSearchOwner, value: false);
		// What is still held (the Esc or Enter that closed it, the clicking button) reaches the game only once let go:
		// Esc would toggle the inventory, a held click swing the held item.
		BookPlayer.HoldPressedKeys();
		// The Enter that opened a pick would open the chat later in this update (Main.DoUpdate_Enter_ToggleChat waits for
		// chatRelease, set again once Enter is let go).
		if (Keyboard.GetState().IsKeyDown(Keys.Enter))
		{
			Main.chatRelease = false;
		}
		Main.clrInput();
		SyncInputEdges();
	}

	// Leaving a world: the index ranks met mobs and reads this world's loot, so it is built again in the next one.
	private static void ResetQuickWorld()
	{
		QuickIndex.Clear();
		QuickConcepts.Clear();
		QuickConceptKey = default;
		QuickLinks.Clear();
		QuickFacts.Clear();
		QuickContents.Clear();
		QuickHitTerms = Array.Empty<string>();
	}

	// The crafts and sightings that boost a match a little, and what this world can get before its next boss.
	private static void ReadQuickBoosts()
	{
		QuickRecentItems.Clear();
		foreach (int index in BookRecent.ResolveRecipeIndexes().Take(24))
		{
			QuickRecentItems.Add(Main.recipe[index].createItem.type);
		}
		QuickRecentMobs.Clear();
		foreach (string credit in SeenMobs.Newest.Take(24))
		{
			int index = BookBestiary.IndexOfCredit(credit);
			if (index >= 0)
			{
				QuickRecentMobs.Add(index);
			}
		}
		if (QuickStageNow.Length != ItemLoader.ItemCount)
		{
			QuickStageNow = new bool[ItemLoader.ItemCount];
		}
		int next = BookSteps.Next();
		foreach (int type in AllItems)
		{
			int step = BookProgression.ItemStep(type);
			QuickStageNow[type] = step >= 0 && (next < 0 || step < next);
		}
	}

	private static void HandleQuickInput(KeyboardState keys)
	{
		Layout();
		if (!Open)
		{
			MouseOver = false;
		}
		Main.LocalPlayer.mouseInterface = true;
		PlayerInput.LockVanillaMouseScroll("ModernRecipeBook");
		KeyboardState last = QuickLastKeys;
		QuickLastKeys = keys;
		bool lastLeft = QuickLastLeft;
		QuickLastLeft = Main.mouseLeft;
		bool lastRight = QuickLastRight;
		QuickLastRight = Main.mouseRight;
		int wheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		int wheel = wheelValue - QuickLastWheel;
		QuickLastWheel = wheelValue;
		bool moved = Main.MouseScreen != QuickLastMouse;
		QuickLastMouse = Main.MouseScreen;
		bool ctrl = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
		bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
		bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
		// Esc closes it as the book's top layer; the query and the scope stay for next time.
		if (Press(Keys.Escape))
		{
			CloseQuickSearch();
			return;
		}
		if (ctrl && !alt && Press(Keys.A))
		{
			QuickAllSelected = QuickQuery.Length > 0;
		}
		if (QuickAllSelected && !ctrl && (Press(Keys.Back) || Press(Keys.Delete)))
		{
			SetQuickQuery("");
		}
		else if (QuickQuery.Length == 0 && QuickScopeShown != QuickScope.All && Press(Keys.Back))
		{
			// Backspace in an empty field takes the scope chip off, as a palette's filter token goes.
			SetQuickScope(QuickScope.All);
		}
		if (Press(Keys.Left) || Press(Keys.Right) || Press(Keys.Home) || Press(Keys.End))
		{
			QuickAllSelected = false;
		}
		if (Press(Keys.Tab) && !ctrl && !alt && !QuickCommandMode)
		{
			CycleQuickScope(shift ? (-1) : 1);
		}
		int step = Repeat(Keys.Down) - Repeat(Keys.Up) + 8 * (Repeat(Keys.PageDown) - Repeat(Keys.PageUp));
		if (step != 0)
		{
			MoveQuickSelection(step);
		}
		QuickRow? selected = (QuickSelected >= 0 && QuickSelected < QuickRows.Count) ? QuickRows[QuickSelected] : null;
		if (Press(Keys.Enter) && !alt)
		{
			if (selected.HasValue)
			{
				// Ctrl+Enter: the row's own action, as in the card (craft) and on Storage (take); Shift+Enter: Used in.
				if (ctrl)
				{
					RunQuickAction(selected.Value, shift ? QuickAction.CraftMax : QuickPrimaryKeyAction(selected.Value));
				}
				else if (shift && QuickItemOf(selected.Value) > 0)
				{
					RunQuickAction(selected.Value, QuickAction.UsedIn);
				}
				else
				{
					ActivateQuickRow(selected.Value);
				}
			}
			return;
		}
		// The card's chords on the selected row: Alt+T track, Alt+F favorite, Alt+G How to Get, Alt+U Used in (a mob's stats).
		if (alt && !ctrl && !shift && selected.HasValue)
		{
			QuickAction chord = Press(Keys.T) ? QuickAction.Track : (Press(Keys.F) ? QuickAction.Favorite : (Press(Keys.G) ? QuickAction.HowToGet : (Press(Keys.U) ? QuickAction.UsedIn : QuickAction.None)));
			if (chord != QuickAction.None)
			{
				BookPlayer.HoldNewKeys(keys, last);
				RunQuickAction(selected.Value, (chord == QuickAction.UsedIn && selected.Value.Kind == QuickKind.Mob) ? QuickAction.Stats : chord);
				return;
			}
		}
		BookRect panel = QuickPanel;
		int hovered = QuickRowAt();
		if (moved && hovered >= 0 && hovered != QuickSelected)
		{
			SelectQuickRow(hovered);
		}
		if (wheel != 0 && QuickList.Contains(Mouse.X, Mouse.Y))
		{
			QuickScroll = Math.Clamp(QuickScroll + ((wheel < 0) ? 3 : (-3)), 0, QuickMaxScroll());
		}
		bool click = Main.mouseLeft && !lastLeft;
		bool rightClick = Main.mouseRight && !lastRight;
		if (!click && !rightClick)
		{
			return;
		}
		if (!panel.Contains(Mouse.X, Mouse.Y))
		{
			if (click)
			{
				CloseQuickSearch();
			}
			return;
		}
		// Buttons first: a row's own and the preview's; they never open the row under them.
		foreach ((BookRect rect, QuickAction action, int row) in QuickButtons)
		{
			if (rect.Contains(Mouse.X, Mouse.Y) && row >= 0 && row < QuickRows.Count)
			{
				QuickAction run = action;
				if (rightClick)
				{
					// Right click on Take takes one, as in the Storage grid.
					if (action != QuickAction.Take)
					{
						return;
					}
					run = QuickAction.TakeOne;
				}
				else if (shift && action == QuickAction.Craft)
				{
					run = QuickAction.CraftMax;
				}
				RunQuickAction(QuickRows[row], run);
				return;
			}
		}
		if (!click)
		{
			return;
		}
		foreach ((BookRect rect, QuickScope scope) in QuickChips)
		{
			if (rect.Contains(Mouse.X, Mouse.Y))
			{
				SetQuickScope(scope);
				return;
			}
		}
		if (QuickQuery.Length > 0 && QuickClear.Contains(Mouse.X, Mouse.Y))
		{
			SetQuickQuery("");
			return;
		}
		if (QuickField.Contains(Mouse.X, Mouse.Y))
		{
			QuickAllSelected = false;
			return;
		}
		foreach ((BookRect rect, int category, bool mobs) in QuickSegments)
		{
			if (rect.Contains(Mouse.X, Mouse.Y))
			{
				ActivateQuickRow(new QuickRow(mobs ? QuickKind.MobCategory : QuickKind.ItemCategory, mobs ? QuickGroup.MobCategories : QuickGroup.ItemCategories, category));
				return;
			}
		}
		if (hovered >= 0)
		{
			QuickRow row = QuickRows[hovered];
			if (shift && QuickItemOf(row) > 0)
			{
				RunQuickAction(row, QuickAction.UsedIn);
			}
			else
			{
				ActivateQuickRow(row);
			}
		}
		bool Press(Keys key)
		{
			return keys.IsKeyDown(key) && !last.IsKeyDown(key);
		}
		// Held arrows repeat after a third of a second, as text fields do.
		int Repeat(Keys key)
		{
			if (!keys.IsKeyDown(key))
			{
				return 0;
			}
			if (!last.IsKeyDown(key))
			{
				QuickRepeatKey = key;
				QuickRepeatTicks = 0;
				return 1;
			}
			if (QuickRepeatKey != key)
			{
				return 0;
			}
			QuickRepeatTicks++;
			return (QuickRepeatTicks >= 20 && (QuickRepeatTicks - 20) % 3 == 0) ? 1 : 0;
		}
	}

	private static void SetQuickQuery(string value)
	{
		QuickQuery = value;
		QuickAllSelected = false;
		QuickQueryAge = 0;
		QuickArmed = -1;
		RunQuickSearch();
	}

	// A ">" first lists the book's commands instead.
	private static bool QuickCommandMode => QuickQuery.TrimStart().StartsWith(">", StringComparison.Ordinal);

	private static void SetQuickScope(QuickScope scope)
	{
		if (scope == QuickScopeShown)
		{
			return;
		}
		QuickScopeShown = scope;
		QuickExpanded.Clear();
		RunQuickSearch();
	}

	// Tab / Shift+Tab: the next chip that lists something (All always does).
	private static void CycleQuickScope(int direction)
	{
		int count = QuickScopeCounts.Length;
		int at = (int)QuickScopeShown;
		for (int i = 0; i < count; i++)
		{
			at = (at + direction + count) % count;
			if (QuickScopeVisible((QuickScope)at))
			{
				SetQuickScope((QuickScope)at);
				return;
			}
		}
	}

	// A chip shows while its scope finds something, and the one in use always; the empty field lists recent things in All.
	private static bool QuickScopeVisible(QuickScope scope)
	{
		return scope == QuickScope.All || scope == QuickScopeShown || (QuickQuery.Trim().Length > 0 && QuickScopeCounts[(int)scope] > 0);
	}

	// The field reads typed text while drawing, as the book's search does (the IME takes text only then).
	private static void ReadQuickText()
	{
		PlayerInput.WritingText = true;
		Main.CurrentInputTextTakerOverride = QuickSearchOwner;
		Main.instance.HandleIME();
		KeyboardState keys = Keyboard.GetState();
		bool ctrl = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
		// A selected query: what is typed replaces it; Ctrl+V pastes over it, Ctrl+C copies all of it.
		string next = Main.GetInputText((QuickAllSelected && !ctrl) ? "" : QuickQuery).Replace("\r", "").Replace("\n", "").Replace("\t", "");
		if (QuickAllSelected)
		{
			if (ctrl && next.Length > QuickQuery.Length && next.StartsWith(QuickQuery, StringComparison.Ordinal))
			{
				next = next.Substring(QuickQuery.Length);
			}
			else if (!ctrl && next.Length == 0)
			{
				next = QuickQuery;
			}
		}
		if (next.Length > 100)
		{
			next = next.Substring(0, 100);
		}
		if (next != QuickQuery)
		{
			SetQuickQuery(next);
		}
	}

	// The index ------------------------------------------------------------------------------------------------------

	private static void EnsureQuickIndex()
	{
		(string, int, int, int, int, bool) key = (Language.ActiveCulture.Name, AllItems.Count, BookBestiary.Mobs.Count, BookSteps.All.Count, ItemCategoryList.Count, BookEnglishNames.Ready);
		if (key == QuickIndexKey && QuickIndex.Count > 0)
		{
			EnsureQuickConcepts();
			return;
		}
		long start = Stopwatch.GetTimestamp();
		QuickIndexKey = key;
		QuickIndex.Clear();
		QuickMods.Clear();
		QuickModSizes.Clear();
		QuickLinks.Clear();
		QuickFacts.Clear();
		QuickHitTerms = Array.Empty<string>();
		Dictionary<string, string> modNames = new Dictionary<string, string>(StringComparer.Ordinal);
		QuickItemEntry = Enumerable.Repeat(-1, ItemLoader.ItemCount).ToArray();
		QuickMobEntry = Enumerable.Repeat(-1, BookBestiary.Mobs.Count).ToArray();
		QuickHasRecipe = new bool[ItemLoader.ItemCount];
		// How often an item is made or used in a recipe: Wood before Wood Fence for "wood".
		int[] uses = new int[ItemLoader.ItemCount];
		foreach (int index in All)
		{
			Recipe recipe = Main.recipe[index];
			Use(recipe.createItem.type);
			if (recipe.createItem.type > 0 && recipe.createItem.type < QuickHasRecipe.Length)
			{
				QuickHasRecipe[recipe.createItem.type] = true;
			}
			foreach (Item ingredient in recipe.requiredItem)
			{
				Use(ingredient.type);
			}
		}
		foreach (int type in AllItems)
		{
			Item item = ContentSamples.ItemsByType[type];
			string mod = item.ModItem?.Mod.Name ?? "Terraria";
			QuickModSizes[mod] = (QuickModSizes.GetValueOrDefault(mod).Items + 1, 0);
			string internalName = item.ModItem?.Name ?? ItemID.Search.GetName(type) ?? "";
			string english = EnglishOf(Lang.GetItemName(type).Key, item.Name);
			QuickItemEntry[type] = QuickIndex.Count;
			Add(QuickKind.Item, type, new string[5] { N(item.Name), N(english), N(BookQuickRank.Words(internalName)), type.ToString(CultureInfo.InvariantCulture), ModField(mod) }, Bucket(uses[type]), english, internalName);
		}
		QuickFlavor = new (string, string)[BookBestiary.Mobs.Count];
		foreach (BookMob mob in BookBestiary.Mobs)
		{
			NPC npc = mob.Sample;
			(int items, int mobs) = QuickModSizes.GetValueOrDefault(mob.Mod);
			QuickModSizes[mob.Mod] = (items, mobs + 1);
			string persistent = ContentSamples.NpcPersistentIdsByNetIds[mob.NetId];
			string english = EnglishOf(Lang.GetNPCName(mob.NetId).Key, npc.FullName);
			int weight = (MobMet(mob.NetId) ? 2 : 0) + ((mob.Kind is BookMobKind.Boss or BookMobKind.MiniBoss) ? 1 : 0);
			QuickMobEntry[mob.Index] = QuickIndex.Count;
			Add(QuickKind.Mob, mob.Index, new string[5] { N(npc.FullName), N(english), N(BookQuickRank.Words(persistent)), mob.NetId.ToString(CultureInfo.InvariantCulture), ModField(mob.Mod) }, weight, english, persistent);
			string flavor = MobFlavor(mob.NetId);
			QuickFlavor[mob.Index] = (N(flavor), flavor);
		}
		// A stage is found by the boss's name, the English one and its parts' names ("Ares" finds the Exo Mechs).
		IReadOnlyList<BookSteps.Step> steps = BookSteps.All;
		for (int k = 1; k < steps.Count; k++)
		{
			int head = BookSteps.HeadNpc(k, out _);
			string english = (head != 0) ? EnglishOf(Lang.GetNPCName(head).Key, BookSteps.Name(k)) : "";
			string parts = string.Join(" ", steps[k].Npcs.Where(ContentSamples.NpcsByNetId.ContainsKey).Select((int npc) => Lang.GetNPCNameValue(npc)).Distinct());
			Add(QuickKind.Stage, k, new string[4] { N(BookSteps.Name(k)), N(english), N(parts), N(BookQuickRank.Words(steps[k].Key)) }, 0, english, "");
		}
		int[] sizes = (QuickCategorySizes = new int[ItemCategoryList.Count]);
		foreach (HashSet<int> matches in ItemCategories.Values)
		{
			foreach (int c in matches)
			{
				sizes[c]++;
			}
		}
		for (int c = 1; c < ItemCategoryList.Count; c++)
		{
			BookCategory category = ItemCategoryList[c];
			Add(QuickKind.ItemCategory, c, new string[4] { N(category.Label()), N(EnglishOf(category.LabelKey, category.Label())), N(category.Key), N(QuickSynonyms(category.Key)) }, Bucket(sizes[c]), "", category.Key);
		}
		for (int t = 0; t < BookBestiary.Tags.Count; t++)
		{
			BookBestiary.Tag tag = BookBestiary.Tags[t];
			if (tag.Key == "all")
			{
				continue;
			}
			string label = BookBestiary.Label(tag);
			string english = EnglishOf(tag.LabelKey, label);
			Add(QuickKind.MobCategory, t, new string[3] { N(label), N(english), N(BookQuickRank.Words(tag.Key)) }, Bucket(tag.Members.Count), english, "");
		}
		QuickMods.AddRange(ItemMods.Concat(MobMods).Distinct());
		for (int m = 0; m < QuickMods.Count; m++)
		{
			Add(QuickKind.Mod, m, new string[3] { ModField(QuickMods[m]), "", N(BookQuickRank.Words(QuickMods[m])) }, Bucket(QuickModSizes.GetValueOrDefault(QuickMods[m]).Items), "", QuickMods[m]);
		}
		QuickRank = new int[QuickIndex.Count];
		QuickStamp = new int[QuickIndex.Count];
		QuickConceptOf = new int[QuickIndex.Count];
		QuickConceptKey = default;
		EnsureQuickConcepts();
		QuickIndexMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
		void Use(int type)
		{
			if (type > 0 && type < uses.Length)
			{
				uses[type]++;
			}
		}
		string ModField(string mod)
		{
			if (!modNames.TryGetValue(mod, out string name))
			{
				name = (modNames[mod] = N(ModName(mod)));
			}
			return name;
		}
		static void Add(QuickKind kind, int id, string[] fields, int weight, string english, string internalName)
		{
			QuickIndex.Add(new QuickEntry(kind, id, fields, weight, english, internalName, string.Join("\n", fields)));
		}
		static int Bucket(int count)
		{
			return Math.Min(6, (int)Math.Log2(1 + count));
		}
	}

	private static string N(string value)
	{
		return string.IsNullOrEmpty(value) ? "" : BookRules.SearchText(value);
	}

	// The Bestiary's flavor text of a mob (its entry's FlavorTextBestiaryInfoElement), "" when it has none.
	private static string MobFlavor(int netId)
	{
		try
		{
			foreach (IBestiaryInfoElement element in MobEntry(netId).Info)
			{
				if (element is FlavorTextBestiaryInfoElement flavor && QuickFlavorKey?.GetValue(flavor) is string key)
				{
					string text = Language.GetTextValue(key);
					return (text == key) ? "" : BookRules.TagFree(text);
				}
			}
		}
		catch (Exception)
		{
		}
		return "";
	}

	// Concepts follow the traits (read from the descriptions after the world loads) and the English labels, so they have
	// their own key: a few ms, never the whole index again.
	private static void EnsureQuickConcepts()
	{
		(string, int, int, bool) key = (Language.ActiveCulture.Name, AllItems.Count, BookItemTraits.Version + (BookItemTraits.Ready ? 1 << 20 : 0), BookEnglishNames.Ready);
		if (key == QuickConceptKey && QuickConcepts.Count > 0)
		{
			return;
		}
		QuickConceptKey = key;
		QuickConcepts.Clear();
		QuickHitTerms = Array.Empty<string>();
		List<int>[] categoryMembers = Enumerable.Range(0, ItemCategoryList.Count).Select((int _) => new List<int>()).ToArray();
		Dictionary<int, List<int>> buffMembers = new Dictionary<int, List<int>>();
		Dictionary<BookTrait, List<int>> traitMembers = new Dictionary<BookTrait, List<int>>();
		Dictionary<BookClass, List<int>> classMembers = new Dictionary<BookClass, List<int>>();
		foreach (int type in AllItems)
		{
			if (ItemCategories.TryGetValue(type, out HashSet<int> categories))
			{
				foreach (int c in categories)
				{
					categoryMembers[c].Add(type);
				}
			}
			Item item = ContentSamples.ItemsByType[type];
			if (item.buffType > 0)
			{
				Member(buffMembers, item.buffType, type);
			}
			foreach ((int tile, int buff) in QuickBuffTiles)
			{
				if (item.createTile == tile)
				{
					Member(buffMembers, buff, type);
				}
			}
			BookTrait traits = BookItemTraits.Get(type);
			for (BookTrait trait = BookTrait.Homing; trait <= BookTrait.Sentry; trait = (BookTrait)((int)trait << 1))
			{
				if ((traits & trait) != 0)
				{
					Member(traitMembers, trait, type);
				}
			}
			BookClass classes = BookItemClasses.Get(type);
			for (BookClass value = BookClass.Melee; value <= BookClass.Rogue; value = (BookClass)((int)value << 1))
			{
				if ((classes & value) != 0)
				{
					Member(classMembers, value, type);
				}
			}
		}
		// Not All items: it would list every item for "all".
		for (int c = 1; c < ItemCategoryList.Count; c++)
		{
			BookCategory category = ItemCategoryList[c];
			string label = category.Label();
			QuickConcepts.Add(new QuickConcept(QuickConceptKind.Category, c, Words(label, EnglishOf(category.LabelKey, label), category.Key, QuickSynonyms(category.Key)), categoryMembers[c].ToArray(), L("QuickSearchDesign.WhyCategory", BookCategoryPath.Path(category, ItemCategoryList))));
		}
		for (int t = 0; t < BookBestiary.Tags.Count; t++)
		{
			BookBestiary.Tag tag = BookBestiary.Tags[t];
			if (tag.Key is "all" || tag.Key == BookBestiary.RecentKey)
			{
				continue;
			}
			string label = BookBestiary.Label(tag);
			QuickConcepts.Add(new QuickConcept(QuickConceptKind.MobCategory, t, Words(label, EnglishOf(tag.LabelKey, label)), tag.Members.ToArray(), L("QuickSearchDesign.WhyCategory", label)));
		}
		foreach ((int buff, List<int> members) in buffMembers)
		{
			string name = Lang.GetBuffName(buff);
			if (string.IsNullOrWhiteSpace(name))
			{
				continue;
			}
			string englishKey = (buff < BuffID.Count) ? ("BuffName." + BuffID.Search.GetName(buff)) : (BuffLoader.GetBuff(buff)?.DisplayName.Key ?? "");
			QuickConcepts.Add(new QuickConcept(QuickConceptKind.Buff, buff, Words(name, EnglishOf(englishKey, name)), members.ToArray(), L("QuickSearchDesign.WhyBuff", name)));
		}
		foreach ((BookTrait trait, List<int> members) in traitMembers)
		{
			string label = BookItemTraits.Label(trait);
			QuickConcepts.Add(new QuickConcept(QuickConceptKind.Trait, (int)trait, Words(label, EnglishOf(KeyOf("Traits.") + "Name" + trait, label)), members.ToArray(), L("QuickSearchDesign.WhyTrait", label)));
		}
		foreach ((BookClass value, List<int> members) in classMembers)
		{
			string label = BookItemClasses.Label(value);
			string labelKey = KeyOf("Classes.") + ((value == BookClass.Summon) ? "Summoner" : value.ToString());
			QuickConcepts.Add(new QuickConcept(QuickConceptKind.Class, (int)value, Words(label, EnglishOf(labelKey, label)), members.ToArray(), L("QuickSearchDesign.WhyClass", label)));
		}
		static string[] Words(params string[] fields)
		{
			// A comma separates short forms ("buff, buffs"): each one is its own field, so each can start the query.
			return fields.SelectMany((string field) => (field ?? "").Split(',')).Select((string field) => N(field.Trim())).Where((string field) => field.Length > 0).Distinct().ToArray();
		}
		static void Member<T>(Dictionary<T, List<int>> members, T key, int type)
		{
			if (!members.TryGetValue(key, out List<int> list))
			{
				list = (members[key] = new List<int>());
			}
			list.Add(type);
		}
	}

	// The English text of a name, "" when it reads the same or is not known (yet, or the game runs in English).
	private static string EnglishOf(string key, string shown)
	{
		string english = BookEnglishNames.Of(key);
		return (english == null || english == shown) ? "" : english;
	}

	private static QuickGroup QuickGroupOf(QuickKind kind)
	{
		return kind switch
		{
			QuickKind.Item => QuickGroup.Items,
			QuickKind.Mob => QuickGroup.Mobs,
			QuickKind.Stage => QuickGroup.Stages,
			QuickKind.ItemCategory => QuickGroup.ItemCategories,
			QuickKind.MobCategory => QuickGroup.MobCategories,
			_ => QuickGroup.Mods
		};
	}

	// The next boss of this world first, then the ones still alive.
	private static int StageWeight(int step)
	{
		BookSteps.Step boss = BookSteps.All[step];
		return (step == BookSteps.Next()) ? 3 : ((!boss.Available()) ? 0 : ((!BookSteps.SafeDowned(boss)) ? 2 : 1));
	}

	// Ranking -------------------------------------------------------------------------------------------------------

	// The groups a scope lists; All lists the first seven, a few rows each.
	private static bool QuickScopeLists(QuickScope scope, QuickGroup group)
	{
		return scope switch
		{
			QuickScope.All => group <= QuickGroup.Goals,
			QuickScope.Items => group == QuickGroup.Items,
			QuickScope.Storage => group == QuickGroup.Storage,
			QuickScope.Recipes => group == QuickGroup.Recipes,
			QuickScope.Bestiary => group == QuickGroup.Mobs,
			QuickScope.Bosses => group is QuickGroup.Stages or QuickGroup.Bosses,
			QuickScope.Tracking => group == QuickGroup.Goals,
			_ => group is QuickGroup.ItemCategories or QuickGroup.MobCategories
		};
	}

	// linkReads: how many matches may read their links now; the others wait (QuickLinksPending).
	private static void RunQuickSearch(int linkReads = 0)
	{
		long start = Stopwatch.GetTimestamp();
		QuickLinksPending = false;
		QuickRows.Clear();
		QuickSegments.Clear();
		QuickMarkCache.Clear();
		QuickScroll = 0;
		Array.Clear(QuickScopeCounts);
		EnsureQuickIndex();
		if (QuickCommandMode)
		{
			AddCommandRows(QuickQuery.TrimStart().Substring(1));
		}
		else
		{
			string[] terms = BookRules.SearchTerms(QuickQuery);
			if (terms.Length == 0)
			{
				AddRecentRows();
			}
			else
			{
				AddMatchRows(terms, ref linkReads);
			}
		}
		QuickSelected = QuickRows.FindIndex((QuickRow row) => row.Kind != QuickKind.Header);
		QuickSelectedAge = 0;
		QuickSearchMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
		QuickSearchMaxMs = Math.Max(QuickSearchMaxMs, QuickSearchMs);
	}

	private static void AddMatchRows(string[] terms, ref int linkReads)
	{
		string query = string.Join(" ", terms);
		FindQuickMatches(query, terms);
		// Only the rows a group shows are kept in order: "i" matches thousands of items, and sorting them all took a
		// frame's worth of time. Every group counts, so each chip shows its number whatever the scope.
		int groupCount = Enum.GetValues<QuickGroup>().Length;
		List<(int Position, int Rank, int Weight)>[] found = new List<(int, int, int)>[groupCount];
		int[] counts = new int[groupCount];
		for (int g = 0; g < groupCount; g++)
		{
			found[g] = new List<(int, int, int)>();
		}
		QuickScope scope = QuickScopeShown;
		foreach (int position in QuickHits)
		{
			QuickEntry entry = QuickIndex[position];
			int rank = QuickRank[position];
			int weight = QuickWeight(entry);
			QuickGroup group = QuickGroupOf(entry.Kind);
			Offer(group);
			if (entry.Kind == QuickKind.Item)
			{
				if (QuickOwned(entry.Id) > 0)
				{
					Offer(QuickGroup.Storage);
				}
				if (QuickHasRecipe[entry.Id])
				{
					Offer(QuickGroup.Recipes);
				}
			}
			else if (entry.Kind == QuickKind.Mob && BookBestiary.Mobs[entry.Id].Kind == BookMobKind.Boss)
			{
				Offer(QuickGroup.Bosses);
			}
			void Offer(QuickGroup g)
			{
				counts[(int)g]++;
				int cap = !QuickScopeLists(scope, g) ? 0 : (QuickExpanded.Contains(g) ? int.MaxValue : ((scope == QuickScope.All) ? QuickCaps[(int)g] : QuickScopeCap));
				if (cap == 0)
				{
					return;
				}
				List<(int Position, int Rank, int Weight)> best = found[(int)g];
				(int, int, int) match = (position, rank, weight);
				int at = best.Count;
				while (at > 0 && QuickOrder(match, best[at - 1]) < 0)
				{
					at--;
				}
				if (at < cap)
				{
					best.Insert(at, match);
					if (best.Count > cap)
					{
						best.RemoveAt(cap);
					}
				}
			}
		}
		List<int> goals = QuickGoalMatches(query, terms);
		counts[(int)QuickGroup.Goals] = goals.Count;
		QuickScopeCounts[(int)QuickScope.All] = counts.Take((int)QuickGroup.Goals + 1).Sum();
		QuickScopeCounts[(int)QuickScope.Items] = counts[(int)QuickGroup.Items];
		QuickScopeCounts[(int)QuickScope.Storage] = counts[(int)QuickGroup.Storage];
		QuickScopeCounts[(int)QuickScope.Recipes] = counts[(int)QuickGroup.Recipes];
		QuickScopeCounts[(int)QuickScope.Bestiary] = counts[(int)QuickGroup.Mobs];
		QuickScopeCounts[(int)QuickScope.Bosses] = counts[(int)QuickGroup.Stages] + counts[(int)QuickGroup.Bosses];
		QuickScopeCounts[(int)QuickScope.Tracking] = goals.Count;
		QuickScopeCounts[(int)QuickScope.Categories] = counts[(int)QuickGroup.ItemCategories] + counts[(int)QuickGroup.MobCategories];
		// One group alone (a scope of one kind) needs no header: the chip names it.
		int listed = Enumerable.Range(0, groupCount).Count((int g) => found[g].Count > 0 || (g == (int)QuickGroup.Goals && goals.Count > 0 && QuickScopeLists(scope, QuickGroup.Goals)));
		bool headers = scope == QuickScope.All || listed > 1;
		// Groups by their best match, the fixed order on a tie, except that a boss goes before the items named after it
		// (its trophy, mask, lore item: "astrum deus" means the boss); goals (the player's own) after Items.
		foreach (int g in from g in Enumerable.Range(0, groupCount)
			where found[g].Count > 0
			orderby found[g][0].Rank descending, (g == (int)QuickGroup.Mobs && QuickIndex[found[g][0].Position] is { Kind: QuickKind.Mob } top && BookBestiary.Mobs[top.Id].Kind == BookMobKind.Boss) ? -1 : g
			select g)
		{
			QuickGroup group = (QuickGroup)g;
			List<(int Position, int Rank, int Weight)> list = found[g];
			if (headers)
			{
				QuickRows.Add(new QuickRow(QuickKind.Header, group, 0, Count: counts[g]));
			}
			for (int i = 0; i < list.Count; i++)
			{
				QuickEntry entry = QuickIndex[list[i].Position];
				int rank = list[i].Rank;
				QuickRows.Add(QuickMatchRow(entry, group, list[i].Position, rank, query, terms));
				// Links only under a strong match (a name's words start with the query, or better), the first two items and
				// the first mob, where the item and Bestiary groups list.
				if (rank >= QuickStrongName + BookQuickRank.Value(BookQuickRank.WordPrefix, name: true) && group is QuickGroup.Items or QuickGroup.Mobs && ((entry.Kind == QuickKind.Item && i < 2) || (entry.Kind == QuickKind.Mob && i == 0)))
				{
					AddQuickLinks(entry.Kind, entry.Id, group, ref linkReads);
				}
			}
			if (counts[g] > list.Count)
			{
				QuickRows.Add(new QuickRow(QuickKind.ShowAll, group, 0, Count: counts[g]));
			}
			if (g == (int)QuickGroup.Items && QuickScopeLists(scope, QuickGroup.Goals))
			{
				AddGoalRows(goals, headers, scope);
				goals.Clear();
			}
		}
		AddGoalRows(goals, headers, scope);
		if (QuickRows.Count == 0)
		{
			// Nothing here: the tabs' own searches (they also read item texts), and All when a scope hid the rest.
			QuickRows.Add(new QuickRow(QuickKind.TextSearch, QuickGroup.Items, 0));
			QuickRows.Add(new QuickRow(QuickKind.TextSearch, QuickGroup.Mobs, 0));
			if (scope != QuickScope.All && QuickScopeCounts[(int)QuickScope.All] > 0)
			{
				QuickRows.Insert(0, new QuickRow(QuickKind.AllScopes, QuickGroup.Items, 0, Count: QuickScopeCounts[(int)QuickScope.All]));
			}
		}
	}

	private static void AddGoalRows(List<int> goals, bool headers, QuickScope scope)
	{
		if (goals.Count == 0 || !QuickScopeLists(scope, QuickGroup.Goals))
		{
			return;
		}
		if (headers)
		{
			QuickRows.Add(new QuickRow(QuickKind.Header, QuickGroup.Goals, 0, Count: goals.Count));
		}
		foreach (int goal in goals.Take((scope == QuickScope.All) ? QuickCaps[(int)QuickGroup.Goals] : QuickScopeCap))
		{
			QuickRows.Add(new QuickRow(QuickKind.Goal, QuickGroup.Goals, goal));
		}
	}

	// Tracked goals whose item (or set) name matches.
	private static List<int> QuickGoalMatches(string query, string[] terms)
	{
		List<int> goals = new List<int>();
		for (int g = 0; g < TrackedGoals.Count; g++)
		{
			TrackedGoal goal = TrackedGoals[g];
			string[] fields = goal.Parts.Select((Item part) => N(part.Name)).Prepend(N(GoalTitle(goal))).ToArray();
			if (BookQuickRank.Score(query, terms, fields, fields.Length) > 0)
			{
				goals.Add(g);
			}
		}
		return goals;
	}

	// Every match of a pass into QuickHits with its rank. Names, then what an item is (a concept's members), then the
	// description: an entry keeps its best tier.
	private static void FindQuickMatches(string query, string[] terms)
	{
		bool texts = query.Length >= 3;
		bool english = texts && BookEnglishNames.Needed && query.All((char c) => c < 128);
		(bool, bool, int) mode = (texts, english, QuickIndex.Count ^ (DescriptionsReady ? 1 << 30 : 0));
		bool narrow = mode == QuickHitMode && QuickExtends(QuickHitTerms, terms);
		QuickCandidates.Clear();
		if (narrow)
		{
			QuickCandidates.AddRange(QuickHits);
		}
		QuickHitTerms = terms;
		QuickHitMode = mode;
		QuickHits.Clear();
		QuickPass++;
		string[] stems = terms.Select(BookQuickRank.Stem).ToArray();
		int count = narrow ? QuickCandidates.Count : QuickIndex.Count;
		for (int n = 0; n < count; n++)
		{
			int position = narrow ? QuickCandidates[n] : n;
			QuickEntry entry = QuickIndex[position];
			int rank = 0;
			if (Contains(entry.All))
			{
				int score = BookQuickRank.Score(query, terms, entry.Fields, 2);
				rank = (score <= 0) ? 0 : (((score >= BookQuickRank.Value(BookQuickRank.WordPrefix, name: false)) ? QuickStrongName : QuickWeakName) + score);
			}
			if (rank == 0 && texts && QuickDescribes(entry, terms, english))
			{
				rank = QuickDescribed;
			}
			if (rank > 0)
			{
				QuickRank[position] = rank;
				QuickStamp[position] = QuickPass;
				QuickConceptOf[position] = -1;
				QuickHits.Add(position);
			}
		}
		// What an item or mob is: every member of a concept the query names strongly (a word of it starts with each term),
		// above a name that only contains the query.
		if (query.Length < 2)
		{
			return;
		}
		for (int c = 0; c < QuickConcepts.Count; c++)
		{
			QuickConcept concept = QuickConcepts[c];
			int score = BookQuickRank.Score(query, terms, concept.Fields, concept.Fields.Length);
			if (score < BookQuickRank.Value(BookQuickRank.WordPrefix, name: true))
			{
				continue;
			}
			int rank = QuickConceptTier + score;
			int[] entries = (concept.Kind == QuickConceptKind.MobCategory) ? QuickMobEntry : QuickItemEntry;
			foreach (int member in concept.Members)
			{
				int position = (member >= 0 && member < entries.Length) ? entries[member] : (-1);
				if (position < 0)
				{
					continue;
				}
				if (QuickStamp[position] != QuickPass)
				{
					QuickStamp[position] = QuickPass;
					QuickRank[position] = rank;
					QuickConceptOf[position] = c;
					QuickHits.Add(position);
				}
				else if (QuickRank[position] < rank)
				{
					QuickRank[position] = rank;
					QuickConceptOf[position] = c;
				}
			}
		}
		bool Contains(string all)
		{
			foreach (string stem in stems)
			{
				if (!all.Contains(stem, StringComparison.Ordinal))
				{
					return false;
				}
			}
			return true;
		}
	}

	// The new terms can only find fewer: the same ones with the last one longer, or more of them.
	private static bool QuickExtends(string[] before, string[] now)
	{
		if (before.Length == 0 || now.Length < before.Length)
		{
			return false;
		}
		for (int i = 0; i < before.Length - 1; i++)
		{
			if (now[i] != before[i])
			{
				return false;
			}
		}
		return now[before.Length - 1].StartsWith(before[^1], StringComparison.Ordinal);
	}

	// An item's description (the Items tab's own text cache, ItemDescriptions) or a mob's flavor text; the English one too
	// for a Latin query in another language.
	private static bool QuickDescribes(QuickEntry entry, string[] terms, bool english)
	{
		if (entry.Kind == QuickKind.Item)
		{
			ItemText text = ItemTextOf(entry.Id);
			return text != null && (BookQuickRank.Describes(text.Text, terms) || (english && BookQuickRank.Describes(text.English, terms)));
		}
		return entry.Kind == QuickKind.Mob && BookQuickRank.Describes(QuickFlavor[entry.Id].Text, terms);
	}

	// The weight plus a little for a recent craft or sighting and for what this world can get before its next boss.
	private static int QuickWeight(QuickEntry entry)
	{
		return entry.Kind switch
		{
			QuickKind.Stage => StageWeight(entry.Id),
			QuickKind.Item => entry.Weight + (QuickRecentItems.Contains(entry.Id) ? 1 : 0) + ((entry.Id < QuickStageNow.Length && QuickStageNow[entry.Id]) ? 1 : 0),
			QuickKind.Mob => entry.Weight + (QuickRecentMobs.Contains(entry.Id) ? 1 : 0),
			_ => entry.Weight
		};
	}

	// A match's row: why it is listed when not by its name.
	private static QuickRow QuickMatchRow(QuickEntry entry, QuickGroup group, int position, int rank, string query, string[] terms)
	{
		if (rank >= QuickConceptTier && rank < QuickStrongName && QuickConceptOf[position] >= 0)
		{
			return new QuickRow(entry.Kind, group, entry.Id, Hint: QuickConcepts[QuickConceptOf[position]].Why, Why: QuickWhy.Concept);
		}
		if (rank < QuickWeakName)
		{
			return new QuickRow(entry.Kind, group, entry.Id, Hint: QuickSnippet(entry, terms), Why: QuickWhy.Description);
		}
		return new QuickRow(entry.Kind, group, entry.Id, Hint: QuickHint(entry, query, terms));
	}

	// The line of the description that holds the typed words, cut to start a little before the first of them.
	private static string QuickSnippet(QuickEntry entry, string[] terms)
	{
		string shown = "";
		if (entry.Kind == QuickKind.Item)
		{
			ItemText text = ItemTextOf(entry.Id);
			shown = (text == null) ? "" : (BookQuickRank.Describes(text.Text, terms) ? text.Shown : text.EnglishShown);
		}
		else if (entry.Kind == QuickKind.Mob)
		{
			shown = QuickFlavor[entry.Id].Shown;
		}
		string[] lines = shown.Split('\n');
		string line = lines.FirstOrDefault((string l) => BookQuickRank.Describes(N(l), terms)) ?? lines.FirstOrDefault((string l) => BookQuickRank.Marks(l, terms).Count > 0) ?? lines[0];
		List<(int Start, int Length)> marks = BookQuickRank.Marks(line, terms);
		int at = (marks.Count == 0) ? 0 : marks[0].Start;
		if (at > 24)
		{
			int cut = line.LastIndexOf(' ', at - 16);
			line = "…" + line.Substring(Math.Max(0, cut + 1));
		}
		return line.Trim();
	}

	// Better first: the match, its weight, the shorter name, then a fixed order.
	private static int QuickOrder((int Position, int Rank, int Weight) a, (int Position, int Rank, int Weight) b)
	{
		int order = b.Rank.CompareTo(a.Rank);
		order = (order != 0) ? order : b.Weight.CompareTo(a.Weight);
		QuickEntry x = QuickIndex[a.Position];
		QuickEntry y = QuickIndex[b.Position];
		order = (order != 0) ? order : x.Fields[0].Length.CompareTo(y.Fields[0].Length);
		order = (order != 0) ? order : string.CompareOrdinal(x.Fields[0], y.Fields[0]);
		return (order != 0) ? order : x.Id.CompareTo(y.Id);
	}

	// Why a row matched when its name did not: the English name, the ID typed in full, the internal name.
	private static string QuickHint(QuickEntry entry, string query, string[] terms)
	{
		if (Has(entry.Fields[0]))
		{
			return "";
		}
		if (entry.English.Length > 0 && Has(entry.Fields[1]))
		{
			return entry.English;
		}
		if (entry.Fields.Length > 3 && entry.Fields[3] == query)
		{
			return L("QuickSearch.Id", query);
		}
		return (entry.Internal.Length > 0 && Has(entry.Fields[2])) ? entry.Internal : "";
		bool Has(string field)
		{
			return terms.All((string term) => field.Contains(BookQuickRank.Stem(term), StringComparison.Ordinal));
		}
	}

	private static void AddQuickLinks(QuickKind kind, int id, QuickGroup group, ref int reads)
	{
		if (!QuickLinks.TryGetValue((kind, id), out List<QuickRow> links))
		{
			if (!QuickFacts.ContainsKey((kind, id)))
			{
				if (reads <= 0)
				{
					QuickLinksPending = true;
					return;
				}
				reads--;
			}
			QuickFact facts = QuickFactsOf(kind, id);
			links = (kind == QuickKind.Item) ? ItemQuickLinks(id, facts) : MobQuickLinks(id, facts);
			QuickLinks[(kind, id)] = links;
		}
		foreach (QuickRow link in links)
		{
			QuickRows.Add(link with { Group = group });
		}
	}

	// What the preview and the links show of an item or mob, read once: How to Get's own rows (SourceEntries, the Treasure
	// Bag rule included) in link order, or the mob card's loot (its rules, the bag's in Expert) and its numbers.
	private sealed record QuickFact(List<BookAcquisitionEntry> Sources, int SourceTotal, List<BookEnemyLoot> Loot, MobStats? Stats);

	private static readonly Dictionary<(QuickKind, int), QuickFact> QuickFacts = new Dictionary<(QuickKind, int), QuickFact>();

	private static QuickFact QuickFactsOf(QuickKind kind, int id)
	{
		if (QuickFacts.TryGetValue((kind, id), out QuickFact facts))
		{
			return facts;
		}
		try
		{
			facts = (kind == QuickKind.Item) ? ReadItemFacts(id) : ReadMobFacts(id);
		}
		catch (Exception error)
		{
			facts = new QuickFact(new List<BookAcquisitionEntry>(), 0, new List<BookEnemyLoot>(), null);
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn($"Quick search: what {kind} {id} has could not be read.", error);
		}
		QuickFacts[(kind, id)] = facts;
		return facts;
	}

	// The preview's facts, once the selection rests: one read per update, as the links.
	private static void ReadQuickPreview()
	{
		if (QuickSelected < 0 || QuickSelected >= QuickRows.Count)
		{
			return;
		}
		(QuickKind kind, int id) = QuickFactKey(QuickRows[QuickSelected]);
		if (kind != QuickKind.Header && !QuickFacts.ContainsKey((kind, id)))
		{
			QuickFactsOf(kind, id);
		}
	}

	// The item or mob a row previews: a link previews where it points, a goal its item.
	private static (QuickKind Kind, int Id) QuickFactKey(QuickRow row)
	{
		int item = QuickItemOf(row);
		if (item > 0)
		{
			return (QuickKind.Item, item);
		}
		int mob = (row.Kind == QuickKind.Mob) ? row.Id : ((row.Kind == QuickKind.Stage) ? QuickStageMob(row.Id) : (-1));
		return (mob >= 0) ? (QuickKind.Mob, mob) : (QuickKind.Header, 0);
	}

	// The Bestiary entry of a stage's boss (its map head's NPC), -1 when it has none.
	private static int QuickStageMob(int step)
	{
		int npc = BookSteps.HeadNpc(step, out _);
		return (npc != 0) ? BookBestiary.IndexOf(npc) : (-1);
	}

	private static QuickFact ReadItemFacts(int type)
	{
		List<BookAcquisitionEntry> entries = SourceEntries(type);
		List<BookAcquisitionEntry> ordered = entries.Where((BookAcquisitionEntry e) => !e.OtherMode).Select((BookAcquisitionEntry e, int order) => (e, order)).OrderBy(((BookAcquisitionEntry e, int order) x) => LinkRank(x.e)).ThenBy(((BookAcquisitionEntry e, int order) x) => x.order).Select(((BookAcquisitionEntry e, int order) x) => x.e).ToList();
		// Every How to Get row: the recipes (one per merged variant) and the sources.
		return new QuickFact(ordered, BookRecipeVariants.For(type).Count + entries.Count, null, null);
		static int LinkRank(BookAcquisitionEntry e)
		{
			if (e.Kind == BookAcquisitionKind.Container)
			{
				return 1;
			}
			int mob = (e.Kind == BookAcquisitionKind.Drop && e.NpcNetId != 0) ? BookBestiary.IndexOf(e.NpcNetId) : (-1);
			return (mob < 0) ? 3 : ((BookBestiary.Mobs[mob].Kind == BookMobKind.Boss) ? 0 : 2);
		}
	}

	// The mob card's own loot: what this world drops, the rarest first, a material after the weapons and accessories of
	// its rarity (Moon Lord's Luminite after Meowmere), then the likeliest; its numbers once the Bestiary shows them.
	private static QuickFact ReadMobFacts(int index)
	{
		BookMob mob = BookBestiary.Mobs[index];
		int keep = LootNpc;
		List<BookEnemyLoot> loot;
		try
		{
			LootNpc = mob.NetId;
			loot = OrderLoot(FromBags(ReadLoot(Main.ItemDropsDB.GetRulesForNPCID(mob.NetId, includeGlobalDrops: false))));
		}
		finally
		{
			LootNpc = keep;
		}
		List<BookEnemyLoot> shown = (from x in loot.Select((BookEnemyLoot l, int order) => (l, order))
			where !x.l.OtherWorld && !ItemID.Sets.BossBag[x.l.ItemType]
			orderby Notability(x.l.ItemType) descending, x.order
			select x.l).ToList();
		NPC sample = ContentSamples.NpcsByNetId[mob.NetId];
		int kills = Main.BestiaryTracker.Kills.GetKillCount(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[mob.NetId]);
		MobStats? stats = MobStatsUnlocked(mob.NetId, sample, kills) ? FreshMobStats(mob.NetId) : null;
		return new QuickFact(null, loot.Count, shown, stats);
		static int Notability(int type)
		{
			Item item = ContentSamples.ItemsByType[type];
			return ((item.expert || item.master) ? 24 : (item.rare * 2)) + ((!item.material) ? 1 : 0);
		}
	}

	// How to Get's rows as links: the bosses that drop it, the containers it is found in, then other mobs; another world's
	// rows are left to How to Get itself.
	private static List<QuickRow> ItemQuickLinks(int type, QuickFact facts)
	{
		List<QuickRow> links = new List<QuickRow>();
		HashSet<int> mobs = new HashSet<int>();
		HashSet<int> items = new HashSet<int>();
		foreach (BookAcquisitionEntry entry in facts.Sources)
		{
			if (links.Count >= QuickLinkCap)
			{
				break;
			}
			if (entry.Kind == BookAcquisitionKind.Drop && entry.NpcNetId != 0)
			{
				int mob = BookBestiary.IndexOf(entry.NpcNetId);
				if (mob >= 0 && mobs.Add(mob))
				{
					links.Add(new QuickRow(QuickKind.Mob, QuickGroup.Items, mob, QuickLink.DroppedBy));
				}
			}
			else if (entry.Kind == BookAcquisitionKind.Container && entry.ItemType > 0 && entry.ItemType != type && items.Add(entry.ItemType))
			{
				links.Add(new QuickRow(QuickKind.Item, QuickGroup.Items, entry.ItemType, QuickLink.FoundIn));
			}
		}
		if (links.Count > 0 && facts.SourceTotal > links.Count)
		{
			links.Add(new QuickRow(QuickKind.Item, QuickGroup.Items, type, QuickLink.AllSources, facts.SourceTotal));
		}
		return links;
	}

	private static List<QuickRow> MobQuickLinks(int index, QuickFact facts)
	{
		List<QuickRow> links = facts.Loot.Take(QuickLinkCap).Select((BookEnemyLoot l) => new QuickRow(QuickKind.Item, QuickGroup.Mobs, l.ItemType, QuickLink.Drops)).ToList();
		if (links.Count > 0 && facts.SourceTotal > links.Count)
		{
			links.Add(new QuickRow(QuickKind.Mob, QuickGroup.Mobs, index, QuickLink.AllLoot, facts.SourceTotal));
		}
		return links;
	}

	// The empty field: tracked goals, picks from this session, then the latest crafts and the mobs seen last (both kept by
	// the player).
	private static void AddRecentRows()
	{
		Add(QuickGroup.Tracked, Enumerable.Range(0, Math.Min(3, TrackedGoals.Count)).Select((int g) => new QuickRow(QuickKind.Goal, QuickGroup.Tracked, g)).ToList());
		List<QuickRow> opened = new List<QuickRow>();
		foreach ((QuickKind kind, int id, string key) in QuickOpened)
		{
			int resolved = kind switch
			{
				QuickKind.Stage => BookSteps.Find(key),
				QuickKind.ItemCategory => ItemCategoryList.FindIndex((BookCategory c) => c.Key == key),
				QuickKind.MobCategory => BookBestiary.Tags.FindIndex((BookBestiary.Tag t) => t.Key == key),
				QuickKind.Mod => QuickMods.IndexOf(key),
				QuickKind.Item => ContentSamples.ItemsByType.ContainsKey(id) ? id : (-1),
				_ => (id < BookBestiary.Mobs.Count) ? id : (-1)
			};
			if (resolved >= 0 && (kind != QuickKind.Stage || resolved < BookSteps.All.Count))
			{
				opened.Add(new QuickRow(kind, QuickGroup.Opened, resolved));
			}
		}
		Add(QuickGroup.Opened, opened);
		Add(QuickGroup.Crafted, (from index in BookRecent.ResolveRecipeIndexes()
			select Main.recipe[index].createItem.type).Distinct().Take(4).Select((int type) => new QuickRow(QuickKind.Item, QuickGroup.Crafted, type)).ToList());
		Add(QuickGroup.Seen, (from credit in SeenMobs.Newest
			select BookBestiary.IndexOfCredit(credit) into index
			where index >= 0
			select index).Distinct().Take(4).Select((int index) => new QuickRow(QuickKind.Mob, QuickGroup.Seen, index)).ToList());
		static void Add(QuickGroup group, List<QuickRow> rows)
		{
			if (rows.Count > 0)
			{
				QuickRows.Add(new QuickRow(QuickKind.Header, group, 0));
				QuickRows.AddRange(rows);
			}
		}
	}

	private static void RememberQuickPick(QuickRow row)
	{
		string key = row.Kind switch
		{
			QuickKind.Stage => BookSteps.All[row.Id].Key,
			QuickKind.ItemCategory => ItemCategoryList[row.Id].Key,
			QuickKind.MobCategory => BookBestiary.Tags[row.Id].Key,
			QuickKind.Mod => QuickMods[row.Id],
			_ => ""
		};
		QuickOpened.RemoveAll(((QuickKind Kind, int Id, string Key) x) => x.Kind == row.Kind && x.Id == row.Id && x.Key == key);
		QuickOpened.Insert(0, (row.Kind, row.Id, key));
		if (QuickOpened.Count > QuickRecentCap)
		{
			QuickOpened.RemoveAt(QuickRecentCap);
		}
	}

	// Opening a pick -------------------------------------------------------------------------------------------------

	// Enter or a click: the pick where it lives. An item opens on the scope's tab: Storage selects its stack, Recipes its
	// recipe, else its card on Items; a goal opens on Tracking.
	private static void ActivateQuickRow(QuickRow row)
	{
		if (row.Kind == QuickKind.Header)
		{
			return;
		}
		if (row.Kind == QuickKind.Command)
		{
			RunQuickCommand((QuickCommand)row.Id);
			return;
		}
		if (row.Kind == QuickKind.AllScopes)
		{
			SetQuickScope(QuickScope.All);
			return;
		}
		if (row.Kind == QuickKind.ShowAll && row.Group is not (QuickGroup.Items or QuickGroup.Mobs or QuickGroup.Storage or QuickGroup.Recipes))
		{
			QuickExpanded.Add(row.Group);
			int keep = QuickSelected;
			RunQuickSearch();
			QuickSelected = Math.Clamp(keep, 0, QuickRows.Count - 1);
			EnsureQuickVisible();
			return;
		}
		if (row.Kind is QuickKind.Item or QuickKind.Mob or QuickKind.Stage or QuickKind.ItemCategory or QuickKind.MobCategory or QuickKind.Mod && row.Link is QuickLink.None or QuickLink.DroppedBy or QuickLink.FoundIn or QuickLink.Drops)
		{
			RememberQuickPick(row with { Link = QuickLink.None, Hint = "", Why = QuickWhy.Name });
		}
		string query = QuickQuery;
		BeginQuickNavigation();
		switch (row.Kind)
		{
			case QuickKind.Item when row.Link == QuickLink.AllSources:
				QuickShowItem(row.Id, CardModeFor(row.Id));
				OpenAcquisition(row.Id, null);
				break;
			case QuickKind.Item when row.Group == QuickGroup.Storage:
				QuickShowStorage(row.Id);
				break;
			case QuickKind.Item when row.Group == QuickGroup.Recipes:
				QuickShowRecipe(row.Id);
				break;
			case QuickKind.Item:
				QuickShowItem(row.Id, CardModeFor(row.Id));
				break;
			case QuickKind.Mob:
				ShowMob(row.Id, BookBestiary.Mobs[row.Id].NetId);
				MobStatsMode = false;
				break;
			case QuickKind.Goal:
				QuickShowGoal(row.Id);
				break;
			case QuickKind.Stage:
				QuickStageFilter(row.Id);
				break;
			case QuickKind.ItemCategory:
			case QuickKind.MobCategory:
			{
				bool mobs = row.Kind == QuickKind.MobCategory;
				QuickTab(mobs ? 2 : 0, "");
				Category = row.Id;
				CategoryRow = Math.Clamp(row.Id - 2, 0, MaxCategoryRow);
				ApplyFilters();
				break;
			}
			case QuickKind.Mod:
			{
				string mod = QuickMods[row.Id];
				QuickTab((!CatalogueMods.Contains(mod) && MobMods.Contains(mod)) ? 2 : 0, "");
				ModFilter = mod;
				ApplyFilters();
				break;
			}
			default:
				// "All on the tab" and the descriptions search: the tab's own search, which reads item texts too.
				QuickTab(row.Group switch
				{
					QuickGroup.Mobs => 2,
					QuickGroup.Storage => 3,
					QuickGroup.Recipes => 1,
					_ => 0
				}, query);
				ApplyFilters();
				break;
		}
	}

	// A pick that navigates closes the field and opens the book there; the book's history keeps where it was.
	private static void BeginQuickNavigation()
	{
		CloseQuickSearch();
		if (!Open)
		{
			Toggle();
		}
		CloseAcquisition();
		SetTyping(value: false);
		ModMenu = (SortMenu = (StageMenu = (StationMenu = (TraitMenu = (HelpOpen = false)))));
	}

	// The shared stage filter "Before: <boss>", on a tab that lists by stage (Storage keeps its own, Tracking has none).
	private static void QuickStageFilter(int step)
	{
		if (StorageView || TrackingView)
		{
			SwitchTab(0);
		}
		StageFilter = BookStageFilter.BeforeBoss;
		StageBoss = BookSteps.All[step].Key;
		Page = (DetailsPage = (Variant = 0));
		ApplyFilters();
	}

	// Opens a tab for what a pick names, with its search set and the tab's other filters cleared as Ctrl+Backspace clears
	// them (the stage and class filters stay, as there).
	private static void QuickTab(int tab, string query)
	{
		if (CurrentTab != tab)
		{
			SwitchTab(tab);
		}
		ResetFilters(apply: false);
		Query = query;
		DetailsPage = 0;
	}

	// The item's card on Items: selected in the grid when the filters show it, else shown in the card (as a step).
	private static void QuickShowItem(int type, BookCardMode mode)
	{
		if (CurrentTab != 0)
		{
			SwitchTab(0);
		}
		ResetCard();
		ArmorFocus = 0;
		int position = Filtered.IndexOf(type);
		if (position < 0)
		{
			StepCard(type, mode);
			return;
		}
		Selected = type;
		Page = position / ActivePageSize;
		CardMode = mode;
		Variant = (DetailsPage = 0);
	}

	// The Storage tab with the item's biggest stack selected, its search cleared so the grid shows it.
	private static void QuickShowStorage(int type)
	{
		QuickTab(3, "");
		ApplyFilters();
		int stack = QuickStorageStack(type);
		Selected = stack;
		int position = Filtered.IndexOf(stack);
		Page = (position < 0) ? 0 : (position / ActivePageSize);
	}

	// The Recipes tab on the item's recipe (the first variant's), where Craft is: its search set to the item's name, so
	// the grid shows it among its own.
	private static void QuickShowRecipe(int type)
	{
		List<BookRecipeVariants.Variant> variants = BookRecipeVariants.For(type);
		if (variants.Count == 0)
		{
			QuickShowItem(type, BookCardMode.Recipe);
			return;
		}
		QuickTab(1, ContentSamples.ItemsByType[type].Name);
		ApplyFilters();
		Selected = BookRecipeVariants.Representative(variants[0].Representative);
		int position = Filtered.IndexOf(Selected);
		Page = (position < 0) ? 0 : (position / ActivePageSize);
	}

	// The Tracking tab on the goal.
	private static void QuickShowGoal(int goal)
	{
		if (goal < 0 || goal >= TrackedGoals.Count)
		{
			return;
		}
		SwitchTab(5);
		TrackingSelection = TrackedGoals[goal];
		Page = goal / BookGeometry.TrackingPageSize;
	}

	// Selection and scrolling ---------------------------------------------------------------------------------------

	private static float QuickRowHeight(QuickRow row)
	{
		return row.Kind switch
		{
			QuickKind.Header => 30f,
			QuickKind.ShowAll or QuickKind.TextSearch or QuickKind.AllScopes => 34f,
			_ when row.Link != QuickLink.None => (row.Link is QuickLink.AllSources or QuickLink.AllLoot) ? 28f : 30f,
			_ when row.Why == QuickWhy.Description => 52f,
			_ => 42f
		};
	}

	private static List<(int Row, BookRect Rect)> QuickVisible()
	{
		List<(int, BookRect)> visible = new List<(int, BookRect)>();
		BookRect list = QuickListRows;
		float y = list.Y;
		for (int i = QuickScroll; i < QuickRows.Count; i++)
		{
			float height = QuickRowHeight(QuickRows[i]);
			if (y + height > list.Y + list.Height)
			{
				break;
			}
			visible.Add((i, new BookRect(list.X, y, list.Width, height)));
			y += height;
		}
		return visible;
	}

	private static int QuickMaxScroll()
	{
		float room = QuickListRows.Height;
		int first = QuickRows.Count;
		while (first > 0 && room >= QuickRowHeight(QuickRows[first - 1]))
		{
			room -= QuickRowHeight(QuickRows[--first]);
		}
		return first;
	}

	private static int QuickRowAt()
	{
		if (!QuickListRows.Contains(Mouse.X, Mouse.Y))
		{
			return -1;
		}
		foreach ((int row, BookRect rect) in QuickVisible())
		{
			if (rect.Contains(Mouse.X, Mouse.Y))
			{
				return (QuickRows[row].Kind == QuickKind.Header) ? (-1) : row;
			}
		}
		return -1;
	}

	private static void SelectQuickRow(int row)
	{
		if (row != QuickSelected)
		{
			QuickSelected = row;
			QuickSelectedAge = 0;
			QuickArmed = -1;
		}
	}

	private static void MoveQuickSelection(int step)
	{
		int at = QuickSelected;
		for (int moves = Math.Abs(step); moves > 0; moves--)
		{
			int next = at;
			do
			{
				next += Math.Sign(step);
			}
			while (next >= 0 && next < QuickRows.Count && QuickRows[next].Kind == QuickKind.Header);
			if (next < 0 || next >= QuickRows.Count)
			{
				break;
			}
			at = next;
		}
		SelectQuickRow(at);
		EnsureQuickVisible();
	}

	// The selected row in view, with its group's header when that sits right above it.
	private static void EnsureQuickVisible()
	{
		if (QuickSelected < 0)
		{
			return;
		}
		int top = (QuickSelected > 0 && QuickRows[QuickSelected - 1].Kind == QuickKind.Header) ? (QuickSelected - 1) : QuickSelected;
		if (top < QuickScroll)
		{
			QuickScroll = top;
			return;
		}
		while (QuickScroll < QuickSelected && !QuickVisible().Exists(((int Row, BookRect Rect) v) => v.Row == QuickSelected))
		{
			QuickScroll++;
		}
	}

	// Stand: index build, a ranking pass for a query and the same pass reading every link (cold after the rebuild), in
	// milliseconds, and what it found. scope: a QuickScope name ("Storage").
	internal static string QuickSearchCost(string query, string scope = "All")
	{
		QuickIndexKey = default;
		EnsureQuickIndex();
		string keep = QuickQuery;
		QuickScope keepScope = QuickScopeShown;
		QuickScopeShown = Enum.TryParse(scope, out QuickScope parsed) ? parsed : QuickScope.All;
		QuickQuery = query;
		QuickHitTerms = Array.Empty<string>();
		RunQuickSearch();
		double search = QuickSearchMs;
		RunQuickSearch(int.MaxValue);
		string rows = string.Join(" | ", QuickRows.Take(18).Select((QuickRow r) => r.Kind == QuickKind.Header ? ("[" + QuickGroupName(r.Group) + " " + r.Count + "]") : (r.Kind + ":" + ((r.Kind == QuickKind.ShowAll) ? r.Count.ToString(CultureInfo.InvariantCulture) : QuickRowName(r)) + ((r.Hint.Length > 0) ? (" (" + r.Hint + ")") : ""))));
		string chips = string.Join(" ", Enum.GetValues<QuickScope>().Select((QuickScope s) => s + "=" + QuickScopeCounts[(int)s]));
		string cost = string.Create(CultureInfo.InvariantCulture, $"index {QuickIndex.Count} entries, {QuickConcepts.Count} concepts in {QuickIndexMs:F1} ms; search {search:F2} ms, with links {QuickSearchMs:F2} ms; english {(BookEnglishNames.Ready ? "ready" : (BookEnglishNames.Needed ? "reading" : "not needed"))}, texts {(DescriptionsReady ? "ready" : "reading")}; {chips}: ");
		QuickQuery = keep;
		QuickScopeShown = keepScope;
		QuickHitTerms = Array.Empty<string>();
		RunQuickSearch();
		return cost + rows;
	}

	// Stand: typing a query a letter at a time, as a player does (each pass narrows the last), the slowest pass and the
	// total in milliseconds.
	internal static string QuickTypingCost(string query)
	{
		string keep = QuickQuery;
		double slowest = 0;
		double total = 0;
		QuickHitTerms = Array.Empty<string>();
		for (int i = 1; i <= query.Length; i++)
		{
			QuickQuery = query.Substring(0, i);
			RunQuickSearch();
			slowest = Math.Max(slowest, QuickSearchMs);
			total += QuickSearchMs;
		}
		QuickQuery = keep;
		RunQuickSearch();
		return string.Create(CultureInfo.InvariantCulture, $"{query.Length} keys: slowest {slowest:F2} ms, total {total:F2} ms");
	}
}
