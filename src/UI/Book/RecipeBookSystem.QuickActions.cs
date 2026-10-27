using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Chat;
using Terraria.ID;
using Terraria.ModLoader;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ModernRecipeBook.UI.Book;

// What the quick search can do with a result without opening it: every action runs the book's own code for it (the
// card's craft, Track and favorite, the Storage grid's take, Deposit All, the stage filter) and gives that code's
// feedback, which the field's footer shows. Each row offers its most useful action as a button (QuickPrimary), the other
// row actions on hover, all of them in the preview. Keys are the card's: Ctrl+Enter the row's action (craft, as in the
// card; take, as on Storage), Ctrl+Shift+Enter craft the most, Alt+T track, Alt+F favorite, Alt+G How to Get, Alt+U Used
// in (a mob's stats), Shift+Enter Used in. A ">" first lists the book's commands (docs/quick-search.md).
public sealed partial class RecipeBookSystem
{
	private enum QuickAction
	{
		None,
		Open,
		Craft,
		CraftMax,
		Take,
		TakeOne,
		Deposit,
		Track,
		TrackSet,
		Favorite,
		HowToGet,
		UsedIn,
		Contents,
		BeforeBoss,
		ChatLink,
		Stats,
		SummonCraft,
		SummonHowToGet,
		SummonTrack
	}

	// The commands a ">" lists. The last five are the quick search's own picks for a player working toward a boss: the
	// stage before it, its card, what can be made now, the favorites and the summoner's items.
	private enum QuickCommand
	{
		DepositAll,
		QuickStack,
		Restock,
		Refresh,
		ClearFilters,
		TabItems,
		TabRecipes,
		TabBestiary,
		TabStorage,
		TabRecent,
		TabTracking,
		TrackerStyle,
		Help,
		Settings,
		UntrackAll,
		BeforeNextBoss,
		NextBoss,
		CraftableNow,
		Favorites,
		Summoner
	}

	// When the footer last took an action's feedback (BookCrafting.LastMessage, BookSources.Message).
	private static long QuickStatusAt;

	// A command that asks a second Enter (Untrack all), and when it was armed.
	private static int QuickArmed = -1;

	private static ulong QuickArmedAt;

	// Per item type, read once per storage count (BookSources.Version): how many the player has.
	private static int[] QuickOwnedCounts = Array.Empty<int>();

	private static int[] QuickOwnedVersions = Array.Empty<int>();

	// The Storage grid's biggest stack of each type (BookSources.Items), per grid version.
	private static readonly Dictionary<int, int> QuickStacks = new Dictionary<int, int>();

	private static int QuickStacksVersion = -1;

	// Per item type: the recipe that can be crafted now, per craftable set and storage count.
	private static readonly Dictionary<int, Recipe> QuickCraftables = new Dictionary<int, Recipe>();

	private static int QuickCraftablesVersion = -1;

	// The versions the rows were ranked with: a craft, a take or a scan changes them, and the rows are ranked again.
	private static (int Sources, int Items, int Craft, int Goals) QuickSeenVersions;

	// The item a row acts on: an item row or link, a goal's item; 0 for the rest.
	private static int QuickItemOf(QuickRow row)
	{
		if (row.Kind == QuickKind.Item)
		{
			return row.Id;
		}
		if (row.Kind == QuickKind.Goal && row.Id >= 0 && row.Id < TrackedGoals.Count)
		{
			TrackedGoal goal = TrackedGoals[row.Id];
			return goal.Recipe?.createItem.type ?? goal.Parts.FirstOrDefault()?.type ?? 0;
		}
		return 0;
	}

	private static int QuickOwned(int type)
	{
		if (QuickOwnedCounts.Length != ItemLoader.ItemCount)
		{
			QuickOwnedCounts = new int[ItemLoader.ItemCount];
			QuickOwnedVersions = new int[ItemLoader.ItemCount];
		}
		if ((uint)type >= (uint)QuickOwnedCounts.Length)
		{
			return 0;
		}
		// Versions start at 0, so the stamp is one more.
		if (QuickOwnedVersions[type] != BookSources.Version + 1)
		{
			QuickOwnedVersions[type] = BookSources.Version + 1;
			QuickOwnedCounts[type] = BookSources.Quantity(type);
		}
		return QuickOwnedCounts[type];
	}

	// The index in BookSources.Items of the biggest stack of the type nearby storage holds, -1 when none.
	private static int QuickStorageStack(int type)
	{
		if (QuickStacksVersion != BookSources.ItemsVersion)
		{
			QuickStacksVersion = BookSources.ItemsVersion;
			QuickStacks.Clear();
			for (int i = 0; i < BookSources.Items.Count; i++)
			{
				Item item = BookSources.Items[i];
				if (!QuickStacks.TryGetValue(item.type, out int best) || BookSources.Items[best].stack < item.stack)
				{
					QuickStacks[item.type] = i;
				}
			}
		}
		return (BookSources.Enabled && QuickStacks.TryGetValue(type, out int index)) ? index : (-1);
	}

