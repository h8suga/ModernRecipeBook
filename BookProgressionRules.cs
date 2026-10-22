using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook;

internal static class BookProgressionRules
{
	internal sealed record Route(int Key, int Output, BookStage Floor, int[][] Ingredients, bool Uncertain = false);

	internal static Dictionary<string, int[]> BuildNameIndex(IEnumerable<KeyValuePair<string, int>> entries)
	{
		return entries.Where((KeyValuePair<string, int> e) => !string.IsNullOrEmpty(e.Key)).GroupBy((KeyValuePair<string, int> e) => e.Key, StringComparer.Ordinal).ToDictionary((IGrouping<string, KeyValuePair<string, int>> g) => g.Key, (IGrouping<string, KeyValuePair<string, int>> g) => g.Select((KeyValuePair<string, int> e) => e.Value).Distinct().ToArray(), StringComparer.Ordinal);
	}

	internal static bool Matches(BookStageFilter filter, BookStage stage)
	{
		if (filter != BookStageFilter.All)
		{
			return stage == (BookStage)(filter - 1);
		}
		return true;
	}

	internal static bool MatchesChain(BookStageFilter filter, BookStage stage, BookStage minimum)
	{
		if (!Matches(filter, stage))
		{
			if (stage == BookStage.Unknown)
			{
				if (filter != BookStageFilter.All)
				{
					return (int)(filter - 1) >= (int)minimum;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	internal static BookStage MinimumStage(Route route, IReadOnlyDictionary<int, BookStage> items, IReadOnlyDictionary<int, BookStage> floors)
	{
		int result = Math.Max(0, Math.Max((int)route.Floor, (int)(floors.TryGetValue(route.Output, out var floor) ? floor : BookStage.PreHardmode)));
		int[][] ingredients = route.Ingredients;
		foreach (int[] group in ingredients)
		{
			int minimum = ((group.Length != 0) ? group.Min((int type) => (int)((items.TryGetValue(type, out var value) && value != BookStage.Unknown) ? value : BookStage.PreHardmode)) : 0);
			result = Math.Max(result, minimum);
		}
		return (BookStage)result;
	}

	internal static (Dictionary<int, BookStage> Items, Dictionary<int, BookStage> Recipes) Resolve(IReadOnlyDictionary<int, BookStage> seeds, IReadOnlyDictionary<int, BookStage> floors, IReadOnlyList<Route> routes)
	{
		Dictionary<int, BookStage> items = new Dictionary<int, BookStage>(seeds);
		Dictionary<int, BookStage> recipes = new Dictionary<int, BookStage>();
		bool changed;
		do
		{
			changed = false;
			foreach (Route route in routes)
			{
				BookStage next = Evaluate(route);
				BookStage old = Read(route.Output);
				if (next != BookStage.Unknown && (old == BookStage.Unknown || next < old))
				{
					items[route.Output] = next;
					changed = true;
				}
			}
		}
		while (changed);
		foreach (Route route2 in routes)
		{
			recipes[route2.Key] = Evaluate(route2);
		}
		return (Items: items, Recipes: recipes);
		BookStage Evaluate(Route route3)
		{
			bool unknown = route3.Uncertain || route3.Floor == BookStage.Unknown;
			BookStage stage = (BookStage)Math.Max(0, Math.Max((int)route3.Floor, (int)Floor(route3.Output)));
			int[][] ingredients = route3.Ingredients;
			for (int i = 0; i < ingredients.Length; i++)
			{
				BookStage[] known = (from s in ingredients[i].Select(Read)
					where s != BookStage.Unknown
					select s).ToArray();
				if (known.Length == 0)
				{
					unknown = true;
				}
				else
				{
					stage = (BookStage)Math.Max((int)stage, known.Min((BookStage s) => (int)s));
				}
			}
			if (!unknown || stage == BookStage.PostHardmode)
			{
				return stage;
			}
			return BookStage.Unknown;
		}
		BookStage Floor(int item)
		{
			if (!floors.TryGetValue(item, out var s))
			{
				return BookStage.PreHardmode;
			}
			return s;
		}
		BookStage Read(int item)
		{
			if (!items.TryGetValue(item, out var s))
			{
				return BookStage.Unknown;
			}
			return s;
		}
	}
}
