using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ModernRecipeBook.UI.Book;

// Quick search: Ctrl+F anywhere (or the "Search Everything" key) opens one field over the game or the book that finds
// items, Bestiary entries, "Before: <boss>" stages, item and Bestiary categories and mods at once, grouped by kind, and
// opens a pick where it lives, through the book's history (docs/quick-search.md). The index is built once per language
// and world; a keystroke ranks it (BookQuickRank). Direct matches carry the book's own links under them: an item the
// mobs and bags How to Get names for it, a mob the loot its card lists.
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
		ShowAll,
		TextSearch
	}

	// The order on a tie; the last three list recent things while the field is empty.
	private enum QuickGroup
	{
		Items,
		Mobs,
		Stages,
		ItemCategories,
		MobCategories,
		Mods,
		Opened,
		Crafted,
		Seen
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

	// Id: item type, Bestiary index, step, category or tag index, QuickMods index. Count: a header's, a "show all" row's or
	// an "all sources" row's total. Hint: why it matched when not by its name (the English name, an ID).
	private readonly record struct QuickRow(QuickKind Kind, QuickGroup Group, int Id, QuickLink Link = QuickLink.None, int Count = 0, string Hint = "");

	// Fields are normalized: the name, the English name ("" when the same or unknown), then an internal name, the ID and
	// the mod. Weight ranks equal matches: how much the item is used, a met mob, a big category.
	private sealed record QuickEntry(QuickKind Kind, int Id, string[] Fields, int Weight, string English, string Internal);

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

	private static int QuickLastWheel;

	private static Vector2 QuickLastMouse;

	private static Keys QuickRepeatKey;

	private static int QuickRepeatTicks;

	private static float QuickAppear;

	private static readonly List<QuickRow> QuickRows = new List<QuickRow>();

	private static int QuickSelected = -1;

	private static int QuickScroll;

	private static readonly HashSet<QuickGroup> QuickExpanded = new HashSet<QuickGroup>();

	// The category names in item and mob rows, as drawn last frame: a click opens that category.
	private static readonly List<(BookRect Rect, int Category, bool Mobs)> QuickSegments = new List<(BookRect, int, bool)>();

	private static readonly List<QuickEntry> QuickIndex = new List<QuickEntry>();

	private static readonly List<string> QuickMods = new List<string>();

	// What category and mod rows say after their name, counted with the index rather than every frame.
	private static int[] QuickCategorySizes = Array.Empty<int>();

	private static readonly Dictionary<string, (int Items, int Mobs)> QuickModSizes = new Dictionary<string, (int, int)>(StringComparer.Ordinal);

	private static (string Culture, int Items, int Mobs, int Steps, int Categories, bool English) QuickIndexKey;

	private static readonly Dictionary<(QuickKind, int), List<QuickRow>> QuickLinks = new Dictionary<(QuickKind, int), List<QuickRow>>();

	// Links not read yet are read once the query rests (QuickLinkDelay updates), one match's per update: reading a match's
	// sources or loot the first time can take tens of ms (condition probes), and "w", "wo", "woo" need none of them.
	private static bool QuickLinksPending;

	private static int QuickQueryAge;

	private const int QuickLinkDelay = 8;

	// Picks from the quick search this session, newest first: what the empty field offers.
	private static readonly List<(QuickKind Kind, int Id, string Key)> QuickOpened = new List<(QuickKind, int, string)>();

	private static readonly Dictionary<string, Asset<Texture2D>> QuickModIcons = new Dictionary<string, Asset<Texture2D>>(StringComparer.Ordinal);

	// Stand: the last index build and the last ranking pass, in milliseconds.
	internal static double QuickIndexMs { get; private set; }

	internal static double QuickSearchMs { get; private set; }

	// Stand: the popup's last frame and its slowest since it opened (drawing, with the ranking a keystroke runs), and the
	// slowest ranking pass since then, in milliseconds.
	internal static double QuickFrameMs { get; private set; }

	internal static double QuickFrameMaxMs { get; private set; }

	internal static double QuickSearchMaxMs { get; private set; }

	private static readonly int[] QuickCaps = { 5, 4, 3, 3, 3, 2 };

	private const int QuickLinkCap = 3;

	private const int QuickRecentCap = 8;

	private static BookRect QuickPanel => new BookRect(200f, 44f, 720f, 608f);

	private static BookRect QuickField => new BookRect(QuickPanel.X + 16f, QuickPanel.Y + 16f, QuickPanel.Width - 32f, 44f);

	private static BookRect QuickClear => new BookRect(QuickField.X + QuickField.Width - 38f, QuickField.Y + 8f, 28f, 28f);

	private static BookRect QuickList => new BookRect(QuickPanel.X + 12f, QuickPanel.Y + 72f, QuickPanel.Width - 24f, QuickPanel.Height - 72f - 48f);

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
		if (QuickLinksPending && ++QuickQueryAge >= QuickLinkDelay)
		{
			RefreshQuickLinks();
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
		QuickLastWheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		QuickLastMouse = Main.MouseScreen;
		QuickRepeatKey = Keys.None;
		QuickAppear = 0f;
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
		int wheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		int wheel = wheelValue - QuickLastWheel;
		QuickLastWheel = wheelValue;
		bool moved = Main.MouseScreen != QuickLastMouse;
		QuickLastMouse = Main.MouseScreen;
		bool ctrl = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
		bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
		bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
		// Esc closes it as the book's top layer; the query stays for next time.
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
		if (Press(Keys.Left) || Press(Keys.Right) || Press(Keys.Home) || Press(Keys.End))
		{
			QuickAllSelected = false;
		}
		int step = Repeat(Keys.Down) - Repeat(Keys.Up) + 8 * (Repeat(Keys.PageDown) - Repeat(Keys.PageUp));
		if (step != 0)
		{
			MoveQuickSelection(step);
		}
		if (Press(Keys.Enter) && !alt)
		{
			if (QuickSelected >= 0 && QuickSelected < QuickRows.Count)
			{
				ActivateQuickRow(QuickRows[QuickSelected], shift, ctrl);
			}
			return;
		}
		BookRect panel = QuickPanel;
		int hovered = QuickRowAt();
		if (moved && hovered >= 0)
		{
			QuickSelected = hovered;
		}
		if (wheel != 0 && panel.Contains(Mouse.X, Mouse.Y))
		{
			QuickScroll = Math.Clamp(QuickScroll + ((wheel < 0) ? 3 : (-3)), 0, QuickMaxScroll());
		}
		if (!Main.mouseLeft || lastLeft)
		{
			return;
		}
		if (!panel.Contains(Mouse.X, Mouse.Y))
		{
			CloseQuickSearch();
			return;
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
				ActivateQuickRow(new QuickRow(mobs ? QuickKind.MobCategory : QuickKind.ItemCategory, mobs ? QuickGroup.MobCategories : QuickGroup.ItemCategories, category), shift: false, ctrl: false);
				return;
			}
		}
		if (hovered >= 0)
		{
			ActivateQuickRow(QuickRows[hovered], shift, ctrl);
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
		RunQuickSearch();
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
		string next = Main.GetInputText((QuickAllSelected && !ctrl) ? "" : QuickQuery).Replace("\r", "").Replace("\n", "");
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
			return;
		}
		long start = Stopwatch.GetTimestamp();
		QuickIndexKey = key;
		QuickIndex.Clear();
		QuickMods.Clear();
		QuickModSizes.Clear();
		QuickLinks.Clear();
		Dictionary<string, string> modNames = new Dictionary<string, string>(StringComparer.Ordinal);
		// How often an item is made or used in a recipe: Wood before Wood Fence for "wood".
		int[] uses = new int[ItemLoader.ItemCount];
		foreach (int index in All)
		{
			Recipe recipe = Main.recipe[index];
			Use(recipe.createItem.type);
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
			QuickIndex.Add(new QuickEntry(QuickKind.Item, type, new string[5] { N(item.Name), N(english), N(BookQuickRank.Words(internalName)), type.ToString(CultureInfo.InvariantCulture), ModField(mod) }, Bucket(uses[type]), english, internalName));
		}
		foreach (BookMob mob in BookBestiary.Mobs)
		{
			NPC npc = mob.Sample;
			(int items, int mobs) = QuickModSizes.GetValueOrDefault(mob.Mod);
			QuickModSizes[mob.Mod] = (items, mobs + 1);
			string persistent = ContentSamples.NpcPersistentIdsByNetIds[mob.NetId];
			string english = EnglishOf(Lang.GetNPCName(mob.NetId).Key, npc.FullName);
			int weight = (MobMet(mob.NetId) ? 2 : 0) + ((mob.Kind is BookMobKind.Boss or BookMobKind.MiniBoss) ? 1 : 0);
			QuickIndex.Add(new QuickEntry(QuickKind.Mob, mob.Index, new string[5] { N(npc.FullName), N(english), N(BookQuickRank.Words(persistent)), mob.NetId.ToString(CultureInfo.InvariantCulture), ModField(mob.Mod) }, weight, english, persistent));
		}
		// A stage is found by the boss's name, the English one and its parts' names ("Ares" finds the Exo Mechs).
		IReadOnlyList<BookSteps.Step> steps = BookSteps.All;
		for (int k = 1; k < steps.Count; k++)
		{
			int head = BookSteps.HeadNpc(k, out _);
			string english = (head != 0) ? EnglishOf(Lang.GetNPCName(head).Key, BookSteps.Name(k)) : "";
			string parts = string.Join(" ", steps[k].Npcs.Where(ContentSamples.NpcsByNetId.ContainsKey).Select((int npc) => Lang.GetNPCNameValue(npc)).Distinct());
			QuickIndex.Add(new QuickEntry(QuickKind.Stage, k, new string[4] { N(BookSteps.Name(k)), N(english), N(parts), N(BookQuickRank.Words(steps[k].Key)) }, 0, english, ""));
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
			QuickIndex.Add(new QuickEntry(QuickKind.ItemCategory, c, new string[3] { N(ItemCategoryList[c].Label()), "", N(ItemCategoryList[c].Key) }, Bucket(sizes[c]), "", ItemCategoryList[c].Key));
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
			QuickIndex.Add(new QuickEntry(QuickKind.MobCategory, t, new string[3] { N(label), N(english), N(BookQuickRank.Words(tag.Key)) }, Bucket(tag.Members.Count), english, ""));
		}
		QuickMods.AddRange(ItemMods.Concat(MobMods).Distinct());
		for (int m = 0; m < QuickMods.Count; m++)
		{
			QuickIndex.Add(new QuickEntry(QuickKind.Mod, m, new string[3] { ModField(QuickMods[m]), "", N(BookQuickRank.Words(QuickMods[m])) }, Bucket(QuickModSizes.GetValueOrDefault(QuickMods[m]).Items), "", QuickMods[m]));
		}
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
		static string N(string value)
		{
			return string.IsNullOrEmpty(value) ? "" : BookRules.SearchText(value);
		}
		static int Bucket(int count)
		{
			return Math.Min(6, (int)Math.Log2(1 + count));
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

	// linkReads: how many matches may read their links now; the others wait (QuickLinksPending).
	private static void RunQuickSearch(int linkReads = 0)
	{
		long start = Stopwatch.GetTimestamp();
		QuickLinksPending = false;
		QuickRows.Clear();
		QuickSegments.Clear();
		QuickScroll = 0;
		EnsureQuickIndex();
		string[] terms = BookRules.SearchTerms(QuickQuery);
		if (terms.Length == 0)
		{
			AddRecentRows();
		}
		else
		{
			string query = string.Join(" ", terms);
			// Only the rows a group shows are kept in order: "i" matches thousands of items, and sorting them all took a
			// frame's worth of time.
			List<(QuickEntry Entry, int Score, int Weight)>[] found = new List<(QuickEntry, int, int)>[6];
			int[] counts = new int[6];
			for (int g = 0; g < found.Length; g++)
			{
				found[g] = new List<(QuickEntry, int, int)>();
			}
			foreach (QuickEntry entry in QuickIndex)
			{
				int score = BookQuickRank.Score(query, terms, entry.Fields, 2);
				if (score <= 0)
				{
					continue;
				}
				int g = (int)QuickGroupOf(entry.Kind);
				counts[g]++;
				List<(QuickEntry Entry, int Score, int Weight)> best = found[g];
				(QuickEntry, int, int) match = (entry, score, (entry.Kind == QuickKind.Stage) ? StageWeight(entry.Id) : entry.Weight);
				int cap = QuickExpanded.Contains((QuickGroup)g) ? int.MaxValue : QuickCaps[g];
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
			// Groups by their best match, the fixed order on a tie.
			foreach (int g in from g in Enumerable.Range(0, found.Length)
				where found[g].Count > 0
				orderby found[g][0].Score descending, g
				select g)
			{
				QuickGroup group = (QuickGroup)g;
				List<(QuickEntry Entry, int Score, int Weight)> list = found[g];
				QuickRows.Add(new QuickRow(QuickKind.Header, group, 0, Count: counts[g]));
				for (int i = 0; i < list.Count; i++)
				{
					(QuickEntry entry, int score, _) = list[i];
					QuickRows.Add(new QuickRow(entry.Kind, group, entry.Id, Hint: QuickHint(entry, query, terms)));
					// Links only under a strong match (a name's words start with the query, or better), the first two items and
					// the first mob.
					bool strong = score >= BookQuickRank.Value(BookQuickRank.WordPrefix, name: true);
					if (strong && ((entry.Kind == QuickKind.Item && i < 2) || (entry.Kind == QuickKind.Mob && i == 0)))
					{
						AddQuickLinks(entry.Kind, entry.Id, group, ref linkReads);
					}
				}
				if (counts[g] > list.Count)
				{
					QuickRows.Add(new QuickRow(QuickKind.ShowAll, group, 0, Count: counts[g]));
				}
			}
			if (QuickRows.Count == 0)
			{
				QuickRows.Add(new QuickRow(QuickKind.TextSearch, QuickGroup.Items, 0));
			}
		}
		QuickSelected = QuickRows.FindIndex((QuickRow row) => row.Kind != QuickKind.Header);
		QuickSearchMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
		QuickSearchMaxMs = Math.Max(QuickSearchMaxMs, QuickSearchMs);
	}

	// Better first: the match, its weight, the shorter name, then a fixed order.
	private static int QuickOrder((QuickEntry Entry, int Score, int Weight) a, (QuickEntry Entry, int Score, int Weight) b)
	{
		int order = b.Score.CompareTo(a.Score);
		order = (order != 0) ? order : b.Weight.CompareTo(a.Weight);
		order = (order != 0) ? order : a.Entry.Fields[0].Length.CompareTo(b.Entry.Fields[0].Length);
		order = (order != 0) ? order : string.CompareOrdinal(a.Entry.Fields[0], b.Entry.Fields[0]);
		return (order != 0) ? order : a.Entry.Id.CompareTo(b.Entry.Id);
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
			return terms.All((string term) => field.Contains(term, StringComparison.Ordinal));
		}
	}

	private static void AddQuickLinks(QuickKind kind, int id, QuickGroup group, ref int reads)
	{
		if (!QuickLinks.TryGetValue((kind, id), out List<QuickRow> links))
		{
			if (reads <= 0)
			{
				QuickLinksPending = true;
				return;
			}
			reads--;
			try
			{
				links = (kind == QuickKind.Item) ? ItemQuickLinks(id) : MobQuickLinks(id);
			}
			catch (Exception error)
			{
				links = new List<QuickRow>();
				ModContent.GetInstance<ModernRecipeBook>().Logger.Warn($"Quick search: the links of {kind} {id} could not be read.", error);
			}
			QuickLinks[(kind, id)] = links;
		}
		foreach (QuickRow link in links)
		{
			QuickRows.Add(link with { Group = group });
		}
	}

	// How to Get's own rows (SourceEntries, the Treasure Bag rule included): the bosses that drop it, the containers it
	// is found in, then other mobs; another world's rows are left to How to Get itself.
	private static List<QuickRow> ItemQuickLinks(int type)
	{
		List<BookAcquisitionEntry> entries = SourceEntries(type);
		List<QuickRow> links = new List<QuickRow>();
		HashSet<int> mobs = new HashSet<int>();
		HashSet<int> items = new HashSet<int>();
		foreach (BookAcquisitionEntry entry in entries.Where((BookAcquisitionEntry e) => !e.OtherMode).Select((BookAcquisitionEntry e, int order) => (e, order)).OrderBy(((BookAcquisitionEntry e, int order) x) => LinkRank(x.e)).ThenBy(((BookAcquisitionEntry e, int order) x) => x.order).Select(((BookAcquisitionEntry e, int order) x) => x.e))
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
		// Every How to Get row: the recipes (one per merged variant) and the sources.
		int total = BookRecipeVariants.For(type).Count + entries.Count;
		if (links.Count > 0 && total > links.Count)
		{
			links.Add(new QuickRow(QuickKind.Item, QuickGroup.Items, type, QuickLink.AllSources, total));
		}
		return links;
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

	// The mob card's own loot (its rules, the Treasure Bag's in Expert): what this world drops, the rarest first, a material
	// after the weapons and accessories of its rarity (Moon Lord's Luminite after Meowmere), then the likeliest.
	private static List<QuickRow> MobQuickLinks(int index)
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
		List<QuickRow> links = (from x in loot.Select((BookEnemyLoot l, int order) => (l, order))
			where !x.l.OtherWorld && !ItemID.Sets.BossBag[x.l.ItemType]
			orderby Notability(x.l.ItemType) descending, x.order
			select new QuickRow(QuickKind.Item, QuickGroup.Mobs, x.l.ItemType, QuickLink.Drops)).Take(QuickLinkCap).ToList();
		if (links.Count > 0 && loot.Count > links.Count)
		{
			links.Add(new QuickRow(QuickKind.Mob, QuickGroup.Mobs, index, QuickLink.AllLoot, loot.Count));
		}
		return links;
		static int Notability(int type)
		{
			Item item = ContentSamples.ItemsByType[type];
			return ((item.expert || item.master) ? 24 : (item.rare * 2)) + ((!item.material) ? 1 : 0);
		}
	}

	// The empty field: picks from this session, then the latest crafts and the mobs seen last (both kept by the player).
	private static void AddRecentRows()
	{
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

	// Enter or a click. Shift: an item opens on Used in (as Shift+Click in the grid); Ctrl: on How to Get.
	private static void ActivateQuickRow(QuickRow row, bool shift, bool ctrl)
	{
		if (row.Kind == QuickKind.Header)
		{
			return;
		}
		if (row.Kind == QuickKind.ShowAll && row.Group != QuickGroup.Items && row.Group != QuickGroup.Mobs)
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
			RememberQuickPick(row with { Link = QuickLink.None, Hint = "" });
		}
		string query = QuickQuery;
		CloseQuickSearch();
		if (!Open)
		{
			Toggle();
		}
		CloseAcquisition();
		SetTyping(value: false);
		ModMenu = (SortMenu = (StageMenu = (StationMenu = (TraitMenu = (HelpOpen = false)))));
		switch (row.Kind)
		{
			case QuickKind.Item:
				QuickShowItem(row.Id, (shift && row.Link != QuickLink.AllSources) ? BookCardMode.UsedIn : CardModeFor(row.Id));
				if ((ctrl && !shift) || row.Link == QuickLink.AllSources)
				{
					OpenAcquisition(row.Id, null);
				}
				break;
			case QuickKind.Mob:
				ShowMob(row.Id, BookBestiary.Mobs[row.Id].NetId);
				break;
			case QuickKind.Stage:
				// The shared stage filter, on a tab that lists by stage (Storage keeps its own, Tracking has none).
				if (StorageView || TrackingView)
				{
					SwitchTab(0);
				}
				StageFilter = BookStageFilter.BeforeBoss;
				StageBoss = BookSteps.All[row.Id].Key;
				Page = (DetailsPage = (Variant = 0));
				ApplyFilters();
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
				// "All on the tab" and the description search: the tab's own search, which reads item texts too.
				QuickTab((row.Group == QuickGroup.Mobs) ? 2 : 0, query);
				ApplyFilters();
				break;
		}
	}

	// Opens a tab for what a pick names, with its search set and the tab's other filters cleared as Ctrl+Backspace clears
	// them (the stage and class filters stay, as there).
	private static void QuickTab(int tab, string query)
	{
		if (CurrentTab != tab)
		{
			SwitchTab(tab);
		}
		Query = query;
		ModFilter = "";
		FavoritesOnly = (MetOnly = (CraftableOnly = false));
		SetExcludedStations(Array.Empty<string>());
		Category = (CategoryRow = (Page = (DetailsPage = 0)));
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

	// Selection and scrolling ---------------------------------------------------------------------------------------

	private static float QuickRowHeight(QuickRow row)
	{
		if (row.Kind == QuickKind.Header)
		{
			return 28f;
		}
		return (row.Link != QuickLink.None || row.Kind == QuickKind.ShowAll) ? 30f : 38f;
	}

	private static List<(int Row, BookRect Rect)> QuickVisible()
	{
		List<(int, BookRect)> visible = new List<(int, BookRect)>();
		BookRect list = QuickList;
		float y = list.Y;
		for (int i = QuickScroll; i < QuickRows.Count; i++)
		{
			float height = QuickRowHeight(QuickRows[i]);
			if (y + height > list.Y + list.Height)
			{
				break;
			}
			visible.Add((i, new BookRect(list.X, y, list.Width - 10f, height)));
			y += height;
		}
		return visible;
	}

	private static int QuickMaxScroll()
	{
		float room = QuickList.Height;
		int first = QuickRows.Count;
		while (first > 0 && room >= QuickRowHeight(QuickRows[first - 1]))
		{
			room -= QuickRowHeight(QuickRows[--first]);
		}
		return first;
	}

	private static int QuickRowAt()
	{
		foreach ((int row, BookRect rect) in QuickVisible())
		{
			if (rect.Contains(Mouse.X, Mouse.Y))
			{
				return (QuickRows[row].Kind == QuickKind.Header) ? (-1) : row;
			}
		}
		return -1;
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
		QuickSelected = at;
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

	// Drawing -------------------------------------------------------------------------------------------------------

	// The book is closed: the popup stands over the game where the book would open, on a dimmed screen.
	private static void DrawQuickSearchOverGame(SpriteBatch sb)
	{
		if (!QuickSearchOpen)
		{
			return;
		}
		Layout();
		MouseOver = false;
		QuickAppear = Math.Min(1f, QuickAppear + 0.15f);
		sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth * 2, Main.screenHeight * 2), BackdropVeil * (0.5f * QuickAppear));
		DrawModal(sb, QuickAppear, DrawQuickSearch);
	}

	// Modal like Help: nothing under it hovers or shows a tooltip.
	private static void DrawQuickSearch(SpriteBatch sb)
	{
		long start = Stopwatch.GetTimestamp();
		PendingTip = null;
		Main.HoverItem = new Item();
		Main.hoverItemName = "";
		Main.LocalPlayer.mouseInterface = true;
		Main.LocalPlayer.cursorItemIconEnabled = false;
		ReadQuickText();
		BookRect panel = QuickPanel;
		Box(sb, panel.X, panel.Y + 6f, panel.Width, panel.Height, new Color(0, 0, 0, 120), border: false, 14f);
		Box(sb, panel, Surface, border: true, 12f);
		DrawQuickField(sb);
		DrawQuickRows(sb);
		DrawQuickFooter(sb);
		QuickFrameMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
		QuickFrameMaxMs = Math.Max(QuickFrameMaxMs, QuickFrameMs);
	}

	private static void DrawQuickField(SpriteBatch sb)
	{
		BookRect field = QuickField;
		Box(sb, field, new Color(35, 51, 40), border: true, 8f, Accent, 2);
		Magnifier(sb, field.X + 22f, field.Y + field.Height / 2f, TextColor);
		float textX = field.X + 44f;
		float right = field.X + field.Width - 48f;
		bool caret = (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0;
		if (BookEnglishNames.Needed && !BookEnglishNames.Ready)
		{
			string reading = L("QuickSearch.EnglishLoading");
			float width = TextWidth(reading, 0.8f) + 4f;
			right -= width + 12f;
			CenterLabel(sb, reading, right + 12f, field.Y + 10f, width, field.Height - 20f, Muted, 0.8f, rightAligned: true);
		}
		if (QuickQuery.Length == 0)
		{
			if (caret)
			{
				CenterLabel(sb, "|", textX - 2f, field.Y + 6f, 8f, field.Height - 12f, TextColor, 0.95f, rightAligned: false, leftAligned: true);
			}
			CenterLabel(sb, L("QuickSearch.Placeholder"), textX + 6f, field.Y + 6f, right - textX - 6f, field.Height - 12f, Muted, 0.95f, rightAligned: false, leftAligned: true, fitWidth: true);
			return;
		}
		if (QuickAllSelected)
		{
			Box(sb, textX - 3f, field.Y + 9f, Math.Min(right - textX, TextWidth(QuickQuery, 0.95f)) + 6f, field.Height - 18f, Accent * 0.35f, border: false, 3f);
		}
		CenterLabel(sb, QuickQuery + ((!QuickAllSelected && caret) ? "|" : ""), textX, field.Y + 6f, right - textX, field.Height - 12f, TextColor, 0.95f, rightAligned: false, leftAligned: true);
		BookRect clear = QuickClear;
		bool over = clear.Contains(Mouse.X, Mouse.Y);
		if (over)
		{
			Box(sb, clear, HoverFill, border: false, 6f);
			MouseTip(L("Common.ClearSearch"));
		}
		Cross(sb, clear.X + clear.Width / 2f, clear.Y + clear.Height / 2f, 4f, over ? TextColor : Muted);
	}

	private static void DrawQuickRows(SpriteBatch sb)
	{
		QuickSegments.Clear();
		QuickScroll = Math.Clamp(QuickScroll, 0, QuickMaxScroll());
		BookRect list = QuickList;
		List<(int Row, BookRect Rect)> visible = QuickVisible();
		foreach ((int index, BookRect rect) in visible)
		{
			DrawQuickRow(sb, QuickRows[index], rect, index == QuickSelected);
		}
		if (QuickRows.Count == 0)
		{
			EmptyState(sb, list, L("QuickSearch.EmptyTitle"), L("QuickSearch.EmptyHint"));
		}
		else if (QuickRows[0].Kind == QuickKind.TextSearch)
		{
			EmptyState(sb, new BookRect(list.X, list.Y + 48f, list.Width, list.Height - 48f), L("QuickSearch.NothingFound", QuickQuery.Trim()), L("QuickSearch.NothingFoundHint"));
		}
		// A thin scroll bar when the rows do not fit, as the mod menu has.
		int max = QuickMaxScroll();
		if (max > 0)
		{
			float track = list.Height;
			float thumb = Math.Max(24f, track * visible.Count / QuickRows.Count);
			float x = list.X + list.Width - 4f;
			Box(sb, x, list.Y, 3f, track, Raised, border: false, 1f);
			Box(sb, x, list.Y + (track - thumb) * QuickScroll / max, 3f, thumb, Accent, border: false, 1f);
		}
	}

	private static void DrawQuickRow(SpriteBatch sb, QuickRow row, BookRect rect, bool selected)
	{
		if (row.Kind == QuickKind.Header)
		{
			string title = QuickGroupName(row.Group) + ((row.Count > 0) ? (" · " + Count(row.Count)) : "");
			SectionLabel(sb, title, rect.X + 8f, rect.Y + 6f, rect.Width - 16f);
			return;
		}
		if (selected)
		{
			Box(sb, rect, SelectedFill, border: true, 6f, Accent, 2);
		}
		Color text = selected ? Accent : TextColor;
		bool linked = row.Link != QuickLink.None;
		if (linked)
		{
			// Under the match it belongs to: an elbow from the parent's icon.
			Line(sb, rect.X + 22f, rect.Y - 4f, rect.X + 22f, rect.Y + rect.Height / 2f, Border, 1.5f);
			Line(sb, rect.X + 22f, rect.Y + rect.Height / 2f, rect.X + 36f, rect.Y + rect.Height / 2f, Border, 1.5f);
		}
		if (row.Link is QuickLink.AllSources or QuickLink.AllLoot || row.Kind == QuickKind.ShowAll)
		{
			string more = row.Link switch
			{
				QuickLink.AllSources => L("QuickSearch.AllSources", Count(row.Count)),
				QuickLink.AllLoot => L("QuickSearch.AllLoot", Count(row.Count)),
				_ => row.Group switch
				{
					QuickGroup.Items => L("QuickSearch.AllInItems", Count(row.Count)),
					QuickGroup.Mobs => L("QuickSearch.AllInBestiary", Count(row.Count)),
					_ => L("QuickSearch.More", Count(row.Count - QuickCaps[(int)row.Group]))
				}
			};
			float x = rect.X + (linked ? 44f : 48f);
			float width = Math.Min(TextWidth(more, 0.85f) + 2f, rect.Width - (x - rect.X) - 24f);
			CenterLabel(sb, more, x, rect.Y + 3f, width, rect.Height - 6f, Accent, 0.85f, rightAligned: false, leftAligned: true);
			Chevron(sb, x + width + 10f, rect.Y + rect.Height / 2f, 3.5f, 3, Accent);
			return;
		}
		float size = linked ? 22f : 28f;
		BookRect icon = new BookRect(rect.X + (linked ? 44f : 8f), rect.Y + (rect.Height - size) / 2f, size, size);
		DrawQuickIcon(sb, row, icon);
		float left = icon.X + size + 10f;
		float right = QuickBadge(sb, rect, QuickBadgeText(row), selected) - 12f;
		float textSize = linked ? 0.85f : 0.9f;
		string name = QuickRowName(row);
		float nameWidth = Math.Max(0f, Math.Min(TextWidth(name, textSize) + 2f, right - left));
		CenterLabel(sb, name, left, rect.Y + 4f, nameWidth, rect.Height - 8f, linked ? (selected ? Accent : Muted * 1.15f) : text, textSize, rightAligned: false, leftAligned: true);
		float at = left + nameWidth;
		string hint = row.Hint.Length > 0 ? row.Hint : QuickRowDetail(row);
		if (hint.Length > 0 && right - at > 48f)
		{
			at = QuickSeparator(sb, "·", at, rect);
			float width = Math.Min(TextWidth(hint, 0.8f) + 2f, right - at);
			CenterLabel(sb, hint, at, rect.Y + 4f, width, rect.Height - 8f, Muted, 0.8f, rightAligned: false, leftAligned: true);
			at += width;
		}
		if (linked || right - at <= 48f)
		{
			return;
		}
		// The item's place as its card names it (BookCategoryPath: group › category), a mob's kind and spawn tag; each
		// step opens its category.
		List<(string Label, int Category, bool Mobs)> path = new List<(string, int, bool)>();
		if (row.Kind == QuickKind.Item)
		{
			path.AddRange(BookCategoryPath.Of(row.Id).Select((BookCategory c) => (c.Label(), ItemCategoryList.FindIndex((BookCategory listed) => listed.Key == c.Key), false)));
		}
		else if (row.Kind == QuickKind.Mob)
		{
			path.AddRange(BookMobCategoryPath.Of(row.Id).Select((BookBestiary.Tag t) => (BookBestiary.Label(t), BookBestiary.Tags.IndexOf(t), true)));
		}
		for (int i = 0; i < path.Count && right - at > 40f; i++)
		{
			at = QuickSeparator(sb, (i == 0 || path[i].Mobs) ? "·" : "›", at, rect);
			float width = Math.Min(TextWidth(path[i].Label, 0.8f) + 2f, right - at);
			BookRect segment = new BookRect(at, rect.Y + 6f, width, rect.Height - 12f);
			bool over = segment.Contains(Mouse.X, Mouse.Y);
			CenterLabel(sb, path[i].Label, at, rect.Y + 4f, width, rect.Height - 8f, over ? Accent : Muted, 0.8f, rightAligned: false, leftAligned: true);
			if (over)
			{
				Line(sb, at, rect.Y + rect.Height - 9f, at + width - 2f, rect.Y + rect.Height - 9f, Accent, 1f);
			}
			if (path[i].Category >= 0)
			{
				QuickSegments.Add((segment, path[i].Category, path[i].Mobs));
			}
			at += width;
		}
	}

	// Labels are placed by their ink, so a spaced " · " would lose its spaces: the gaps are explicit.
	private static float QuickSeparator(SpriteBatch sb, string glyph, float at, BookRect rect)
	{
		at += 7f;
		float width = TextWidth(glyph, 0.8f);
		CenterLabel(sb, glyph, at, rect.Y + 4f, width + 2f, rect.Height - 8f, Muted, 0.8f, rightAligned: false, leftAligned: true);
		return at + width + 7f;
	}

	// A vanilla texture nobody drew yet is read from disk on the first frame that asks (Main.LoadItem, several ms each:
	// the first query's rows took 50-90 ms). The row asks for it in the background and shows the icon once it is in, so
	// typing never waits for icons.
	private static bool QuickTextureReady(Asset<Texture2D>[] textures, int index)
	{
		if (index <= 0 || index >= textures.Length)
		{
			return false;
		}
		Asset<Texture2D> asset = textures[index];
		if (asset.State == AssetState.NotLoaded)
		{
			Main.Assets.Request<Texture2D>(asset.Name, AssetRequestMode.AsyncLoad);
		}
		return asset.IsLoaded;
	}

	private static void DrawQuickIcon(SpriteBatch sb, QuickRow row, BookRect icon)
	{
		switch (row.Kind)
		{
			case QuickKind.Item:
				if (QuickTextureReady(TextureAssets.Item, row.Id) && ContentSamples.ItemsByType.TryGetValue(row.Id, out Item item))
				{
					Icon(sb, item, icon.X, icon.Y, icon.Width, tooltip: false);
				}
				break;
			case QuickKind.Mob:
			{
				int netId = BookBestiary.Mobs[row.Id].NetId;
				if (ContentSamples.NpcsByNetId.TryGetValue(netId, out NPC npc) && QuickTextureReady(TextureAssets.Npc, npc.type))
				{
					DrawNpcIcon(sb, netId, icon);
				}
				break;
			}
			case QuickKind.Stage:
				DrawBossHead(sb, row.Id, icon, 1f);
				break;
			case QuickKind.ItemCategory:
				if (QuickTextureReady(TextureAssets.Item, ItemCategoryList[row.Id].IconType))
				{
					DrawCategoryIcon(sb, ItemCategoryList[row.Id], new BookRect(icon.X - 4f, icon.Y, icon.Width, icon.Height), mobs: false);
				}
				break;
			case QuickKind.MobCategory:
				if (MobCategories[row.Id].IconType <= 0 || QuickTextureReady(TextureAssets.Item, MobCategories[row.Id].IconType))
				{
					DrawCategoryIcon(sb, MobCategories[row.Id], new BookRect(icon.X - 4f, icon.Y, icon.Width, icon.Height), mobs: true);
				}
				break;
			case QuickKind.Mod:
				DrawModIcon(sb, QuickMods[row.Id], icon);
				break;
			default:
				Magnifier(sb, icon.X + icon.Width / 2f, icon.Y + icon.Height / 2f, Muted);
				break;
		}
	}

	// The mod's icon.png; vanilla gets the Guide's book.
	private static void DrawModIcon(SpriteBatch sb, string mod, BookRect rect)
	{
		if (!QuickModIcons.TryGetValue(mod, out Asset<Texture2D> asset))
		{
			if (mod == "Terraria" || !ModContent.RequestIfExists(mod + "/icon", out asset, AssetRequestMode.AsyncLoad))
			{
				asset = null;
			}
			QuickModIcons[mod] = asset;
		}
		if (asset == null || !asset.IsLoaded)
		{
			if (QuickTextureReady(TextureAssets.Item, ItemID.Book) && ContentSamples.ItemsByType.TryGetValue(ItemID.Book, out Item book))
			{
				Icon(sb, book, rect.X, rect.Y, rect.Width, tooltip: false);
			}
			return;
		}
		Texture2D texture = asset.Value;
		float scale = Math.Min(rect.Width / texture.Width, rect.Height / texture.Height);
		sb.Draw(texture, Origin + new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f) * Scale, null, Color.White * GraphicsOpacity, 0f, texture.Size() / 2f, scale * Scale, SpriteEffects.None, 0f);
	}

	// What a row is and where it opens, on the right; returns the badge's left edge.
	private static float QuickBadge(SpriteBatch sb, BookRect rect, string label, bool selected)
	{
		float width = TextWidth(label, 0.8f) + 16f;
		float x = rect.X + rect.Width - width - 8f;
		float y = rect.Y + (rect.Height - 22f) / 2f;
		Box(sb, x, y, width, 22f, selected ? new Color(40, 62, 48) : Raised, border: true, 11f, selected ? Accent : Border);
		CenterLabel(sb, label, x, y, width, 22f, selected ? Accent : Muted, 0.8f);
		return x;
	}

	private static string QuickBadgeText(QuickRow row)
	{
		return row.Kind switch
		{
			QuickKind.Item => L("QuickSearch.BadgeItem"),
			QuickKind.Mob => (BookBestiary.Mobs[row.Id].Kind == BookMobKind.Boss) ? L("QuickSearch.BadgeBoss") : L("QuickSearch.BadgeMob"),
			QuickKind.Stage => L("QuickSearch.BadgeStage"),
			QuickKind.ItemCategory or QuickKind.MobCategory => L("QuickSearch.BadgeCategory"),
			QuickKind.Mod => L("QuickSearch.BadgeMod"),
			_ => TabLabel(0)
		};
	}

	private static string QuickRowName(QuickRow row)
	{
		string name = row.Kind switch
		{
			QuickKind.Item => ContentSamples.ItemsByType[row.Id].Name,
			QuickKind.Mob => BookBestiary.Mobs[row.Id].Sample.FullName,
			QuickKind.Stage => L("Stages.Before", BookSteps.Name(row.Id)),
			QuickKind.ItemCategory => ItemCategoryList[row.Id].Label(),
			QuickKind.MobCategory => BookBestiary.Label(BookBestiary.Tags[row.Id]),
			QuickKind.Mod => ModName(QuickMods[row.Id]),
			_ => L("QuickSearch.SearchDescriptions")
		};
		return row.Link switch
		{
			QuickLink.DroppedBy => L("QuickSearch.DroppedBy", name),
			QuickLink.FoundIn => L("QuickSearch.FoundIn", name),
			QuickLink.Drops => L("QuickSearch.Drops", name),
			_ => name
		};
	}

	// After the name: a stage's state, a category's or a mod's size.
	private static string QuickRowDetail(QuickRow row)
	{
		switch (row.Kind)
		{
			case QuickKind.Stage:
			{
				BookSteps.Step boss = BookSteps.All[row.Id];
				return (row.Id == BookSteps.Next()) ? L("Stages.NextBoss") : ((!boss.Available()) ? L("Stages.NotInThisWorldTheOther") : (BookSteps.SafeDowned(boss) ? L("Stages.AlreadyDown") : L("Stages.NotDownYet")));
			}
			case QuickKind.ItemCategory:
				return (row.Id < QuickCategorySizes.Length) ? L("QuickSearch.ItemCount", QuickCategorySizes[row.Id]) : "";
			case QuickKind.MobCategory:
				return (BookBestiary.Tags[row.Id].Key == BookBestiary.RecentKey) ? "" : L("QuickSearch.MobCount", BookBestiary.Tags[row.Id].Members.Count);
			case QuickKind.Mod:
			{
				(int items, int mobs) = QuickModSizes.GetValueOrDefault(QuickMods[row.Id]);
				return (items > 0) ? L("QuickSearch.ItemCount", items) : L("QuickSearch.MobCount", mobs);
			}
			default:
				return "";
		}
	}

	private static string QuickGroupName(QuickGroup group)
	{
		return group switch
		{
			QuickGroup.Items => TabLabel(0),
			QuickGroup.Mobs => TabLabel(2),
			QuickGroup.Stages => L("QuickSearch.GroupStages"),
			QuickGroup.ItemCategories => L("QuickSearch.GroupItemCategories"),
			QuickGroup.MobCategories => L("QuickSearch.GroupMobCategories"),
			QuickGroup.Mods => L("QuickSearch.GroupMods"),
			QuickGroup.Opened => L("QuickSearch.GroupOpened"),
			QuickGroup.Crafted => L("QuickSearch.GroupCrafted"),
			_ => L("QuickSearch.GroupSeen")
		};
	}

	// The keys, as the station menu names its own.
	private static void DrawQuickFooter(SpriteBatch sb)
	{
		BookRect panel = QuickPanel;
		float y = panel.Y + panel.Height - 36f;
		Box(sb, panel.X + 16f, y - 8f, panel.Width - 32f, 1f, Border, border: false, 0f);
		float x = panel.X + 20f;
		foreach ((string key, string action) in new (string, string)[4]
		{
			(L("QuickSearch.KeyEnter"), L("QuickSearch.Open")),
			(L("QuickSearch.KeyShiftEnter"), L("Common.UsedIn")),
			(L("Help.KeyCtrlEnter"), L("Help.HowToGet")),
			(L("Help.KeyEsc"), L("QuickSearch.Close"))
		})
		{
			float keyWidth = TextWidth(key, 0.8f) + 16f;
			HelpKeycap(sb, new BookRect(x, y, keyWidth, 24f), key);
			x += keyWidth + 6f;
			float actionWidth = TextWidth(action, 0.8f) + 2f;
			CenterLabel(sb, action, x, y, actionWidth, 24f, Muted, 0.8f, rightAligned: false, leftAligned: true);
			x += actionWidth + 16f;
		}
	}

	// Stand: index build, a ranking pass for a query and the same pass reading every link (cold after the rebuild), in
	// milliseconds, and what it found.
	internal static string QuickSearchCost(string query)
	{
		QuickIndexKey = default;
		EnsureQuickIndex();
		string keep = QuickQuery;
		QuickQuery = query;
		RunQuickSearch();
		double search = QuickSearchMs;
		RunQuickSearch(int.MaxValue);
		string rows = string.Join(" | ", QuickRows.Take(16).Select((QuickRow r) => r.Kind == QuickKind.Header ? ("[" + QuickGroupName(r.Group) + " " + r.Count + "]") : (r.Kind + ":" + ((r.Kind == QuickKind.ShowAll) ? r.Count.ToString(CultureInfo.InvariantCulture) : QuickRowName(r)) + ((r.Hint.Length > 0) ? (" (" + r.Hint + ")") : ""))));
		string cost = string.Create(CultureInfo.InvariantCulture, $"index {QuickIndex.Count} entries in {QuickIndexMs:F1} ms; search {search:F2} ms, with links {QuickSearchMs:F2} ms; english {(BookEnglishNames.Ready ? "ready" : (BookEnglishNames.Needed ? "reading" : "not needed"))}: ");
		QuickQuery = keep;
		RunQuickSearch();
		return cost + rows;
	}
}