	// The recipe the card would show for the item (its first variant's member: one that can be crafted now first), per
	// craftable set and storage count: rows ask every frame.
	private static Recipe QuickRecipe(int type)
	{
		FollowQuickCraftables();
		if (!QuickRecipes.TryGetValue(type, out Recipe recipe))
		{
			List<BookRecipeVariants.Variant> variants = BookRecipeVariants.For(type);
			recipe = (variants.Count == 0) ? null : ActiveMember(variants[0].Representative);
			QuickRecipes[type] = recipe;
		}
		return recipe;
	}

	private static readonly Dictionary<int, Recipe> QuickRecipes = new Dictionary<int, Recipe>();

	// Whether the item opens on Contents (HasContents reads the drop database, a new list each time), per world.
	private static readonly Dictionary<int, bool> QuickContents = new Dictionary<int, bool>();

	private static bool QuickHasContents(int type)
	{
		if (!QuickContents.TryGetValue(type, out bool has))
		{
			has = (QuickContents[type] = HasContents(type));
		}
		return has;
	}

	private static void FollowQuickCraftables()
	{
		int version = BookCrafting.AvailabilityVersion * 7919 + BookSources.Version;
		if (QuickCraftablesVersion != version)
		{
			QuickCraftablesVersion = version;
			QuickCraftables.Clear();
			QuickRecipes.Clear();
		}
	}

	// A recipe of the item that can be crafted now (any variant), or null.
	private static Recipe QuickCraftable(int type)
	{
		FollowQuickCraftables();
		if (!QuickCraftables.TryGetValue(type, out Recipe recipe))
		{
			recipe = BookRecipeVariants.For(type).Select((BookRecipeVariants.Variant v) => ActiveMember(v.Representative)).FirstOrDefault(BookCrafting.IsAvailable);
			QuickCraftables[type] = recipe;
		}
		return recipe;
	}

	private static bool QuickTracked(Recipe recipe)
	{
		return recipe != null && TrackedGoals.Any((TrackedGoal g) => g.Key == TrackKey(recipe));
	}

	// A stack of it in slots 10..49 that Deposit All would take, with somewhere to put it.
	private static bool QuickDepositable(int type)
	{
		if (!BookSources.CanDeposit)
		{
			return false;
		}
		Item[] inventory = Main.LocalPlayer.inventory;
		for (int k = 10; k < 50; k++)
		{
			if (inventory[k] != null && inventory[k].type == type && !inventory[k].IsAir && !inventory[k].favorited)
			{
				return true;
			}
		}
		return false;
	}

	// A boss's summon item as Boss Checklist (or Calamity's registration) names it, 0 when none is known.
	private static int QuickSummonItem(int mob)
	{
		if (mob < 0 || mob >= BookBestiary.Mobs.Count || BookBestiary.Mobs[mob].Kind != BookMobKind.Boss)
		{
			return 0;
		}
		int npc = BookBestiary.Mobs[mob].Sample.type;
		return BookBossInfo.Of(npc).SelectMany((BookBossInfo.Entry e) => e.Items).FirstOrDefault(ContentSamples.ItemsByType.ContainsKey);
	}

	// The stage that first lists the item: "Before: <the boss after the one it needs>" (BookProgression.ItemStep: step k
	// means boss k is down; the filter lists steps below its boss), -1 when its stage is unknown or after the last boss.
	private static int QuickStageAfter(int type)
	{
		int step = BookProgression.ItemStep(type);
		return (step >= 0 && step + 1 < BookSteps.All.Count) ? (step + 1) : (-1);
	}

	// The step of a boss mob, for "Before: <this boss>"; -1 for anything else.
	private static int QuickBossStep(int mob)
	{
		if (mob < 0 || mob >= BookBestiary.Mobs.Count || BookBestiary.Mobs[mob].Kind != BookMobKind.Boss)
		{
			return -1;
		}
		int step = BookSteps.Npc(BookBestiary.Mobs[mob].Sample.type);
		return (step > 0 && step < BookSteps.All.Count) ? step : (-1);
	}

