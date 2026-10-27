using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Recent;

internal sealed class BookRecentHistory
{
	internal const int Limit = 40;

	// Newest first: a recipe's key, how many of its item it made and when it last did (UTC ticks). Saves before 1.18 kept
	// the order alone: those read 0 until the next craft.
	private readonly List<(string Key, long Made, long Ticks)> Entries = new List<(string, long, long)>();

	internal int Count => Entries.Count;

	internal int Version { get; private set; }

	internal bool Record(string key, long made = 0)
	{
		if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
		{
			return false;
		}
		int existing = Entries.FindIndex(((string Key, long Made, long Ticks) e) => e.Key == key);
		(string, long, long) entry = (key, ((existing >= 0) ? Entries[existing].Made : 0) + made, DateTime.UtcNow.Ticks);
		if (existing == 0)
		{
			Entries[0] = entry;
			return false;
		}
		if (existing > 0)
		{
			Entries.RemoveAt(existing);
		}
		Entries.Insert(0, entry);
		if (Entries.Count > 40)
		{
			Entries.RemoveRange(40, Entries.Count - 40);
		}
		Version++;
		return true;
	}

	// The keys newest first, with what each made and when, by position (lists shorter than the keys read 0).
	internal void Load(IList<string> keys, IList<long> made, IList<long> ticks)
	{
		Entries.Clear();
		if (keys != null)
		{
			for (int i = 0; i < keys.Count; i++)
			{
				string key = keys[i];
				if (!string.IsNullOrWhiteSpace(key) && key.Length <= 128 && !Entries.Any(((string Key, long Made, long Ticks) e) => e.Key == key))
				{
					Entries.Add((key, (made != null && i < made.Count) ? made[i] : 0, (ticks != null && i < ticks.Count) ? ticks[i] : 0));
					if (Entries.Count == 40)
					{
						break;
					}
				}
			}
		}
		Version++;
	}

	internal void Clear()
	{
		if (Entries.Count != 0)
		{
			Entries.Clear();
			Version++;
		}
	}

	internal List<string> Snapshot()
	{
		return Entries.Select(((string Key, long Made, long Ticks) e) => e.Key).ToList();
	}

	internal List<long> MadeSnapshot()
	{
		return Entries.Select(((string Key, long Made, long Ticks) e) => e.Made).ToList();
	}

	internal List<long> TicksSnapshot()
	{
		return Entries.Select(((string Key, long Made, long Ticks) e) => e.Ticks).ToList();
	}

	// What the recipes the keys match made in all, and the last time any did.
	internal (long Made, long Ticks) Stats(Func<string, bool> match)
	{
		long made = 0;
		long ticks = 0;
		foreach ((string key, long n, long at) in Entries)
		{
			if (match(key))
			{
				made += n;
				ticks = Math.Max(ticks, at);
			}
		}
		return (made, ticks);
	}

	internal List<int> Resolve(IReadOnlyDictionary<string, int> currentRecipes)
	{
		List<int> result = new List<int>();
		foreach ((string key, _, _) in Entries)
		{
			if (currentRecipes.TryGetValue(key, out var recipeIndex))
			{
				result.Add(recipeIndex);
			}
		}
		return result;
	}
}
