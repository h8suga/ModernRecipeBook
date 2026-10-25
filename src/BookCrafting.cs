using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

using static ModernRecipeBook.BookText;

namespace ModernRecipeBook;

internal static class BookCrafting
{
	private static readonly HashSet<int> Available = new HashSet<int>();

	private static readonly HashSet<int> NextAvailable = new HashSet<int>();

	private static int Ticks;

	private static int EvaluatedVersion = -1;

	internal static int AvailabilityVersion { get; private set; }

	// Per recipe, vanilla's ingredient keys (Recipe.CreateRequiredItemQuickLookups): the type, or ~group for the last
	// accepted group that holds it. Recipes do not change after loading.
	private static int[][] Lookups = Array.Empty<int[]>();

	private static readonly Dictionary<int, long> GroupTotals = new Dictionary<int, long>();

	private static (int Recipe, int Batches, int Version, string Text) SourcesCache = (-1, 0, -1, "");

	private static bool PlayerReady
	{
		get
		{
			if (Main.LocalPlayer.active && !Main.LocalPlayer.dead && !Main.LocalPlayer.ghost && Main.LocalPlayer.ItemTimeIsZero && Main.LocalPlayer.itemAnimation == 0)
			{
				return !Main.LocalPlayer.HasLockedInventory();
			}
			return false;
		}
	}

	internal static void Clear()
	{
		Available.Clear();
		NextAvailable.Clear();
		Ticks = 0;
		EvaluatedVersion = -1;
		LastMessage = "";
		AvailabilityVersion++;
		SourcesCache = (-1, 0, -1, "");
	}

	// Craftable recipes, every 20 ticks while the book is open (conditions such as time or biome change on their own)
	// and at once when the sources changed.
	internal static void Refresh(bool force = false)
	{
		if (!force && Ticks++ % 20 != 0 && EvaluatedVersion == BookSources.Version)
		{
			return;
		}
		EvaluatedVersion = BookSources.Version;
		NextAvailable.Clear();
		Player player = Main.LocalPlayer;
		if (!Main.gameMenu && !Main.dedServ && player.active && !player.dead && Main.guideItem.IsAir)
		{
			player.AdjTiles();
			Collect(player, NextAvailable);
		}
		if (BookRules.AvailabilityChanged(Available, NextAvailable))
		{
			Available.Clear();
			Available.UnionWith(NextAvailable);
			AvailabilityVersion++;
		}
	}

	// Vanilla's rule (Recipe.FindRecipes) over the book's sources: every required tile in adjTile, each ingredient key
	// covered, every condition met. With the feature off the sources are vanilla's own, so the result is vanilla's.
	private static void Collect(Player player, HashSet<int> into)
	{
		GroupTotals.Clear();
		bool scoped = BookMagicStorage.BeginStations(player, BookSources.CraftStations);
		try
		{
			// The footer's active stations read this very state.
			RecipeBookSystem.CaptureActiveStations(player, scoped);
			for (int i = 0; i < Recipe.numRecipes; i++)
			{
				Recipe recipe = Main.recipe[i];
				if (!recipe.Disabled && HasTiles(player, recipe) && HasItems(recipe, null) && RecipeLoader.RecipeAvailable(recipe))
				{
					into.Add(i);
				}
			}
		}
		finally
		{
			if (scoped)
			{
				BookMagicStorage.EndStations(player);
			}
		}
	}

	private static bool HasTiles(Player player, Recipe recipe)
	{
		for (int i = 0; i < recipe.requiredTile.Count && recipe.requiredTile[i] != -1; i++)
		{
			if (!player.adjTile[recipe.requiredTile[i]])
			{
				return false;
			}
		}
		return true;
	}