	// The row's button, picked the same way everywhere: an item that can be crafted now crafts, one in nearby storage is
	// taken, one with a recipe is tracked; everything else opens. Links have none.
	private static QuickAction QuickPrimary(QuickRow row)
	{
		if (row.Link != QuickLink.None || row.Kind is QuickKind.Header or QuickKind.ShowAll)
		{
			return QuickAction.None;
		}
		if (row.Kind == QuickKind.Item)
		{
			int type = row.Id;
			if (QuickCraftable(type) != null)
			{
				return QuickAction.Craft;
			}
			if (QuickStorageStack(type) >= 0)
			{
				return QuickAction.Take;
			}
			return (QuickRecipe(type) != null) ? QuickAction.Track : QuickAction.Open;
		}
		return QuickAction.Open;
	}

	// Ctrl+Enter: the card's craft, or Storage's take for an item nearby storage holds that cannot be crafted now.
	private static QuickAction QuickPrimaryKeyAction(QuickRow row)
	{
		int type = QuickItemOf(row);
		if (type <= 0)
		{
			return QuickAction.Open;
		}
		return (QuickCraftable(type) == null && QuickStorageStack(type) >= 0) ? QuickAction.Take : QuickAction.Craft;
	}

	// The other actions a row shows as small buttons while hovered.
	private static IEnumerable<QuickAction> QuickRowExtras(QuickRow row, QuickAction primary)
	{
		if (row.Link != QuickLink.None)
		{
			yield break;
		}
		if (row.Kind == QuickKind.Item)
		{
			if (primary != QuickAction.Track && QuickRecipe(row.Id) != null)
			{
				yield return QuickAction.Track;
			}
			yield return QuickAction.Favorite;
			yield return QuickAction.HowToGet;
			yield return QuickAction.UsedIn;
		}
		else if (row.Kind == QuickKind.Mob)
		{
			yield return QuickAction.Stats;
			yield return QuickAction.Favorite;
		}
		else if (row.Kind == QuickKind.Goal)
		{
			yield return QuickAction.Track;
			yield return QuickAction.HowToGet;
		}
	}

	// Everything the preview offers for the selected row, the row's own action first.
	private static List<QuickAction> QuickActionsOf(QuickRow row)
	{
		List<QuickAction> actions = new List<QuickAction>();
		int type = QuickItemOf(row);
		if (row.Kind is QuickKind.Item or QuickKind.Goal)
		{
			Item sample = ContentSamples.ItemsByType[type];
			bool craftable = QuickCraftable(type) != null;
			actions.Add(QuickAction.Open);
			if (craftable)
			{
				actions.Add(QuickAction.Craft);
				actions.Add(QuickAction.CraftMax);
			}
			if (QuickStorageStack(type) >= 0)
			{
				actions.Add(QuickAction.Take);
			}
			if (QuickDepositable(type))
			{
				actions.Add(QuickAction.Deposit);
			}
			if (QuickRecipe(type) != null || row.Kind == QuickKind.Goal)
			{
				actions.Add(QuickAction.Track);
			}
			if (SetPartsOf(sample) != null)
			{
				actions.Add(QuickAction.TrackSet);
			}
			actions.Add(QuickAction.Favorite);
			actions.Add(QuickAction.HowToGet);
			actions.Add(QuickAction.UsedIn);
			if (QuickHasContents(type))
			{
				actions.Add(QuickAction.Contents);
			}
			if (QuickStageAfter(type) >= 0)
			{
				actions.Add(QuickAction.BeforeBoss);
			}
			actions.Add(QuickAction.ChatLink);
		}
		else if (row.Kind == QuickKind.Mob)
		{
			actions.Add(QuickAction.Open);
			actions.Add(QuickAction.Stats);
			actions.Add(QuickAction.Favorite);
			if (QuickBossStep(row.Id) >= 0)
			{
				actions.Add(QuickAction.BeforeBoss);
			}
			int summon = QuickSummonItem(row.Id);
			if (summon > 0)
			{
				if (QuickCraftable(summon) != null)
				{
					actions.Add(QuickAction.SummonCraft);
				}
				actions.Add(QuickAction.SummonHowToGet);
				if (QuickRecipe(summon) != null)
				{
					actions.Add(QuickAction.SummonTrack);
				}
			}
		}
		else if (row.Kind is not (QuickKind.Header or QuickKind.ShowAll))
		{
			actions.Add(QuickAction.Open);
		}
		QuickAction primary = QuickPrimary(row);
		if (primary != QuickAction.None && actions.Remove(primary))
		{
			actions.Insert(0, primary);
		}
		return actions;
	}

