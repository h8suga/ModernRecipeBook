using System;
using System.Collections.Generic;
using System.Globalization;

namespace ModernRecipeBook;

internal static class BookMaterialRules
{
	internal readonly record struct Assessment(long Required, int Available, bool Unknown, bool Infinite, bool Overlapping, bool AvailableSaturated, long Deficit, long Remaining)
	{
		internal bool HasDeficit
		{
			get
			{
				if (!Unknown && !Infinite)
				{
					return Deficit > 0;
				}
				return false;
			}
		}

		internal bool CanConfirmSufficiency
		{
			get
			{
				if (!Unknown)
				{
					if (!Infinite)
					{
						if (!Overlapping)
						{
							return Deficit == 0;
						}
						return false;
					}
					return true;
				}
				return false;
			}
		}

		internal bool CanShowRemaining
		{
			get
			{
				if (!Unknown)
				{
					if (!Infinite)
					{
						return !Overlapping;
					}
					return true;
				}
				return false;
			}
		}
	}

	internal static string Ratio(Assessment value)
	{
		string? text;
		if (value.Infinite)
		{
			text = "∞";
		}
		else
		{
			text = ((value.Available < 0) ? "?" : ((value.AvailableSaturated ? "≥" : "") + value.Available.ToString("N0", CultureInfo.CurrentCulture)));
		}
		return text + "/" + value.Required.ToString("N0", CultureInfo.CurrentCulture);
	}

	internal static int NormalizeBatches(int batches)
	{
		if (batches >= 1)
		{
			return batches;
		}
		return 1;
	}

	internal static Assessment Evaluate(int perBatch, int batches, int available, bool infinite, bool overlapping)
	{
		long required = (long)((perBatch >= 0) ? perBatch : 0) * (long)NormalizeBatches(batches);
		if (infinite)
		{
			return new Assessment(required, available, Unknown: false, Infinite: true, overlapping, AvailableSaturated: false, 0L, 0L);
		}
		bool saturated = available == int.MaxValue;
		if (available < 0 || (saturated && required > int.MaxValue))
		{
			return new Assessment(required, available, Unknown: true, Infinite: false, overlapping, saturated, 0L, 0L);
		}
		long knownAvailable = available;
		long deficit = Math.Max(0L, required - knownAvailable);
		long remaining = Math.Max(0L, knownAvailable - required);
		return new Assessment(required, available, Unknown: false, Infinite: false, overlapping, saturated, deficit, remaining);
	}

	internal static bool[] FindOverlappingRequirements(IReadOnlyList<HashSet<int>> itemSets)
	{
		bool[] overlaps = new bool[itemSets.Count];
		for (int i = 0; i < itemSets.Count; i++)
		{
			if (itemSets[i] == null)
			{
				continue;
			}
			for (int j = i + 1; j < itemSets.Count; j++)
			{
				if (itemSets[j] == null)
				{
					continue;
				}
				HashSet<int> hashSet = ((itemSets[i].Count <= itemSets[j].Count) ? itemSets[i] : itemSets[j]);
				HashSet<int> larger = ((hashSet == itemSets[i]) ? itemSets[j] : itemSets[i]);
				foreach (int type in hashSet)
				{
					if (larger.Contains(type))
					{
						overlaps[i] = (overlaps[j] = true);
						break;
					}
				}
			}
		}
		return overlaps;
	}
}