	private static int[] Lookup(Recipe recipe)
	{
		int index = recipe.RecipeIndex;
		if (index >= Lookups.Length)
		{
			Array.Resize(ref Lookups, Math.Max(index + 1, Recipe.numRecipes));
		}
		int[] keys = Lookups[index];
		if (keys != null)
		{
			return keys;
		}
		keys = new int[recipe.requiredItem.Count];
		for (int j = 0; j < keys.Length; j++)
		{
			int type = recipe.requiredItem[j].type;
			keys[j] = type;
			foreach (int group in recipe.acceptedGroups)
			{
				if (RecipeGroup.recipeGroups.TryGetValue(group, out var recipeGroup) && recipeGroup.ValidItems.Contains(type))
				{
					keys[j] = ~group;
				}
			}
		}
		Lookups[index] = keys;
		return keys;
	}

	// `local` null: the whole source set; otherwise counts of what Recipe.Create itself can take.
	private static bool HasItems(Recipe recipe, Dictionary<int, long> local)
	{
		int[] keys = Lookup(recipe);
		for (int j = 0; j < keys.Length; j++)
		{
			int stack = recipe.requiredItem[j].stack;
			long have;
			if (keys[j] >= 0)
			{
				have = (local == null) ? BookSources.Quantity(keys[j]) : local.GetValueOrDefault(keys[j]);
			}
			else
			{
				have = GroupTotal(~keys[j], local);
			}
			if (have < stack)
			{
				return false;
			}
		}
		return true;
	}

	private static long GroupTotal(int group, Dictionary<int, long> local)
	{
		if (local == null && GroupTotals.TryGetValue(group, out var cached))
		{
			return cached;
		}
		long total = 0;
		if (RecipeGroup.recipeGroups.TryGetValue(group, out var recipeGroup))
		{
			foreach (int type in recipeGroup.ValidItems)
			{
				total += (local == null) ? BookSources.Quantity(type) : local.GetValueOrDefault(type);
			}
		}
		if (local == null)
		{
			GroupTotals[group] = total;
		}
		return total;
	}

	internal static bool IsAvailable(Recipe recipe)
	{
		if (recipe != null && !recipe.Disabled)
		{
			return Available.Contains(recipe.RecipeIndex);
		}
		return false;
	}

	internal static bool CanCraft(Recipe recipe)
	{
		return BookRules.CraftAllowed(recipe != null && !recipe.Disabled, PlayerReady && !BookSources.CraftPending, Main.guideItem.IsAir, Main.mouseItem.IsAir, IsAvailable(recipe));
	}

	internal static string Reason(Recipe recipe)
	{
		return L(ReasonKey(recipe));
	}

	private static string ReasonKey(Recipe recipe)
	{
		if (recipe == null)
		{
			return "Crafting.NoCraftingRecipe";
		}
		if (BookSources.CraftPendingShown)
		{
			return "Crafting.GatheringMaterials";
		}
		if (!Main.guideItem.IsAir)
		{
			return "Crafting.EmptyTheGuideSlotFirst";
		}
		if (!Main.mouseItem.IsAir)
		{
			return "Crafting.ClearTheCursorFirst";
		}
		if (!PlayerReady)
		{
			return "Crafting.CantCraftRightNow";
		}
		if (!IsAvailable(recipe))
		{
			return "Crafting.MissingMaterialsStationOrCondition";
		}
		return "Crafting.ReadyToCraft";
	}

	// The Craft tooltip's second line: where a craft of this size takes its materials from, when not only from the
	// inventory ("From: inventory, 2 chests, Magic Storage"). Cached per recipe, size and sources version.
	internal static string Sources(Recipe recipe, int batches)
	{
		if (recipe == null || !BookSources.Enabled || !IsAvailable(recipe))
		{
			return "";
		}
		if (SourcesCache.Recipe == recipe.RecipeIndex && SourcesCache.Batches == batches && SourcesCache.Version == BookSources.Version)
		{
			return SourcesCache.Text;
		}
		Dictionary<int, long>[] plan = Plan(recipe, batches, out _);
		string text = (plan == null) ? "" : BookSources.CraftSources(plan[1], plan[2]);
		SourcesCache = (recipe.RecipeIndex, batches, BookSources.Version, text);
		return text;
	}