	// The button's label: what it does to this row ("Craft ×1", "Untrack", "Open in the Bestiary").
	private static string QuickActionLabel(QuickRow row, QuickAction action)
	{
		int type = QuickItemOf(row);
		switch (action)
		{
			case QuickAction.Open:
				return row.Kind switch
				{
					QuickKind.Mob => L("QuickSearchDesign.ActionLoot"),
					QuickKind.Goal => L("QuickSearchDesign.ActionOpenGoal"),
					QuickKind.Stage => L("QuickSearchDesign.ActionFilter"),
					QuickKind.Command => L("QuickSearchDesign.ActionRun"),
					QuickKind.MobCategory => L("Common.OpenInBestiary"),
					QuickKind.Item when row.Group == QuickGroup.Storage => L("QuickSearchDesign.ActionOpenStorage"),
					QuickKind.Item when row.Group == QuickGroup.Recipes => L("Common.OpenRecipe"),
					_ => L("QuickSearch.Open")
				};
			case QuickAction.Craft:
				return L("Common.CraftTimes", 1);
			case QuickAction.CraftMax:
			{
				Recipe recipe = QuickCraftable(type);
				return L("Common.CraftTimes", (recipe == null) ? 1 : Math.Max(1, CachedAvailableBatches(recipe)));
			}
			case QuickAction.Take:
				return L("QuickSearchDesign.ActionTake");
			case QuickAction.Deposit:
				return L("QuickSearchDesign.ActionDeposit");
			case QuickAction.Track:
				return (row.Kind == QuickKind.Goal || QuickTracked(QuickRecipe(type))) ? L("Common.Untrack") : L("Common.Track");
			case QuickAction.TrackSet:
				return TrackedGoals.Any((TrackedGoal g) => g.Key == SetGoalKey(SetPartsOf(ContentSamples.ItemsByType[type]))) ? L("Common.UntrackSet") : L("Common.TrackSet");
			case QuickAction.Favorite:
				return QuickFavorite(row) ? L("QuickSearchDesign.ActionUnfavorite") : L("Help.Favorite");
			case QuickAction.HowToGet:
				return L("Common.HowToGet");
			case QuickAction.UsedIn:
				return L("Common.UsedIn");
			case QuickAction.Contents:
				return L("QuickSearchDesign.ActionContents");
			case QuickAction.BeforeBoss:
			{
				int step = (row.Kind == QuickKind.Mob) ? QuickBossStep(row.Id) : QuickStageAfter(type);
				return L("Stages.Before", BookSteps.Name(Math.Max(0, step)));
			}
			case QuickAction.ChatLink:
				return L("QuickSearchDesign.ActionChat");
			case QuickAction.Stats:
				return L("QuickSearchDesign.ActionStats");
			case QuickAction.SummonCraft:
				return L("QuickSearchDesign.ActionSummonCraft", ContentSamples.ItemsByType[QuickSummonItem(row.Id)].Name);
			case QuickAction.SummonHowToGet:
				return L("QuickSearchDesign.ActionSummonHowToGet", ContentSamples.ItemsByType[QuickSummonItem(row.Id)].Name);
			case QuickAction.SummonTrack:
				return L("QuickSearchDesign.ActionSummonTrack", ContentSamples.ItemsByType[QuickSummonItem(row.Id)].Name);
			default:
				return "";
		}
	}

	// The key that runs it on the selected row, "" when none does.
	private static string QuickActionKey(QuickRow row, QuickAction action)
	{
		return action switch
		{
			QuickAction.Open => L("QuickSearch.KeyEnter"),
			QuickAction.Craft or QuickAction.Take when QuickPrimaryKeyAction(row) == action => L("Help.KeyCtrlEnter"),
			QuickAction.CraftMax => L("Help.KeyCtrlShiftEnter"),
			QuickAction.Track => L("Help.KeyAltT"),
			QuickAction.Favorite => L("Help.KeyAltF"),
			QuickAction.HowToGet => L("Help.KeyAltG"),
			QuickAction.UsedIn or QuickAction.Stats => L("Help.KeyAltU"),
			_ => ""
		};
	}

	// What the button does, for its tooltip: the label, the key and how a modifier changes it.
	private static string QuickActionTip(QuickRow row, QuickAction action)
	{
		string key = QuickActionKey(row, action);
		string tip = QuickActionLabel(row, action) + ((key.Length > 0) ? (" · " + key) : "");
		int type = QuickItemOf(row);
		return action switch
		{
			QuickAction.Craft => tip + "\n" + L("QuickSearchDesign.TipCraft", L("Help.KeyCtrlShiftEnter")) + "\n" + BookCrafting.Sources(QuickCraftable(type), 1),
			QuickAction.Take => tip + "\n" + L("QuickSearchDesign.TipTake") + "\n" + BookSources.Breakdown(type),
			QuickAction.Deposit => tip + "\n" + L("QuickSearchDesign.TipDeposit"),
			QuickAction.ChatLink => tip + "\n" + L("QuickSearchDesign.TipChat"),
			QuickAction.BeforeBoss => tip + "\n" + L("QuickSearchDesign.TipBeforeBoss"),
			_ => tip
		};
	}

