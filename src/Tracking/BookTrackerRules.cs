using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Tracking;

internal static class BookTrackerRules
{
	internal const int MaxGoals = 5;

	internal const int PanelWidth = 300;

	internal const int HeaderHeight = 32;

	internal const int GoalHeight = 44;

	internal const int MaterialHeight = 24;

	internal const int MaxMaterialLines = 8;

	internal static bool CanAdd(int count, bool duplicate, bool resourcesOnly = false)
	{
		if (count >= 0 && (resourcesOnly || count < 5))
		{
			return !duplicate;
		}
		return false;
	}

	// Expanded goal: one line per material, the last line becomes "+N more" when they do not fit.
	internal static int MaterialLines(int materials)
	{
		return Math.Clamp(materials, 0, MaxMaterialLines);
	}

	internal static int ExpandedHeight(int materials)
	{
		if (materials <= 0)
		{
			return 0;
		}
		return MaterialLines(materials) * MaterialHeight + 8;
	}

	internal static int ListHeight(int goals, int expanded, int expandedMaterials)
	{
		return HeaderHeight + goals * GoalHeight + ((expanded >= 0 && expanded < goals) ? ExpandedHeight(expandedMaterials) : 0) + 4;
	}

	internal static float GoalTop(int index, int expanded, int expandedMaterials)
	{
		return HeaderHeight + index * GoalHeight + ((expanded >= 0 && expanded < index) ? ExpandedHeight(expandedMaterials) : 0);
	}

	internal static int GoalAt(float y, int goals, int expanded, int expandedMaterials)
	{
		for (int i = 0; i < goals; i++)
		{
			float top = GoalTop(i, expanded, expandedMaterials);
			if (y >= top && y < top + GoalHeight + ((i == expanded) ? ExpandedHeight(expandedMaterials) : 0))
			{
				return i;
			}
		}
		return -1;
	}

	internal static BookGeometry PanelLayoutHeight(int panelHeight, int width, int height, bool custom, float x, float y, int sizePercent = 100, float uiScale = 1f)
	{
		float scale = Math.Max(0.01f, Math.Min((float)Math.Clamp(sizePercent, 60, 150) / 100f / Math.Max(0.1f, uiScale), Math.Min(((float)width - 32f) / 300f, ((float)height - 100f) / (float)panelHeight)));
		float travelX = Math.Max(0f, (float)width - 32f - 300f * scale);
		float travelY = Math.Max(0f, (float)height - 32f - (float)panelHeight * scale);
		return new BookGeometry(16f + travelX * (custom ? BookUiState.NormalizePosition(x) : 0f), 16f + travelY * (custom ? BookUiState.NormalizePosition(y) : 1f), scale);
	}

	internal static (float X, float Y) Position(float left, float top, int panelHeight, int width, int height, float scale)
	{
		float travelX = Math.Max(0f, (float)width - 32f - 300f * scale);
		float travelY = Math.Max(0f, (float)height - 32f - (float)panelHeight * scale);
		return (X: (travelX > 0f) ? Math.Clamp((left - 16f) / travelX, 0f, 1f) : 0f, Y: (travelY > 0f) ? Math.Clamp((top - 16f) / travelY, 0f, 1f) : 1f);
	}

	internal static string MaterialKey(IEnumerable<int> types)
	{
		return string.Join(",", from t in types.Distinct()
			orderby t
			select t);
	}

	internal static BookBatchNeed[] MergeNeeds(IEnumerable<BookBatchNeed> needs)
	{
		List<BookBatchNeed> result = new List<BookBatchNeed>();
		foreach (IGrouping<string, BookBatchNeed> group in from n in needs
			group n by MaterialKey(n.AcceptedTypes))
		{
			long quantity = ((IEnumerable<BookBatchNeed>)group).Sum((Func<BookBatchNeed, long>)((BookBatchNeed n) => Math.Max(0, n.Quantity)));
			if (quantity > int.MaxValue)
			{
				throw new ArgumentOutOfRangeException("needs", "Merged material quantity exceeds supported bounds");
			}
			result.Add(new BookBatchNeed((int)quantity, group.First().AcceptedTypes.Distinct().ToArray()));
		}
		return result.ToArray();
	}

	internal static bool Ready(IReadOnlyList<BookBatchNeed> needs, IReadOnlyDictionary<int, int> counts, int batches)
	{
		if (batches < 1 || needs.Count > 256 || ((IEnumerable<BookBatchNeed>)needs).Sum((Func<BookBatchNeed, long>)((BookBatchNeed n) => n.AcceptedTypes.Count)) > 16384 || needs.SelectMany((BookBatchNeed n) => n.AcceptedTypes).Distinct().Take(4097)
			.Count() > 4096)
		{
			return false;
		}
		return BookBatchPlanner.MaxBatches(needs, counts, batches) >= batches;
	}
}
