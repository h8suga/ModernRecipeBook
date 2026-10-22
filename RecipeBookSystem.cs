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

public sealed class RecipeBookSystem : ModSystem
{
	private readonly record struct BatchEstimate(int Count, ulong Tick, int AvailabilityVersion, int StorageVersion, bool Storage);

	private sealed class TrackedGoal
	{
		internal Recipe Recipe;

		internal Recipe[] Recipes;

		internal Item[] Parts;

		internal string Key;

		internal int Batches;

		internal int Page;

		internal int ResourcePage;

		internal bool Storage;

		internal bool Ready;

		internal bool Unknown;

		internal List<MaterialRequirement> Materials = new List<MaterialRequirement>();

		internal Dictionary<int, int> Counts = new Dictionary<int, int>();

		internal readonly BookTrackerSignal Signal = new BookTrackerSignal();

		internal readonly BookTrackerDismiss Dismiss = new BookTrackerDismiss();

		internal bool IsSet => Parts.Length == 3;
	}

	private sealed record MaterialRequirement(Item Ingredient, RecipeGroup Group, string Name, HashSet<int> ItemTypes, bool Overlapping);

	private sealed record Detail(Item Icon, string Name, string Sub, Color Color, Color SubColor, string Tooltip, int[] SourceTypes = null);

	private sealed record ChainMaterialPreview(string Summary, Color Color, string Tooltip);

	private static bool AcquisitionOpen;

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

	private static readonly BookRect[] BatchChoiceBounds = Enumerable.Range(0, 4).Select(BookGeometry.BatchChoice).ToArray();

	private static bool PendingChainBack;

	private static bool PendingChainForward;

	private static readonly List<TrackedGoal> TrackedGoals = new List<TrackedGoal>();

	private static TrackedGoal ActiveGoal;

	private static int TrackerTicks;

	internal static bool BlockTrackerPress;

	private static bool TrackerLastLeft;

	private static bool TrackerDragging;

	private static bool TrackerCustomPosition;

	private static float TrackerPositionX;

	private static float TrackerPositionY = 1f;

	private static float TrackerDragX;

	private static float TrackerDragY;

	private static float GraphicsOpacity = 1f;

	private static bool EnemyOpen;

	private static int[] EnemyVariants = Array.Empty<int>();

	private static int EnemyVariant;

	private static int EnemyPage;

	private static int EnemyReadErrors;

	private static string EnemyBiomes = "";

	private static string EnemySpawn = "";

	private static List<BookEnemyLoot> EnemyLoot = new List<BookEnemyLoot>();

	private static BookStageFilter StageFilter;

	private static bool? LastResourceTrackerStyle;

	private static readonly BookTrackerSignal ResourceTrackerSignal = new BookTrackerSignal();

	private static readonly BookTrackerDismiss ResourceTrackerDismiss = new BookTrackerDismiss();

	private static BookResourceProgress[] ResourceTrackerRows = Array.Empty<BookResourceProgress>();

	private static readonly Dictionary<string, MaterialRequirement> ResourceTrackerMaterials = new Dictionary<string, MaterialRequirement>();

	private static int ResourceTrackerPage;

	private static readonly Color ResourceCompleteColor = new Color(211, 173, 74);

	private static TrackedGoal TrackingSelection;

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

	private static bool Chains;

	private static bool ChainCraftableOnly;

	private static bool StorageView;

	private static bool RecentView;

	private static bool TrackingView;

	private static int AppliedStorageVersion = -1;

	private static int AppliedRecentVersion = -1;

	private static int AppliedAvailabilityVersion = -1;

	private static int ChainEligibleCount;

	private static int ChainStageExcludedCount;

	private static int ChainUnknownVisibleCount;

	private static readonly BookChainHistory ChainHistory = new BookChainHistory();

	private static int Variant;

	private static int RegisteredRecipes;

	private static int DisabledRecipes;

	private static readonly object SearchOwner = new object();

	private static bool OwnInputBlock;

	private static bool LastMiddle;

	private static readonly List<int> Filtered = new List<int>();

	private static readonly List<string> Mods = new List<string>();

	private static readonly Dictionary<int, Item> StationItems = new Dictionary<int, Item>();

	private static readonly Dictionary<int, string> StationNames = new Dictionary<int, string>();

	private static int Selected = -1;

	private static int Category;

	private static int Page;

	private static int DetailsPage;

	private static int ModOffset;

	private static int CategoryRow;

	private static List<BookCategory> Categories = new List<BookCategory>();

	private static readonly Dictionary<int, HashSet<int>> ItemCategories = new Dictionary<int, HashSet<int>>();

	private static string Query = "";

	private static string ModFilter = "";

	private static bool FavoritesOnly;

	private static bool ModMenu;

	private static bool Dirty = true;

	private static bool LastLeft;

	private static bool LastRight;

	private static bool LastEscape;

	private static bool HelpOpen;

	private static KeyboardState LastKeys;

	private static int SampleType;

	private static readonly HashSet<int> SampleRecipes = new HashSet<int>();

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

	private static int[] TrackerRows => TrackedGoals.Select((TrackedGoal g) => g.Materials.Count).ToArray();

	private static int TrackerActiveIndex => Math.Max(0, TrackedGoals.IndexOf(ActiveGoal));

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

	private static int TrackerPanelHeight
	{
		get
		{
			if (!ResourceTrackerStyle)
			{
				return BookTrackerRules.PanelHeight(TrackerRows, TrackerActiveIndex);
			}
			return BookResourceTrackerRules.Height(ResourceTrackerRows.Length);
		}
	}

	private static float ResourcePagerY => 240f;

	private static int TrackingGoalPages => BookTrackingSelectionRules.Pages(TrackedGoals.Count, 60);

	internal static bool UiStateInitialized { get; private set; }

	public static bool Open { get; private set; }

	internal static bool ItemIconSearchAllowed
	{
		get
		{
			if (Open)
			{
				if (!HelpOpen && !AcquisitionOpen && !ModMenu)
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
			if (Typing || HelpOpen || AcquisitionOpen)
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

	private static bool SampleUses => Chains;

	private static int ActivePageSize
	{
		get
		{
			if (!Chains)
			{
				return 40;
			}
			return 12;
		}
	}

	private static bool Ru => Language.ActiveCulture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

	private static HashSet<string> Favorites => Main.LocalPlayer.GetModPlayer<BookPlayer>().Favorites;

	private static int MaxCategoryRow => Math.Max(0, (Categories.Count + 3 - 1) / 3 - 10);

	private static Item SelectedItem
	{
		get
		{
			if (!TrackingView)
			{
				if (ArmorFocus <= 0 || StorageView)
				{
					if (Selected < 0 || (StorageView && Selected >= BookStorage.Items.Count))
					{
						return null;
					}
					return Result(Selected);
				}
				return ContentSamples.ItemsByType[ArmorFocus];
			}
			return null;
		}
	}

	private static Recipe SelectedRecipe
	{
		get
		{
			if (Selected < 0 || StorageView || TrackingView)
			{
				return null;
			}
			if (!Catalogue && ArmorFocus <= 0)
			{
				return Main.recipe[Selected];
			}
			if (!RecipesByItem.TryGetValue((ArmorFocus > 0) ? ArmorFocus : Selected, out var variants) || variants.Count == 0)
			{
				return null;
			}
			Variant = Math.Clamp(Variant, 0, variants.Count - 1);
			return Main.recipe[variants[Variant]];
		}
	}

	private static int VariantCount
	{
		get
		{
			if (!Catalogue && ArmorFocus <= 0)
			{
				return (Selected >= 0) ? 1 : 0;
			}
			if (Selected < 0 || !RecipesByItem.TryGetValue((ArmorFocus > 0) ? ArmorFocus : Selected, out var variants))
			{
				return 0;
			}
			return variants.Count;
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
		CloseEnemyCard();
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
			ModMenu = (HelpOpen = false);
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
			foreach (int index in recipes)
			{
				Recipe recipe = Main.recipe[index];
				if (recipe.Disabled)
				{
					continue;
				}
				IEnumerable<string> materials = from i in recipe.requiredItem
					where !i.IsAir
					select GroupName(recipe, i, out var _) + " ×" + i.stack;
				List<string> stations = recipe.requiredTile.Where((int t) => t >= 0).Select((int t) =>
				{
					string text;
					if (!StationNames.TryGetValue(t, out var value))
					{
						text = TileLoader.GetTile(t)?.Name;
						if (text == null)
						{
							return "Tile " + t;
						}
					}
					else
					{
						text = value;
					}
					return text;
				}).ToList();
				// Row: stations as the title, the station's icon, conditions or the recipe's mod below; materials on hover.
				string conditionText = string.Join("; ", recipe.Conditions.Select((Condition c) => c.Description.Value));
				int stationItem = recipe.requiredTile.Where((int t) => t >= 0).Select((int t) => StationItems.TryGetValue(t, out var station) ? station.type : 0).FirstOrDefault((int t) => t > 0);
				Add(new BookAcquisitionEntry((stations.Count == 0) ? T("Вручную", "By hand") : string.Join(", ", stations), string.Join(", ", materials), BookAcquisitionKind.Craft, "", "×" + recipe.createItem.stack, (stationItem > 0) ? stationItem : type, 0, recipe.Conditions.Count > 0, index, Owner(recipe), conditionText));
			}
		}
		try
		{
			HashSet<IItemDropRule> own = new HashSet<IItemDropRule>(Main.ItemDropsDB.GetRulesForNPCID(0, includeGlobalDrops: false), ReferenceEqualityComparer.Instance);
			Drops(from rule in Main.ItemDropsDB.GetRulesForNPCID(0)
				where !own.Contains(rule)
				select rule, T("Любые враги", "Any enemy"), BookAcquisitionKind.Drop, 0, 0, "<global>");
			foreach (KeyValuePair<int, NPC> npc in ContentSamples.NpcsByNetId.OrderBy((KeyValuePair<int, NPC> x) => x.Key))
			{
				if (npc.Key != 0)
				{
					try
					{
						Drops(Main.ItemDropsDB.GetRulesForNPCID(npc.Key, includeGlobalDrops: false), npc.Value.FullName, BookAcquisitionKind.Drop, 0, npc.Key, npc.Value.ModNPC?.Mod.Name ?? "Terraria");
					}
					catch (Exception)
					{
						AcquisitionReadErrors++;
					}
				}
			}
			foreach (KeyValuePair<int, Item> item in ContentSamples.ItemsByType.OrderBy((KeyValuePair<int, Item> x) => x.Key))
			{
				if (!item.Value.IsAir)
				{
					try
					{
						Drops(Main.ItemDropsDB.GetRulesForItemID(item.Key), item.Value.Name, BookAcquisitionKind.Container, item.Key, 0, item.Value.ModItem?.Mod.Name ?? "Terraria");
					}
					catch (Exception)
					{
						AcquisitionReadErrors++;
					}
				}
			}
		}
		catch (Exception)
		{
			AcquisitionReadErrors++;
		}
		foreach (NPCShop shop in NPCShopDatabase.AllShops.OfType<NPCShop>())
		{
			foreach (NPCShop.Entry entry in shop.Entries)
			{
				if (entry.Disabled || entry.Item.type != type)
				{
					continue;
				}
				try
				{
					string[] conditions = entry.Conditions.Select((Condition c) => c.Description.Value).ToArray();
					Add(new BookAcquisitionEntry(Lang.GetNPCNameValue(shop.NpcType), string.Join("; ", conditions), BookAcquisitionKind.Shop, "", "", 0, shop.NpcType, conditions.Length != 0, -1, "Terraria", string.Join("; ", conditions), shop.Name));
				}
				catch (Exception)
				{
					AcquisitionReadErrors++;
				}
			}
		}
		if (reports.Count == 0)
		{
			Add(new BookAcquisitionEntry(T("Источники не найдены", "No known sources"), "", BookAcquisitionKind.Unknown));
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
		void Drops(IEnumerable<IItemDropRule> rules, string name, BookAcquisitionKind kind, int itemType = 0, int npcNetId = 0, string owner = "Terraria")
		{
			foreach (IItemDropRule rule in rules)
			{
				List<DropRateInfo> rates = new List<DropRateInfo>();
				try
				{
					rule.ReportDroprates(rates, new DropRateInfoChainFeed(1f));
				}
				catch (Exception)
				{
					AcquisitionReadErrors++;
					continue;
				}
				foreach (DropRateInfo rate in rates.Where((DropRateInfo r) => r.itemId == type))
				{
					List<string> conditions2 = new List<string>();
					List<string> conditionKeys = new List<string>();
					List<string> shortConditions = new List<string>();
					foreach (IItemDropRuleCondition condition in rate.conditions ?? new List<IItemDropRuleCondition>())
					{
						conditionKeys.Add(condition?.GetType().AssemblyQualifiedName ?? "<null>");
						try
						{
							string description = condition.GetConditionDescription();
							string full = (string.IsNullOrWhiteSpace(description) ? T("Особое условие", "Special condition") : description);
							conditions2.Add(full);
							shortConditions.Add(DropConditionLabel(condition, full));
						}
						catch (Exception)
						{
							string unavailable = T("Неизвестное условие", "Unknown condition");
							conditions2.Add(unavailable);
							shortConditions.Add(unavailable);
							AcquisitionReadErrors++;
						}
					}
					Add(new BookAcquisitionEntry(name, string.Join("; ", conditions2.Distinct()), kind, BookAcquisitionRules.Chance(rate.dropRate), "×" + BookAcquisitionRules.Stack(rate.stackMin, rate.stackMax), itemType, npcNetId, conditions2.Count > 0, -1, owner, string.Join("; ", shortConditions.Distinct()), BookAcquisitionGrouping.DropRuleKey(rate.dropRate, rate.stackMin, rate.stackMax, string.Join("|", conditionKeys.OrderBy((string x) => x, StringComparer.Ordinal)))));
				}
			}
		}
	}

	private static void OpenAcquisitionRecipe(int index)
	{
		if (index >= 0 && index < Recipe.numRecipes && Main.recipe[index] != null && !Main.recipe[index].Disabled && All.Contains(index))
		{
			int type = Main.recipe[index].createItem.type;
			CloseAcquisition();
			SampleType = type;
			Query = (ModFilter = "");
			StageFilter = BookStageFilter.All;
			Category = (CategoryRow = 0);
			FavoritesOnly = false;
			SwitchTab(1);
			Selected = index;
			int position = Filtered.IndexOf(index);
			Page = ((position >= 0) ? (position / 40) : 0);
			DetailsPage = 0;
		}
	}

	private static void BackAcquisitionGroup()
	{
		AcquisitionGroup = -1;
		AcquisitionRulePage = 0;
	}

	// Difficulty conditions get a one-word label; anything else keeps the mod's own description.
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
		if (EnemyOpen)
		{
			HandleEnemyInput(click, keys);
			return;
		}
		int scrollWheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		int delta = scrollWheelValue - LastWheel;
		LastWheel = scrollWheelValue;
		bool expanded = AcquisitionGroup >= 0;
		if (expanded && (Press(Keys.Escape) || (click && BookGeometry.AcquisitionBack.Contains(Mouse.X, Mouse.Y))))
		{
			BackAcquisitionGroup();
			return;
		}
		if (Press(Keys.Escape) || (click && (BookGeometry.AcquisitionClose.Contains(Mouse.X, Mouse.Y) || !BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y))))
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
				OpenEnemyCard(source);
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
			_ => T("Нет данных", "No data"),
		};
	}

	private static void DrawAcquisitionIcon(SpriteBatch sb, BookAcquisitionEntry source, BookRect rect)
	{
		NPC npc;
		if (source.ItemType > 0 && ContentSamples.ItemsByType.TryGetValue(source.ItemType, out var item))
		{
			Icon(sb, item, rect.X, rect.Y, rect.Width, tooltip: false);
		}
		else if (source.NpcNetId != 0 && ContentSamples.NpcsByNetId.TryGetValue(source.NpcNetId, out npc) && npc.type > 0 && npc.type < TextureAssets.Npc.Length)
		{
			Main.instance.LoadNPC(npc.type);
			Texture2D texture = TextureAssets.Npc[npc.type].Value;
			int frames = Math.Max(1, Main.npcFrameCount[npc.type]);
			Rectangle frame = new Rectangle(0, 0, texture.Width, Math.Max(1, texture.Height / frames));
			float scale = Math.Min(rect.Width / (float)frame.Width, rect.Height / (float)frame.Height);
			sb.Draw(texture, Origin + new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f) * Scale, frame, Color.White, 0f, frame.Size() / 2f, scale * Scale, SpriteEffects.None, 0f);
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
		return (withName ? (group.Source.Name + "\n") : "") + (CanOpenEnemy(group) ? T("Открыть карточку врага", "Open enemy card") : T("Показать условия", "Show conditions"));
	}

	private static void DrawAcquisition(SpriteBatch sb)
	{
		if (EnemyOpen)
		{
			DrawEnemyCard(sb);
			return;
		}
		Box(sb, 0f, 0f, 1120f, 696f, new Color(10, 16, 12, 235), border: false, 18f);
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
			DrawCardEmpty(sb, BookGeometry.AcquisitionList, T("Источники не найдены", "No known sources"), T("Попробуйте добычу блоков, сундуки или рыбалку", "Try mining, chests or fishing"));
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
		}
		DrawCardFooter(sb, page, BookAcquisitionRules.PageCount(count), (AcquisitionReadErrors > 0) ? T("Часть источников не прочитана", "Some sources could not be read") : "");
	}