	private static bool QuickFavorite(QuickRow row)
	{
		if (row.Kind == QuickKind.Mob)
		{
			return Favorites.Contains(BookBestiary.Mobs[row.Id].Key);
		}
		int type = QuickItemOf(row);
		return type > 0 && Favorites.Contains(ItemKey(ContentSamples.ItemsByType[type]));
	}

	// Runs an action through the book's own code. Those that show something open the book there (through its history);
	// the others stay in the field, and its footer shows what they said.
	private static void RunQuickAction(QuickRow row, QuickAction action)
	{
		int type = QuickItemOf(row);
		// The footer shows only what this action says (a take says nothing; its sound is the feedback).
		BookCrafting.LastMessage = "";
		switch (action)
		{
			case QuickAction.None:
				return;
			case QuickAction.Open:
				ActivateQuickRow(row);
				return;
			case QuickAction.Stats:
				if (row.Kind == QuickKind.Mob)
				{
					RememberQuickPick(row with { Link = QuickLink.None, Hint = "", Why = QuickWhy.Name });
					BeginQuickNavigation();
					ShowMob(row.Id, BookBestiary.Mobs[row.Id].NetId);
					MobStatsMode = true;
				}
				return;
			case QuickAction.Favorite when row.Kind == QuickKind.Mob:
				ToggleMobFavorite(BookBestiary.Mobs[row.Id]);
				return;
			case QuickAction.BeforeBoss when row.Kind == QuickKind.Mob:
			{
				int step = QuickBossStep(row.Id);
				if (step >= 0)
				{
					BeginQuickNavigation();
					QuickStageFilter(step);
				}
				return;
			}
			case QuickAction.Track when row.Kind == QuickKind.Goal:
				if (row.Id >= 0 && row.Id < TrackedGoals.Count)
				{
					TrackedGoal goal = TrackedGoals[row.Id];
					ToggleTrack(goal.Recipe, goal.IsSet ? goal.Parts : null, goal.IsSet);
					QuickActed();
					KeepQuickRows();
				}
				return;
			case QuickAction.SummonCraft:
				RunItemAction(QuickSummonItem(row.Id), QuickAction.Craft);
				return;
			case QuickAction.SummonHowToGet:
				RunItemAction(QuickSummonItem(row.Id), QuickAction.HowToGet);
				return;
			case QuickAction.SummonTrack:
				RunItemAction(QuickSummonItem(row.Id), QuickAction.Track);
				return;
		}
		if (type > 0)
		{
			RunItemAction(type, action);
		}
		else if (action == QuickAction.Craft)
		{
			ActivateQuickRow(row);
		}
	}

	private static void RunItemAction(int type, QuickAction action)
	{
		if (type <= 0 || !ContentSamples.ItemsByType.TryGetValue(type, out Item sample))
		{
			return;
		}
		switch (action)
		{
			case QuickAction.Craft:
				// The card's Ctrl+Enter: its recipe, once; BookCrafting says why when it cannot.
				BookCrafting.TryCraft(QuickCraftable(type) ?? QuickRecipe(type), 1);
				QuickActed();
				break;
			case QuickAction.CraftMax:
			{
				Recipe recipe = QuickCraftable(type) ?? QuickRecipe(type);
				BookCrafting.TryCraft(recipe, (recipe == null) ? 1 : Math.Max(1, CachedAvailableBatches(recipe)));
				QuickActed();
				break;
			}
			case QuickAction.Take:
			case QuickAction.TakeOne:
			{
				int stack = QuickStorageStack(type);
				if (stack < 0)
				{
					return;
				}
				Item shown = BookSources.Items[stack];
				if (action == QuickAction.Take)
				{
					BookSources.TakeToInventory(shown);
				}
				else if (BookSources.TakeOne(shown))
				{
					SoundEngine.PlaySound(in SoundID.MenuTick);
				}
				QuickActed();
				break;
			}
			case QuickAction.Deposit:
				BookSources.DepositAll(type);
				QuickActed();
				break;
			case QuickAction.Track:
			case QuickAction.TrackSet:
				ToggleTrack(QuickRecipe(type), SetPartsOf(sample), action == QuickAction.TrackSet);
				QuickActed();
				KeepQuickRows();
				break;
			case QuickAction.Favorite:
				ToggleFavorite(sample);
				break;
			case QuickAction.HowToGet:
				BeginQuickNavigation();
				QuickShowItem(type, CardModeFor(type));
				OpenAcquisition(type, null);
				break;
			case QuickAction.UsedIn:
				BeginQuickNavigation();
				QuickShowItem(type, BookCardMode.UsedIn);
				break;
			case QuickAction.Contents:
				BeginQuickNavigation();
				QuickShowItem(type, BookCardMode.Contents);
				break;
			case QuickAction.BeforeBoss:
			{
				int step = QuickStageAfter(type);
				if (step >= 0)
				{
					BeginQuickNavigation();
					QuickStageFilter(step);
				}
				break;
			}
			case QuickAction.ChatLink:
				// The chat opens with the item's tag ([i:ID]), as a shift-click on an inventory item puts it there.
				CloseQuickSearch();
				Main.OpenPlayerChat();
				Main.chatText += ItemTagHandler.GenerateTag(sample);
				break;
		}
	}