	// The book's needs for `batches` (each ingredient with every type Recipe.Create accepts for it, as the card rows
	// count), cut to what the sources can cover, split over the layers: [0] Recipe.Create's own sources, [1] banks to
	// move, [2] remote to pull.
	private static Dictionary<int, long>[] Plan(Recipe recipe, int batches, out int feasible)
	{
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
		int[] types = needs.SelectMany((BookBatchNeed n) => n.AcceptedTypes).Where((int t) => t > 0).Distinct().ToArray();
		feasible = BookBatchPlanner.MaxBatches(needs, BookSources.CountsFor(types), batches);
		if (feasible <= 0)
		{
			return null;
		}
		int count = feasible;
		return BookBatchPlanner.PlanLayers(needs.Select((BookBatchNeed n) => (long)n.Quantity * count).ToList(), needs.Select((BookBatchNeed n) => n.AcceptedTypes).ToList(), BookSources.Layers(types));
	}

	// A message keeps how to build it, so it reads in the language shown now.
	private static Func<string> Text = () => "";

	internal static string LastMessage
	{
		get
		{
			return Text();
		}
		set
		{
			string text = value ?? "";
			Text = () => text;
		}
	}

	internal static void Say(Func<string> text)
	{
		Text = text;
	}

	// The message of a localization key.
	internal static void Say(string key)
	{
		Text = () => L(key);
	}

	internal static void TryCraft(Recipe recipe, int batches = 1)
	{
		batches = Math.Clamp(batches, 1, 100);
		if (recipe == null)
		{
			Say(ReasonKey(null));
			return;
		}
		if (BookSources.CraftPending)
		{
			Say("Crafting.GatheringMaterials");
			return;
		}
		if (!CanCraft(recipe))
		{
			// A reason depends on the state, which moves on: the message keeps the reason of the moment.
			Say(ReasonKey(recipe));
			return;
		}
		int feasible = 0;
		Dictionary<int, long>[] plan = BookSources.Enabled ? Plan(recipe, batches, out feasible) : null;
		if (plan == null || (plan[1].Count == 0 && plan[2].Count == 0))
		{
			// The inventory, the open container and a carried Void Bag cover it: vanilla crafting as before.
			CraftNow(recipe, batches, batches);
			return;
		}
		int shortage = BookSourceRules.Shortage(InventorySlots(), Incoming(recipe, feasible, plan));
		if (shortage > 0)
		{
			Say(() => L("Crafting.InventoryFullFreeSlots", shortage));
			return;
		}
		if (!BookSources.MoveBanks(plan[1]))
		{
			Say("Crafting.MaterialsInInventoryInventoryFull");
			return;
		}
		if (plan[2].Count == 0)
		{
			CraftNow(recipe, feasible, batches);
			return;
		}
		LastMessage = "";
		BookSources.RequestCraft(recipe, batches, feasible, plan[2]);
	}

	private static List<BookSourceRules.Slot> InventorySlots()
	{
		List<BookSourceRules.Slot> slots = new List<BookSourceRules.Slot>(50);
		for (int i = 0; i < 50; i++)
		{
			Item item = Main.LocalPlayer.inventory[i];
			slots.Add((item == null || item.IsAir) ? default(BookSourceRules.Slot) : new BookSourceRules.Slot(item.type, item.prefix, item.stack, item.maxStack));
		}
		return slots;
	}

	// The moved and pulled materials plus every result of the craft: results always go to the inventory.
	private static List<BookSourceRules.Incoming> Incoming(Recipe recipe, int batches, Dictionary<int, long>[] plan)
	{
		List<BookSourceRules.Incoming> incoming = new List<BookSourceRules.Incoming>();
		for (int layer = 1; layer < plan.Length; layer++)
		{
			foreach (KeyValuePair<int, long> pair in plan[layer])
			{
				incoming.Add(new BookSourceRules.Incoming(pair.Key, 0, pair.Value, ContentSamples.ItemsByType[pair.Key].maxStack));
			}
		}
		incoming.Add(new BookSourceRules.Incoming(recipe.createItem.type, 0, (long)recipe.createItem.stack * batches, recipe.createItem.maxStack));
		return incoming;
	}

