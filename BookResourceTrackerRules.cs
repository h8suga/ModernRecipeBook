using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook;

internal static class BookResourceTrackerRules
{
	internal const int PageSize = 8;

	internal static string Key(bool storage, IEnumerable<int> types)
	{
		return (storage ? "storage:" : "inventory:") + BookTrackerRules.MaterialKey(types);
	}

	internal static BookResourceNeed[] Merge(IEnumerable<BookResourceNeed> needs)
	{
		return (from n in needs
			group n by Key(n.Storage, n.Types) into g
			select new BookResourceNeed(g.First().Storage, g.Sum((BookResourceNeed n) => Math.Max(0L, n.Quantity)), (from t in g.First().Types.Distinct()
				orderby t
				select t).ToArray())).ToArray();
	}

	internal static BookResourceProgress[] Assess(IReadOnlyList<BookResourceNeed> needs, IReadOnlyDictionary<int, long> inventory, IReadOnlyDictionary<int, long> storage, bool storageUnknown, IReadOnlySet<int> infiniteStorage)
	{
		BookResourceProgress[] result = new BookResourceProgress[needs.Count];
		bool[] array = new bool[2] { false, true };
		foreach (bool pool in array)
		{
			int[] indexes = (from index in Enumerable.Range(0, needs.Count)
				where needs[index].Storage == pool
				select index).ToArray();
			IReadOnlyDictionary<int, long> counts = (pool ? storage : inventory);
			bool unknown = pool & storageUnknown;
			long[] allocated = (unknown ? new long[indexes.Length] : BookBatchPlanner.AllocateMaterials(indexes.Select((int index) => needs[index].Quantity).ToArray(), indexes.Select((int index) => needs[index].Types).ToArray(), counts));
			for (int n = 0; n < indexes.Length; n++)
			{
				int i2 = indexes[n];
				long owned = needs[i2].Types.Distinct().Sum((int t) => Math.Max(0L, counts.GetValueOrDefault(t)));
				result[i2] = new BookResourceProgress(needs[i2], owned, allocated[n], unknown, pool && !unknown && needs[i2].Types.Any(infiniteStorage.Contains));
			}
		}
		return result;
	}

	internal static BookResourceProgress[] AssessGoal(IReadOnlyList<BookResourceNeed> needs, IReadOnlyDictionary<int, long> counts, bool unknown, IReadOnlySet<int> infiniteStorage)
	{
		Dictionary<int, long> available = counts.ToDictionary((KeyValuePair<int, long> p) => p.Key, (KeyValuePair<int, long> p) => Math.Max(0L, p.Value));
		long demand = needs.Sum((BookResourceNeed n) => n.Quantity);
		foreach (int type in infiniteStorage)
		{
			available[type] = demand;
		}
		BookResourceProgress[] rows = Assess(needs, available, available, unknown, infiniteStorage);
		if (!unknown)
		{
			return rows;
		}
		return rows.Select((BookResourceProgress r) => r with
		{
			Unknown = true
		}).ToArray();
	}

	internal static bool Ready(IReadOnlyList<BookResourceProgress> rows, bool hasGoals, bool sourcesKnown)
	{
		if (hasGoals & sourcesKnown)
		{
			return rows.All((BookResourceProgress r) => r.Met);
		}
		return false;
	}

	internal static int PageCount(int rows)
	{
		return Math.Max(1, (rows + 8 - 1) / 8);
	}

	internal static int Height(int rows)
	{
		return 36 + Math.Min(8, Math.Max(1, rows)) * 26 + ((rows > 8) ? 24 : 0);
	}
}