	// The footer shows what the action said for a few seconds.
	private static void QuickActed()
	{
		QuickStatusAt = Stopwatch.GetTimestamp();
	}

	private static string QuickStatus()
	{
		if (QuickStatusAt == 0 || Stopwatch.GetTimestamp() - QuickStatusAt > Stopwatch.Frequency * 4)
		{
			return "";
		}
		string said = BookCrafting.LastMessage;
		return (said.Length > 0) ? said : BookSources.Message;
	}

	// After a craft, a take or a scan the counts and what can be crafted change: the same query ranked again, the selected
	// row kept.
	private static void FollowQuickVersions()
	{
		(int, int, int, int) seen = (BookSources.Version, BookSources.ItemsVersion, BookCrafting.AvailabilityVersion, TrackedGoals.Count);
		if (seen != QuickSeenVersions)
		{
			QuickSeenVersions = seen;
			KeepQuickRows();
		}
	}

	private static void KeepQuickRows()
	{
		QuickRow? selected = (QuickSelected >= 0 && QuickSelected < QuickRows.Count) ? QuickRows[QuickSelected] : null;
		int scroll = QuickScroll;
		int age = QuickSelectedAge;
		RunQuickSearch();
		KeepQuickSelection(selected, scroll);
		QuickSelectedAge = age;
	}

	// Commands ----------------------------------------------------------------------------------------------------------

	private static void AddCommandRows(string rest)
	{
		string[] terms = BookRules.SearchTerms(rest);
		string query = string.Join(" ", terms);
		List<(QuickCommand Command, int Score)> found = new List<(QuickCommand, int)>();
		foreach (QuickCommand command in Enum.GetValues<QuickCommand>())
		{
			string[] fields = { N(QuickCommandLabel(command)), N(EnglishOf(KeyOf(QuickCommandKey(command)), QuickCommandLabel(command))) };
			int score = (terms.Length == 0) ? 1 : BookQuickRank.Score(query, terms, fields, fields.Length);
			if (score > 0)
			{
				found.Add((command, score));
			}
		}
		// The chips' row names them already: no header.
		foreach ((QuickCommand command, _) in found.OrderByDescending(((QuickCommand Command, int Score) f) => f.Score).ThenBy(((QuickCommand Command, int Score) f) => f.Command))
		{
			QuickRows.Add(new QuickRow(QuickKind.Command, QuickGroup.Commands, (int)command));
		}
	}

	// The localization key of a command's name (QuickSearchDesign.Command*), its hint is the same key with "Tip".
	private static string QuickCommandKey(QuickCommand command)
	{
		return command switch
		{
			QuickCommand.DepositAll => "QuickSearchDesign.CommandDepositAll",
			QuickCommand.QuickStack => "QuickSearchDesign.CommandQuickStack",
			QuickCommand.Restock => "QuickSearchDesign.CommandRestock",
			QuickCommand.Refresh => "QuickSearchDesign.CommandRefresh",
			QuickCommand.ClearFilters => "QuickSearchDesign.CommandClearFilters",
			QuickCommand.TabItems => "QuickSearchDesign.CommandTabItems",
			QuickCommand.TabRecipes => "QuickSearchDesign.CommandTabRecipes",
			QuickCommand.TabBestiary => "QuickSearchDesign.CommandTabBestiary",
			QuickCommand.TabStorage => "QuickSearchDesign.CommandTabStorage",
			QuickCommand.TabRecent => "QuickSearchDesign.CommandTabRecent",
			QuickCommand.TabTracking => "QuickSearchDesign.CommandTabTracking",
			QuickCommand.TrackerStyle => "QuickSearchDesign.CommandTrackerStyle",
			QuickCommand.Help => "QuickSearchDesign.CommandHelp",
			QuickCommand.Settings => "QuickSearchDesign.CommandSettings",
			QuickCommand.UntrackAll => "QuickSearchDesign.CommandUntrackAll",
			QuickCommand.BeforeNextBoss => "QuickSearchDesign.CommandBeforeNextBoss",
			QuickCommand.NextBoss => "QuickSearchDesign.CommandNextBoss",
			QuickCommand.CraftableNow => "QuickSearchDesign.CommandCraftableNow",
			QuickCommand.Favorites => "QuickSearchDesign.CommandFavorites",
			_ => "QuickSearchDesign.CommandSummoner"
		};
	}

