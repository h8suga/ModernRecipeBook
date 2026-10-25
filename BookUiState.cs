using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook;

internal sealed class BookUiState
{
	// 5: the global class filter. 6: per-tab excluded stations. 7: Storage's own stage filter. 8: per-tab Craftable.
	// 9: "Before: <boss>" (stage filter 4, the boss's key; 5 is the unknown stage) and the global traits filter.
	internal const int CurrentVersion = 9;

	// Search, mod, category, favorites and sort belong to one tab and stay until the player changes them;
	// the stage filter is shared by every tab but Storage, which has its own.
	internal sealed class TabFilters
	{
		internal string Query = "";

		internal string ModFilter = "";

		internal string CategoryKey = "all";

		internal bool FavoritesOnly;

		// Bestiary tab: only the mobs the player has met.
		internal bool MetOnly;

		// Items and Recipes: only what the player can craft right now.
		internal bool CraftableOnly;

		internal BookSortMode SortMode;

		internal bool SortDescending;

		// Items and Recipes: stations the player lacks, as stable keys (Terraria/<tile id>, or Mod/TileName).
		internal string[] ExcludedStations = Array.Empty<string>();

		internal TabFilters Sanitize()
		{
			string key = Clean(CategoryKey, 128);
			return new TabFilters
			{
				Query = Clean(Query, 100),
				ModFilter = Clean(ModFilter, 128),
				CategoryKey = ((key.Length > 0) ? key : "all"),
				FavoritesOnly = FavoritesOnly,
				MetOnly = MetOnly,
				CraftableOnly = CraftableOnly,
				SortMode = (Enum.IsDefined(typeof(BookSortMode), SortMode) ? SortMode : BookSortMode.Name),
				SortDescending = SortDescending,
				ExcludedStations = (ExcludedStations ?? Array.Empty<string>()).Select((string station) => Clean(station, 128)).Where((string station) => station.Length > 0).Distinct(StringComparer.Ordinal).OrderBy((string station) => station, StringComparer.Ordinal).Take(64).ToArray()
			};
		}
	}

	// Indexed by tab number (Tracking, the last tab, has no filters).
	internal static readonly string[] FilterTabs = new string[5] { "items", "recipes", "bestiary", "storage", "recent" };

	// Filters of every tab; the top-level Query..SortDescending below are the open tab's (and the only set
	// version 3 saves had).
	internal Dictionary<string, TabFilters> Tabs = new Dictionary<string, TabFilters>(StringComparer.Ordinal);

	internal string LastTab = "items";

	internal string Query = "";

	internal string ModFilter = "";

	internal string CategoryKey = "all";


	// Every tab but Storage; Storage has its own.
	internal int StageFilter;

	internal int StorageStageFilter;

	// The boss "Before" names: a BookSteps key ("Terraria Golem") or "next", which follows the world.
	internal string StageBoss = "next";

	internal string StorageStageBoss = "next";

	// Global, for every tab: one BookClass value, 0 = all classes.
	internal int ClassFilter;

	// Global like the class: BookTrait flags (all must match), 0 = no trait asked.
	internal int TraitFilter;

	internal bool FavoritesOnly;

	internal bool UsedInMode;

	internal bool UsedInCraftableOnly;

	internal BookSortMode SortMode;

	internal bool SortDescending;

	internal int WindowScalePercent = 100;

	internal bool HasCustomPosition;

	internal float WindowX = 0.5f;

	internal float WindowY = 0.5f;

	internal bool TrackerHasCustomPosition;

	internal float TrackerX;

	internal float TrackerY = 1f;

	internal BookUiState Sanitize()
	{
		BookUiState bookUiState = new BookUiState();
		BookUiState bookUiState2 = bookUiState;
		bool flag;
		switch (LastTab)
		{
		case "items":
		case "recipes":
		case "bestiary":
		case "storage":
		case "recent":
		case "tracking":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		bookUiState2.LastTab = (flag ? LastTab : "items");
		bookUiState.Query = Clean(Query, 100);
		bookUiState.ModFilter = Clean(ModFilter, 128);
		string key = Clean(CategoryKey, 128);
		bookUiState.CategoryKey = ((key != null && key.Length > 0) ? key : "all");

		bookUiState.StageFilter = Math.Clamp(StageFilter, 0, 5);
		bookUiState.StorageStageFilter = Math.Clamp(StorageStageFilter, 0, 5);
		bookUiState.StageBoss = Clean(StageBoss, 128) is { Length: > 0 } boss ? boss : "next";
		bookUiState.StorageStageBoss = Clean(StorageStageBoss, 128) is { Length: > 0 } storageBoss ? storageBoss : "next";
		bookUiState.ClassFilter = ((Array.IndexOf(BookItemClasses.Buttons, (BookClass)ClassFilter) >= 0) ? ClassFilter : 0);
		bookUiState.TraitFilter = TraitFilter & BookItemTraits.KnownMask;
		bookUiState.FavoritesOnly = FavoritesOnly;
		bookUiState.UsedInMode = UsedInMode;
		bookUiState.UsedInCraftableOnly = UsedInCraftableOnly;
		bookUiState.SortMode = (Enum.IsDefined(typeof(BookSortMode), SortMode) ? SortMode : BookSortMode.Name);
		bookUiState.SortDescending = SortDescending;
		bookUiState.WindowScalePercent = Math.Clamp(WindowScalePercent, 70, 130);
		bookUiState.HasCustomPosition = HasCustomPosition;
		bookUiState.WindowX = NormalizePosition(WindowX);
		bookUiState.WindowY = NormalizePosition(WindowY);
		bookUiState.TrackerHasCustomPosition = TrackerHasCustomPosition;
		bookUiState.TrackerX = NormalizePosition(TrackerX);
		bookUiState.TrackerY = NormalizePosition(TrackerY);
		foreach (string tab in FilterTabs)
		{
			if (Tabs != null && Tabs.TryGetValue(tab, out var filters) && filters != null)
			{
				bookUiState.Tabs[tab] = filters.Sanitize();
			}
		}
		return bookUiState;
	}

	internal static float NormalizePosition(float value)
	{
		if (!float.IsFinite(value))
		{
			return 0.5f;
		}
		return Math.Clamp(value, 0f, 1f);
	}

	private static string Clean(string value, int maxLength)
	{
		value = (value ?? "").Replace("\r", "").Replace("\n", "");
		if (value.Length > maxLength)
		{
			return value.Substring(0, maxLength);
		}
		return value;
	}
}
