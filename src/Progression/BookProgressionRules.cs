using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Progression;

// Steps are BookSteps indexes: 0 the start, k after boss k, -1 unknown.
internal static class BookProgressionRules
{
	// Source names where the route comes from for the audit ("drop:Bag", recipes leave it null).
	internal sealed record Route(int Key, int Output, int Floor, int[][] Ingredients, bool Uncertain = false, string Source = null);

	internal static Dictionary<string, int[]> BuildNameIndex(IEnumerable<KeyValuePair<string, int>> entries)
	{
		return entries.Where((KeyValuePair<string, int> e) => !string.IsNullOrEmpty(e.Key)).GroupBy((KeyValuePair<string, int> e) => e.Key, StringComparer.Ordinal).ToDictionary((IGrouping<string, KeyValuePair<string, int>> g) => g.Key, (IGrouping<string, KeyValuePair<string, int>> g) => g.Select((KeyValuePair<string, int> e) => e.Value).Distinct().ToArray(), StringComparer.Ordinal);
	}

	// The list filter: everything; one stage's steps; everything below a boss's step ("before" is that step); or
	// what the model could not place.
	internal static bool Matches(BookStageFilter filter, int step, int before)
	{
		return filter switch
		{
			BookStageFilter.All => true,
			BookStageFilter.BeforeBoss => step >= 0 && step < before,
			BookStageFilter.Unknown => step < 0,
			_ => step >= 0 && BookSteps.StageOf(step) == (BookStage)(filter - 1)
		};
	}

	// Used In: a recipe the model could not place still shows when what it takes at least is no later than the filter.
	internal static bool MatchesChain(BookStageFilter filter, int step, int minimum, int before)
	{
		if (Matches(filter, step, before))
		{
			return true;
		}
		if (step >= 0)
		{
			return false;
		}
		return filter switch
		{
			BookStageFilter.BeforeBoss => minimum < before,
			BookStageFilter.PreHardmode or BookStageFilter.Hardmode or BookStageFilter.PostHardmode => (int)(filter - 1) >= (int)BookSteps.StageOf(minimum),
			_ => true
		};
	}

	internal static int MinimumStep(Route route, IReadOnlyDictionary<int, int> items, IReadOnlyDictionary<int, int> floors)
	{
		int result = Math.Max(0, Math.Max(route.Floor, floors.TryGetValue(route.Output, out var floor) ? floor : 0));
		foreach (int[] group in route.Ingredients)
		{
			int minimum = (group.Length != 0) ? group.Min((int type) => (items.TryGetValue(type, out var value) && value >= 0) ? value : 0) : 0;
			result = Math.Max(result, minimum);
		}
		return result;
	}

	// Least fixed point from the seeds: a route gives its output the latest of its floor and, per ingredient group, the
	// earliest member. via: for every item a route placed, that route (the rest keep their seed).
	internal static (Dictionary<int, int> Items, Dictionary<int, int> Recipes) Resolve(IReadOnlyDictionary<int, int> seeds, IReadOnlyDictionary<int, int> floors, IReadOnlyList<Route> routes, Dictionary<int, Route> via = null)
	{
		Dictionary<int, int> items = new Dictionary<int, int>(seeds);
		Dictionary<int, int> recipes = new Dictionary<int, int>();
		// Items placed only by a lower bound (see Evaluate): any real value replaces them.
		HashSet<int> bounded = new HashSet<int>();
		via?.Clear();
		bool changed;
		do
		{
			changed = false;
			foreach (Route route in routes)
			{
				int next = Evaluate(route, out bool bound);
				int old = Read(route.Output);
				bool wasBound = bounded.Contains(route.Output);
				if (next >= 0 && (old < 0 || (bound ? (wasBound && next < old) : (next < old || wasBound))))
				{
					items[route.Output] = next;
					if (bound)
					{
						bounded.Add(route.Output);
					}
					else
					{
						bounded.Remove(route.Output);
					}
					if (via != null)
					{
						via[route.Output] = route;
					}
					changed = true;
				}
			}
		}
		while (changed);
		foreach (Route route2 in routes)
		{
			recipes[route2.Key] = Evaluate(route2, out _);
		}
		return (Items: items, Recipes: recipes);
		int Evaluate(Route route3, out bool bound)
		{
			bound = false;
			bool unknown = route3.Uncertain || route3.Floor < 0;
			int step = Math.Max(0, Math.Max(route3.Floor, Floor(route3.Output)));
			foreach (int[] group in route3.Ingredients)
			{
				int best = -1;
				foreach (int type in group)
				{
					int s = Read(type);
					if (s >= 0 && (best < 0 || s < best))
					{
						best = s;
					}
				}
				if (best < 0)
				{
					unknown = true;
				}
				else
				{
					step = Math.Max(step, best);
				}
			}
			// Nothing comes later than post-Moon Lord, so a route already there is placed even with a part unknown, as a
			// lower bound that only stands while nothing else places the item.
			if (!unknown)
			{
				return step;
			}
			bound = BookSteps.StageOf(step) == BookStage.PostHardmode;
			return bound ? step : -1;
		}
		int Floor(int item)
		{
			return floors.TryGetValue(item, out var s) ? s : 0;
		}
		int Read(int item)
		{
			return items.TryGetValue(item, out var s) ? s : -1;
		}
	}
}