	private static string QuickCommandLabel(QuickCommand command)
	{
		return L(QuickCommandKey(command));
	}

	// The command's hint; the keys checker sees each key spelled out.
	private static string QuickCommandTip(QuickCommand command)
	{
		return command switch
		{
			QuickCommand.DepositAll => L("QuickSearchDesign.CommandDepositAllTip"),
			QuickCommand.QuickStack => L("QuickSearchDesign.CommandQuickStackTip"),
			QuickCommand.Restock => L("QuickSearchDesign.CommandRestockTip"),
			QuickCommand.Refresh => L("QuickSearchDesign.CommandRefreshTip"),
			QuickCommand.ClearFilters => L("QuickSearchDesign.CommandClearFiltersTip"),
			QuickCommand.TabItems or QuickCommand.TabRecipes or QuickCommand.TabBestiary or QuickCommand.TabStorage or QuickCommand.TabRecent or QuickCommand.TabTracking => L("QuickSearchDesign.CommandTabTip", TabLabel(QuickCommandTab(command))),
			QuickCommand.TrackerStyle => ResourceTrackerStyle ? L("QuickSearchDesign.CommandTrackerStyleToCards") : L("QuickSearchDesign.CommandTrackerStyleToCompact"),
			QuickCommand.Help => L("QuickSearchDesign.CommandHelpTip"),
			QuickCommand.Settings => L("QuickSearchDesign.CommandSettingsTip"),
			QuickCommand.UntrackAll => (QuickArmed == (int)command) ? L("QuickSearchDesign.CommandUntrackAllArmed", TrackedGoals.Count) : L("QuickSearchDesign.CommandUntrackAllTip", TrackedGoals.Count),
			QuickCommand.BeforeNextBoss => L("QuickSearchDesign.CommandBeforeNextBossTip", (BookSteps.Next() > 0) ? BookSteps.Name(BookSteps.Next()) : L("Stages.NextBoss")),
			QuickCommand.NextBoss => L("QuickSearchDesign.CommandNextBossTip"),
			QuickCommand.CraftableNow => L("QuickSearchDesign.CommandCraftableNowTip"),
			QuickCommand.Favorites => L("QuickSearchDesign.CommandFavoritesTip"),
			_ => L("QuickSearchDesign.CommandSummonerTip")
		};
	}

	// The book's own key for it, "" when it has none.
	private static string QuickCommandShortcut(QuickCommand command)
	{
		return command switch
		{
			QuickCommand.QuickStack => L("QuickSearchDesign.KeyCtrlQ"),
			QuickCommand.Restock => L("QuickSearchDesign.KeyCtrlR"),
			QuickCommand.Refresh => L("Help.KeyF5"),
			QuickCommand.ClearFilters => L("Help.KeyCtrlBackspace"),
			QuickCommand.Help => L("Help.KeyF1"),
			QuickCommand.BeforeNextBoss => "Alt+5",
			QuickCommand.TabItems or QuickCommand.TabRecipes or QuickCommand.TabBestiary or QuickCommand.TabStorage or QuickCommand.TabRecent or QuickCommand.TabTracking => "Ctrl+" + (VisibleTabs().IndexOf(QuickCommandTab(command)) + 1),
			_ => ""
		};
	}

	private static int QuickCommandTab(QuickCommand command)
	{
		return command - QuickCommand.TabItems;
	}

	// A command that would do nothing now (no storage nearby, nothing tracked, no next boss).
	private static bool QuickCommandEnabled(QuickCommand command)
	{
		return command switch
		{
			QuickCommand.DepositAll => StorageButtonEnabled(StorageButton.DepositAll),
			QuickCommand.QuickStack => StorageButtonEnabled(StorageButton.QuickStack),
			QuickCommand.Restock => StorageButtonEnabled(StorageButton.Restock),
			QuickCommand.UntrackAll => TrackedGoals.Count > 0,
			QuickCommand.BeforeNextBoss or QuickCommand.NextBoss => QuickNextBossMob() >= 0 || command == QuickCommand.BeforeNextBoss,
			_ => true
		};
	}

	// The next boss's Bestiary entry, -1 when every boss is down.
	private static int QuickNextBossMob()
	{
		int next = BookSteps.Next();
		if (next <= 0)
		{
			return -1;
		}
		int npc = BookSteps.HeadNpc(next, out _);
		return (npc != 0) ? BookBestiary.IndexOf(npc) : (-1);
	}

