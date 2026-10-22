using System;
using System.Collections.Generic;

namespace ModernRecipeBook;

internal sealed class BookStorageSnapshot
{
	private Dictionary<int, int> Counts = new Dictionary<int, int>();

	private HashSet<int> Infinite = new HashSet<int>();

	private bool AllInfinite;

	internal bool HasValue { get; private set; }

	internal void Publish(IEnumerable<(int Type, int Count)> counts, IEnumerable<int> infinite, bool allInfinite)
	{
		Dictionary<int, int> next = new Dictionary<int, int>();
		foreach (var count2 in counts)
		{
			int type = count2.Type;
			int count = count2.Count;
			next[type] = Math.Max(0, count);
		}
		HashSet<int> nextInfinite = new HashSet<int>(infinite);
		Counts = next;
		Infinite = nextInfinite;
		AllInfinite = allInfinite;
		HasValue = true;
	}

	internal int Quantity(IEnumerable<int> types)
	{
		if (!HasValue)
		{
			return -1;
		}
		long total = 0L;
		foreach (int type in new HashSet<int>(types))
		{
			if (AllInfinite || Infinite.Contains(type))
			{
				return int.MaxValue;
			}
			if (Counts.TryGetValue(type, out var count))
			{
				total += count;
			}
			if (total >= int.MaxValue)
			{
				return int.MaxValue;
			}
		}
		return (int)total;
	}

	internal bool IsInfinite(IEnumerable<int> types)
	{
		if (!HasValue)
		{
			return false;
		}
		foreach (int type in new HashSet<int>(types))
		{
			if (AllInfinite || Infinite.Contains(type))
			{
				return true;
			}
		}
		return false;
	}

	internal void Clear()
	{
		Counts = new Dictionary<int, int>();
		Infinite = new HashSet<int>();
		bool allInfinite = (HasValue = false);
		AllInfinite = allInfinite;
	}
}