	// After a pull for a craft arrived (at once in single player).
	internal static void ContinueCraft(Recipe recipe, int requested, int batches, BookNearbyServer.Status status, long got)
	{
		if (recipe == null)
		{
			if (got > 0)
			{
				Say("Crafting.MaterialsAreInTheInventory");
			}
			else
			{
				LastMessage = "";
			}
			return;
		}
		if (got == 0 && status is BookNearbyServer.Status.Off or BookNearbyServer.Status.Busy or BookNearbyServer.Status.Invalid)
		{
			switch (status)
			{
			case BookNearbyServer.Status.Off:
				Say("Crafting.NearbyStorageIsOff");
				break;
			case BookNearbyServer.Status.Busy:
				Say("Crafting.TooFastWaitAMoment");
				break;
			default:
				Say("Crafting.CraftCancelledConditionsChanged");
				break;
			}
			return;
		}
		Refresh(force: true);
		CraftNow(recipe, batches, requested, pulled: got > 0);
	}

	private static void CraftNow(Recipe recipe, int batches, int requested, bool pulled = false)
	{
		Player player = Main.LocalPlayer;
		int crafted = 0;
		// Localization keys of why the run stopped, or of why nothing could be made.
		string stop = null;
		string failure = null;
		bool scoped = BookMagicStorage.BeginStations(player, BookSources.CraftStations);
		try
		{
			for (int batch = 0; batch < batches; batch++)
			{
				if (!SafeForNextBatch(recipe, player))
				{
					if (crafted == 0)
					{
						failure = ReasonKey(recipe);
						break;
					}
					stop = "Crafting.CraftingStoppedSomethingChanged";
					break;
				}
				if (!CanCraftHere(player, recipe))
				{
					if (crafted == 0)
					{
						failure = "Crafting.MissingMaterialsStationOrCondition";
						break;
					}
					stop = "Crafting.StoppedOutOfMaterials";
					break;
				}
				Main.CraftItem(recipe);
				if (Main.mouseItem.IsAir || Main.mouseItem.type != recipe.createItem.type)
				{
					stop = "Crafting.CraftingFailed";
					break;
				}
				crafted++;
				Main.mouseItem = player.GetItem(Main.myPlayer, Main.mouseItem, GetItemSettings.InventoryUIToInventorySettingsShowAsNew);
				if (!Main.mouseItem.IsAir)
				{
					Main.playerInventory = true;
					stop = "Crafting.InventoryFullResultOnCursor";
					break;
				}
			}
		}
		finally
		{
			if (scoped)
			{
				BookMagicStorage.EndStations(player);
			}
		}
		// Recipe.Create refreshed vanilla's list with the Crafting Interface stations applied; redo it without them.
		if (scoped)
		{
			Recipe.FindRecipes();
		}
		RecipeBookSystem.InvalidateBatchEstimate();
		BookSources.Refresh(force: true);
		Refresh(force: true);
		if (crafted == 0)
		{
			string reason = failure ?? stop ?? ReasonKey(recipe);
			Say(() => pulled ? L("Crafting.CraftStoppedMaterialsInInventory", L(reason)) : L(reason));
			return;
		}
		if (stop == null && crafted < requested)
		{
			stop = "Crafting.StoppedOutOfMaterials";
		}
		string why = stop;
		int made = crafted;
		Say(() =>
		{
			string completion = (made == 1 && requested == 1) ? L("Crafting.Crafted", recipe.createItem.Name) : L("Crafting.CraftedSome", made, requested, recipe.createItem.Name);
			return (why != null) ? (completion + " · " + L(why)) : completion;
		});
	}

