using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace ModernRecipeBook;

public sealed partial class RecipeBookSystem : ModSystem
{
	private readonly record struct BatchEstimate(int Count, ulong Tick, int AvailabilityVersion, int SourcesVersion);

	private sealed class TrackedGoal
	{
		internal Recipe Recipe;

		internal Recipe[] Recipes;

		internal Item[] Parts;

		internal string Key;

		internal int Batches;

		internal int Possible;

		internal int ResourcePage;

		internal bool Ready;

		internal bool Unknown;

		internal List<MaterialRequirement> Materials = new List<MaterialRequirement>();

		internal Dictionary<int, int> Counts = new Dictionary<int, int>();

		// One row per entry of Materials, same order; refreshed with Counts.
		internal BookResourceProgress[] Progress = Array.Empty<BookResourceProgress>();

		internal readonly BookTrackerSignal Signal = new BookTrackerSignal();

		internal readonly BookTrackerDismiss Dismiss = new BookTrackerDismiss();

		internal bool IsSet => Parts.Length == 3;
	}

	// Alternatives: the item types of a merged recipe's "A or B" row (BookRecipeVariants), Ingredient's first.
	private sealed record MaterialRequirement(Item Ingredient, RecipeGroup Group, string Name, HashSet<int> ItemTypes, bool Overlapping, int[] Alternatives = null);

	// Alternatives: an "A or B" row of a merged recipe; SourceTypes then lists them, Icon's first.
	private sealed record Detail(Item Icon, string Name, string Sub, Color Color, Color SubColor, string Tooltip, int[] SourceTypes = null, bool Alternatives = false);

	private sealed record MaterialPreview(string Summary, Color Color, string Materials);

	private static bool AcquisitionOpen;

	// The inventory key was pressed this update with a key other than Esc (BookPlayer.GuardTriggers): it acts as Esc.
	internal static bool InventoryKeyPressed;

	// Something above the book that Esc closes first.
	internal static bool LayerOpen => Typing || HelpOpen || AcquisitionOpen || MenuOpen;

	// The book as the player saw it before pressing the inventory key: the end of the update before last. The book
	// reacts to Esc on the live keyboard (the help and the search field while drawing, the rest in UpdateUI), but the
	// game's triggers read the keyboard one update late (Main.keyState is refreshed after PlayerInput.UpdateInput), so
	// when BookPlayer.GuardTriggers sees the key, the last update's state may already show Esc's effect.
	internal static bool OpenAtUpdate;

	internal static bool LayerAtUpdate;

	internal static bool TypingAtUpdate;

	private static bool OpenLastUpdate;

	private static bool LayerLastUpdate;

	private static bool TypingLastUpdate;

	private static int[] AcquisitionVariants = Array.Empty<int>();

	private static int AcquisitionVariant;

	private static int AcquisitionPage;

	private static List<BookAcquisitionGroup> AcquisitionSources = new List<BookAcquisitionGroup>();

	private static int AcquisitionGroup = -1;

	private static int AcquisitionRulePage;

	private static int AcquisitionReadErrors;

	private static int ArmorFocus;

	private const int BatchLimit = 100;

	private static int BatchChoice;

	private static readonly Dictionary<Recipe, BatchEstimate> BatchEstimates = new Dictionary<Recipe, BatchEstimate>();

	private static bool PendingBack;

	private static bool PendingForward;

	private static readonly List<TrackedGoal> TrackedGoals = new List<TrackedGoal>();

	internal static bool HasTrackedGoals => TrackedGoals.Count > 0;

	internal static bool StorageTabOpen => Open && StorageView;

	// The Storage tab cell under the mouse this frame; its tooltip gets the "Chests ×3: 120" line.
	internal static Item StorageHover;

	private static TrackedGoal ActiveGoal;

	private static int TrackerTicks;

	internal static bool BlockTrackerPress;

	private static bool TrackerLastLeft;

	private static bool TrackerLastRight;

	private static bool TrackerDragging;

	private static bool TrackerCustomPosition;

	private static float TrackerPositionX;

	private static float TrackerPositionY = 1f;

	private static float TrackerDragX;

	private static float TrackerDragY;

	private static float GraphicsOpacity = 1f;

	private static int EnemyReadErrors;

	private static string EnemyBiomes = "";

	private static string EnemySpawn = "";

	private static List<BookEnemyLoot> EnemyLoot = new List<BookEnemyLoot>();

	private static BookStageFilter StageFilter;

	// Global like the stage filter (the sidebar's class row); ClassHidden counts what it alone hid last time.
	private static BookClass ClassFilter;

	private static int ClassHidden;

	private static bool? LastResourceTrackerStyle;

	private static readonly BookTrackerSignal ResourceTrackerSignal = new BookTrackerSignal();

	private static readonly BookTrackerDismiss ResourceTrackerDismiss = new BookTrackerDismiss();

	private static BookResourceProgress[] ResourceTrackerRows = Array.Empty<BookResourceProgress>();

	private static readonly Dictionary<string, MaterialRequirement> ResourceTrackerMaterials = new Dictionary<string, MaterialRequirement>();

	private static readonly Color EnoughColor = new Color(130, 214, 140);

	private static readonly Color MissingColor = new Color(236, 136, 116);

	private static readonly Color ConditionColor = new Color(223, 185, 139);

	// Terraria's own green (rarity 2), readable on the bright world behind the compact lines.
	private static readonly Color InfoDoneColor = new Color(150, 255, 150);

	private static TrackedGoal TrackingSelection;

	private static ulong TrackingClearArmedAt;

	// Armed for three seconds after the first click on Clear.
	private static bool TrackingClearArmed => TrackingClearArmedAt != 0 && Main.GameUpdateCount - TrackingClearArmedAt < 180;

	private const string PlayerUiStateTag = "ModernRecipeBookUI";

	private static BookSortMode SortMode = BookSortMode.Name;

	private static bool SortDescending;

	private static int WindowScalePercent = 100;

	private static bool HasCustomWindowPosition;

	private static float WindowPositionX = 0.5f;

	private static float WindowPositionY = 0.5f;

	private static bool DraggingWindow;

	private static float WindowDragOffsetX;

	private static float WindowDragOffsetY;

	private static BookUiState PendingUiState;

	private static bool RestorePending;

	internal static bool Typing;

	internal static bool MouseOver;

	private static readonly List<int> All = new List<int>();

	private static readonly List<int> AllItems = new List<int>();

	private static readonly Dictionary<int, List<int>> RecipesByItem = new Dictionary<int, List<int>>();

	private static bool Catalogue = true;

	// The card's "Used in" list: recipes that take the card's item, optionally only the craftable ones.
	private static bool UsedInMode;

	private static bool UsedInCraftableOnly;

	private static bool StorageView;

	private static bool RecentView;

	private static bool TrackingView;

	private static bool BestiaryView;

	private static int AppliedStorageVersion = -1;

	private static int AppliedRecentVersion = -1;

	private static int AppliedAvailabilityVersion = -1;

	// Steps taken inside the card (an ingredient, a Used in row, a How to Get recipe) show another item without
	// touching the grid.
	private static int FocusType;

	// Back/Forward (Mouse4/5, Alt+Left/Right, the card's back button) walk every navigation in the book.
	private static readonly BookHistory<NavState> History = new BookHistory<NavState>(100);

	private static bool HistoryTypingEntry;

	private static bool HistoryArrowEntry;

	private static bool ArrowMoved;

	private static MouseState HistoryMouse;

	// Updates left in which a state change still counts as the player's: the game sees a press an update or
	// two after the device reports it.
	private static int ActedUpdates;

	private static KeyboardState HistoryKeys;

	private static readonly Dictionary<int, List<int>> UsedInCache = new Dictionary<int, List<int>>();

	private static int Variant;

	private static int RegisteredRecipes;

	private static int DisabledRecipes;

	private static readonly object SearchOwner = new object();

	private static bool OwnInputBlock;

	private static bool LastMiddle;

	private static readonly List<int> Filtered = new List<int>();

	// Every mod with an item or a recipe: what a saved mod filter is checked against.
	private static readonly List<string> ItemMods = new List<string>();

	// Mods with an item in the catalogue, and mods with a recipe (its own or one for their item).
	private static readonly List<string> CatalogueMods = new List<string>();

	private static readonly List<string> RecipeMods = new List<string>();

	private static readonly List<string> TabMods = new List<string>();

	private static (int Tab, int Version, string Filter, int Count) TabModsKey = (-1, 0, null, -1);

	// The mod dropdown lists only mods that have something on the open tab (a music mod that only adds recipes
	// for other mods' items is not on Items or Bestiary). The chosen mod stays listed even when the tab has
	// nothing from it right now (an emptied storage), so a saved filter never disappears by itself.
	private static List<string> Mods
	{
		get
		{
			int version = StorageView ? BookSources.ItemsVersion : (RecentView ? BookRecent.Version : 0);
			int count = BestiaryView ? MobMods.Count : ItemMods.Count;
			(int, int, string, int) key = (CurrentTab, version, ModFilter, count);
			if (key == TabModsKey)
			{
				return TabMods;
			}
			TabModsKey = key;
			TabMods.Clear();
			IEnumerable<string> source;
			if (BestiaryView)
			{
				source = MobMods;
			}
			else if (Catalogue)
			{
				source = CatalogueMods;
			}
			else if (StorageView)
			{
				source = BookSources.Items.Select((Item item) => item.ModItem?.Mod.Name ?? "Terraria").Distinct();
			}
			else if (RecentView)
			{
				source = BookRecent.ResolveRecipeIndexes().SelectMany((int index) => new string[2]
				{
					Owner(Main.recipe[index]),
					Main.recipe[index].createItem.ModItem?.Mod.Name ?? "Terraria"
				}).Distinct();
			}
			else
			{
				source = RecipeMods;
			}
			TabMods.AddRange(source);
			if (ModFilter.Length > 0 && !TabMods.Contains(ModFilter))
			{
				TabMods.Add(ModFilter);
			}
			TabMods.Sort((string a, string b) => string.Compare(ModName(a), ModName(b), StringComparison.CurrentCultureIgnoreCase));
			return TabMods;
		}
	}

	private static bool KnownMod(string mod)
	{
		return ItemMods.Contains(mod, StringComparer.Ordinal) || MobMods.Contains(mod, StringComparer.Ordinal);
	}

	private static readonly Dictionary<int, Item> StationItems = new Dictionary<int, Item>();

	// Read live: Item.Name follows the game's language, a name cached at index time would not. A station no item places
	// (the Demon or Crimson Altar) takes its map name in the style vanilla's crafting list uses (Recipe.GetRequiredTileStyle).
	private static string StationName(int tile)
	{
		return StationItems.TryGetValue(tile, out var station) ? station.Name : MapTileName(tile, Recipe.GetRequiredTileStyle(tile));
	}

	// A recipe condition's text. A mod that never translated its key shows the key itself
	// ("Mods.MagicStorage.CookedMarshmallowCondition"): known ones get their meaning, others the key's last part in words.
	private static string ConditionName(Condition condition)
	{
		string text = condition.Description.Value;
		if (text != condition.Description.Key)
		{
			return text;
		}
		if (text == "Mods.MagicStorage.CookedMarshmallowCondition")
		{
			return T("Рядом костёр (Magic Storage)", "Near a campfire (Magic Storage)");
		}
		string last = text[(text.LastIndexOf('.') + 1)..];
		if (last.EndsWith("Condition", StringComparison.Ordinal) && last.Length > "Condition".Length)
		{
			last = last[..^"Condition".Length];
		}
		return System.Text.RegularExpressions.Regex.Replace(last, "(?<=[a-z])(?=[A-Z])", " ");
	}

	private static int Selected = -1;

	private static int Category;

	private static int Page;

	private static int DetailsPage;

	private static int ModOffset;

	private static int CategoryRow;

	private static List<BookCategory> ItemCategoryList = new List<BookCategory>();

	private static List<BookCategory> Categories => BestiaryView ? MobCategories : ItemCategoryList;

	private static readonly Dictionary<int, HashSet<int>> ItemCategories = new Dictionary<int, HashSet<int>>();

	private static string Query = "";

	private static string ModFilter = "";

	private static bool FavoritesOnly;

	private static bool ModMenu;

	private static bool SortMenu;

	private static bool StageMenu;

	// Storage tab: the list of networks in reach, open under the "Network: …" dropdown.
	private static bool Dirty = true;

	private static bool LastLeft;

	private static bool LastRight;

	private static bool LastEscape;

	private static bool HelpOpen;

	private static KeyboardState LastKeys;

	private static int LastWheel;

	private static Vector2 Origin;

	private static Vector2 Mouse;

	private static float Scale = 1f;

	private const int Width = 1120;

	private const int Height = 696;

	private const int PageSize = 40;

	private const int DetailPageSize = 3;

	private static readonly Color Panel = new Color(25, 32, 28);

	private static readonly Color Surface = new Color(31, 41, 34);

	private static readonly Color Raised = new Color(39, 51, 43);

	private static readonly Color Border = new Color(62, 80, 67);

	private static readonly Color TextColor = new Color(233, 242, 235);

	private static readonly Color Muted = new Color(166, 188, 172);

	private static readonly Color Accent = new Color(178, 218, 191);

	// -1 when every card is collapsed.
	private static int TrackerActiveIndex => (ActiveGoal == null) ? (-1) : TrackedGoals.IndexOf(ActiveGoal);

	private static int TrackerActiveMaterials => ActiveGoal?.Materials.Count ?? 0;

	private static bool ResourceTrackerStyle
	{
		get
		{
			if (!ModContent.GetInstance<BookConfig>().ResourceOnlyTracker)
			{
				return TrackedGoals.Count > 5;
			}
			return true;
		}
	}

	private static int TrackerPanelHeight => BookTrackerRules.ListHeight(TrackedGoals.Count, TrackerActiveIndex, TrackerActiveMaterials);

	private static int TrackingGoalPages => BookTrackingSelectionRules.Pages(TrackedGoals.Count, BookGeometry.TrackingPageSize);

	internal static bool UiStateInitialized { get; private set; }

	public static bool Open { get; private set; }

	internal static bool ItemIconSearchAllowed
	{
		get
		{
			if (Open)
			{
				if (!HelpOpen && !AcquisitionOpen && !MenuOpen)
				{
					return !DraggingWindow;
				}
				return false;
			}
			return true;
		}
	}

	internal static bool CaptureKeyboard
	{
		get
		{
			// The station menu takes the arrows, Enter and Space while it is open.
			if (Typing || HelpOpen || AcquisitionOpen || StationMenu)
			{
				return true;
			}
			if (Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.blockInput)
			{
				return false;
			}
			KeyboardState keys = Keyboard.GetState();
			if (!keys.IsKeyDown(Keys.LeftControl) && !keys.IsKeyDown(Keys.RightControl) && !keys.IsKeyDown(Keys.LeftAlt))
			{
				return keys.IsKeyDown(Keys.RightAlt);
			}
			return true;
		}
	}


	// Storage keeps its toolbar above a 5-row grid; the other tabs show 6 rows.
	private static int ActivePageSize => StorageView ? 40 : 48;

	internal static bool Ru => Language.ActiveCulture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

	private static HashSet<string> Favorites => Main.LocalPlayer.GetModPlayer<BookPlayer>().Favorites;

	private static int MaxCategoryRow => Math.Max(0, Categories.Count - CategoryRows);

	// The class row is about items: the Bestiary tab's sidebar has only its categories.
	private static bool ClassRowShown => !BestiaryView;

	private static int CategoryRows => ClassRowShown ? BookGeometry.VisibleCategoryRows : BookGeometry.VisibleCategoryRowsWithoutClass;

	// The item the card shows with all its recipes: an armor piece picked in the set view, a step taken in the
	// card, or the selected item on Items. 0 when the card shows the grid's selected recipe.
	private static int CardItemType
	{
		get
		{
			if (StorageView || TrackingView || BestiaryView)
			{
				return 0;
			}
			if (ArmorFocus > 0)
			{
				return ArmorFocus;
			}
			if (FocusType > 0)
			{
				return FocusType;
			}
			return (Catalogue && Selected >= 0) ? Selected : 0;
		}
	}

	private static Item SelectedItem
	{
		get
		{
			if (TrackingView || BestiaryView)
			{
				return null;
			}
			int type = CardItemType;
			if (type > 0)
			{
				return ContentSamples.ItemsByType[type];
			}
			if (Selected < 0 || (StorageView && Selected >= BookSources.Items.Count))
			{
				return null;
			}
			return Result(Selected);
		}
	}

	private static Recipe SelectedRecipe
	{
		get
		{
			if (StorageView || TrackingView || BestiaryView)
			{
				return null;
			}
			int type = CardItemType;
			if (type <= 0)
			{
				return (Selected >= 0) ? ActiveMember(Selected) : null;
			}
			List<BookRecipeVariants.Variant> variants = BookRecipeVariants.For(type);
			if (variants.Count == 0)
			{
				return null;
			}
			Variant = Math.Clamp(Variant, 0, variants.Count - 1);
			return ActiveMember(variants[Variant].Representative);
		}
	}

	private static int VariantCount
	{
		get
		{
			int type = CardItemType;
			if (type <= 0)
			{
				return (Selected >= 0 && !StorageView && !TrackingView && !BestiaryView) ? 1 : 0;
			}
			return BookRecipeVariants.For(type).Count;
		}
	}

	private static int Pages
	{
		get
		{
			if (!TrackingView)
			{
				return Math.Max(1, (Filtered.Count + ActivePageSize - 1) / ActivePageSize);
			}
			return TrackingGoalPages;
		}
	}

	private static float TextScale => (float)Math.Clamp(ModContent.GetInstance<BookConfig>().TextScalePercent, 90, 115) / 100f;

	private static void CloseAcquisition()
	{
		AcquisitionOpen = false;
		AcquisitionVariants = Array.Empty<int>();
		AcquisitionSources.Clear();
		AcquisitionVariant = (AcquisitionPage = (AcquisitionReadErrors = (AcquisitionRulePage = 0)));
		AcquisitionGroup = -1;
	}

	private static void OpenAcquisition(int primary, IEnumerable<int> alternatives)
	{
		AcquisitionVariants = (from type in BookAcquisitionRules.Variants(primary, alternatives)
			where ContentSamples.ItemsByType.TryGetValue(type, out var value) && !value.IsAir
			select type).ToArray();
		if (AcquisitionVariants.Length != 0)
		{
			SetTyping(value: false);
			ModMenu = (StationMenu = (HelpOpen = false));
			AcquisitionVariant = (AcquisitionPage = 0);
			AcquisitionOpen = true;
			ReadAcquisitionSources();
		}
	}

