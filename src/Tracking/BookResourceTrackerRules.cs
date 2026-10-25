using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Tracking;

internal static class BookResourceTrackerRules
{
	internal static string Key(IEnumerable<int> types)
	{
		return BookTrackerRules.MaterialKey(types);
	}

	internal static BookResourceNeed[] Merge(IEnumerable<BookResourceNeed> needs)
	{
		return (from n in needs
			group n by Key(n.Types) into g
			select new BookResourceNeed(g.Sum((BookResourceNeed n) => Math.Max(0L, n.Quantity)), (from t in g.First().Types.Distinct()
				orderby t
				select t).ToArray())).ToArray();
	}

	// One pool: every source the book counts (docs/nearby-storage.md §2.3).
	internal static BookResourceProgress[] Assess(IReadOnlyList<BookResourceNeed> needs, IReadOnlyDictionary<int, long> counts, bool unknown, IReadOnlySet<int> infinite)
	{
		BookResourceProgress[] result = new BookResourceProgress[needs.Count];
		long[] allocated = unknown ? new long[needs.Count] : BookBatchPlanner.AllocateMaterials(needs.Select((BookResourceNeed n) => n.Quantity).ToArray(), needs.Select((BookResourceNeed n) => n.Types).ToArray(), counts);
		for (int i = 0; i < needs.Count; i++)
		{
			long owned = needs[i].Types.Distinct().Sum((int t) => Math.Max(0L, counts.GetValueOrDefault(t)));
			result[i] = new BookResourceProgress(needs[i], owned, allocated[i], unknown, !unknown && needs[i].Types.Any(infinite.Contains));
		}
		return result;
	}

	internal static BookResourceProgress[] AssessGoal(IReadOnlyList<BookResourceNeed> needs, IReadOnlyDictionary<int, long> counts, bool unknown, IReadOnlySet<int> infinite)
	{
		Dictionary<int, long> available = counts.ToDictionary((KeyValuePair<int, long> p) => p.Key, (KeyValuePair<int, long> p) => Math.Max(0L, p.Value));
		long demand = needs.Sum((BookResourceNeed n) => n.Quantity);
		foreach (int type in infinite)
		{
			available[type] = demand;
		}
		return Assess(needs, available, unknown, infinite);
	}

	internal static bool Ready(IReadOnlyList<BookResourceProgress> rows, bool hasGoals, bool sourcesKnown)
	{
		if (hasGoals & sourcesKnown)
		{
			return rows.All((BookResourceProgress r) => r.Met);
		}
		return false;
	}
}
