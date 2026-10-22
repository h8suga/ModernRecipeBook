using System;

namespace ModernRecipeBook;

internal static class BookTrackingSelectionRules
{
	internal static int Pages(int count, int pageSize)
	{
		return Math.Max(1, (int)(((long)Math.Max(0, count) + (long)pageSize - 1) / pageSize));
	}

	internal static int Resolve(int count, int page, int selected, int pageSize)
	{
		if (count <= 0)
		{
			return -1;
		}
		page = Math.Clamp(page, 0, Pages(count, pageSize) - 1);
		int first = page * pageSize;
		if (selected < first || selected >= Math.Min((long)first + (long)pageSize, count))
		{
			return first;
		}
		return selected;
	}
}