	// Before each batch: what Recipe.Create itself consumes from must cover it, at the stations in effect.
	private static bool CanCraftHere(Player player, Recipe recipe)
	{
		Dictionary<int, long> local = new Dictionary<int, long>();
		HashSet<Item> seen = new HashSet<Item>(ReferenceEqualityComparer.Instance);
		foreach (Item item in Materials())
		{
			if (item != null && !item.IsAir && seen.Add(item))
			{
				local[item.type] = local.GetValueOrDefault(item.type) + item.stack;
			}
		}
		return HasTiles(player, recipe) && HasItems(recipe, local) && RecipeLoader.RecipeAvailable(recipe);
	}

	private static bool SafeForNextBatch(Recipe recipe, Player player)
	{
		int index = recipe?.RecipeIndex ?? (-1);
		if (!Main.gameMenu && !Main.dedServ && player == Main.LocalPlayer && player.active && !player.dead && !player.ghost && player.ItemTimeIsZero && player.itemAnimation == 0 && !player.HasLockedInventory() && Main.guideItem.IsAir && Main.mouseItem.IsAir && recipe != null && !recipe.Disabled && index >= 0 && index < Recipe.numRecipes)
		{
			return Main.recipe[index] == recipe;
		}
		return false;
	}

	// What Recipe.Create consumes from, in its order (Recipe.cs:245-340).
	internal static IEnumerable<Item> Materials()
	{
		Player player = Main.LocalPlayer;
		for (int i = 0; i < 58; i++)
		{
			yield return player.inventory[i];
		}
		if (player.useVoidBag() && player.chest != -5)
		{
			foreach (Item item in player.bank4.item)
			{
				yield return item;
			}
		}
		Item[] open = player.chest switch
		{
			-2 => player.bank.item,
			-3 => player.bank2.item,
			-4 => player.bank3.item,
			-5 => player.bank4.item,
			_ => (player.chest >= 0 && player.chest < Main.chest.Length) ? Main.chest[player.chest]?.item : null,
		};
		if (open != null)
		{
			foreach (Item item in open.Take(40))
			{
				yield return item;
			}
		}
		foreach (var (items, _) in PlayerLoader.GetModdedCraftingMaterials(player))
		{
			foreach (Item item in items)
			{
				yield return item;
			}
		}
	}

	// Stand: stand.ps1 c call p=ModernRecipeBook.BookCrafting.CraftRecipe a=<recipe index> a=<batches>
	internal static string CraftRecipe(int index, int batches)
	{
		TryCraft(Main.recipe[index], batches);
		return LastMessage;
	}

	// Stand check (stand.ps1 c call p=ModernRecipeBook.BookCrafting.VanillaDiff): the book's craftable set against
	// vanilla's Recipe.FindRecipes over all recipes. With the feature off, or nothing in range, it must be diff=0.
	internal static string VanillaDiff()
	{
		Player player = Main.LocalPlayer;
		if (!Main.guideItem.IsAir)
		{
			return "empty the Guide slot first";
		}
		player.AdjTiles();
		Recipe.FindRecipes();
		HashSet<int> vanilla = new HashSet<int>();
		for (int i = 0; i < Main.numAvailableRecipes; i++)
		{
			vanilla.Add(Main.availableRecipe[i]);
		}
		BookSources.Refresh(force: true);
		HashSet<int> book = new HashSet<int>();
		Collect(player, book);
		List<int> onlyVanilla = vanilla.Where((int r) => !book.Contains(r)).ToList();
		List<int> onlyBook = book.Where((int r) => !vanilla.Contains(r)).ToList();
		return $"diff={onlyVanilla.Count + onlyBook.Count} vanilla={vanilla.Count} book={book.Count} recipes={Recipe.numRecipes} onlyVanilla=[{string.Join(",", onlyVanilla.Take(8))}] onlyBook=[{string.Join(",", onlyBook.Take(8))}]";
	}
}