	private static readonly Color HoverFill = new Color(51, 67, 55);

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
		sb.DrawString(FontAssets.MouseText.Value, line.Value, pos, Panel, 0f, Vector2.Zero, line.Scale * Scale, SpriteEffects.None, 0f);
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
			return T("Сет (база): ", "Set (base): ") + sum;
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
		return text + " · " + (NativeArmorParts(parts) ? T("Сет: ", "Set: ") : T("Сет (база): ", "Set (base): ")) + (ArmorPartsDefense(parts) + ArmorNativeSetBonus(parts));
	}

	private static string ArmorDefenseTooltip(Item item, Item[] displayedParts = null)
	{
		if (!HasArmorDefense(item))
		{
			return "";
		}
		string text = item.Name + "\n" + T("Защита этой части: ", "This piece's defense: ") + item.defense;
		BookArmorSets.Set set = BookArmorSets.Get(item.type);
		if (set != null)
		{
			Item[] parts = displayedParts ?? (from type in ArmorChoices(set, item)
				select ContentSamples.ItemsByType[type]).ToArray();
			text = text + "\n\n" + string.Join("\n", parts.Select((Item p) => p.Name + ": " + p.defense)) + "\n" + T("Сумма трёх частей: ", "Sum of the three pieces: ") + ArmorPartsDefense(parts);
			text = ((!NativeArmorParts(parts)) ? (text + "\n" + T("Показана базовая сумма. Дополнительная защита из бонуса модового сета здесь не рассчитывается.", "This is the base sum. Extra defense from the modded set bonus is not evaluated here.")) : (text + "\n" + T("Фиксированный бонус комплекта: +", "Fixed set bonus: +") + ArmorNativeSetBonus(parts) + "\n" + ArmorSetDefenseText(parts)));
		}
		return text + "\n\n" + T("Без аксессуаров, баффов и динамических эффектов модов.\nПредпросмотр не надевает броню и не меняет характеристики персонажа.", "Excludes accessories, buffs and dynamic mod effects.\nThis preview does not equip armor or change player stats.");
	}

	private static void DrawArmorDefense(SpriteBatch sb, Item item)
	{
		BookRect rect = BookGeometry.ArmorDefense;
		FixedLabel(sb, ArmorDefenseText(item), rect.X, rect.Y, Accent, 0.76f, rect.Width, rect.Height);
		if (!ModMenu && rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(ArmorDefenseTooltip(item));
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
		Item[] displayedParts = choices.Select((int type) => ContentSamples.ItemsByType[type]).ToArray();
		for (int slot = 0; slot < 3; slot++)
		{
			BookRect rect = BookGeometry.ArmorPart(slot);
			bool active = choices[slot] == chosen.type;
			Button(sb, rect, "", active);
			Item part = ContentSamples.ItemsByType[choices[slot]];
			Icon(sb, part, rect.X + 15f, rect.Y + 4f, 30f, tooltip: false);
			BookRect defense = BookGeometry.ArmorPartDefense(slot);
			CenterLabel(sb, part.defense + T(" защ.", " def"), defense.X, defense.Y, defense.Width, defense.Height, Accent, 0.62f, rightAligned: false, leftAligned: false, fitWidth: true);
			if (!ModMenu && rect.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(ArmorDefenseTooltip(part, displayedParts) + T("\nЛКМ — рецепт этой части сета", "\nLeft-click: inspect this set piece") + ((set.Parts[slot].Length > 1) ? (T("\nПовторный клик — следующий вариант (", "\nClick again for next variant (") + set.Parts[slot].Length + ")") : ""));
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
			if (BookGeometry.ArmorPart(slot).Contains(Mouse.X, Mouse.Y))
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
		bool storage = BookStorage.UseForCrafting;
		int availabilityVersion = BookCrafting.AvailabilityVersion;
		int storageVersion = (storage ? BookStorage.DataVersion : (-1));
		if (BatchEstimates.TryGetValue(recipe, out var cached) && cached.Storage == storage && cached.AvailabilityVersion == availabilityVersion && cached.StorageVersion == storageVersion && tick >= cached.Tick && tick - cached.Tick < 10)
		{
			return cached.Count;
		}
		int estimate = EstimateAvailableBatches(recipe);
		if (BatchEstimates.Count >= 128)
		{
			BatchEstimates.Clear();
		}
		BatchEstimates[recipe] = new BatchEstimate(estimate, tick, availabilityVersion, storageVersion, storage);
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
		Dictionary<int, int> counts = (BookStorage.UseForCrafting ? BookStorage.PreviewCounts(relevantTypes) : CountPlayerMaterials(relevantTypes));
		return BookBatchPlanner.MaxBatches(needs, counts, 100);
	}

	private static Dictionary<int, int> CountPlayerMaterials(IEnumerable<int> relevantTypes)
	{
		HashSet<int> relevant = new HashSet<int>(relevantTypes);
		Dictionary<int, int> counts = new Dictionary<int, int>();
		HashSet<Item> seen = new HashSet<Item>(ReferenceEqualityComparer.Instance);
		foreach (Item item in BookCrafting.Materials())
		{
			if (item != null && !item.IsAir && relevant.Contains(item.type) && seen.Add(item))
			{
				counts.TryGetValue(item.type, out var previous);
				counts[item.type] = (int)Math.Min(2147483647L, (long)previous + (long)item.stack);
			}
		}
		return counts;
	}

	private static bool HandleBatchChoiceClick(Recipe recipe)
	{
		for (int index = 0; index < BatchChoiceBounds.Length; index++)
		{
			if (BatchChoiceBounds[index].Contains(Mouse.X, Mouse.Y))
			{
				BatchChoice = index;
				BookCrafting.LastMessage = "";
				return true;
			}
		}
		return false;
	}

	private static void DrawBatchChoices(SpriteBatch sb, Recipe recipe)
	{
		Text(sb, T("Рецепт ", "Recipe ") + ((!Catalogue) ? 1 : (Variant + 1)) + "/" + VariantCount, 812f, 268f, Muted, 0.76f, 76f, 28f);
		string[] labels = new string[4]
		{
			"×1",
			"×5",
			"×10",
			T("Макс", "Max")
		};
		for (int index = 0; index < BatchChoiceBounds.Length; index++)
		{
			BookRect bounds = BatchChoiceBounds[index];
			Button(sb, bounds, labels[index], BatchChoice == index);
			if (bounds.Contains(Mouse.X, Mouse.Y))
			{
				int batches = index switch
				{
					0 => 1, 
					1 => 5, 
					2 => 10, 
					_ => Math.Max(1, CachedAvailableBatches(recipe)), 
				};
				string detail = T($"Создать до {batches} партий. Перед каждой партией проверяются курсор, состояние игрока и доступность рецепта.", $"Craft up to {batches} batches. Cursor, player state and native recipe availability are checked before each batch.");
				detail += T("\nМаксимум — оценка по материалам, до 100 партий за нажатие. Остатки показаны в строках материалов.", "\nMax is a materials estimate, capped at 100 batches per click. Material rows show projected leftovers.");
				MouseTip(detail);
			}
		}
	}

	internal static void QueueChainNavigation(bool back, bool forward)
	{
		if (Open && Chains && !Main.gameMenu)
		{
			PendingChainBack |= back;
			PendingChainForward |= forward;
		}
	}

	private static void ProcessChainNavigation()
	{
		bool back = PendingChainBack;
		bool forward = PendingChainForward;
		PendingChainBack = (PendingChainForward = false);
		if (back == forward || !Open || !Chains || Typing || HelpOpen || AcquisitionOpen || ModMenu || DraggingWindow || Main.blockInput || Main.drawingPlayerChat || Main.editSign || Main.editChest || PlayerInput.UsingGamepad)
		{
			return;
		}
		if (back)
		{
			GoToChain(ChainHistory.Count - 2);
			return;
		}
		int type = ChainHistory.Forward();
		if (type <= 0)
		{
			type = SelectedItem?.type ?? 0;
		}
		if (type > 0 && type != SampleType)
		{
			SetSample(type, append: true);
		}
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
		if (goal == ActiveGoal)
		{
			ActiveGoal = TrackedGoals.FirstOrDefault();
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
		return AddTrackerGoal(new Recipe[1] { recipe }, new Item[1] { recipe.createItem }, "recipe:" + recipe.RecipeIndex, MaterialRequirements(recipe), PreviewBatchCount(recipe));
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
			Storage = BookStorage.UseForCrafting,
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
				BookCrafting.LastMessage = T("У части сета нет рецепта: ", "No recipe for set piece: ") + part.Name;
				return false;
			}
			recipes.Add(recipe);
		}
		List<MaterialRequirement> raw = recipes.SelectMany(MaterialRequirements).ToList();
		BookBatchNeed[] merged;
		try
		{
			merged = BookTrackerRules.MergeNeeds(raw.Select((MaterialRequirement m) => new BookBatchNeed(m.Ingredient.stack, m.ItemTypes)));
		}
		catch (ArgumentOutOfRangeException)
		{
			BookCrafting.LastMessage = T("Слишком большое количество материалов сета.", "Set material amount exceeds supported bounds.");
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
			materials.Add(new MaterialRequirement(item, first.Group, first.Name, need.AcceptedTypes.ToHashSet(), Overlapping: false));
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
			Dictionary<int, int> counts = (BookStorage.UseForCrafting ? BookStorage.PreviewCounts(types) : CountPlayerMaterials(types));
			batches = Math.Max(1, BookBatchPlanner.MaxBatches(merged, counts, 100));
		}
		return AddTrackerGoal(recipes.ToArray(), parts, SetGoalKey(parts), materials, batches);
	}

	public override void PostUpdatePlayers()
	{
		if (Main.dedServ || Main.gameMenu || TrackedGoals.Count == 0 || Main.LocalPlayer == null || !Main.LocalPlayer.active || Main.LocalPlayer.dead || Main.LocalPlayer.ghost)
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
		if (goal.Storage)
		{
			BookStorage.Tick(manageFocus: false);
		}
		BookBatchNeed[] needs = goal.Materials.Select((MaterialRequirement m) => new BookBatchNeed(m.Ingredient.stack, m.ItemTypes)).ToArray();
		int[] types = needs.SelectMany((BookBatchNeed n) => n.AcceptedTypes).Distinct().ToArray();
		goal.Unknown = goal.Storage && (!BookStorage.CraftingConnected || !BookStorage.CraftReady);
		goal.Counts = (goal.Storage ? BookStorage.PreviewCounts(types) : CountPlayerMaterials(types));
		if (goal.Storage && !goal.Unknown)
		{
			int[] array = types;
			foreach (int type in array)
			{
				if (BookStorage.IsInfinite(new int[1] { type }))
				{
					goal.Counts[type] = int.MaxValue;
				}
			}
		}
		goal.Unknown |= goal.Counts.Values.Any((int n) => n < 0);
		goal.Ready = !goal.Unknown && BookTrackerRules.Ready(needs, goal.Counts, goal.Batches);
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
		bool flag = parts != null;
		bool tracked = recipe != null && TrackedGoals.Any((TrackedGoal g) => g.Key == "recipe:" + recipe.RecipeIndex);
		bool full = !ResourceTrackerStyle && TrackedGoals.Count >= 5;
		BookRect single = (flag ? BookGeometry.TrackSingleAction : BookGeometry.TrackAction);
		SpriteBatch sb2 = sb;
		BookRect rect = single;
		string label;
		if (tracked)
		{
			label = T("Убрать цель", "Remove goal");
		}
		else
		{
			label = (full ? T("Лимит: 5/5", "Limit: 5/5") : T("Начать крафт", "Start crafting"));
		}
		Button(sb2, rect, label, tracked);
		if (flag)
		{
			bool setTracked = TrackedGoals.Any((TrackedGoal g) => g.Key == SetGoalKey(parts));
			Button(sb, BookGeometry.TrackSetAction, setTracked ? T("Снять сет", "Unpin set") : T("Весь сет", "Full set"), setTracked);
			if (BookGeometry.TrackSetAction.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Отслеживать три части как одну цель, с общими материалами.\nИспользуется выбранный вариант брони. Для остальных частей — первый включённый рецепт того же источника, иначе первый доступный.\nКоличество: ×1/×5/×10; Макс считается по всему сету. Готовые части брони не вычитаются из плана.", "Track all three pieces as one goal with summed materials.\nUses the chosen armor variant. Other pieces use the first enabled recipe from the same owner, or the first available recipe.\nQuantity: ×1/×5/×10; Max uses the whole set. Existing armor pieces are not subtracted."));
			}
		}
		if (single.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip((recipe == null) ? T("Для отслеживания нужен обычный рецепт", "An ordinary recipe is needed for tracking") : (T("Целей: ", "Goals: ") + TrackedGoals.Count + (ResourceTrackerStyle ? "\n" : "/5\n") + T("Добавляет цель в панель. Перетаскивайте заголовок. Размер и прозрачность — в настройках мода.\n", "Adds a panel objective. Drag its header. Size and opacity are in mod settings.\n") + T("Карточки: цели независимы. Стиль «только ресурсы»: потребности суммируются, сигнал — за весь список.\nМатериалы не резервируются и не расходуются. Крафт остаётся ручным; станции и условия проверяются отдельно.", "Cards: goals are independent. Resources-only style: needs are summed, with one sound for the entire list.\nMaterials are not reserved or consumed. Craft manually; stations and conditions are separate.")));
		}
	}

	private static bool HandleTrackButton()
	{
		if (!BookGeometry.TrackAction.Contains(Mouse.X, Mouse.Y))
		{
			return false;
		}
		Recipe recipe = SelectedRecipe;
		Item[] parts = SelectedSetParts();
		bool wholeSet = parts != null && BookGeometry.TrackSetAction.Contains(Mouse.X, Mouse.Y);
		string key;
		if (wholeSet)
		{
			key = SetGoalKey(parts);
		}
		else
		{
			key = ((recipe == null) ? "" : ("recipe:" + recipe.RecipeIndex));
		}
		TrackedGoal existing = TrackedGoals.FirstOrDefault((TrackedGoal g) => g.Key == key);
		if (existing != null)
		{
			RemoveGoal(existing);
			return true;
		}
		if (!ResourceTrackerStyle && TrackedGoals.Count >= 5)
		{
			BookCrafting.LastMessage = T("В стиле карточек — до 5 целей. Включите «только ресурсы» для списка без лимита.", "Cards support up to 5 goals. Enable resources-only style for an uncapped list.");
			return true;
		}
		if (wholeSet ? StartSetTracker(parts) : StartTracker(recipe))
		{
			BlockTrackerPress = true;
			CloseForStorage();
		}
		return true;
	}

	private static void InsertTrackerLayer(List<GameInterfaceLayer> layers)
	{
		if (TrackedGoals.Count != 0 && !Open && !Main.gameMenu)
		{
			int index = layers.FindIndex((GameInterfaceLayer l) => l.Name == "Vanilla: Mouse Text");
			if (index < 0)
			{
				index = layers.Count;
			}
			layers.Insert(index, new LegacyGameInterfaceLayer("ModernRecipeBook: Craft tracker", () =>
			{
				SafeDrawTracker(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}
	}

	private static BookGeometry TrackerLayout()
	{
		return BookTrackerRules.PanelLayoutHeight(TrackerPanelHeight, Main.screenWidth, Main.screenHeight, TrackerCustomPosition, TrackerPositionX, TrackerPositionY, ModContent.GetInstance<BookConfig>().TrackerScalePercent, Main.UIScale);
	}

	private static BookRect TrackerGoalClose(BookRect row)
	{
		return new BookRect(272f, row.Y + 10f, 20f, 20f);
	}

	private static void HandleTrackerInput()
	{
		if (!Main.mouseLeft)
		{
			BlockTrackerPress = false;
		}
		bool click = Main.mouseLeft && !TrackerLastLeft;
		TrackerLastLeft = Main.mouseLeft;
		if (Open || TrackedGoals.Count == 0 || Main.gameMenu || Main.LocalPlayer.dead || Main.LocalPlayer.ghost)
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
		(float, float) mouse = layout.ToLocal(Main.MouseScreen.X, Main.MouseScreen.Y);
		if (!new BookRect(0f, 0f, 300f, height).Contains(mouse.Item1, mouse.Item2))
		{
			return;
		}
		Main.LocalPlayer.mouseInterface = true;
		PlayerInputLockTracker();
		if (Main.mouseLeft)
		{
			BlockTrackerPress = true;
		}
		if (!click)
		{
			return;
		}
		if (new BookRect(272f, 4f, 20f, 20f).Contains(mouse.Item1, mouse.Item2))
		{
			StopTracker();
			return;
		}
		if (new BookRect(0f, 0f, 264f, 28f).Contains(mouse.Item1, mouse.Item2))
		{
			TrackerDragging = true;
			TrackerDragX = Main.MouseScreen.X - layout.Left;
			TrackerDragY = Main.MouseScreen.Y - layout.Top;
			return;
		}
		if (ResourceTrackerStyle)
		{
			HandleResourceTrackerInput(mouse.Item1, mouse.Item2);
			return;
		}
		for (int i = 0; i < TrackedGoals.Count; i++)
		{
			BookRect row = BookTrackerRules.GoalRow(TrackerRows, TrackerActiveIndex, i);
			if (TrackerGoalClose(row).Contains(mouse.Item1, mouse.Item2))
			{
				RemoveGoal(TrackedGoals[i]);
				return;
			}
			if (row.Contains(mouse.Item1, mouse.Item2))
			{
				ActiveGoal = TrackedGoals[i];
				return;
			}
		}
		if (ActiveGoal != null && ActiveGoal.Materials.Count > 5)
		{
			float pagerY = BookTrackerRules.GoalRow(TrackerRows, TrackerActiveIndex, TrackerActiveIndex).Y + 40f + (float)BookTrackerRules.DetailHeight(ActiveGoal.Materials.Count) - 24f;
			if (new BookRect(8f, pagerY, 24f, 20f).Contains(mouse.Item1, mouse.Item2))
			{
				ActiveGoal.Page = Math.Max(0, ActiveGoal.Page - 1);
			}
			if (new BookRect(268f, pagerY, 24f, 20f).Contains(mouse.Item1, mouse.Item2))
			{
				ActiveGoal.Page = Math.Min(BookTrackerRules.PageCount(ActiveGoal.Materials.Count) - 1, ActiveGoal.Page + 1);
			}
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

	private static void DrawTracker(SpriteBatch sb)
	{
		if (TrackedGoals.Count == 0 || Main.gameMenu || Main.LocalPlayer.dead || Main.LocalPlayer.ghost)
		{
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
			int[] rows = TrackerRows;
			int active = TrackerActiveIndex;
			int height = BookTrackerRules.PanelHeight(rows, active);
			float opacity = (float)Math.Clamp(ModContent.GetInstance<BookConfig>().TrackerOpacityPercent, 20, 100) / 100f;
			if (ResourceTrackerStyle)
			{
				DrawResourceTracker(sb, opacity);
				return;
			}
			GraphicsOpacity = opacity * ((TrackedGoals.Count == 1) ? (1f - TrackedGoals[0].Dismiss.Progress) : 1f);
			Box(sb, 0f, 0f, 300f, height, new Color(25, 32, 28), border: true, 12f, Border);
			Text(sb, T("КРАФТ · ", "CRAFT · ") + TrackedGoals.Count + "/5", 8f, 3f, Muted, 0.7f, 252f, 20f);
			Button(sb, new BookRect(272f, 4f, 20f, 20f), "X");
			if (Hit(0f, 0f, 264f, 28f))
			{
				MouseTip(T("Перетащите заголовок, чтобы переместить панель.\nРазмер и прозрачность: настройки Modern Recipe Book.", "Drag the header to move this panel.\nSize and opacity: Modern Recipe Book settings."));
			}
			if (Hit(272f, 4f, 20f, 20f))
			{
				MouseTip(T("Убрать все цели", "Remove all objectives"));
			}
			for (int i = 0; i < TrackedGoals.Count; i++)
			{
				TrackedGoal goal = TrackedGoals[i];
				BookRect row = BookTrackerRules.GoalRow(rows, active, i);
				GraphicsOpacity = opacity * (1f - goal.Dismiss.Progress);
				if (i == active)
				{
					Box(sb, 4f, row.Y, 292f, 40f, Raised, border: true, 8f, goal.Ready ? Accent : Border);
				}
				if (goal.IsSet)
				{
					for (int part = 0; part < 3; part++)
					{
						Icon(sb, goal.Parts[part], 8 + part * 24, row.Y + 8f, 24f, tooltip: false);
					}
				}
				else
				{
					Icon(sb, goal.Recipe.createItem, 8f, row.Y + 6f, 28f, tooltip: false);
				}
				float textX = (goal.IsSet ? 84 : 44);
				FixedLabel(sb, goal.IsSet ? T("Комплект брони", "Armor set") : goal.Recipe.createItem.Name, textX, row.Y + 2f, TextColor, 0.78f, 264f - textX, 20f);
				int collected = goal.Materials.Count((MaterialRequirement m) => !goal.Unknown && ((IEnumerable<int>)m.ItemTypes).Sum((Func<int, long>)((int type) => Math.Max(0, goal.Counts.GetValueOrDefault(type)))) >= (long)m.Ingredient.stack * (long)goal.Batches);
				string summary;
				if (goal.Unknown)
				{
					summary = "?";
				}
				else
				{
					summary = (goal.Ready ? T("Готово!", "Ready!") : (collected + "/" + goal.Materials.Count));
				}
				Text(sb, "×" + goal.Batches + " · " + summary, textX, row.Y + 22f, goal.Ready ? Accent : Muted, 0.64f, 264f - textX, 16f);
				Button(sb, TrackerGoalClose(row), "X");
				if (Hit(8f, row.Y, 256f, 40f))
				{
					Main instance = Main.instance;
					string text = string.Join("\n", goal.Parts.Select((Item item) => item.Name));
					string? text2;
					if (goal.IsSet)
					{
						text2 = "\n" + ArmorSetDefenseText(goal.Parts);
					}
					else
					{
						text2 = (HasArmorDefense(goal.Parts[0]) ? ("\n" + T("Защита: ", "Defense: ") + goal.Parts[0].defense) : "");
					}
					instance.MouseText(text + text2 + T("\nНажмите, чтобы раскрыть материалы. Цели независимы: общие материалы не резервируются.", "\nClick to expand materials. Goals are independent: shared materials are not reserved."), 0, 0);
				}
				if (i == active)
				{
					DrawGoalMaterials(sb, goal, row.Y + 40f);
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

	private static void DrawGoalMaterials(SpriteBatch sb, TrackedGoal goal, float y)
	{
		string status;
		if (goal.Unknown)
		{
			status = T("Недоступно", "Unavailable");
		}
		else
		{
			status = (goal.Ready ? T("Готово! Крафт — вручную.", "Ready! Craft manually.") : T("Сбор материалов", "Collecting materials"));
		}
		Text(sb, (goal.Storage ? "Magic Storage" : T("Инвентарь", "Inventory")) + " · " + status, 8f, y + 1f, goal.Ready ? Accent : Muted, 0.65f, 284f, 18f);
		goal.Page = Math.Clamp(goal.Page, 0, BookTrackerRules.PageCount(goal.Materials.Count) - 1);
		for (int row = 0; row < 5; row++)
		{
			int n = goal.Page * 5 + row;
			if (n >= goal.Materials.Count)
			{
				break;
			}
			MaterialRequirement material = goal.Materials[n];
			float itemY = y + 20f + (float)(row * 24);
			long owned = ((IEnumerable<int>)material.ItemTypes).Sum((Func<int, long>)((int t) => Math.Max(0, goal.Counts.GetValueOrDefault(t))));
			long needed = (long)material.Ingredient.stack * (long)goal.Batches;
			bool infinite = goal.Storage && !goal.Unknown && BookStorage.IsInfinite(material.ItemTypes);
			bool met = !goal.Unknown && (infinite || owned >= needed);
			string ratio = (goal.Unknown ? ("?/" + Count(needed)) : ((infinite ? "∞" : Count(owned)) + "/" + Count(needed)));
			Icon(sb, material.Ingredient, 8f, itemY, 20f, tooltip: false);
			Text(sb, material.Name, 36f, itemY, TextColor, 0.7f, 174f, 22f);
			CenterLabel(sb, ratio, 216f, itemY, 76f, 22f, met ? Accent : new Color(238, 184, 139), 0.68f, rightAligned: true);
			if (Hit(8f, itemY, 284f, 24f))
			{
				MouseTip(material.Name + "\n" + ratio + (material.Overlapping ? T("\nГруппы пересекаются; готовность рассчитана без двойного учёта.", "\nGroups overlap; readiness prevents double counting.") : ""));
			}
		}
		if (goal.Materials.Count > 5)
		{
			float pagerY = y + (float)BookTrackerRules.DetailHeight(goal.Materials.Count) - 24f;
			Button(sb, new BookRect(8f, pagerY, 24f, 20f), "<");
			Button(sb, new BookRect(268f, pagerY, 24f, 20f), ">");
			CenterLabel(sb, goal.Page + 1 + " / " + BookTrackerRules.PageCount(goal.Materials.Count), 40f, pagerY, 220f, 20f, Muted, 0.68f);
		}
	}

	private static bool CanOpenEnemy(BookAcquisitionGroup group)
	{
		if (group.Source.Kind == BookAcquisitionKind.Drop && group.Source.NpcNetId != 0)
		{
			return ContentSamples.NpcsByNetId.ContainsKey(group.Source.NpcNetId);
		}
		return false;
	}

	private static void CloseEnemyCard()
	{
		EnemyOpen = false;
		EnemyVariants = Array.Empty<int>();
		EnemyVariant = (EnemyPage = (EnemyReadErrors = 0));
		EnemyLoot.Clear();
		EnemyBiomes = (EnemySpawn = "");
	}

	private static void OpenEnemyCard(BookAcquisitionGroup group)
	{
		if (CanOpenEnemy(group))
		{
			EnemyVariants = BookEnemyRules.Variants(group.Source.NpcNetId, from x in @group.Rules.SelectMany((BookAcquisitionRule x) => x.Reports)
				select x.NpcNetId).Where(ContentSamples.NpcsByNetId.ContainsKey).ToArray();
			if (EnemyVariants.Length != 0)
			{
				EnemyOpen = true;
				EnemyVariant = 0;
				ReadEnemyCard();
			}
		}
	}

	private static void ReadEnemyCard()
	{
		EnemyPage = (EnemyReadErrors = 0);
		int id = EnemyVariants[EnemyVariant];
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
		List<BookEnemyDrop> drops = new List<BookEnemyDrop>();
		try
		{
			foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForNPCID(id))
			{
				List<DropRateInfo> rates = new List<DropRateInfo>();
				try
				{
					rule.ReportDroprates(rates, new DropRateInfoChainFeed(1f));
				}
				catch (Exception)
				{
					EnemyReadErrors++;
					continue;
				}
				foreach (DropRateInfo rate in rates)
				{
					if (!ContentSamples.ItemsByType.TryGetValue(rate.itemId, out var item) || item.IsAir)
					{
						continue;
					}
					List<string> conditions = new List<string>();
					List<string> keys = new List<string>();
					foreach (IItemDropRuleCondition condition in rate.conditions ?? new List<IItemDropRuleCondition>())
					{
						keys.Add(condition?.GetType().AssemblyQualifiedName ?? "<null>");
						try
						{
							string description = condition.GetConditionDescription();
							conditions.Add(DropConditionLabel(condition, string.IsNullOrWhiteSpace(description) ? T("Особое условие", "Special condition") : description));
						}
						catch (Exception)
						{
							conditions.Add(T("Неизвестное условие", "Unknown condition"));
							EnemyReadErrors++;
						}
					}
					drops.Add(new BookEnemyDrop(rate.itemId, rate.dropRate, rate.stackMin, rate.stackMax, string.Join("; ", conditions.Distinct(StringComparer.Ordinal)), string.Join("|", keys.OrderBy((string x) => x, StringComparer.Ordinal))));
				}
			}
		}
		catch (Exception)
		{
			EnemyReadErrors++;
		}
		EnemyLoot = BookEnemyRules.Group(drops).OrderBy((BookEnemyLoot x) => ContentSamples.ItemsByType[x.ItemType].Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		if (EnemyReadErrors > 0)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn($"Enemy card for NPC net ID {id}: {EnemyReadErrors} reports/tags could not be read.");
		}
	}

	// Esc and the back button return to How to Get; X and a click outside close the whole card.
	private static void HandleEnemyInput(bool click, KeyboardState keys)
	{
		int scrollWheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		int delta = scrollWheelValue - LastWheel;
		LastWheel = scrollWheelValue;
		if (Press(Keys.Escape) || (click && BookGeometry.AcquisitionBack.Contains(Mouse.X, Mouse.Y)))
		{
			CloseEnemyCard();
			return;
		}
		if (click && (BookGeometry.AcquisitionClose.Contains(Mouse.X, Mouse.Y) || !BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y)))
		{
			CloseAcquisition();
			return;
		}
		int step = ((EnemyVariants.Length > 1) ? ((click && BookGeometry.AcquisitionVariantPrevious.Contains(Mouse.X, Mouse.Y)) ? (-1) : ((click && BookGeometry.AcquisitionVariantNext.Contains(Mouse.X, Mouse.Y)) ? 1 : 0)) : 0);
		if (step != 0)
		{
			int next = Math.Clamp(EnemyVariant + step, 0, EnemyVariants.Length - 1);
			if (next != EnemyVariant)
			{
				EnemyVariant = next;
				ReadEnemyCard();
			}
			return;
		}
		int pageStep = ((Press(Keys.PageUp) || (delta > 0 && BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y)) || (click && BookGeometry.AcquisitionPrevious.Contains(Mouse.X, Mouse.Y))) ? (-1) : ((Press(Keys.PageDown) || (delta < 0 && BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y)) || (click && BookGeometry.AcquisitionNext.Contains(Mouse.X, Mouse.Y))) ? 1 : 0));
		EnemyPage = BookEnemyRules.ClampPage(EnemyPage + pageStep, EnemyLoot.Count);
		if (Press(Keys.Home))
		{
			EnemyPage = 0;
		}
		if (Press(Keys.End))
		{
			EnemyPage = BookEnemyRules.PageCount(EnemyLoot.Count) - 1;
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

	private static string EnemyLootTooltip(BookEnemyLoot loot)
	{
		return string.Join("\n", loot.Rules.Select((BookEnemyDrop x) => BookAcquisitionRules.Chance(x.Chance) + " · ×" + BookAcquisitionRules.Stack(x.Min, x.Max) + " · " + ((x.Conditions.Length > 0) ? x.Conditions : T("без условий", "no conditions"))));
	}

	private static void DrawEnemyCard(SpriteBatch sb)
	{
		Box(sb, 0f, 0f, 1120f, 696f, new Color(10, 16, 12, 235), border: false, 18f);
		BookRect panel = BookGeometry.AcquisitionPanel;
		Box(sb, panel, Surface, border: true, 12f);
		NPC npc = ContentSamples.NpcsByNetId[EnemyVariants[EnemyVariant]];
		DrawAcquisitionIcon(sb, new BookAcquisitionEntry(npc.FullName, "", BookAcquisitionKind.Drop, "", "", 0, EnemyVariants[EnemyVariant]), BookGeometry.EnemyPortrait);
		string kind = (npc.friendly ? T("Существо", "Creature") : (npc.boss ? T("Босс", "Boss") : T("Враг", "Enemy")));
		DrawCardHeader(sb, back: true, npc.FullName, kind + " · " + ModName(npc.ModNPC?.Mod.Name ?? "Terraria"), EnemyVariant, EnemyVariants.Length, T("Варианты этого врага", "Variants of this enemy"));
		BookRect tags = BookGeometry.EnemyTags;
		Box(sb, tags, Raised, border: false, 8f);
		Text(sb, T("Биом", "Biome"), tags.X + 12f, tags.Y + 8f, Muted, 0.8f, 96f, 22f);
		Text(sb, (EnemyBiomes.Length > 0) ? EnemyBiomes : T("Не указан", "Not listed"), tags.X + 116f, tags.Y + 8f, TextColor, 0.85f, tags.Width - 128f, 22f);
		Text(sb, T("Появление", "Spawns"), tags.X + 12f, tags.Y + 34f, Muted, 0.8f, 96f, 22f);
		Text(sb, (EnemySpawn.Length > 0) ? EnemySpawn : T("Не указано", "Not listed"), tags.X + 116f, tags.Y + 34f, TextColor, 0.85f, tags.Width - 128f, 22f);
		if (tags.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("По данным бестиария; может быть неполно", "From the Bestiary; may be incomplete"));
		}
		BookRect lootLabel = BookGeometry.EnemyLootLabel;
		Text(sb, T("ДОБЫЧА", "LOOT") + " · " + EnemyLoot.Count, lootLabel.X, lootLabel.Y, Muted, 0.8f, lootLabel.Width, lootLabel.Height);
		if (EnemyLoot.Count == 0)
		{
			BookRect first = BookGeometry.EnemyLootRow(0);
			CenterLabel(sb, T("Добыча не указана", "No loot listed"), first.X, first.Y + 64f, first.Width, 28f, TextColor);
		}
		for (int row = 0; row < 5; row++)
		{
			int index = EnemyPage * 5 + row;
			if (index >= EnemyLoot.Count)
			{
				break;
			}
			BookEnemyLoot loot = EnemyLoot[index];
			Item item = ContentSamples.ItemsByType[loot.ItemType];
			BookRect rect = BookGeometry.EnemyLootRow(row);
			Box(sb, rect, Raised, border: true, 8f);
			Icon(sb, item, rect.X + 8f, rect.Y + 8f, 32f, tooltip: false);
			BookRect chance = BookGeometry.EnemyLootChance(row);
			bool varies = loot.Rules.Length > 1;
			string detail = (varies ? T("Зависит от условий", "Varies by condition") : loot.Rules[0].Conditions);
			float nameWidth = chance.X - rect.X - 64f;
			if (detail.Length > 0)
			{
				Text(sb, item.Name, rect.X + 52f, rect.Y + 4f, TextColor, 0.95f, nameWidth, 22f);
				Text(sb, detail, rect.X + 52f, rect.Y + 26f, Muted, 0.85f, nameWidth, 18f);
			}
			else
			{
				Text(sb, item.Name, rect.X + 52f, rect.Y + 12f, TextColor, 0.95f, nameWidth, 24f);
			}
			AcquisitionBadge(sb, chance, varies ? "" : BookAcquisitionRules.Chance(loot.Rules[0].Chance));
			AcquisitionBadge(sb, BookGeometry.EnemyLootAmount(row), varies ? "" : loot.Amount);
			if (varies && rect.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(item.Name + "\n" + EnemyLootTooltip(loot));
			}
		}
		DrawCardFooter(sb, EnemyPage, BookEnemyRules.PageCount(EnemyLoot.Count), (EnemyReadErrors > 0) ? T("Часть данных не прочитана", "Some data could not be read") : "");
	}

	// An Unknown stage is either a source the current difficulty rules out or no regular source at all.
	private static string StageName(Item item, Recipe recipe)
	{
		BookStage stage = (recipe != null) ? BookProgression.RecipeStage(recipe.RecipeIndex) : BookProgression.ItemStage(item.type);
		bool modeExcluded = (recipe != null) ? BookProgression.RecipeModeExcluded(recipe.RecipeIndex) : BookProgression.ItemModeExcluded(item.type);
		return (stage == BookStage.Unknown && modeExcluded) ? T("Недоступно в этом режиме", "Not available at this difficulty") : StageName(stage);
	}

	private static string StageName(BookStage stage)
	{
		return stage switch
		{
			BookStage.PreHardmode => T("Пре-хардмод", "Pre-hardmode"), 
			BookStage.Hardmode => T("Хардмод", "Hardmode"), 
			BookStage.PostHardmode => T("Постхардмод", "Post-hardmode"), 
			_ => T("Нет обычного способа получения", "No regular source"), 
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
		BookSkin.Panel(Main.spriteBatch, new Rectangle(x - 8, y - 6, (int)size.X + 16, (int)size.Y + 8), Panel * 0.96f, 8f, Border);
		Main.instance.MouseText(wrapped, 0, 0);
	}

	private static void DrawStageFilter(SpriteBatch sb)
	{
		Button(sb, BookGeometry.StageFilter, (StageFilter == BookStageFilter.All) ? T("Этап: Все", "Stage: All") : StageName((BookStage)(StageFilter - 1)), StageFilter != BookStageFilter.All);
		if (BookGeometry.StageFilter.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Нажмите: Все → Пре-хардмод → Хардмод → Постхардмод.\nПостхардмод — после Лунного лорда. Этап — самый ранний момент, когда предмет можно получить: рецепт по цепочке материалов, станций и условий, лут боссов и мешков, магазины жителей, ящики и враги, включая Calamity.\nРежим мира учитывается: Classic, Expert, Master и Revengeance. То, что в этом режиме не выпадает, подписано «Недоступно в этом режиме», а то, что нельзя получить обычным путём, — «Нет обычного способа получения»; они видны только в «Все». Если источник не найден, этап оценивается по редкости. В цепочках неопределённые рецепты помечены ?.", "Click: All → Pre-hardmode → Hardmode → Post-hardmode.\nPost-hardmode means after Moon Lord. The stage is the earliest point an item can be obtained: a recipe through its materials, stations and conditions, boss and bag loot, town shops, crates and enemies, Calamity included.\nThe world's difficulty counts: Classic, Expert, Master and Revengeance. What does not drop in this mode is marked “Not available at this difficulty”, what cannot be obtained the regular way “No regular source”; both stay visible only in All. Without a known source the stage is estimated from rarity. Uncertain chain recipes carry a ? marker."));
		}
	}

	private static string StageExplanation(Item item, Recipe recipe)
	{
		return StageName(item, recipe) + T("\nСамый ранний способ получить предмет. Постхардмод = после Лунного лорда.\nДля каталога — лучший из рецептов, лута, магазинов и ящиков; для рецепта — его материалы, станция и условия. Режим мира учитывается.\nНестандартные источники других модов могут быть неизвестны, тогда этап оценивается по редкости.", "\nEarliest way to obtain the item. Post-hardmode = after Moon Lord.\nCatalogue: the best of recipes, loot, shops and crates; recipe: its own materials, station and conditions. The world's difficulty counts.\nUnusual sources from other mods may be unknown; the stage is then estimated from rarity.");
	}

	private static void ResetResourceTracker()
	{
		ResourceTrackerSignal.Reset();
		ResourceTrackerDismiss.Reset();
		ResourceTrackerPage = 0;
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
				string key = BookResourceTrackerRules.Key(goal.Storage, material.ItemTypes);
				ResourceTrackerMaterials.TryAdd(key, material);
				raw.Add(new BookResourceNeed(goal.Storage, (long)material.Ingredient.stack * (long)goal.Batches, material.ItemTypes));
			}
		}
		BookResourceNeed[] needs = BookResourceTrackerRules.Merge(raw);
		Dictionary<int, long> inventory = new Dictionary<int, long>();
		Dictionary<int, long> storage = new Dictionary<int, long>();
		foreach (TrackedGoal goal2 in TrackedGoals)
		{
			foreach (KeyValuePair<int, int> pair in goal2.Counts)
			{
				Dictionary<int, long> pool = (goal2.Storage ? storage : inventory);
				pool[pair.Key] = Math.Max(pool.GetValueOrDefault(pair.Key), Math.Max(0, pair.Value));
			}
		}
		bool unknown = TrackedGoals.Any((TrackedGoal g) => g.Storage && g.Unknown);
		HashSet<int> infinite = new HashSet<int>();
		if (!unknown && TrackedGoals.Any((TrackedGoal g) => g.Storage))
		{
			foreach (int type in needs.Where((BookResourceNeed n) => n.Storage).SelectMany((BookResourceNeed n) => n.Types).Distinct())
			{
				if (BookStorage.IsInfinite(new int[1] { type }))
				{
					infinite.Add(type);
				}
			}
		}
		long totalDemand = needs.Sum((BookResourceNeed n) => n.Quantity);
		foreach (int type2 in infinite)
		{
			storage[type2] = totalDemand;
		}
		ResourceTrackerRows = BookResourceTrackerRules.Assess(needs, inventory, storage, unknown, infinite);
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

	private static void HandleResourceTrackerInput(float x, float y)
	{
		int pages = BookResourceTrackerRules.PageCount(ResourceTrackerRows.Length);
		if (pages > 1)
		{
			if (new BookRect(8f, ResourcePagerY, 24f, 20f).Contains(x, y))
			{
				ResourceTrackerPage = Math.Max(0, ResourceTrackerPage - 1);
			}
			if (new BookRect(268f, ResourcePagerY, 24f, 20f).Contains(x, y))
			{
				ResourceTrackerPage = Math.Min(pages - 1, ResourceTrackerPage + 1);
			}
		}
	}

	private static void DrawResourceTracker(SpriteBatch sb, float opacity)
	{
		GraphicsOpacity = opacity * (1f - ResourceTrackerDismiss.Progress);
		int pages = BookResourceTrackerRules.PageCount(ResourceTrackerRows.Length);
		ResourceTrackerPage = Math.Clamp(ResourceTrackerPage, 0, pages - 1);
		int complete = ResourceTrackerRows.Count((BookResourceProgress r) => r.Met);
		FixedLabel(sb, T("РЕСУРСЫ · ", "RESOURCES · ") + complete + "/" + ResourceTrackerRows.Length, 8f, 3f, Muted, 0.7f, 252f, 20f);
		CenterLabel(sb, "X", 272f, 4f, 20f, 20f, TextColor, 0.75f);
		if (Hit(0f, 0f, 264f, 28f))
		{
			MouseTip(T("Перетащите список. Стиль, размер и прозрачность — в настройках.\nЗвук — только когда собран весь список. Крафт остаётся ручным.", "Drag this list. Style, size and opacity are in settings.\nOne sound only when the entire list is collected. Craft manually."));
		}
		if (Hit(272f, 4f, 20f, 20f))
		{
			MouseTip(T("Убрать все цели", "Remove all objectives"));
		}
		if (ResourceTrackerRows.Length == 0)
		{
			FixedLabel(sb, T("Нет требуемых ресурсов", "No resources required"), 8f, 32f, ResourceCompleteColor, 0.72f, 284f, 24f);
		}
		bool mixed = ResourceTrackerRows.Any((BookResourceProgress r) => r.Need.Storage) && ResourceTrackerRows.Any((BookResourceProgress r) => !r.Need.Storage);
		for (int line = 0; line < 8; line++)
		{
			int i = ResourceTrackerPage * 8 + line;
			if (i >= ResourceTrackerRows.Length)
			{
				break;
			}
			BookResourceProgress row = ResourceTrackerRows[i];
			MaterialRequirement material = ResourceTrackerMaterials[BookResourceTrackerRules.Key(row.Need.Storage, row.Need.Types)];
			float y = 32 + line * 26;
			Color color = (row.Met ? ResourceCompleteColor : TextColor);
			string ratio = (row.Unknown ? ("?/" + Count(row.Need.Quantity)) : ((row.Infinite ? "∞" : Count(row.Allocated)) + "/" + Count(row.Need.Quantity)));
			string name = material.Name;
			string text;
			if (mixed)
			{
				text = (row.Need.Storage ? " · MS" : T(" · ИНВ", " · INV"));
			}
			else
			{
				text = "";
			}
			string name2 = name + text;
			Icon(sb, material.Ingredient, 8f, y + 2f, 22f, tooltip: false);
			FixedLabel(sb, name2, 38f, y, color, 0.74f, 160f, 24f);
			CenterLabel(sb, ratio, 204f, y, 88f, 24f, color, 0.7f, rightAligned: true);
			if (Hit(8f, y, 284f, 26f))
			{
				MouseTip(material.Name + "\n" + ratio + "\n" + (row.Need.Storage ? "Magic Storage" : T("Инвентарь", "Inventory")) + "\n" + T("Всего доступно: ", "Total available: ") + (row.Unknown ? "?" : (row.Infinite ? "∞" : Count(row.Owned))) + "\n" + T("Общие ресурсы целей суммируются. Пересекающиеся группы распределяются без двойного учёта.", "Shared objective needs are summed. Overlapping groups are allocated without double counting."));
			}
		}
		if (pages > 1)
		{
			CenterLabel(sb, "<", 8f, ResourcePagerY, 24f, 20f, TextColor, 0.75f);
			CenterLabel(sb, ">", 268f, ResourcePagerY, 24f, 20f, TextColor, 0.75f);
			CenterLabel(sb, ResourceTrackerPage + 1 + " / " + pages, 40f, ResourcePagerY, 220f, 20f, Muted, 0.68f);
		}
	}

	private static TrackedGoal SelectedTrackingGoal()
	{
		Page = Math.Clamp(Page, 0, TrackingGoalPages - 1);
		int index = BookTrackingSelectionRules.Resolve(TrackedGoals.Count, Page, (TrackingSelection == null) ? (-1) : TrackedGoals.IndexOf(TrackingSelection), 60);
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
		if (BookGeometry.TrackingClear.Contains(Mouse.X, Mouse.Y))
		{
			StopTracker();
			Page = 0;
			return;
		}
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
		for (int cell = 0; cell < 60; cell++)
		{
			int index = Page * 60 + cell;
			if (index >= TrackedGoals.Count)
			{
				break;
			}
			if (BookGeometry.TrackingCell(cell).Contains(Mouse.X, Mouse.Y))
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
		int pages = TrackingDetailPages(goal);
		if (BookGeometry.DetailPrevious.Contains(Mouse.X, Mouse.Y))
		{
			goal.ResourcePage = Math.Max(0, goal.ResourcePage - 1);
			return;
		}
		if (BookGeometry.DetailNext.Contains(Mouse.X, Mouse.Y))
		{
			goal.ResourcePage = Math.Min(pages - 1, goal.ResourcePage + 1);
			return;
		}
		List<Detail> rows = TrackingDetails(goal);
		for (int line = 0; line < 3; line++)
		{
			int index2 = goal.ResourcePage * 3 + line;
			if (index2 < rows.Count)
			{
				Detail detail = rows[index2];
				if (detail.Icon != null && !detail.Icon.IsAir && BookGeometry.DetailObtain(line).Contains(Mouse.X, Mouse.Y))
				{
					OpenAcquisition(detail.Icon.type, detail.SourceTypes);
					break;
				}
				continue;
			}
			break;
		}
	}

	private static string TrackingGoalName(TrackedGoal goal)
	{
		if (!goal.IsSet)
		{
			return goal.Recipe.createItem.Name + " ×" + Count((long)goal.Recipe.createItem.stack * (long)goal.Batches);
		}
		return T("Комплект брони", "Armor set") + " ×" + goal.Batches;
	}

	private static string TrackingSource(TrackedGoal goal)
	{
		if (!goal.Storage)
		{
			return T("Инвентарь", "Inventory");
		}
		return "Magic Storage";
	}

	private static void DrawTrackingTab(SpriteBatch sb)
	{
		TrackedGoal selected = SelectedTrackingGoal();
		Box(sb, BookGeometry.TrackingPanel, Surface, border: false, 12f);
		FixedLabel(sb, T("ОТСЛЕЖИВАЕМЫЕ КРАФТЫ", "TRACKED CRAFTS"), 28f, 108f, TextColor, 0.94f, 560f, 28f);
		FixedLabel(sb, ResourceTrackerStyle ? T("Без лимита целей · выберите крафт в сетке", "No goal cap · select a craft in the grid") : T("До 5 целей · выберите крафт в сетке", "Up to 5 goals · select a craft in the grid"), 28f, 144f, Muted, 0.78f, 736f, 24f);
		Button(sb, BookGeometry.TrackingClear, T("Убрать все", "Remove all"));
		if (TrackedGoals.Count == 0)
		{
			CenterLabel(sb, T("Пока нет отслеживаемых крафтов", "No tracked crafts yet"), 28f, 300f, 736f, 36f, TextColor);
			CenterLabel(sb, T("Добавьте рецепт кнопкой «Начать крафт» или «Весь сет».", "Add a recipe using Start crafting or Full set."), 28f, 344f, 736f, 32f, Muted, 0.8f);
		}
		for (int cell = 0; cell < 60; cell++)
		{
			int index = Page * 60 + cell;
			if (index >= TrackedGoals.Count)
			{
				break;
			}
			TrackedGoal goal = TrackedGoals[index];
			BookRect rect = BookGeometry.TrackingCell(cell);
			bool active = goal == selected;
			bool hover = rect.Contains(Mouse.X, Mouse.Y);
			SpriteBatch sb2 = sb;
			float x = rect.X;
			float y = rect.Y;
			float width = rect.Width;
			float height = rect.Height;
			Color color;
			if (hover)
			{
				color = new Color(61, 84, 67);
			}
			else
			{
				color = (active ? new Color(50, 76, 59) : Raised);
			}
			Box(sb2, x, y, width, height, color, border: true, 8f, active ? new Color?(Accent) : ((Color?)null), (!active) ? 1 : 2);
			if (goal.IsSet)
			{
				for (int part = 0; part < 3; part++)
				{
					Icon(sb, goal.Parts[part], rect.X + 2f + (float)(part * 20), rect.Y + 16f, 20f, tooltip: false);
				}
			}
			else
			{
				Icon(sb, goal.Recipe.createItem, rect.X + 12f, rect.Y + 6f, 40f, tooltip: false);
			}
			CenterLabel(sb, "×" + goal.Batches, rect.X + 4f, rect.Y + 46f, 56f, 16f, Muted, 0.62f);
			if (goal.Ready)
			{
				CenterLabel(sb, "+", rect.X + 46f, rect.Y + 2f, 16f, 16f, ResourceCompleteColor, 0.72f);
			}
			if (hover)
			{
				MouseTip(TrackingGoalName(goal) + "\n" + string.Join("\n", goal.Parts.Select((Item p) => p.Name)) + "\n" + TrackingSource(goal) + "\n" + T("Партий: ", "Batches: ") + goal.Batches + "\n" + (goal.Unknown ? T("Источник недоступен", "Source unavailable") : (goal.Ready ? T("Материалы собраны", "Materials collected") : T("Идёт сбор ресурсов", "Collecting resources"))) + "\n" + T("ЛКМ — выбрать крафт. Подробнее — в правой панели.", "Left-click to select. Details appear on the right."));
			}
		}
		Button(sb, BookGeometry.TrackingPrevious, "<");
		Button(sb, BookGeometry.TrackingNext, ">");
		BookRect pager = BookGeometry.TrackingPager;
		CenterLabel(sb, T("Крафтов: ", "Crafts: ") + TrackedGoals.Count + " · " + (Page + 1) + " / " + TrackingGoalPages, pager.X, pager.Y, pager.Width, pager.Height, Muted, 0.84f);
		DrawSelectedTrackingGoal(sb, selected);
	}

	private static void DrawSelectedTrackingGoal(SpriteBatch sb, TrackedGoal goal)
	{
		Box(sb, BookGeometry.DetailsPanel, Surface, border: false, 12f);
		FixedLabel(sb, T("ВЫБРАННЫЙ КРАФТ", "SELECTED CRAFT"), 812f, 108f, Muted, 0.82f, 284f, 24f);
		if (goal == null)
		{
			CenterLabel(sb, T("Выберите крафт слева", "Select a craft on the left"), 812f, 300f, 284f, 32f, Muted, 0.88f);
			return;
		}
		Item output = goal.Recipe.createItem;
		bool armor = goal.IsSet || HasArmorDefense(output);
		if (goal.IsSet)
		{
			for (int slot = 0; slot < 3; slot++)
			{
				BookRect rect = BookGeometry.ArmorPart(slot);
				Item part = goal.Parts[slot];
				Box(sb, rect, Raised, border: true, 8f);
				Icon(sb, part, rect.X + 15f, rect.Y + 4f, 30f, tooltip: false);
				BookRect defense = BookGeometry.ArmorPartDefense(slot);
				CenterLabel(sb, part.defense + T(" защ.", " def"), defense.X, defense.Y, defense.Width, defense.Height, Accent, 0.62f, rightAligned: false, leftAligned: false, fitWidth: true);
				if (rect.Contains(Mouse.X, Mouse.Y))
				{
					MouseTip(ArmorDefenseTooltip(part, goal.Parts));
				}
			}
		}
		else
		{
			Icon(sb, output, 926f, 136f, 56f);
		}
		BookRect name = (armor ? BookGeometry.ArmorName : new BookRect(812f, 200f, 284f, 32f));
		FixedLabel(sb, TrackingGoalName(goal), name.X, name.Y, TextColor, armor ? 1f : 1.16f, name.Width, name.Height);
		if (armor)
		{
			BookRect defense2 = BookGeometry.ArmorDefense;
			FixedLabel(sb, goal.IsSet ? ArmorSetDefenseText(goal.Parts) : (T("Защита части: ", "Piece defense: ") + output.defense), defense2.X, defense2.Y, Accent, 0.76f, defense2.Width, defense2.Height);
			if (defense2.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(ArmorDefenseTooltip(goal.Parts[0], goal.IsSet ? goal.Parts : null));
			}
		}
		BookRect source = (armor ? BookGeometry.ArmorSource : new BookRect(812f, 240f, 284f, 20f));
		string origin = (goal.IsSet ? string.Join(" / ", goal.Recipes.Select((Recipe r) => ModName(Owner(r))).Distinct()) : (ModName(Owner(goal.Recipe)) + " · " + StageName(null, goal.Recipe)));
		FixedLabel(sb, origin, source.X, source.Y, Muted, 0.78f, source.Width, source.Height);
		BookRect quantity = BookGeometry.TrackingQuantity;
		FixedLabel(sb, T("Партий: ×", "Batches: ×") + goal.Batches + " · " + TrackingSource(goal), quantity.X, quantity.Y, Muted, 0.78f, quantity.Width, quantity.Height);
		if (quantity.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Количество и источник сохранены при добавлении цели. Материалы ниже рассчитаны для всех её партий.", "Quantity and source were captured when the goal was added. Materials below cover all its batches."));
		}
		List<Detail> details = TrackingDetails(goal);
		int pages = BookTrackingSelectionRules.Pages(details.Count, 3);
		goal.ResourcePage = Math.Clamp(goal.ResourcePage, 0, pages - 1);
		for (int line = 0; line < 3; line++)
		{
			int index = goal.ResourcePage * 3 + line;
			if (index >= details.Count)
			{
				break;
			}
			Detail detail = details[index];
			BookRect rect2 = BookGeometry.DetailRow(line);
			Box(sb, rect2, Raised);
			BookRect icon = BookGeometry.DetailIcon(line);
			if (detail.Icon != null)
			{
				Icon(sb, detail.Icon, icon.X, icon.Y, icon.Width, tooltip: false);
			}
			float textX = ((detail.Icon == null) ? (rect2.X + 8f) : (icon.X + icon.Width + 8f));
			bool num = detail.Icon != null && !detail.Icon.IsAir;
			BookRect action = BookGeometry.DetailObtain(line);
			DrawTextPair(width: (num ? (action.X - 8f) : (rect2.X + rect2.Width - 8f)) - textX, sb: sb, name: detail.Name, subtitle: detail.Sub, x: textX, y: rect2.Y, height: rect2.Height, titleColor: detail.Color, subColor: detail.SubColor);
			if (num)
			{
				Button(sb, action, "?");
			}
			if (num && action.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Как получить: ", "How to obtain: ") + detail.Name);
			}
			else if (rect2.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(detail.Name + "\n" + detail.Tooltip);
			}
		}
		Button(sb, BookGeometry.DetailPrevious, "<");
		Button(sb, BookGeometry.DetailNext, ">");
		Label(sb, BookGeometry.DetailPager, goal.ResourcePage + 1 + " / " + pages, Muted, 0.86f);
		Button(sb, BookGeometry.TrackingRemove, T("Убрать выбранный крафт", "Remove selected craft"));
		BookRect status = BookGeometry.TrackingStatus;
		SpriteBatch sb2 = sb;
		string label;
		if (goal.Unknown)
		{
			label = T("Источник недоступен", "Source unavailable");
		}
		else
		{
			label = (goal.Ready ? T("Материалы собраны", "Materials collected") : T("Идёт сбор ресурсов", "Collecting resources"));
		}
		FixedLabel(sb2, label, status.X, status.Y, goal.Ready ? ResourceCompleteColor : Muted, 0.78f, status.Width, status.Height);
		if (status.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Статус материалов только этого крафта, без резервирования. Станции и условия проверяет игра.\nВ минималистичном HUD потребности всех целей суммируются; сигнал — только за полный список. Крафт остаётся ручным.", "Material status for this craft only, without reservation. The game checks stations and conditions.\nThe minimal HUD sums all goals; sound plays only for the complete list. Craft manually."));
		}
	}

	private static int TrackingDetailPages(TrackedGoal goal)
	{
		return BookTrackingSelectionRules.Pages(TrackingDetails(goal).Count, 3);
	}

	private static List<Detail> TrackingDetails(TrackedGoal goal)
	{
		BookResourceNeed[] needs = goal.Materials.Select((MaterialRequirement m) => new BookResourceNeed(goal.Storage, (long)m.Ingredient.stack * (long)goal.Batches, m.ItemTypes)).ToArray();
		HashSet<int> infinite = new HashSet<int>();
		if (goal.Storage && !goal.Unknown)
		{
			foreach (int type in needs.SelectMany((BookResourceNeed n) => n.Types).Distinct())
			{
				if (BookStorage.IsInfinite(new int[1] { type }))
				{
					infinite.Add(type);
				}
			}
		}
		BookResourceProgress[] progress = BookResourceTrackerRules.AssessGoal(needs, ((IEnumerable<KeyValuePair<int, int>>)goal.Counts).ToDictionary((Func<KeyValuePair<int, int>, int>)((KeyValuePair<int, int> p) => p.Key), (Func<KeyValuePair<int, int>, long>)((KeyValuePair<int, int> p) => p.Value)), goal.Unknown, infinite);
		List<Detail> details = new List<Detail>();
		for (int i = 0; i < progress.Length; i++)
		{
			BookResourceProgress row = progress[i];
			MaterialRequirement material = goal.Materials[i];
			Color color;
			if (row.Met)
			{
				color = ResourceCompleteColor;
			}
			else
			{
				color = (row.Unknown ? Muted : TextColor);
			}
			string ratio = (row.Unknown ? ("?/" + Count(row.Need.Quantity)) : ((row.Infinite ? "∞" : Count(row.Allocated)) + "/" + Count(row.Need.Quantity)));
			details.Add(new Detail(material.Ingredient, material.Name, ratio, color, color, ratio + "\n" + TrackingSource(goal) + "\n" + T("Партий: ", "Batches: ") + goal.Batches + "\n" + T("Доступно всего: ", "Total available: ") + (row.Unknown ? "?" : (row.Infinite ? "∞" : Count(row.Owned))) + "\n" + T("Для выбранного крафта, без резервирования. Пересекающиеся группы распределены без двойного учёта.", "For the selected craft, without reservation. Overlapping groups are allocated without double counting."), material.ItemTypes.OrderBy((int t) => t).ToArray()));
		}
		int[] tiles = (from t in goal.Recipes.SelectMany((Recipe r) => r.requiredTile)
			where t >= 0
			select t).Distinct().ToArray();
		int[] array = tiles;
		for (int num = 0; num < array.Length; num++)
		{
			int tile = array[num];
			StationItems.TryGetValue(tile, out var station);
			string stationName = (StationNames.TryGetValue(tile, out var value) ? value : (TileLoader.GetTile(tile)?.Name ?? ("Tile " + tile)));
			details.Add(new Detail(station, stationName, T("Рабочая станция", "Crafting station"), Muted, Muted, T("Требование рецепта. Текущую доступность проверяет игра.", "Recipe requirement. The game checks current availability.")));
		}
		if (tiles.Length == 0)
		{
			details.Add(new Detail(null, T("Создаётся вручную", "Crafted by hand"), T("Станция не требуется", "No station required"), Muted, Muted, ""));
		}
		foreach (string condition in (from c in goal.Recipes.SelectMany((Recipe r) => r.Conditions)
			select c.Description.Value).Distinct())
		{
			details.Add(new Detail(null, condition, T("Условие рецепта", "Recipe condition"), new Color(223, 185, 139), Muted, T("Текущее выполнение условия проверяет игра.", "The game checks whether this condition is currently met.")));
		}
		return details;
	}

	internal static void ReportUiFailure(string phase, Exception error)
	{
		SetTyping(value: false);
		Open = (MouseOver = (ModMenu = (HelpOpen = false)));
		CloseAcquisition();
		BookStorage.CancelCraft();
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
			["Version"] = 3,
			["LastTab"] = state.LastTab,
			["Query"] = state.Query,
			["ModFilter"] = state.ModFilter,
			["StageFilter"] = state.StageFilter,
			["CategoryKey"] = state.CategoryKey,
			["SampleItemKey"] = state.SampleItemKey,
			["FavoritesOnly"] = state.FavoritesOnly,
			["ChainCraftableOnly"] = state.ChainCraftableOnly,
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
			if (tag.ContainsKey("ModFilter"))
			{
				state.ModFilter = tag.GetString("ModFilter");
			}
			if (tag.ContainsKey("CategoryKey"))
			{
				state.CategoryKey = tag.GetString("CategoryKey");
			}
			if (tag.ContainsKey("SampleItemKey"))
			{
				state.SampleItemKey = tag.GetString("SampleItemKey");
			}
			if (tag.ContainsKey("FavoritesOnly"))
			{
				state.FavoritesOnly = tag.GetBool("FavoritesOnly");
			}
			if (tag.ContainsKey("ChainCraftableOnly"))
			{
				state.ChainCraftableOnly = tag.GetBool("ChainCraftableOnly");
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
		string sampleKey = ((SampleType > 0 && ContentSamples.ItemsByType.TryGetValue(SampleType, out var sample)) ? ItemKey(sample) : "");
		return new BookUiState
		{
			LastTab = (TrackingView ? "tracking" : (RecentView ? "recent" : (StorageView ? "storage" : (Chains ? "chains" : (Catalogue ? "items" : "recipes"))))),
			Query = Query,
			ModFilter = ModFilter,
			StageFilter = (int)StageFilter,
			CategoryKey = categoryKey,
			SampleItemKey = sampleKey,
			FavoritesOnly = FavoritesOnly,
			ChainCraftableOnly = ChainCraftableOnly,
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
		Catalogue = state.LastTab == "items";
		Chains = state.LastTab == "chains";
		StorageView = state.LastTab == "storage";
		RecentView = state.LastTab == "recent";
		TrackingView = state.LastTab == "tracking";
		if (state.LastTab == "recipes")
		{
			Catalogue = false;
		}
		Query = state.Query;
		ModFilter = (Mods.Contains(state.ModFilter, StringComparer.Ordinal) ? state.ModFilter : "");
		StageFilter = (BookStageFilter)state.StageFilter;
		FavoritesOnly = state.FavoritesOnly;
		int category = Categories.FindIndex((BookCategory value) => value.Key == state.CategoryKey);
		Category = ((category >= 0) ? category : 0);
		ChainCraftableOnly = state.ChainCraftableOnly;
		SortMode = state.SortMode;
		SortDescending = state.SortDescending;
		WindowScalePercent = state.WindowScalePercent;
		HasCustomWindowPosition = state.HasCustomPosition;
		WindowPositionX = state.WindowX;
		WindowPositionY = state.WindowY;
		TrackerCustomPosition = state.TrackerHasCustomPosition;
		TrackerPositionX = state.TrackerX;
		TrackerPositionY = state.TrackerY;
		SampleType = ResolveItemType(state.SampleItemKey);
		if (Chains && SampleType <= 0)
		{
			Catalogue = true;
			Chains = false;
			StorageView = false;
		}
		SetTyping(value: false);
		ModMenu = (HelpOpen = false);
		ArmorFocus = 0;
		Selected = -1;
		Page = (DetailsPage = (Variant = 0));
		CategoryRow = Math.Clamp(CategoryRow, 0, MaxCategoryRow);
		ChainHistory.Clear();
		if (Chains)
		{
			ChainHistory.Push(SampleType);
		}
		RefreshSampleRecipes();
		ApplyFilters();
	}

	private static int ResolveItemType(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return 0;
		}
		foreach (Item item in ContentSamples.ItemsByType.Values)
		{
			if (item != null && !item.IsAir && ItemKey(item) == key)
			{
				return item.type;
			}
		}
		return 0;
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
		if (!Open || HelpOpen || ModMenu || AcquisitionOpen)
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
		if (click && !TrackingView && BookGeometry.Sort.Contains(Mouse.X, Mouse.Y))
		{
			if (RecentView)
			{
				return true;
			}
			SortMode = (BookSortMode)((int)(SortMode + 1) % 3);
			Page = 0;
			ApplyFilters();
			return true;
		}
		if (click && !TrackingView && BookGeometry.SortDirection.Contains(Mouse.X, Mouse.Y))
		{
			if (RecentView)
			{
				return true;
			}
			SortDescending = !SortDescending;
			Page = 0;
			ApplyFilters();
			return true;
		}
		if (click && BookGeometry.DragHandle.Contains(Mouse.X, Mouse.Y))
		{
			DraggingWindow = true;
			WindowDragOffsetX = Main.MouseScreen.X - Origin.X;
			WindowDragOffsetY = Main.MouseScreen.Y - Origin.Y;
			return true;
		}
		return false;
	}

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
		Item a = Result(left);
		Item b = Result(right);
		int comparison;
		if (SortMode == BookSortMode.ItemId)
		{
			comparison = a.type.CompareTo(b.type);
		}
		else if (SortMode == BookSortMode.Mod)
		{
			comparison = StringComparer.CurrentCultureIgnoreCase.Compare(SortOwner(left), SortOwner(right));
			if (comparison == 0)
			{
				comparison = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
			}
		}
		else
		{
			comparison = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
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
		return SortMode switch
		{
			BookSortMode.ItemId => "ID", 
			BookSortMode.Mod => T("Мод", "Mod"), 
			_ => T("Имя", "Name"), 
		};
	}

	private static string T(string ru, string en)
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
	}

	public override void Unload()
	{
		BookSkin.Unload();
		BookProgression.Clear();
		BookArmorSets.Clear();
		ResetView();
		AllItems.Clear();
		RecipesByItem.Clear();
		All.Clear();
		Filtered.Clear();
		Mods.Clear();
		StationItems.Clear();
		StationNames.Clear();
		Categories.Clear();
		ItemCategories.Clear();
		Dirty = true;
	}

	internal static void ResetView()
	{
		CloseAcquisition();
		StopTracker();
		BlockTrackerPress = false;
		PendingChainBack = (PendingChainForward = false);
		TrackerCustomPosition = false;
		TrackerPositionX = 0f;
		TrackerPositionY = 1f;
		SetTyping(value: false);
		BookCrafting.Clear();
		ChainCraftableOnly = false;
		AppliedAvailabilityVersion = -1;
		Open = (MouseOver = (ModMenu = (Chains = (StorageView = (RecentView = (TrackingView = false))))));
		Catalogue = true;
		BookStorage.ClearView();
		AppliedStorageVersion = -1;
		AppliedRecentVersion = -1;
		Variant = 0;
		ChainHistory.Clear();
		BatchChoice = 0;
		InvalidateBatchEstimate();
		Query = (ModFilter = "");
		StageFilter = BookStageFilter.All;
		Page = (DetailsPage = (Category = (CategoryRow = 0)));
		ArmorFocus = 0;
		Selected = -1;
		FavoritesOnly = false;
		LastLeft = (LastRight = (HelpOpen = false));
		LastEscape = false;
		LastKeys = default;
		SampleType = 0;
		SampleRecipes.Clear();
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
		if (!Main.dedServ && !Main.gameMenu)
		{
			bool flag = !Open;
			SetTyping(value: false);
			// Help and the How to Get / enemy cards survive closing the book; only the mod dropdown folds.
			ModMenu = false;
			if (!flag)
			{
				Open = (MouseOver = false);
				BookStorage.CancelCraft();
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
				BookStorage.OnBookOpened();
				BookCrafting.Refresh(force: true);
				ApplyFilters();
				Open = true;
			}
		}
	}

	internal static bool OpenForInventoryItem(int type)
	{
		if (Main.dedServ || Main.gameMenu || type <= 0 || !ContentSamples.ItemsByType.TryGetValue(type, out var sample) || sample.IsAir)
		{
			return false;
		}
		SetTyping(value: false);
		ModMenu = (HelpOpen = false);
		CloseAcquisition();
		BookStorage.OnBookOpened();
		Rebuild();
		BookCrafting.Refresh(force: true);
		RestorePendingPlayerUiState();
		ChainCraftableOnly = false;
		SetSample(type);
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
		Mods.Clear();
		StationItems.Clear();
		StationNames.Clear();
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
			if (item != null && !item.IsAir && item.type > 0)
			{
				AllItems.Add(item.type);
				sources.Add(item.ModItem?.Mod.Name ?? "Terraria");
			}
		}
		Mods.AddRange(sources.OrderBy(ModName, StringComparer.CurrentCultureIgnoreCase));
		if (ModFilter.Length > 0 && !Mods.Contains(ModFilter))
		{
			ModFilter = "";
		}
		foreach (Item item2 in ContentSamples.ItemsByType.Values)
		{
			if (item2 != null && !item2.IsAir && item2.createTile >= 0 && !StationItems.ContainsKey(item2.createTile))
			{
				StationItems[item2.createTile] = item2;
				StationNames[item2.createTile] = item2.Name;
			}
		}
		List<Item> list2 = AllItems.Select((int type) => ContentSamples.ItemsByType[type]).ToList();
		HashSet<int> usedStations = new HashSet<int>(from tile in All.SelectMany((int index) => Main.recipe[index].requiredTile)
			where tile >= 0
			select tile);
		Categories = BookCategories.Create(list2, usedStations);
		Category = Math.Max(0, Categories.FindIndex((BookCategory bookCategory) => bookCategory.Key == selectedCategory));
		ItemCategories.Clear();
		foreach (Item item3 in list2)
		{
			HashSet<int> matches = new HashSet<int>();
			for (int c = 0; c < Categories.Count; c++)
			{
				if (Categories[c].Matches(item3))
				{
					matches.Add(c);
				}
			}
			ItemCategories[item3.type] = matches;
		}
		CategoryRow = Math.Clamp(CategoryRow, 0, MaxCategoryRow);
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
		try
		{
			BookArmorSets.Rebuild();
		}
		catch (Exception exception2)
		{
			BookArmorSets.Clear();
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("MRB armor index failed; using individual items.", exception2);
		}
		RefreshSampleRecipes();
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
		if (!StorageView)
		{
			if (!Catalogue)
			{
				return Main.recipe[index].createItem;
			}
			return ContentSamples.ItemsByType[index];
		}
		// Disconnect or a refresh can shrink the stored list before the grid re-filters on the next update.
		return (index >= 0 && index < BookStorage.Items.Count) ? BookStorage.Items[index] : new Item();
	}

	private static void ApplyFilters(bool clearMessage = true)
	{
		Filtered.Clear();
		ChainEligibleCount = (ChainStageExcludedCount = (ChainUnknownVisibleCount = 0));
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
		if (Chains && SampleType <= 0)
		{
			ArmorFocus = 0;
			Selected = -1;
			Page = 0;
			return;
		}
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
				enumerable = (RecentView ? BookRecent.ResolveRecipeIndexes() : All);
			}
			enumerable2 = enumerable;
		}
		else
		{
			enumerable2 = Enumerable.Range(0, BookStorage.Items.Count);
		}
		foreach (int index in enumerable2)
		{
			Item item = Result(index);
			Recipe r = ((Catalogue || StorageView) ? null : Main.recipe[index]);
			if ((r != null && r.Disabled) || !MatchCategory(item) || (SampleType > 0 && !Catalogue && !StorageView && !RecentView && !SampleRecipes.Contains(index)) || (FavoritesOnly && !Favorites.Contains(ItemKey(item))) || (ModFilter.Length > 0 && (item.ModItem?.Mod.Name ?? "Terraria") != ModFilter && (r == null || Owner(r) != ModFilter)))
			{
				continue;
			}
			string internalName = item.ModItem?.Name ?? ItemID.Search.GetName(item.type) ?? "";
			if (!BookRules.Search(Query, item.Name, ItemKey(item), internalName, ModName(item.ModItem?.Mod.Name ?? "Terraria")))
			{
				continue;
			}
			BookStage stage = ((r != null) ? BookProgression.RecipeStage(index) : BookProgression.ItemStage(item.type));
			if (!(Chains ? BookProgressionRules.MatchesChain(StageFilter, stage, BookProgression.RecipeMinimumStage(index)) : BookProgressionRules.Matches(StageFilter, stage)))
			{
				if (Chains)
				{
					ChainStageExcludedCount++;
				}
				continue;
			}
			if (Chains)
			{
				ChainEligibleCount++;
				if (!BookRules.ChainRecipeVisible(ChainCraftableOnly, BookCrafting.IsAvailable(r)))
				{
					continue;
				}
				if (stage == BookStage.Unknown)
				{
					ChainUnknownVisibleCount++;
				}
			}
			Filtered.Add(index);
		}
		if (!RecentView)
		{
			Filtered.Sort(CompareFilteredIndexes);
		}
		if (Chains && StageFilter != BookStageFilter.All)
		{
			int[] ordered = Filtered.OrderBy((int index2) => (BookProgression.RecipeStage(index2) == BookStage.Unknown) ? 1 : 0).ToArray();
			Filtered.Clear();
			Filtered.AddRange(ordered);
		}
		Page = Math.Clamp(Page, 0, Math.Max(0, (Filtered.Count - 1) / ActivePageSize));
		if (!Filtered.Contains(Selected))
		{
			ArmorFocus = 0;
			Selected = ((Filtered.Count > 0) ? Filtered[0] : (-1));
			DetailsPage = (Variant = 0);
		}
	}

	private static void RefreshSampleRecipes()
	{
		SampleRecipes.Clear();
		if (SampleType <= 0)
		{
			return;
		}
		if (!ContentSamples.ItemsByType.ContainsKey(SampleType))
		{
			SampleType = 0;
			return;
		}
		foreach (int index in All)
		{
			Recipe r = Main.recipe[index];
			if (BookRules.RecipeMatchesSample(SampleType, r.createItem.type, SampleUses, from item in r.requiredItem
				where !item.IsAir
				select item.type, r.AcceptedByItemGroups))
			{
				SampleRecipes.Add(index);
			}
		}
	}

	private static void SwitchTab(int index)
	{
		Catalogue = index == 0;
		Chains = index == 2;
		StorageView = index == 3;
		RecentView = index == 4;
		TrackingView = index == 5;
		if (StorageView)
		{
			BookStorage.Tick();
			AppliedStorageVersion = BookStorage.ItemsVersion;
		}
		SetTyping(value: false);
		ModMenu = false;
		ArmorFocus = 0;
		Selected = -1;
		Page = (DetailsPage = (Variant = 0));
		RefreshSampleRecipes();
		ApplyFilters();
		AppliedRecentVersion = BookRecent.Version;
	}

	private static void SetSample(int type, bool append = false)
	{
		StorageView = (RecentView = (TrackingView = false));
		SampleType = Math.Max(0, type);
		if (!append)
		{
			ChainHistory.Clear();
		}
		if (SampleType > 0)
		{
			ChainHistory.Push(SampleType);
			Catalogue = false;
			Chains = true;
		}
		SetTyping(value: false);
		ModMenu = false;
		Query = (ModFilter = "");
		StageFilter = BookStageFilter.All;
		Category = 0;
		FavoritesOnly = false;
		Page = (DetailsPage = (Variant = 0));
		ArmorFocus = 0;
		Selected = -1;
		RefreshSampleRecipes();
		ApplyFilters();
	}

	private static void GoToChain(int index)
	{
		int type = ChainHistory.GoTo(index);
		if (type > 0)
		{
			SetSample(type, append: true);
		}
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
				Category = 0;
				ModFilter = "";
				FavoritesOnly = false;
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
	}

	private static bool Hit(float x, float y, float w, float h)
	{
		return new BookRect(x, y, w, h).Contains(Mouse.X, Mouse.Y);
	}

	public override void UpdateUI(GameTime gameTime)
	{
		if (Main.gameMenu)
		{
			CloseAcquisition();
			SetTyping(value: false);
			Open = (MouseOver = false);
			return;
		}
		BookInventoryShortcut.ProcessRequests();
		ProcessChainNavigation();
		bool middle = Microsoft.Xna.Framework.Input.Mouse.GetState().MiddleButton == ButtonState.Pressed;
		if (((Open && Typing) & middle) && !LastMiddle && ModernRecipeBook.ToggleBook.GetAssignedKeys().Contains("Mouse3"))
		{
			BookInventoryShortcut.RequestToggle();
		}
		LastMiddle = middle;
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
		BookStorage.UpdateBookConnection();
		if (StorageView && AppliedStorageVersion != BookStorage.ItemsVersion)
		{
			AppliedStorageVersion = BookStorage.ItemsVersion;
			Selected = ((selectedType > 0) ? BookStorage.Items.FindIndex((Item item) => item.type == selectedType && item.prefix == selectedPrefix) : (-1));
			ApplyFilters(clearMessage: false);
		}
		BookCrafting.Refresh();
		if (Dirty)
		{
			Rebuild();
			ApplyFilters();
		}
		if (RecentView && AppliedRecentVersion != BookRecent.Version)
		{
			AppliedRecentVersion = BookRecent.Version;
			ApplyFilters(clearMessage: false);
		}
		if (Chains && ChainCraftableOnly && AppliedAvailabilityVersion != BookCrafting.AvailabilityVersion)
		{
			ApplyFilters(clearMessage: false);
		}
		Layout();
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
		if (escape && !LastEscape)
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
			else
			{
				Open = false;
				BookStorage.CancelCraft();
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
		bool flag = HandleKeys(keys);
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
			else if (BookGeometry.CategoryPanel.Contains(Mouse.X, Mouse.Y))
			{
				CategoryRow = Math.Clamp(CategoryRow + step, 0, MaxCategoryRow);
			}
			else if (BookGeometry.DetailsPanel.Contains(Mouse.X, Mouse.Y))
			{
				DetailsPage = Math.Clamp(DetailsPage + step, 0, MaxDetailsPage());
			}
			else if (Hit(164f, 92f, 626f, 548f))
			{
				Page = Math.Clamp(Page + step, 0, Pages - 1);
			}
		}
		BookRect sampleSlot = BookGeometry.SampleSlot;
		if (rightClick && StorageView && !ModMenu)
		{
			for (int n = 0; n < 40; n++)
			{
				int index = Page * 40 + n;
				if (index >= Filtered.Count)
				{
					break;
				}
				if (BookGeometry.RecipeCell(n).Contains(Mouse.X, Mouse.Y))
				{
					if (BookStorageGesture.Read(StorageView, hovered: true, ModMenu, leftPressed: false, rightClick, shift: false) == BookStorageGesture.Transfer.One)
					{
						BookStorage.Withdraw(Result(Filtered[index]), single: true);
					}
					return;
				}
			}
		}
		if (rightClick && !StorageView && !RecentView && !TrackingView && !ModMenu && sampleSlot.Contains(Mouse.X, Mouse.Y))
		{
			SetSample(0);
		}
		else
		{
			if (!click)
			{
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
				BookStorage.CancelCraft();
				return;
			}
			for (int tab = 0; tab < 6; tab++)
			{
				if (BookGeometry.Tab(tab).Contains(Mouse.X, Mouse.Y))
				{
					SwitchTab(tab);
					return;
				}
			}
			if (!TrackingView && BookGeometry.StageFilter.Contains(Mouse.X, Mouse.Y))
			{
				StageFilter = (BookStageFilter)((int)(StageFilter + 1) % 4);
				Page = (DetailsPage = (Variant = 0));
				ApplyFilters();
				if (Chains)
				{
					ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"Chain stage audit: sample={SampleType}, stage={StageFilter}, visible={Filtered.Count}, uncertain={ChainUnknownVisibleCount}, excludedByStage={ChainStageExcludedCount}, craftableOnly={ChainCraftableOnly}");
				}
				return;
			}
			if (BookGeometry.CraftSource.Contains(Mouse.X, Mouse.Y))
			{
				BookStorage.ChooseCraftSource(!BookStorage.UseForCrafting);
				BookCrafting.Refresh(force: true);
				if (Chains && ChainCraftableOnly)
				{
					ApplyFilters(clearMessage: false);
				}
				return;
			}
			if (TrackingView)
			{
				HandleTrackingTabInput();
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
				if (BookGeometry.StorageConnect.Contains(Mouse.X, Mouse.Y))
				{
					SetTyping(value: false);
					if (BookStorage.ConnectNearby() && !BookStorage.Connected)
					{
						CloseForStorage();
					}
					return;
				}
				if (BookGeometry.StorageQuickStack.Contains(Mouse.X, Mouse.Y))
				{
					BookStorage.InventoryAction(true);
					return;
				}
				if (BookGeometry.StorageRestock.Contains(Mouse.X, Mouse.Y))
				{
					BookStorage.InventoryAction(null);
					return;
				}
				if (BookGeometry.StorageDepositAll.Contains(Mouse.X, Mouse.Y))
				{
					BookStorage.InventoryAction(false);
					return;
				}
				if (BookGeometry.StorageDeposit.Contains(Mouse.X, Mouse.Y))
				{
					BookStorage.DepositCursor();
					return;
				}
				if (BookGeometry.StorageNative.Contains(Mouse.X, Mouse.Y))
				{
					CloseForStorage();
					return;
				}
				if (BookGeometry.StorageDisconnect.Contains(Mouse.X, Mouse.Y))
				{
					BookStorage.Disconnect();
					return;
				}
			}
			if (!StorageView && !RecentView && sampleSlot.Contains(Mouse.X, Mouse.Y))
			{
				if (!Main.mouseItem.IsAir)
				{
					SetSample(Main.mouseItem.type);
					return;
				}
				if (SampleType > 0)
				{
					SwitchTab(2);
					return;
				}
				SetTyping(value: false);
				Main.playerInventory = true;
				return;
			}
			if (!StorageView && !RecentView && BookGeometry.SampleClear.Contains(Mouse.X, Mouse.Y))
			{
				SetSample(0);
				return;
			}
			if (!StorageView && !RecentView && BookGeometry.SampleOpen.Contains(Mouse.X, Mouse.Y))
			{
				if (SampleType > 0)
				{
					SwitchTab(2);
					return;
				}
				SetTyping(value: false);
				Main.playerInventory = true;
				return;
			}
			if (BookGeometry.SearchClear.Contains(Mouse.X, Mouse.Y))
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
			if (!TrackingView && BookGeometry.Refresh.Contains(Mouse.X, Mouse.Y))
			{
				RefreshView();
				return;
			}
			if (BookGeometry.ModFilter.Contains(Mouse.X, Mouse.Y))
			{
				ModMenu = true;
				ModOffset = 0;
				return;
			}
			if (!TrackingView && BookGeometry.FavoritesFilter.Contains(Mouse.X, Mouse.Y))
			{
				FavoritesOnly = !FavoritesOnly;
				Page = 0;
				ApplyFilters();
				return;
			}
			if (BookGeometry.CategoryPrevious.Contains(Mouse.X, Mouse.Y))
			{
				CategoryRow = Math.Max(0, CategoryRow - 1);
				return;
			}
			if (BookGeometry.CategoryNext.Contains(Mouse.X, Mouse.Y))
			{
				CategoryRow = Math.Min(MaxCategoryRow, CategoryRow + 1);
				return;
			}
			for (int visible = 0; visible < 30; visible++)
			{
				int index2 = CategoryRow * 3 + visible;
				if (index2 >= Categories.Count)
				{
					break;
				}
				if (BookGeometry.CategoryCell(visible).Contains(Mouse.X, Mouse.Y))
				{
					Category = ((Category != index2) ? index2 : 0);
					Page = (DetailsPage = 0);
					ApplyFilters();
					return;
				}
			}
			if (Chains)
			{
				for (int choice = 0; choice < 2; choice++)
				{
					if (BookGeometry.ChainFilter(choice).Contains(Mouse.X, Mouse.Y))
					{
						ChainCraftableOnly = choice == 1;
						if (ChainCraftableOnly)
						{
							BookCrafting.Refresh(force: true);
						}
						Page = (DetailsPage = 0);
						ApplyFilters();
						return;
					}
				}
				if (BookGeometry.ChainBack.Contains(Mouse.X, Mouse.Y))
				{
					GoToChain(ChainHistory.Count - 2);
					return;
				}
				int first = Math.Max(0, ChainHistory.Count - 4);
				for (int visible2 = 0; visible2 < Math.Min(4, ChainHistory.Count); visible2++)
				{
					if (BookGeometry.ChainCrumb(visible2).Contains(Mouse.X, Mouse.Y))
					{
						GoToChain(first + visible2);
						return;
					}
				}
			}
			for (int n3 = 0; n3 < ActivePageSize; n3++)
			{
				int index3 = Page * ActivePageSize + n3;
				if (index3 >= Filtered.Count)
				{
					break;
				}
				if ((Chains ? BookGeometry.ChainCell(n3) : BookGeometry.RecipeCell(n3)).Contains(Mouse.X, Mouse.Y))
				{
					KeyboardState keyboard = Keyboard.GetState();
					if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
					{
						ToggleFavorite(Result(Filtered[index3]));
						return;
					}
					if (BookStorageGesture.Read(StorageView, hovered: true, ModMenu, click, rightPressed: false, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)) == BookStorageGesture.Transfer.Stack)
					{
						Selected = Filtered[index3];
						BookStorage.Withdraw(Result(Selected), single: false);
						return;
					}
					if (Chains && BookGeometry.ChainDrill(n3).Contains(Mouse.X, Mouse.Y))
					{
						SetSample(Result(Filtered[index3]).type, append: true);
						return;
					}
					if (!StorageView && (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)))
					{
						SetSample(Result(Filtered[index3]).type);
						return;
					}
					ArmorFocus = 0;
					Selected = Filtered[index3];
					DetailsPage = (Variant = 0);
					BookCrafting.LastMessage = "";
					return;
				}
			}
			if ((Chains ? BookGeometry.ChainPrev : BookGeometry.PagePrevious).Contains(Mouse.X, Mouse.Y))
			{
				Page = Math.Max(0, Page - 1);
			}
			if (BookGeometry.PageNext.Contains(Mouse.X, Mouse.Y))
			{
				Page = Math.Min(Pages - 1, Page + 1);
			}
			if (Selected < 0 || StorageView || HandleArmorSetClick() || HandleTrackButton())
			{
				return;
			}
			if (SelectedRecipe == null)
			{
				Item obtainItem = SelectedItem;
				if (obtainItem != null && !obtainItem.IsAir && BookGeometry.SelectedItemObtain.Contains(Mouse.X, Mouse.Y))
				{
					OpenAcquisition(obtainItem.type, null);
					return;
				}
			}
			Recipe materialRecipe = SelectedRecipe;
			if (materialRecipe != null)
			{
				if (HandleBatchChoiceClick(materialRecipe))
				{
					return;
				}
				List<Detail> details = Details(materialRecipe);
				for (int row = 0; row < 3; row++)
				{
					int detail = DetailsPage * 3 + row;
					if (detail >= details.Count)
					{
						continue;
					}
					Item material = details[detail].Icon;
					if (material != null && !material.IsAir && BookGeometry.DetailRow(row).Contains(Mouse.X, Mouse.Y))
					{
						if (BookGeometry.DetailObtain(row).Contains(Mouse.X, Mouse.Y))
						{
							OpenAcquisition(material.type, details[detail].SourceTypes);
						}
						else
						{
							SetSample(material.type);
						}
						return;
					}
				}
			}
			if (BookGeometry.FavoriteAction.Contains(Mouse.X, Mouse.Y))
			{
				ToggleFavorite(SelectedItem);
			}
			if (BookGeometry.CraftAction.Contains(Mouse.X, Mouse.Y))
			{
				Recipe recipe = SelectedRecipe;
				BookCrafting.TryCraft(recipe, Ru, PreviewBatches(recipe));
				if (Chains && ChainCraftableOnly)
				{
					ApplyFilters(clearMessage: false);
				}
				return;
			}
			if ((Catalogue || ArmorFocus > 0) && VariantCount > 1)
			{
				if (BookGeometry.VariantPrevious.Contains(Mouse.X, Mouse.Y))
				{
					Variant = Math.Max(0, Variant - 1);
					DetailsPage = 0;
					return;
				}
				if (BookGeometry.VariantNext.Contains(Mouse.X, Mouse.Y))
				{
					Variant = Math.Min(VariantCount - 1, Variant + 1);
					DetailsPage = 0;
					return;
				}
			}
			if (BookGeometry.DetailPrevious.Contains(Mouse.X, Mouse.Y))
			{
				DetailsPage = Math.Max(0, DetailsPage - 1);
			}
			if (BookGeometry.DetailNext.Contains(Mouse.X, Mouse.Y))
			{
				DetailsPage = Math.Min(MaxDetailsPage(), DetailsPage + 1);
			}
		}
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
		if (BookStorage.Connected)
		{
			BookStorage.RefreshStorage();
		}
		Rebuild();
		BookCrafting.Refresh(force: true);
		ApplyFilters(clearMessage: false);
	}

	private static bool HandleKeys(KeyboardState keys)
	{
		BookHotkeys.Action action = BookHotkeys.Read(keys, LastKeys, Typing, Main.blockInput, Main.drawingPlayerChat || Main.editSign || Main.editChest, HelpOpen, ModMenu, StorageView, Chains);
		switch (action)
		{
		case BookHotkeys.Action.None:
			return false;
		case BookHotkeys.Action.Help:
			SetTyping(value: false);
			ModMenu = false;
			HelpOpen = !HelpOpen;
			break;
		case BookHotkeys.Action.Search:
			if (!TrackingView)
			{
				ModMenu = false;
				SetTyping(value: true);
			}
			break;
		case BookHotkeys.Action.Items:
			SwitchTab(0);
			break;
		case BookHotkeys.Action.Recipes:
			SwitchTab(1);
			break;
		case BookHotkeys.Action.Chains:
			SwitchTab(2);
			break;
		case BookHotkeys.Action.Storage:
			SwitchTab(3);
			break;
		case BookHotkeys.Action.Recent:
			SwitchTab(4);
			break;
		case BookHotkeys.Action.Tracking:
			SwitchTab(5);
			break;
		case BookHotkeys.Action.NextTab:
		case BookHotkeys.Action.PreviousTab:
			SwitchTab(((TrackingView ? 5 : (RecentView ? 4 : (StorageView ? 3 : (Chains ? 2 : ((!Catalogue) ? 1 : 0))))) + ((action == BookHotkeys.Action.NextTab) ? 1 : 5)) % 6);
			break;
		case BookHotkeys.Action.ResetFilters:
			Query = (ModFilter = "");
			StageFilter = BookStageFilter.All;
			FavoritesOnly = false;
			Category = (CategoryRow = (Page = 0));
			ApplyFilters();
			break;
		case BookHotkeys.Action.Craft:
			if (!TrackingView)
			{
				BookCrafting.TryCraft(SelectedRecipe, Ru, PreviewBatches(SelectedRecipe));
			}
			break;
		case BookHotkeys.Action.DepositCursor:
			BookStorage.DepositCursor();
			break;
		case BookHotkeys.Action.DepositAll:
			BookStorage.InventoryAction(false);
			break;
		case BookHotkeys.Action.QuickStack:
			BookStorage.InventoryAction(true);
			break;
		case BookHotkeys.Action.Restock:
			BookStorage.InventoryAction(null);
			break;
		case BookHotkeys.Action.Refresh:
			RefreshView();
			break;
		case BookHotkeys.Action.ChainBack:
			GoToChain(ChainHistory.Count - 2);
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
		Recipe r = SelectedRecipe;
		if (r == null)
		{
			return 0;
		}
		int n = r.requiredItem.Count((Item i) => !i.IsAir) + r.requiredTile.Count((int t) => t >= 0) + r.Conditions.Count;
		if (r.requiredTile.Count((int t) => t >= 0) == 0)
		{
			n++;
		}
		return Math.Max(0, (n - 1) / 3);
	}

	internal static void CloseForStorage()
	{
		SetTyping(value: false);
		CloseAcquisition();
		Open = (MouseOver = (ModMenu = (HelpOpen = false)));
		BookStorage.CancelCraft();
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		InsertTrackerLayer(layers);
		if (!Open)
		{
			return;
		}
		if (BookStorage.HideNativeUI)
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
		CenterLabel(sb, label, x + padding, y + 4f, w - padding * 2f, h - 8f, active ? Accent : TextColor, 1f, rightAligned: false, leftAligned: false, fitWidth || w <= 72f);
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

	private static void CenterLabel(SpriteBatch sb, string label, float x, float y, float width, float height, Color color, float size = 1f, bool rightAligned = false, bool leftAligned = false, bool fitWidth = false)
	{
		label = BookRules.PlainText(label).Replace("\n", " ").Replace("\r", " ");
		if (fitWidth && Ink(label).Width > 0f)
		{
			size = Math.Min(size, width / (Ink(label).Width * TextScale));
		}
		BookTypography.Line line = BookTypography.Layout(label, width, height, size, TextScale, (string value) =>
		{
			BookGlyphLayout.Bounds bounds = Ink(value);
			return (Width: bounds.Width, Height: bounds.Height);
		}, centered: true);
		BookGlyphLayout.Bounds ink = Ink(line.Value);
		Vector2 pos = Origin + new Vector2(x + (rightAligned ? (width - ink.Width * line.Scale) : (leftAligned ? 0f : line.X)) - ink.X * line.Scale, y + line.Y - ink.Y * line.Scale) * Scale;
		DrawInkLine(sb, line.Value, pos, color, line.Scale);
		if (line.Truncated && Hit(x, y, width, height))
		{
			MouseTip(label);
		}
	}

	private static void FixedLabel(SpriteBatch sb, string label, float x, float y, Color? color = null, float size = 1f, float width = 1000f, float height = 32f)
	{
		label = BookRules.PlainText(label).Replace("\n", " ").Replace("\r", " ");
		BookGlyphLayout.Bounds reference = Ink("АБВЁЙДЦЩруфgjpqy0123456789");
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
			sb.DrawString(font, value, pos + offset, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}
		sb.DrawString(font, value, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
	}

	private static void DrawTextPair(SpriteBatch sb, string name, string subtitle, float x, float y, float width, float height, Color titleColor, Color subColor)
	{
		BookTypography.Line title = BookTypography.Layout(BookRules.PlainText(name), width, 24f, 1f, TextScale, MeasureInk);
		BookTypography.Line sub = BookTypography.Layout(BookRules.PlainText(subtitle), width, 20f, 0.78f, TextScale, MeasureInk);
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

	private static void Icon(SpriteBatch sb, Item item, float x, float y, float size, bool tooltip = true)
	{
		if (item != null && !item.IsAir && item.type < TextureAssets.Item.Length)
		{
			Main.instance.LoadItem(item.type);
			Texture2D texture = TextureAssets.Item[item.type].Value;
			Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Bounds;
			float s = Math.Min(size / (float)Math.Max(1, frame.Width), size / (float)Math.Max(1, frame.Height));
			sb.Draw(texture, Origin + new Vector2(x + size / 2f, y + size / 2f) * Scale, frame, Color.White, 0f, frame.Size() / 2f, s * Scale, SpriteEffects.None, 0f);
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

	private static int Owned(Item item, RecipeGroup group)
	{
		if (BookStorage.UseForCrafting)
		{
			if (!BookStorage.CraftingConnected)
			{
				return -1;
			}
			return BookStorage.Quantity(item, group);
		}
		long total = 0L;
		foreach (Item inv in BookCrafting.Materials())
		{
			if (inv != null && !inv.IsAir && (group?.ValidItems.Contains(inv.type) ?? (inv.type == item.type)))
			{
				total += inv.stack;
			}
		}
		return (int)Math.Min(total, 2147483647L);
	}

	private static int Owned(HashSet<int> acceptedTypes)
	{
		if (BookStorage.UseForCrafting)
		{
			if (!BookStorage.CraftingConnected)
			{
				return -1;
			}
			return BookStorage.Quantity(acceptedTypes);
		}
		long total = 0L;
		HashSet<Item> seen = new HashSet<Item>(ReferenceEqualityComparer.Instance);
		foreach (Item item in BookCrafting.Materials())
		{
			if (item != null && !item.IsAir && acceptedTypes.Contains(item.type) && seen.Add(item))
			{
				total = Math.Min(2147483647L, total + item.stack);
			}
		}
		return (int)total;
	}

	private static bool Infinite(Item item, RecipeGroup group)
	{
		if (BookStorage.UseForCrafting)
		{
			return BookStorage.IsInfinite(item, group);
		}
		return false;
	}

	private static string Count(long value)
	{
		return value.ToString("N0", CultureInfo.CurrentCulture);
	}

	private static List<MaterialRequirement> MaterialRequirements(Recipe recipe)
	{
		List<(Item, RecipeGroup, string, HashSet<int>)> raw = new List<(Item, RecipeGroup, string, HashSet<int>)>();
		foreach (Item ingredient in recipe.requiredItem)
		{
			if (ingredient.IsAir)
			{
				continue;
			}
			string name = GroupName(recipe, ingredient, out var group);
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
			result.Add(new MaterialRequirement(raw[i].Item1, raw[i].Item2, raw[i].Item3, raw[i].Item4, overlap[i]));
		}
		return result;
	}

	private static int PreviewBatchCount(Recipe recipe)
	{
		return BookMaterialRules.NormalizeBatches(PreviewBatches(recipe));
	}

	private static BookMaterialRules.Assessment Assess(MaterialRequirement requirement, int batches)
	{
		return BookMaterialRules.Evaluate(requirement.Ingredient.stack, batches, Owned(requirement.ItemTypes), BookStorage.UseForCrafting && BookStorage.IsInfinite(requirement.ItemTypes), requirement.Overlapping);
	}

	private static string MaterialSub(BookMaterialRules.Assessment assessment)
	{
		return BookMaterialRules.Ratio(assessment);
	}

	private static string MaterialTooltip(MaterialRequirement requirement, BookMaterialRules.Assessment assessment, int batches)
	{
		return MaterialSub(assessment) + T(" (есть/нужно; ", " (have/need; ") + Count(requirement.Ingredient.stack) + T(" за партию × ", " per batch × ") + batches + ")." + (assessment.Overlapping ? T(" Группы пересекаются: одни предметы могут учитываться в нескольких строках.", " Groups overlap: the same items may be counted in multiple rows.") : "");
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
			else
			{
				color = ((assessment.HasDeficit || assessment.Overlapping) ? new Color(223, 185, 139) : Accent);
			}
			string name = requirement.Name;
			list.Add(new Detail(requirement.Ingredient, name, MaterialSub(assessment), color, color, MaterialTooltip(requirement, assessment, batches) + T("\nОценка без скидок расхода; станции и условия проверяет игра.", "\nEstimate before consumption discounts; the game checks stations and conditions."), requirement.ItemTypes.OrderBy((int x) => x).ToArray()));
		}
		foreach (int tile in r.requiredTile.Where((int t) => t >= 0))
		{
			StationItems.TryGetValue(tile, out var station);
			string name2 = (StationNames.TryGetValue(tile, out var value) ? value : (TileLoader.GetTile(tile)?.Name ?? ("Tile " + tile)));
			list.Add(new Detail(station, name2, T("Рабочая станция", "Crafting station"), Muted, Muted, ""));
		}
		if (!r.requiredTile.Any((int t) => t >= 0))
		{
			list.Add(new Detail(null, T("Создаётся вручную", "Crafted by hand"), T("Станция не требуется", "No station required"), Muted, Muted, ""));
		}
		foreach (Condition c in r.Conditions)
		{
			list.Add(new Detail(null, c.Description.Value, T("Условие рецепта", "Recipe condition"), new Color(223, 185, 139), Muted, ""));
		}
		return list;
	}

	private static void DrawSample(SpriteBatch sb)
	{
		BookRect panel = BookGeometry.SamplePanel;
		BookRect slot = BookGeometry.SampleSlot;
		Box(sb, panel.X, panel.Y, panel.Width, panel.Height, Surface, border: true, 12f);
		bool hasSample = SampleType > 0 && ContentSamples.ItemsByType.TryGetValue(SampleType, out var _);
		Box(sb, slot.X, slot.Y, slot.Width, slot.Height, new Color(36, 51, 41), border: true, 10f, Accent, (!hasSample) ? 1 : 2);
		if (hasSample)
		{
			Item sample = ContentSamples.ItemsByType[SampleType];
			Icon(sb, sample, slot.X + 8f, slot.Y + 8f, 32f, tooltip: false);
			SpriteBatch sb2 = sb;
			string name = sample.Name;
			string subtitle;
			if (Catalogue)
			{
				subtitle = T("Образец для цепочки · Shift+клик в каталоге", "Chain sample · Shift-click a catalogue item");
			}
			else
			{
				subtitle = (Chains ? T("Из него можно сделать · ПКМ — очистить", "Crafting uses · Right-click to clear") : T("Рецепты создания · X — сброс фильтра", "Crafting recipes · X clears the filter"));
			}
			DrawTextPair(sb2, name, subtitle, 256f, panel.Y, 312f, panel.Height, TextColor, Muted);
			if (!ModMenu && slot.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(sample.Name + "\n" + T("ЛКМ: заменить предметом с курсора или открыть цепочку. ПКМ: очистить. Это образец, не хранилище.", "Left-click: copy cursor item type or open its chain. Right-click: clear. This is a sample, not storage."));
			}
		}
		else
		{
			CenterLabel(sb, "+", slot.X + 4f, slot.Y + 4f, slot.Width - 8f, slot.Height - 8f, Accent, 1.2f);
			DrawTextPair(sb, T("Поместите предмет", "Place an item here"), T("С курсора — в ячейку · или Shift+клик", "Click with a held item · or Shift-click"), 256f, panel.Y, 312f, panel.Height, TextColor, Muted);
			if (!ModMenu && slot.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Возьмите предмет из инвентаря на курсор и нажмите на эту ячейку. Сам предмет останется на курсоре. Можно также нажать Shift+ЛКМ по предмету в книге. Без предмета — открыть инвентарь.", "Pick up an inventory item and click this slot. The real stack stays on the cursor. Or Shift-click an item in the book. Empty click opens inventory."));
			}
		}
		Button(sb, BookGeometry.SampleOpen, hasSample ? T("Цепочка", "Chain") : T("Выбрать", "Choose"));
		Button(sb, BookGeometry.SampleClear, "X");
		if (!ModMenu && BookGeometry.SampleClear.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Очистить образец и фильтр рецептов", "Clear sample and recipe filter"));
		}
	}

	private static void DrawRecentToolbar(SpriteBatch sb)
	{
		BookRect panel = BookGeometry.SamplePanel;
		Box(sb, panel.X, panel.Y, panel.Width, panel.Height, Surface, border: true, 12f);
		int count = Main.LocalPlayer.GetModPlayer<BookPlayer>().RecentRecipes.Count;
		Text(sb, T("Последние успешные крафты", "Most recent successful crafts"), 196f, 176f, TextColor, 0.98f, 462f, 24f);
		Text(sb, T("История персонажа · максимум ", "Character history · up to ") + 40 + T(" рецептов · записываются только подтверждённые крафты", " recipes · only confirmed crafts are recorded"), 196f, 208f, Muted, 0.78f, 552f, 16f);
		CenterLabel(sb, count + "/" + 40, 670f, 176f, 78f, 24f, Accent, 0.88f);
	}

	private static ChainMaterialPreview PreviewChainMaterials(Recipe recipe, Item output)
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
			color = new Color(238, 151, 126);
		}
		else
		{
			color = (assessed.Any(((MaterialRequirement Requirement, BookMaterialRules.Assessment Assessment) x) => x.Assessment.Unknown || x.Assessment.Overlapping) ? Muted : Accent);
		}
		string tooltip = output.Name + ((output.stack > 1) ? (" ×" + output.stack) : "") + "\n" + T("Потребности для ", "Materials for ") + batches + T(" партий:", " batches:");
		if (assessed.Count == 0)
		{
			tooltip = tooltip + "\n" + T("Ингредиентов нет.", "No ingredient inputs.");
		}
		foreach (var entry in assessed)
		{
			tooltip = tooltip + "\n• " + entry.Item1.Name + ": " + MaterialTooltip(entry.Item1, entry.Item2, batches);
		}
		return new ChainMaterialPreview(summary, color, tooltip);
	}

	private static void DrawChains(SpriteBatch sb)
	{
		Button(sb, BookGeometry.ChainFilter(0), T("Все предметы", "All items"), !ChainCraftableOnly);
		Button(sb, BookGeometry.ChainFilter(1), T("Можно скрафтить", "Craftable now"), ChainCraftableOnly);
		if (!ModMenu && BookGeometry.ChainFilter(0).Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Все рецепты текущего шага, даже если не хватает ресурсов или станции.", "All recipes at this step, even without the required materials or stations."));
		}
		if (!ModMenu && BookGeometry.ChainFilter(1).Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Только нативно доступные рецепты: материалы, группы замен, станции и условия. Список обновляется автоматически. Перед крафтом уберите предмет с курсора.", "Only natively available recipes: materials, recipe groups, stations and conditions. Updates automatically. Clear the cursor before crafting."));
		}
		if (SampleType <= 0)
		{
			Text(sb, T("Начните с предмета", "Start with an item"), 200f, 292f, TextColor, 1.15f, 540f, 34f);
			Text(sb, T("Поместите его в подписанную ячейку выше.", "Place it in the labeled slot above."), 200f, 340f, Muted, 0.92f, 540f, 28f);
			Text(sb, T("Или Shift+клик по предмету во вкладке «Предметы».", "Or Shift-click an item in the Items tab."), 200f, 376f, Muted, 0.86f, 540f, 28f);
			Text(sb, T("Предмет > что из него сделать > следующий шаг", "Item > what it makes > next step"), 200f, 432f, Accent, 0.9f, 540f, 28f);
			return;
		}
		int first = Math.Max(0, ChainHistory.Count - 4);
		for (int visible = 0; visible < Math.Min(4, ChainHistory.Count); visible++)
		{
			int index = first + visible;
			Item item = ContentSamples.ItemsByType[ChainHistory[index]];
			BookRect crumb = BookGeometry.ChainCrumb(visible);
			Button(sb, crumb, "", index == ChainHistory.Count - 1);
			Icon(sb, item, crumb.X + 4f, crumb.Y + 4f, 24f, tooltip: false);
			CenterLabel(sb, ((visible == 0 && first > 0) ? "... " : "") + item.Name, crumb.X + 36f, crumb.Y + 4f, 92f, 24f, TextColor, 0.78f);
			if (!ModMenu && crumb.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(item.Name + "\n" + T("Вернуться к этому шагу", "Return to this step"));
			}
			if (visible < Math.Min(4, ChainHistory.Count) - 1)
			{
				CenterLabel(sb, ">", crumb.X + crumb.Width, crumb.Y, 8f, 32f, Muted, 0.78f);
			}
		}
		if (Filtered.Count == 0)
		{
			bool terminal = SampleRecipes.Count == 0;
			bool noCraftable = !terminal && ChainCraftableOnly && ChainEligibleCount > 0;
			bool noStage = !terminal && !noCraftable && ChainStageExcludedCount > 0;
			SpriteBatch sb2 = sb;
			string text;
			if (terminal)
			{
				text = T("Конец цепочки", "End of this branch");
			}
			else if (noCraftable)
			{
				text = T("Сейчас нечего скрафтить", "Nothing craftable right now");
			}
			else
			{
				text = (noStage ? T("Нет рецептов выбранного этапа", "No recipes for the selected stage") : T("Ветки скрыты фильтрами", "Branches hidden by filters"));
			}
			Text(sb2, text, 200f, 350f, TextColor, 1f, 540f, 30f);
			SpriteBatch sb3 = sb;
			string text2;
			if (terminal)
			{
				text2 = T("Обычных рецептов с этим ингредиентом нет.", "No standard recipes use this ingredient.");
			}
			else if (noCraftable)
			{
				text2 = T("Проверьте ресурсы, станции и условия.", "Check materials, stations and conditions.");
			}
			else
			{
				text2 = (noStage ? T("Выберите «Этап: Все», чтобы увидеть остальные этапы.", "Choose Stage: All to see the other stages.") : T("Сбросьте поиск, категорию или фильтр мода.", "Clear the search, category or mod filter."));
			}
			Text(sb3, text2, 200f, 392f, Muted, 0.86f, 540f, 28f);
			Text(sb, noCraftable ? T("«Все предметы» покажет и недоступные рецепты.", "All items also shows unavailable recipes.") : T("«Рецепты» сверху покажут, как получить сам предмет.", "The Recipes tab shows how to craft this item."), 200f, 434f, Muted, 0.86f, 540f, 28f);
			return;
		}
		int onPage = Math.Min(12, Filtered.Count - Page * 12);
		int cols = Math.Min(4, onPage);
		float rootCenter = 256 + (Math.Min(4, ChainHistory.Count) - 1) * 144;
		float left = Math.Min(rootCenter, 256f);
		float right = Math.Max(rootCenter, 256 + (cols - 1) * 144);
		Box(sb, rootCenter, 316f, 1f, 28f, Border, border: false, 0f);
		Box(sb, left, 344f, Math.Max(1f, right - left), 1f, Border, border: false, 0f);
		for (int col = 0; col < cols; col++)
		{
			Box(sb, 256 + col * 144, 344f, 1f, 4f, Border, border: false, 0f);
		}
		for (int n = 0; n < onPage; n++)
		{
			int index2 = Filtered[Page * 12 + n];
			Item item2 = Result(index2);
			Recipe recipe = Main.recipe[index2];
			ChainMaterialPreview materialPreview = PreviewChainMaterials(recipe, item2);
			bool flag = BookProgression.RecipeStage(index2) == BookStage.Unknown;
			BookRect cell = BookGeometry.ChainCell(n);
			BookRect drill = BookGeometry.ChainDrill(n);
			bool hover = !ModMenu && cell.Contains(Mouse.X, Mouse.Y);
			bool active = Selected == index2;
			SpriteBatch sb4 = sb;
			float x = cell.X;
			float y = cell.Y;
			float width = cell.Width;
			float height = cell.Height;
			Color color;
			if (hover)
			{
				color = new Color(61, 84, 67);
			}
			else
			{
				color = (active ? new Color(47, 66, 53) : Raised);
			}
			Box(sb4, x, y, width, height, color, border: true, 10f, active ? new Color?(Accent) : ((Color?)null), (!active) ? 1 : 2);
			Icon(sb, item2, cell.X + 8f, cell.Y + 8f, 32f, tooltip: false);
			CenterLabel(sb, item2.Name, cell.X + 48f, cell.Y + 8f, 40f, 32f, TextColor, 0.72f);
			if (flag)
			{
				CenterLabel(sb, "?", cell.X + 28f, cell.Y + 28f, 16f, 16f, new Color(211, 173, 74), 0.64f);
			}
			CenterLabel(sb, materialPreview.Summary, cell.X + 8f, cell.Y + 48f, cell.Width - 16f, 16f, materialPreview.Color, 0.7f);
			Button(sb, drill, ">");
			if (hover && !drill.Contains(Mouse.X, Mouse.Y))
			{
				Main.HoverItem = new Item();
				Main.hoverItemName = "";
				MouseTip(materialPreview.Tooltip + "\n" + StageExplanation(item2, recipe));
			}
			if (!ModMenu && drill.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Продолжить цепочку от: ", "Continue chain from: ") + item2.Name + "\n" + materialPreview.Summary + "\n" + StageExplanation(item2, recipe));
			}
		}
		string chainHint = ((ChainUnknownVisibleCount > 0) ? (T("С известным этапом: ", "Known stage: ") + (Filtered.Count - ChainUnknownVisibleCount) + T(" · Не определён (?): ", " · Uncertain (?): ") + ChainUnknownVisibleCount) : T("Клик — посмотреть рецепт    > — следующий шаг", "Click: inspect recipe    >: next step"));
		Text(sb, chainHint, 184f, 588f, Muted, 0.8f, 576f, 16f);
		if (Hit(184f, 588f, 576f, 16f))
		{
			MouseTip(T("Клик — посмотреть рецепт; > — следующий шаг.\n? — этап не определён: рецепт оставлен видимым, но не считается точным совпадением с этапом.\nCtrl+Backspace — сбросить фильтры.", "Click: inspect recipe; >: next step.\n? means uncertain stage: the recipe remains visible, not a confirmed stage match.\nCtrl+Backspace resets filters."));
		}
	}

	// Bulk inventory actions: four equal buttons, disabled until a network is connected.
	private static void DrawStorageToolbar(SpriteBatch sb)
	{
		Box(sb, BookGeometry.SamplePanel, Surface, border: true, 10f);
		bool ready = BookStorage.Connected;
		CardButton(sb, BookGeometry.StorageQuickStack, T("Быстрый стак", "Quick Stack"), ready);
		CardButton(sb, BookGeometry.StorageDepositAll, T("Сложить всё", "Deposit All"), ready);
		CardButton(sb, BookGeometry.StorageRestock, T("Пополнить", "Restock"), ready);
		CardButton(sb, BookGeometry.StorageDeposit, T("Положить", "Deposit Held"), ready && !Main.mouseItem.IsAir);
		if (ModMenu)
		{
			return;
		}
		if (BookGeometry.StorageQuickStack.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Сложить то, что уже есть в хранилище · Ctrl+Q", "Store items already in storage · Ctrl+Q"));
		}
		if (BookGeometry.StorageDepositAll.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Сложить инвентарь, кроме панели и избранного · Ctrl+Shift+D", "Store inventory except hotbar and favorites · Ctrl+Shift+D"));
		}
		if (BookGeometry.StorageRestock.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Дополнить стопки инвентаря из хранилища · Ctrl+R", "Top up inventory stacks from storage · Ctrl+R"));
		}
		if (BookGeometry.StorageDeposit.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Положить предмет с курсора · Ctrl+D", "Store the item on your cursor · Ctrl+D"));
		}
	}

	// Details panel: status, selected stack, withdraw shortcuts, last action; network block pinned to the bottom.
	private static void DrawStorageDetails(SpriteBatch sb)
	{
		Box(sb, BookGeometry.DetailsPanel, Surface, border: false, 12f);
		BookRect content = BookGeometry.StorageContent;
		string state;
		Color stateColor;
		if (BookStorage.Connected)
		{
			state = (BookStorage.Updating ? T("Синхронизация", "Syncing") : T("Подключено", "Connected"));
			stateColor = (BookStorage.Updating ? new Color(219, 184, 112) : Accent);
		}
		else
		{
			state = BookStorage.ConnectionStatus;
			stateColor = Muted;
		}
		BookRect status = BookGeometry.StorageStatus;
		CenterLabel(sb, state, status.X, status.Y, status.Width, status.Height, stateColor, 0.8f, rightAligned: true);
		float stateWidth = Math.Min(status.Width, Ink(state).Width * 0.8f * TextScale);
		Box(sb, status.X + status.Width - stateWidth - 14f, status.Y + 6f, 8f, 8f, stateColor, border: false, 4f);
		Text(sb, "MAGIC STORAGE", content.X, content.Y, Muted, 0.8f, content.Width - stateWidth - 28f, 20f);
		if (status.Contains(Mouse.X, Mouse.Y) && !ModMenu)
		{
			MouseTip(BookStorage.ConnectionDetails);
		}
		BookRect icon = BookGeometry.StorageItemIcon;
		Item selected = SelectedItem;
		if (selected != null)
		{
			Box(sb, icon, Raised, border: false, 8f);
			Icon(sb, selected, icon.X + 4f, icon.Y + 4f, 40f);
			Text(sb, selected.Name, icon.X + 60f, icon.Y, TextColor, 1f, content.Width - 60f, 26f);
			Text(sb, "×" + selected.stack + " · " + ModName(selected.ModItem?.Mod.Name ?? "Terraria"), icon.X + 60f, icon.Y + 28f, Muted, 0.85f, content.Width - 60f, 20f);
		}
		else
		{
			Text(sb, BookStorage.Connected ? T("Предмет не выбран", "No item selected") : T("Нет предметов", "No items"), icon.X, icon.Y, TextColor, 1f, content.Width, 26f);
			Text(sb, BookStorage.Connected ? T("Нажмите на предмет в сетке", "Click an item in the grid") : T("Подойдите к точке доступа", "Stand near a storage access point"), icon.X, icon.Y + 28f, Muted, 0.85f, content.Width, 20f);
		}
		Box(sb, content.X, icon.Y + icon.Height + 16f, content.Width, 1f, Border);
		BookRect keys = BookGeometry.StorageShortcuts;
		Text(sb, T("УПРАВЛЕНИЕ", "SHORTCUTS"), keys.X, keys.Y, Muted, 0.8f, keys.Width, 20f);
		StorageKeyRow(sb, keys.Y + 28f, T("Shift+ЛКМ", "Shift+LMB"), T("Взять стопку", "Take stack"));
		StorageKeyRow(sb, keys.Y + 56f, T("ПКМ", "RMB"), T("Взять одну", "Take one"));
		StorageKeyRow(sb, keys.Y + 84f, T("Alt+ЛКМ", "Alt+LMB"), T("В избранное", "Favorite"));
		if (BookStorage.Message.Length > 0)
		{
			BookRect message = BookGeometry.StorageMessage;
			Text(sb, BookStorage.Message, message.X, message.Y, TextColor, 0.85f, message.Width, message.Height);
		}
		BookRect network = BookGeometry.StorageNetwork;
		Box(sb, content.X, network.Y - 16f, content.Width, 1f, Border);
		Text(sb, T("СЕТЬ", "NETWORK"), network.X, network.Y, Muted, 0.8f, network.Width, 20f);
		if (BookStorage.Connected)
		{
			Text(sb, BookStorage.AccessName, network.X, network.Y + 26f, TextColor, 0.85f, network.Width, 20f);
			Text(sb, BookStorage.AccessCoordinates, network.X, network.Y + 48f, Muted, 0.8f, network.Width, 20f);
		}
		else
		{
			Text(sb, T("Подключается сама у точки доступа", "Connects by itself near an access point"), network.X, network.Y + 26f, Muted, 0.85f, network.Width, 20f);
		}
		CardButton(sb, BookGeometry.StorageConnect, BookStorage.Connected ? T("Обновить", "Refresh") : T("Подключить", "Connect"), enabled: true, !BookStorage.Connected);
		CardButton(sb, BookGeometry.StorageDisconnect, T("Отключить", "Disconnect"), BookStorage.Connected);
		CardButton(sb, BookGeometry.StorageNative, T("Открыть Magic Storage", "Open Magic Storage"));
		if (ModMenu)
		{
			return;
		}
		if (BookGeometry.StorageConnect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(BookStorage.Connected ? T("Перечитать хранилище · F5", "Reload storage · F5") : T("Подключиться к ближайшей точке доступа", "Connect to the nearest access point"));
		}
		if (BookGeometry.StorageDisconnect.Contains(Mouse.X, Mouse.Y) && BookStorage.Connected)
		{
			MouseTip(T("Не подключаться до следующего открытия книги", "Stay offline until the book is reopened"));
		}
		if (BookGeometry.StorageNative.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Окно Magic Storage; книга закроется", "Magic Storage window; closes the book"));
		}
	}

	private static void StorageKeyRow(SpriteBatch sb, float y, string key, string action)
	{
		BookRect content = BookGeometry.StorageContent;
		HelpKeycap(sb, new BookRect(content.X, y, 112f, 22f), key);
		Text(sb, action, content.X + 124f, y, TextColor, 0.85f, content.Width - 124f, 22f);
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
		}
		Box(sb, -4f, -4f, 1128f, 704f, new Color(0, 0, 0, 170), border: false, 22f);
		Box(sb, 0f, 0f, 1120f, 696f, Panel, border: true, 18f);
		if (AcquisitionOpen)
		{
			DrawAcquisition(sb);
			return;
		}
		Box(sb, 1f, 1f, 1118f, 90f, Surface, border: false, 17f);
		Text(sb, T("Книга рецептов", "Recipe Book"), 24f, 16f, TextColor, 1.3f, 276f, 32f);
		if (TrackingView)
		{
			Button(sb, BookGeometry.StageFilter, T("Целей: ", "Goals: ") + TrackedGoals.Count);
		}
		else
		{
			DrawStageFilter(sb);
		}
		Text(sb, T("Рецептов: ", "Recipes: ") + All.Count + T("  ·  Предметов: ", "  ·  Items: ") + AllItems.Count, 24f, 56f, Muted, 0.86f, 280f, 24f);
		if (Hit(26f, 57f, 280f, 23f))
		{
			MouseTip(T("Просканировано ячеек: ", "Scanned recipe slots: ") + Recipe.numRecipes + "\n" + T("Зарегистрировано рецептов: ", "Registered recipes: ") + RegisteredRecipes + "\n" + T("Включено: ", "Enabled: ") + All.Count + T("  Отключено модами: ", "  Disabled by mods: ") + DisabledRecipes + "\n" + T("Загружено предметов: ", "Loaded item types: ") + AllItems.Count);
		}
		Box(sb, BookGeometry.TabPanel, new Color(22, 31, 25), border: false, 12f);
		Button(sb, BookGeometry.Tab(0), T("Предметы", "Items"), Catalogue, fitWidth: true);
		Button(sb, BookGeometry.Tab(1), T("Рецепты", "Recipes"), !Catalogue && !Chains && !StorageView && !RecentView && !TrackingView, fitWidth: true);
		Button(sb, BookGeometry.Tab(2), T("Цепочки", "Chains"), Chains, fitWidth: true);
		Button(sb, BookGeometry.Tab(3), T("Хранилище", "Storage"), StorageView, fitWidth: true);
		Button(sb, BookGeometry.Tab(4), T("Недавние", "Recent"), RecentView, fitWidth: true);
		Button(sb, BookGeometry.Tab(5), T("Трекер", "Tracking"), TrackingView, fitWidth: true);
		if (BookGeometry.Tab(5).Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Отслеживание крафтов · Ctrl+6", "Tracked crafts · Ctrl+6"));
		}
		Button(sb, BookGeometry.HeaderClose, "X");
		if (TrackingView)
		{
			DrawTrackingTab(sb);
		}
		else
		{
			Box(sb, BookGeometry.CategoryPanel, new Color(23, 30, 26), border: false, 12f);
			Text(sb, T("КАТЕГОРИИ", "CATEGORIES"), 12f, 100f, Muted, 0.78f, 140f, 20f);
			for (int visible = 0; visible < 30; visible++)
			{
				int index = CategoryRow * 3 + visible;
				if (index >= Categories.Count)
				{
					break;
				}
				BookCategory category = Categories[index];
				BookRect rect = BookGeometry.CategoryCell(visible);
				bool hover = rect.Contains(Mouse.X, Mouse.Y);
				bool active = Category == index;
				SpriteBatch sb2 = sb;
				float x = rect.X;
				float y = rect.Y;
				float width = rect.Width;
				float height = rect.Height;
				Color color;
				if (hover)
				{
					color = new Color(61, 84, 67);
				}
				else
				{
					color = (active ? new Color(50, 76, 59) : Raised);
				}
				Box(sb2, x, y, width, height, color, border: true, 8f, active ? new Color?(Accent) : ((Color?)null), (!active) ? 1 : 2);
				if (ContentSamples.ItemsByType.TryGetValue(category.IconType, out var icon))
				{
					Icon(sb, icon, rect.X + 8f, rect.Y + 8f, 28f, tooltip: false);
				}
				if (hover)
				{
					MouseTip(category.Label(Ru) + "\n" + T("Нажмите для фильтра. Повторный клик — сброс.", "Click to filter. Click again to reset."));
				}
			}
			if (MaxCategoryRow > 0)
			{
				float track = 476f;
				float thumb = Math.Max(28f, track * 10f / (float)(MaxCategoryRow + 10));
				Box(sb, 157f, 124f, 3f, track, Raised);
				Box(sb, 157f, 124f + (track - thumb) * (float)CategoryRow / (float)MaxCategoryRow, 3f, thumb, Accent);
			}
			Button(sb, BookGeometry.CategoryPrevious, "^");
			Label(sb, BookGeometry.CategoryPager, CategoryRow + 1 + "/" + (MaxCategoryRow + 1), Muted, 0.78f);
			Button(sb, BookGeometry.CategoryNext, "v");
			BookRect search = BookGeometry.Search;
			Box(sb, search.X, search.Y, search.Width, search.Height, Typing ? new Color(35, 51, 40) : new Color(19, 28, 22), border: true, 10f, Typing ? new Color?(Accent) : ((Color?)null), (!Typing) ? 1 : 2);
			BookRect searchText = BookGeometry.SearchText;
			CenterLabel(sb, (Query.Length > 0) ? (Query + ((Typing && (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0) ? "|" : "")) : T("Поиск предмета...", "Search items..."), searchText.X, searchText.Y, searchText.Width, searchText.Height, (Query.Length > 0) ? TextColor : Muted, 1f, rightAligned: false, leftAligned: true);
			Button(sb, BookGeometry.SearchClear, "X");
			Button(sb, BookGeometry.ModFilter, (ModFilter.Length == 0) ? T("Все источники", "All sources") : ModName(ModFilter));
			Button(sb, BookGeometry.FavoritesFilter, "", FavoritesOnly);
			if (ContentSamples.ItemsByType.TryGetValue(75, out var star))
			{
				Icon(sb, star, BookGeometry.FavoritesFilter.X + 10f, BookGeometry.FavoritesFilter.Y + 12f, 24f, tooltip: false);
			}
			if (!TrackingView && BookGeometry.FavoritesFilter.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Только избранное", "Favorites only"));
			}
			Button(sb, BookGeometry.Refresh, "R");
			if (!TrackingView && BookGeometry.Refresh.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Обновить рецепты", "Refresh recipes"));
			}
			Button(sb, BookGeometry.Sort, RecentView ? T("Новые", "Newest") : SortLabel());
			SpriteBatch sb3 = sb;
			BookRect sortDirection = BookGeometry.SortDirection;
			string label;
			if (RecentView)
			{
				label = "--";
			}
			else
			{
				label = (SortDescending ? "Z>A" : "A>Z");
			}
			Button(sb3, sortDirection, label);
			if (BookGeometry.Sort.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(RecentView ? T("Недавние всегда идут от новых к старым", "Recent recipes always appear newest first") : T("Сортировка: щёлкните для смены поля (имя, ID, мод)", "Sort: click to cycle field (name, ID, mod)"));
			}
			if (BookGeometry.SortDirection.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(RecentView ? T("Недавние всегда идут от новых к старым", "Recent recipes always appear newest first") : T("Направление сортировки", "Sort direction"));
			}
			if (StorageView)
			{
				DrawStorageToolbar(sb);
			}
			else if (RecentView)
			{
				DrawRecentToolbar(sb);
			}
			else
			{
				DrawSample(sb);
			}
			if (Chains)
			{
				DrawChains(sb);
			}
			else
			{
				for (int n = 0; n < 40; n++)
				{
					int f = Page * 40 + n;
					if (f >= Filtered.Count)
					{
						break;
					}
					int num = Filtered[f];
					Item item = Result(num);
					BookRect cell = BookGeometry.RecipeCell(n);
					int x2 = (int)cell.X;
					int y2 = (int)cell.Y;
					bool hovered = !ModMenu && cell.Contains(Mouse.X, Mouse.Y);
					bool selected = num == Selected;
					SpriteBatch sb4 = sb;
					float x3 = x2;
					float y3 = y2;
					Color color2;
					if (hovered)
					{
						color2 = new Color(61, 84, 67);
					}
					else
					{
						color2 = (selected ? new Color(47, 66, 53) : Raised);
					}
					Box(sb4, x3, y3, 64f, 64f, color2, border: true, 10f, selected ? new Color?(Accent) : ((Color?)null), (!selected) ? 1 : 2);
					Icon(sb, item, x2 + 12, y2 + 12, 40f, tooltip: false);
					if (hovered)
					{
						Main.HoverItem = item.Clone();
						Main.hoverItemName = item.Name;
					}
					if ((!Catalogue || StorageView) && item.stack > 1)
					{
						Text(sb, item.stack.ToString(), x2 + 3, y2 + 42, TextColor, 0.8f, 58f, 20f);
					}
					if (Favorites.Contains(ItemKey(item)))
					{
						Text(sb, "+", x2 + 48, y2 + 1, Accent, 0.82f, 15f, 22f);
					}
				}
			}
			if (!Chains && Filtered.Count == 0)
			{
				string emptyStorage;
				if (!BookStorage.Connected || BookStorage.CraftInitializing)
				{
					emptyStorage = BookStorage.Status;
				}
				else
				{
					emptyStorage = ((BookStorage.Items.Count == 0) ? T("В этой сети пока нет предметов", "This network is empty") : T("Нет совпадений · Ctrl+Backspace — сбросить фильтры", "No matches · Ctrl+Backspace resets filters"));
				}
				string emptyRecent;
				if (Main.LocalPlayer.GetModPlayer<BookPlayer>().RecentRecipes.Count != 0)
				{
					emptyRecent = ((BookRecent.ResolveRecipeIndexes().Count == 0) ? T("Рецепты из истории сейчас не загружены", "Recipes in history are not currently loaded") : T("Нет совпадений · Ctrl+Backspace — сбросить фильтры", "No matches · Ctrl+Backspace resets filters"));
				}
				else
				{
					emptyRecent = T("Здесь появятся недавно созданные рецепты", "Successfully crafted recipes will appear here");
				}
				SpriteBatch sb5 = sb;
				string text;
				if (StorageView)
				{
					text = emptyStorage;
				}
				else
				{
					text = (RecentView ? emptyRecent : T("Ничего не найдено. Проверьте режим и фильтры.", "Nothing found. Check mode and filters."));
				}
				Text(sb5, text, 188f, 290f, Muted, 1f, 556f, 30f);
			}
			if (Chains)
			{
				Button(sb, BookGeometry.ChainBack, "<<", ChainHistory.Count > 1);
				if (BookGeometry.ChainBack.Contains(Mouse.X, Mouse.Y))
				{
					MouseTip(T("На шаг назад по цепочке", "Go back one chain step"));
				}
				Button(sb, BookGeometry.ChainPrev, "<");
			}
			else
			{
				Button(sb, BookGeometry.PagePrevious, "<");
			}
			CenterLabel(sb, (StorageView ? T("Стопок: ", "Stacks: ") : (Catalogue ? T("Предметов: ", "Items: ") : (RecentView ? T("Недавние рецепты: ", "Recent recipes: ") : T("Рецептов: ", "Recipes: ")))) + Filtered.Count + "  ·  " + (Page + 1) + " / " + Pages, BookGeometry.PageLabel.X, BookGeometry.PageLabel.Y, BookGeometry.PageLabel.Width, BookGeometry.PageLabel.Height, Muted, 0.86f);
			Button(sb, BookGeometry.PageNext, ">");
			if (StorageView)
			{
				DrawStorageDetails(sb);
			}
			else
			{
				Box(sb, BookGeometry.DetailsPanel, Surface, border: false, 12f);
				Text(sb, RecentView ? T("НЕДАВНИЙ РЕЦЕПТ", "RECENT RECIPE") : T("ВЫБРАННЫЙ РЕЦЕПТ", "SELECTED RECIPE"), 812f, 108f, Muted, 0.82f, 284f, 24f);
				Item chosen = SelectedItem;
				if (chosen != null)
				{
					Recipe r = SelectedRecipe;
					if (!DrawArmorSet(sb, chosen))
					{
						Icon(sb, chosen, 926f, 136f, 56f);
					}
					bool armorStats = HasArmorDefense(chosen);
					BookRect nameRect = (armorStats ? BookGeometry.ArmorName : new BookRect(812f, 200f, 284f, 32f));
					FixedLabel(sb, chosen.Name + ((r != null && r.createItem.stack > 1) ? (" x" + r.createItem.stack) : ""), nameRect.X, nameRect.Y, TextColor, armorStats ? 1f : 1.16f, nameRect.Width, nameRect.Height);
					if (armorStats)
					{
						DrawArmorDefense(sb, chosen);
					}
					string itemStage = StageName(chosen, r);
					BookRect sourceRect = (armorStats ? BookGeometry.ArmorSource : new BookRect(812f, 240f, 284f, 20f));
					FixedLabel(sb, ModName((r != null) ? Owner(r) : (chosen.ModItem?.Mod.Name ?? "Terraria")) + " · " + itemStage, sourceRect.X, sourceRect.Y, Muted, 0.78f, sourceRect.Width, sourceRect.Height);
					if (sourceRect.Contains(Mouse.X, Mouse.Y))
					{
						MouseTip(StageExplanation(chosen, r));
					}
					if ((Catalogue || ArmorFocus > 0) && VariantCount > 1)
					{
						Button(sb, BookGeometry.VariantPrevious, "<");
						Button(sb, BookGeometry.VariantNext, ">");
					}
					if (r != null)
					{
						DrawBatchChoices(sb, r);
						List<Detail> rows = Details(r);
						DetailsPage = Math.Clamp(DetailsPage, 0, Math.Max(0, (rows.Count - 1) / 3));
						for (int i = 0; i < 3; i++)
						{
							int j = DetailsPage * 3 + i;
							if (j >= rows.Count)
							{
								break;
							}
							Detail d = rows[j];
							BookRect row = BookGeometry.DetailRow(i);
							float y4 = row.Y;
							Box(sb, row, Raised);
							BookRect icon2 = BookGeometry.DetailIcon(i);
							if (d.Icon != null)
							{
								Icon(sb, d.Icon, icon2.X, icon2.Y, icon2.Width);
							}
							float tx = ((d.Icon != null) ? (icon2.X + icon2.Width + 8f) : (row.X + 8f));
							bool num2 = d.Icon != null && !d.Icon.IsAir;
							BookRect obtain = BookGeometry.DetailObtain(i);
							DrawTextPair(width: (num2 ? (obtain.X - 8f) : (row.X + row.Width - 8f)) - tx, sb: sb, name: d.Name, subtitle: d.Sub, x: tx, y: y4, height: row.Height, titleColor: d.Color, subColor: d.SubColor);
							if (num2)
							{
								Button(sb, obtain, "?");
							}
							if (num2 && obtain.Contains(Mouse.X, Mouse.Y))
							{
								MouseTip(T("Как получить: ", "How to obtain: ") + d.Name);
							}
							else if (row.Contains(Mouse.X, Mouse.Y))
							{
								MouseTip(d.Name + "\n" + ((d.Tooltip.Length > 0) ? d.Tooltip : (d.Sub + ((d.Icon != null) ? T("\nЛКМ — цепочка этого предмета", "\nLeft-click: this item's chain") : ""))));
							}
						}
						Button(sb, BookGeometry.DetailPrevious, "<");
						Label(sb, BookGeometry.DetailPager, DetailsPage + 1 + " / " + (MaxDetailsPage() + 1), Muted, 0.86f);
						Button(sb, BookGeometry.DetailNext, ">");
					}
					else
					{
						Text(sb, T("Обычного рецепта нет", "No ordinary crafting recipe"), 812f, 285f, Muted, 1f, 284f, 30f);
						Text(sb, T("Этот предмет получают иначе:", "Obtained by other means:"), 812f, 326f, Muted, 0.86f, 284f, 28f);
						Text(sb, T("добыча, ловля, торговля и т. п.", "loot, catching, shops, etc."), 812f, 358f, Muted, 0.86f, 284f, 28f);
						Text(sb, T("Книга не добавляет ему рецепт.", "The book does not add a recipe."), 812f, 404f, Muted, 0.86f, 284f, 28f);
						Button(sb, BookGeometry.SelectedItemObtain, T("Как получить", "How to obtain"));
						if (BookGeometry.SelectedItemObtain.Contains(Mouse.X, Mouse.Y))
						{
							MouseTip(T("Показать источники: ", "Show sources: ") + chosen.Name);
						}
					}
					bool favorite = Favorites.Contains(ItemKey(chosen));
					Button(sb, BookGeometry.FavoriteAction, favorite ? T("Избранное +", "Favorite +") : T("Избранное", "Favorite"), favorite);
					Button(sb, BookGeometry.CraftAction, T("Создать", "Craft") + ((r != null) ? (" ×" + PreviewBatches(r)) : ""), BookCrafting.CanCraft(r));
					if (BookGeometry.CraftAction.Contains(Mouse.X, Mouse.Y))
					{
						MouseTip(BookCrafting.Reason(r, Ru));
					}
					DrawTrackButton(sb, r);
				}
			}
		}
		Box(sb, 0f, 648f, 1120f, 1f, Border);
		Button(sb, BookGeometry.DragHandle, T("Двиг.", "Move"));
		Button(sb, BookGeometry.WindowSizeDecrease, "-");
		CenterLabel(sb, WindowScalePercent + "%", BookGeometry.WindowSizeLabel.X, BookGeometry.WindowSizeLabel.Y, BookGeometry.WindowSizeLabel.Width, BookGeometry.WindowSizeLabel.Height, Muted, 0.78f);
		Button(sb, BookGeometry.WindowSizeIncrease, "+");
		if (BookGeometry.DragHandle.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Перетащите, чтобы переместить окно", "Drag to move the window"));
		}
		if (BookGeometry.WindowSizeDecrease.Contains(Mouse.X, Mouse.Y) || BookGeometry.WindowSizeIncrease.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Размер окна относительно настройки масштаба клиента", "Window size relative to the existing client scale setting"));
		}
		Text(sb, TrackingView ? T("PgUp/PgDn страницы · Ctrl+6 трекер · F1 помощь", "PgUp/PgDn pages · Ctrl+6 tracking · F1 help") : T("Ctrl+F поиск · PgUp/PgDn страницы · F1 помощь", "Ctrl+F search · PgUp/PgDn pages · F1 help"), 204f, 660f, Muted, 0.8f, 336f, 24f);
		Button(sb, BookGeometry.CraftSource, BookStorage.UseForCrafting ? T("Крафт: Magic Storage", "Craft: Magic Storage") : T("Крафт: инвентарь", "Craft: inventory"), BookStorage.UseForCrafting);
		if (BookGeometry.CraftSource.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Нажмите для смены источника. Magic Storage требует подключённый интерфейс крафта и создаёт результат в хранилище. Инвентарь использует обычный крафт; источники не смешиваются.", "Click to change source. Magic Storage needs a connected Crafting Interface and deposits results into storage. Inventory uses native player crafting; sources are not mixed."));
		}
		Button(sb, BookGeometry.Help, "?");
		CenterLabel(sb, "MRB " + ModContent.GetInstance<ModernRecipeBook>().Version, 990f, 660f, BookGeometry.HeaderClose.X + BookGeometry.HeaderClose.Width - 990f, 24f, Muted, 0.8f, rightAligned: true);
		if (!TrackingView && BookGeometry.Search.Contains(Mouse.X, Mouse.Y) && !Typing)
		{
			MouseTip(T("Ctrl+F · Поиск по названию, ID или моду", "Ctrl+F · Search name, ID or mod"));
		}
		if (!TrackingView && BookGeometry.Refresh.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("F5 · Обновить рецепты и сеть", "F5 · Refresh recipes and network"));
		}
		if (!TrackingView && BookGeometry.FavoritesFilter.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Только избранное · Alt+ЛКМ по предмету — добавить/убрать", "Favorites only · Alt+left-click an item to toggle"));
		}
		if (BookGeometry.Help.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("F1 · Все сочетания клавиш", "F1 · All keyboard shortcuts"));
		}
		if (ModMenu)
		{
			DrawModMenu(sb);
		}
		if (HelpOpen)
		{
			Main.HoverItem = new Item();
			Main.hoverItemName = "";
			DrawHelp(sb);
		}
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
		Box(sb, 0f, 0f, 1120f, 696f, new Color(10, 16, 12, 235), border: false, 18f);
		BookRect modal = BookGeometry.HelpPanel;
		Box(sb, modal, Surface, border: true, 12f);
		Text(sb, T("Горячие клавиши", "Shortcuts"), modal.X + 24f, modal.Y + 24f, TextColor, 1.1f, modal.Width - 112f, 32f);
		CardButton(sb, BookGeometry.HelpClose, "X");
		if (BookGeometry.HelpClose.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Закрыть · Esc или F1", "Close · Esc or F1"));
		}
		Box(sb, modal.X + 24f, modal.Y + 80f, modal.Width - 48f, 1f, Border);
		float[] top = new float[2]
		{
			BookHelp.Column(0).Y,
			BookHelp.Column(1).Y
		};
		foreach (BookHelp.Section section in BookHelp.Sections)
		{
			if (section.StorageOnly && !BookStorage.Installed)
			{
				continue;
			}
			BookRect column = BookHelp.Column(section.Column);
			float y = top[section.Column];
			Text(sb, T(section.Ru, section.En), column.X, y, Muted, 0.8f, column.Width, 20f);
			y += BookHelp.HeaderStep;
			foreach (BookHelp.Entry row in section.Rows)
			{
				HelpKeycap(sb, new BookRect(column.X, y, BookHelp.KeyWidth, BookHelp.KeyHeight), T(row.KeyRu, row.KeyEn));
				Text(sb, T(row.Ru, row.En), column.X + BookHelp.KeyWidth + 12f, y, TextColor, 0.85f, column.Width - BookHelp.KeyWidth - 12f, BookHelp.KeyHeight);
				y += BookHelp.RowStep;
			}
			top[section.Column] = y + BookHelp.SectionGap;
		}
	}
}
