using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook;

internal static class BookTrackerRules
{
	internal const int MaxGoals = 5;

	internal const int PanelWidth = 300;

	internal const int HeaderHeight = 28;

	internal const int GoalHeight = 40;

	internal static bool CanAdd(int count, bool duplicate, bool resourcesOnly = false)
	{
		if (count >= 0 && (resourcesOnly || count < 5))
		{
			return !duplicate;
		}
		return false;
	}

	internal static int DetailHeight(int rows)
	{
		return 24 + Math.Min(5, Math.Max(0, rows)) * 24 + ((rows > 5) ? 24 : 0);
	}

	internal static int PanelHeight(int[] rows, int active)
	{
		return 36 + rows.Length * 40 + ((active >= 0 && active < rows.Length) ? DetailHeight(rows[active]) : 0);
	}

	internal static BookRect GoalRow(int[] rows, int active, int index)
	{
		return new BookRect(0f, 28 + index * 40 + ((active >= 0 && active < index) ? DetailHeight(rows[active]) : 0), 300f, 40f);
	}

	internal static BookGeometry PanelLayout(int[] rows, int active, int width, int height, bool custom, float x, float y, int sizePercent = 100, float uiScale = 1f)
	{
		return PanelLayoutHeight(PanelHeight(rows, active), width, height, custom, x, y, sizePercent, uiScale);
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

	internal static int PageCount(int rows)
	{
		return Math.Max(1, (rows + 4) / 5);
	}

	internal static int Height(int rows)
	{
		return 108 + Math.Min(5, rows) * 36 + ((rows > 5) ? 28 : 0);
	}

	internal static BookGeometry Layout(int rows, int width, int height)
	{
		float scale = Math.Max(0.1f, Math.Min(1f, Math.Min(((float)width - 32f) / 340f, ((float)height - 100f) / (float)Height(rows))));
		return new BookGeometry(16f, Math.Max(16f, (float)(height - 16) - (float)Height(rows) * scale), scale);
	}
}