	private static void ReadAcquisitionSources()
	{
		int type = AcquisitionVariants[AcquisitionVariant];
		AcquisitionPage = (AcquisitionReadErrors = 0);
		AcquisitionGroup = -1;
		AcquisitionRulePage = 0;
		List<BookAcquisitionEntry> reports = new List<BookAcquisitionEntry>();
		if (RecipesByItem.TryGetValue(type, out var recipes))
		{
			HashSet<int> shownVariants = new HashSet<int>();
			foreach (int index in recipes)
			{
				Recipe recipe = Main.recipe[index];
				// Recipes that differ in one ingredient are one row, as in the card: "A or B ×10".
				if (recipe.Disabled || !shownVariants.Add(BookRecipeVariants.Representative(index)))
				{
					continue;
				}
				int[] alternatives = BookRecipeVariants.Alternatives(index, out int alternativeSlot);
				IEnumerable<string> materials = recipe.requiredItem.Select((Item i, int slot) => (i, slot)).Where(((Item i, int slot) x) => !x.i.IsAir).Select(((Item i, int slot) x) => ((alternatives != null && x.slot == alternativeSlot) ? AlternativesName(alternatives, x.i.type) : GroupName(recipe, x.i, out var _)) + " ×" + x.i.stack);
				List<string> stations = recipe.requiredTile.Where((int t) => t >= 0).Select(StationName).ToList();
				// Row: stations as the title, the station's icon, conditions or the recipe's mod below; materials on hover.
				string conditionText = string.Join("; ", recipe.Conditions.Select(ConditionName));
				int stationItem = recipe.requiredTile.Where((int t) => t >= 0).Select((int t) => StationItems.TryGetValue(t, out var station) ? station.type : 0).FirstOrDefault((int t) => t > 0);
				Add(new BookAcquisitionEntry((stations.Count == 0) ? T("Вручную", "By hand") : string.Join(", ", stations), string.Join(", ", materials), BookAcquisitionKind.Craft, "", "×" + recipe.createItem.stack, (stationItem > 0) ? stationItem : type, 0, recipe.Conditions.Count > 0, index, Owner(recipe), conditionText));
			}
		}
		// Everything else comes from the shared source model, the same one the stages are computed from. What the
		// world itself gives (plants, ores, trees, fishing) leads: for raw materials it is the main way to get them.
		List<BookAcquisitionEntry> sources = SourceEntries(type);
		reports.InsertRange(0, sources.Where((BookAcquisitionEntry e) => IsNatural(e.Kind)));
		reports.AddRange(sources.Where((BookAcquisitionEntry e) => !IsNatural(e.Kind)));
		AcquisitionReadErrors += BookItemSources.ReadErrors;
		if (reports.Count == 0)
		{
			Add(new BookAcquisitionEntry(T("Нет обычного источника", "No regular source"), "", BookAcquisitionKind.Unknown));
		}
		AcquisitionSources = BookAcquisitionGrouping.Build(reports);
		if (AcquisitionReadErrors > 0)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn($"Acquisition lookup for item {type}: {AcquisitionReadErrors} source reports/descriptions could not be read.");
		}
		void Add(BookAcquisitionEntry source)
		{
			reports.Add(source);
		}
	}

	private static void OpenAcquisitionRecipe(int index)
	{
		if (index >= 0 && index < Recipe.numRecipes && Main.recipe[index] != null && !Main.recipe[index].Disabled && All.Contains(index))
		{
			int type = Main.recipe[index].createItem.type;
			CloseAcquisition();
			if (StorageView || TrackingView || BestiaryView)
			{
				SwitchTab(0);
			}
			StepCard(type, usedIn: false);
			Variant = Math.Max(0, BookRecipeVariants.For(type).IndexOf(BookRecipeVariants.Of(index)));
		}
	}

	private static void BackAcquisitionGroup()
	{
		AcquisitionGroup = -1;
		AcquisitionRulePage = 0;
	}

	// Difficulty conditions get a one-word label; anything else keeps the mod's own description.
	// A drop condition its mod left without a text (Calamity's AddIf lambdas, e.g. Hadarian Wings once Astrum Aureus is
	// dead) cannot be named, but it can be asked whether it holds in this world now.
	private static string HiddenConditionLabel(IItemDropRuleCondition condition, int npcNetId)
	{
		string name = T("Особое условие", "Special condition");
		try
		{
			NPC npc = (npcNetId != 0 && ContentSamples.NpcsByNetId.TryGetValue(npcNetId, out NPC sample)) ? sample : null;
			DropAttemptInfo info = new DropAttemptInfo { player = Main.LocalPlayer, npc = npc, IsExpertMode = Main.expertMode, IsMasterMode = Main.masterMode, IsInSimulation = true, rng = Main.rand };
			return name + (condition.CanDrop(info) ? T(" · выполнено", " · met") : T(" · пока не выполнено", " · not met yet"));
		}
		catch (Exception)
		{
			return name;
		}
	}

	private static string DropConditionLabel(IItemDropRuleCondition condition, string full)
	{
		if (condition is Conditions.IsExpert)
		{
			return T("Эксперт / Мастер", "Expert / Master");
		}
		if (condition is Conditions.NotExpert)
		{
			return T("Обычный", "Classic");
		}
		if (condition is Conditions.IsMasterMode)
		{
			return T("Мастер", "Master");
		}
		if (condition is Conditions.NotMasterMode)
		{
			return T("Не Мастер", "Not Master");
		}
		return full;
	}

	// A row opens something only when there is more to see: its recipe, the enemy card or several drop rules.
	private static bool AcquisitionOpens(BookAcquisitionGroup group)
	{
		if (group.Source.RecipeIndex < 0 && !CanOpenEnemy(group))
		{
			if (group.CanExpand)
			{
				return group.Rules.Length > 1;
			}
			return false;
		}
		return true;
	}

	private static void HandleAcquisitionInput(bool click, KeyboardState keys)
	{
		int scrollWheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		int delta = scrollWheelValue - LastWheel;
		LastWheel = scrollWheelValue;
		bool expanded = AcquisitionGroup >= 0;
		if (expanded && (Press(Keys.Escape) || InventoryKeyPressed || (click && BookGeometry.AcquisitionBack.Contains(Mouse.X, Mouse.Y))))
		{
			BackAcquisitionGroup();
			return;
		}
		if (Press(Keys.Escape) || InventoryKeyPressed || (click && (BookGeometry.AcquisitionClose.Contains(Mouse.X, Mouse.Y) || !BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y))))
		{
			CloseAcquisition();
			return;
		}
		int variantStep = ((!expanded && AcquisitionVariants.Length > 1) ? ((click && BookGeometry.AcquisitionVariantPrevious.Contains(Mouse.X, Mouse.Y)) ? (-1) : ((click && BookGeometry.AcquisitionVariantNext.Contains(Mouse.X, Mouse.Y)) ? 1 : 0)) : 0);
		if (variantStep != 0)
		{
			int next = Math.Clamp(AcquisitionVariant + variantStep, 0, AcquisitionVariants.Length - 1);
			if (next != AcquisitionVariant)
			{
				AcquisitionVariant = next;
				ReadAcquisitionSources();
			}
			return;
		}
		int pageStep = ((Press(Keys.PageUp) || (delta > 0 && BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y)) || (click && BookGeometry.AcquisitionPrevious.Contains(Mouse.X, Mouse.Y))) ? (-1) : ((Press(Keys.PageDown) || (delta < 0 && BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y)) || (click && BookGeometry.AcquisitionNext.Contains(Mouse.X, Mouse.Y))) ? 1 : 0));
		int count = (expanded ? AcquisitionSources[AcquisitionGroup].Rules.Length : AcquisitionSources.Count);
		int page = (expanded ? AcquisitionRulePage : AcquisitionPage);
		page = BookAcquisitionRules.ClampPage(page + pageStep, count);
		if (Press(Keys.Home))
		{
			page = 0;
		}
		if (Press(Keys.End))
		{
			page = BookAcquisitionRules.PageCount(count) - 1;
		}
		if (expanded)
		{
			AcquisitionRulePage = page;
			return;
		}
		AcquisitionPage = page;
		if (!click)
		{
			return;
		}
		for (int row = 0; row < 6; row++)
		{
			int index = AcquisitionPage * 6 + row;
			if (index >= AcquisitionSources.Count || !BookGeometry.AcquisitionRow(row).Contains(Mouse.X, Mouse.Y))
			{
				continue;
			}
			BookAcquisitionGroup source = AcquisitionSources[index];
			if (source.Source.RecipeIndex >= 0)
			{
				OpenAcquisitionRecipe(source.Source.RecipeIndex);
			}
			else if (CanOpenEnemy(source))
			{
				ShowMobFromSource(source);
			}
			else if (AcquisitionOpens(source))
			{
				AcquisitionGroup = index;
				AcquisitionRulePage = 0;
			}
			break;
		}
		bool Press(Keys key)
		{
			if (keys.IsKeyDown(key))
			{
				return !LastKeys.IsKeyDown(key);
			}
			return false;
		}
	}

	private static string AcquisitionKindName(BookAcquisitionKind kind)
	{
		return kind switch
		{
			BookAcquisitionKind.Craft => T("Крафт", "Crafting"),
			BookAcquisitionKind.Drop => T("Добыча", "Drop"),
			BookAcquisitionKind.Container => T("Контейнер", "Container"),
			BookAcquisitionKind.Shop => T("Магазин", "Shop"),
			BookAcquisitionKind.Trade => T("Обмен", "Trade"),
			BookAcquisitionKind.Banner => T("Знамя", "Banner"),
			BookAcquisitionKind.Catch => T("Сачок", "Bug Net"),
			BookAcquisitionKind.Shimmer => T("Мерцание", "Shimmer"),
			BookAcquisitionKind.Use => T("Использование", "Use"),
			BookAcquisitionKind.Record => T("Запись", "Recording"),
			BookAcquisitionKind.Plant => T("Растение", "Plant"),
			BookAcquisitionKind.Tree => T("Дерево", "Tree"),
			BookAcquisitionKind.Mining => T("Копание", "Mining"),
			BookAcquisitionKind.Fishing => T("Рыбалка", "Fishing"),
			BookAcquisitionKind.Chest => T("Сундук", "Chest"),
			BookAcquisitionKind.World => T("Мир", "World"),
			_ => T("Нет данных", "No data"),
		};
	}

	private static void DrawAcquisitionIcon(SpriteBatch sb, BookAcquisitionEntry source, BookRect rect)
	{
		if (source.ItemType > 0 && ContentSamples.ItemsByType.TryGetValue(source.ItemType, out var item))
		{
			Icon(sb, item, rect.X, rect.Y, rect.Width, tooltip: false);
		}
		else if (source.NpcNetId != 0)
		{
			DrawNpcIcon(sb, source.NpcNetId, rect);
		}
		else if (source.TileType >= 0)
		{
			DrawTileIcon(sb, source.TileType, source.FrameX, source.FrameY, rect, (AcquisitionVariants.Length > 0) ? AcquisitionVariants[AcquisitionVariant] : 0);
		}
		else
		{
			Label(sb, rect, "?", Muted);
		}
	}

	// Chance and amount columns: plain right-aligned values, so the numbers line up from row to row.
	private static void AcquisitionBadge(SpriteBatch sb, BookRect rect, string value)
	{
		if (value.Length != 0)
		{
			CenterLabel(sb, value, rect.X, rect.Y, rect.Width, rect.Height, TextColor, 0.95f, rightAligned: true);
		}
	}

	private static string AcquisitionRuleReport(BookAcquisitionRule rule)
	{
		return string.Join("\n", (from x in rule.Reports
			select x.Body into x
			where x.Length > 0
			select x).Distinct(StringComparer.Ordinal));
	}

	// Crafting rows list their materials (several rows can share a station); other rows say what kind of source.
	private static string AcquisitionDetailText(BookAcquisitionGroup group)
	{
		BookAcquisitionEntry source = group.Source;
		if (source.Kind == BookAcquisitionKind.Craft)
		{
			string materials = ((source.Body.Length > 0) ? source.Body : AcquisitionKindName(source.Kind));
			return (source.ConditionLabel.Length > 0) ? (materials + " · " + source.ConditionLabel) : materials;
		}
		string kind = AcquisitionKindName(source.Kind);
		if (group.Rules.Length > 1)
		{
			return kind + " · " + T("зависит от условий", "varies by condition");
		}
		if (source.ConditionLabel.Length > 0)
		{
			return kind + " · " + source.ConditionLabel;
		}
		return kind;
	}

	private static string AcquisitionHint(BookAcquisitionGroup group, bool withName)
	{
		if (group.Source.RecipeIndex >= 0)
		{
			return ((group.Source.Owner != "Terraria") ? (T("Рецепт: ", "Recipe: ") + ModName(group.Source.Owner) + "\n") : "") + T("Открыть рецепт", "Open recipe");
		}
		return (withName ? (group.Source.Name + "\n") : "") + (CanOpenEnemy(group) ? T("Открыть в бестиарии", "Open in Bestiary") : T("Показать условия", "Show conditions"));
	}

	private static void DrawAcquisition(SpriteBatch sb)
	{
		Box(sb, BookGeometry.AcquisitionPanel, Surface, border: true, 12f);
		bool expanded = AcquisitionGroup >= 0;
		BookAcquisitionGroup current = (expanded ? AcquisitionSources[AcquisitionGroup] : null);
		Item item = ContentSamples.ItemsByType[AcquisitionVariants[AcquisitionVariant]];
		BookRect icon = BookGeometry.AcquisitionIcon(expanded);
		if (expanded)
		{
			DrawAcquisitionIcon(sb, current.Source, icon);
		}
		else
		{
			Icon(sb, item, icon.X, icon.Y, icon.Width, tooltip: false);
		}
		bool unknown = !expanded && AcquisitionSources.All((BookAcquisitionGroup x) => x.Source.Kind == BookAcquisitionKind.Unknown);
		string subtitle = (expanded ? (T("Условия · ", "Conditions · ") + item.Name) : (T("Где взять", "How to Get") + (unknown ? "" : (" · " + CountLabel(AcquisitionSources.Count, "источник", "источника", "источников", "source", "sources")))));
		DrawCardHeader(sb, expanded, expanded ? current.Source.Name : item.Name, subtitle, (!expanded) ? AcquisitionVariant : 0, (!expanded) ? AcquisitionVariants.Length : 1, T("Другие подходящие предметы", "Other accepted items"));
		int count = (expanded ? current.Rules.Length : AcquisitionSources.Count);
		int page = (expanded ? AcquisitionRulePage : AcquisitionPage);
		if (unknown)
		{
			DrawCardEmpty(sb, BookGeometry.AcquisitionList, T("Нет обычного источника", "No regular source"), T("Нет в рецептах, добыче, магазинах и мире", "Not in recipes, drops, shops or the world"));
		}
		for (int row = 0; row < 6 && !unknown; row++)
		{
			int index = page * 6 + row;
			if (index >= count)
			{
				break;
			}
			BookAcquisitionGroup group = (expanded ? current : AcquisitionSources[index]);
			BookAcquisitionRule rule = (expanded ? group.Rules[index] : group.Rules[0]);
			BookAcquisitionEntry source = (expanded ? rule.Source : group.Source);
			bool opens = !expanded && AcquisitionOpens(group);
			bool varies = !expanded && group.Rules.Length > 1;
			BookRect bounds = BookGeometry.AcquisitionRow(row);
			bool hover = bounds.Contains(Mouse.X, Mouse.Y);
			Box(sb, bounds, (opens && hover) ? HoverFill : Raised, border: true, 8f);
			DrawAcquisitionIcon(sb, source, BookGeometry.AcquisitionSourceIcon(row));
			string title = (expanded ? ((source.ConditionLabel.Length > 0) ? source.ConditionLabel : T("Без условий", "No conditions")) : source.Name);
			BookRect name = BookGeometry.AcquisitionName(row);
			BookRect detail = BookGeometry.AcquisitionDetail(row);
			Text(sb, title, name.X, name.Y, TextColor, 1f, name.Width, name.Height);
			Text(sb, expanded ? AcquisitionKindName(source.Kind) : AcquisitionDetailText(group), detail.X, detail.Y, Muted, 0.85f, detail.Width, detail.Height);
			AcquisitionBadge(sb, BookGeometry.AcquisitionChance(row), varies ? "" : source.Chance);
			AcquisitionBadge(sb, BookGeometry.AcquisitionAmount(row), varies ? "" : (expanded ? source.Amount : group.Amount));
			if (opens)
			{
				Label(sb, BookGeometry.AcquisitionSourceAction(row), ">", hover ? TextColor : Muted);
			}
			if (hover && opens)
			{
				MouseTip(AcquisitionHint(group, Ink(BookRules.PlainText(title)).Width * TextScale > name.Width));
			}
			else if (hover && expanded)
			{
				string report = AcquisitionRuleReport(rule);
				if (report.Length > 0 && report != title)
				{
					MouseTip(report);
				}
			}
			else if (hover && source.Kind != BookAcquisitionKind.Craft && source.Body.Length > 0 && source.Body != source.ConditionLabel)
			{
				// Rows that open nothing show what the row leaves out: every place, what it grows on.
				MouseTip(source.Body);
			}
		}
		DrawCardFooter(sb, page, BookAcquisitionRules.PageCount(count), (AcquisitionReadErrors > 0) ? T("Часть источников не прочитана", "Some sources could not be read") : "");
	}

	// Button for this zone's panels: disabled is muted with no hover, primary is filled with Accent.
	private static void CardButton(SpriteBatch sb, BookRect rect, string label, bool enabled = true, bool primary = false)
	{
		bool hover = enabled && rect.Contains(Mouse.X, Mouse.Y);
		Color fill = ((!enabled) ? Panel : (primary ? (hover ? new Color(205, 235, 214) : Accent) : (hover ? HoverFill : Raised)));
		Box(sb, rect, fill, border: true, 8f, (!enabled) ? new Color(46, 58, 50) : (primary ? fill : Border));
		if (!primary || !enabled)
		{
			CenterLabel(sb, label, rect.X + 6f, rect.Y + 4f, rect.Width - 12f, rect.Height - 8f, (!enabled) ? new Color(110, 128, 115) : TextColor);
			return;
		}
		// Dark text on the Accent fill, without the black outline every other label gets.
		BookTypography.Line line = BookTypography.Layout(BookRules.PlainText(label), rect.Width - 12f, rect.Height - 8f, 1f, TextScale, (string value) =>
		{
			BookGlyphLayout.Bounds bounds = Ink(value);
			return (Width: bounds.Width, Height: bounds.Height);
		}, centered: true);
		BookGlyphLayout.Bounds ink = Ink(line.Value);
		Vector2 pos = Origin + new Vector2(rect.X + 6f + line.X - ink.X * line.Scale, rect.Y + 4f + line.Y - ink.Y * line.Scale) * Scale;
		sb.DrawString(FontAssets.MouseText.Value, line.Value, pos, Panel * ModalOpacity, 0f, Vector2.Zero, line.Scale * Scale, SpriteEffects.None, 0f);
	}

	// Shared header of the How to Get and enemy cards: [back] [icon] title / subtitle ... [variants] [X].
	private static void DrawCardHeader(SpriteBatch sb, bool back, string title, string subtitle, int variant, int variants, string variantTip)
	{
		bool pager = variants > 1;
		if (back)
		{
			CardButton(sb, BookGeometry.AcquisitionBack, "<");
			if (BookGeometry.AcquisitionBack.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Назад · Esc", "Back · Esc"));
			}
		}
		BookRect name = BookGeometry.AcquisitionTitle(back, pager);
		BookRect sub = BookGeometry.AcquisitionSubtitle(back, pager);
		Text(sb, title, name.X, name.Y, TextColor, 1.08f, name.Width, name.Height);
		Text(sb, subtitle, sub.X, sub.Y, Muted, 0.85f, sub.Width, sub.Height);
		if (pager)
		{
			CardButton(sb, BookGeometry.AcquisitionVariantPrevious, "<", variant > 0);
			Label(sb, BookGeometry.AcquisitionVariantLabel, variant + 1 + "/" + variants, Muted, 0.85f);
			CardButton(sb, BookGeometry.AcquisitionVariantNext, ">", variant < variants - 1);
			if (BookGeometry.AcquisitionVariantLabel.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(variantTip);
			}
			if (BookGeometry.AcquisitionVariantPrevious.Contains(Mouse.X, Mouse.Y) && variant > 0)
			{
				MouseTip(T("Предыдущий вариант", "Previous variant"));
			}
			if (BookGeometry.AcquisitionVariantNext.Contains(Mouse.X, Mouse.Y) && variant < variants - 1)
			{
				MouseTip(T("Следующий вариант", "Next variant"));
			}
		}
		CardButton(sb, BookGeometry.AcquisitionClose, "X");
		if (BookGeometry.AcquisitionClose.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(back ? T("Закрыть", "Close") : T("Закрыть · Esc", "Close · Esc"));
		}
		Box(sb, BookGeometry.AcquisitionDivider, Border);
	}

	private static void DrawCardFooter(SpriteBatch sb, int page, int pages, string status)
	{
		BookRect note = BookGeometry.AcquisitionStatus;
		if (status.Length > 0)
		{
			Text(sb, status, note.X, note.Y, Muted, 0.85f, note.Width, note.Height);
		}
		if (pages > 1)
		{
			CardButton(sb, BookGeometry.AcquisitionPrevious, "<", page > 0);
			Label(sb, BookGeometry.AcquisitionPager, page + 1 + " / " + pages, Muted, 0.85f);
			CardButton(sb, BookGeometry.AcquisitionNext, ">", page < pages - 1);
			if (BookGeometry.AcquisitionPrevious.Contains(Mouse.X, Mouse.Y) && page > 0)
			{
				MouseTip(T("Назад · PgUp", "Previous · PgUp"));
			}
			if (BookGeometry.AcquisitionNext.Contains(Mouse.X, Mouse.Y) && page < pages - 1)
			{
				MouseTip(T("Дальше · PgDn", "Next · PgDn"));
			}
		}
	}

	private static void DrawCardEmpty(SpriteBatch sb, BookRect area, string title, string hint)
	{
		float y = area.Y + area.Height / 2f - 28f;
		CenterLabel(sb, title, area.X, y, area.Width, 28f, TextColor);
		CenterLabel(sb, hint, area.X, y + 32f, area.Width, 24f, Muted, 0.85f);
	}

	// "21 источник", "3 источника", "5 источников" / "1 source", "2 sources".
	private static string CountLabel(int count, string ru1, string ru2, string ru5, string en1, string en2)
	{
		if (!Ru)
		{
			return count + " " + ((count == 1) ? en1 : en2);
		}
		int ones = count % 10;
		int tens = count % 100;
		return count + " " + ((ones == 1 && tens != 11) ? ru1 : ((ones >= 2 && ones <= 4 && (tens < 12 || tens > 14)) ? ru2 : ru5));
	}

	private static bool HasArmorDefense(Item item)
	{
		if (item != null && !item.IsAir && !item.vanity)
		{
			return BookArmorSets.Slot(item) >= 0;
		}
		return false;
	}

	private static bool NativeArmorParts(Item[] parts)
	{
		if (parts.Length == 3)
		{
			return parts.All((Item p) => p.ModItem == null);
		}
		return false;
	}

	private static long ArmorPartsDefense(Item[] parts)
	{
		return BookArmorDefenseRules.SumParts(parts.Select((Item p) => p.defense));
	}

	private static int ArmorNativeSetBonus(Item[] parts)
	{
		if (!NativeArmorParts(parts))
		{
			return 0;
		}
		return BookArmorDefenseRules.VanillaSetBonus(parts[0].headSlot, parts[1].bodySlot, parts[2].legSlot);
	}

	private static string ArmorSetDefenseText(Item[] parts)
	{
		if (parts.Length != 3)
		{
			return "";
		}
		long sum = ArmorPartsDefense(parts);
		if (!NativeArmorParts(parts))
		{
			return T("Защита сета: ", "Set defense: ") + sum + "+";
		}
		return T("Защита сета: ", "Set defense: ") + (sum + ArmorNativeSetBonus(parts));
	}

	private static string ArmorDefenseText(Item item)
	{
		if (!HasArmorDefense(item))
		{
			return "";
		}
		string text = T("Защита: ", "Defense: ") + item.defense;
		BookArmorSets.Set set = BookArmorSets.Get(item.type);
		if (set == null)
		{
			return text;
		}
		Item[] parts = (from type in ArmorChoices(set, item)
			select ContentSamples.ItemsByType[type]).ToArray();
		return text + " · " + T("Сет: ", "Set: ") + (ArmorPartsDefense(parts) + ArmorNativeSetBonus(parts)) + (NativeArmorParts(parts) ? "" : "+");
	}

	// Two short lines: how the set number adds up, and what it leaves out.
	private static string ArmorDefenseTooltip(Item item, Item[] displayedParts = null)
	{
		if (!HasArmorDefense(item))
		{
			return "";
		}
		BookArmorSets.Set set = BookArmorSets.Get(item.type);
		if (set == null)
		{
			return item.Name + ": " + item.defense + "\n" + T("Без аксессуаров и баффов", "Without accessories and buffs");
		}
		Item[] parts = displayedParts ?? (from type in ArmorChoices(set, item)
			select ContentSamples.ItemsByType[type]).ToArray();
		string sum = T("Части: ", "Pieces: ") + string.Join(" + ", parts.Select((Item p) => p.defense));
		if (!NativeArmorParts(parts))
		{
			return sum + "\n" + T("Бонус модового сета не учтён", "Modded set bonus not counted");
		}
		return sum + T(", бонус сета +", ", set bonus +") + ArmorNativeSetBonus(parts) + "\n" + T("Без аксессуаров и баффов", "Without accessories and buffs");
	}

	private static void DrawArmorDefense(SpriteBatch sb, Item item)
	{
		DrawArmorDefenseAt(sb, item, BookGeometry.ArmorStats);
	}

	private static void DrawArmorDefenseAt(SpriteBatch sb, Item item, BookRect rect)
	{
		FixedLabel(sb, ArmorDefenseText(item), rect.X, rect.Y, Accent, 0.8f, rect.Width, rect.Height);
		if (!MenuOpen && rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(ArmorDefenseTooltip(item));
		}
	}

	// The set bonus of the shown combination in the game's own words ("Set bonus: 16% reduced mana costs"); long
	// or multi-line bonuses read in full in the tooltip.
	private static void DrawArmorBonus(SpriteBatch sb, Item chosen)
	{
		BookArmorSets.Set set = BookArmorSets.Get(chosen.type);
		string bonus = (set == null) ? "" : BookItemClasses.SetBonus(ArmorChoices(set, chosen));
		if (bonus.Length == 0)
		{
			return;
		}
		// The bonus takes the whole line in Accent (the line above already says "Set: 17"); the tooltip names it.
		BookRect rect = BookGeometry.ArmorBonus;
		FixedLabel(sb, bonus, rect.X, rect.Y, Accent, 0.8f, rect.Width, rect.Height);
		if (!MenuOpen && rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Бонус сета", "Set bonus") + "\n" + bonus);
		}
	}

	private static int[] ArmorChoices(BookArmorSets.Set set, Item chosen)
	{
		return BookArmorSetRules.Choices(set.Parts, set.Combinations, chosen.type);
	}

	private static bool DrawArmorSet(SpriteBatch sb, Item chosen)
	{
		BookArmorSets.Set set = BookArmorSets.Get(chosen.type);
		if (set == null)
		{
			return false;
		}
		int[] choices = ArmorChoices(set, chosen);
		for (int slot = 0; slot < 3; slot++)
		{
			BookRect rect = BookGeometry.ArmorTile(slot);
			bool active = choices[slot] == chosen.type;
			bool hover = !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
			Box(sb, rect, active ? SelectedFill : (hover ? HoverFill : Raised), border: true, 8f, active ? new Color?(Accent) : null, (!active) ? 1 : 2);
			Item part = ContentSamples.ItemsByType[choices[slot]];
			Icon(sb, part, rect.X + (rect.Width - 28f) / 2f, rect.Y + 4f, 28f, tooltip: false);
			DefenseBadge(sb, rect.X + rect.Width / 2f, rect.Y + 32f, part.defense, active ? Accent : Muted);
			if (hover)
			{
				string action = active ? ((set.Parts[slot].Length > 1) ? T("Клик: следующий вариант", "Click: next variant") : "") : T("Клик: показать эту часть", "Click: show this piece");
				MouseTip(part.Name + " · " + T("защита ", "defense ") + part.defense + ((action.Length > 0) ? ("\n" + action) : ""));
			}
		}
		return true;
	}

	private static bool HandleArmorSetClick()
	{
		Item chosen = SelectedItem;
		BookArmorSets.Set set = ((chosen == null) ? null : BookArmorSets.Get(chosen.type));
		if (set == null)
		{
			return false;
		}
		for (int slot = 0; slot < 3; slot++)
		{
			if (BookGeometry.ArmorTile(slot).Contains(Mouse.X, Mouse.Y))
			{
				ArmorFocus = ((BookArmorSets.Slot(chosen) == slot) ? BookArmorSetRules.Next(set.Parts[slot], chosen.type, alreadyActive: true) : ArmorChoices(set, chosen)[slot]);
				DetailsPage = (Variant = 0);
				BatchChoice = 0;
				BookCrafting.LastMessage = "";
				InvalidateBatchEstimate();
				return true;
			}
		}
		return false;
	}

	internal static int PreviewBatches(Recipe recipe)
	{
		if (recipe == null)
		{
			return 1;
		}
		return BatchChoice switch
		{
			0 => 1, 
			1 => 5, 
			2 => 10, 
			_ => Math.Max(1, CachedAvailableBatches(recipe)), 
		};
	}

	internal static void InvalidateBatchEstimate()
	{
		BatchEstimates.Clear();
	}

	private static int CachedAvailableBatches(Recipe recipe)
	{
		ulong tick = Main.GameUpdateCount;
		int availabilityVersion = BookCrafting.AvailabilityVersion;
		int sourcesVersion = BookSources.Version;
		if (BatchEstimates.TryGetValue(recipe, out var cached) && cached.AvailabilityVersion == availabilityVersion && cached.SourcesVersion == sourcesVersion && tick >= cached.Tick && tick - cached.Tick < 10)
		{
			return cached.Count;
		}
		int estimate = EstimateAvailableBatches(recipe);
		if (BatchEstimates.Count >= 128)
		{
			BatchEstimates.Clear();
		}
		BatchEstimates[recipe] = new BatchEstimate(estimate, tick, availabilityVersion, sourcesVersion);
		return estimate;
	}

	private static int EstimateAvailableBatches(Recipe recipe)
	{
		if (recipe == null || recipe.Disabled)
		{
			return 1;
		}
		List<BookBatchNeed> needs = new List<BookBatchNeed>();
		foreach (Item ingredient in recipe.requiredItem)
		{
			if (ingredient == null || ingredient.IsAir || ingredient.stack <= 0)
			{
				continue;
			}
			HashSet<int> accepted = new HashSet<int> { ingredient.type };
			foreach (int groupId in recipe.acceptedGroups)
			{
				if (RecipeGroup.recipeGroups.TryGetValue(groupId, out var group) && group.ValidItems.Contains(ingredient.type))
				{
					accepted.UnionWith(group.ValidItems);
				}
			}
			needs.Add(new BookBatchNeed(ingredient.stack, accepted.ToArray()));
		}
		int[] relevantTypes = (from type in needs.SelectMany((BookBatchNeed need) => need.AcceptedTypes)
			where type > 0
			select type).Distinct().ToArray();
		Dictionary<int, int> counts = BookSources.CountsFor(relevantTypes);
		return BookBatchPlanner.MaxBatches(needs, counts, 100);
	}

	private static bool HandleBatchChoiceClick(Recipe recipe)
	{
		int index = SegmentAt(BatchRects());
		if (index < 0)
		{
			return false;
		}
		BatchChoice = index;
		BookCrafting.LastMessage = "";
		return true;
	}

	private static string[] BatchLabels()
	{
		return new string[4]
		{
			"×1",
			"×5",
			"×10",
			T("Макс", "Max")
		};
	}

	private static BookRect[] BatchRects()
	{
		return SegmentRects(BookGeometry.BatchBar(RecipeBlockTop(SelectedItem)), BatchLabels(), 0.85f, natural: false);
	}

	private static void DrawBatchChoices(SpriteBatch sb, Recipe recipe)
	{
		BookRect[] rects = BatchRects();
		Segmented(sb, rects, BatchLabels(), BatchChoice, 0.85f);
		int hovered = MenuOpen ? (-1) : SegmentAt(rects);
		if (hovered >= 0)
		{
			MouseTip((hovered == 3) ? T("Сколько хватит материалов, до 100 · Ctrl+Shift+Enter", "As many as materials allow, up to 100 · Ctrl+Shift+Enter") : T("Создать столько раз за нажатие", "Craft this many times per click"));
		}
	}

	internal static void QueueCardNavigation(bool back, bool forward)
	{
		if (Open && !Main.gameMenu)
		{
			PendingBack |= back;
			PendingForward |= forward;
		}
	}

	private static void ProcessHistoryNavigation()
	{
		bool back = PendingBack;
		bool forward = PendingForward;
		PendingBack = (PendingForward = false);
		if (back == forward || !Open || Typing || HelpOpen || MenuOpen || DraggingWindow || Main.blockInput || Main.drawingPlayerChat || Main.editSign || Main.editChest || PlayerInput.UsingGamepad)
		{
			return;
		}
		if (back)
		{
			HistoryBack();
		}
		else
		{
			HistoryForward();
		}
	}

	private static void ResetCard()
	{
		FocusType = 0;
		MobFocus = -1;
		LiveMob = 0;
	}

	// Shows another item in the card without touching the grid; History records the step.
	private static void StepCard(int type, bool usedIn)
	{
		if (type <= 0 || !ContentSamples.ItemsByType.ContainsKey(type))
		{
			return;
		}
		FocusType = type;
		ArmorFocus = 0;
		Variant = 0;
		DetailsPage = 0;
		UsedInMode = usedIn;
	}

	// Everything Back/Forward restores: the tab and its filters, the stage and class, the selection and the card, and the
	// How to Get / enemy cards. Pages are kept too, but paging alone never adds an entry.
	private sealed record NavState(int Tab, string Query, string ModFilter, string CategoryKey, bool FavoritesOnly, bool MetOnly, BookSortMode SortMode, bool SortDescending, string Stations, BookStageFilter Stage, BookClass Class, int Selected, int Variant, int ArmorFocus, int FocusType, int MobFocus, int LiveMob, bool UsedInMode, bool UsedInCraftableOnly, string AcquisitionVariants, int AcquisitionVariant, int AcquisitionGroup, int Page, int DetailsPage, int AcquisitionPage, int AcquisitionRulePage)
	{
		internal NavState WithoutPages => this with
		{
			Page = 0,
			DetailsPage = 0,
			AcquisitionPage = 0,
			AcquisitionRulePage = 0
		};
	}

	private static NavState CaptureNav()
	{
		BookUiState.TabFilters filters = LiveTabFilters();
		return new NavState(CurrentTab, filters.Query, filters.ModFilter, filters.CategoryKey, filters.FavoritesOnly, filters.MetOnly, filters.SortMode, filters.SortDescending, string.Join("|", filters.ExcludedStations), StageFilter, ClassFilter, Selected, Variant, ArmorFocus, FocusType, MobFocus, LiveMob, UsedInMode, UsedInCraftableOnly, AcquisitionOpen ? string.Join(",", AcquisitionVariants) : "", AcquisitionVariant, AcquisitionGroup, Page, DetailsPage, AcquisitionPage, AcquisitionRulePage);
	}

	private static void RestoreNav(NavState state)
	{
		int from = CurrentTab;
		int tab = state.Tab;
		// The leaving tab's filters are read before the switch: the Bestiary has its own categories.
		BookUiState.TabFilters leaving = LiveTabFilters();
		SetTabFlags(tab);
		if (from != tab && from < BookUiState.FilterTabs.Length)
		{
			TabFilterSlots[from] = leaving;
		}
		LoadTabFilters(new BookUiState.TabFilters
		{
			Query = state.Query,
			ModFilter = state.ModFilter,
			CategoryKey = state.CategoryKey,
			FavoritesOnly = state.FavoritesOnly,
			MetOnly = state.MetOnly,
			SortMode = state.SortMode,
			SortDescending = state.SortDescending,
			ExcludedStations = ((state.Stations.Length == 0) ? Array.Empty<string>() : state.Stations.Split('|'))
		});
		StageFilter = state.Stage;
		ClassFilter = state.Class;
		UsedInCraftableOnly = state.UsedInCraftableOnly;
		SetTyping(value: false);
		ModMenu = (SortMenu = (StageMenu = (StationMenu = (HelpOpen = false))));
		ApplyFilters(clearMessage: false);
		Selected = state.Selected;
		Variant = state.Variant;
		ArmorFocus = state.ArmorFocus;
		FocusType = state.FocusType;
		MobFocus = state.MobFocus;
		LiveMob = state.LiveMob;
		UsedInMode = state.UsedInMode;
		Page = Math.Clamp(state.Page, 0, Pages - 1);
		DetailsPage = state.DetailsPage;
		RestoreCards(state);
	}

	private static void RestoreCards(NavState state)
	{
		if (state.AcquisitionVariants.Length == 0)
		{
			CloseAcquisition();
			return;
		}
		if (!AcquisitionOpen || string.Join(",", AcquisitionVariants) != state.AcquisitionVariants || AcquisitionVariant != state.AcquisitionVariant)
		{
			AcquisitionVariants = state.AcquisitionVariants.Split(',').Select(int.Parse).ToArray();
			AcquisitionVariant = Math.Clamp(state.AcquisitionVariant, 0, AcquisitionVariants.Length - 1);
			AcquisitionOpen = true;
			ReadAcquisitionSources();
		}
		AcquisitionGroup = (state.AcquisitionGroup < AcquisitionSources.Count) ? state.AcquisitionGroup : (-1);
		AcquisitionPage = state.AcquisitionPage;
		AcquisitionRulePage = state.AcquisitionRulePage;
	}

	// Runs after every UI update: a change the player made becomes a new entry. Typing into the search and
	// moving the selection with the arrows keep updating one entry; paging and changes the game makes on its
	// own (a refreshed list, a storage update) only update the current entry.
	private static void RecordHistory(bool acted)
	{
		if (!Open)
		{
			return;
		}
		NavState now = CaptureNav();
		if (History.IsEmpty)
		{
			History.Push(now);
			return;
		}
		NavState current = History.Current;
		if (now != current)
		{
			if (!acted || now.WithoutPages == current.WithoutPages || (Typing && HistoryTypingEntry) || (ArrowMoved && HistoryArrowEntry))
			{
				History.ReplaceCurrent(now);
			}
			else
			{
				History.Push(now);
				HistoryTypingEntry = Typing;
				HistoryArrowEntry = ArrowMoved;
			}
		}
		if (!Typing)
		{
			HistoryTypingEntry = false;
		}
	}

	private static void HistoryBack()
	{
		if (History.CanBack)
		{
			RestoreNav(History.Back());
		}
	}

	private static void HistoryForward()
	{
		if (History.CanForward)
		{
			RestoreNav(History.Forward());
		}
	}

	// What Back returns to, for the back button's tooltip.
	private static string HistoryLabel(NavState state)
	{
		if (state.Tab == 2 && state.AcquisitionVariants.Length == 0)
		{
			int mob = (state.MobFocus >= 0) ? state.MobFocus : state.Selected;
			return ((mob >= 0 && mob < BookBestiary.Mobs.Count) ? (BookBestiary.Mobs[mob].Sample.FullName + " · ") : "") + TabLabel(2);
		}
		int type = (state.AcquisitionVariants.Length > 0) ? int.Parse(state.AcquisitionVariants.Split(',')[Math.Clamp(state.AcquisitionVariant, 0, state.AcquisitionVariants.Split(',').Length - 1)]) : ((state.ArmorFocus > 0) ? state.ArmorFocus : ((state.FocusType > 0) ? state.FocusType : 0));
		if (type <= 0 && state.Selected >= 0 && (state.Tab == 0 || state.Tab == 1 || state.Tab == 4))
		{
			type = (state.Tab == 0) ? state.Selected : ((state.Selected < Recipe.numRecipes) ? Main.recipe[state.Selected].createItem.type : 0);
		}
		string tab = TabLabel(state.Tab);
		return (type > 0 && ContentSamples.ItemsByType.TryGetValue(type, out var item)) ? (item.Name + " · " + tab) : tab;
	}

	private static void StopTracker()
	{
		TrackedGoals.Clear();
		ActiveGoal = (TrackingSelection = null);
		TrackerTicks = 0;
		TrackerDragging = false;
		ResetResourceTracker();
	}

	private static void RemoveGoal(TrackedGoal goal)
	{
		TrackedGoals.Remove(goal);
		ResetResourceTracker();
		TrackerTicks = 0;
		// Collapse rather than expand another card: the list must not rearrange itself under the cursor.
		if (goal == ActiveGoal)
		{
			ActiveGoal = null;
		}
		if (TrackedGoals.Count == 0)
		{
			TrackerDragging = false;
		}
	}

	private static bool StartTracker(Recipe recipe)
	{
		if (recipe == null)
		{
			return false;
		}
		return AddTrackerGoal(new Recipe[1] { recipe }, new Item[1] { recipe.createItem }, TrackKey(recipe), TrackedMaterials(recipe), PreviewBatchCount(recipe));
	}

	private static bool AddTrackerGoal(Recipe[] recipes, Item[] parts, string key, List<MaterialRequirement> materials, int batches)
	{
		if (recipes.Any((Recipe r) => r?.Disabled ?? true) || !BookTrackerRules.CanAdd(TrackedGoals.Count, TrackedGoals.Any((TrackedGoal g) => g.Key == key), ResourceTrackerStyle))
		{
			return false;
		}
		SyncTrackerStyle();
		TrackedGoal goal = new TrackedGoal
		{
			Recipe = recipes[0],
			Recipes = recipes,
			Parts = parts,
			Key = key,
			Batches = batches,
			Materials = materials
		};
		TrackedGoals.Add(goal);
		ActiveGoal = goal;
		TrackerLastLeft = Main.mouseLeft;
		if (ResourceTrackerStyle)
		{
			ResetResourceTracker();
			TrackedGoal[] array = TrackedGoals.ToArray();
			for (int num = 0; num < array.Length; num++)
			{
				UpdateTrackerMaterials(array[num]);
			}
			RefreshResourceTracker(notify: true);
		}
		else if (UpdateTrackerMaterials(goal))
		{
			SoundEngine.PlaySound(in SoundID.ResearchComplete);
		}
		return true;
	}

	private static Item[] SelectedSetParts()
	{
		Item chosen = SelectedItem;
		BookArmorSets.Set set = ((chosen == null) ? null : BookArmorSets.Get(chosen.type));
		if (!(set == null))
		{
			return (from type in ArmorChoices(set, chosen)
				select ContentSamples.ItemsByType[type]).ToArray();
		}
		return null;
	}

	private static string SetGoalKey(Item[] parts)
	{
		return "set:" + string.Join(",", parts.Select((Item p) => p.type));
	}

	private static bool StartSetTracker(Item[] parts)
	{
		if (parts == null || parts.Length != 3)
		{
			return false;
		}
		Recipe current = SelectedRecipe;
		List<Recipe> recipes = new List<Recipe>();
		foreach (Item part in parts)
		{
			Recipe recipe = ((current?.createItem.type == part.type) ? current : null);
			if (recipe == null && RecipesByItem.TryGetValue(part.type, out var variants))
			{
				recipe = (from num in variants
					select Main.recipe[num] into r
					where !r.Disabled
					orderby (current == null || !(Owner(r) == Owner(current))) ? 1 : 0
					select r).FirstOrDefault();
			}
			if (recipe == null)
			{
				BookCrafting.Say((bool r) => (r ? "Нет рецепта: " : "No recipe: ") + part.Name);
				return false;
			}
			recipes.Add(recipe);
		}
		List<MaterialRequirement> raw = recipes.SelectMany(TrackedMaterials).ToList();
		BookBatchNeed[] merged;
		try
		{
			merged = BookTrackerRules.MergeNeeds(raw.Select((MaterialRequirement m) => new BookBatchNeed(m.Ingredient.stack, m.ItemTypes)));
		}
		catch (ArgumentOutOfRangeException)
		{
			BookCrafting.Say("Слишком много материалов для одной цели", "Too many materials for one goal");
			return false;
		}
		List<MaterialRequirement> materials = new List<MaterialRequirement>();
		BookBatchNeed[] array = merged;
		for (int i = 0; i < array.Length; i++)
		{
			BookBatchNeed need = array[i];
			MaterialRequirement first = raw.First((MaterialRequirement m) => BookTrackerRules.MaterialKey(m.ItemTypes) == BookTrackerRules.MaterialKey(need.AcceptedTypes));
			Item item = first.Ingredient.Clone();
			item.stack = need.Quantity;
			materials.Add(new MaterialRequirement(item, first.Group, first.Name, need.AcceptedTypes.ToHashSet(), Overlapping: false, first.Alternatives));
		}
		bool[] overlap = BookMaterialRules.FindOverlappingRequirements(materials.Select((MaterialRequirement m) => m.ItemTypes).ToList());
		for (int i2 = 0; i2 < materials.Count; i2++)
		{
			materials[i2] = materials[i2]with
			{
				Overlapping = overlap[i2]
			};
		}
		int batches = BatchChoice switch
		{
			1 => 5, 
			2 => 10, 
			_ => 1, 
		};
		if (BatchChoice == 3)
		{
			int[] types = merged.SelectMany((BookBatchNeed n) => n.AcceptedTypes).Distinct().ToArray();
			Dictionary<int, int> counts = BookSources.CountsFor(types);
			batches = Math.Max(1, BookBatchPlanner.MaxBatches(merged, counts, 100));
		}
		return AddTrackerGoal(recipes.ToArray(), parts, SetGoalKey(parts), materials, batches);
	}

	public override void PostUpdatePlayers()
	{
		if (Main.gameMenu || TrackedGoals.Count == 0 || Main.LocalPlayer == null || !Main.LocalPlayer.active || Main.LocalPlayer.dead || Main.LocalPlayer.ghost)
		{
			return;
		}
		SyncTrackerStyle();
		bool refresh = TrackerTicks++ % 15 == 0;
		bool notify = false;
		if (ResourceTrackerStyle)
		{
			UpdateResourceTracker(refresh);
			return;
		}
		TrackedGoal[] array = TrackedGoals.ToArray();
		foreach (TrackedGoal goal in array)
		{
			if (!Open && !TrackerDragging && goal.Dismiss.Tick())
			{
				RemoveGoal(goal);
			}
			else if (refresh && !goal.Dismiss.Started)
			{
				notify |= UpdateTrackerMaterials(goal);
			}
		}
		if (notify)
		{
			SoundEngine.PlaySound(in SoundID.ResearchComplete);
		}
	}

	private static bool UpdateTrackerMaterials(TrackedGoal goal)
	{
		if (goal.Recipes.Any((Recipe recipe) => recipe.Disabled || recipe.RecipeIndex < 0 || recipe.RecipeIndex >= Recipe.numRecipes || Main.recipe[recipe.RecipeIndex] != recipe))
		{
			RemoveGoal(goal);
			return false;
		}
		int[] types = goal.Materials.SelectMany((MaterialRequirement m) => m.Alternatives ?? (IEnumerable<int>)m.ItemTypes).Distinct().ToArray();
		// Until the first chest snapshot arrives in multiplayer the counts are only local: not ready yet.
		goal.Unknown = BookSources.Syncing;
		goal.Counts = BookSources.CountsFor(types);
		goal.Materials = NarrowAlternatives(goal.Materials, goal.Counts);
		BookBatchNeed[] needs = goal.Materials.Select((MaterialRequirement m) => new BookBatchNeed(m.Ingredient.stack, m.ItemTypes)).ToArray();
		goal.Unknown |= goal.Counts.Values.Any((int n) => n < 0);
		goal.Ready = !goal.Unknown && BookTrackerRules.Ready(needs, goal.Counts, goal.Batches);
		goal.Progress = AssessTrackedGoal(goal);
		goal.Possible = (goal.Unknown ? 0 : BookBatchPlanner.MaxBatches(needs, goal.Counts, 9999));
		if (ResourceTrackerStyle)
		{
			return false;
		}
		if (!goal.Signal.Update(goal.Ready))
		{
			return false;
		}
		goal.Dismiss.Begin();
		return true;
	}

	private static void DrawTrackButton(SpriteBatch sb, Recipe recipe)
	{
		Item[] parts = SelectedSetParts();
		bool hasSet = parts != null;
		bool tracked = recipe != null && TrackedGoals.Any((TrackedGoal g) => g.Key == TrackKey(recipe));
		bool full = !ResourceTrackerStyle && TrackedGoals.Count >= BookTrackerRules.MaxGoals;
		BookRect single = (hasSet ? BookGeometry.TrackSingleAction : BookGeometry.TrackAction);
		string fullTip = T("В карточках до 5 целей · Ctrl+" + TrackingTabNumber + " — список", "Up to 5 goals in cards style · Ctrl+" + TrackingTabNumber + " opens the list");
		if (tracked)
		{
			Button(sb, single, T("Не отслеживать", "Untrack"), active: true);
		}
		else if (recipe == null || full)
		{
			DisabledButton(sb, single, T("Отслеживать", "Track"));
		}
		else
		{
			Button(sb, single, T("Отслеживать", "Track"));
		}
		if (single.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip((recipe == null) ? T("У предмета нет рецепта", "This item has no recipe") : (tracked ? T("Больше не показывать на экране · Alt+T", "Stop showing it on screen · Alt+T") : (full ? fullTip : T("Показывать материалы на экране · Alt+T", "Show its materials on screen · Alt+T"))));
		}
		if (!hasSet)
		{
			return;
		}
		bool setTracked = TrackedGoals.Any((TrackedGoal g) => g.Key == SetGoalKey(parts));
		if (setTracked)
		{
			Button(sb, BookGeometry.TrackSetAction, T("Снять сет", "Untrack Set"), active: true);
		}
		else if (full)
		{
			DisabledButton(sb, BookGeometry.TrackSetAction, T("Весь сет", "Track Set"));
		}
		else
		{
			Button(sb, BookGeometry.TrackSetAction, T("Весь сет", "Track Set"));
		}
		if (BookGeometry.TrackSetAction.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(setTracked ? T("Не отслеживать весь сет", "Untrack all three pieces") : (full ? fullTip : T("Отслеживать три части одной целью", "Track all three pieces as one goal")));
		}
	}

	// Disabled state of the shared button: muted label, no hover.
	private static void DisabledButton(SpriteBatch sb, BookRect rect, string label)
	{
		Box(sb, rect, Panel, border: true, 8f, Border);
		CenterLabel(sb, label, rect.X + 8f, rect.Y + 4f, rect.Width - 16f, rect.Height - 8f, Muted);
	}

	// Destructive action: outlined, muted red label.
	private static void DestructiveButton(SpriteBatch sb, BookRect rect, string label)
	{
		Box(sb, rect, rect.Contains(Mouse.X, Mouse.Y) ? new Color(58, 44, 40) : Panel, border: true, 8f, new Color(112, 78, 70));
		CenterLabel(sb, label, rect.X + 8f, rect.Y + 4f, rect.Width - 16f, rect.Height - 8f, MissingColor);
	}

	private static bool HandleTrackButton()
	{
		if (!BookGeometry.TrackAction.Contains(Mouse.X, Mouse.Y))
		{
			return false;
		}
		ToggleTrack(BookGeometry.TrackSetAction.Contains(Mouse.X, Mouse.Y));
		return true;
	}

	// Track / Untrack the card's recipe, or its whole armor set (button or Alt+T).
	private static void ToggleTrack(bool wholeSet)
	{
		Recipe recipe = SelectedRecipe;
		Item[] parts = SelectedSetParts();
		wholeSet &= parts != null;
		if (!wholeSet && recipe == null)
		{
			BookCrafting.Say("У предмета нет рецепта", "This item has no recipe");
			return;
		}
		string key;
		if (wholeSet)
		{
			key = SetGoalKey(parts);
		}
		else
		{
			key = ((recipe == null) ? "" : TrackKey(recipe));
		}
		TrackedGoal existing = TrackedGoals.FirstOrDefault((TrackedGoal g) => g.Key == key);
		if (existing != null)
		{
			RemoveGoal(existing);
			BookCrafting.Say("Больше не отслеживается", "No longer tracked");
			return;
		}
		if (!ResourceTrackerStyle && TrackedGoals.Count >= 5)
		{
			BookCrafting.Say("В карточках до 5 целей. Уберите одну или включите компактный вид.", "Cards hold 5 goals. Untrack one or switch to compact.");
			return;
		}
		// Track is a toggle like Untrack: the book stays open so several goals can be added in a row.
		if (wholeSet ? StartSetTracker(parts) : StartTracker(recipe))
		{
			BookCrafting.Say((bool r) => wholeSet ? (r ? "Сет отслеживается" : "Tracking the set") : ((r ? "Отслеживается: " : "Tracking: ") + recipe.createItem.Name));
		}
	}

	private static void InsertTrackerLayer(List<GameInterfaceLayer> layers)
	{
		if (TrackedGoals.Count == 0 || Main.gameMenu)
		{
			return;
		}
		// The compact style continues the info accessory lines and stays visible with the book open, like they do.
		// Cards are interactive and hide while the book is open.
		bool compact = ResourceTrackerStyle;
		if (!compact && Open)
		{
			return;
		}
		int index = compact ? (layers.FindIndex((GameInterfaceLayer l) => l.Name == "Vanilla: Info Accessories Bar") + 1) : layers.FindIndex((GameInterfaceLayer l) => l.Name == "Vanilla: Mouse Text");
		if (index <= 0)
		{
			index = layers.Count;
		}
		layers.Insert(index, new LegacyGameInterfaceLayer("ModernRecipeBook: Craft tracker", () =>
		{
			SafeDrawTracker(Main.spriteBatch);
			return true;
		}, InterfaceScaleType.UI));
	}

	private static BookGeometry TrackerLayout()
	{
		return BookTrackerRules.PanelLayoutHeight(TrackerPanelHeight, Main.screenWidth, Main.screenHeight, TrackerCustomPosition, TrackerPositionX, TrackerPositionY, ModContent.GetInstance<BookConfig>().TrackerScalePercent, Main.UIScale);
	}

	private static void HandleTrackerInput()
	{
		if (!Main.mouseLeft && !Main.mouseRight)
		{
			BlockTrackerPress = false;
		}
		bool click = Main.mouseLeft && !TrackerLastLeft;
		bool rightClick = Main.mouseRight && !TrackerLastRight;
		TrackerLastLeft = Main.mouseLeft;
		TrackerLastRight = Main.mouseRight;
		// The compact style is not interactive, like the info accessory lines it sits under.
		if (Open || TrackedGoals.Count == 0 || ResourceTrackerStyle || Main.gameMenu || Main.LocalPlayer.dead || Main.LocalPlayer.ghost)
		{
			TrackerDragging = false;
			return;
		}
		BookGeometry layout = TrackerLayout();
		int height = TrackerPanelHeight;
		if (TrackerDragging)
		{
			Main.LocalPlayer.mouseInterface = true;
			PlayerInputLockTracker();
			if (!Main.mouseLeft)
			{
				TrackerDragging = false;
				return;
			}
			BlockTrackerPress = true;
			TrackerCustomPosition = true;
			(TrackerPositionX, TrackerPositionY) = BookTrackerRules.Position(Main.MouseScreen.X - TrackerDragX, Main.MouseScreen.Y - TrackerDragY, height, Main.screenWidth, Main.screenHeight, layout.Scale);
			return;
		}
		(float x, float y) = layout.ToLocal(Main.MouseScreen.X, Main.MouseScreen.Y);
		if (!new BookRect(0f, 0f, BookTrackerRules.PanelWidth, height).Contains(x, y))
		{
			return;
		}
		Main.LocalPlayer.mouseInterface = true;
		PlayerInputLockTracker();
		if (Main.mouseLeft || Main.mouseRight)
		{
			BlockTrackerPress = true;
		}
		if (click && y < BookTrackerRules.HeaderHeight)
		{
			TrackerDragging = true;
			TrackerDragX = Main.MouseScreen.X - layout.Left;
			TrackerDragY = Main.MouseScreen.Y - layout.Top;
			return;
		}
		int index = BookTrackerRules.GoalAt(y, TrackedGoals.Count, TrackerActiveIndex, TrackerActiveMaterials);
		if (index < 0)
		{
			return;
		}
		TrackedGoal goal = TrackedGoals[index];
		if (rightClick)
		{
			RemoveGoal(goal);
		}
		else if (click)
		{
			ActiveGoal = ((ActiveGoal == goal) ? null : goal);
		}
	}

	private static void PlayerInputLockTracker()
	{
		PlayerInput.LockVanillaMouseScroll("ModernRecipeBookTracker");
	}

	private static void SafeDrawTracker(SpriteBatch sb)
	{
		try
		{
			DrawTracker(sb);
			FlushTip();
		}
		catch (Exception exception)
		{
			PendingTip = null;
			StopTracker();
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("MRB tracker draw failed; tracker cleared safely.", exception);
		}
	}

	// Cards style: a compact list. The header "Tracking · n" drags the panel; a row shows crafts possible/wanted and
	// a thin progress bar; click expands its materials (missing first), right-click untracks.
	private static void DrawTracker(SpriteBatch sb)
	{
		if (TrackedGoals.Count == 0 || Main.gameMenu || Main.LocalPlayer.dead || Main.LocalPlayer.ghost)
		{
			return;
		}
		if (ResourceTrackerStyle)
		{
			DrawResourceTracker(sb);
			return;
		}
		Vector2 savedOrigin = Origin;
		Vector2 savedMouse = Mouse;
		float savedScale = Scale;
		float savedOpacity = GraphicsOpacity;
		try
		{
			BookGeometry layout = TrackerLayout();
			Origin = new Vector2(layout.Left, layout.Top);
			Scale = layout.Scale;
			(float, float) mouse = layout.ToLocal(Main.MouseScreen.X, Main.MouseScreen.Y);
			Mouse = new Vector2(mouse.Item1, mouse.Item2);
			int active = TrackerActiveIndex;
			int activeMaterials = TrackerActiveMaterials;
			float width = BookTrackerRules.PanelWidth;
			float opacity = (float)Math.Clamp(ModContent.GetInstance<BookConfig>().TrackerOpacityPercent, 20, 100) / 100f;
			GraphicsOpacity = opacity * ((TrackedGoals.Count == 1) ? (1f - TrackedGoals[0].Dismiss.Progress) : 1f);
			Box(sb, 0f, 0f, width, TrackerPanelHeight, Panel, border: true, 10f, Border);
			bool header = TrackerDragging || Hit(0f, 0f, width, BookTrackerRules.HeaderHeight);
			FixedLabel(sb, T("ОТСЛЕЖИВАНИЕ · ", "TRACKING · ") + TrackedGoals.Count, 12f, 5f, header ? TextColor : Muted, 0.8f, width - 56f, 22f);
			for (int line = 0; line < 3; line++)
			{
				Box(sb, width - 26f, 11 + line * 4, 14f, 2f, header ? TextColor : Muted, border: false, 1f);
			}
			if (header && !TrackerDragging)
			{
				MouseTip(T("Перетащите, чтобы переместить", "Drag to move"));
			}
			for (int i = 0; i < TrackedGoals.Count; i++)
			{
				TrackedGoal goal = TrackedGoals[i];
				bool expanded = i == active;
				float top = BookTrackerRules.GoalTop(i, active, activeMaterials);
				float block = BookTrackerRules.GoalHeight + (expanded ? BookTrackerRules.ExpandedHeight(goal.Materials.Count) : 0);
				float fade = 1f - goal.Dismiss.Progress;
				GraphicsOpacity = opacity * fade;
				bool hover = !TrackerDragging && Hit(0f, top, width, block);
				if (expanded || hover)
				{
					Box(sb, 4f, top + 2f, width - 8f, block - 4f, (expanded && !hover) ? Surface : Raised, border: false, 8f);
				}
				Icon(sb, GoalIcon(goal), 12f, top + 8f, 28f, tooltip: false);
				string crafts = CraftsRatio(goal);
				float craftsWidth = CountWidth(crafts, 0.88f);
				FixedLabel(sb, GoalTitle(goal), 48f, top + 5f, TextColor * fade, 0.88f, width - 48f - 12f - craftsWidth - 12f, 22f);
				CenterLabel(sb, crafts, width - 12f - craftsWidth, top + 5f, craftsWidth, 22f, (goal.Ready ? EnoughColor : TextColor) * fade, 0.88f, rightAligned: true);
				DrawProgressBar(sb, 48f, top + 31f, width - 48f - 12f, 4f, goal);
				if (expanded)
				{
					DrawGoalMaterials(sb, goal, top + BookTrackerRules.GoalHeight, fade);
				}
				if (hover)
				{
					MouseTip(GoalTitle(goal) + "\n" + T("ЛКМ — материалы · ПКМ — не отслеживать", "Click: materials · Right-click: untrack"));
				}
			}
		}
		finally
		{
			Origin = savedOrigin;
			Mouse = savedMouse;
			Scale = savedScale;
			GraphicsOpacity = savedOpacity;
		}
	}

	private static void DrawGoalMaterials(SpriteBatch sb, TrackedGoal goal, float y, float fade)
	{
		if (goal.Progress.Length != goal.Materials.Count)
		{
			return;
		}
		float width = BookTrackerRules.PanelWidth;
		int[] order = MissingFirst(goal.Progress);
		bool more = order.Length > BookTrackerRules.MaxMaterialLines;
		int shown = more ? (BookTrackerRules.MaxMaterialLines - 1) : order.Length;
		for (int line = 0; line < shown; line++)
		{
			BookResourceProgress row = goal.Progress[order[line]];
			MaterialRequirement material = goal.Materials[order[line]];
			float itemY = y + (float)(line * BookTrackerRules.MaterialHeight);
			Icon(sb, material.Ingredient, 48f, itemY + 2f, 20f, tooltip: false);
			string ratio = ProgressRatio(row);
			float ratioWidth = CountWidth(ratio, 0.82f);
			FixedLabel(sb, MaterialName(material), 76f, itemY + 1f, (row.Met ? Muted : TextColor) * fade, 0.82f, width - 76f - 12f - ratioWidth - 12f, 22f);
			CenterLabel(sb, ratio, width - 12f - ratioWidth, itemY + 1f, ratioWidth, 22f, ProgressColor(row) * fade, 0.82f, rightAligned: true);
		}
		if (more)
		{
			FixedLabel(sb, "+" + (order.Length - shown) + T(" ещё · Ctrl+", " more · Ctrl+") + TrackingTabNumber, 76f, y + (float)(shown * BookTrackerRules.MaterialHeight) + 1f, Muted * fade, 0.82f, width - 88f, 22f);
		}
	}

	// Width a count needs, so the name next to it gets the rest of the row.
	private static float CountWidth(string count, float size)
	{
		return Ink(count).Width * size * Math.Clamp(TextScale, 0.9f, 1.15f) + 2f;
	}

	// Track + fill. A ready goal fills green; an unknown source stays empty.
	private static void DrawProgressBar(SpriteBatch sb, float x, float y, float width, float height, TrackedGoal goal)
	{
		Box(sb, x, y, width, height, Border, border: false, height / 2f);
		float fraction = GoalFraction(goal);
		if (fraction > 0f)
		{
			Box(sb, x, y, Math.Max(height, width * fraction), height, goal.Ready ? EnoughColor : Accent, border: false, height / 2f);
		}
	}

	private static float GoalFraction(TrackedGoal goal)
	{
		if (goal.Ready)
		{
			return 1f;
		}
		if (goal.Unknown || goal.Progress.Length == 0)
		{
			return 0f;
		}
		return goal.Progress.Average((BookResourceProgress r) => (r.Need.Quantity <= 0) ? 1f : ((float)Math.Min(1.0, (double)r.Allocated / (double)r.Need.Quantity)));
	}

	// The real amount on hand, never clamped to the need: 10 Silk for a 2 Silk need reads 10/2.
	private static string ProgressRatio(BookResourceProgress row)
	{
		return (row.Unknown ? "?" : (row.Infinite ? "∞" : Count(row.Owned))) + "/" + Count(row.Need.Quantity);
	}

	private static Color ProgressColor(BookResourceProgress row)
	{
		if (row.Unknown)
		{
			return Muted;
		}
		return row.Met ? EnoughColor : MissingColor;
	}

	private static string CraftsRatio(TrackedGoal goal)
	{
		return (goal.Unknown ? "?" : Count(goal.Possible)) + "/" + Count(goal.Batches);
	}

	// Stable: missing rows keep their recipe order, then the collected ones.
	private static int[] MissingFirst(IReadOnlyList<BookResourceProgress> rows)
	{
		return (from i in Enumerable.Range(0, rows.Count)
			orderby rows[i].Met ? 1 : 0
			select i).ToArray();
	}

	// Live name, so a language switch also renames the materials of goals tracked before it.
	private static string MaterialName(MaterialRequirement material)
	{
		if (material.Alternatives != null)
		{
			return AlternativesName(material.Alternatives, material.Ingredient.type);
		}
		return material.Group?.GetText() ?? material.Ingredient.Name;
	}

	private static Item GoalIcon(TrackedGoal goal)
	{
		if (!goal.IsSet)
		{
			return goal.Recipe.createItem;
		}
		return goal.Parts[1];
	}

	private static string GoalTitle(TrackedGoal goal)
	{
		if (!goal.IsSet)
		{
			return goal.Recipe.createItem.Name;
		}
		// "Molten Helmet / Molten Breastplate / Molten Greaves" reads "Molten Armor".
		string[][] words = goal.Parts.Select((Item p) => p.Name.Split(' ')).ToArray();
		int common = 0;
		while (words.All((string[] w) => w.Length > common + 1 && w[common] == words[0][common]))
		{
			common++;
		}
		if (common == 0)
		{
			return T("Комплект брони", "Armor set");
		}
		string prefix = string.Join(" ", words[0].Take(common));
		return Ru ? ("Комплект: " + prefix) : (prefix + " Armor");
	}

	private static BookResourceProgress[] AssessTrackedGoal(TrackedGoal goal)
	{
		BookResourceNeed[] needs = goal.Materials.Select((MaterialRequirement m) => new BookResourceNeed((long)m.Ingredient.stack * (long)goal.Batches, m.ItemTypes)).ToArray();
		HashSet<int> infinite = new HashSet<int>();
		if (!goal.Unknown)
		{
			foreach (int type in needs.SelectMany((BookResourceNeed n) => n.Types).Distinct())
			{
				if (BookSources.IsInfinite(type))
				{
					infinite.Add(type);
				}
			}
		}
		return BookResourceTrackerRules.AssessGoal(needs, goal.Counts.ToDictionary((KeyValuePair<int, int> p) => p.Key, (KeyValuePair<int, int> p) => (long)p.Value), goal.Unknown, infinite);
	}

	private static bool CanOpenEnemy(BookAcquisitionGroup group)
	{
		if (group.Source.Kind is BookAcquisitionKind.Drop or BookAcquisitionKind.Banner or BookAcquisitionKind.Catch && group.Source.NpcNetId != 0)
		{
			return ContentSamples.NpcsByNetId.ContainsKey(group.Source.NpcNetId);
		}
		return false;
	}

	// Everything the mob card shows that is heavy to read (bestiary tags, the drop rules, the stats, the
	// portrait): once per mob shown, never per frame.
	private static void ReadMobCard(int id)
	{
		EnemyReadErrors = 0;
		NPC npc = ContentSamples.NpcsByNetId[id];
		List<string> biomes = new List<string>();
		List<string> spawn = new List<string>();
		try
		{
			HashSet<object> biomeTags = (from f in typeof(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes).GetFields(BindingFlags.Static | BindingFlags.Public)
				select f.GetValue(null)).ToHashSet();
			BestiaryEntry entry = Main.BestiaryDB.FindEntryByNPCID(npc.type);
			if (entry != null)
			{
				foreach (IBestiaryInfoElement element in entry.Info)
				{
					if ((!(element is SpawnConditionBestiaryInfoElement) && !(element is SpawnConditionBestiaryOverlayInfoElement)) || !(element is IFilterInfoProvider filter))
					{
						continue;
					}
					try
					{
						string key = filter.GetDisplayNameKey();
						string label = (string.IsNullOrWhiteSpace(key) ? "" : Language.GetTextValue(key));
						if (!string.IsNullOrWhiteSpace(label) && !(label == key))
						{
							(biomeTags.Contains(element) ? biomes : spawn).Add(label);
						}
					}
					catch (Exception)
					{
						EnemyReadErrors++;
					}
				}
			}
			int[] modBiomeIds = npc.ModNPC?.SpawnModBiomes;
			if (modBiomeIds != null)
			{
				foreach (ModBiome biome in from b in ModContent.GetContent<ModBiome>()
					where Enumerable.Contains(modBiomeIds, b.Type)
					select b)
				{
					biomes.Add(biome.DisplayName.Value);
				}
			}
		}
		catch (Exception)
		{
			EnemyReadErrors++;
		}
		EnemyBiomes = string.Join(" · ", biomes.Distinct(StringComparer.Ordinal));
		EnemySpawn = string.Join(" · ", spawn.Distinct(StringComparer.Ordinal));
		ReadMobLoot(id);
		ReadEnemyStats(id);
		ReadEnemyPortrait(id);
		if (EnemyReadErrors > 0)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn($"Enemy card for NPC net ID {id}: {EnemyReadErrors} reports/tags could not be read.");
		}
	}

	private static string EnemyLootTooltip(BookEnemyLoot loot)
	{
		return string.Join("\n", loot.Rules.Select((BookEnemyDrop x) => BookAcquisitionRules.Chance(x.Chance) + " · ×" + BookAcquisitionRules.Stack(x.Min, x.Max) + " · " + ((x.Conditions.Length > 0) ? x.Conditions : T("без условий", "no conditions"))));
	}

	// An Unknown stage is either a source the current difficulty rules out or no regular source at all.
	private static string StageName(Item item, Recipe recipe)
	{
		BookStage stage = (recipe != null) ? BookProgression.RecipeStage(recipe.RecipeIndex) : BookProgression.ItemStage(item.type);
		bool modeExcluded = (recipe != null) ? BookProgression.RecipeModeExcluded(recipe.RecipeIndex) : BookProgression.ItemModeExcluded(item.type);
		return (stage == BookStage.Unknown && modeExcluded) ? T("Не в этом режиме", "Not in this mode") : StageName(stage);
	}

	private static string StageName(BookStage stage)
	{
		return stage switch
		{
			BookStage.PreHardmode => T("Прехардмод", "Pre-Hardmode"),
			BookStage.Hardmode => T("Хардмод", "Hardmode"),
			BookStage.PostHardmode => T("После Лунного лорда", "Post-Moon Lord"),
			_ => T("Нет обычного источника", "No regular source"),
		};
	}

	private static string PendingTip;

	// Hover hints are drawn when their layer ends (FlushTip), so panels drawn later cannot cover them.
	private static void MouseTip(string text)
	{
		PendingTip = text;
	}

	// Vanilla MouseText draws every line as is and without a backdrop, so long hints ran off the screen and
	// were hard to read over the book. Wrap them, and draw a panel exactly where MouseTextInner puts the text.
	private static void FlushTip()
	{
		string text = PendingTip;
		PendingTip = null;
		if (text == null)
		{
			return;
		}
		string[] lines = Utils.WordwrapString(text, FontAssets.MouseText.Value, 560, 60, out int last);
		string wrapped = string.Join("\n", lines.Take(last + 1).Select((string line) => line?.TrimEnd('\n')));
		Vector2 size = FontAssets.MouseText.Value.MeasureString(wrapped);
		int x = Main.mouseX + 14 + (Main.ThickMouse ? 6 : 0);
		int y = Main.mouseY + 14 + (Main.ThickMouse ? 6 : 0);
		x = Math.Min(x, (int)(Main.screenWidth - size.X - 4f));
		y = Math.Min(y, (int)(Main.screenHeight - size.Y - 4f));
		DrawTooltipPanel(new Rectangle(x - 8, y - 6, (int)size.X + 16, (int)size.Y + 8));
		Main.instance.MouseText(wrapped, 0, 0);
	}

	// One look for every tooltip over the book: the book's tips and the game's item tooltips.
	internal static void DrawTooltipPanel(Rectangle bounds)
	{
		BookSkin.Panel(Main.spriteBatch, bounds, Panel * 0.96f, 8f, Border);
	}

	// The stage filter is global, so it sits in the header next to the title, not in a tab's filter bar.
	// Its width fits the longest option, so picking another stage never moves the tabs.
	private static BookRect StageDropdownRect()
	{
		float width = StageLabels().Max((string label) => TextWidth(label, 0.85f)) + 52f;
		return new BookRect(TitleRight() + 16f, BookGeometry.TabTop + 2f, width, BookGeometry.TabHeight - 4f);
	}

	private static BookRect StageOption(int option)
	{
		BookRect anchor = StageDropdownRect();
		return new BookRect(anchor.X + 4f, anchor.Y + anchor.Height + 8f + option * 32, Math.Max(anchor.Width, 200f) - 8f, 32f);
	}

	private static void DrawStageMenu(SpriteBatch sb)
	{
		BookRect anchor = StageDropdownRect();
		Box(sb, anchor.X, anchor.Y + anchor.Height + 4f, Math.Max(anchor.Width, 200f), 136f, new Color(17, 25, 20), border: true, 10f, Accent);
		string[] labels = StageLabels();
		for (int option = 0; option < 4; option++)
		{
			BookRect rect = StageOption(option);
			bool active = option == (int)StageFilter;
			bool hover = rect.Contains(Mouse.X, Mouse.Y);
			if (active)
			{
				Box(sb, rect, SelectedFill, border: false, 6f);
			}
			else if (hover)
			{
				Box(sb, rect, HoverFill, border: false, 6f);
			}
			CenterLabel(sb, labels[option], rect.X + 12f, rect.Y + 4f, rect.Width - 24f, rect.Height - 8f, active ? Accent : TextColor, 0.85f, rightAligned: false, leftAligned: true);
		}
	}

	private static string[] StageLabels()
	{
		return new string[4]
		{
			T("Все этапы", "All stages"),
			StageName(BookStage.PreHardmode),
			StageName(BookStage.Hardmode),
			StageName(BookStage.PostHardmode)
		};
	}


	private static string StageExplanation(Item item, Recipe recipe)
	{
		return ((recipe != null) ? T("Этап рецепта: по материалам, станции и условиям", "Recipe stage: from its materials, station and conditions") : T("Самый ранний этап, когда предмет можно получить", "The earliest stage you can get this item")) + "\n" + T("Учитывается сложность мира", "The world's difficulty counts");
	}

	private static void ResetResourceTracker()
	{
		ResourceTrackerSignal.Reset();
		ResourceTrackerDismiss.Reset();
		ResourceTrackerRows = Array.Empty<BookResourceProgress>();
		ResourceTrackerMaterials.Clear();
	}

	private static void SyncTrackerStyle()
	{
		bool style = ResourceTrackerStyle;
		if (LastResourceTrackerStyle == style)
		{
			return;
		}
		LastResourceTrackerStyle = style;
		ResetResourceTracker();
		TrackerTicks = 0;
		foreach (TrackedGoal trackedGoal in TrackedGoals)
		{
			trackedGoal.Signal.Reset();
			trackedGoal.Dismiss.Reset();
		}
	}

	private static void RefreshResourceTracker(bool notify)
	{
		ResourceTrackerMaterials.Clear();
		List<BookResourceNeed> raw = new List<BookResourceNeed>();
		foreach (TrackedGoal goal in TrackedGoals)
		{
			foreach (MaterialRequirement material in goal.Materials)
			{
				string key = BookResourceTrackerRules.Key(material.ItemTypes);
				ResourceTrackerMaterials.TryAdd(key, material);
				raw.Add(new BookResourceNeed((long)material.Ingredient.stack * (long)goal.Batches, material.ItemTypes));
			}
		}
		BookResourceNeed[] needs = BookResourceTrackerRules.Merge(raw);
		Dictionary<int, long> counts = new Dictionary<int, long>();
		foreach (TrackedGoal goal2 in TrackedGoals)
		{
			foreach (KeyValuePair<int, int> pair in goal2.Counts)
			{
				counts[pair.Key] = Math.Max(counts.GetValueOrDefault(pair.Key), Math.Max(0, pair.Value));
			}
		}
		bool unknown = TrackedGoals.Any((TrackedGoal g) => g.Unknown);
		HashSet<int> infinite = new HashSet<int>();
		if (!unknown)
		{
			foreach (int type in needs.SelectMany((BookResourceNeed n) => n.Types).Distinct())
			{
				if (BookSources.IsInfinite(type))
				{
					infinite.Add(type);
				}
			}
		}
		long totalDemand = needs.Sum((BookResourceNeed n) => n.Quantity);
		foreach (int type2 in infinite)
		{
			counts[type2] = totalDemand;
		}
		ResourceTrackerRows = BookResourceTrackerRules.Assess(needs, counts, unknown, infinite);
		if (!BookResourceTrackerRules.Ready(ResourceTrackerRows, TrackedGoals.Count > 0, TrackedGoals.All((TrackedGoal g) => !g.Unknown)))
		{
			ResourceTrackerDismiss.Reset();
		}
		else if (notify)
		{
			ResourceTrackerDismiss.Begin();
			if (ResourceTrackerSignal.Update(ready: true))
			{
				SoundEngine.PlaySound(in SoundID.ResearchComplete);
			}
		}
	}

	private static void UpdateResourceTracker(bool refresh)
	{
		if (refresh)
		{
			TrackedGoal[] array = TrackedGoals.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				UpdateTrackerMaterials(array[i]);
			}
			RefreshResourceTracker(notify: true);
		}
		if (!Open && !TrackerDragging && ResourceTrackerDismiss.Tick())
		{
			StopTracker();
		}
	}

	// Compact style: one vanilla info accessory line per material ("Silk 40/10"), continuing below the lines the game
	// draws under the minimap. Same font, colours, shadow, line height and column as Main.DrawInfoAccs; no panel, no
	// input. Hidden whenever the game hides its own info text (inventory open, NPC chat).
	private static void DrawResourceTracker(SpriteBatch sb)
	{
		if (Main.playerInventory || !Main.CanShowInfoAccs || ResourceTrackerRows.Length == 0)
		{
			return;
		}
		bool ru = GameCulture.FromCultureName(GameCulture.CultureName.Russian).IsActive;
		int line = InfoAccessoryLines();
		// Under the minimap's edge. In Russian the game draws its own info lines 50 px further left (room for longer
		// text); when they are on screen ours continue their column, otherwise they stay under the minimap.
		int x = Main.screenWidth - 280 + ((ru && line > 0) ? (-50) : 0);
		int top = -32 + ((Main.mapStyle == 1 && Main.mapEnabled) ? 261 : 0) + 74 + 48;
		int step = (Main.screenHeight < 650) ? 20 : 22;
		float maxWidth = ru ? 265f : 215f;
		int room = Math.Max(1, (Main.screenHeight - 24 - top) / step - line);
		float fade = 1f - ResourceTrackerDismiss.Progress;
		int[] order = MissingFirst(ResourceTrackerRows);
		int shown = (order.Length > room) ? (room - 1) : order.Length;
		for (int n = 0; n < shown; n++)
		{
			BookResourceProgress row = ResourceTrackerRows[order[n]];
			MaterialRequirement material = ResourceTrackerMaterials[BookResourceTrackerRules.Key(row.Need.Types)];
			string name = MaterialName(material);
			DrawInfoLine(sb, material.Ingredient, name, " " + ProgressRatio(row), x, top + (line + n) * step, maxWidth, row.Met ? InfoDoneColor : Color.White, fade);
		}
		if (shown < order.Length)
		{
			DrawInfoLine(sb, null, "+" + (order.Length - shown) + T(" ещё", " more"), "", x, top + (line + shown) * step, maxWidth, new Color(150, 150, 150), fade);
		}
	}

	// Lines the game draws above ours. The built-in displays are not listed by GetContent, so they are named here.
	// ponytail: counts active, unhidden displays; a modded display whose value is empty draws no line but is counted.
	private static int InfoAccessoryLines()
	{
		bool[] hidden = Main.LocalPlayer.hideInfo;
		InfoDisplay[] builtIn = new InfoDisplay[12]
		{
			InfoDisplay.Watches,
			InfoDisplay.WeatherRadio,
			InfoDisplay.Sextant,
			InfoDisplay.FishFinder,
			InfoDisplay.MetalDetector,
			InfoDisplay.LifeformAnalyzer,
			InfoDisplay.Radar,
			InfoDisplay.TallyCounter,
			InfoDisplay.DPSMeter,
			InfoDisplay.Stopwatch,
			InfoDisplay.Compass,
			InfoDisplay.DepthMeter
		};
		return builtIn.Concat(ModContent.GetContent<InfoDisplay>()).Distinct().Count((InfoDisplay d) => InfoDisplayLoader.Active(d) && (d.Type >= hidden.Length || !hidden[d.Type]));
	}

	// One line exactly as Main.DrawInfoAccs draws it: the icon's box at (x, line top + 4), the text at x + 20 with a
	// 2 px black shadow on four sides, in MouseText pulsing with mouseTextColor, squeezed horizontally (never cut)
	// when wider than the column. Item sprites are not the game's 20x20 info icons: they fit the same box, left edge
	// on the column, centred in its height.
	private static void DrawInfoLine(SpriteBatch sb, Item icon, string name, string value, int x, int y, float maxWidth, Color tint, float fade)
	{
		DynamicSpriteFont font = FontAssets.MouseText.Value;
		if (icon != null && !icon.IsAir && icon.type < TextureAssets.Item.Length)
		{
			Main.instance.LoadItem(icon.type);
			Texture2D texture = TextureAssets.Item[icon.type].Value;
			Rectangle frame = Main.itemAnimations[icon.type]?.GetFrame(texture) ?? texture.Bounds;
			float scale = Math.Min(1f, 20f / (float)Math.Max(frame.Width, frame.Height));
			sb.Draw(texture, new Vector2(x, y + 14), frame, Color.White * fade, 0f, new Vector2(0f, frame.Height / 2f), scale, SpriteEffects.None, 0f);
		}
		string text = name + value;
		Vector2 textScale = Vector2.One;
		float width = font.MeasureString(text).X;
		if (width > maxWidth)
		{
			textScale.X = maxWidth / width;
		}
		if (textScale.X < 0.58f)
		{
			textScale.Y = 1f - textScale.X / 3f;
		}
		float pulse = (float)(int)Main.mouseTextColor / 255f;
		Color color = new Color((int)((float)(int)tint.R * pulse), (int)((float)(int)tint.G * pulse), (int)((float)(int)tint.B * pulse), (int)Main.mouseTextColor) * fade;
		Vector2 position = new Vector2(x + 20, y);
		Vector2[] shadow = new Vector2[4]
		{
			new Vector2(-2f, 0f),
			new Vector2(2f, 0f),
			new Vector2(0f, -2f),
			new Vector2(0f, 2f)
		};
		foreach (Vector2 offset in shadow)
		{
			sb.DrawString(font, text, position + offset, Color.Black * fade, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
		}
		sb.DrawString(font, text, position, color, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
	}

	private static TrackedGoal SelectedTrackingGoal()
	{
		Page = Math.Clamp(Page, 0, TrackingGoalPages - 1);
		int index = BookTrackingSelectionRules.Resolve(TrackedGoals.Count, Page, (TrackingSelection == null) ? (-1) : TrackedGoals.IndexOf(TrackingSelection), BookGeometry.TrackingPageSize);
		return TrackingSelection = ((index < 0) ? null : TrackedGoals[index]);
	}

	private static void HandleTrackingWheel(int step)
	{
		TrackedGoal goal = SelectedTrackingGoal();
		if (BookGeometry.DetailsPanel.Contains(Mouse.X, Mouse.Y))
		{
			if (goal != null)
			{
				goal.ResourcePage = Math.Clamp(goal.ResourcePage + step, 0, TrackingDetailPages(goal) - 1);
			}
		}
		else if (BookGeometry.TrackingPanel.Contains(Mouse.X, Mouse.Y))
		{
			Page = Math.Clamp(Page + step, 0, TrackingGoalPages - 1);
			SelectedTrackingGoal();
		}
	}

	private static void HandleTrackingTabInput()
	{
		if (TrackedGoals.Count == 0)
		{
			return;
		}
		if (BookGeometry.TrackingClear.Contains(Mouse.X, Mouse.Y))
		{
			// Clearing everything has no undo: the first click arms the button, the second one clears.
			if (!TrackingClearArmed)
			{
				TrackingClearArmedAt = Main.GameUpdateCount;
				return;
			}
			StopTracker();
			Page = 0;
			return;
		}
		if (TrackingGoalPages > 1)
		{
			if (BookGeometry.TrackingPrevious.Contains(Mouse.X, Mouse.Y))
			{
				Page = Math.Max(0, Page - 1);
				SelectedTrackingGoal();
				return;
			}
			if (BookGeometry.TrackingNext.Contains(Mouse.X, Mouse.Y))
			{
				Page = Math.Min(TrackingGoalPages - 1, Page + 1);
				SelectedTrackingGoal();
				return;
			}
		}
		for (int row = 0; row < BookGeometry.TrackingPageSize; row++)
		{
			int index = Page * BookGeometry.TrackingPageSize + row;
			if (index >= TrackedGoals.Count)
			{
				break;
			}
			if (BookGeometry.TrackingRow(row).Contains(Mouse.X, Mouse.Y))
			{
				TrackingSelection = TrackedGoals[index];
				return;
			}
		}
		TrackedGoal goal = SelectedTrackingGoal();
		if (goal == null)
		{
			return;
		}
		if (BookGeometry.TrackingRemove.Contains(Mouse.X, Mouse.Y))
		{
			RemoveGoal(goal);
			Page = Math.Clamp(Page, 0, TrackingGoalPages - 1);
			SelectedTrackingGoal();
			if (ResourceTrackerStyle)
			{
				RefreshResourceTracker(notify: false);
			}
			return;
		}
		List<List<(TrackingLine Line, BookRect Rect)>> pages = TrackingDetailLayout(goal);
		if (pages.Count > 1)
		{
			if (BookGeometry.TrackingDetailPrevious.Contains(Mouse.X, Mouse.Y))
			{
				goal.ResourcePage = Math.Max(0, goal.ResourcePage - 1);
				return;
			}
			if (BookGeometry.TrackingDetailNext.Contains(Mouse.X, Mouse.Y))
			{
				goal.ResourcePage = Math.Min(pages.Count - 1, goal.ResourcePage + 1);
				return;
			}
		}
		foreach ((TrackingLine line, BookRect rect) in pages[Math.Clamp(goal.ResourcePage, 0, pages.Count - 1)])
		{
			if (line.SourceTypes != null && TrackingObtain(rect).Contains(Mouse.X, Mouse.Y))
			{
				OpenAcquisition(line.Icon.type, line.SourceTypes);
				return;
			}
		}
	}

	private static string TrackingStatus(TrackedGoal goal)
	{
		if (goal.Unknown)
		{
			return T("Синхронизация…", "Syncing…");
		}
		if (goal.Ready)
		{
			return T("Можно создать", "Ready to craft");
		}
		int met = goal.Progress.Count((BookResourceProgress r) => r.Met);
		return T("Ингредиенты: ", "Ingredients: ") + met + "/" + goal.Progress.Length;
	}

	// "<item's own mod> · Stage", like the item details on the other tabs.
	private static string TrackingOrigin(TrackedGoal goal)
	{
		if (goal.IsSet)
		{
			return string.Join(" / ", goal.Parts.Select((Item p) => ModName(p.ModItem?.Mod.Name ?? "Terraria")).Distinct());
		}
		return ModName(goal.Recipe.createItem.ModItem?.Mod.Name ?? "Terraria") + " · " + StageName(null, goal.Recipe);
	}

	// "Recipe: Calamity Mod" when another mod than the item's own one made this recipe (vanilla Flying Carpet).
	private static string RecipeOwnerNote(TrackedGoal goal)
	{
		string owner = ModName(Owner(goal.Recipe));
		if (goal.IsSet || owner == ModName(goal.Recipe.createItem.ModItem?.Mod.Name ?? "Terraria"))
		{
			return "";
		}
		return T("Рецепт: ", "Recipe: ") + owner;
	}

	private static void DrawTrackingTab(SpriteBatch sb)
	{
		TrackedGoal selected = SelectedTrackingGoal();
		BookRect panel = BookGeometry.TrackingPanel;
		if (TrackedGoals.Count == 0)
		{
			BookRect area = new BookRect(panel.X, panel.Y, BookGeometry.DetailsPanel.X + BookGeometry.DetailsPanel.Width - panel.X, panel.Height);
			Box(sb, area, Surface, border: false, 12f);
			CenterLabel(sb, T("Ничего не отслеживается", "Nothing tracked yet"), area.X, area.Y + area.Height / 2f - 36f, area.Width, 32f, TextColor, 1.1f);
			CenterLabel(sb, T("Выберите предмет и нажмите «Отслеживать»", "Select an item and press Track"), area.X, area.Y + area.Height / 2f + 4f, area.Width, 24f, Muted, 0.9f);
			return;
		}
		Box(sb, panel, Surface, border: false, 12f);
		FixedLabel(sb, T("ОТСЛЕЖИВАЕТСЯ · ", "TRACKED · ") + TrackedGoals.Count + (ResourceTrackerStyle ? "" : ("/" + BookTrackerRules.MaxGoals)), panel.X + 16f, panel.Y + 17f, Muted, 0.8f, 400f, 22f);
		DestructiveButton(sb, BookGeometry.TrackingClear, TrackingClearArmed ? T("Очистить всё?", "Clear All?") : T("Очистить", "Clear"));
		if (BookGeometry.TrackingClear.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(TrackingClearArmed ? T("Нажмите ещё раз, чтобы убрать все цели", "Click again to untrack everything") : T("Не отслеживать ничего", "Untrack everything"));
		}
		for (int row = 0; row < BookGeometry.TrackingPageSize; row++)
		{
			int index = Page * BookGeometry.TrackingPageSize + row;
			if (index >= TrackedGoals.Count)
			{
				break;
			}
			TrackedGoal goal = TrackedGoals[index];
			BookRect rect = BookGeometry.TrackingRow(row);
			bool active = goal == selected;
			Color fill = (active ? new Color(50, 76, 59) : (rect.Contains(Mouse.X, Mouse.Y) ? new Color(51, 67, 55) : Raised));
			Box(sb, rect.X, rect.Y, rect.Width, rect.Height, fill, border: true, 8f, active ? new Color?(Accent) : null, active ? 2 : 1);
			Icon(sb, GoalIcon(goal), rect.X + 12f, rect.Y + 8f, 40f, tooltip: false);
			float countX = rect.X + rect.Width - 16f - 80f;
			float barX = countX - 16f - 140f;
			float nameWidth = barX - 24f - (rect.X + 64f);
			FixedLabel(sb, GoalTitle(goal), rect.X + 64f, rect.Y + 7f, TextColor, 1f, nameWidth, 24f);
			FixedLabel(sb, TrackingStatus(goal), rect.X + 64f, rect.Y + 31f, goal.Ready ? EnoughColor : Muted, 0.8f, nameWidth, 18f);
			DrawProgressBar(sb, barX, rect.Y + 26f, 140f, 4f, goal);
			CenterLabel(sb, CraftsRatio(goal), countX, rect.Y + 7f, 80f, 24f, goal.Ready ? EnoughColor : TextColor, 1f, rightAligned: true);
			CenterLabel(sb, T("крафтов", "crafts"), countX, rect.Y + 31f, 80f, 18f, Muted, 0.8f, rightAligned: true);
		}
		if (TrackingGoalPages > 1)
		{
			ChevronPager(sb, BookGeometry.TrackingPrevious, BookGeometry.TrackingNext, BookGeometry.TrackingPager, Page, TrackingGoalPages);
		}
		DrawSelectedTrackingGoal(sb, selected);
	}

	private static void DrawSelectedTrackingGoal(SpriteBatch sb, TrackedGoal goal)
	{
		BookRect d = BookGeometry.DetailsPanel;
		Box(sb, d, Surface, border: false, 12f);
		if (goal == null)
		{
			return;
		}
		Icon(sb, GoalIcon(goal), d.X + 16f, d.Y + 16f, 48f);
		string title = GoalTitle(goal);
		float titleSize = Math.Clamp((d.Width - 92f) / Math.Max(1f, Ink(title).Width * TextScale), 0.85f, 1.05f);
		FixedLabel(sb, title, d.X + 76f, d.Y + 17f, TextColor, titleSize, d.Width - 92f, 26f);
		FixedLabel(sb, TrackingOrigin(goal), d.X + 76f, d.Y + 45f, Muted, 0.8f, d.Width - 92f, 20f);
		FixedLabel(sb, T("Крафтов: ", "Crafts: ") + CraftsRatio(goal), d.X + 16f, d.Y + 76f, goal.Ready ? EnoughColor : TextColor, 0.85f, 160f, 20f);
		DrawProgressBar(sb, d.X + 16f, d.Y + 102f, d.Width - 32f, 6f, goal);
		List<List<(TrackingLine Line, BookRect Rect)>> pages = TrackingDetailLayout(goal);
		goal.ResourcePage = Math.Clamp(goal.ResourcePage, 0, pages.Count - 1);
		foreach ((TrackingLine line, BookRect rect) in pages[goal.ResourcePage])
		{
			if (line.Section)
			{
				float labelWidth = Math.Min(rect.Width, CountWidth(line.Name, 0.78f));
				FixedLabel(sb, line.Name, rect.X, rect.Y + 6f, Muted, 0.78f, labelWidth, 20f);
				if (line.Value.Length > 0)
				{
					CenterLabel(sb, line.Value, rect.X + labelWidth + 12f, rect.Y + 6f, rect.Width - labelWidth - 12f, 20f, Muted, 0.78f, rightAligned: true);
				}
				continue;
			}
			Box(sb, rect, Raised, border: false, 8f);
			float textX = rect.X + 12f;
			if (line.Icon != null && !line.Icon.IsAir)
			{
				Icon(sb, line.Icon, rect.X + 8f, rect.Y + 6f, 24f);
				textX = rect.X + 40f;
			}
			float right = rect.X + rect.Width - ((line.SourceTypes != null) ? 44f : 12f);
			float valueWidth = ((line.Value.Length > 0) ? CountWidth(line.Value, 0.85f) : 0f);
			FixedLabel(sb, line.Name, textX, rect.Y + 7f, line.NameColor, 0.85f, right - valueWidth - 6f - textX, 22f);
			if (valueWidth > 0f)
			{
				CenterLabel(sb, line.Value, right - valueWidth, rect.Y + 7f, valueWidth, 22f, line.ValueColor, 0.85f, rightAligned: true);
			}
			if (line.SourceTypes != null)
			{
				BookRect obtain = TrackingObtain(rect);
				Button(sb, obtain, "?");
				if (obtain.Contains(Mouse.X, Mouse.Y))
				{
					MouseTip(T("Где взять: ", "How to Get: ") + line.Name);
				}
			}
		}
		if (pages.Count > 1)
		{
			ChevronPager(sb, BookGeometry.TrackingDetailPrevious, BookGeometry.TrackingDetailNext, BookGeometry.TrackingDetailPager, goal.ResourcePage, pages.Count);
		}
		DestructiveButton(sb, BookGeometry.TrackingRemove, T("Не отслеживать", "Untrack"));
	}

	private static BookRect TrackingObtain(BookRect row)
	{
		return new BookRect(row.X + row.Width - 32f, row.Y + 4f, 28f, 28f);
	}

	private static int TrackingDetailPages(TrackedGoal goal)
	{
		return TrackingDetailLayout(goal).Count;
	}

	// Section labels and rows of the selected goal, split into pages that fit above the Untrack button.
	private static List<List<(TrackingLine Line, BookRect Rect)>> TrackingDetailLayout(TrackedGoal goal)
	{
		BookRect d = BookGeometry.DetailsPanel;
		List<TrackingLine> lines = TrackingDetails(goal);
		float top = d.Y + 120f;
		float total = lines.Sum((TrackingLine l) => l.Section ? 28f : 40f);
		float bottom = ((top + total <= BookGeometry.TrackingRemove.Y - 8f) ? (BookGeometry.TrackingRemove.Y - 8f) : (BookGeometry.TrackingDetailPrevious.Y - 8f));
		List<List<(TrackingLine, BookRect)>> pages = new List<List<(TrackingLine, BookRect)>> { new List<(TrackingLine, BookRect)>() };
		float y = top;
		foreach (TrackingLine line in lines)
		{
			float height = (line.Section ? 28f : 40f);
			// A section label never ends a page: it moves on with its first row.
			if (y + height + (line.Section ? 40f : 0f) > bottom && pages[^1].Count > 0)
			{
				pages.Add(new List<(TrackingLine, BookRect)>());
				y = top;
			}
			pages[^1].Add((line, new BookRect(d.X + 16f, y, d.Width - 32f, line.Section ? 28f : 36f)));
			y += height;
		}
		return pages;
	}

	private sealed record TrackingLine(Item Icon, string Name, string Value, Color NameColor, Color ValueColor, int[] SourceTypes = null, bool Section = false);

	private static List<TrackingLine> TrackingDetails(TrackedGoal goal)
	{
		List<TrackingLine> lines = new List<TrackingLine>
		{
			new TrackingLine(null, T("ИНГРЕДИЕНТЫ", "INGREDIENTS"), RecipeOwnerNote(goal), Muted, Muted, null, Section: true)
		};
		if (goal.Progress.Length == goal.Materials.Count)
		{
			foreach (int i in MissingFirst(goal.Progress))
			{
				BookResourceProgress row = goal.Progress[i];
				MaterialRequirement material = goal.Materials[i];
				lines.Add(new TrackingLine(material.Ingredient, MaterialName(material), ProgressRatio(row), TextColor, ProgressColor(row), material.ItemTypes.OrderBy((int t) => t).ToArray()));
			}
		}
		lines.Add(new TrackingLine(null, T("СТАНЦИЯ", "STATION"), "", Muted, Muted, null, Section: true));
		int[] tiles = (from t in goal.Recipes.SelectMany((Recipe r) => r.requiredTile)
			where t >= 0
			select t).Distinct().ToArray();
		foreach (int tile in tiles)
		{
			StationItems.TryGetValue(tile, out var station);
			string stationName = StationName(tile);
			lines.Add(new TrackingLine(station, stationName, "", TextColor, Muted));
		}
		if (tiles.Length == 0)
		{
			lines.Add(new TrackingLine(null, T("Вручную", "By hand"), "", Muted, Muted));
		}
		string[] conditions = (from c in goal.Recipes.SelectMany((Recipe r) => r.Conditions)
			select ConditionName(c)).Distinct().ToArray();
		if (conditions.Length > 0)
		{
			lines.Add(new TrackingLine(null, T("УСЛОВИЯ", "CONDITIONS"), "", Muted, Muted, null, Section: true));
			foreach (string condition in conditions)
			{
				lines.Add(new TrackingLine(null, condition, "", ConditionColor, Muted));
			}
		}
		return lines;
	}

	internal static void ReportUiFailure(string phase, Exception error)
	{
		SetTyping(value: false);
		Open = (MouseOver = (ModMenu = (HelpOpen = false)));
		CloseAcquisition();
		BookSources.CancelCraft();
		ModContent.GetInstance<ModernRecipeBook>().Logger.Error("MRB " + ModContent.GetInstance<ModernRecipeBook>().Version + " UI failure (" + phase + "). Book closed safely.", error);
		Main.NewText(T("Книга рецептов: ошибка интерфейса. Подробности в client.log.", "Recipe Book: UI error. Details in client.log."), 240, 160, 100);
	}

	private static void SafeDrawBook(SpriteBatch sb)
	{
		try
		{
			Draw(sb);
			FlushTip();
		}
		catch (Exception error)
		{
			PendingTip = null;
			ReportUiFailure("book/draw", error);
		}
	}

	internal static BookUiState CapturePlayerUiState()
	{
		return CaptureUiState();
	}

	internal static TagCompound WritePlayerUiState(BookUiState source)
	{
		BookUiState state = (source ?? new BookUiState()).Sanitize();
		return new TagCompound
		{
			["Version"] = BookUiState.CurrentVersion,
			["Tabs"] = state.Tabs.Select((KeyValuePair<string, BookUiState.TabFilters> tab) => new TagCompound
			{
				["Tab"] = tab.Key,
				["Query"] = tab.Value.Query,
				["ModFilter"] = tab.Value.ModFilter,
				["CategoryKey"] = tab.Value.CategoryKey,
				["FavoritesOnly"] = tab.Value.FavoritesOnly,
				["MetOnly"] = tab.Value.MetOnly,
				["SortMode"] = tab.Value.SortMode.ToString(),
				["SortDescending"] = tab.Value.SortDescending,
				["ExcludedStations"] = tab.Value.ExcludedStations.ToList()
			}).ToList(),
			["LastTab"] = state.LastTab,
			["Query"] = state.Query,
			["ModFilter"] = state.ModFilter,
			["StageFilter"] = state.StageFilter,
			["ClassFilter"] = state.ClassFilter,
			["CategoryKey"] = state.CategoryKey,

			["FavoritesOnly"] = state.FavoritesOnly,
			["UsedInMode"] = state.UsedInMode,
			["UsedInCraftableOnly"] = state.UsedInCraftableOnly,
			["SortMode"] = state.SortMode.ToString(),
			["SortDescending"] = state.SortDescending,
			["WindowScalePercent"] = state.WindowScalePercent,
			["HasCustomPosition"] = state.HasCustomPosition,
			["WindowX"] = state.WindowX,
			["WindowY"] = state.WindowY,
			["TrackerHasCustomPosition"] = state.TrackerHasCustomPosition,
			["TrackerX"] = state.TrackerX,
			["TrackerY"] = state.TrackerY
		};
	}

	internal static BookUiState ReadPlayerUiState(TagCompound playerTag)
	{
		if (playerTag == null || !playerTag.ContainsKey("ModernRecipeBookUI"))
		{
			return new BookUiState();
		}
		try
		{
			TagCompound tag = playerTag.GetCompound("ModernRecipeBookUI");
			BookUiState state = new BookUiState();
			if (tag.ContainsKey("LastTab"))
			{
				state.LastTab = tag.GetString("LastTab");
			}
			if (tag.ContainsKey("Query"))
			{
				state.Query = tag.GetString("Query");
			}
			if (tag.ContainsKey("StageFilter"))
			{
				state.StageFilter = tag.GetInt("StageFilter");
			}
			// Saves before version 5 have no class filter: all classes.
			if (tag.ContainsKey("ClassFilter"))
			{
				state.ClassFilter = tag.GetInt("ClassFilter");
			}
			if (tag.ContainsKey("ModFilter"))
			{
				state.ModFilter = tag.GetString("ModFilter");
			}
			if (tag.ContainsKey("CategoryKey"))
			{
				state.CategoryKey = tag.GetString("CategoryKey");
			}

			if (tag.ContainsKey("FavoritesOnly"))
			{
				state.FavoritesOnly = tag.GetBool("FavoritesOnly");
			}
			if (tag.ContainsKey("UsedInMode"))
			{
				state.UsedInMode = tag.GetBool("UsedInMode");
			}
			// Version 3 called it ChainCraftableOnly (the Chains tab's "Craftable" toggle).
			if (tag.ContainsKey("UsedInCraftableOnly") || tag.ContainsKey("ChainCraftableOnly"))
			{
				state.UsedInCraftableOnly = tag.GetBool(tag.ContainsKey("UsedInCraftableOnly") ? "UsedInCraftableOnly" : "ChainCraftableOnly");
			}
			if (tag.ContainsKey("SortMode") && Enum.TryParse<BookSortMode>(tag.GetString("SortMode"), ignoreCase: true, out var sortMode))
			{
				state.SortMode = sortMode;
			}
			if (tag.ContainsKey("SortDescending"))
			{
				state.SortDescending = tag.GetBool("SortDescending");
			}
			if (tag.ContainsKey("WindowScalePercent"))
			{
				state.WindowScalePercent = tag.GetInt("WindowScalePercent");
			}
			if (tag.ContainsKey("HasCustomPosition"))
			{
				state.HasCustomPosition = tag.GetBool("HasCustomPosition");
			}
			if (tag.ContainsKey("WindowX"))
			{
				state.WindowX = tag.GetFloat("WindowX");
			}
			if (tag.ContainsKey("WindowY"))
			{
				state.WindowY = tag.GetFloat("WindowY");
			}
			if (tag.ContainsKey("TrackerHasCustomPosition"))
			{
				state.TrackerHasCustomPosition = tag.GetBool("TrackerHasCustomPosition");
			}
			if (tag.ContainsKey("TrackerX"))
			{
				state.TrackerX = tag.GetFloat("TrackerX");
			}
			if (tag.ContainsKey("TrackerY"))
			{
				state.TrackerY = tag.GetFloat("TrackerY");
			}
			if (tag.ContainsKey("Tabs"))
			{
				foreach (TagCompound tab in tag.GetList<TagCompound>("Tabs"))
				{
					BookUiState.TabFilters filters = new BookUiState.TabFilters
					{
						Query = tab.GetString("Query"),
						ModFilter = tab.GetString("ModFilter"),
						CategoryKey = tab.GetString("CategoryKey"),
						FavoritesOnly = tab.GetBool("FavoritesOnly"),
						MetOnly = tab.GetBool("MetOnly"),
						SortDescending = tab.GetBool("SortDescending"),
						// Saves before version 6 have none.
						ExcludedStations = (tab.ContainsKey("ExcludedStations") ? tab.GetList<string>("ExcludedStations").ToArray() : Array.Empty<string>())
					};
					if (Enum.TryParse<BookSortMode>(tab.GetString("SortMode"), ignoreCase: true, out var tabSort))
					{
						filters.SortMode = tabSort;
					}
					state.Tabs[tab.GetString("Tab")] = filters;
				}
			}
			return state.Sanitize();
		}
		catch
		{
			return new BookUiState();
		}
	}

	internal static void QueuePlayerUiRestore(BookUiState state)
	{
		PendingUiState = (state ?? new BookUiState()).Sanitize();
		RestorePending = true;
	}

	internal static void RestorePendingPlayerUiState()
	{
		if (RestorePending)
		{
			RestoreUiState(PendingUiState ?? new BookUiState());
			PendingUiState = null;
			RestorePending = false;
			UiStateInitialized = true;
		}
	}

	private static BookUiState CaptureUiState()
	{
		string categoryKey = ((Category >= 0 && Category < Categories.Count) ? Categories[Category].Key : "all");

		if (CurrentTab < BookUiState.FilterTabs.Length)
		{
			TabFilterSlots[CurrentTab] = LiveTabFilters();
		}
		return new BookUiState
		{
			Tabs = TabFilterSlots.Where((KeyValuePair<int, BookUiState.TabFilters> slot) => slot.Key < BookUiState.FilterTabs.Length).ToDictionary((KeyValuePair<int, BookUiState.TabFilters> slot) => BookUiState.FilterTabs[slot.Key], (KeyValuePair<int, BookUiState.TabFilters> slot) => slot.Value),
			LastTab = (TrackingView ? "tracking" : (RecentView ? "recent" : (StorageView ? "storage" : (BestiaryView ? "bestiary" : (Catalogue ? "items" : "recipes"))))),
			Query = Query,
			ModFilter = ModFilter,
			StageFilter = (int)StageFilter,
			ClassFilter = (int)ClassFilter,
			CategoryKey = categoryKey,
			FavoritesOnly = FavoritesOnly,
			UsedInMode = UsedInMode,
			UsedInCraftableOnly = UsedInCraftableOnly,
			SortMode = SortMode,
			SortDescending = SortDescending,
			WindowScalePercent = WindowScalePercent,
			HasCustomPosition = HasCustomWindowPosition,
			WindowX = WindowPositionX,
			WindowY = WindowPositionY,
			TrackerHasCustomPosition = TrackerCustomPosition,
			TrackerX = TrackerPositionX,
			TrackerY = TrackerPositionY
		}.Sanitize();
	}

	private static void RestoreUiState(BookUiState source)
	{
		BookUiState state = source.Sanitize();
		int lastTab = Array.IndexOf(new string[6] { "items", "recipes", "bestiary", "storage", "recent", "tracking" }, state.LastTab);
		SetTabFlags((lastTab < 0) ? 0 : lastTab);
		// Version 3 saves had one filter set shared by all tabs: the tab that was open keeps it, the others
		// keep only its sort order.
		BookUiState.TabFilters shared = new BookUiState.TabFilters
		{
			Query = state.Query,
			ModFilter = state.ModFilter,
			CategoryKey = state.CategoryKey,
			FavoritesOnly = state.FavoritesOnly,
			SortMode = state.SortMode,
			SortDescending = state.SortDescending
		};
		TabFilterSlots.Clear();
		for (int tab = 0; tab < BookUiState.FilterTabs.Length; tab++)
		{
			if (state.Tabs.TryGetValue(BookUiState.FilterTabs[tab], out var filters))
			{
				TabFilterSlots[tab] = filters;
			}
			else if (state.Tabs.Count == 0)
			{
				TabFilterSlots[tab] = ((BookUiState.FilterTabs[tab] == state.LastTab) ? shared : new BookUiState.TabFilters
				{
					SortMode = shared.SortMode,
					SortDescending = shared.SortDescending
				});
			}
		}
		StageFilter = (BookStageFilter)state.StageFilter;
		ClassFilter = (BookClass)state.ClassFilter;
		UsedInMode = state.UsedInMode;
		UsedInCraftableOnly = state.UsedInCraftableOnly;
		WindowScalePercent = state.WindowScalePercent;
		HasCustomWindowPosition = state.HasCustomPosition;
		WindowPositionX = state.WindowX;
		WindowPositionY = state.WindowY;
		TrackerCustomPosition = state.TrackerHasCustomPosition;
		TrackerPositionX = state.TrackerX;
		TrackerPositionY = state.TrackerY;
		LoadTabFilters(TabFilterSlots.GetValueOrDefault(CurrentTab));
		SetTyping(value: false);
		ModMenu = (StationMenu = (HelpOpen = false));
		ArmorFocus = 0;
		Selected = -1;
		Page = (DetailsPage = (Variant = 0));
		CategoryRow = Math.Clamp(CategoryRow, 0, MaxCategoryRow);
		ResetCard();
		ApplyFilters();
	}

	// Each tab keeps its own search, mod, category, favorites and sort; the live fields hold the open tab's.
	// Nothing resets them except the player (the search's clear button, Ctrl+Backspace, picking a value).
	private static readonly Dictionary<int, BookUiState.TabFilters> TabFilterSlots = new Dictionary<int, BookUiState.TabFilters>();

	private static BookUiState.TabFilters LiveTabFilters()
	{
		return new BookUiState.TabFilters
		{
			Query = Query,
			ModFilter = ModFilter,
			CategoryKey = ((Category >= 0 && Category < Categories.Count) ? Categories[Category].Key : "all"),
			FavoritesOnly = FavoritesOnly,
			MetOnly = MetOnly,
			SortMode = SortMode,
			SortDescending = SortDescending,
			ExcludedStations = ExcludedStations
		};
	}

	private static void LoadTabFilters(BookUiState.TabFilters filters)
	{
		BookUiState.TabFilters source = (filters ?? new BookUiState.TabFilters()).Sanitize();
		Query = source.Query;
		ModFilter = (KnownMod(source.ModFilter) ? source.ModFilter : "");
		Category = Math.Max(0, Categories.FindIndex((BookCategory value) => value.Key == source.CategoryKey));
		if (Category < CategoryRow || Category >= CategoryRow + CategoryRows)
		{
			CategoryRow = Math.Clamp(Category - CategoryRows / 2, 0, MaxCategoryRow);
		}
		FavoritesOnly = source.FavoritesOnly;
		MetOnly = source.MetOnly;
		SortMode = source.SortMode;
		SortDescending = source.SortDescending;
		SetExcludedStations(source.ExcludedStations);
	}

	private static void ChangeFilterTab(int from, int to, BookUiState.TabFilters leaving)
	{
		if (from == to)
		{
			return;
		}
		TabFilterSlots[from] = leaving;
		LoadTabFilters(TabFilterSlots.GetValueOrDefault(to));
	}

	private static bool HandleUiStateInput(bool click)
	{
		if (DraggingWindow)
		{
			if (Main.mouseLeft)
			{
				MoveWindowWithPointer();
				Main.LocalPlayer.mouseInterface = true;
				PlayerInput.LockVanillaMouseScroll("ModernRecipeBook");
				return true;
			}
			DraggingWindow = false;
		}
		if (!Open || HelpOpen || MenuOpen || AcquisitionOpen)
		{
			return false;
		}
		if (click && BookGeometry.WindowSizeDecrease.Contains(Mouse.X, Mouse.Y))
		{
			WindowScalePercent = Math.Clamp(WindowScalePercent - 10, 70, 130);
			return true;
		}
		if (click && BookGeometry.WindowSizeIncrease.Contains(Mouse.X, Mouse.Y))
		{
			WindowScalePercent = Math.Clamp(WindowScalePercent + 10, 70, 130);
			return true;
		}
		if (click && HeaderGrab())
		{
			DraggingWindow = true;
			WindowDragOffsetX = Main.MouseScreen.X - Origin.X;
			WindowDragOffsetY = Main.MouseScreen.Y - Origin.Y;
			return true;
		}
		return false;
	}

	// The window moves only by the grip (the dots left of the title); the frame and the header don't drag.
	private static bool HeaderGrab()
	{
		return BookGeometry.WindowGrip.Contains(Mouse.X, Mouse.Y);
	}

	// The grip's dots light up while hovered or dragged; the frame lights only during a drag.
	private static bool GrabHint => DraggingWindow || (!MenuOpen && !HelpOpen && HeaderGrab());

	private static void MoveWindowWithPointer()
	{
		float travelX = Math.Max(0f, (float)(Main.screenWidth - 32) - 1120f * Scale);
		float travelY = Math.Max(0f, (float)(Main.screenHeight - 32) - 696f * Scale);
		float left = Math.Clamp(Main.MouseScreen.X - WindowDragOffsetX, 16f, 16f + travelX);
		float top = Math.Clamp(Main.MouseScreen.Y - WindowDragOffsetY, 16f, 16f + travelY);
		WindowPositionX = ((travelX > 0f) ? BookUiState.NormalizePosition((left - 16f) / travelX) : 0.5f);
		WindowPositionY = ((travelY > 0f) ? BookUiState.NormalizePosition((top - 16f) / travelY) : 0.5f);
		HasCustomWindowPosition = true;
	}

	private static int CompareFilteredIndexes(int left, int right)
	{
		if (StationSort)
		{
			return CompareStationRank(left, right);
		}
		Item a = Result(left);
		Item b = Result(right);
		int comparison;
		if (SortMode == BookSortMode.ItemId)
		{
			comparison = a.type.CompareTo(b.type);
		}
		else if (SortMode == BookSortMode.Mod)
		{
			comparison = string.Compare(SortOwner(left), SortOwner(right), StringComparison.CurrentCultureIgnoreCase);
			if (comparison == 0)
			{
				comparison = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
			}
		}
		else
		{
			comparison = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
		}
		if (comparison == 0)
		{
			comparison = a.type.CompareTo(b.type);
		}
		if (comparison == 0)
		{
			comparison = left.CompareTo(right);
		}
		if (!SortDescending)
		{
			return comparison;
		}
		return -Math.Sign(comparison);
	}

	private static string SortOwner(int index)
	{
		if (Catalogue || StorageView || index < 0 || index >= Recipe.numRecipes)
		{
			return Result(index)?.ModItem?.Mod.Name ?? "Terraria";
		}
		return Owner(Main.recipe[index]);
	}

	private static string SortLabel()
	{
		if (BestiaryView)
		{
			return MobSortShortLabel(SortOptionIndex);
		}
		// The closed dropdown has room for about ten letters: the station orders keep an arrow there, as Met does.
		return SortOptionIndex switch
		{
			6 => T("Станция ↑", "Station ↑"),
			7 => T("Станция ↓", "Station ↓"),
			_ => SortOptionLabel(SortOptionIndex),
		};
	}

	// One list for field and direction: Name, ID and Mod, each ascending then descending, then Station on Items and
	// Recipes. The Bestiary tab has its own list (MobSortOptions). A mode a tab does not offer reads as Name.
	private static int SortOptionIndex => BestiaryView ? MobSortOptionIndex : (SortMode switch
	{
		BookSortMode.ItemId => 2,
		BookSortMode.Mod => 4,
		BookSortMode.Station when StationFilterTab => 6,
		_ => 0,
	} + (SortDescending ? 1 : 0));

	private static int SortOptionCount => BestiaryView ? MobSortOptions.Length : (StationFilterTab ? 8 : 6);

	private static string SortOptionLabel(int option)
	{
		if (BestiaryView)
		{
			return MobSortOptionLabel(option);
		}
		return option switch
		{
			0 => T("Имя А-Я", "Name A-Z"),
			1 => T("Имя Я-А", "Name Z-A"),
			2 => "ID 1-9",
			3 => "ID 9-1",
			4 => T("Мод А-Я", "Mod A-Z"),
			5 => T("Мод Я-А", "Mod Z-A"),
			6 => T("Станция: от слабой", "Station: weakest first"),
			_ => T("Станция: от сильной", "Station: strongest first"),
		};
	}

	private static void ApplySortOption(int option)
	{
		if (BestiaryView)
		{
			(SortMode, SortDescending) = MobSortOptions[Math.Clamp(option, 0, MobSortOptions.Length - 1)];
		}
		else
		{
			SortMode = (option / 2) switch
			{
				1 => BookSortMode.ItemId,
				2 => BookSortMode.Mod,
				3 => BookSortMode.Station,
				_ => BookSortMode.Name,
			};
			SortDescending = option % 2 == 1;
		}
		Page = 0;
		ApplyFilters();
	}

	internal static string T(string ru, string en)
	{
		if (!Ru)
		{
			return en;
		}
		return ru;
	}

	private static string ItemKey(Item item)
	{
		return item.ModItem?.FullName ?? ("Terraria/" + item.type);
	}

	private static string Owner(Recipe r)
	{
		return r.Mod?.Name ?? r.createItem.ModItem?.Mod.Name ?? "Terraria";
	}

	private static string ModName(string name)
	{
		if (!(name == "Terraria"))
		{
			if (!ModLoader.TryGetMod(name, out var mod))
			{
				return name;
			}
			return mod.DisplayName;
		}
		return name;
	}

	public override void PostAddRecipes()
	{
		Dirty = true;
	}

	public override void OnWorldUnload()
	{
		ResetView();
		// Tile drops read a little of the world (seed flags, time of day for herb seeds): read them again next world.
		BookWorldSources.Clear();
	}

	public override void Unload()
	{
		BookSkin.Unload();
		BookProgression.Clear();
		BookItemSources.Clear();
		BookArmorSets.Clear();
		BookItemClasses.Clear();
		ResetView();
		AllItems.Clear();
		RecipesByItem.Clear();
		BookRecipeVariants.Clear();
		All.Clear();
		Filtered.Clear();
		ItemMods.Clear();
		CatalogueMods.Clear();
		RecipeMods.Clear();
		StationItems.Clear();
		ItemCategoryList.Clear();
		ItemCategories.Clear();
		ClearBestiary();
		Dirty = true;
	}

	internal static void ResetView()
	{
		CloseMarkerPanel();
		CloseAcquisition();
		StopTracker();
		BlockTrackerPress = false;
		PendingBack = (PendingForward = false);
		TrackerCustomPosition = false;
		TrackerPositionX = 0f;
		TrackerPositionY = 1f;
		SetTyping(value: false);
		BookCrafting.Clear();
		UsedInMode = (UsedInCraftableOnly = false);
		AppliedAvailabilityVersion = -1;
		Open = (MouseOver = (ModMenu = (StorageView = (RecentView = (TrackingView = (BestiaryView = false))))));
		Catalogue = true;
		BookSources.Clear();
		AppliedStorageVersion = -1;
		AppliedRecentVersion = -1;
		Variant = 0;
		ResetCard();
		History.Clear();
		LiveMobStats.Clear();
		UsedInCache.Clear();
		BatchChoice = 0;
		InvalidateBatchEstimate();
		Query = (ModFilter = "");
		StageFilter = BookStageFilter.All;
		ClassFilter = BookClass.None;
		TabFilterSlots.Clear();
		Page = (DetailsPage = (Category = (CategoryRow = 0)));
		ArmorFocus = 0;
		Selected = -1;
		FavoritesOnly = false;
		LastLeft = (LastRight = (HelpOpen = false));
		LastEscape = false;
		LastKeys = default;
		SortMode = BookSortMode.Name;
		SortDescending = false;
		WindowScalePercent = 100;
		HasCustomWindowPosition = (DraggingWindow = false);
		WindowPositionX = (WindowPositionY = 0.5f);
		PendingUiState = null;
		RestorePending = (UiStateInitialized = false);
	}

	internal static void Toggle()
	{
		if (!Main.gameMenu)
		{
			bool flag = !Open;
			SetTyping(value: false);
			// Help and the How to Get / enemy cards survive closing the book; only the dropdowns fold.
			ModMenu = (StationMenu = false);
			if (!flag)
			{
				Open = (MouseOver = false);
				BookSources.CancelCraft();
			}
			LastMiddle = Microsoft.Xna.Framework.Input.Mouse.GetState().MiddleButton == ButtonState.Pressed;
			LastLeft = Main.mouseLeft;
			LastRight = Main.mouseRight;
			LastKeys = Keyboard.GetState();
			LastEscape = LastKeys.IsKeyDown(Keys.Escape);
			LastWheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
			if (flag)
			{
				Rebuild();
				RestorePendingPlayerUiState();
				BookSources.OnBookOpened();
				BookCrafting.Refresh(force: true);
				ApplyFilters();
				Open = true;
			}
		}
	}

	internal static bool OpenForInventoryItem(int type)
	{
		if (Main.gameMenu || type <= 0 || !ContentSamples.ItemsByType.TryGetValue(type, out var sample) || sample.IsAir)
		{
			return false;
		}
		SetTyping(value: false);
		ModMenu = (StationMenu = (HelpOpen = false));
		CloseAcquisition();
		BookSources.OnBookOpened();
		Rebuild();
		BookCrafting.Refresh(force: true);
		RestorePendingPlayerUiState();
		if (StorageView || TrackingView || BestiaryView)
		{
			SwitchTab(0);
		}
		StepCard(type, usedIn: true);
		Open = true;
		LastLeft = Main.mouseLeft;
		LastRight = Main.mouseRight;
		LastMiddle = Microsoft.Xna.Framework.Input.Mouse.GetState().MiddleButton == ButtonState.Pressed;
		LastKeys = Keyboard.GetState();
		LastEscape = LastKeys.IsKeyDown(Keys.Escape);
		LastWheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		Main.cursorOverride = 0;
		Main.LocalPlayer.mouseInterface = true;
		return true;
	}

	private static void Rebuild()
	{
		string selectedCategory = ((Category >= 0 && Category < Categories.Count) ? Categories[Category].Key : "all");
		All.Clear();
		AllItems.Clear();
		RecipesByItem.Clear();
		ItemMods.Clear();
		CatalogueMods.Clear();
		RecipeMods.Clear();
		StationItems.Clear();
		RegisteredRecipes = (DisabledRecipes = 0);
		BookRecent.RefreshIndex();
		HashSet<string> sources = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < Recipe.numRecipes; i++)
		{
			Recipe r = Main.recipe[i];
			if (r == null || r.createItem == null || r.createItem.IsAir)
			{
				continue;
			}
			RegisteredRecipes++;
			if (r.Disabled)
			{
				DisabledRecipes++;
				continue;
			}
			All.Add(i);
			if (!RecipesByItem.TryGetValue(r.createItem.type, out var variants))
			{
				variants = (RecipesByItem[r.createItem.type] = new List<int>());
			}
			variants.Add(i);
			sources.Add(Owner(r));
			sources.Add(r.createItem.ModItem?.Mod.Name ?? "Terraria");
		}
		foreach (Item item in ContentSamples.ItemsByType.Values.OrderBy((Item item4) => item4.type))
		{
			// Nameless internal items (e.g. ManaCloakStar, an effect sprite) are not catalogue entries.
			if (item != null && !item.IsAir && item.type > 0 && !string.IsNullOrWhiteSpace(item.Name))
			{
				AllItems.Add(item.type);
				sources.Add(item.ModItem?.Mod.Name ?? "Terraria");
			}
		}
		ItemMods.AddRange(sources.OrderBy(ModName, StringComparer.CurrentCultureIgnoreCase));
		CatalogueMods.AddRange(AllItems.Select((int type) => ContentSamples.ItemsByType[type].ModItem?.Mod.Name ?? "Terraria").Distinct());
		RecipeMods.AddRange(All.SelectMany((int index) => new string[2]
		{
			Owner(Main.recipe[index]),
			Main.recipe[index].createItem.ModItem?.Mod.Name ?? "Terraria"
		}).Distinct());
		TabModsKey = (-1, 0, null, -1);
		if (ModFilter.Length > 0 && !KnownMod(ModFilter))
		{
			ModFilter = "";
		}
		foreach (Item item2 in ContentSamples.ItemsByType.Values)
		{
			if (item2 != null && !item2.IsAir && item2.createTile >= 0 && !StationItems.ContainsKey(item2.createTile))
			{
				StationItems[item2.createTile] = item2;
			}
		}
		List<Item> list2 = AllItems.Select((int type) => ContentSamples.ItemsByType[type]).ToList();
		HashSet<int> usedStations = new HashSet<int>(from tile in All.SelectMany((int index) => Main.recipe[index].requiredTile)
			where tile >= 0
			select tile);
		ItemCategoryList = BookCategories.Create(usedStations);
		Category = Math.Max(0, Categories.FindIndex((BookCategory bookCategory) => bookCategory.Key == selectedCategory));
		ItemCategories.Clear();
		foreach (Item item3 in list2)
		{
			HashSet<int> matches = new HashSet<int>();
			for (int c = 0; c < ItemCategoryList.Count; c++)
			{
				if (ItemCategoryList[c].Matches(item3))
				{
					matches.Add(c);
				}
			}
			ItemCategories[item3.type] = matches;
		}
		CategoryRow = Math.Clamp(CategoryRow, 0, MaxCategoryRow);
		BookRecipeVariants.Build(RecipesByItem);
		try
		{
			BookProgression.Rebuild(All, RecipesByItem);
		}
		catch (Exception exception)
		{
			BookProgression.Clear();
			StageFilter = BookStageFilter.All;
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("MRB progression index failed; showing unfiltered catalogue.", exception);
		}
		// After the stages: the Station sort ranks stations by them.
		BuildStationIndex();
		try
		{
			BookArmorSets.Rebuild();
		}
		catch (Exception exception2)
		{
			BookArmorSets.Clear();
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("MRB armor index failed; using individual items.", exception2);
		}
		try
		{
			BookItemClasses.Build();
		}
		catch (Exception exception3)
		{
			ClassFilter = BookClass.None;
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("MRB class index failed; the class filter shows everything.", exception3);
		}
		UsedInCache.Clear();
		Dirty = false;
		ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"Recipe index audit: scanned {Recipe.numRecipes} slots; {RegisteredRecipes} registered, {All.Count} enabled, {DisabledRecipes} disabled; {AllItems.Count} loaded items.");
	}

	private static bool MatchCategory(Item item)
	{
		if (Category != 0)
		{
			if (ItemCategories.TryGetValue(item.type, out var matches))
			{
				return matches.Contains(Category);
			}
			return false;
		}
		return true;
	}

	private static Item Result(int index)
	{
		if (BestiaryView)
		{
			return new Item();
		}
		if (!StorageView)
		{
			if (!Catalogue)
			{
				return Main.recipe[index].createItem;
			}
			return ContentSamples.ItemsByType[index];
		}
		// A refresh can shrink the list before the grid re-filters on the next update.
		return (index >= 0 && index < BookSources.Items.Count) ? BookSources.Items[index] : new Item();
	}

	private static void ApplyFilters(bool clearMessage = true)
	{
		Filtered.Clear();
		ClassHidden = 0;
		StationCandidates.Clear();
		StationCandidatesVersion++;
		StationHidden = 0;

		AppliedAvailabilityVersion = BookCrafting.AvailabilityVersion;
		if (clearMessage)
		{
			BookCrafting.LastMessage = "";
		}
		if (TrackingView)
		{
			Selected = -1;
			Page = Math.Clamp(Page, 0, TrackingGoalPages - 1);
			return;
		}
		if (BestiaryView)
		{
			ApplyMobFilters();
			return;
		}

		bool stations = StationFilterActive;
		string[] terms = BookRules.SearchTerms(Query);
		IEnumerable<int> enumerable2;
		if (!StorageView)
		{
			IEnumerable<int> enumerable;
			if (Catalogue)
			{
				enumerable = AllItems;
			}
			else
			{
				// One cell per merged recipe; a recent craft of any member shows as that recipe.
				enumerable = (RecentView ? BookRecent.ResolveRecipeIndexes().Select(BookRecipeVariants.Representative).Distinct() : All.Where((int index) => BookRecipeVariants.Representative(index) == index));
				if (Selected >= 0)
				{
					Selected = BookRecipeVariants.Representative(Selected);
				}
			}
			enumerable2 = enumerable;
		}
		else
		{
			enumerable2 = Enumerable.Range(0, BookSources.Items.Count);
		}
		foreach (int index in enumerable2)
		{
			Item item = Result(index);
			Recipe r = ((Catalogue || StorageView) ? null : Main.recipe[index]);
			if ((r != null && r.Disabled) || !MatchCategory(item) || (FavoritesOnly && !Favorites.Contains(ItemKey(item))) || (ModFilter.Length > 0 && (item.ModItem?.Mod.Name ?? "Terraria") != ModFilter && (r == null || Owner(r) != ModFilter)))
			{
				continue;
			}
			if (terms.Length > 0 && !BookRules.Search(terms, ItemSearchText(item)))
			{
				continue;
			}
			BookStage stage = ((r != null) ? BookProgression.RecipeStage(index) : BookProgression.ItemStage(item.type));
			if (!BookProgressionRules.Matches(StageFilter, stage))
			{
				continue;
			}
			if (ClassFilter != BookClass.None && (BookItemClasses.Get(item.type) & ClassFilter) == 0)
			{
				ClassHidden++;
				continue;
			}
			StationCandidates.Add(index);
			if (stations && !StationAllowed(index))
			{
				StationHidden++;
				continue;
			}
			Filtered.Add(index);
		}
		if (!RecentView)
		{
			if (StationSort)
			{
				PrepareStationSort();
			}
			Filtered.Sort(CompareFilteredIndexes);
		}

		Page = Math.Clamp(Page, 0, Math.Max(0, (Filtered.Count - 1) / ActivePageSize));
		if (!Filtered.Contains(Selected))
		{
			ArmorFocus = 0;
			Selected = ((Filtered.Count > 0) ? Filtered[0] : (-1));
			DetailsPage = (Variant = 0);
		}
	}

	// The normalized search fields per item type, with the name they were built from: a language change rebuilds them.
	private static readonly Dictionary<int, (string Name, string Text)> ItemSearchTexts = new Dictionary<int, (string, string)>();

	private static string ItemSearchText(Item item)
	{
		string name = item.Name;
		if (!ItemSearchTexts.TryGetValue(item.type, out var cached) || cached.Name != name)
		{
			string internalName = item.ModItem?.Name ?? ItemID.Search.GetName(item.type) ?? "";
			cached = (name, BookRules.SearchText(name, ItemKey(item), internalName, ModName(item.ModItem?.Mod.Name ?? "Terraria")));
			ItemSearchTexts[item.type] = cached;
		}
		return cached.Text;
	}

	// Tabs: 0 Items, 1 Recipes, 2 Bestiary, 3 Storage, 4 Recent, 5 Tracking.
	private static void SetTabFlags(int tab)
	{
		Catalogue = tab == 0;
		BestiaryView = tab == 2;
		StorageView = tab == 3;
		RecentView = tab == 4;
		TrackingView = tab == 5;
		if (BestiaryView)
		{
			EnsureBestiary();
		}
	}

	private static void SwitchTab(int index)
	{
		int from = CurrentTab;
		// The leaving tab's filters are read before the switch: the Bestiary has its own categories.
		BookUiState.TabFilters leaving = LiveTabFilters();
		SetTabFlags(index);
		ChangeFilterTab(from, index, leaving);
		ResetCard();
		if (StorageView)
		{
			BookSources.Refresh(force: true);
			AppliedStorageVersion = BookSources.ItemsVersion;
		}
		SetTyping(value: false);
		ModMenu = (StationMenu = false);
		ArmorFocus = 0;
		Selected = -1;
		Page = (DetailsPage = (Variant = 0));
		ApplyFilters();
		AppliedRecentVersion = BookRecent.Version;
	}


	private static void SetTyping(bool value)
	{
		if (value && !Typing)
		{
			Main.clrInput();
		}
		Typing = value;
		if (value)
		{
			Main.CurrentInputTextTakerOverride = SearchOwner;
			if (!Main.blockInput)
			{
				Main.blockInput = true;
				OwnInputBlock = true;
			}
			return;
		}
		if (Main.CurrentInputTextTakerOverride == SearchOwner)
		{
			Main.CurrentInputTextTakerOverride = null;
		}
		if (OwnInputBlock)
		{
			Main.blockInput = false;
			OwnInputBlock = false;
		}
	}

	private static void ReadSearchText()
	{
		if (Typing)
		{
			PlayerInput.WritingText = true;
			Main.CurrentInputTextTakerOverride = SearchOwner;
			Main.instance.HandleIME();
			string next = Main.GetInputText(Query);
			next = next.Replace("\r", "").Replace("\n", "");
			if (next.Length > 100)
			{
				next = next.Substring(0, 100);
			}
			if (next != Query)
			{
				Query = next;
				Page = (DetailsPage = (Variant = 0));
				ApplyFilters();
			}
			if (Main.inputTextEnter || Main.inputTextEscape)
			{
				SetTyping(value: false);
			}
		}
	}

	private static void Layout()
	{
		BookGeometry geometry = BookGeometry.Create(Main.screenWidth, Main.screenHeight, Main.UIScale, ModContent.GetInstance<BookConfig>().WindowScalePercent, WindowScalePercent, HasCustomWindowPosition, WindowPositionX, WindowPositionY);
		Scale = geometry.Scale;
		Origin = new Vector2(geometry.Left, geometry.Top);
		(float, float) local = geometry.ToLocal(Main.MouseScreen.X, Main.MouseScreen.Y);
		Mouse = new Vector2(local.Item1, local.Item2);
		MouseOver = Hit(0f, 0f, 1120f, 696f);
		// The filter bar makes room for one more toggle: the Bestiary's Encountered, the station filter on Items and Recipes.
		BookGeometry.FilterInset = (BestiaryView || StationFilterTab) ? 44f : 0f;
	}

	private static bool Hit(float x, float y, float w, float h)
	{
		return new BookRect(x, y, w, h).Contains(Mouse.X, Mouse.Y);
	}

	// The book is the local player's UI: a dedicated server never loads it, nor builds its catalogue.
	public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

	public override void PostUpdateInput()
	{
		BookPlayer.GuardTriggers();
	}

	public override void UpdateUI(GameTime gameTime)
	{
		UpdateMarkerInput();
		MouseState mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
		KeyboardState keys = Keyboard.GetState();
		bool acted = (mouse.LeftButton == ButtonState.Pressed && HistoryMouse.LeftButton != ButtonState.Pressed) || (mouse.RightButton == ButtonState.Pressed && HistoryMouse.RightButton != ButtonState.Pressed) || (mouse.MiddleButton == ButtonState.Pressed && HistoryMouse.MiddleButton != ButtonState.Pressed) || mouse.ScrollWheelValue != HistoryMouse.ScrollWheelValue || keys.GetPressedKeys().Any((Keys key) => !HistoryKeys.IsKeyDown(key));
		HistoryMouse = mouse;
		HistoryKeys = keys;
		ActedUpdates = acted ? 3 : Math.Max(0, ActedUpdates - 1);
		ArrowMoved = false;
		UpdateBookUI(gameTime);
		RecordHistory(ActedUpdates > 0);
	}

	private static void UpdateBookUI(GameTime gameTime)
	{
		if (Main.gameMenu)
		{
			CloseAcquisition();
			SetTyping(value: false);
			Open = (MouseOver = false);
			return;
		}
		BookInventoryShortcut.ProcessRequests();
		ProcessHistoryNavigation();
		bool middle = Microsoft.Xna.Framework.Input.Mouse.GetState().MiddleButton == ButtonState.Pressed;
		if (((Open && Typing) & middle) && !LastMiddle && ModernRecipeBook.ToggleBook.GetAssignedKeys().Contains("Mouse3"))
		{
			BookInventoryShortcut.RequestToggle();
		}
		LastMiddle = middle;
		// The fullscreen map covers the book and the tracker: a click there is the map's.
		if (Main.mapFullscreen)
		{
			return;
		}
		HandleTrackerInput();
		if (!Open)
		{
			return;
		}
		if (Typing)
		{
			SetTyping(value: true);
			PlayerInput.WritingText = true;
		}
		Item previousStorageItem = (StorageView ? SelectedItem : null);
		int selectedType = previousStorageItem?.type ?? 0;
		int selectedPrefix = previousStorageItem?.prefix ?? 0;
		if (StorageView && AppliedStorageVersion != BookSources.ItemsVersion)
		{
			AppliedStorageVersion = BookSources.ItemsVersion;
			Selected = ((selectedType > 0) ? BookSources.Items.FindIndex((Item item) => item.type == selectedType && item.prefix == selectedPrefix) : (-1));
			ApplyFilters(clearMessage: false);
		}
		BookCrafting.Refresh();
		if (Dirty)
		{
			Rebuild();
			ApplyFilters();
		}
		if (MobRecentCategory && AppliedSeenMobsVersion != SeenMobs.Version)
		{
			ApplyFilters(clearMessage: false);
		}
		if (RecentView && AppliedRecentVersion != BookRecent.Version)
		{
			AppliedRecentVersion = BookRecent.Version;
			ApplyFilters(clearMessage: false);
		}

		Layout();
		if (BestiaryView && !AcquisitionOpen)
		{
			UpdateMobCard();
		}
		if (MouseOver)
		{
			Main.LocalPlayer.mouseInterface = true;
			PlayerInput.LockVanillaMouseScroll("ModernRecipeBook");
		}
		bool click = Main.mouseLeft && !LastLeft;
		LastLeft = Main.mouseLeft;
		bool rightClick = Main.mouseRight && !LastRight;
		LastRight = Main.mouseRight;
		if (AcquisitionOpen)
		{
			KeyboardState acquisitionKeys = Keyboard.GetState();
			BookHotkeys.Action navigation = BookHotkeys.Read(acquisitionKeys, LastKeys, typing: false, Main.blockInput, Main.drawingPlayerChat || Main.editSign || Main.editChest, help: false, menu: false, storage: false, arrows: false);
			if (navigation == BookHotkeys.Action.Back || navigation == BookHotkeys.Action.Forward)
			{
				if (navigation == BookHotkeys.Action.Back)
				{
					HistoryBack();
				}
				else
				{
					HistoryForward();
				}
				LastKeys = acquisitionKeys;
				return;
			}
			HandleAcquisitionInput(click, acquisitionKeys);
			LastKeys = acquisitionKeys;
			LastEscape = acquisitionKeys.IsKeyDown(Keys.Escape);
			return;
		}
		if (HandleUiStateInput(click))
		{
			LastKeys = Keyboard.GetState();
			LastEscape = LastKeys.IsKeyDown(Keys.Escape);
			LastWheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
			return;
		}
		KeyboardState keys = Keyboard.GetState();
		bool escape = keys.IsKeyDown(Keys.Escape);
		if ((escape && !LastEscape) || InventoryKeyPressed)
		{
			if (HelpOpen)
			{
				HelpOpen = false;
			}
			else if (Typing)
			{
				SetTyping(value: false);
			}
			else if (ModMenu)
			{
				ModMenu = false;
			}
			else if (SortMenu)
			{
				SortMenu = false;
			}
			else if (StageMenu)
			{
				StageMenu = false;
			}
			else if (StationMenu)
			{
				StationMenu = false;
			}
			else
			{
				Open = false;
				BookSources.CancelCraft();
			}
		}
		LastEscape = escape;
		if (!Open)
		{
			SetTyping(value: false);
			MouseOver = false;
			LastKeys = keys;
			return;
		}
		bool flag = (StationMenu && HandleStationMenuKeys(keys)) || HandleKeys(keys);
		LastKeys = keys;
		if (flag | escape)
		{
			return;
		}
		if (HelpOpen)
		{
			if (click && (BookGeometry.HelpClose.Contains(Mouse.X, Mouse.Y) || !BookGeometry.HelpPanel.Contains(Mouse.X, Mouse.Y)))
			{
				HelpOpen = false;
			}
			return;
		}
		// Storage: no click selects. The details panel follows the hovered cell and keeps the last one when the mouse
		// leaves the grid; it changes only when another cell is hovered, so the arrow keys still move it.
		if (StorageView && !MenuOpen)
		{
			int hoveredCell = StorageCellAt();
			if (hoveredCell >= 0)
			{
				Item hoveredItem = Result(Filtered[hoveredCell]);
				if ((hoveredItem.type, hoveredItem.prefix) != StorageHoverKey)
				{
					StorageHoverKey = (hoveredItem.type, hoveredItem.prefix);
					Selected = Filtered[hoveredCell];
				}
			}
			else if (!BookGeometry.StorageGrid.Contains(Mouse.X, Mouse.Y))
			{
				StorageHoverKey = (0, 0);
			}
		}
		int scrollWheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		int wheel = scrollWheelValue - LastWheel;
		LastWheel = scrollWheelValue;
		if (wheel != 0 && MouseOver)
		{
			int step = ((wheel <= 0) ? 1 : (-1));
			if (TrackingView)
			{
				HandleTrackingWheel(step);
			}
			else if (ModMenu)
			{
				ModOffset = Math.Clamp(ModOffset + step, 0, Math.Max(0, Mods.Count + 1 - 9));
			}
			else if (StationMenu)
			{
				StationOffset = Math.Clamp(StationOffset + step, 0, Math.Max(0, StationRowCount - StationVisibleRows));
			}
			else if (!SortMenu && !StageMenu && BookGeometry.CategoryPanel.Contains(Mouse.X, Mouse.Y))
			{
				CategoryRow = Math.Clamp(CategoryRow + step * 3, 0, MaxCategoryRow);
			}
			else if (!SortMenu && !StageMenu && BookGeometry.DetailsPanel.Contains(Mouse.X, Mouse.Y))
			{
				DetailsPage = Math.Clamp(DetailsPage + step, 0, MaxDetailsPage());
			}
			else if (!SortMenu && !StageMenu && Hit(196f, 76f, 600f, 564f))
			{
				Page = Math.Clamp(Page + step, 0, Pages - 1);
			}
		}

		if (Main.mouseRight && StorageView && !MenuOpen)
		{
			int cell = StorageCellAt();
			if (cell >= 0)
			{
				StorageRightClick(Result(Filtered[cell]), rightClick);
				return;
			}
		}

		if (!click)
		{
			return;
		}
		if (StageMenu)
		{
			for (int option = 0; option < 4; option++)
			{
				if (StageOption(option).Contains(Mouse.X, Mouse.Y))
				{
					StageFilter = (BookStageFilter)option;
					Page = (DetailsPage = (Variant = 0));
					ApplyFilters();
					break;
				}
			}
			StageMenu = false;
			return;
		}
		if (StageDropdownRect().Contains(Mouse.X, Mouse.Y))
		{
			SetTyping(value: false);
			ModMenu = (SortMenu = (StationMenu = false));
			StageMenu = true;
			return;
		}
		if (SortMenu)
		{
			for (int option = 0; option < SortOptionCount; option++)
			{
				if (BookGeometry.SortOption(option).Contains(Mouse.X, Mouse.Y))
				{
					ApplySortOption(option);
					break;
				}
			}
			SortMenu = false;
			return;
		}
		if (BookGeometry.Help.Contains(Mouse.X, Mouse.Y))
		{
			SetTyping(value: false);
			HelpOpen = true;
			return;
		}
		if (BookGeometry.HeaderClose.Contains(Mouse.X, Mouse.Y))
		{
			SetTyping(value: false);
			Open = (MouseOver = false);
			BookSources.CancelCraft();
			return;
		}
		foreach (int tab in VisibleTabs())
		{
			if (TabRect(tab).Contains(Mouse.X, Mouse.Y))
			{
				SwitchTab(tab);
				return;
			}
		}
		if (TrackingView)
		{
			HandleTrackingTabInput();
			return;
		}
		// A row ticks or unticks its station and the menu stays open for more; a click elsewhere only closes it.
		if (StationMenu)
		{
			if (StationMenuPanel.Contains(Mouse.X, Mouse.Y))
			{
				int row = StationRowAt();
				if (row >= 0)
				{
					ActivateStationRow(row);
				}
				return;
			}
			StationMenu = false;
			return;
		}
		if (ModMenu)
		{
			if (BookGeometry.ModMenuPanel.Contains(Mouse.X, Mouse.Y))
			{
				for (int j = 0; j < 9; j++)
				{
					if (BookGeometry.ModOption(j).Contains(Mouse.X, Mouse.Y))
					{
						int n2 = ModOffset + j;
						if (n2 <= Mods.Count)
						{
							ModFilter = ((n2 == 0) ? "" : Mods[n2 - 1]);
							ModMenu = false;
							Page = (DetailsPage = 0);
							ApplyFilters();
						}
						return;
					}
				}
			}
			ModMenu = false;
			return;
		}
		if (StorageView)
		{
			foreach (StorageButton button in StorageButtons)
			{
				if (StorageButtonRect(button).Contains(Mouse.X, Mouse.Y))
				{
					SetTyping(value: false);
					PressStorageButton(button);
					return;
				}
			}
		}
		if (Query.Length > 0 && BookGeometry.SearchClear.Contains(Mouse.X, Mouse.Y))
		{
			Query = "";
			SetTyping(value: false);
			Page = 0;
			ApplyFilters();
			return;
		}
		SetTyping(BookGeometry.Search.Contains(Mouse.X, Mouse.Y));
		if (Typing)
		{
			return;
		}
		if (BookGeometry.ModFilter.Contains(Mouse.X, Mouse.Y))
		{
			ModMenu = true;
			ModOffset = 0;
			return;
		}
		if (StationFilterTab && BookGeometry.StationFilter.Contains(Mouse.X, Mouse.Y))
		{
			OpenStationMenu();
			return;
		}
		if (BookGeometry.Sort.Contains(Mouse.X, Mouse.Y))
		{
			SortMenu = !RecentView && !MobRecentCategory;
			return;
		}
		if (BookGeometry.FavoritesFilter.Contains(Mouse.X, Mouse.Y))
		{
			FavoritesOnly = !FavoritesOnly;
			Page = 0;
			ApplyFilters();
			return;
		}
		if (!BestiaryView && HandleClassClick())
		{
			return;
		}
		if (BestiaryView && BookGeometry.MetFilter.Contains(Mouse.X, Mouse.Y))
		{
			MetOnly = !MetOnly;
			Page = 0;
			ApplyFilters();
			return;
		}
		for (int visible = 0; visible < CategoryRows; visible++)
		{
			int index2 = CategoryRow + visible;
			if (index2 >= Categories.Count)
			{
				break;
			}
			if (BookGeometry.CategoryCell(visible, ClassRowShown).Contains(Mouse.X, Mouse.Y))
			{
				Category = ((Category != index2) ? index2 : 0);
				Page = (DetailsPage = 0);
				ApplyFilters();
				return;
			}
		}
		if (BestiaryView && HandleMobGridClick())
		{
			return;
		}
		if (StorageView)
		{
			int cell = StorageCellAt();
			if (cell >= 0)
			{
				StorageLeftClick(Result(Filtered[cell]));
				return;
			}
			// An item on the cursor dropped onto the grid's empty part goes into nearby storage, as into an empty
			// chest slot.
			if (!Main.mouseItem.IsAir && BookGeometry.StorageGrid.Contains(Mouse.X, Mouse.Y))
			{
				BookSources.DepositCursor();
				return;
			}
		}
		for (int n3 = 0; n3 < ActivePageSize && !BestiaryView; n3++)
		{
			int index3 = Page * ActivePageSize + n3;
			if (index3 >= Filtered.Count)
			{
				break;
			}
			if (BookGeometry.GridCell(n3, GridTop).Contains(Mouse.X, Mouse.Y))
			{
				KeyboardState keyboard = Keyboard.GetState();
				if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
				{
					ToggleFavorite(Result(Filtered[index3]));
					return;
				}
				ArmorFocus = 0;
				ResetCard();
				Selected = Filtered[index3];
				// Shift+Click opens the item straight on its Used in list.
				if (!StorageView && (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)))
				{
					UsedInMode = true;
				}
				DetailsPage = (Variant = 0);
				BookCrafting.LastMessage = "";
				return;
			}
		}
		if (BookGeometry.PagePrevious.Contains(Mouse.X, Mouse.Y))
		{
			Page = Math.Max(0, Page - 1);
		}
		if (BookGeometry.PageNext.Contains(Mouse.X, Mouse.Y))
		{
			Page = Math.Min(Pages - 1, Page + 1);
		}
		if (BestiaryView)
		{
			HandleMobCardClick();
			return;
		}
		if (StorageView || SelectedItem == null || HandleCardNavClick() || HandleArmorSetClick() || HandleTrackButton())
		{
			return;
		}
		Item chosen = SelectedItem;
		Recipe materialRecipe = SelectedRecipe;
		float top = RecipeBlockTop(chosen);
		if (UsedInMode)
		{
			if (HandleUsedInClick(chosen.type, top))
			{
				return;
			}
		}
		else if (materialRecipe == null)
		{
			if (chosen != null && !chosen.IsAir && BookGeometry.SelectedItemObtain(top).Contains(Mouse.X, Mouse.Y))
			{
				OpenAcquisition(chosen.type, null);
				return;
			}
		}
		else
		{
			if (CardItemType > 0 && VariantCount > 1)
			{
				if (BookGeometry.VariantPrevious(top).Contains(Mouse.X, Mouse.Y))
				{
					Variant = Math.Max(0, Variant - 1);
					DetailsPage = 0;
					return;
				}
				if (BookGeometry.VariantNext(top).Contains(Mouse.X, Mouse.Y))
				{
					Variant = Math.Min(VariantCount - 1, Variant + 1);
					DetailsPage = 0;
					return;
				}
			}
			if (HandleBatchChoiceClick(materialRecipe))
			{
				return;
			}
			List<Detail> details = Details(materialRecipe);
			List<List<DetailSlot>> pages = DetailPages(materialRecipe, details.Count, top);
			foreach (DetailSlot slot in pages[Math.Clamp(DetailsPage, 0, pages.Count - 1)])
			{
				if (slot.Detail < 0 || !slot.Rect.Contains(Mouse.X, Mouse.Y))
				{
					continue;
				}
				Item material = details[slot.Detail].Icon;
				if (material != null && !material.IsAir)
				{
					if (BookGeometry.DetailListObtain(slot.Rect).Contains(Mouse.X, Mouse.Y))
					{
						OpenAcquisition(material.type, details[slot.Detail].SourceTypes);
					}
					else
					{
						StepCard(material.type, usedIn: false);
					}
				}
				return;
			}
			if (pages.Count > 1 && BookGeometry.DetailListPrevious.Contains(Mouse.X, Mouse.Y))
			{
				DetailsPage = Math.Max(0, DetailsPage - 1);
				return;
			}
			if (pages.Count > 1 && BookGeometry.DetailListNext.Contains(Mouse.X, Mouse.Y))
			{
				DetailsPage = Math.Min(pages.Count - 1, DetailsPage + 1);
				return;
			}
		}
		if (BookGeometry.FavoriteAction.Contains(Mouse.X, Mouse.Y))
		{
			ToggleFavorite(chosen);
			return;
		}
		if (materialRecipe != null && CraftRect.Contains(Mouse.X, Mouse.Y))
		{
			BookCrafting.TryCraft(materialRecipe, Ru, PreviewBatches(materialRecipe));
		}
	}

	private static float GridTop => StorageView ? BookGeometry.StorageGridTop : BookGeometry.GridTop;

	// The last hovered Storage cell's kind: the selection follows the mouse only when it moves to another cell.
	private static (int Type, int Prefix) StorageHoverKey;

	// The Filtered position of the Storage cell under the mouse, or -1.
	private static int StorageCellAt()
	{
		for (int n = 0; n < ActivePageSize; n++)
		{
			int index = Page * ActivePageSize + n;
			if (index >= Filtered.Count)
			{
				break;
			}
			if (BookGeometry.GridCell(n, BookGeometry.StorageGridTop).Contains(Mouse.X, Mouse.Y))
			{
				return index;
			}
		}
		return -1;
	}

	// A Storage cell clicked as a chest slot (Terraria.UI/ItemSlot.cs): Shift to the inventory (OverrideLeftClick,
	// :572-617), Ctrl to the trash when vanilla's quick trash is on (LeftClick_SellOrTrash, :973-1050), otherwise the
	// slot's left click (:663-750). Alt marks a book favorite, as in the other tabs.
	private static void StorageLeftClick(Item item)
	{
		KeyboardState keyboard = Keyboard.GetState();
		if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
		{
			ToggleFavorite(item);
		}
		else if (ItemSlot.ShiftInUse)
		{
			BookSources.TakeToInventory(item);
		}
		else if (ItemSlot.ControlInUse && BookSources.QuickTrashOn)
		{
			BookSources.Trash(item);
		}
		else
		{
			BookSources.LeftClick(item);
		}
	}

	// Right-click as on a chest slot (ItemSlot.RightClick, :1373-1419): one onto the cursor, again whenever
	// Main.stackSplit runs out while the button is held; Main shortens the delay the way it does for every slot.
	// An unstackable item comes onto the cursor once per press (vanilla equips it from a chest instead).
	private static void StorageRightClick(Item item, bool pressed)
	{
		if (item.maxStack <= 1)
		{
			if (pressed && BookSources.TakeOne(item))
			{
				SoundEngine.PlaySound(in SoundID.MenuTick);
			}
			return;
		}
		if (Main.stackSplit > 1)
		{
			return;
		}
		for (int i = 0; i <= Main.superFastStack; i++)
		{
			if (BookSources.TakeOne(item))
			{
				SoundEngine.PlaySound(in SoundID.MenuTick);
				ItemSlot.RefreshStackSplitCooldown();
			}
		}
	}

	// The item card is on screen (Items, Recipes, Recent) with an item in it.
	private static bool CardShown => !StorageView && !TrackingView && SelectedItem != null;

	// Arrow keys move the grid selection unless the player bound them to a game control.
	private static bool ArrowsFree()
	{
		if (!PlayerInput.CurrentProfile.InputModes.TryGetValue(PlayerInput.CurrentInputMode, out var mode))
		{
			return true;
		}
		return !mode.KeyStatus.Values.Any((List<string> keys) => keys.Contains("Left") || keys.Contains("Right") || keys.Contains("Up") || keys.Contains("Down"));
	}

	// Moves the grid selection by a step (±1 across, ±8 a row), turning pages as needed; the card follows.
	private static void MoveSelection(int step)
	{
		if (TrackingView || Filtered.Count == 0)
		{
			return;
		}
		int position = Filtered.IndexOf(Selected);
		int next = (position < 0) ? (Page * ActivePageSize) : Math.Clamp(position + step, 0, Filtered.Count - 1);
		next = Math.Clamp(next, 0, Filtered.Count - 1);
		ArmorFocus = 0;
		ResetCard();
		Selected = Filtered[next];
		Page = next / ActivePageSize;
		DetailsPage = (Variant = 0);
		ArrowMoved = true;
	}

	private static void ToggleFavorite(Item item)
	{
		if (item != null && !item.IsAir)
		{
			string key = ItemKey(item);
			if (!Favorites.Add(key))
			{
				Favorites.Remove(key);
			}
			ApplyFilters(clearMessage: false);
		}
	}

	private static void RefreshView()
	{
		BookSources.Refresh(force: true);
		Rebuild();
		BookCrafting.Refresh(force: true);
		ApplyFilters(clearMessage: false);
	}

	private static bool HandleKeys(KeyboardState keys)
	{
		BookHotkeys.Action action = BookHotkeys.Read(keys, LastKeys, Typing, Main.blockInput, Main.drawingPlayerChat || Main.editSign || Main.editChest, HelpOpen, MenuOpen, StorageView, ArrowsFree());
		switch (action)
		{
		case BookHotkeys.Action.None:
			return false;
		case BookHotkeys.Action.Help:
			SetTyping(value: false);
			ModMenu = (SortMenu = (StageMenu = (StationMenu = false)));
			HelpOpen = !HelpOpen;
			break;
		case BookHotkeys.Action.Search:
			if (!TrackingView)
			{
				ModMenu = (SortMenu = (StageMenu = (StationMenu = false)));
				SetTyping(value: true);
			}
			break;
		case BookHotkeys.Action.Tab1:
		case BookHotkeys.Action.Tab2:
		case BookHotkeys.Action.Tab3:
		case BookHotkeys.Action.Tab4:
		case BookHotkeys.Action.Tab5:
		case BookHotkeys.Action.Tab6:
		{
			List<int> visible = VisibleTabs();
			int position = action - BookHotkeys.Action.Tab1;
			if (position < visible.Count)
			{
				SwitchTab(visible[position]);
			}
			break;
		}
		case BookHotkeys.Action.NextTab:
		case BookHotkeys.Action.PreviousTab:
		{
			List<int> tabs = VisibleTabs();
			int current = Math.Max(0, tabs.IndexOf(CurrentTab));
			SwitchTab(tabs[(current + ((action == BookHotkeys.Action.NextTab) ? 1 : (tabs.Count - 1))) % tabs.Count]);
			break;
		}
		case BookHotkeys.Action.ResetFilters:
			Query = (ModFilter = "");
			FavoritesOnly = (MetOnly = false);
			SetExcludedStations(Array.Empty<string>());
			Category = (CategoryRow = (Page = 0));
			ApplyFilters();
			break;
		case BookHotkeys.Action.Craft:
			if (!TrackingView && !BestiaryView)
			{
				BookCrafting.TryCraft(SelectedRecipe, Ru, PreviewBatches(SelectedRecipe));
			}
			break;
		case BookHotkeys.Action.CraftMax:
			if (!TrackingView && SelectedRecipe != null)
			{
				BookCrafting.TryCraft(SelectedRecipe, Ru, Math.Max(1, CachedAvailableBatches(SelectedRecipe)));
			}
			break;
		case BookHotkeys.Action.ToggleTrack:
			if (CardShown)
			{
				ToggleTrack(wholeSet: false);
			}
			break;
		case BookHotkeys.Action.ToggleFavorite:
			if (CardShown)
			{
				ToggleFavorite(SelectedItem);
			}
			else if (CardMob != null)
			{
				ToggleMobFavorite(CardMob);
			}
			break;
		case BookHotkeys.Action.HowToGet:
			if (CardShown)
			{
				OpenAcquisition(SelectedItem.type, null);
			}
			break;
		case BookHotkeys.Action.ToggleUsedIn:
			if (CardShown)
			{
				UsedInMode = !UsedInMode;
				DetailsPage = 0;
			}
			else if (CardMob != null)
			{
				MobStatsMode = !MobStatsMode;
			}
			break;
		case BookHotkeys.Action.Stage1:
		case BookHotkeys.Action.Stage2:
		case BookHotkeys.Action.Stage3:
		case BookHotkeys.Action.Stage4:
			StageFilter = (BookStageFilter)(action - BookHotkeys.Action.Stage1);
			Page = (DetailsPage = (Variant = 0));
			ApplyFilters();
			break;
		case BookHotkeys.Action.MoveLeft:
			MoveSelection(-1);
			break;
		case BookHotkeys.Action.MoveRight:
			MoveSelection(1);
			break;
		case BookHotkeys.Action.MoveUp:
			MoveSelection(-8);
			break;
		case BookHotkeys.Action.MoveDown:
			MoveSelection(8);
			break;
		case BookHotkeys.Action.QuickStack:
			PressStorageButton(StorageButton.QuickStack);
			break;
		case BookHotkeys.Action.Restock:
			PressStorageButton(StorageButton.Restock);
			break;
		case BookHotkeys.Action.TakeSelected:
			BookSources.TakeToInventory(SelectedItem);
			break;
		case BookHotkeys.Action.Refresh:
			RefreshView();
			break;
		case BookHotkeys.Action.Back:
			HistoryBack();
			break;
		case BookHotkeys.Action.Forward:
			HistoryForward();
			break;
		case BookHotkeys.Action.PagePrevious:
			Page = Math.Max(0, Page - 1);
			break;
		case BookHotkeys.Action.PageNext:
			Page = Math.Min(Pages - 1, Page + 1);
			break;
		case BookHotkeys.Action.FirstPage:
			Page = 0;
			break;
		case BookHotkeys.Action.LastPage:
			Page = Pages - 1;
			break;
		}
		return true;
	}

	private static int MaxDetailsPage()
	{
		if (BestiaryView)
		{
			return MobMaxDetailsPage();
		}
		if (UsedInMode && SelectedItem != null)
		{
			float top = RecipeBlockTop(SelectedItem);
			int count = VisibleUsedIn(SelectedItem.type).Rows.Count;
			int size = UsedInPageSize(top, count);
			return Math.Max(0, (count - 1) / size);
		}
		Recipe r = SelectedRecipe;
		if (r == null)
		{
			return 0;
		}
		return DetailPages(r, DetailCount(r), RecipeBlockTop(SelectedItem)).Count - 1;
	}

	// Details(r) rows in order: ingredients, stations (or "By hand"), conditions.
	private static int DetailCount(Recipe r)
	{
		return r.requiredItem.Count((Item i) => !i.IsAir) + Math.Max(1, r.requiredTile.Count((int t) => t >= 0)) + r.Conditions.Count;
	}

	private readonly record struct DetailSlot(BookRect Rect, int Detail, string Header);

	// Lays the recipe rows out under section labels (Ingredients, Station, Conditions). Everything fits on
	// one page when it can; otherwise pages leave room for the pager and repeat the section label.
	private static List<List<DetailSlot>> DetailPages(Recipe r, int count, float top)
	{
		int materials = r.requiredItem.Count((Item i) => !i.IsAir);
		int realStations = r.requiredTile.Count((int t) => t >= 0);
		int stations = Math.Max(1, realStations);
		// An "A or B" row of a merged recipe takes two lines: the shown alternative, then "or B" below it.
		List<MaterialRequirement> requirements = MaterialRequirements(r);
		List<List<DetailSlot>> pages = Place((top >= BookGeometry.ArmorRecipeTop) ? BookGeometry.ArmorDetailListBottom : BookGeometry.DetailListBottom);
		if (pages.Count > 1)
		{
			pages = Place(BookGeometry.DetailListPagedBottom);
		}
		return pages;
		int Kind(int index)
		{
			if (index >= materials)
			{
				if (index >= materials + stations)
				{
					return 2;
				}
				return 1;
			}
			return 0;
		}
		string Header(int kind)
		{
			return kind switch
			{
				0 => T("Ингредиенты", "Ingredients"),
				1 => (realStations > 1) ? T("Станции", "Stations") : T("Станция", "Station"),
				_ => T("Условия", "Conditions"),
			};
		}
		List<List<DetailSlot>> Place(float bottom)
		{
			// The armor view sits lower, so its list starts 4 px closer to the batch row to keep one page.
			float listTop = top + ((top >= BookGeometry.ArmorRecipeTop) ? 76f : 80f);
			List<List<DetailSlot>> result = new List<List<DetailSlot>>
			{
				new List<DetailSlot>()
			};
			float y = listTop;
			int lastKind = -1;
			for (int i = 0; i < count; i++)
			{
				int kind = Kind(i);
				bool header = kind != lastKind;
				float height = (i < requirements.Count && requirements[i].Alternatives != null) ? BookGeometry.DetailRowTallHeight : 32f;
				float need = (header ? (((y > listTop) ? 8f : 0f) + 22f) : 0f) + height;
				if (y + need > bottom && result[^1].Count > 0)
				{
					result.Add(new List<DetailSlot>());
					y = listTop;
					header = true;
				}
				if (header)
				{
					if (y > listTop)
					{
						y += 8f;
					}
					result[^1].Add(new DetailSlot(new BookRect(812f, y, 284f, 18f), -1, Header(kind)));
					y += 22f;
				}
				result[^1].Add(new DetailSlot(new BookRect(812f, y, 284f, height), i, null));
				y += height + 4f;
				lastKind = kind;
			}
			return result;
		}
	}

	private static float RecipeBlockTop(Item chosen)
	{
		if (chosen == null || BookArmorSets.Get(chosen.type) == null)
		{
			return BookGeometry.RecipeTop;
		}
		return BookGeometry.ArmorRecipeTop;
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		InsertTrackerLayer(layers);
		if (!Open)
		{
			return;
		}
		if (BookMagicStorage.HideNativeUI)
		{
			layers.RemoveAll((GameInterfaceLayer layer) =>
			{
				string name = layer.Name;
				return (name == "MagicStorage: StorageAccess" || name == "MagicStorage: UI Panel Logic") ? true : false;
			});
		}
		int index = layers.FindIndex((GameInterfaceLayer l) => l.Name == "Vanilla: Mouse Text");
		if (index < 0)
		{
			index = layers.Count;
		}
		layers.Insert(index, new LegacyGameInterfaceLayer("ModernRecipeBook: Book", () =>
		{
			SafeDrawBook(Main.spriteBatch);
			return true;
		}, InterfaceScaleType.UI));
	}

	private static Rectangle Rect(float x, float y, float w, float h)
	{
		return new Rectangle((int)(Origin.X + x * Scale), (int)(Origin.Y + y * Scale), Math.Max(1, (int)(w * Scale)), Math.Max(1, (int)(h * Scale)));
	}

	private static void Box(SpriteBatch sb, float x, float y, float w, float h, Color color, bool border = false, float radius = 9f, Color? borderColor = null, float borderWidth = 1f)
	{
		BookSkin.Panel(sb, Rect(x, y, w, h), color * GraphicsOpacity, radius * Scale, border ? new Color?((borderColor ?? Border) * GraphicsOpacity) : ((Color?)null), borderWidth * Scale);
	}

	private static void Box(SpriteBatch sb, BookRect rect, Color color, bool border = false, float radius = 10f, Color? borderColor = null, int borderWidth = 1)
	{
		Box(sb, rect.X, rect.Y, rect.Width, rect.Height, color, border, radius, borderColor, borderWidth);
	}

	private static void Label(SpriteBatch sb, BookRect rect, string value, Color color, float size = 1f)
	{
		CenterLabel(sb, value, rect.X, rect.Y, rect.Width, rect.Height, color, size);
	}

	private static (float Width, float Height) MeasureText(string value)
	{
		Vector2 bounds = FontAssets.MouseText.Value.MeasureString(value);
		return (Width: bounds.X, Height: bounds.Y);
	}

	private static void Text(SpriteBatch sb, string text, float x, float y, Color? color = null, float size = 1f, float width = 1000f, float maxHeight = 0f)
	{
		if (maxHeight > 0f)
		{
			CenterLabel(sb, text, x, y, width, maxHeight, color ?? TextColor, size, rightAligned: false, leftAligned: true);
			return;
		}
		BookTypography.Line line = BookTypography.Layout(text, width, maxHeight, size, TextScale, MeasureText);
		Utils.DrawBorderString(sb, line.Value, Origin + new Vector2(x, y) * Scale, color ?? TextColor, line.Scale * Scale);
		float height = ((maxHeight > 0f) ? maxHeight : (MeasureText(line.Value).Height * line.Scale));
		if (line.Truncated && Hit(x, y, width, height))
		{
			MouseTip(text);
		}
	}

	private static void Button(SpriteBatch sb, float x, float y, float w, float h, string label, bool active = false, bool fitWidth = false)
	{
		SpriteBatch sb2 = sb;
		float x2 = x;
		float y2 = y;
		float w2 = w;
		float h2 = h;
		Color color;
		if (active)
		{
			color = new Color(50, 76, 59);
		}
		else
		{
			color = (Hit(x, y, w, h) ? new Color(51, 67, 55) : Raised);
		}
		Box(sb2, x2, y2, w2, h2, color, border: true, 8f, active ? new Color?(Accent) : ((Color?)null), (!active) ? 1 : 2);
		float padding = ((w <= 48f) ? 4 : 8);
		CenterLabel(sb, label, x + padding, y + 4f, w - padding * 2f, h - 8f, active ? Accent : TextColor, 0.9f, rightAligned: false, leftAligned: false, fitWidth || w <= 72f);
	}

	private static BookGlyphLayout.Bounds Ink(string value)
	{
		DynamicSpriteFont font = FontAssets.MouseText.Value;
		return BookGlyphLayout.Measure(value, font.CharacterSpacing, font.LineSpacing, (char c) =>
		{
			DynamicSpriteFont.SpriteCharacterData spriteCharacterData = (font.SpriteCharacters.TryGetValue(c, out var value2) ? value2 : font.DefaultCharacterData);
			return new BookGlyphLayout.Glyph(spriteCharacterData.Kerning.X, spriteCharacterData.Kerning.Y + spriteCharacterData.Kerning.Z, spriteCharacterData.Padding.X, spriteCharacterData.Padding.Y, spriteCharacterData.Glyph.Width, spriteCharacterData.Glyph.Height);
		});
	}

	// Tallest accent to deepest descender in both alphabets: the line box every label is centered in.
	private const string ReferenceGlyphs = "АБВЁЙДЦЩруфgjpqy0123456789";

	private static void CenterLabel(SpriteBatch sb, string label, float x, float y, float width, float height, Color color, float size = 1f, bool rightAligned = false, bool leftAligned = false, bool fitWidth = false)
	{
		label = BookRules.PlainText(label).Replace("\n", " ").Replace("\r", " ");
		if (fitWidth && Ink(label).Width > 0f)
		{
			size = Math.Min(size, width / (Ink(label).Width * TextScale));
		}
		// Vertical placement and size come from shared reference glyphs, so labels in one row share a baseline
		// whatever letters they contain ("Items" and "Recipes" used to sit at different heights).
		BookGlyphLayout.Bounds reference = Ink(ReferenceGlyphs);
		BookTypography.Line line = BookTypography.FixedLine(label, width, height, size, TextScale, (string value) =>
		{
			BookGlyphLayout.Bounds bounds = Ink(value);
			return (Width: bounds.Width, Height: bounds.Height);
		}, reference.Y, reference.Height);
		BookGlyphLayout.Bounds ink = Ink(line.Value);
		float inkWidth = ink.Width * line.Scale;
		float left = rightAligned ? (width - inkWidth) : (leftAligned ? 0f : Math.Max(0f, (width - inkWidth) / 2f));
		Vector2 pos = Origin + new Vector2(x + left - ink.X * line.Scale, y + line.Y) * Scale;
		DrawInkLine(sb, line.Value, pos, color, line.Scale);
		if (line.Truncated && Hit(x, y, width, height))
		{
			MouseTip(label);
		}
	}

	private static void FixedLabel(SpriteBatch sb, string label, float x, float y, Color? color = null, float size = 1f, float width = 1000f, float height = 32f)
	{
		label = BookRules.PlainText(label).Replace("\n", " ").Replace("\r", " ");
		BookGlyphLayout.Bounds reference = Ink(ReferenceGlyphs);
		BookTypography.Line line = BookTypography.FixedLine(label, width, height, size, TextScale, (string value) =>
		{
			BookGlyphLayout.Bounds bounds = Ink(value);
			return (Width: bounds.Width, Height: bounds.Height);
		}, reference.Y, reference.Height);
		BookGlyphLayout.Bounds ink = Ink(line.Value);
		DrawInkLine(sb, line.Value, Origin + new Vector2(x - ink.X * line.Scale, y + line.Y) * Scale, color ?? TextColor, line.Scale);
		if (line.Truncated && Hit(x, y, width, height))
		{
			MouseTip(label);
		}
	}

	private static void DrawInkLine(SpriteBatch sb, string value, Vector2 pos, Color color, float size)
	{
		DynamicSpriteFont font = FontAssets.MouseText.Value;
		float scale = size * Scale;
		float shadow = Math.Max(0.5f, Scale);
		Vector2[] array = new Vector2[4]
		{
			new Vector2(0f - shadow, 0f),
			new Vector2(shadow, 0f),
			new Vector2(0f, 0f - shadow),
			new Vector2(0f, shadow)
		};
		foreach (Vector2 offset in array)
		{
			sb.DrawString(font, value, pos + offset, Color.Black * ModalOpacity, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}
		sb.DrawString(font, value, pos, color * ModalOpacity, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
	}

	private static void DrawTextPair(SpriteBatch sb, string name, string subtitle, float x, float y, float width, float height, Color titleColor, Color subColor)
	{
		BookTypography.Line title = BookTypography.Layout(BookRules.PlainText(name), width, 24f, 1f, TextScale, MeasureInk);
		BookTypography.Line sub = BookTypography.Layout(BookRules.PlainText(subtitle), width, 20f, 0.8f, TextScale, MeasureInk);
		BookGlyphLayout.Bounds titleInk = Ink(title.Value);
		BookGlyphLayout.Bounds subInk = Ink(sub.Value);
		float titleHeight = titleInk.Height * title.Scale;
		float subHeight = subInk.Height * sub.Scale;
		float top = y + BookTypography.StackTop(height, titleHeight, subHeight, 8f);
		DrawInkLine(sb, title.Value, Origin + new Vector2(x - titleInk.X * title.Scale, top - titleInk.Y * title.Scale) * Scale, titleColor, title.Scale);
		DrawInkLine(sb, sub.Value, Origin + new Vector2(x - subInk.X * sub.Scale, top + titleHeight + 8f - subInk.Y * sub.Scale) * Scale, subColor, sub.Scale);
		if ((title.Truncated || sub.Truncated) && Hit(x, y, width, height))
		{
			MouseTip(name + "\n" + subtitle);
		}
		static (float Width, float Height) MeasureInk(string value)
		{
			BookGlyphLayout.Bounds b = Ink(value);
			return (Width: b.Width, Height: b.Height);
		}
	}

	private static void Button(SpriteBatch sb, BookRect rect, string label, bool active = false, bool fitWidth = false)
	{
		Button(sb, rect.X, rect.Y, rect.Width, rect.Height, label, active, fitWidth);
	}

	private static readonly Color HoverFill = new Color(51, 67, 55);

	private static readonly Color SelectedFill = new Color(50, 76, 59);

	private static readonly Color FieldFill = new Color(19, 28, 22);

	private static readonly Color Good = new Color(150, 220, 160);

	private static readonly Color Bad = new Color(238, 151, 126);

	private static readonly Color Caution = new Color(223, 185, 139);

	private static bool MenuOpen => ModMenu || SortMenu || StageMenu || StationMenu;

	private static float TextWidth(string value, float size)
	{
		return Ink(BookRules.PlainText(value)).Width * size * Math.Clamp(TextScale, 0.9f, 1.15f);
	}

	// Thin vector strokes for icons (chevrons, crosses, plus) so every glyph shares one weight.
	private static void Line(SpriteBatch sb, float x1, float y1, float x2, float y2, Color color, float thickness = 2f)
	{
		Vector2 a = Origin + new Vector2(x1, y1) * Scale;
		Vector2 b = Origin + new Vector2(x2, y2) * Scale;
		Vector2 delta = b - a;
		float length = delta.Length();
		if (!(length <= 0f))
		{
			sb.Draw(TextureAssets.MagicPixel.Value, a, new Rectangle(0, 0, 1, 1), color * GraphicsOpacity, (float)Math.Atan2(delta.Y, delta.X), new Vector2(0f, 0.5f), new Vector2(length, Math.Max(1f, thickness * Scale)), SpriteEffects.None, 0f);
		}
	}

	// direction: 0 down, 1 up, 2 left, 3 right
	private static void Chevron(SpriteBatch sb, float cx, float cy, float size, int direction, Color color)
	{
		float h = size / 2f;
		switch (direction)
		{
		case 0:
			Line(sb, cx - size, cy - h, cx + 0.5f, cy + h + 0.5f, color);
			Line(sb, cx - 0.5f, cy + h + 0.5f, cx + size, cy - h, color);
			break;
		case 1:
			Line(sb, cx - size, cy + h, cx + 0.5f, cy - h - 0.5f, color);
			Line(sb, cx - 0.5f, cy - h - 0.5f, cx + size, cy + h, color);
			break;
		case 2:
			Line(sb, cx + h, cy - size, cx - h - 0.5f, cy + 0.5f, color);
			Line(sb, cx - h - 0.5f, cy - 0.5f, cx + h, cy + size, color);
			break;
		default:
			Line(sb, cx - h, cy - size, cx + h + 0.5f, cy + 0.5f, color);
			Line(sb, cx + h + 0.5f, cy - 0.5f, cx - h, cy + size, color);
			break;
		}
	}

	private static void Cross(SpriteBatch sb, float cx, float cy, float size, Color color)
	{
		Line(sb, cx - size, cy - size, cx + size, cy + size, color);
		Line(sb, cx - size, cy + size, cx + size, cy - size, color);
	}

	private static void Plus(SpriteBatch sb, float cx, float cy, float size, Color color)
	{
		Line(sb, cx - size, cy, cx + size, cy, color);
		Line(sb, cx, cy - size, cx, cy + size, color);
	}

	// Search glyph: an octagon lens and a handle.
	private static void Magnifier(SpriteBatch sb, float cx, float cy, Color color)
	{
		float x = cx - 1.5f;
		float y = cy - 1.5f;
		for (int i = 0; i < 8; i++)
		{
			double a = Math.PI / 4.0 * i + Math.PI / 8.0;
			double b = Math.PI / 4.0 * (i + 1) + Math.PI / 8.0;
			Line(sb, x + (float)Math.Cos(a) * 5f, y + (float)Math.Sin(a) * 5f, x + (float)Math.Cos(b) * 5f, y + (float)Math.Sin(b) * 5f, color);
		}
		Line(sb, x + 3.5f, y + 3.5f, x + 7.5f, y + 7.5f, color);
	}

	// Vanilla's defense shield followed by the number, as in the inventory.
	private static void DefenseBadge(SpriteBatch sb, float centerX, float y, int defense, Color color)
	{
		string value = defense.ToString();
		float width = TextWidth(value, 0.8f) + 4f;
		float left = centerX - (16f + 4f + width) / 2f;
		Texture2D shield = TextureAssets.Extra[58].Value;
		float scale = 16f / Math.Max(shield.Width, shield.Height);
		sb.Draw(shield, Origin + new Vector2(left + 8f, y + 8f) * Scale, null, Color.White * GraphicsOpacity, 0f, shield.Size() / 2f, scale * Scale, SpriteEffects.None, 0f);
		CenterLabel(sb, value, left + 20f, y, width, 16f, color, 0.8f, rightAligned: false, leftAligned: true);
	}

	// Favorite marker: the Fallen Star icon, dimmed when off.
	private static void Star(SpriteBatch sb, float x, float y, float size, bool on)
	{
		if (ContentSamples.ItemsByType.TryGetValue(75, out var star))
		{
			Main.instance.LoadItem(75);
			Texture2D texture = TextureAssets.Item[75].Value;
			Rectangle frame = Main.itemAnimations[75]?.GetFrame(texture) ?? texture.Bounds;
			float s = Math.Min(size / (float)Math.Max(1, frame.Width), size / (float)Math.Max(1, frame.Height));
			sb.Draw(texture, Origin + new Vector2(x + size / 2f, y + size / 2f) * Scale, frame, on ? Color.White : (Color.White * 0.3f), 0f, frame.Size() / 2f, s * Scale, SpriteEffects.None, 0f);
		}
	}

	// Square icon button frame (hover brighter, selected Accent, disabled flat). Returns hover.
	private static bool IconButtonFrame(SpriteBatch sb, BookRect rect, bool active = false, bool enabled = true)
	{
		bool hover = enabled && !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
		Color fill = ((active || (hover && Main.mouseLeft)) ? SelectedFill : (hover ? HoverFill : (enabled ? Raised : Surface)));
		Box(sb, rect, fill, border: true, 8f, active ? new Color?(Accent) : null, (!active) ? 1 : 2);
		return hover;
	}

	// Primary: Accent fill with dark text. Secondary: outlined. Disabled: flat and muted, no hover.
	private static void ActionButton(SpriteBatch sb, BookRect rect, string label, bool primary, bool enabled = true)
	{
		bool hover = enabled && !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
		if (primary && enabled)
		{
			Color fill = (hover ? (Main.mouseLeft ? new Color(158, 198, 171) : new Color(204, 236, 214)) : Accent);
			Box(sb, rect, fill, border: false, 8f);
			DrawPlainLabel(sb, label, rect, Panel, 0.9f);
			return;
		}
		Box(sb, rect, enabled ? (hover ? (Main.mouseLeft ? SelectedFill : HoverFill) : Raised) : Surface, border: true, 8f);
		CenterLabel(sb, label, rect.X + 8f, rect.Y + 4f, rect.Width - 16f, rect.Height - 8f, enabled ? TextColor : Muted, 0.9f);
	}

	// Centered text without the black outline, for dark text on a light fill.
	private static void DrawPlainLabel(SpriteBatch sb, string label, BookRect rect, Color color, float size)
	{
		label = BookRules.PlainText(label);
		BookTypography.Line line = BookTypography.Layout(label, rect.Width - 16f, rect.Height - 8f, size, TextScale, (string value) =>
		{
			BookGlyphLayout.Bounds bounds = Ink(value);
			return (Width: bounds.Width, Height: bounds.Height);
		}, centered: true);
		BookGlyphLayout.Bounds ink = Ink(line.Value);
		Vector2 pos = Origin + new Vector2(rect.X + 8f + line.X - ink.X * line.Scale, rect.Y + 4f + line.Y - ink.Y * line.Scale) * Scale;
		sb.DrawString(FontAssets.MouseText.Value, line.Value, pos, color * GraphicsOpacity, 0f, Vector2.Zero, line.Scale * Scale, SpriteEffects.None, 0f);
	}

	// Segmented control: all options visible, the selected one highlighted. Natural widths fit each label.
	private static BookRect[] SegmentRects(BookRect bounds, string[] labels, float size, bool natural, bool alignRight = false)
	{
		float[] widths = (natural ? labels.Select((string label) => TextWidth(label, size) + 24f).ToArray() : Enumerable.Repeat(bounds.Width / (float)labels.Length, labels.Length).ToArray());
		float x = (alignRight ? (bounds.X + bounds.Width - widths.Sum()) : bounds.X);
		BookRect[] rects = new BookRect[labels.Length];
		for (int i = 0; i < labels.Length; i++)
		{
			rects[i] = new BookRect(x, bounds.Y, widths[i], bounds.Height);
			x += widths[i];
		}
		return rects;
	}

	private static int SegmentAt(BookRect[] rects)
	{
		for (int i = 0; i < rects.Length; i++)
		{
			if (rects[i].Contains(Mouse.X, Mouse.Y))
			{
				return i;
			}
		}
		return -1;
	}

	private static void Segmented(SpriteBatch sb, BookRect[] rects, string[] labels, int selected, float size, bool enabled = true)
	{
		BookRect first = rects[0];
		BookRect last = rects[^1];
		Box(sb, first.X, first.Y, last.X + last.Width - first.X, first.Height, FieldFill, border: true, 8f);
		for (int i = 0; i < rects.Length; i++)
		{
			BookRect r = rects[i];
			bool on = i == selected;
			bool hover = enabled && !on && !MenuOpen && r.Contains(Mouse.X, Mouse.Y);
			if (on)
			{
				Box(sb, r.X + 2f, r.Y + 2f, r.Width - 4f, r.Height - 4f, SelectedFill, border: true, 6f, Accent);
			}
			else if (hover)
			{
				Box(sb, r.X + 2f, r.Y + 2f, r.Width - 4f, r.Height - 4f, HoverFill, border: false, 6f);
			}
			else if (i > 0 && i - 1 != selected)
			{
				Box(sb, r.X, r.Y + 8f, 1f, r.Height - 16f, Border, border: false, 0f);
			}
			CenterLabel(sb, labels[i], r.X + 6f, r.Y + 2f, r.Width - 12f, r.Height - 4f, on ? Accent : (enabled ? TextColor : Muted), size);
		}
	}

	// Dropdown (closed state): current value and a chevron; the list opens inside the book.
	// highlight marks a filter that is narrowing the list: its value is drawn in Accent.
	private static void Dropdown(SpriteBatch sb, BookRect rect, string value, bool open, bool enabled = true, bool highlight = false)
	{
		bool hover = enabled && !open && !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
		Box(sb, rect, hover ? HoverFill : (enabled ? Raised : Surface), border: true, 8f, open ? new Color?(Accent) : null, (!open) ? 1 : 2);
		CenterLabel(sb, value, rect.X + 12f, rect.Y + 4f, rect.Width - 40f, rect.Height - 8f, (!enabled) ? Muted : (highlight ? Accent : TextColor), 0.85f, rightAligned: false, leftAligned: true);
		if (enabled)
		{
			Chevron(sb, rect.X + rect.Width - 18f, rect.Y + rect.Height / 2f, 4f, open ? 1 : 0, Muted);
		}
	}

	private static void SectionLabel(SpriteBatch sb, string text, float x, float y, float width)
	{
		CenterLabel(sb, text.ToUpperInvariant(), x, y, width, 18f, Muted, 0.8f, rightAligned: false, leftAligned: true);
	}

	// Empty state: one title line and one hint, centered in the area.
	private static void EmptyState(SpriteBatch sb, BookRect area, string title, string hint)
	{
		float middle = area.Y + area.Height / 2f;
		float titleY = ((hint.Length > 0) ? (middle - 30f) : (middle - 14f));
		CenterLabel(sb, title, area.X + 16f, titleY, area.Width - 32f, 28f, TextColor, 1f);
		if (hint.Length > 0)
		{
			CenterLabel(sb, hint, area.X + 16f, middle + 4f, area.Width - 32f, 24f, Muted, 0.85f);
		}
	}

	private static void Icon(SpriteBatch sb, Item item, float x, float y, float size, bool tooltip = true)
	{
		if (item != null && !item.IsAir && item.type < TextureAssets.Item.Length)
		{
			Main.instance.LoadItem(item.type);
			Texture2D texture = TextureAssets.Item[item.type].Value;
			Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Bounds;
			float s = Math.Min(size / (float)Math.Max(1, frame.Width), size / (float)Math.Max(1, frame.Height));
			sb.Draw(texture, Origin + new Vector2(x + size / 2f, y + size / 2f) * Scale, frame, Color.White * ModalOpacity, 0f, frame.Size() / 2f, s * Scale, SpriteEffects.None, 0f);
			if (Hit(x, y, size, size))
			{
				BookInventoryShortcut.TryOpenIcon(item);
			}
			if (tooltip && Hit(x, y, size, size))
			{
				Main.HoverItem = item.Clone();
				Main.hoverItemName = item.Name;
			}
		}
	}

	private static string GroupName(Recipe recipe, Item ingredient, out RecipeGroup group)
	{
		group = null;
		foreach (int id in recipe.acceptedGroups)
		{
			if (RecipeGroup.recipeGroups.TryGetValue(id, out var g) && g.ValidItems.Contains(ingredient.type))
			{
				group = g;
				return g.GetText();
			}
		}
		return ingredient.Name;
	}

	// Every counted source (BookSources): a dictionary read, no allocation.
	private static int Owned(HashSet<int> acceptedTypes)
	{
		return BookSources.Quantity(acceptedTypes);
	}

	private static string Count(long value)
	{
		return value.ToString("N0", CultureInfo.CurrentCulture);
	}

	private static List<MaterialRequirement> MaterialRequirements(Recipe recipe)
	{
		List<(Item, RecipeGroup, string, HashSet<int>)> raw = new List<(Item, RecipeGroup, string, HashSet<int>)>();
		int[] alternatives = BookRecipeVariants.Alternatives(recipe.RecipeIndex, out int alternativeSlot);
		int alternativeRow = -1;
		for (int slot = 0; slot < recipe.requiredItem.Count; slot++)
		{
			Item ingredient = recipe.requiredItem[slot];
			if (ingredient.IsAir)
			{
				continue;
			}
			string name = GroupName(recipe, ingredient, out var group);
			if (slot == alternativeSlot)
			{
				alternativeRow = raw.Count;
				name = AlternativesName(alternatives, ingredient.type);
			}
			HashSet<int> accepted = new HashSet<int> { ingredient.type };
			foreach (int groupId in recipe.acceptedGroups)
			{
				if (RecipeGroup.recipeGroups.TryGetValue(groupId, out var alternative) && alternative.ValidItems.Contains(ingredient.type))
				{
					accepted.UnionWith(alternative.ValidItems);
				}
			}
			raw.Add((ingredient, group, name, accepted));
		}
		bool[] overlap = BookMaterialRules.FindOverlappingRequirements(raw.Select(((Item Ingredient, RecipeGroup Group, string Name, HashSet<int> Types) x) => x.Types).ToList());
		List<MaterialRequirement> result = new List<MaterialRequirement>(raw.Count);
		for (int i = 0; i < raw.Count; i++)
		{
			result.Add(new MaterialRequirement(raw[i].Item1, raw[i].Item2, raw[i].Item3, raw[i].Item4, overlap[i], (i == alternativeRow) ? Ordered(alternatives, raw[i].Item1.type) : null));
		}
		return result;
	}

	private static int PreviewBatchCount(Recipe recipe)
	{
		return BookMaterialRules.NormalizeBatches(PreviewBatches(recipe));
	}

	private static BookMaterialRules.Assessment Assess(MaterialRequirement requirement, int batches)
	{
		return BookMaterialRules.Evaluate(requirement.Ingredient.stack, batches, Owned(requirement.ItemTypes), BookSources.IsInfinite(requirement.ItemTypes), requirement.Overlapping);
	}

	private static string MaterialSub(BookMaterialRules.Assessment assessment)
	{
		return BookMaterialRules.Ratio(assessment);
	}

	private static string MaterialTooltip(MaterialRequirement requirement, BookMaterialRules.Assessment assessment, int batches)
	{
		string have = (assessment.Unknown ? "?" : (assessment.Infinite ? "∞" : Count(assessment.Available)));
		string where = (requirement.ItemTypes.Count == 1) ? BookSources.Breakdown(requirement.Ingredient.type) : "";
		return T("Есть ", "Have ") + have + T(", нужно ", ", need ") + Count(assessment.Required) + ((batches > 1) ? (" (×" + batches + ")") : "") + (assessment.Overlapping ? T(" · учтено и в другой строке", " · also counted in another row") : "") + ((where.Length > 0) ? ("\n" + where) : "");
	}

	private static List<Detail> Details(Recipe r)
	{
		List<Detail> list = new List<Detail>();
		int batches = PreviewBatchCount(r);
		foreach (MaterialRequirement requirement in MaterialRequirements(r))
		{
			BookMaterialRules.Assessment assessment = Assess(requirement, batches);
			Color color;
			if (assessment.Unknown)
			{
				color = Muted;
			}
			else if (assessment.HasDeficit)
			{
				color = Bad;
			}
			else
			{
				color = (assessment.Overlapping ? Caution : Good);
			}
			if (requirement.Alternatives != null)
			{
				// "A or B": the counts are the shown alternative's (the one you have most of); the tooltip lists each.
				list.Add(new Detail(requirement.Ingredient, requirement.Name, MaterialSub(assessment), color, color, AlternativesTooltip(requirement, batches), requirement.Alternatives, Alternatives: true));
				continue;
			}
			list.Add(new Detail(requirement.Ingredient, requirement.Name, MaterialSub(assessment), color, color, MaterialTooltip(requirement, assessment, batches), requirement.ItemTypes.OrderBy((int x) => x).ToArray()));
		}
		foreach (int tile in r.requiredTile.Where((int t) => t >= 0))
		{
			StationItems.TryGetValue(tile, out var station);
			string name = StationName(tile);
			list.Add(new Detail(station, name, T("Станция", "Station"), Muted, Muted, ""));
		}
		if (!r.requiredTile.Any((int t) => t >= 0))
		{
			list.Add(new Detail(null, T("Вручную", "By hand"), T("Станция не нужна", "No station needed"), Muted, Muted, ""));
		}
		foreach (Condition c in r.Conditions)
		{
			list.Add(new Detail(null, ConditionName(c), T("Условие", "Condition"), Caution, Muted, ""));
		}
		return list;
	}

	private static MaterialPreview PreviewMaterials(Recipe recipe)
	{
		int batches = PreviewBatchCount(recipe);
		List<(MaterialRequirement, BookMaterialRules.Assessment)> assessed = (from requirement in MaterialRequirements(recipe)
			select (Requirement: requirement, Assessment: Assess(requirement, batches))).ToList();
		(MaterialRequirement, BookMaterialRules.Assessment) focus = assessed.FirstOrDefault(((MaterialRequirement Requirement, BookMaterialRules.Assessment Assessment) x) => x.Assessment.HasDeficit);
		if (focus.Item1 == null)
		{
			focus = assessed.FirstOrDefault();
		}
		string summary = ((focus.Item1 == null) ? T("Без материалов", "No materials") : (MaterialSub(focus.Item2) + " " + focus.Item1.Name));
		Color color;
		if (assessed.Any(((MaterialRequirement Requirement, BookMaterialRules.Assessment Assessment) x) => x.Assessment.HasDeficit))
		{
			color = Bad;
		}
		else
		{
			color = (assessed.Any(((MaterialRequirement Requirement, BookMaterialRules.Assessment Assessment) x) => x.Assessment.Unknown || x.Assessment.Overlapping) ? Muted : Good);
		}
		string materials = string.Join(", ", assessed.Take(3).Select(((MaterialRequirement, BookMaterialRules.Assessment) x) => x.Item1.Name + " " + MaterialSub(x.Item2))) + ((assessed.Count > 3) ? ", ..." : "");
		return new MaterialPreview(summary, color, (assessed.Count == 0) ? T("Без материалов", "No materials") : materials);
	}

	// The Storage tab's toolbar: vanilla's chest buttons in vanilla's order, minus Loot All. The tab is one big chest:
	// what goes in is placed by BookNearbyServer.Deposit's rule.
	private enum StorageButton
	{
		DepositAll,
		QuickStack,
		Restock
	}

	private static readonly StorageButton[] StorageButtons = new StorageButton[3]
	{
		StorageButton.DepositAll,
		StorageButton.QuickStack,
		StorageButton.Restock
	};

	private static BookRect StorageButtonRect(StorageButton button)
	{
		return BookGeometry.StorageToolbarButton(Array.IndexOf(StorageButtons, button), StorageButtons.Length);
	}

	private static bool StorageButtonEnabled(StorageButton button)
	{
		return button switch
		{
			StorageButton.DepositAll => BookSources.CanDeposit,
			StorageButton.QuickStack => !Main.LocalPlayer.HasLockedInventory(),
			_ => BookSources.Enabled && !BookSources.Busy,
		};
	}

	private static void PressStorageButton(StorageButton button)
	{
		if (!StorageButtonEnabled(button))
		{
			return;
		}
		switch (button)
		{
		case StorageButton.DepositAll:
			BookSources.DepositAll();
			break;
		case StorageButton.QuickStack:
			BookSources.QuickStack();
			break;
		default:
			BookSources.Restock();
			break;
		}
	}

	private static string StorageButtonLabel(StorageButton button)
	{
		return button switch
		{
			StorageButton.DepositAll => T("Сложить всё", "Deposit All"),
			StorageButton.QuickStack => T("Быстрый стак", "Quick Stack"),
			_ => T("Пополнить", "Restock"),
		};
	}

	private static string StorageButtonTip(StorageButton button)
	{
		switch (button)
		{
		case StorageButton.QuickStack:
			return BookMagicStorage.Installed ? T("Сложить в сундуки и хранилища рядом · Ctrl+Q", "Quick stack to nearby chests and storages · Ctrl+Q") : T("Сложить в сундуки рядом · Ctrl+Q", "Quick stack to nearby chests · Ctrl+Q");
		case StorageButton.Restock:
			return BookSources.Enabled ? T("Дополнить стопки в инвентаре из того, что рядом · Ctrl+R", "Top up inventory stacks from nearby · Ctrl+R") : StorageOffTitle;
		}
		if (!BookSources.Enabled)
		{
			return StorageOffTitle;
		}
		if (!BookSources.CanDeposit)
		{
			return T("Сложить в хранилища рядом", "Deposit to nearby storage") + "\n" + T("Рядом некуда складывать", "Nothing nearby to deposit into");
		}
		return T("Сложить в хранилища рядом, кроме панели и избранного", "Deposit to nearby storage, except hotbar and favorites") + "\n" + (BookMagicStorage.Installed ? T("Сначала к таким же, потом Magic Storage, потом сундук", "Onto matching stacks first, then Magic Storage, then a free chest") : T("Сначала к таким же, потом в свободный сундук", "Onto matching stacks first, then a free chest"));
	}

	private static void DrawStorageToolbar(SpriteBatch sb)
	{
		Box(sb, BookGeometry.StorageToolbar, Surface, border: true, 10f);
		foreach (StorageButton button in StorageButtons)
		{
			CardButton(sb, StorageButtonRect(button), StorageButtonLabel(button), StorageButtonEnabled(button));
		}
		if (ModMenu)
		{
			return;
		}
		foreach (StorageButton button in StorageButtons)
		{
			if (StorageButtonRect(button).Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(StorageButtonTip(button));
			}
		}
	}

	private static string StorageOffTitle => BookSources.PullsOff ? T("Недоступно на этом сервере", "Not available on this server") : T("Хранилища рядом выключены", "Nearby storage is off");

	private static string StorageOffHint => BookSources.PullsOff ? T("Сервер сам хранит инвентари", "The server keeps inventories itself") : ((Main.netMode == NetmodeID.MultiplayerClient) ? T("Хост выключил эту функцию", "The host turned it off") : T("Включите в настройках мода", "Turn it on in the mod settings"));

	// Details panel, top to bottom: label + pill (the state); what is nearby; the radius; what was not counted; the
	// selected stack and where it is; the last action. Gestures are in the stack's tooltip and in F1.
	private static void DrawStorageDetails(SpriteBatch sb)
	{
		Box(sb, BookGeometry.DetailsPanel, Surface, border: false, 12f);
		BookRect content = BookGeometry.StorageContent;
		bool on = BookSources.Enabled;
		string state;
		Color stateColor;
		if (!on)
		{
			state = T("Выключено", "Off");
			stateColor = Muted;
		}
		else if (BookSources.Syncing)
		{
			state = T("Синхронизация", "Syncing");
			stateColor = Caution;
		}
		else
		{
			state = T("Готово", "Ready");
			stateColor = Accent;
		}
		BookRect status = BookGeometry.StorageStatus;
		float pillWidth = TextWidth(state, 0.8f) + 34f;
		BookRect pill = new BookRect(status.X + status.Width - pillWidth, status.Y - 2f, pillWidth, 24f);
		Box(sb, pill, FieldFill, border: true, 12f);
		Box(sb, pill.X + 10f, pill.Y + 8f, 8f, 8f, stateColor, border: false, 4f);
		CenterLabel(sb, state, pill.X + 24f, pill.Y + 2f, pill.Width - 30f, pill.Height - 4f, stateColor, 0.8f, rightAligned: false, leftAligned: true);
		SectionLabel(sb, T("Рядом", "Nearby"), content.X, content.Y + 1f, pill.X - content.X - 8f);
		BookRect source = BookGeometry.StorageSource;
		FixedLabel(sb, on ? BookSources.Summary : StorageOffTitle, source.X, source.Y, on ? TextColor : Muted, 0.9f, source.Width, source.Height);
		BookRect hint = BookGeometry.StorageHint;
		FixedLabel(sb, on ? BookSources.RadiusLine : StorageOffHint, hint.X, hint.Y, Muted, 0.8f, hint.Width, hint.Height);
		BookRect skipped = BookGeometry.StorageSkipped;
		string skippedText = (BookMagicStorage.Installed && !BookMagicStorage.Readable) ? T("Связь с Magic Storage отключена (client.log)", "Magic Storage support is off (see client.log)") : (on ? BookSources.NotCounted : "");
		if (skippedText.Length > 0)
		{
			FixedLabel(sb, skippedText, skipped.X, skipped.Y, Caution, 0.8f, skipped.Width, skipped.Height);
		}
		Box(sb, content.X, skipped.Y + skipped.Height + 8f, content.Width, 1f, Border);
		BookRect icon = BookGeometry.StorageItemIcon;
		BookRect stack = new BookRect(icon.X, icon.Y, content.Width, icon.Height);
		Item selected = on ? SelectedItem : null;
		if (selected != null)
		{
			Box(sb, icon, Raised, border: false, 8f);
			Icon(sb, selected, icon.X + 4f, icon.Y + 4f, 40f);
			// A long name shrinks to fit before it is cut; the stack's tooltip then repeats it whole.
			float nameSize = Math.Clamp((content.Width - 60f) / Math.Max(1f, TextWidth(selected.Name, 1f)), 0.8f, 1f);
			FixedLabel(sb, selected.Name, icon.X + 60f, icon.Y + (1f - nameSize) * 12f, TextColor, nameSize, content.Width - 60f, 26f);
			FixedLabel(sb, "×" + Count(selected.stack) + " · " + ModName(selected.ModItem?.Mod.Name ?? "Terraria"), icon.X + 60f, icon.Y + 28f, Muted, 0.85f, content.Width - 60f, 20f);
			string[] rows = BookSources.BreakdownRows(selected.type);
			for (int row = 0; row < rows.Length && row < 5; row++)
			{
				BookRect where = BookGeometry.StorageWhere(row);
				FixedLabel(sb, rows[row], where.X, where.Y, TextColor, 0.85f, where.Width, where.Height);
			}
		}
		else if (on)
		{
			FixedLabel(sb, T("Предмет не выбран", "No item selected"), icon.X, icon.Y, TextColor, 1f, content.Width, 26f);
			FixedLabel(sb, T("Нажмите на предмет в сетке", "Click an item in the grid"), icon.X, icon.Y + 28f, Muted, 0.85f, content.Width, 20f);
		}
		string message = BookSources.TakingShown ? T("Забираю…", "Taking…") : BookSources.Message;
		if (message.Length > 0)
		{
			BookRect line = BookGeometry.StorageMessage;
			FixedLabel(sb, message, line.X, line.Y, TextColor, 0.85f, line.Width, line.Height);
		}
		if (MenuOpen)
		{
			return;
		}
		if (pill.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(on ? T("Перечитать · F5", "Refresh · F5") : StorageOffHint);
		}
		else if (on && source.Contains(Mouse.X, Mouse.Y))
		{
			string tip = BookSources.SummaryTip;
			if (tip.Length > 0)
			{
				MouseTip(tip);
			}
		}
		else if (on && skippedText.Length > 0 && skipped.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(BookSources.NotCountedTip);
		}
		else if (selected != null && stack.Contains(Mouse.X, Mouse.Y))
		{
			string cut = (TextWidth(selected.Name, 0.8f) > content.Width - 60f) ? (selected.Name + "\n") : "";
			MouseTip(cut + BookSources.TakeHint + "\n" + T("Ctrl+клик — в корзину · Alt+клик — в избранное", "Ctrl+Click: trash · Alt+Click: favorite"));
		}
	}

	// Mod filter list: drops down under its button, rows are plain list items (hover, selected = Accent bar).
	private static void DrawModMenu(SpriteBatch sb)
	{
		BookRect panel = BookGeometry.ModMenuPanel;
		if (panel.Contains(Mouse.X, Mouse.Y))
		{
			PendingTip = null;
			Main.HoverItem = new Item();
			Main.hoverItemName = "";
		}
		float height = 16f + 32f * (float)Math.Min(9, Mods.Count + 1 - ModOffset);
		Box(sb, panel.X, panel.Y + 4f, panel.Width, height, new Color(0, 0, 0, 110), border: false, 12f);
		Box(sb, panel.X, panel.Y, panel.Width, height, Panel, border: true, 12f);
		for (int k = 0; k < 9; k++)
		{
			int j = ModOffset + k;
			if (j > Mods.Count)
			{
				break;
			}
			string mod = ((j == 0) ? "" : Mods[j - 1]);
			BookRect row = BookGeometry.ModOption(k);
			bool selected = ModFilter == mod;
			bool hover = row.Contains(Mouse.X, Mouse.Y);
			if (selected || hover)
			{
				Box(sb, row, selected ? new Color(50, 76, 59) : HoverFill, border: false, 6f);
			}
			if (selected)
			{
				Box(sb, row.X, row.Y + 8f, 3f, row.Height - 16f, Accent, border: false, 1f);
			}
			CenterLabel(sb, (j == 0) ? T("Все источники", "All sources") : ModName(mod), row.X + 12f, row.Y + 4f, row.Width - 20f, row.Height - 8f, selected ? Accent : TextColor, 0.95f, rightAligned: false, leftAligned: true);
		}
		int rows = Mods.Count + 1;
		if (rows > 9)
		{
			float track = 9f * 32f;
			float thumb = Math.Max(24f, track * 9f / (float)rows);
			float x = panel.X + panel.Width - 10f;
			Box(sb, x, panel.Y + 8f, 3f, track, Raised, border: false, 1f);
			Box(sb, x, panel.Y + 8f + (track - thumb) * (float)ModOffset / (float)(rows - 9), 3f, thumb, Accent, border: false, 1f);
		}
	}

	private static void Draw(SpriteBatch sb)
	{
		if (!Open || Main.gameMenu)
		{
			return;
		}
		Layout();
		ReadSearchText();
		if (MouseOver)
		{
			Main.HoverItem = new Item();
			Main.hoverItemName = "";
			Main.LocalPlayer.cursorItemIconEnabled = false;
			// The game names what is under the cursor in a later layer ("Zombie: 45/45" over the book) unless
			// the mouse is taken by an interface.
			Main.LocalPlayer.mouseInterface = true;
		}
		Box(sb, -4f, -4f, 1128f, 704f, new Color(0, 0, 0, 170), border: false, 22f);
		bool grabHint = !AcquisitionOpen && DraggingWindow;
		Box(sb, 0f, 0f, 1120f, 696f, Panel, border: true, 18f, grabHint ? Accent : Border, grabHint ? 2f : 1f);
		// Under a modal the book stays as it was, blurred and dimmed, and ignores the mouse.
		float backdrop = BookBackdrop.Advance(ModalOpen);
		if (!BookBackdrop.Covers(backdrop))
		{
			DrawBookContent(sb, ModalOpen);
		}
		DrawBackdrop(sb, backdrop);
		if (AcquisitionOpen)
		{
			DrawModal(sb, backdrop, DrawAcquisition);
		}
		else if (HelpOpen)
		{
			Main.HoverItem = new Item();
			Main.hoverItemName = "";
			DrawModal(sb, backdrop, DrawHelp);
		}
	}

	private static void DrawBookContent(SpriteBatch sb)
	{
		StorageHover = null;
		DrawHeader(sb);
		if (TrackingView)
		{
			DrawTrackingTab(sb);
		}
		else
		{
			DrawCategories(sb);
			DrawFilterBar(sb);
			if (StorageView)
			{
				DrawStorageToolbar(sb);
			}
			DrawGrid(sb);
			DrawPager(sb);
			if (StorageView)
			{
				DrawStorageDetails(sb);
			}
			else if (BestiaryView)
			{
				DrawMobCard(sb);
			}
			else
			{
				DrawDetails(sb);
			}
		}
		DrawFooter(sb);
		Button(sb, BookGeometry.Help, "?");
		CenterLabel(sb, "MRB " + ModContent.GetInstance<ModernRecipeBook>().Version, 990f, 660f, BookGeometry.HeaderClose.X + BookGeometry.HeaderClose.Width - 990f, 24f, Muted, 0.8f, rightAligned: true);
		if (BookGeometry.Help.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("F1 · Все сочетания клавиш", "F1 · All keyboard shortcuts"));
		}
		if (ModMenu)
		{
			DrawModMenu(sb);
		}
		if (SortMenu)
		{
			DrawSortMenu(sb);
		}
		if (StageMenu)
		{
			DrawStageMenu(sb);
		}
		if (StationMenu)
		{
			DrawStationMenu(sb);
		}
	}

	private static void DrawHeader(SpriteBatch sb)
	{
		for (int dot = 0; dot < 6; dot++)
		{
			Box(sb, 20 + dot % 2 * 5, 25 + dot / 2 * 6, 2f, 2f, GrabHint ? Accent : (Muted * 0.8f), border: false, 0f);
		}
		CenterLabel(sb, TitleText, 34f, 16f, 260f, 32f, TextColor, 1.1f, rightAligned: false, leftAligned: true);
		if (!MenuOpen && !DraggingWindow && HeaderGrab())
		{
			MouseTip(T("Перетащите, чтобы переместить окно", "Drag to move the window"));
		}
		BookRect stage = StageDropdownRect();
		Dropdown(sb, stage, StageLabels()[(int)StageFilter], StageMenu, enabled: true, highlight: StageFilter != BookStageFilter.All);
		if (!MenuOpen && stage.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Этап для всех вкладок · Alt+1…4", "Stage for every tab · Alt+1…4") + "\n" + T("Самый ранний этап получения; учитывается сложность мира", "Earliest stage you can get an item; world difficulty counts"));
		}
		List<int> tabs = VisibleTabs();
		BookRect firstTab = TabRect(tabs[0]);
		Box(sb, firstTab.X - 4f, BookGeometry.TabTop - 4f, BookGeometry.TabRight - firstTab.X + 8f, BookGeometry.TabHeight + 8f, new Color(22, 31, 25), border: false, 12f);
		foreach (int tab in tabs)
		{
			BookRect rect = TabRect(tab);
			bool active = tab == CurrentTab;
			bool hover = !active && !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
			if (active)
			{
				Box(sb, rect, SelectedFill, border: true, 8f, Accent, 2);
			}
			else if (hover)
			{
				Box(sb, rect, HoverFill, border: false, 8f);
			}
			CenterLabel(sb, TabLabel(tab), rect.X + 8f, rect.Y + 4f, rect.Width - 16f, rect.Height - 8f, active ? Accent : TextColor, TabTextSize);
			if (hover)
			{
				MouseTip(TabLabel(tab) + " · Ctrl+" + (tabs.IndexOf(tab) + 1));
			}
		}
		BookRect close = BookGeometry.HeaderClose;
		IconButtonFrame(sb, close);
		Cross(sb, close.X + close.Width / 2f, close.Y + close.Height / 2f, 6f, TextColor);
		if (!MenuOpen && close.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Закрыть · Esc", "Close · Esc"));
		}
		Box(sb, 8f, 64f, 1104f, 1f, Border, border: false, 0f);
	}

	private static void DrawCategories(SpriteBatch sb)
	{
		Box(sb, BookGeometry.CategoryPanel, new Color(23, 30, 26), border: false, 12f);
		if (ClassRowShown)
		{
			DrawClassRow(sb);
		}
		SectionLabel(sb, T("Категории", "Categories"), 20f, ClassRowShown ? BookGeometry.CategoryLabelTop : BookGeometry.ClassLabelTop, 164f);
		CategoryRow = Math.Clamp(CategoryRow, 0, MaxCategoryRow);
		int rows = CategoryRows;
		for (int visible = 0; visible < rows; visible++)
		{
			int index = CategoryRow + visible;
			if (index >= Categories.Count)
			{
				break;
			}
			BookCategory category = Categories[index];
			BookRect rect = BookGeometry.CategoryCell(visible, ClassRowShown);
			bool active = Category == index;
			bool hover = !active && !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
			if (active)
			{
				Box(sb, rect, SelectedFill, border: true, 8f, Accent);
			}
			else if (hover)
			{
				Box(sb, rect, HoverFill, border: false, 8f);
			}
			DrawCategoryIcon(sb, category, rect);
			CenterLabel(sb, category.Label(Ru), rect.X + 36f, rect.Y + 4f, rect.Width - 40f, rect.Height - 8f, active ? Accent : TextColor, CategoryLabelSize(category.Label(Ru), rect.Width - 40f), rightAligned: false, leftAligned: true);
		}
		if (MaxCategoryRow > 0)
		{
			float track = rows * 28;
			float thumb = Math.Max(28f, track * rows / Categories.Count);
			float top = BookGeometry.CategoryCell(0, ClassRowShown).Y;
			Box(sb, 188f, top, 3f, track, Raised, border: false, 1f);
			Box(sb, 188f, top + (track - thumb) * CategoryRow / MaxCategoryRow, 3f, thumb, Muted, border: false, 1f);
		}
	}

	// One click narrows every tab to a class; it combines with the category (Armor + Magic = magic armor).
	private static void DrawClassRow(SpriteBatch sb)
	{
		SectionLabel(sb, T("Класс", "Class"), 20f, BookGeometry.ClassLabelTop, 164f);
		BookClass[] buttons = BookItemClasses.Buttons;
		for (int i = 0; i < buttons.Length; i++)
		{
			BookRect rect = BookGeometry.ClassButton(i);
			bool active = ClassFilter == buttons[i];
			bool hover = IconButtonFrame(sb, rect, active);
			if (buttons[i] == BookClass.None)
			{
				// "All" is a plain 2×2 grid: no item reads as every class at once.
				for (int cell = 0; cell < 4; cell++)
				{
					Box(sb, rect.X + rect.Width / 2f - 7f + (float)(cell % 2 * 8), rect.Y + rect.Height / 2f - 7f + (float)(cell / 2 * 8), 6f, 6f, active ? Accent : TextColor, border: false, 1f);
				}
			}
			else if (ContentSamples.ItemsByType.TryGetValue(BookItemClasses.IconType(buttons[i]), out var icon))
			{
				Icon(sb, icon, rect.X + rect.Width / 2f - 12f, rect.Y + 4f, 24f, tooltip: false);
			}
			if (hover)
			{
				MouseTip(BookItemClasses.Label(buttons[i], Ru) + ((active && buttons[i] != BookClass.None) ? ("\n" + T("Клик ещё раз — все классы", "Click again for all classes")) : ""));
			}
		}
	}

	private static bool HandleClassClick()
	{
		BookClass[] buttons = BookItemClasses.Buttons;
		for (int i = 0; i < buttons.Length; i++)
		{
			if (BookGeometry.ClassButton(i).Contains(Mouse.X, Mouse.Y))
			{
				ClassFilter = ((ClassFilter != buttons[i]) ? buttons[i] : BookClass.None);
				Page = (DetailsPage = 0);
				ApplyFilters();
				return true;
			}
		}
		return false;
	}

	private static void DrawFilterBar(SpriteBatch sb)
	{
		BookRect search = BookGeometry.Search;
		Box(sb, search, Typing ? new Color(35, 51, 40) : FieldFill, border: true, 8f, Typing ? new Color?(Accent) : null, (!Typing) ? 1 : 2);
		Magnifier(sb, search.X + 20f, search.Y + search.Height / 2f, (Typing || Query.Length > 0) ? TextColor : Muted);
		BookRect searchText = BookGeometry.SearchText;
		string shown = ((Query.Length > 0) ? (Query + ((Typing && (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0) ? "|" : "")) : T("Имя, ID или мод", "Name, ID or mod"));
		CenterLabel(sb, shown, searchText.X, searchText.Y, searchText.Width, searchText.Height, (Query.Length > 0) ? TextColor : Muted, 0.85f, rightAligned: false, leftAligned: true);
		BookRect clear = BookGeometry.SearchClear;
		bool overClear = Query.Length > 0 && !MenuOpen && clear.Contains(Mouse.X, Mouse.Y);
		if (Query.Length > 0)
		{
			if (overClear)
			{
				Box(sb, clear, HoverFill, border: false, 6f);
				MouseTip(T("Очистить поиск", "Clear search"));
			}
			Cross(sb, clear.X + clear.Width / 2f, clear.Y + clear.Height / 2f, 4f, overClear ? TextColor : Muted);
		}
		if (!overClear && !Typing && !MenuOpen && search.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Поиск · Ctrl+F", "Search · Ctrl+F"));
		}
		Dropdown(sb, BookGeometry.ModFilter, (ModFilter.Length == 0) ? T("Все моды", "All mods") : ModName(ModFilter), ModMenu, enabled: true, highlight: ModFilter.Length > 0);
		if (!MenuOpen && BookGeometry.ModFilter.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(((ModFilter.Length == 0) ? "" : (ModName(ModFilter) + "\n")) + T("Фильтр по моду", "Filter by mod"));
		}
		bool newest = RecentView || MobRecentCategory;
		Dropdown(sb, BookGeometry.Sort, newest ? T("Новые", "Newest") : SortLabel(), SortMenu, !newest);
		if (!MenuOpen && BookGeometry.Sort.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(RecentView ? T("Недавние всегда от новых к старым", "Recent crafts are always newest first") : (MobRecentCategory ? T("Недавние мобы всегда от новых к старым", "Recently seen mobs are always newest first") : T("Порядок сортировки", "Sort order")));
		}
		BookRect favorites = BookGeometry.FavoritesFilter;
		IconButtonFrame(sb, favorites, FavoritesOnly);
		Star(sb, favorites.X + 8f, favorites.Y + 8f, 20f, FavoritesOnly);
		if (!MenuOpen && favorites.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Только избранное", "Favorites only") + "\n" + (BestiaryView ? T("Alt+клик по мобу добавляет", "Alt+Click a mob to add it") : T("Alt+клик по предмету добавляет", "Alt+Click an item to add it")));
		}
		if (BestiaryView)
		{
			DrawMetFilter(sb);
		}
		if (StationFilterTab)
		{
			DrawStationButton(sb);
		}
	}

	private static void DrawGrid(SpriteBatch sb)
	{
		if (BestiaryView)
		{
			DrawMobGrid(sb);
			return;
		}
		float top = GridTop;
		int size = ActivePageSize;
		for (int n = 0; n < size; n++)
		{
			int f = Page * size + n;
			if (f >= Filtered.Count)
			{
				break;
			}
			int num = Filtered[f];
			Item item = Result(num);
			BookRect cell = BookGeometry.GridCell(n, top);
			bool hovered = !MenuOpen && cell.Contains(Mouse.X, Mouse.Y);
			bool selected = num == Selected;
			Box(sb, cell, selected ? SelectedFill : (hovered ? HoverFill : Raised), border: true, 10f, selected ? new Color?(Accent) : null, (!selected) ? 1 : 2);
			Icon(sb, item, cell.X + 12f, cell.Y + 12f, 40f, tooltip: false);
			if (hovered)
			{
				Main.HoverItem = item.Clone();
				Main.hoverItemName = item.Name;
				if (StorageView)
				{
					StorageHover = item;
					// Vanilla's chest-slot cursors: to the inventory, to the trash.
					if (ItemSlot.ShiftInUse)
					{
						Main.cursorOverride = 8;
					}
					else if (ItemSlot.ControlInUse && BookSources.QuickTrashOn)
					{
						Main.cursorOverride = 6;
					}
				}
			}
			if ((!Catalogue || StorageView) && item.stack > 1)
			{
				// Nearby totals can pass what a cell holds: 399,960 reads "399K".
				Text(sb, (item.stack >= 100000) ? ((item.stack >= 10000000) ? (item.stack / 1000000 + "M") : (item.stack / 1000 + "K")) : item.stack.ToString(), cell.X + 4f, cell.Y + 42f, TextColor, 0.8f, 56f, 20f);
			}
			if (Favorites.Contains(ItemKey(item)))
			{
				Star(sb, cell.X + 46f, cell.Y + 4f, 14f, on: true);
			}
		}
		if (Filtered.Count > 0)
		{
			return;
		}
		BookRect area = new BookRect(212f, top, 568f, 596f - top);
		if (ClassHidden > 0)
		{
			EmptyState(sb, area, T("Скрыто фильтром класса", "Hidden by the class filter"), T("Нажмите «Все классы» над категориями", "Pick All classes above the categories"));
		}
		else if (StationHidden > 0)
		{
			EmptyState(sb, area, T("Скрыто фильтром станций", "Hidden by the station filter"), T("Откройте фильтр станций и выберите «Показать все»", "Open the station filter and pick Show all"));
		}
		else if (StorageView)
		{
			// The state itself lives in the details panel; the empty grid only says what to do.
			if (!BookSources.Enabled)
			{
				EmptyState(sb, area, StorageOffTitle, StorageOffHint);
			}
			else if (BookSources.Syncing)
			{
				EmptyState(sb, area, T("Синхронизация…", "Syncing…"), "");
			}
			else if (BookSources.Items.Count > 0)
			{
				EmptyState(sb, area, T("Ничего не найдено", "Nothing found"), T("Ctrl+Backspace сбрасывает фильтры", "Ctrl+Backspace resets filters"));
			}
			else if (BookSources.AnySource)
			{
				EmptyState(sb, area, T("Рядом всё пусто", "Nearby containers are empty"), "");
			}
			else
			{
				int radius = BookNearbyConfig.Radius;
				EmptyState(sb, area, T("Рядом ничего нет", "Nothing nearby"), Ru ? ("Здесь появятся сундуки и хранилища в радиусе " + radius + " " + BookSourceRules.Plural(radius, "блока", "блоков", "блоков")) : ("Chests and storages within " + radius + " tiles show up here"));
			}
		}
		else if (RecentView)
		{
			if (Main.LocalPlayer.GetModPlayer<BookPlayer>().RecentRecipes.Count == 0)
			{
				EmptyState(sb, area, T("Пока нет крафтов", "No crafts yet"), T("Здесь появится то, что вы создадите", "Items you craft will appear here"));
			}
			else if (BookRecent.ResolveRecipeIndexes().Count == 0)
			{
				EmptyState(sb, area, T("Рецепты из истории не загружены", "Recipes from history aren't loaded"), T("Включите моды, которые их добавляют", "Enable the mods that add them"));
			}
			else
			{
				EmptyState(sb, area, T("Ничего не найдено", "Nothing found"), T("Ctrl+Backspace сбрасывает фильтры", "Ctrl+Backspace resets filters"));
			}
		}
		else
		{
			EmptyState(sb, area, T("Ничего не найдено", "Nothing found"), T("Ctrl+Backspace сбрасывает фильтры", "Ctrl+Backspace resets filters"));
		}
	}

	private static void DrawPager(SpriteBatch sb)
	{
		BookRect previous = BookGeometry.PagePrevious;
		BookRect next = BookGeometry.PageNext;
		IconButtonFrame(sb, previous, active: false, Page > 0);
		Chevron(sb, previous.X + previous.Width / 2f - 1f, previous.Y + previous.Height / 2f, 4f, 2, (Page > 0) ? TextColor : Border);
		IconButtonFrame(sb, next, active: false, Page < Pages - 1);
		Chevron(sb, next.X + next.Width / 2f + 1f, next.Y + next.Height / 2f, 4f, 3, (Page < Pages - 1) ? TextColor : Border);
		string count;
		if (StorageView)
		{
			count = T("Стопок: ", "Stacks: ") + Count(Filtered.Count);
		}
		else if (Catalogue)
		{
			count = T("Предметов: ", "Items: ") + Count(Filtered.Count);
		}
		else if (BestiaryView)
		{
			count = T("Мобов: ", "Mobs: ") + Count(Filtered.Count);
		}
		else if (RecentView)
		{
			count = T("Недавние: ", "Recent: ") + Filtered.Count + "/" + 40;
		}
		else
		{
			count = T("Рецептов: ", "Recipes: ") + Count(Filtered.Count);
		}
		BookRect label = BookGeometry.PageLabel;
		CenterLabel(sb, count + "  ·  " + (Page + 1) + " / " + Pages, label.X, label.Y, label.Width, label.Height, Muted, 0.85f);
		if (!MenuOpen && previous.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Предыдущая страница · PgUp", "Previous page · PgUp"));
		}
		else if (!MenuOpen && next.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Следующая страница · PgDn", "Next page · PgDn"));
		}
	}

	private static void DrawDetails(SpriteBatch sb)
	{
		Box(sb, BookGeometry.DetailsPanel, Surface, border: false, 12f);
		Item chosen = SelectedItem;
		if (chosen == null)
		{
			EmptyState(sb, BookGeometry.DetailsPanel, T("Ничего не выбрано", "Nothing selected"), T("Выберите предмет слева", "Pick an item on the left"));
			return;
		}
		Recipe r = SelectedRecipe;
		float top = RecipeBlockTop(chosen);
		DrawCardNav(sb);
		DrawDetailHeader(sb, chosen, r);
		Box(sb, 812f, top - 12f, 284f, 1f, Border, border: false, 0f);
		if (UsedInMode)
		{
			DrawUsedIn(sb, chosen, top);
		}
		else if (r != null)
		{
			DrawRecipeBlock(sb, chosen, r, top);
		}
		else
		{
			FixedLabel(sb, T("Нет рецепта крафта", "No crafting recipe"), 812f, top + 4f, TextColor, 1f, 284f, 28f);
			FixedLabel(sb, T("Добыча, магазины, рыбалка и другое", "Get it from loot, shops, fishing and more"), 812f, top + 36f, Muted, 0.8f, 284f, 20f);
			BookRect obtain = BookGeometry.SelectedItemObtain(top);
			ActionButton(sb, obtain, T("Где взять", "How to Get"), primary: false);
			if (!MenuOpen && obtain.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Все источники: ", "All sources: ") + chosen.Name + " · Alt+G");
			}
		}
		bool favorite = Favorites.Contains(ItemKey(chosen));
		BookRect favoriteRect = BookGeometry.FavoriteAction;
		IconButtonFrame(sb, favoriteRect, favorite);
		Star(sb, favoriteRect.X + 6f, favoriteRect.Y + 6f, 20f, favorite);
		if (!MenuOpen && favoriteRect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(favorite ? T("Убрать из избранного · Alt+F", "Remove from favorites · Alt+F") : T("В избранное · Alt+F", "Add to favorites · Alt+F"));
		}
		if (r != null)
		{
			ActionButton(sb, CraftRect, T("Создать", "Craft") + " ×" + PreviewBatches(r), primary: true, BookCrafting.CanCraft(r));
			if (!MenuOpen && CraftRect.Contains(Mouse.X, Mouse.Y))
			{
				string from = BookCrafting.Sources(r, PreviewBatches(r));
				MouseTip(BookCrafting.Reason(r, Ru) + ((from.Length > 0) ? (" · " + from) : "") + "\nCtrl+Enter · " + T("максимум", "max") + ": Ctrl+Shift+Enter");
			}
		}
		DrawTrackButton(sb, r);
	}

	// Header: icon, name and "<item's own mod> · stage". An armor set shows its three pieces instead.
	private static void DrawDetailHeader(SpriteBatch sb, Item chosen, Recipe r)
	{
		string name = chosen.Name + ((r != null && r.createItem.stack > 1) ? (" ×" + r.createItem.stack) : "");
		string mod = ModName(chosen.ModItem?.Mod.Name ?? "Terraria");
		string stage = StageName(chosen, (CardItemType > 0) ? null : r);
		Recipe stageRecipe = (CardItemType > 0) ? null : r;
		if (DrawArmorSet(sb, chosen))
		{
			BookRect title = BookGeometry.ArmorTitle;
			BookRect subtitle = BookGeometry.ArmorSubtitle;
			FixedLabel(sb, name, title.X, title.Y, TextColor, 1.05f, title.Width, title.Height);
			FixedLabel(sb, mod + " · " + stage, subtitle.X, subtitle.Y, Muted, 0.8f, subtitle.Width, subtitle.Height);
			if (!MenuOpen && subtitle.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(StageExplanation(chosen, stageRecipe));
			}
			DrawArmorDefense(sb, chosen);
			DrawArmorBonus(sb, chosen);
			return;
		}
		BookRect tile = BookGeometry.DetailHeaderIcon;
		Box(sb, tile, Raised, border: true, 10f);
		Icon(sb, chosen, tile.X + 8f, tile.Y + 8f, 40f);
		bool armor = HasArmorDefense(chosen);
		bool wrap = TextWidth(mod + " · " + stage, 0.8f) > BookGeometry.DetailHeaderSub.Width;
		int lines = 1 + ((armor || wrap) ? 1 : 0) + ((armor && wrap) ? 1 : 0);
		float y = tile.Y + (tile.Height - (26f + lines * 20f)) / 2f;
		FixedLabel(sb, name, 880f, y, TextColor, 1.05f, 216f, 26f);
		y += 26f;
		BookRect sub = new BookRect(880f, y, 216f, wrap ? 40f : 20f);
		if (wrap)
		{
			FixedLabel(sb, mod, 880f, y, Muted, 0.8f, 216f, 20f);
			FixedLabel(sb, stage, 880f, y + 20f, Muted, 0.8f, 216f, 20f);
			y += 40f;
		}
		else
		{
			FixedLabel(sb, mod + " · " + stage, 880f, y, Muted, 0.8f, 216f, 20f);
			y += 20f;
		}
		if (!MenuOpen && sub.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(StageExplanation(chosen, stageRecipe));
		}
		if (armor)
		{
			DrawArmorDefenseAt(sb, chosen, new BookRect(880f, y, 216f, 20f));
		}
	}

	private static void DrawRecipeBlock(SpriteBatch sb, Item chosen, Recipe r, float top)
	{
		bool variants = CardItemType > 0 && VariantCount > 1;
		string itemMod = chosen.ModItem?.Mod.Name ?? "Terraria";
		string owner = Owner(r);
		string line = T("Рецепт", "Recipe") + (variants ? (" " + (Variant + 1) + "/" + VariantCount) : "") + ((owner != itemMod) ? (": " + ModName(owner)) : "");
		BookRect lineRect = BookGeometry.RecipeLine(top);
		CenterLabel(sb, line, lineRect.X, lineRect.Y, variants ? lineRect.Width : 284f, lineRect.Height, Muted, 0.85f, rightAligned: false, leftAligned: true);
		if (variants)
		{
			BookRect previous = BookGeometry.VariantPrevious(top);
			BookRect next = BookGeometry.VariantNext(top);
			IconButtonFrame(sb, previous, active: false, Variant > 0);
			Chevron(sb, previous.X + previous.Width / 2f - 1f, previous.Y + previous.Height / 2f, 4f, 2, (Variant > 0) ? TextColor : Border);
			IconButtonFrame(sb, next, active: false, Variant < VariantCount - 1);
			Chevron(sb, next.X + next.Width / 2f + 1f, next.Y + next.Height / 2f, 4f, 3, (Variant < VariantCount - 1) ? TextColor : Border);
			if (!MenuOpen && (previous.Contains(Mouse.X, Mouse.Y) || next.Contains(Mouse.X, Mouse.Y)))
			{
				MouseTip(T("Другие рецепты этого предмета", "Other recipes for this item"));
			}
		}
		DrawBatchChoices(sb, r);
		List<Detail> rows = Details(r);
		int materials = r.requiredItem.Count((Item i) => !i.IsAir);
		int stations = Math.Max(1, r.requiredTile.Count((int t) => t >= 0));
		List<List<DetailSlot>> pages = DetailPages(r, rows.Count, top);
		DetailsPage = Math.Clamp(DetailsPage, 0, pages.Count - 1);
		foreach (DetailSlot slot in pages[DetailsPage])
		{
			if (slot.Detail < 0)
			{
				SectionLabel(sb, slot.Header, slot.Rect.X, slot.Rect.Y, slot.Rect.Width);
				continue;
			}
			int kind = (slot.Detail < materials) ? 0 : ((slot.Detail < materials + stations) ? 1 : 2);
			DrawDetailRow(sb, rows[slot.Detail], slot.Rect, kind);
		}
		if (pages.Count > 1)
		{
			BookRect previous2 = BookGeometry.DetailListPrevious;
			BookRect next2 = BookGeometry.DetailListNext;
			IconButtonFrame(sb, previous2, active: false, DetailsPage > 0);
			Chevron(sb, previous2.X + previous2.Width / 2f - 1f, previous2.Y + previous2.Height / 2f, 4f, 2, (DetailsPage > 0) ? TextColor : Border);
			IconButtonFrame(sb, next2, active: false, DetailsPage < pages.Count - 1);
			Chevron(sb, next2.X + next2.Width / 2f + 1f, next2.Y + next2.Height / 2f, 4f, 3, (DetailsPage < pages.Count - 1) ? TextColor : Border);
			Label(sb, BookGeometry.DetailListPager, DetailsPage + 1 + " / " + pages.Count, Muted, 0.85f);
			if (!MenuOpen && (previous2.Contains(Mouse.X, Mouse.Y) || next2.Contains(Mouse.X, Mouse.Y)))
			{
				MouseTip(T("Остальные строки рецепта · колесо мыши", "More of this recipe · Mouse wheel"));
			}
		}
	}

	private static string[] CardModeLabels()
	{
		return new string[2]
		{
			T("Рецепт", "Recipe"),
			T("Применение", "Used in")
		};
	}

	private static BookRect[] CardModeRects()
	{
		return SegmentRects(BookGeometry.CardMode, CardModeLabels(), 0.8f, natural: false);
	}

	// Card navigation: back through the steps taken in the card, and Recipe / Used in for the shown item.
	private static void DrawCardNav(SpriteBatch sb)
	{
		BookRect back = BookGeometry.CardBack;
		bool canBack = History.CanBack;
		IconButtonFrame(sb, back, active: false, canBack);
		Chevron(sb, back.X + back.Width / 2f - 1f, back.Y + back.Height / 2f, 4f, 2, canBack ? TextColor : Border);
		if (!MenuOpen && back.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Назад", "Back") + (canBack ? (": " + HistoryLabel(History.Previous)) : "") + "\nMouse4 · Alt+←");
		}
		BookRect[] modes = CardModeRects();
		Segmented(sb, modes, CardModeLabels(), UsedInMode ? 1 : 0, 0.8f);
		int hover = MenuOpen ? (-1) : SegmentAt(modes);
		if (hover >= 0)
		{
			MouseTip((hover == 0) ? T("Из чего сделан предмет · Alt+U", "What the item is made of · Alt+U") : T("Что можно сделать из предмета · Alt+U", "What you can make from it · Alt+U"));
		}
	}

	private static bool HandleCardNavClick()
	{
		if (BookGeometry.CardBack.Contains(Mouse.X, Mouse.Y))
		{
			HistoryBack();
			return true;
		}
		int mode = SegmentAt(CardModeRects());
		if (mode < 0)
		{
			return false;
		}
		UsedInMode = mode == 1;
		DetailsPage = 0;
		if (UsedInMode && UsedInCraftableOnly)
		{
			BookCrafting.Refresh(force: true);
		}
		return true;
	}

	private readonly record struct UsedInView(List<int> Rows, int StageHidden, int NotCraftable, int Total);

	// Recipes that take the item, earliest stage first. Only the global stage filter and the Craftable toggle
	// narrow the list: the grid's search, mod and category filters do not reach the card.
	private static List<int> UsedInRecipes(int type)
	{
		if (!UsedInCache.TryGetValue(type, out var list))
		{
			list = All.Where((int index) => BookRules.RecipeMatchesSample(type, Main.recipe[index].createItem.type, uses: true, from item in Main.recipe[index].requiredItem
					where !item.IsAir
					select item.type, Main.recipe[index].AcceptedByItemGroups))
				.OrderBy((int index) => (BookProgression.RecipeStage(index) == BookStage.Unknown) ? 3 : (int)BookProgression.RecipeStage(index))
				.ThenBy((int index) => Main.recipe[index].createItem.Name, StringComparer.CurrentCultureIgnoreCase)
				.DistinctBy(BookRecipeVariants.Representative)
				.ToList();
			UsedInCache[type] = list;
		}
		return list;
	}

	private static UsedInView VisibleUsedIn(int type)
	{
		List<int> all = UsedInRecipes(type);
		List<int> rows = new List<int>();
		int stageHidden = 0;
		int notCraftable = 0;
		foreach (int index in all)
		{
			if (!BookProgressionRules.MatchesChain(StageFilter, BookProgression.RecipeStage(index), BookProgression.RecipeMinimumStage(index)))
			{
				stageHidden++;
			}
			else if (!BookRules.ChainRecipeVisible(UsedInCraftableOnly, BookCrafting.IsAvailable(ActiveMember(index))))
			{
				notCraftable++;
			}
			else
			{
				rows.Add(index);
			}
		}
		return new UsedInView(rows, stageHidden, notCraftable, all.Count);
	}

	private const float UsedInRowStep = 44f;

	private static BookRect UsedInCraftableToggle(float top)
	{
		float width = TextWidth(T("Можно создать", "Craftable"), 0.8f) + 24f;
		return new BookRect(1096f - width, top, width, 24f);
	}

	private static BookRect UsedInRow(float top, int slot)
	{
		return new BookRect(812f, top + 32f + slot * UsedInRowStep, 284f, 40f);
	}

	// Rows that fit above the actions; when the list needs pages, the pager takes the last row's place.
	private static int UsedInPageSize(float top, int count)
	{
		int unpaged = Math.Max(1, (int)((BookGeometry.DetailListBottom - top - 32f + 4f) / UsedInRowStep));
		if (count <= unpaged)
		{
			return unpaged;
		}
		return Math.Max(1, (int)((BookGeometry.DetailListPagedBottom - top - 32f + 4f) / UsedInRowStep));
	}

	private static void DrawUsedIn(SpriteBatch sb, Item chosen, float top)
	{
		UsedInView view = VisibleUsedIn(chosen.type);
		BookRect toggle = UsedInCraftableToggle(top);
		SectionLabel(sb, T("Рецептов: ", "Recipes: ") + view.Rows.Count, 812f, top + 3f, toggle.X - 820f);
		bool toggleHover = !MenuOpen && toggle.Contains(Mouse.X, Mouse.Y);
		Box(sb, toggle, UsedInCraftableOnly ? SelectedFill : (toggleHover ? HoverFill : Raised), border: true, 6f, UsedInCraftableOnly ? new Color?(Accent) : null);
		CenterLabel(sb, T("Можно создать", "Craftable"), toggle.X + 6f, toggle.Y + 2f, toggle.Width - 12f, toggle.Height - 4f, UsedInCraftableOnly ? Accent : TextColor, 0.8f);
		if (toggleHover)
		{
			MouseTip(UsedInCraftableOnly ? T("Показать все рецепты", "Show every recipe") : T("Только то, что можно создать сейчас", "Only what you can craft right now"));
		}
		if (view.Rows.Count == 0)
		{
			string title;
			string hint;
			if (view.Total == 0)
			{
				title = T("Нигде не используется", "Not used in any recipe");
				hint = "";
			}
			else if (view.StageHidden == 0)
			{
				title = T("Сейчас нечего создать", "Nothing to craft right now");
				hint = T("Снимите «Можно создать»", "Turn off Craftable");
			}
			else
			{
				title = T("Скрыто фильтром этапа", "Hidden by the stage filter");
				hint = T("Этап выбирается вверху книги", "Pick the stage at the top of the book");
			}
			EmptyState(sb, new BookRect(812f, top + 32f, 284f, BookGeometry.DetailListBottom - top - 32f), title, hint);
			return;
		}
		int size = UsedInPageSize(top, view.Rows.Count);
		int pages = (view.Rows.Count + size - 1) / size;
		DetailsPage = Math.Clamp(DetailsPage, 0, pages - 1);
		for (int slot = 0; slot < size; slot++)
		{
			int n = DetailsPage * size + slot;
			if (n >= view.Rows.Count)
			{
				break;
			}
			Recipe recipe = ActiveMember(view.Rows[n]);
			Item output = recipe.createItem;
			MaterialPreview preview = PreviewMaterials(recipe);
			BookRect row = UsedInRow(top, slot);
			bool hover = !MenuOpen && row.Contains(Mouse.X, Mouse.Y);
			Box(sb, row, hover ? HoverFill : Raised, border: false, 8f);
			Icon(sb, output, row.X + 6f, row.Y + 4f, 32f, tooltip: false);
			float textWidth = row.Width - 46f - 24f;
			CenterLabel(sb, output.Name + ((output.stack > 1) ? (" ×" + output.stack) : ""), row.X + 46f, row.Y + 3f, textWidth, 18f, TextColor, 0.85f, rightAligned: false, leftAligned: true);
			CenterLabel(sb, preview.Summary, row.X + 46f, row.Y + 21f, textWidth, 16f, preview.Color, 0.8f, rightAligned: false, leftAligned: true);
			Chevron(sb, row.X + row.Width - 14f, row.Y + row.Height / 2f, 4f, 3, hover ? TextColor : Muted);
			if (hover)
			{
				Main.HoverItem = new Item();
				Main.hoverItemName = "";
				MouseTip(preview.Materials + "\n" + T("Клик: открыть этот предмет", "Click: open this item"));
			}
		}
		if (pages > 1)
		{
			ChevronPager(sb, BookGeometry.DetailListPrevious, BookGeometry.DetailListNext, BookGeometry.DetailListPager, DetailsPage, pages);
			if (!MenuOpen && (BookGeometry.DetailListPrevious.Contains(Mouse.X, Mouse.Y) || BookGeometry.DetailListNext.Contains(Mouse.X, Mouse.Y)))
			{
				MouseTip(T("Ещё рецепты · колесо мыши", "More recipes · Mouse wheel"));
			}
		}
	}

	// Previous / next icon buttons with "n / m" between them; an end that cannot move is disabled.
	private static void ChevronPager(SpriteBatch sb, BookRect previous, BookRect next, BookRect label, int page, int pages)
	{
		IconButtonFrame(sb, previous, active: false, page > 0);
		Chevron(sb, previous.X + previous.Width / 2f - 1f, previous.Y + previous.Height / 2f, 4f, 2, (page > 0) ? TextColor : Border);
		IconButtonFrame(sb, next, active: false, page < pages - 1);
		Chevron(sb, next.X + next.Width / 2f + 1f, next.Y + next.Height / 2f, 4f, 3, (page < pages - 1) ? TextColor : Border);
		Label(sb, label, page + 1 + " / " + pages, Muted, 0.85f);
	}

	private static bool HandleUsedInClick(int type, float top)
	{
		if (UsedInCraftableToggle(top).Contains(Mouse.X, Mouse.Y))
		{
			UsedInCraftableOnly = !UsedInCraftableOnly;
			if (UsedInCraftableOnly)
			{
				BookCrafting.Refresh(force: true);
			}
			DetailsPage = 0;
			return true;
		}
		UsedInView view = VisibleUsedIn(type);
		int size = UsedInPageSize(top, view.Rows.Count);
		int pages = Math.Max(1, (view.Rows.Count + size - 1) / size);
		if (pages > 1 && BookGeometry.DetailListPrevious.Contains(Mouse.X, Mouse.Y))
		{
			DetailsPage = Math.Max(0, DetailsPage - 1);
			return true;
		}
		if (pages > 1 && BookGeometry.DetailListNext.Contains(Mouse.X, Mouse.Y))
		{
			DetailsPage = Math.Min(pages - 1, DetailsPage + 1);
			return true;
		}
		for (int slot = 0; slot < size; slot++)
		{
			int n = DetailsPage * size + slot;
			if (n >= view.Rows.Count)
			{
				break;
			}
			if (UsedInRow(top, slot).Contains(Mouse.X, Mouse.Y))
			{
				StepCard(Main.recipe[view.Rows[n]].createItem.type, usedIn: true);
				return true;
			}
		}
		return false;
	}

	private static void DrawDetailRow(SpriteBatch sb, Detail d, BookRect row, int kind)
	{
		bool hasItem = d.Icon != null && !d.Icon.IsAir;
		Box(sb, row, Raised, border: false, 8f);
		float textX = row.X + 10f;
		if (d.Icon != null)
		{
			Icon(sb, d.Icon, row.X + 6f, row.Y + (row.Height - 24f) / 2f, 24f, tooltip: false);
			textX = row.X + 38f;
		}
		BookRect obtain = BookGeometry.DetailListObtain(row);
		float right = (hasItem ? (obtain.X - 8f) : (row.X + row.Width - 10f));
		if (kind == 0)
		{
			float valueWidth = TextWidth(d.Sub, 0.85f) + 4f;
			CenterLabel(sb, d.Sub, right - valueWidth, row.Y + 4f, valueWidth, row.Height - 8f, d.Color, 0.85f, rightAligned: true);
			right -= valueWidth + 10f;
		}
		if (d.Alternatives && row.Height > 32f)
		{
			CenterLabel(sb, d.Icon.Name, textX, row.Y + 2f, right - textX, 19f, TextColor, 0.85f, rightAligned: false, leftAligned: true);
			CenterLabel(sb, OthersLabel(d.SourceTypes, d.Icon.type, right - textX), textX, row.Y + 20f, right - textX, 16f, Muted, 0.8f, rightAligned: false, leftAligned: true);
		}
		else
		{
			string name = d.Alternatives ? AlternativesLabel(d.SourceTypes, d.Icon.type, right - textX) : d.Name;
			CenterLabel(sb, name, textX, row.Y + 4f, right - textX, row.Height - 8f, (kind == 2) ? Caution : ((d.Icon == null) ? Muted : TextColor), 0.85f, rightAligned: false, leftAligned: true);
		}
		if (hasItem)
		{
			bool hover = IconButtonFrame(sb, obtain);
			CenterLabel(sb, "?", obtain.X + 2f, obtain.Y + 2f, obtain.Width - 4f, obtain.Height - 4f, hover ? TextColor : Muted, 0.85f);
			if (hover)
			{
				MouseTip(T("Где взять: ", "How to Get: ") + d.Name);
				return;
			}
		}
		if (!MenuOpen && row.Contains(Mouse.X, Mouse.Y))
		{
			// An "A or B" row opens the alternative it shows.
			string action = hasItem ? (d.Alternatives ? T("Клик: открыть «" + d.Icon.Name + "»", "Click: open " + d.Icon.Name) : T("Клик: открыть этот предмет", "Click: open this item")) : "";
			string detail = (kind == 0) ? d.Tooltip : "";
			// Where the item is ("Chests ×2: 120") stays the last line, under the click hint.
			string where = "";
			int cut = detail.IndexOf('\n');
			if (cut >= 0)
			{
				where = detail.Substring(cut);
				detail = detail.Substring(0, cut);
			}
			string body = string.Join(d.Alternatives ? "\n" : " · ", new string[2] { detail, action }.Where((string s) => s.Length > 0));
			MouseTip(d.Name + ((body.Length > 0) ? ("\n" + body) : "") + where);
		}
	}

	private static void DrawFooter(SpriteBatch sb)
	{
		Box(sb, 8f, 648f, 1104f, 1f, Border, border: false, 0f);
		BookRect decrease = BookGeometry.WindowSizeDecrease;
		BookRect increase = BookGeometry.WindowSizeIncrease;
		IconButtonFrame(sb, decrease, active: false, WindowScalePercent > 70);
		Line(sb, decrease.X + 7f, decrease.Y + 12f, decrease.X + 17f, decrease.Y + 12f, (WindowScalePercent > 70) ? TextColor : Border);
		Label(sb, BookGeometry.WindowSizeLabel, WindowScalePercent + "%", Muted, 0.8f);
		IconButtonFrame(sb, increase, active: false, WindowScalePercent < 130);
		Plus(sb, increase.X + 12f, increase.Y + 12f, 5f, (WindowScalePercent < 130) ? TextColor : Border);
		if (!MenuOpen && (decrease.Contains(Mouse.X, Mouse.Y) || increase.Contains(Mouse.X, Mouse.Y)))
		{
			MouseTip(T("Размер окна", "Window size"));
		}
		float hintRight = 924f;
		string message = BookCrafting.LastMessage ?? "";
		CenterLabel(sb, (message.Length > 0) ? message : FooterHint(), 140f, 660f, hintRight - 140f, 24f, (message.Length > 0) ? Accent : Muted, 0.8f, rightAligned: false, leftAligned: true);
	}

	private static string FooterHint()
	{
		return CurrentTab switch
		{
			0 => T("Shift+клик: где используется · F1: справка", "Shift+Click: where it's used · F1 Help"),
			1 => T("Ctrl+Enter: создать · F1: справка", "Ctrl+Enter crafts · F1 Help"),
			2 => T("Mouse3 по мобу в мире: его карточка · F1: справка", "Mouse3 on a mob in the world: its card · F1 Help"),
			3 => T("Клик: взять стопку · ПКМ: одну · F1: справка", "Click takes a stack · Right-click takes one · F1 Help"),
			4 => T("Новые сверху · F1: справка", "Newest first · F1 Help"),
			_ => T("PgUp/PgDn: страницы · F1: справка", "PgUp/PgDn pages · F1 Help"),
		};
	}

	private static BookRect CraftRect => BookGeometry.CraftAction;

	private static void DrawSortMenu(SpriteBatch sb)
	{
		BookRect panel = BookGeometry.SortMenuPanel;
		Box(sb, panel.X, panel.Y, panel.Width, 8f + SortOptionCount * 32f, new Color(17, 25, 20), border: true, 10f, Accent);
		for (int option = 0; option < SortOptionCount; option++)
		{
			BookRect rect = BookGeometry.SortOption(option);
			bool active = option == SortOptionIndex;
			bool hover = rect.Contains(Mouse.X, Mouse.Y);
			if (active)
			{
				Box(sb, rect, SelectedFill, border: false, 6f);
			}
			else if (hover)
			{
				Box(sb, rect, HoverFill, border: false, 6f);
			}
			CenterLabel(sb, SortOptionLabel(option), rect.X + 12f, rect.Y + 4f, rect.Width - 24f, rect.Height - 8f, active ? Accent : TextColor, 0.85f, rightAligned: false, leftAligned: true);
		}
	}

	private static string TitleText => T("Книга рецептов", "Recipe Book");

	private static float TitleRight()
	{
		return 34f + TextWidth(TitleText, 1.1f);
	}

	private const float TabTextSize = 0.9f;

	private static int CurrentTab => TrackingView ? 5 : (RecentView ? 4 : (StorageView ? 3 : (BestiaryView ? 2 : ((!Catalogue) ? 1 : 0))));

	private static string TabLabel(int tab)
	{
		return tab switch
		{
			0 => T("Предметы", "Items"),
			1 => T("Рецепты", "Recipes"),
			2 => T("Бестиарий", "Bestiary"),
			3 => T("Хранилище", "Storage"),
			4 => T("Недавние", "Recent"),
			_ => T("Отслеживание", "Tracking"),
		};
	}

	private static int TrackingTabNumber => VisibleTabs().IndexOf(5) + 1;

	// Every tab, Storage included: nearby chests count without Magic Storage.
	private static List<int> VisibleTabs()
	{
		List<int> tabs = new List<int>();
		for (int tab = 0; tab < BookGeometry.TabCount; tab++)
		{
			tabs.Add(tab);
		}
		return tabs;
	}

	// Tabs share the widest label's width when that fits next to the title, otherwise each gets its own.
	private static BookRect TabRect(int tab)
	{
		List<int> tabs = VisibleTabs();
		int slot = tabs.IndexOf(tab);
		if (slot < 0)
		{
			return default(BookRect);
		}
		// Equal tabs keep 16 px of air each side; when they do not fit (long Russian labels), each tab hugs its
		// label with 12 px, down to 6 px when six tabs still crowd the stage dropdown.
		float room = BookGeometry.TabRight - StageDropdownRect().X - StageDropdownRect().Width - 24f;
		float[] labels = tabs.Select((int t) => TextWidth(TabLabel(t), TabTextSize)).ToArray();
		float pad = 24f;
		while (pad > 12f && labels.Sum() + pad * tabs.Count + 8f * (tabs.Count - 1) > room)
		{
			pad -= 4f;
		}
		float[] widths = labels.Select((float w) => w + pad).ToArray();
		float equal = Math.Max(96f, labels.Max() + 32f);
		bool same = equal * tabs.Count + 8f * (tabs.Count - 1) <= room;
		float x = BookGeometry.TabRight;
		for (int i = tabs.Count - 1; i > slot; i--)
		{
			x -= (same ? equal : widths[i]) + 8f;
		}
		float width = (same ? equal : widths[slot]);
		return new BookRect(x - width, BookGeometry.TabTop, width, BookGeometry.TabHeight);
	}

	// A key the way keyboards draw it: quiet fill, hairline border, the label centered.
	private static void HelpKeycap(SpriteBatch sb, BookRect rect, string label)
	{
		Box(sb, rect, Raised, border: true, 6f, new Color(78, 99, 84));
		CenterLabel(sb, label, rect.X + 6f, rect.Y, rect.Width - 12f, rect.Height, TextColor, 0.8f);
	}

	// F1 cheat sheet: two columns of sections, each row a keycap and what it does. Modal: nothing under it hints.
	private static void DrawHelp(SpriteBatch sb)
	{
		PendingTip = null;
		BookRect modal = BookGeometry.HelpPanel;
		Box(sb, modal, Surface, border: true, 12f);
		Text(sb, T("Горячие клавиши", "Shortcuts"), modal.X + 24f, modal.Y + 24f, TextColor, 1.1f, modal.Width - 112f, 32f);
		CardButton(sb, BookGeometry.HelpClose, "X");
		if (BookGeometry.HelpClose.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Закрыть · Esc или F1", "Close · Esc or F1"));
		}
		Text(sb, T("Наведите на кнопку — в подсказке есть её клавиша", "Hover any button: its tooltip shows the shortcut"), modal.X + 24f, modal.Y + 60f, Muted, 0.85f, modal.Width - 112f, 24f);
		Box(sb, modal.X + 24f, modal.Y + 96f, modal.Width - 48f, 1f, Border);
		float[] top = new float[BookHelp.Columns];
		for (int column = 0; column < BookHelp.Columns; column++)
		{
			top[column] = BookHelp.Column(column).Y;
		}
		foreach (BookHelp.Section section in BookHelp.Sections)
		{
			if (!Array.Exists(section.Rows, (BookHelp.Entry row) => row.Visible))
			{
				continue;
			}
			BookRect column = BookHelp.Column(section.Column);
			float y = top[section.Column];
			Text(sb, T(section.Ru, section.En), column.X, y, Muted, 0.8f, column.Width, 20f);
			y += BookHelp.HeaderStep;
			foreach (BookHelp.Entry row in section.Rows)
			{
				if (!row.Visible)
				{
					continue;
				}
				HelpKeycap(sb, new BookRect(column.X, y, BookHelp.KeyWidth, BookHelp.KeyHeight), T(row.KeyRu, row.KeyEn));
				Text(sb, T(row.Ru, row.En), column.X + BookHelp.KeyWidth + 12f, y, TextColor, 0.85f, column.Width - BookHelp.KeyWidth - 12f, BookHelp.KeyHeight);
				y += BookHelp.RowStep;
			}
			top[section.Column] = y + BookHelp.SectionGap;
		}
	}
}
