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

	private static bool AcquisitionInfoOpen;

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

	private static bool EnemyInfoOpen;

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

	private static bool SortMenu;

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
				if (!HelpOpen && !AcquisitionOpen && !ModMenu && !SortMenu)
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

	// Chains list 12 recipes; Storage keeps its toolbar above a 5-row grid; the other tabs show 6 rows.
	private static int ActivePageSize
	{
		get
		{
			if (!Chains)
			{
				return StorageView ? 40 : 48;
			}
			return 12;
		}
	}

	private static bool Ru => Language.ActiveCulture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

	private static HashSet<string> Favorites => Main.LocalPlayer.GetModPlayer<BookPlayer>().Favorites;

	private static int MaxCategoryRow => Math.Max(0, Categories.Count - BookGeometry.VisibleCategoryRows);

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
		AcquisitionOpen = (AcquisitionInfoOpen = false);
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
		AcquisitionInfoOpen = false;
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
				string body = T("Материалы: ", "Materials: ") + string.Join(", ", materials) + ". " + ((stations.Count == 0) ? T("Вручную.", "By hand.") : (T("Станции: ", "Stations: ") + string.Join(", ", stations) + "."));
				if (recipe.Conditions.Count > 0)
				{
					body = body + " " + T("Условия: ", "Conditions: ") + string.Join("; ", recipe.Conditions.Select((Condition c) => c.Description.Value));
				}
				body = body + " " + T("Нажмите, чтобы открыть рецепт.", "Click to open this recipe.");
				body = body + " " + ModName(Owner(recipe));
				Add(new BookAcquisitionEntry((stations.Count == 0) ? T("Вручную", "By hand") : string.Join(", ", stations), body, BookAcquisitionKind.Craft, "", "×" + recipe.createItem.stack, type, 0, recipe.Conditions.Count > 0, index));
			}
		}
		try
		{
			HashSet<IItemDropRule> own = new HashSet<IItemDropRule>(Main.ItemDropsDB.GetRulesForNPCID(0, includeGlobalDrops: false), ReferenceEqualityComparer.Instance);
			Drops(from rule in Main.ItemDropsDB.GetRulesForNPCID(0)
				where !own.Contains(rule)
				select rule, T("Подходящие враги", "Eligible enemies"), BookAcquisitionKind.Drop, 0, 0, "<global>");
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
					string body2 = T("Магазин: ", "Shop: ") + shop.Name + ".";
					string[] conditions = entry.Conditions.Select((Condition c) => c.Description.Value).ToArray();
					if (conditions.Length != 0)
					{
						body2 = body2 + " " + T("Условия: ", "Conditions: ") + string.Join("; ", conditions);
					}
					body2 = body2 + " " + T("Наличие и цену проверяйте у продавца.", "Check current stock and price with the vendor.");
					Add(new BookAcquisitionEntry(Lang.GetNPCNameValue(shop.NpcType), body2, BookAcquisitionKind.Shop, "", "", 0, shop.NpcType, conditions.Length != 0, -1, "Terraria", string.Join("; ", conditions), shop.Name));
				}
				catch (Exception)
				{
					AcquisitionReadErrors++;
				}
			}
		}
		if (reports.Count == 0)
		{
			Add(new BookAcquisitionEntry(T("Источники не найдены", "No sources found"), T("Это не означает, что предмет недоступен. Добыча блоков, сундуки мира, рыбалка и особые механики модов могут не публиковать данные в этих списках.", "This does not mean the item is unobtainable. Mining, world chests, fishing and custom mod mechanics may not publish data in these lists."), BookAcquisitionKind.Unknown));
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
							string full = (string.IsNullOrWhiteSpace(description) ? T("Особое условие источника", "Special source condition") : description);
							conditions2.Add(full);
							string item2;
							if (condition is Conditions.IsExpert)
							{
								item2 = T("Эксперт / Мастер", "Expert / Master");
							}
							else if (condition is Conditions.NotExpert)
							{
								item2 = T("Обычный", "Classic");
							}
							else if (condition is Conditions.IsMasterMode)
							{
								item2 = T("Мастер", "Master");
							}
							else
							{
								item2 = ((!(condition is Conditions.NotMasterMode)) ? full : T("Не Мастер", "Not Master"));
							}
							shortConditions.Add(item2);
						}
						catch (Exception)
						{
							string unavailable = T("Условие мода: описание недоступно", "Mod condition: description unavailable");
							conditions2.Add(unavailable);
							shortConditions.Add(unavailable);
							AcquisitionReadErrors++;
						}
					}
					string body3 = T("Базовый шанс: ", "Base chance: ") + BookAcquisitionRules.Chance(rate.dropRate) + T(" · Количество: ", " · Amount: ") + BookAcquisitionRules.Stack(rate.stackMin, rate.stackMax) + ".";
					if (conditions2.Count > 0)
					{
						body3 = body3 + " " + T("Условия: ", "Conditions: ") + string.Join("; ", conditions2.Distinct());
					}
					Add(new BookAcquisitionEntry(name, body3, kind, BookAcquisitionRules.Chance(rate.dropRate), "×" + BookAcquisitionRules.Stack(rate.stackMin, rate.stackMax), itemType, npcNetId, conditions2.Count > 0, -1, owner, string.Join("; ", shortConditions.Distinct()), BookAcquisitionGrouping.DropRuleKey(rate.dropRate, rate.stackMin, rate.stackMax, string.Join("|", conditionKeys.OrderBy((string x) => x, StringComparer.Ordinal)))));
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
		AcquisitionInfoOpen = false;
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
		if (Press(Keys.Escape) && AcquisitionInfoOpen)
		{
			AcquisitionInfoOpen = false;
			return;
		}
		if ((Press(Keys.Escape) && AcquisitionGroup >= 0) || (click && AcquisitionGroup >= 0 && BookGeometry.AcquisitionBack.Contains(Mouse.X, Mouse.Y)))
		{
			BackAcquisitionGroup();
			return;
		}
		if (Press(Keys.Escape) || (click && (BookGeometry.AcquisitionClose.Contains(Mouse.X, Mouse.Y) || !BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y))))
		{
			CloseAcquisition();
			return;
		}
		if (click && BookGeometry.AcquisitionInfo.Contains(Mouse.X, Mouse.Y))
		{
			AcquisitionInfoOpen = !AcquisitionInfoOpen;
			return;
		}
		if (AcquisitionInfoOpen)
		{
			if (click)
			{
				AcquisitionInfoOpen = false;
			}
			return;
		}
		if (click && AcquisitionGroup >= 0 && CanOpenEnemy(AcquisitionSources[AcquisitionGroup]) && BookGeometry.EnemyOpenAction.Contains(Mouse.X, Mouse.Y))
		{
			OpenEnemyCard(AcquisitionSources[AcquisitionGroup]);
			return;
		}
		int variantStep = ((AcquisitionGroup < 0) ? ((click && BookGeometry.AcquisitionVariantPrevious.Contains(Mouse.X, Mouse.Y)) ? (-1) : ((click && BookGeometry.AcquisitionVariantNext.Contains(Mouse.X, Mouse.Y)) ? 1 : 0)) : 0);
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
		int count = ((AcquisitionGroup >= 0) ? AcquisitionSources[AcquisitionGroup].Rules.Length : AcquisitionSources.Count);
		int page = ((AcquisitionGroup >= 0) ? AcquisitionRulePage : AcquisitionPage);
		page = BookAcquisitionRules.ClampPage(page + pageStep, count);
		if (Press(Keys.Home))
		{
			page = 0;
		}
		if (Press(Keys.End))
		{
			page = BookAcquisitionRules.PageCount(count) - 1;
		}
		if (AcquisitionGroup >= 0)
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
			if (index < AcquisitionSources.Count && BookGeometry.AcquisitionRow(row).Contains(Mouse.X, Mouse.Y))
			{
				BookAcquisitionGroup source = AcquisitionSources[index];
				if (source.Source.RecipeIndex >= 0)
				{
					OpenAcquisitionRecipe(source.Source.RecipeIndex);
				}
				else if (CanOpenEnemy(source) && !BookGeometry.AcquisitionSourceAction(row).Contains(Mouse.X, Mouse.Y))
				{
					OpenEnemyCard(source);
				}
				else if (source.CanExpand)
				{
					AcquisitionGroup = index;
					AcquisitionRulePage = 0;
				}
				break;
			}
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
			BookAcquisitionKind.Drop => T("Лут", "Drop"), 
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

	private static void AcquisitionBadge(SpriteBatch sb, BookRect rect, string value)
	{
		if (value.Length != 0)
		{
			Box(sb, rect, Surface, border: false, 6f);
			Label(sb, rect, value, TextColor, 0.82f);
		}
	}

	private static string AcquisitionVariantsLabel(int count)
	{
		return T("Варианты: ", "Variants: ") + count;
	}

	private static string AcquisitionRuleReport(BookAcquisitionRule rule)
	{
		string body = string.Join("\n", rule.Reports.Select((BookAcquisitionEntry x) => x.Body).Distinct(StringComparer.Ordinal));
		string variants = string.Join(", ", (from x in rule.Reports
			select x.NpcNetId into x
			where x != 0
			select x).Distinct());
		if (variants.Length > 0)
		{
			body = body + "\n" + T("Варианты NPC (ID): ", "NPC variants (IDs): ") + variants;
		}
		return body;
	}

	private static void DrawAcquisition(SpriteBatch sb)
	{
		if (EnemyOpen)
		{
			DrawEnemyCard(sb);
			return;
		}
		Box(sb, 0f, 0f, 1120f, 696f, new Color(10, 16, 12, 235), border: false, 18f);
		BookRect panel = BookGeometry.AcquisitionPanel;
		Box(sb, panel, Surface, border: true, 12f);
		bool expanded = AcquisitionGroup >= 0;
		BookAcquisitionGroup current = (expanded ? AcquisitionSources[AcquisitionGroup] : null);
		if (expanded)
		{
			Button(sb, BookGeometry.AcquisitionBack, "<");
		}
		Text(sb, expanded ? T("УСЛОВИЯ", "CONDITIONS") : T("КАК ПОЛУЧИТЬ", "HOW TO OBTAIN"), panel.X + (float)(expanded ? 68 : 24), panel.Y + 16f, Accent, 1f, panel.Width - 140f, 24f);
		Button(sb, BookGeometry.AcquisitionClose, "X");
		Item item = ContentSamples.ItemsByType[AcquisitionVariants[AcquisitionVariant]];
		if (expanded)
		{
			DrawAcquisitionIcon(sb, current.Source, new BookRect(panel.X + 24f, panel.Y + 48f, 40f, 40f));
		}
		else
		{
			Icon(sb, item, panel.X + 24f, panel.Y + 48f, 40f, tooltip: false);
		}
		if (expanded && CanOpenEnemy(current))
		{
			Button(sb, BookGeometry.EnemyOpenAction, T("Враг", "Enemy"));
		}
		float nameWidth;
		if (expanded && CanOpenEnemy(current))
		{
			nameWidth = panel.Width - 240f;
		}
		else
		{
			nameWidth = ((!expanded && AcquisitionVariants.Length > 1) ? (panel.Width - 252f) : (panel.Width - 104f));
		}
		Text(sb, expanded ? current.Source.Name : item.Name, panel.X + 80f, panel.Y + 48f, TextColor, 1.08f, nameWidth, 40f);
		if (!expanded && AcquisitionVariants.Length > 1)
		{
			Button(sb, BookGeometry.AcquisitionVariantPrevious, "<");
			Button(sb, BookGeometry.AcquisitionVariantNext, ">");
			Label(sb, BookGeometry.AcquisitionVariantLabel, AcquisitionVariant + 1 + "/" + AcquisitionVariants.Length, Muted, 0.8f);
			if (BookGeometry.AcquisitionVariantLabel.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Варианты группы рецепта", "Recipe-group alternatives"));
			}
		}
		int count = (expanded ? current.Rules.Length : AcquisitionSources.Count);
		int page = (expanded ? AcquisitionRulePage : AcquisitionPage);
		for (int row = 0; row < 6; row++)
		{
			int index = page * 6 + row;
			if (index >= count)
			{
				break;
			}
			if (AcquisitionInfoOpen && row >= 4)
			{
				continue;
			}
			BookAcquisitionGroup group = (expanded ? current : AcquisitionSources[index]);
			BookAcquisitionRule rule = (expanded ? group.Rules[index] : group.Rules[0]);
			BookAcquisitionEntry source = (expanded ? rule.Source : group.Source);
			BookRect bounds = BookGeometry.AcquisitionRow(row);
			bool hover = !AcquisitionInfoOpen && bounds.Contains(Mouse.X, Mouse.Y);
			Box(sb, bounds, hover ? (Raised * 1.12f) : Raised, border: true, 8f);
			DrawAcquisitionIcon(sb, source, BookGeometry.AcquisitionSourceIcon(row));
			BookRect name = BookGeometry.AcquisitionName(row);
			string title;
			if (expanded)
			{
				title = ((source.ConditionLabel.Length == 0) ? T("Без указанных условий", "No reported conditions") : source.ConditionLabel);
			}
			else
			{
				title = source.Name;
			}
			Text(sb, title, name.X, name.Y, (source.RecipeIndex >= 0) ? Accent : TextColor, 0.92f, name.Width, name.Height);
			string subtitle = AcquisitionKindName(source.Kind);
			int variants = (expanded ? rule.VariantCount : group.VariantCount);
			if (variants > 1)
			{
				subtitle = subtitle + " · " + AcquisitionVariantsLabel(variants);
			}
			Text(sb, subtitle, name.X, bounds.Y + 32f, Muted, 0.72f, name.Width, 16f);
			string chance = ((!expanded && group.Rules.Length > 1) ? (group.Rules.Length + T(" усл.", " cond.")) : source.Chance);
			AcquisitionBadge(sb, BookGeometry.AcquisitionChance(row), chance);
			AcquisitionBadge(sb, BookGeometry.AcquisitionAmount(row), expanded ? source.Amount : group.Amount);
			BookRect action = BookGeometry.AcquisitionSourceAction(row);
			if (!expanded && (source.RecipeIndex >= 0 || group.CanExpand))
			{
				Button(sb, action, ">");
			}
			else
			{
				Box(sb, action, Surface, source.Conditional, 6f, source.Conditional ? Accent : Border);
				Label(sb, action, source.Conditional ? "!" : "i", source.Conditional ? Accent : Muted, 0.86f);
			}
			if (hover)
			{
				string body;
				if (expanded)
				{
					body = AcquisitionRuleReport(rule);
				}
				else
				{
					body = ((group.Rules.Length == 1) ? AcquisitionRuleReport(group.Rules[0]) : (group.Rules.Length + T(" отдельных правил. Шансы не складываются.", " separate rules. Chances are not added.")));
				}
				if (!expanded && CanOpenEnemy(group))
				{
					body = body + "\n" + T("ЛКМ — карточка врага; > — условия выпадения.", "Click: enemy card; >: drop conditions.");
				}
				else if (!expanded && group.CanExpand)
				{
					body = body + "\n" + T("Нажмите, чтобы посмотреть условия отдельно.", "Click to view conditions separately.");
				}
				MouseTip(AcquisitionKindName(source.Kind) + " · " + group.Source.Name + "\n" + body);
			}
		}
		Button(sb, BookGeometry.AcquisitionInfo, (AcquisitionReadErrors > 0) ? "!" : "i", AcquisitionInfoOpen);
		Button(sb, BookGeometry.AcquisitionPrevious, "<");
		Button(sb, BookGeometry.AcquisitionNext, ">");
		Label(sb, BookGeometry.AcquisitionPager, page + 1 + "/" + BookAcquisitionRules.PageCount(count), Muted, 0.86f);
		if (BookGeometry.AcquisitionInfo.Contains(Mouse.X, Mouse.Y) && !AcquisitionInfoOpen)
		{
			MouseTip(T("Об источниках и управлении", "About sources and controls"));
		}
		if (AcquisitionInfoOpen)
		{
			BookRect info = BookGeometry.AcquisitionInfoPanel;
			Box(sb, info, Panel, border: true, 8f);
			Text(sb, T("Источники объединены. Шансы по условиям не складываются.", "Sources are grouped. Conditional chances are never added."), info.X + 16f, info.Y + 12f, TextColor, 0.8f, info.Width - 32f, 24f);
			Text(sb, (AcquisitionReadErrors > 0) ? T("Часть отчётов мода не прочитана; список может быть неполным.", "Some mod reports could not be read; the list may be incomplete.") : T("Добыча, сундуки мира, рыбалка и особые механики могут отсутствовать.", "Mining, world chests, fishing and custom mechanics may be missing."), info.X + 16f, info.Y + 44f, Muted, 0.76f, info.Width - 32f, 24f);
			Text(sb, T("Колесо / PgUp / PgDn — страницы. Esc — назад.", "Wheel / PgUp / PgDn: pages. Esc: back."), info.X + 16f, info.Y + 76f, Muted, 0.76f, info.Width - 32f, 24f);
		}
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
			Icon(sb, part, rect.X + (rect.Width - 28f) / 2f, rect.Y + 6f, 28f, tooltip: false);
			DefenseBadge(sb, rect.X + rect.Width / 2f, rect.Y + 36f, part.defense, active ? Accent : Muted);
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
			MouseTip((hovered == 3) ? T("Сколько хватит материалов, до 100 за раз", "As many as materials allow, up to 100") : T("Создать столько раз за нажатие", "Craft this many times per click"));
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
			label = T("Не отслеживать", "Untrack");
		}
		else
		{
			label = (full ? T("Лимит: 5/5", "Limit: 5/5") : T("Отслеживать", "Track"));
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
		EnemyOpen = (EnemyInfoOpen = false);
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
				AcquisitionInfoOpen = false;
				ReadEnemyCard();
			}
		}
	}

	private static void ReadEnemyCard()
	{
		EnemyPage = (EnemyReadErrors = 0);
		EnemyInfoOpen = false;
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
							conditions.Add(string.IsNullOrWhiteSpace(description) ? T("Особое условие источника", "Special source condition") : description);
						}
						catch (Exception)
						{
							conditions.Add(T("Описание условия недоступно", "Condition description unavailable"));
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

	private static void HandleEnemyInput(bool click, KeyboardState keys)
	{
		int scrollWheelValue = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
		int delta = scrollWheelValue - LastWheel;
		LastWheel = scrollWheelValue;
		if (Press(Keys.Escape) && EnemyInfoOpen)
		{
			EnemyInfoOpen = false;
			return;
		}
		if (Press(Keys.Escape) || (click && (BookGeometry.AcquisitionBack.Contains(Mouse.X, Mouse.Y) || BookGeometry.AcquisitionClose.Contains(Mouse.X, Mouse.Y) || !BookGeometry.AcquisitionPanel.Contains(Mouse.X, Mouse.Y))))
		{
			CloseEnemyCard();
			return;
		}
		if (click && BookGeometry.AcquisitionInfo.Contains(Mouse.X, Mouse.Y))
		{
			EnemyInfoOpen = !EnemyInfoOpen;
			return;
		}
		if (EnemyInfoOpen)
		{
			if (click)
			{
				EnemyInfoOpen = false;
			}
			return;
		}
		int step = ((click && BookGeometry.AcquisitionVariantPrevious.Contains(Mouse.X, Mouse.Y)) ? (-1) : ((click && BookGeometry.AcquisitionVariantNext.Contains(Mouse.X, Mouse.Y)) ? 1 : 0));
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
		return string.Join("\n", loot.Rules.Select((BookEnemyDrop x) => BookAcquisitionRules.Chance(x.Chance) + " · ×" + BookAcquisitionRules.Stack(x.Min, x.Max) + " · " + ((x.Conditions.Length > 0) ? x.Conditions : T("Без указанных условий", "No reported conditions"))));
	}

	private static void DrawEnemyCard(SpriteBatch sb)
	{
		Box(sb, 0f, 0f, 1120f, 696f, new Color(10, 16, 12, 235), border: false, 18f);
		BookRect panel = BookGeometry.AcquisitionPanel;
		Box(sb, panel, Surface, border: true, 12f);
		NPC npc = ContentSamples.NpcsByNetId[EnemyVariants[EnemyVariant]];
		Button(sb, BookGeometry.AcquisitionBack, "<");
		Button(sb, BookGeometry.AcquisitionClose, "X");
		Text(sb, npc.friendly ? T("СУЩЕСТВО", "CREATURE") : T("ВРАГ", "ENEMY"), panel.X + 68f, panel.Y + 16f, Accent, 1f, panel.Width - 140f, 24f);
		DrawAcquisitionIcon(sb, new BookAcquisitionEntry(npc.FullName, "", BookAcquisitionKind.Drop, "", "", 0, EnemyVariants[EnemyVariant]), BookGeometry.EnemyPortrait);
		Text(sb, npc.FullName, panel.X + 104f, panel.Y + 52f, TextColor, 1.08f, (EnemyVariants.Length > 1) ? (panel.Width - 276f) : (panel.Width - 128f), 32f);
		Text(sb, ModName(npc.ModNPC?.Mod.Name ?? "Terraria"), panel.X + 104f, panel.Y + 88f, Muted, 0.8f, panel.Width - 128f, 24f);
		if (EnemyVariants.Length > 1)
		{
			Button(sb, BookGeometry.AcquisitionVariantPrevious, "<");
			Button(sb, BookGeometry.AcquisitionVariantNext, ">");
			Label(sb, BookGeometry.AcquisitionVariantLabel, EnemyVariant + 1 + "/" + EnemyVariants.Length, Muted, 0.8f);
		}
		if (BookGeometry.EnemyPortrait.Contains(Mouse.X, Mouse.Y) || BookGeometry.AcquisitionVariantLabel.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(npc.FullName + "\nNPC ID: " + EnemyVariants[EnemyVariant]);
		}
		BookRect tags = BookGeometry.EnemyTags;
		Box(sb, tags, Raised, border: false, 8f);
		Text(sb, T("Биом", "Biome"), tags.X + 12f, tags.Y + 4f, Muted, 0.76f, 92f, 24f);
		Text(sb, (EnemyBiomes.Length > 0) ? EnemyBiomes : T("Нет меток бестиария", "No bestiary tags"), tags.X + 112f, tags.Y + 4f, TextColor, 0.82f, tags.Width - 124f, 24f);
		Text(sb, T("Появление", "Spawning"), tags.X + 12f, tags.Y + 32f, Muted, 0.76f, 92f, 24f);
		Text(sb, (EnemySpawn.Length > 0) ? EnemySpawn : T("Условия не указаны", "Conditions not reported"), tags.X + 112f, tags.Y + 32f, Muted, 0.82f, tags.Width - 124f, 24f);
		if (tags.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Метки бестиария, не полный список условий спавна.", "Bestiary tags, not a complete list of spawning requirements.") + "\n" + ((EnemyBiomes.Length > 0) ? EnemyBiomes : T("Биом не указан", "Biome not reported")) + "\n" + ((EnemySpawn.Length > 0) ? EnemySpawn : T("Время и события не указаны", "Time and events not reported")));
		}
		Text(sb, T("ДОБЫЧА", "LOOT") + " · " + EnemyLoot.Count, panel.X + 24f, panel.Y + 196f, Muted, 0.82f, panel.Width - 48f, 20f);
		if (EnemyLoot.Count == 0)
		{
			Text(sb, T("Нет зарегистрированных отчётов добычи", "No registered loot reports"), panel.X + 24f, panel.Y + 224f, Muted, 0.9f, panel.Width - 48f, 40f);
		}
		for (int row = 0; row < 5; row++)
		{
			int index = EnemyPage * 5 + row;
			if (index >= EnemyLoot.Count)
			{
				break;
			}
			BookRect rect = BookGeometry.EnemyLootRow(row);
			if (!EnemyInfoOpen || !(rect.Y + rect.Height > BookGeometry.AcquisitionInfoPanel.Y))
			{
				BookEnemyLoot loot = EnemyLoot[index];
				Item item = ContentSamples.ItemsByType[loot.ItemType];
				Box(sb, rect, Raised, border: true, 8f);
				Icon(sb, item, rect.X + 8f, rect.Y + 4f, 32f, tooltip: false);
				BookRect chance = BookGeometry.EnemyLootChance(row);
				BookRect amount = BookGeometry.EnemyLootAmount(row);
				BookRect info = BookGeometry.EnemyLootInfo(row);
				Text(sb, item.Name, rect.X + 48f, rect.Y + 4f, TextColor, 0.88f, chance.X - rect.X - 60f, 32f);
				AcquisitionBadge(sb, chance, (loot.Rules.Length > 1) ? (loot.Rules.Length + T(" усл.", " cond.")) : BookAcquisitionRules.Chance(loot.Rules[0].Chance));
				AcquisitionBadge(sb, amount, loot.Amount);
				bool conditional = loot.Rules.Length > 1 || loot.Rules.Any((BookEnemyDrop x) => x.Conditions.Length > 0);
				Box(sb, info, Surface, conditional, 6f, conditional ? Accent : Border);
				Label(sb, info, conditional ? "!" : "i", conditional ? Accent : Muted, 0.86f);
				if (!EnemyInfoOpen && rect.Contains(Mouse.X, Mouse.Y))
				{
					MouseTip(item.Name + "\n" + EnemyLootTooltip(loot));
				}
			}
		}
		Button(sb, BookGeometry.AcquisitionInfo, (EnemyReadErrors > 0) ? "!" : "i", EnemyInfoOpen);
		Button(sb, BookGeometry.AcquisitionPrevious, "<");
		Button(sb, BookGeometry.AcquisitionNext, ">");
		Label(sb, BookGeometry.AcquisitionPager, EnemyPage + 1 + "/" + BookEnemyRules.PageCount(EnemyLoot.Count), Muted, 0.86f);
		if (BookGeometry.AcquisitionInfo.Contains(Mouse.X, Mouse.Y) && !EnemyInfoOpen)
		{
			MouseTip(T("О данных врага", "About enemy data"));
		}
		if (EnemyInfoOpen)
		{
			BookRect info2 = BookGeometry.AcquisitionInfoPanel;
			Box(sb, info2, Panel, border: true, 8f);
			Text(sb, T("Метки бестиария не описывают все условия появления.", "Bestiary tags do not describe all spawning requirements."), info2.X + 16f, info2.Y + 12f, TextColor, 0.8f, info2.Width - 32f, 24f);
			Text(sb, T("Шансы базовые. Условия добычи — при наведении; проценты не складываются.", "Base chances. Hover loot for conditions; percentages are never added."), info2.X + 16f, info2.Y + 44f, Muted, 0.76f, info2.Width - 32f, 24f);
			Text(sb, (EnemyReadErrors > 0) ? T("Часть данных не прочитана. Esc — назад.", "Some data could not be read. Esc: back.") : T("Монеты и особая логика модов могут отсутствовать. Esc — назад.", "Coins and custom mod logic may be missing. Esc: back."), info2.X + 16f, info2.Y + 76f, Muted, 0.76f, info2.Width - 32f, 24f);
		}
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
		BookSkin.Panel(Main.spriteBatch, new Rectangle(x - 8, y - 6, (int)size.X + 16, (int)size.Y + 8), Panel * 0.96f, 8f, Border);
		Main.instance.MouseText(wrapped, 0, 0);
	}

	private static void DrawStageFilter(SpriteBatch sb)
	{
		BookRect[] rects = StageRects();
		Segmented(sb, rects, StageLabels(), (int)StageFilter, 0.85f);
		if (!MenuOpen && SegmentAt(rects) >= 0)
		{
			MouseTip(T("Самый ранний этап, когда предмет можно получить", "The earliest stage you can get an item") + "\n" + T("Учитывается сложность мира", "The world's difficulty counts"));
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

	private static BookRect[] StageRects()
	{
		return SegmentRects(BookGeometry.StageFilter, StageLabels(), 0.85f, natural: true);
	}

	private static string StageExplanation(Item item, Recipe recipe)
	{
		return ((recipe != null) ? T("Этап рецепта: по материалам, станции и условиям", "Recipe stage: from its materials, station and conditions") : T("Самый ранний этап, когда предмет можно получить", "The earliest stage you can get this item")) + "\n" + T("Учитывается сложность мира", "The world's difficulty counts");
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
		if (StorageView && !StorageTabVisible)
		{
			StorageView = false;
			Catalogue = true;
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

	// The window is dragged by its frame (an 8 px band on every side, free of controls) or by any empty
	// part of the header; tabs and the close button keep their clicks.
	private static bool HeaderGrab()
	{
		if (!BookGeometry.Frame.Contains(Mouse.X, Mouse.Y))
		{
			return false;
		}
		if (Mouse.X < BookGeometry.FrameGrip || Mouse.X >= BookGeometry.Frame.Width - BookGeometry.FrameGrip || Mouse.Y < BookGeometry.FrameGrip || Mouse.Y >= BookGeometry.Frame.Height - BookGeometry.FrameGrip)
		{
			return true;
		}
		if (!BookGeometry.Header.Contains(Mouse.X, Mouse.Y) || BookGeometry.HeaderClose.Contains(Mouse.X, Mouse.Y))
		{
			return false;
		}
		return !VisibleTabs().Any((int tab) => TabRect(tab).Contains(Mouse.X, Mouse.Y));
	}

	// Grab zones light the frame in Accent while hovered or dragged.
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
		return SortOptionLabel(SortOptionIndex);
	}

	// One list for field and direction: Name, ID and Mod, each ascending then descending.
	private static int SortOptionIndex => (int)SortMode * 2 + (SortDescending ? 1 : 0);

	private static string SortOptionLabel(int option)
	{
		return option switch
		{
			0 => T("Имя А-Я", "Name A-Z"),
			1 => T("Имя Я-А", "Name Z-A"),
			2 => "ID 1-9",
			3 => "ID 9-1",
			4 => T("Мод А-Я", "Mod A-Z"),
			_ => T("Мод Я-А", "Mod Z-A"),
		};
	}

	private static void ApplySortOption(int option)
	{
		SortMode = (BookSortMode)Math.Clamp(option / 2, 0, 2);
		SortDescending = option % 2 == 1;
		Page = 0;
		ApplyFilters();
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
			ModMenu = (HelpOpen = false);
			CloseAcquisition();
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
			// Nameless internal items (e.g. ManaCloakStar, an effect sprite) are not catalogue entries.
			if (item != null && !item.IsAir && item.type > 0 && !string.IsNullOrWhiteSpace(item.Name))
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
		return BookStorage.Items[index];
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
			else if (SortMenu)
			{
				SortMenu = false;
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
			else if (!SortMenu && BookGeometry.CategoryPanel.Contains(Mouse.X, Mouse.Y))
			{
				CategoryRow = Math.Clamp(CategoryRow + step * 3, 0, MaxCategoryRow);
			}
			else if (!SortMenu && BookGeometry.DetailsPanel.Contains(Mouse.X, Mouse.Y))
			{
				DetailsPage = Math.Clamp(DetailsPage + step, 0, MaxDetailsPage());
			}
			else if (!SortMenu && Hit(196f, 76f, 600f, 564f))
			{
				Page = Math.Clamp(Page + step, 0, Pages - 1);
			}
		}
		BookRect sampleSlot = BookGeometry.SampleSlot;
		if (rightClick && StorageView && !ModMenu)
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
					if (BookStorageGesture.Read(StorageView, hovered: true, ModMenu, leftPressed: false, rightClick, shift: false) == BookStorageGesture.Transfer.One)
					{
						BookStorage.Withdraw(Result(Filtered[index]), single: true);
					}
					return;
				}
			}
		}
		if (rightClick && Chains && !MenuOpen && sampleSlot.Contains(Mouse.X, Mouse.Y))
		{
			ClearSample();
			return;
		}
		if (!click)
		{
			return;
		}
		if (SortMenu)
		{
			for (int option = 0; option < 6; option++)
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
			BookStorage.CancelCraft();
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
		int stage = (TrackingView || ModMenu) ? (-1) : SegmentAt(StageRects());
		if (stage >= 0)
		{
			StageFilter = (BookStageFilter)stage;
			Page = (DetailsPage = (Variant = 0));
			ApplyFilters();
			if (Chains)
			{
				ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"Chain stage audit: sample={SampleType}, stage={StageFilter}, visible={Filtered.Count}, uncertain={ChainUnknownVisibleCount}, excludedByStage={ChainStageExcludedCount}, craftableOnly={ChainCraftableOnly}");
			}
			return;
		}
		int source = StorageTabVisible ? SegmentAt(CraftSourceRects()) : (-1);
		if (source >= 0)
		{
			if ((source == 1) != BookStorage.UseForCrafting)
			{
				BookStorage.ChooseCraftSource(source == 1);
				BookCrafting.Refresh(force: true);
				if (Chains && ChainCraftableOnly)
				{
					ApplyFilters(clearMessage: false);
				}
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
		if (Chains)
		{
			if (SampleType > 0 && BookGeometry.SampleClear.Contains(Mouse.X, Mouse.Y))
			{
				ClearSample();
				return;
			}
			if (sampleSlot.Contains(Mouse.X, Mouse.Y))
			{
				SetTyping(value: false);
				if (!Main.mouseItem.IsAir)
				{
					SetSample(Main.mouseItem.type);
				}
				else
				{
					Main.playerInventory = true;
				}
				return;
			}
			int filter = SegmentAt(ChainFilterRects());
			if (filter >= 0)
			{
				ChainCraftableOnly = filter == 1;
				if (ChainCraftableOnly)
				{
					BookCrafting.Refresh(force: true);
				}
				Page = (DetailsPage = 0);
				ApplyFilters();
				return;
			}
			int first = Math.Max(0, ChainHistory.Count - 4);
			for (int visible = 0; ChainHistory.Count > 1 && visible < Math.Min(4, ChainHistory.Count); visible++)
			{
				if (BookGeometry.ChainCrumb(visible).Contains(Mouse.X, Mouse.Y))
				{
					GoToChain(first + visible);
					return;
				}
			}
		}
		if (RecipeSampleFilter && SampleChipRect().Contains(Mouse.X, Mouse.Y))
		{
			ClearSample();
			return;
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
		if (BookGeometry.Sort.Contains(Mouse.X, Mouse.Y))
		{
			SortMenu = !RecentView;
			return;
		}
		if (BookGeometry.FavoritesFilter.Contains(Mouse.X, Mouse.Y))
		{
			FavoritesOnly = !FavoritesOnly;
			Page = 0;
			ApplyFilters();
			return;
		}
		for (int visible = 0; visible < BookGeometry.VisibleCategoryRows; visible++)
		{
			int index2 = CategoryRow + visible;
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
		for (int n3 = 0; n3 < ActivePageSize; n3++)
		{
			int index3 = Page * ActivePageSize + n3;
			if (index3 >= Filtered.Count)
			{
				break;
			}
			if ((Chains ? BookGeometry.ChainCell(n3) : BookGeometry.GridCell(n3, GridTop)).Contains(Mouse.X, Mouse.Y))
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
		if (BookGeometry.PagePrevious.Contains(Mouse.X, Mouse.Y))
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
		Item chosen = SelectedItem;
		Recipe materialRecipe = SelectedRecipe;
		float top = RecipeBlockTop(chosen);
		if (materialRecipe == null)
		{
			if (chosen != null && !chosen.IsAir && BookGeometry.SelectedItemObtain(top).Contains(Mouse.X, Mouse.Y))
			{
				OpenAcquisition(chosen.type, null);
				return;
			}
		}
		else
		{
			if ((Catalogue || ArmorFocus > 0) && VariantCount > 1)
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
						SetSample(material.type);
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
		if (materialRecipe != null && BookGeometry.CraftAction.Contains(Mouse.X, Mouse.Y))
		{
			BookCrafting.TryCraft(materialRecipe, Ru, PreviewBatches(materialRecipe));
			if (Chains && ChainCraftableOnly)
			{
				ApplyFilters(clearMessage: false);
			}
		}
	}

	private static float GridTop => StorageView ? BookGeometry.StorageGridTop : BookGeometry.GridTop;

	// Clearing the chain item only drops the sample; search and the other filters stay as they are.
	private static void ClearSample()
	{
		SampleType = 0;
		ChainHistory.Clear();
		ArmorFocus = 0;
		Selected = -1;
		Page = (DetailsPage = (Variant = 0));
		RefreshSampleRecipes();
		ApplyFilters();
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
		BookHotkeys.Action action = BookHotkeys.Read(keys, LastKeys, Typing, Main.blockInput, Main.drawingPlayerChat || Main.editSign || Main.editChest, HelpOpen, MenuOpen, StorageView, Chains);
		switch (action)
		{
		case BookHotkeys.Action.None:
			return false;
		case BookHotkeys.Action.Help:
			SetTyping(value: false);
			ModMenu = (SortMenu = false);
			HelpOpen = !HelpOpen;
			break;
		case BookHotkeys.Action.Search:
			if (!TrackingView)
			{
				ModMenu = (SortMenu = false);
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
			if (StorageTabVisible)
			{
				SwitchTab(3);
			}
			break;
		case BookHotkeys.Action.Recent:
			SwitchTab(4);
			break;
		case BookHotkeys.Action.Tracking:
			SwitchTab(5);
			break;
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
		List<List<DetailSlot>> pages = Place(BookGeometry.DetailListBottom);
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
			float listTop = top + 80f;
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
				float need = (header ? (((y > listTop) ? 8f : 0f) + 22f) : 0f) + 32f;
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
				result[^1].Add(new DetailSlot(new BookRect(812f, y, 284f, 32f), i, null));
				y += 36f;
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

	private static bool MenuOpen => ModMenu || SortMenu;

	private static bool StorageTabVisible => BookStorage.Installed;

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
	private static void Dropdown(SpriteBatch sb, BookRect rect, string value, bool open, bool enabled = true)
	{
		bool hover = enabled && !open && !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
		Box(sb, rect, hover ? HoverFill : (enabled ? Raised : Surface), border: true, 8f, open ? new Color?(Accent) : null, (!open) ? 1 : 2);
		CenterLabel(sb, value, rect.X + 12f, rect.Y + 4f, rect.Width - 40f, rect.Height - 8f, enabled ? TextColor : Muted, 0.85f, rightAligned: false, leftAligned: true);
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
		string have = (assessment.Unknown ? "?" : (assessment.Infinite ? "∞" : Count(assessment.Available)));
		return T("Есть ", "Have ") + have + T(", нужно ", ", need ") + Count(assessment.Required) + ((batches > 1) ? (" (×" + batches + ")") : "") + (assessment.Overlapping ? T(" · учтено и в другой строке", " · also counted in another row") : "");
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
			list.Add(new Detail(requirement.Ingredient, requirement.Name, MaterialSub(assessment), color, color, MaterialTooltip(requirement, assessment, batches), requirement.ItemTypes.OrderBy((int x) => x).ToArray()));
		}
		foreach (int tile in r.requiredTile.Where((int t) => t >= 0))
		{
			StationItems.TryGetValue(tile, out var station);
			string name = (StationNames.TryGetValue(tile, out var value) ? value : (TileLoader.GetTile(tile)?.Name ?? ("Tile " + tile)));
			list.Add(new Detail(station, name, T("Станция", "Station"), Muted, Muted, ""));
		}
		if (!r.requiredTile.Any((int t) => t >= 0))
		{
			list.Add(new Detail(null, T("Вручную", "By hand"), T("Станция не нужна", "No station needed"), Muted, Muted, ""));
		}
		foreach (Condition c in r.Conditions)
		{
			list.Add(new Detail(null, c.Description.Value, T("Условие", "Condition"), Caution, Muted, ""));
		}
		return list;
	}

	// The chain item slot sits in the top-left corner of the Chains content: the slot and one line of text.
	private static void DrawSample(SpriteBatch sb)
	{
		BookRect slot = BookGeometry.SampleSlot;
		bool hasSample = SampleType > 0 && ContentSamples.ItemsByType.ContainsKey(SampleType);
		bool hover = !MenuOpen && slot.Contains(Mouse.X, Mouse.Y);
		Box(sb, slot, hover ? HoverFill : new Color(36, 51, 41), border: true, 10f, Accent, (!hasSample) ? 1 : 2);
		float labelWidth = ChainFilterRects()[0].X - 12f - (slot.X + slot.Width + 12f);
		if (hasSample)
		{
			Item sample = ContentSamples.ItemsByType[SampleType];
			Icon(sb, sample, slot.X + 8f, slot.Y + 8f, 32f, tooltip: false);
			CenterLabel(sb, sample.Name, slot.X + slot.Width + 12f, slot.Y + 8f, labelWidth, 32f, TextColor, 1f, rightAligned: false, leftAligned: true);
			BookRect clear = BookGeometry.SampleClear;
			bool clearHover = !MenuOpen && clear.Contains(Mouse.X, Mouse.Y);
			Box(sb, clear, clearHover ? Bad : Raised, border: true, 9f);
			Cross(sb, clear.X + clear.Width / 2f, clear.Y + clear.Height / 2f, 3f, clearHover ? Panel : TextColor);
			if (clearHover)
			{
				MouseTip(T("Убрать предмет", "Remove the item"));
			}
			else if (hover)
			{
				MouseTip(sample.Name + "\n" + T("Клик предметом с курсора заменяет · ПКМ: убрать", "Click with an item to replace · Right-click removes"));
			}
			return;
		}
		Plus(sb, slot.X + slot.Width / 2f, slot.Y + slot.Height / 2f, 7f, Accent);
		CenterLabel(sb, T("Положите предмет сюда", "Place an item here"), slot.X + slot.Width + 12f, slot.Y + 8f, labelWidth, 32f, Muted, 0.9f, rightAligned: false, leftAligned: true);
		if (hover)
		{
			MouseTip(T("Нажмите предметом с курсора", "Click with an item held on the cursor") + "\n" + T("Пустой клик открывает инвентарь", "An empty click opens the inventory"));
		}
	}

	private static string[] ChainFilterLabels()
	{
		return new string[2]
		{
			T("Все", "All"),
			T("Можно создать", "Craftable")
		};
	}

	private static BookRect[] ChainFilterRects()
	{
		return SegmentRects(new BookRect(212f, 172f, 568f, 32f), ChainFilterLabels(), 0.85f, natural: true, alignRight: true);
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
			color = Bad;
		}
		else
		{
			color = (assessed.Any(((MaterialRequirement Requirement, BookMaterialRules.Assessment Assessment) x) => x.Assessment.Unknown || x.Assessment.Overlapping) ? Muted : Good);
		}
		string materials = string.Join(", ", assessed.Take(3).Select(((MaterialRequirement, BookMaterialRules.Assessment) x) => x.Item1.Name + " " + MaterialSub(x.Item2))) + ((assessed.Count > 3) ? ", ..." : "");
		string tooltip = output.Name + ((output.stack > 1) ? (" ×" + output.stack) : "") + "\n" + ((assessed.Count == 0) ? T("Без материалов", "No materials") : materials);
		return new ChainMaterialPreview(summary, color, tooltip);
	}

	private static void DrawChains(SpriteBatch sb)
	{
		DrawSample(sb);
		BookRect[] filters = ChainFilterRects();
		Segmented(sb, filters, ChainFilterLabels(), ChainCraftableOnly ? 1 : 0, 0.85f);
		int filterHover = MenuOpen ? (-1) : SegmentAt(filters);
		if (filterHover >= 0)
		{
			MouseTip((filterHover == 0) ? T("Все рецепты с этим предметом", "Every recipe that uses this item") : T("Только то, что можно создать сейчас", "Only what you can craft right now"));
		}
		if (SampleType <= 0)
		{
			EmptyState(sb, new BookRect(212f, 220f, 568f, 368f), T("Начните с предмета", "Start with an item"), T("Положите его в ячейку или Shift+клик в «Предметах»", "Drop it in the slot, or Shift+Click one in Items"));
			return;
		}
		int first = Math.Max(0, ChainHistory.Count - 4);
		int crumbs = Math.Min(4, ChainHistory.Count);
		for (int visible = 0; ChainHistory.Count > 1 && visible < crumbs; visible++)
		{
			int index = first + visible;
			Item item = ContentSamples.ItemsByType[ChainHistory[index]];
			BookRect crumb = BookGeometry.ChainCrumb(visible);
			bool current = index == ChainHistory.Count - 1;
			bool hover = !current && !MenuOpen && crumb.Contains(Mouse.X, Mouse.Y);
			Box(sb, crumb, current ? SelectedFill : (hover ? HoverFill : Raised), border: true, 8f, current ? new Color?(Accent) : null);
			Icon(sb, item, crumb.X + 8f, crumb.Y + 6f, 20f, tooltip: false);
			CenterLabel(sb, ((visible == 0 && first > 0) ? "... " : "") + item.Name, crumb.X + 34f, crumb.Y + 4f, crumb.Width - 42f, crumb.Height - 8f, current ? Accent : TextColor, 0.8f, rightAligned: false, leftAligned: true);
			if (hover)
			{
				MouseTip(item.Name + "\n" + T("Вернуться к этому шагу", "Back to this step"));
			}
			if (visible < crumbs - 1)
			{
				Chevron(sb, crumb.X + crumb.Width + 4f, crumb.Y + crumb.Height / 2f, 2.5f, 3, Muted);
			}
		}
		SectionLabel(sb, T("Используется в", "Used in"), 212f, 264f, 400f);
		if (Filtered.Count == 0)
		{
			bool terminal = SampleRecipes.Count == 0;
			bool noCraftable = !terminal && ChainCraftableOnly && ChainEligibleCount > 0;
			bool noStage = !terminal && !noCraftable && ChainStageExcludedCount > 0;
			string title;
			string hint;
			if (terminal)
			{
				title = T("Конец ветки", "End of this branch");
				hint = T("Этот предмет не используется в рецептах", "No recipe uses this item");
			}
			else if (noCraftable)
			{
				title = T("Сейчас нечего создать", "Nothing to craft right now");
				hint = T("«Все» покажет и недоступные рецепты", "All also shows recipes you can't craft yet");
			}
			else if (noStage)
			{
				title = T("Нет рецептов этого этапа", "No recipes for this stage");
				hint = T("Выберите «Все этапы», чтобы увидеть остальные", "Choose All stages to see the rest");
			}
			else
			{
				title = T("Скрыто фильтрами", "Hidden by filters");
				hint = T("Ctrl+Backspace сбрасывает фильтры", "Ctrl+Backspace resets filters");
			}
			EmptyState(sb, new BookRect(212f, 288f, 568f, 300f), title, hint);
			return;
		}
		int onPage = Math.Min(12, Filtered.Count - Page * 12);
		for (int n = 0; n < onPage; n++)
		{
			int index2 = Filtered[Page * 12 + n];
			Item item2 = Result(index2);
			Recipe recipe = Main.recipe[index2];
			ChainMaterialPreview materialPreview = PreviewChainMaterials(recipe, item2);
			bool uncertain = BookProgression.RecipeStage(index2) == BookStage.Unknown;
			BookRect cell = BookGeometry.ChainCell(n);
			BookRect drill = BookGeometry.ChainDrill(n);
			bool hover = !MenuOpen && cell.Contains(Mouse.X, Mouse.Y);
			bool active = Selected == index2;
			Box(sb, cell, active ? SelectedFill : (hover ? HoverFill : Raised), border: true, 8f, active ? new Color?(Accent) : null, (!active) ? 1 : 2);
			Icon(sb, item2, cell.X + 8f, cell.Y + 6f, 32f, tooltip: false);
			if (uncertain)
			{
				CenterLabel(sb, "?", cell.X + 30f, cell.Y + 24f, 14f, 16f, Caution, 0.8f);
			}
			float textWidth = drill.X - 8f - (cell.X + 48f);
			CenterLabel(sb, item2.Name + ((item2.stack > 1) ? (" ×" + item2.stack) : ""), cell.X + 48f, cell.Y + 4f, textWidth, 18f, TextColor, 0.85f, rightAligned: false, leftAligned: true);
			CenterLabel(sb, materialPreview.Summary, cell.X + 48f, cell.Y + 24f, textWidth, 16f, materialPreview.Color, 0.8f, rightAligned: false, leftAligned: true);
			IconButtonFrame(sb, drill);
			Chevron(sb, drill.X + drill.Width / 2f + 1f, drill.Y + drill.Height / 2f, 4f, 3, TextColor);
			if (!MenuOpen && drill.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(T("Следующий шаг: ", "Next step: ") + item2.Name);
			}
			else if (hover)
			{
				Main.HoverItem = new Item();
				Main.hoverItemName = "";
				MouseTip(materialPreview.Tooltip + (uncertain ? (" · " + T("этап не определён", "stage uncertain")) : ""));
			}
		}
	}

	private static void DrawStorageToolbar(SpriteBatch sb)
	{
		BookRect panel = BookGeometry.SamplePanel;
		Box(sb, panel.X, panel.Y, panel.Width, panel.Height, Surface, border: true, 10f);
		Button(sb, BookGeometry.StorageQuickStack, T("Быстрый стак", "Quick stack"), BookStorage.Connected);
		Button(sb, BookGeometry.StorageDeposit, T("Положить с курсора", "Deposit cursor"), BookStorage.Connected && !Main.mouseItem.IsAir);
		Button(sb, BookGeometry.StorageRestock, T("Пополнить", "Restock"), BookStorage.Connected);
		if (BookGeometry.StorageQuickStack.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Ctrl+Q · Сложить из инвентаря только предметы, уже лежащие в сети. Панель быстрого доступа и избранное не трогаются.", "Ctrl+Q · Deposit matching inventory items already present in storage. Hotbar and favorites are kept."));
		}
		if (BookGeometry.StorageRestock.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Ctrl+R · Пополнить неполные стопки в инвентаре из сети. Используется штатное действие Magic Storage.", "Ctrl+R · Refill partial inventory stacks from storage using the native Magic Storage action."));
		}
		if (BookGeometry.StorageDeposit.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Ctrl+D · Передать реальный предмет с курсора в подключённую сеть. Непоместившийся остаток остаётся на курсоре.", "Ctrl+D · Deposit the actual held item into the connected network. Any remainder stays on the cursor."));
		}
	}

	private static void DrawStorageDetails(SpriteBatch sb)
	{
		Box(sb, BookGeometry.DetailsPanel, Surface, border: false, 12f);
		Text(sb, T("MAGIC STORAGE", "MAGIC STORAGE"), 812f, 108f, Muted, 0.82f, 264f, 24f);
		SpriteBatch sb2 = sb;
		Color color;
		if (BookStorage.Connected)
		{
			color = (BookStorage.Updating ? new Color(219, 184, 112) : Accent);
		}
		else
		{
			color = Muted;
		}
		Box(sb2, 1084f, 116f, 8f, 8f, color, border: false, 4f);
		if (Hit(1076f, 102f, 24f, 30f))
		{
			MouseTip(BookStorage.ConnectionDetails);
		}
		Item selected = SelectedItem;
		if (selected != null)
		{
			Icon(sb, selected, 926f, 136f, 56f);
			Text(sb, selected.Name, 812f, 200f, TextColor, 1.1f, 284f, 32f);
			Text(sb, T("В этой стопке: ", "In this stack: ") + selected.stack, 812f, 240f, Muted, 0.95f, 284f, 28f);
			Text(sb, ModName(selected.ModItem?.Mod.Name ?? "Terraria"), 812f, 276f, Muted, 0.84f, 284f, 24f);
		}
		else
		{
			Text(sb, T("Выберите предмет слева", "Select a stored item"), 812f, 185f, Muted, 1f, 284f, 30f);
		}
		Text(sb, T("Shift+ЛКМ — стопка в инвентарь", "Shift+left-click: stack to inventory"), 812f, 324f, Muted, 0.84f, 284f, 28f);
		Text(sb, T("ПКМ — 1 предмет в инвентарь", "Right-click: 1 item to inventory"), 812f, 360f, Muted, 0.84f, 284f, 28f);
		Text(sb, T("Alt+ЛКМ — избранное", "Alt+left-click: favorite"), 812f, 396f, Muted, 0.84f, 284f, 28f);
		Button(sb, BookGeometry.StorageDepositAll, T("Сложить всё", "Deposit all"), BookStorage.Connected);
		Button(sb, BookGeometry.StorageConnect, T("Подключить", "Reconnect"), BookStorage.Connected);
		Button(sb, BookGeometry.StorageNative, T("Окно MS", "MS window"));
		Button(sb, BookGeometry.StorageDisconnect, T("Отключить сеть", "Disconnect network"), BookStorage.Connected);
		Text(sb, BookStorage.ConnectionStatus, 812f, 576f, BookStorage.Connected ? Accent : Muted, 0.76f, 284f, 16f);
		if (BookStorage.Connected)
		{
			Text(sb, BookStorage.AccessName, 812f, 596f, Muted, 0.76f, 284f, 16f);
			Text(sb, BookStorage.AccessCoordinates, 812f, 616f, Muted, 0.72f, 284f, 16f);
		}
		if (Hit(812f, 576f, 284f, 64f))
		{
			MouseTip(BookStorage.ConnectionDetails);
		}
		if (BookGeometry.StorageDepositAll.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Ctrl+Shift+D · Сложить весь обычный инвентарь. Панель быстрого доступа (слоты 1–10) и избранные предметы сохраняются. Курсор должен быть пустым.", "Ctrl+Shift+D · Deposit the main inventory. Hotbar (slots 1–10) and favorited items are kept. Clear the cursor first."));
		}
		if (BookGeometry.StorageConnect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Подключение рядом выполняется автоматически. Эта кнопка — повторная попытка; защищённые сети требуют штатного доступа.", "Nearby connection is automatic. Retry here; protected networks still need native authorization."));
		}
		if (BookGeometry.StorageNative.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Открыть родное окно Magic Storage: станции, модули, безопасность и особые режимы. Книга закроется.", "Return to native Magic Storage for stations, modules, security and special modes. Closes the book."));
		}
		if (BookGeometry.StorageDisconnect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Отключиться до следующего открытия книги. Автоподключение в этот раз не будет повторяться.", "Disconnect until the book is opened again. Automatic reconnect is suspended for this opening."));
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
		bool grabHint = !AcquisitionOpen && GrabHint;
		Box(sb, 0f, 0f, 1120f, 696f, Panel, border: true, 18f, grabHint ? Accent : Border, grabHint ? 2f : 1f);
		if (AcquisitionOpen)
		{
			DrawAcquisition(sb);
			return;
		}
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
			if (Chains)
			{
				DrawChains(sb);
			}
			else
			{
				DrawGrid(sb);
			}
			DrawPager(sb);
			if (StorageView)
			{
				DrawStorageDetails(sb);
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
			Box(sb, BookGeometry.ModMenuPanel, new Color(17, 25, 20), border: true, 14f);
			for (int k = 0; k < 9; k++)
			{
				int j2 = ModOffset + k;
				if (j2 > Mods.Count)
				{
					break;
				}
				string mod = ((j2 == 0) ? "" : Mods[j2 - 1]);
				string label2 = ((j2 == 0) ? T("Все источники", "All sources") : ModName(mod));
				Button(sb, BookGeometry.ModOption(k), label2, ModFilter == mod);
				if (BookGeometry.ModOption(k).Contains(Mouse.X, Mouse.Y))
				{
					MouseTip(BookRules.PlainText(label2));
				}
			}
			Text(sb, T("Прокрутите список колёсиком", "Scroll to see more mods"), 196f, 552f, Muted, 0.8f, 530f, 24f);
		}
		if (SortMenu)
		{
			DrawSortMenu(sb);
		}
		if (HelpOpen)
		{
			Main.HoverItem = new Item();
			Main.hoverItemName = "";
			DrawHelp(sb);
		}
	}

	private static void DrawHeader(SpriteBatch sb)
	{
		BookRect title = new BookRect(16f, 16f, TitleRight() - 16f, 32f);
		for (int dot = 0; dot < 6; dot++)
		{
			Box(sb, 20 + dot % 2 * 5, 25 + dot / 2 * 6, 2f, 2f, GrabHint ? Accent : (Muted * 0.8f), border: false, 0f);
		}
		CenterLabel(sb, TitleText, 34f, 16f, 260f, 32f, TextColor, 1.1f, rightAligned: false, leftAligned: true);
		if (!MenuOpen && title.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Перетащите заголовок, чтобы переместить окно", "Drag the header to move the window"));
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
				MouseTip(TabLabel(tab) + " · Ctrl+" + (tab + 1));
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
		SectionLabel(sb, T("Категории", "Categories"), 20f, 86f, 164f);
		CategoryRow = Math.Clamp(CategoryRow, 0, MaxCategoryRow);
		int rows = BookGeometry.VisibleCategoryRows;
		for (int visible = 0; visible < rows; visible++)
		{
			int index = CategoryRow + visible;
			if (index >= Categories.Count)
			{
				break;
			}
			BookCategory category = Categories[index];
			BookRect rect = BookGeometry.CategoryCell(visible);
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
			if (ContentSamples.ItemsByType.TryGetValue(category.IconType, out var icon))
			{
				Icon(sb, icon, rect.X + 8f, rect.Y + 4f, 20f, tooltip: false);
			}
			CenterLabel(sb, category.Label(Ru), rect.X + 36f, rect.Y + 4f, rect.Width - 40f, rect.Height - 8f, active ? Accent : TextColor, 0.8f, rightAligned: false, leftAligned: true);
		}
		if (MaxCategoryRow > 0)
		{
			float track = rows * 28;
			float thumb = Math.Max(28f, track * rows / Categories.Count);
			Box(sb, 188f, 108f, 3f, track, Raised, border: false, 1f);
			Box(sb, 188f, 108f + (track - thumb) * CategoryRow / MaxCategoryRow, 3f, thumb, Muted, border: false, 1f);
		}
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
		Dropdown(sb, BookGeometry.ModFilter, (ModFilter.Length == 0) ? T("Все моды", "All mods") : ModName(ModFilter), ModMenu);
		if (!MenuOpen && BookGeometry.ModFilter.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(((ModFilter.Length == 0) ? "" : (ModName(ModFilter) + "\n")) + T("Фильтр по моду", "Filter by mod"));
		}
		Dropdown(sb, BookGeometry.Sort, RecentView ? T("Новые", "Newest") : SortLabel(), SortMenu, !RecentView);
		if (!MenuOpen && BookGeometry.Sort.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(RecentView ? T("Недавние всегда от новых к старым", "Recent crafts are always newest first") : T("Порядок сортировки", "Sort order"));
		}
		BookRect favorites = BookGeometry.FavoritesFilter;
		IconButtonFrame(sb, favorites, FavoritesOnly);
		Star(sb, favorites.X + 8f, favorites.Y + 8f, 20f, FavoritesOnly);
		if (!MenuOpen && favorites.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Только избранное", "Favorites only") + "\n" + T("Alt+клик по предмету добавляет", "Alt+Click an item to add it"));
		}
		DrawStageFilter(sb);
		if (RecipeSampleFilter)
		{
			DrawSampleChip(sb);
		}
	}

	// On Recipes a chain item narrows the list to the recipes that make it; the chip shows and clears it.
	private static bool RecipeSampleFilter => !Catalogue && !Chains && !StorageView && !RecentView && !TrackingView && SampleType > 0 && ContentSamples.ItemsByType.ContainsKey(SampleType);

	private static BookRect SampleChipRect()
	{
		BookRect[] stages = StageRects();
		float left = stages[^1].X + stages[^1].Width + 12f;
		Item sample = ContentSamples.ItemsByType[SampleType];
		float width = Math.Min(780f - left, 72f + TextWidth(sample.Name, 0.8f));
		if (width < 100f)
		{
			width = 56f;
		}
		return new BookRect(780f - width, BookGeometry.StageFilter.Y, width, BookGeometry.StageFilter.Height);
	}

	private static void DrawSampleChip(SpriteBatch sb)
	{
		BookRect chip = SampleChipRect();
		Item sample = ContentSamples.ItemsByType[SampleType];
		bool hover = !MenuOpen && chip.Contains(Mouse.X, Mouse.Y);
		Box(sb, chip, hover ? HoverFill : Raised, border: true, 8f, Accent);
		Icon(sb, sample, chip.X + 8f, chip.Y + 6f, 20f, tooltip: false);
		if (chip.Width > 56f)
		{
			CenterLabel(sb, sample.Name, chip.X + 34f, chip.Y + 4f, chip.Width - 62f, chip.Height - 8f, TextColor, 0.8f, rightAligned: false, leftAligned: true);
		}
		Cross(sb, chip.X + chip.Width - 16f, chip.Y + chip.Height / 2f, 4f, hover ? TextColor : Muted);
		if (hover)
		{
			MouseTip(T("Рецепты для: ", "Recipes for: ") + sample.Name + "\n" + T("Клик снимает фильтр", "Click to clear"));
		}
	}

	private static void DrawGrid(SpriteBatch sb)
	{
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
			}
			if ((!Catalogue || StorageView) && item.stack > 1)
			{
				Text(sb, item.stack.ToString(), cell.X + 4f, cell.Y + 42f, TextColor, 0.8f, 56f, 20f);
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
		if (StorageView)
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
			EmptyState(sb, area, emptyStorage, "");
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
		DrawDetailHeader(sb, chosen, r);
		Box(sb, 812f, top - 12f, 284f, 1f, Border, border: false, 0f);
		if (r != null)
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
				MouseTip(T("Все источники: ", "All sources: ") + chosen.Name);
			}
		}
		bool favorite = Favorites.Contains(ItemKey(chosen));
		BookRect favoriteRect = BookGeometry.FavoriteAction;
		IconButtonFrame(sb, favoriteRect, favorite);
		Star(sb, favoriteRect.X + 6f, favoriteRect.Y + 6f, 20f, favorite);
		if (!MenuOpen && favoriteRect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(favorite ? T("Убрать из избранного · Alt+клик", "Remove from favorites · Alt+Click") : T("В избранное · Alt+клик", "Add to favorites · Alt+Click"));
		}
		if (r != null)
		{
			ActionButton(sb, BookGeometry.CraftAction, T("Создать", "Craft") + " ×" + PreviewBatches(r), primary: true, BookCrafting.CanCraft(r));
			if (!MenuOpen && BookGeometry.CraftAction.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(BookCrafting.Reason(r, Ru) + "\nCtrl+Enter");
			}
		}
		DrawTrackButton(sb, r);
	}

	// Header: icon, name and "<item's own mod> · stage". An armor set shows its three pieces instead.
	private static void DrawDetailHeader(SpriteBatch sb, Item chosen, Recipe r)
	{
		string name = chosen.Name + ((r != null && r.createItem.stack > 1) ? (" ×" + r.createItem.stack) : "");
		string mod = ModName(chosen.ModItem?.Mod.Name ?? "Terraria");
		string stage = StageName(chosen, (Catalogue || ArmorFocus > 0) ? null : r);
		Recipe stageRecipe = (Catalogue || ArmorFocus > 0) ? null : r;
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
		bool variants = (Catalogue || ArmorFocus > 0) && VariantCount > 1;
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

	private static void DrawDetailRow(SpriteBatch sb, Detail d, BookRect row, int kind)
	{
		bool hasItem = d.Icon != null && !d.Icon.IsAir;
		Box(sb, row, Raised, border: false, 8f);
		float textX = row.X + 10f;
		if (d.Icon != null)
		{
			Icon(sb, d.Icon, row.X + 6f, row.Y + 4f, 24f, tooltip: false);
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
		CenterLabel(sb, d.Name, textX, row.Y + 4f, right - textX, row.Height - 8f, (kind == 2) ? Caution : ((d.Icon == null) ? Muted : TextColor), 0.85f, rightAligned: false, leftAligned: true);
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
			string action = hasItem ? T("Клик: цепочка этого предмета", "Click: open its chain") : "";
			string detail = (kind == 0) ? d.Tooltip : "";
			MouseTip(d.Name + "\n" + string.Join(" · ", new string[2] { detail, action }.Where((string s) => s.Length > 0)));
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
		if (StorageTabVisible)
		{
			BookRect[] sources = CraftSourceRects();
			Segmented(sb, sources, CraftSourceLabels(), BookStorage.UseForCrafting ? 1 : 0, 0.8f);
			string from = T("Крафт из", "Craft from");
			float fromWidth = TextWidth(from, 0.8f) + 4f;
			CenterLabel(sb, from, sources[0].X - 8f - fromWidth, 660f, fromWidth, 24f, Muted, 0.8f, rightAligned: true);
			hintRight = sources[0].X - 24f - fromWidth;
			if (!MenuOpen && SegmentAt(sources) >= 0)
			{
				MouseTip(T("Откуда брать материалы", "Where materials come from") + "\n" + T("Хранилищу нужен подключённый интерфейс крафта", "Storage needs a connected Crafting Interface"));
			}
		}
		string message = BookCrafting.LastMessage ?? "";
		CenterLabel(sb, (message.Length > 0) ? message : FooterHint(), 140f, 660f, hintRight - 140f, 24f, (message.Length > 0) ? Accent : Muted, 0.8f, rightAligned: false, leftAligned: true);
	}

	private static string FooterHint()
	{
		return CurrentTab switch
		{
			0 => T("Shift+клик: цепочка предмета · F1: справка", "Shift+Click an item for its chain · F1 Help"),
			1 => T("Ctrl+Enter: создать · F1: справка", "Ctrl+Enter crafts · F1 Help"),
			2 => T("Кнопка >: следующий шаг · Alt+←: назад", "> opens the next step · Alt+← goes back"),
			3 => T("Shift+клик: стопку в инвентарь · F1: справка", "Shift+Click takes a stack · F1 Help"),
			4 => T("Новые сверху · F1: справка", "Newest first · F1 Help"),
			_ => T("PgUp/PgDn: страницы · F1: справка", "PgUp/PgDn pages · F1 Help"),
		};
	}

	private static string[] CraftSourceLabels()
	{
		return new string[2]
		{
			T("Инвентарь", "Inventory"),
			T("Хранилище", "Storage")
		};
	}

	private static BookRect[] CraftSourceRects()
	{
		return SegmentRects(BookGeometry.CraftSource, CraftSourceLabels(), 0.8f, natural: false);
	}

	private static void DrawSortMenu(SpriteBatch sb)
	{
		BookRect panel = BookGeometry.SortMenuPanel;
		Box(sb, panel, new Color(17, 25, 20), border: true, 10f, Accent);
		for (int option = 0; option < 6; option++)
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

	private static int CurrentTab => TrackingView ? 5 : (RecentView ? 4 : (StorageView ? 3 : (Chains ? 2 : ((!Catalogue) ? 1 : 0))));

	private static string TabLabel(int tab)
	{
		return tab switch
		{
			0 => T("Предметы", "Items"),
			1 => T("Рецепты", "Recipes"),
			2 => T("Цепочки", "Chains"),
			3 => T("Хранилище", "Storage"),
			4 => T("Недавние", "Recent"),
			_ => T("Отслеживание", "Tracking"),
		};
	}

	// The Storage tab exists only when Magic Storage is loaded.
	private static List<int> VisibleTabs()
	{
		List<int> tabs = new List<int>();
		for (int tab = 0; tab < BookGeometry.TabCount; tab++)
		{
			if (tab != 3 || StorageTabVisible)
			{
				tabs.Add(tab);
			}
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
		float[] widths = tabs.Select((int t) => TextWidth(TabLabel(t), TabTextSize) + 32f).ToArray();
		float equal = Math.Max(96f, widths.Max());
		bool same = equal * tabs.Count + 8f * (tabs.Count - 1) <= BookGeometry.TabRight - TitleRight() - 24f;
		float x = BookGeometry.TabRight;
		for (int i = tabs.Count - 1; i > slot; i--)
		{
			x -= (same ? equal : widths[i]) + 8f;
		}
		float width = (same ? equal : widths[slot]);
		return new BookRect(x - width, BookGeometry.TabTop, width, BookGeometry.TabHeight);
	}

	private static void HelpKeycap(SpriteBatch sb, BookRect rect, string label)
	{
		Box(sb, rect.X, rect.Y, rect.Width, rect.Height, new Color(50, 76, 59), border: true, 7f, Accent, 2f);
		CenterLabel(sb, label, rect.X + 6f, rect.Y + 3f, rect.Width - 12f, rect.Height - 6f, Color.White, 0.82f);
	}

	private static void DrawHelp(SpriteBatch sb)
	{
		Box(sb, 0f, 0f, 1120f, 696f, new Color(10, 16, 12, 235), border: false, 12f);
		BookRect modal = BookGeometry.HelpPanel;
		Box(sb, modal.X, modal.Y, modal.Width, modal.Height, Surface, border: true, 14f);
		Text(sb, T("БЫСТРЫЕ ДЕЙСТВИЯ", "QUICK ACTIONS"), modal.X + 24f, modal.Y + 16f, Accent, 1f, 720f, 32f);
		HelpKeycap(sb, new BookRect(modal.X + modal.Width - 132f, modal.Y + 16f, 54f, 32f), "F1");
		Button(sb, BookGeometry.HelpClose, "X");
		BookHelp.Section[] sections = BookHelp.Sections;
		for (int i = 0; i < sections.Length; i++)
		{
			BookHelp.Section section = sections[i];
			BookRect bounds = section.Bounds;
			Box(sb, bounds.X, bounds.Y, bounds.Width, bounds.Height, Panel, border: true, 10f);
			Text(sb, T(section.Ru, section.En), bounds.X + 16f, bounds.Y + 8f, Accent, 0.78f, bounds.Width - 32f, 24f);
			for (int j = 0; j < section.Rows.Length; j++)
			{
				BookHelp.Entry row = section.Rows[j];
				HelpKeycap(sb, BookHelp.KeyRect(section, j), T(row.KeyRu, row.KeyEn));
				BookRect label = BookHelp.LabelRect(section, j);
				Text(sb, T(row.Ru, row.En), label.X, label.Y, TextColor, 0.78f, label.Width, label.Height);
			}
		}
		BookRect storage = BookHelp.Sections[2].Bounds;
		float nx = storage.X + 16f;
		float ny = storage.Y + 314f;
		float nw = storage.Width - 32f;
		Box(sb, nx, ny - 8f, nw, 1f, Border, border: false, 0f);
		Text(sb, T("Получение: ячейки слева, курсор пустой.", "Withdraw: use left-side cells, cursor empty."), nx, ny + 4f, Muted, 0.78f, nw, 24f);
		Text(sb, T("Быстрый стак и «Сложить всё» сохраняют", "Quick stack and Deposit all preserve"), nx, ny + 44f, Muted, 0.78f, nw, 24f);
		Text(sb, T("панель быстрого доступа и избранное.", "your hotbar and favorited items."), nx, ny + 68f, Muted, 0.78f, nw, 24f);
		Text(sb, T("* Сброс — вне поля поиска.", "* Reset — outside the search box."), modal.X + 24f, modal.Y + 548f, Muted, 0.76f, 610f, 24f);
		HelpKeycap(sb, new BookRect(modal.X + 24f, modal.Y + 580f, 54f, 28f), "Esc");
		Text(sb, T("Закрыть текущее окно", "Close the current window"), modal.X + 90f, modal.Y + 580f, Muted, 0.8f, 500f, 28f);
	}
}
