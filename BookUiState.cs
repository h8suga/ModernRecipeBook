using System;
using System.Collections.Generic;

namespace ModernRecipeBook;

internal sealed class BookUiState
{
	// 5: the global class filter.
	internal const int CurrentVersion = 5;

	// Search, mod, category, favorites and sort belong to one tab and stay until the player changes them;
	// the stage filter is shared by every tab.
	internal sealed class TabFilters
	{
		internal string Query = "";

		internal string ModFilter = "";

		internal string CategoryKey = "all";

		internal bool FavoritesOnly;

		internal BookSortMode SortMode;

		internal bool SortDescending;

		internal TabFilters Sanitize()
		{
			string key = Clean(CategoryKey, 128);
			return new TabFilters
			{
				Query = Clean(Query, 100),
				ModFilter = Clean(ModFilter, 128),
				CategoryKey = ((key.Length > 0) ? key : "all"),
				FavoritesOnly = FavoritesOnly,
				SortMode = (Enum.IsDefined(typeof(BookSortMode), SortMode) ? SortMode : BookSortMode.Name),
				SortDescending = SortDescending
			};
		}
	}

	internal static readonly string[] FilterTabs = new string[4] { "items", "recipes", "storage", "recent" };

	// Filters of every tab; the top-level Query..SortDescending below are the open tab's (and the only set
	// version 3 saves had).
	internal Dictionary<string, TabFilters> Tabs = new Dictionary<string, TabFilters>(StringComparer.Ordinal);

	internal string LastTab = "items";

	internal string Query = "";

	internal string ModFilter = "";

	internal string CategoryKey = "all";


	internal int StageFilter;

	// Global like the stage filter: one BookClass value, 0 = all classes.
	internal int ClassFilter;

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

		bookUiState.StageFilter = Math.Clamp(StageFilter, 0, 3);
		bookUiState.ClassFilter = ((Array.IndexOf(BookItemClasses.Buttons, (BookClass)ClassFilter) >= 0) ? ClassFilter : 0);
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