	private static void RunQuickCommand(QuickCommand command)
	{
		if (!QuickCommandEnabled(command))
		{
			return;
		}
		BookCrafting.LastMessage = "";
		switch (command)
		{
			case QuickCommand.DepositAll:
				PressStorageButton(StorageButton.DepositAll);
				QuickActed();
				return;
			case QuickCommand.QuickStack:
				PressStorageButton(StorageButton.QuickStack);
				QuickActed();
				return;
			case QuickCommand.Restock:
				PressStorageButton(StorageButton.Restock);
				QuickActed();
				return;
			case QuickCommand.Refresh:
				RefreshView();
				QuickHitTerms = Array.Empty<string>();
				KeepQuickRows();
				return;
			case QuickCommand.TrackerStyle:
			{
				BookConfig config = ModContent.GetInstance<BookConfig>();
				config.ResourceOnlyTracker = !config.ResourceOnlyTracker;
				config.SaveChanges();
				BookCrafting.Say(config.ResourceOnlyTracker ? "QuickSearchDesign.TrackerCompact" : "QuickSearchDesign.TrackerCards");
				QuickActed();
				return;
			}
			case QuickCommand.UntrackAll:
				// Twice, as the Tracking tab's Untrack everything asks.
				if (QuickArmed != (int)command || Main.GameUpdateCount - QuickArmedAt > 180)
				{
					QuickArmed = (int)command;
					QuickArmedAt = Main.GameUpdateCount;
					return;
				}
				QuickArmed = -1;
				StopTracker();
				Page = 0;
				BookCrafting.Say("Common.NoLongerTracked");
				QuickActed();
				return;
			case QuickCommand.Settings:
				CloseQuickSearch();
				ModContent.GetInstance<BookConfig>().Open();
				return;
		}
		BeginQuickNavigation();
		switch (command)
		{
			case QuickCommand.ClearFilters:
				ResetFilters();
				break;
			case QuickCommand.Help:
				HelpOpen = true;
				break;
			case QuickCommand.BeforeNextBoss:
				if (StorageView || TrackingView)
				{
					SwitchTab(0);
				}
				SetStageShortcut(BookStageFilter.BeforeBoss);
				break;
			case QuickCommand.NextBoss:
			{
				int mob = QuickNextBossMob();
				ShowMob(mob, BookBestiary.Mobs[mob].NetId);
				MobStatsMode = false;
				break;
			}
			case QuickCommand.CraftableNow:
				QuickTab(0, "");
				CraftableOnly = true;
				ApplyFilters();
				break;
			case QuickCommand.Favorites:
				QuickTab(0, "");
				FavoritesOnly = true;
				ApplyFilters();
				break;
			case QuickCommand.Summoner:
				QuickTab(0, "");
				ClassFilter = BookClass.Summon;
				ApplyFilters();
				break;
			default:
				SwitchTab(QuickCommandTab(command));
				break;
		}
	}

	// A category's short forms the players use ("buff" for Buff potions, «баф» for «Зелья усилений»), comma-separated in
	// both languages; "" for the others.
	private static string QuickSynonyms(string category)
	{
		return category switch
		{
			"buffs" => L("QuickSearchDesign.SynonymsBuffs"),
			"potions" => L("QuickSearchDesign.SynonymsPotions"),
			"healing" => L("QuickSearchDesign.SynonymsHealing"),
			"wings" => L("QuickSearchDesign.SynonymsWings"),
			"summon" => L("QuickSearchDesign.SynonymsMinions"),
			"whips" => L("QuickSearchDesign.SynonymsWhips"),
			"sentries" => L("QuickSearchDesign.SynonymsSentries"),
			"stations" => L("QuickSearchDesign.SynonymsStations"),
			"ores" => L("QuickSearchDesign.SynonymsOres"),
			"bars" => L("QuickSearchDesign.SynonymsBars"),
			"accessories" => L("QuickSearchDesign.SynonymsAccessories"),
			"armor" => L("QuickSearchDesign.SynonymsArmor"),
			"hooks" => L("QuickSearchDesign.SynonymsHooks"),
			"mounts" => L("QuickSearchDesign.SynonymsMounts"),
			"pets" => L("QuickSearchDesign.SynonymsPets"),
			"summons" => L("QuickSearchDesign.SynonymsBossSummons"),
			"bags" => L("QuickSearchDesign.SynonymsBags"),
			"picks" => L("QuickSearchDesign.SynonymsPicks"),
			"food" => L("QuickSearchDesign.SynonymsFood"),
			"ammo" => L("QuickSearchDesign.SynonymsAmmo"),
			_ => ""
		};
	}
}
