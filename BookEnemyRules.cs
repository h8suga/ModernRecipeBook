using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook;

internal static class BookEnemyRules
{
	internal const int PageSize = 5;

	internal static int PageCount(int count)
	{
		if (count > 0)
		{
			return 1 + (count - 1) / 5;
		}
		return 1;
	}

	internal static int ClampPage(int page, int count)
	{
		return Math.Clamp(page, 0, PageCount(count) - 1);
	}

	internal static int[] Variants(int primary, IEnumerable<int> alternatives)
	{
		return (from x in new int[1] { primary }.Concat(alternatives ?? Array.Empty<int>())
			where x != 0
			select x).Distinct().ToArray();
	}

	internal static List<BookEnemyLoot> Group(IEnumerable<BookEnemyDrop> reports)
	{
		return (from x in reports
			where x.ItemType > 0
			group x by x.ItemType into x
			select new BookEnemyLoot(x)).ToList();
	}
}
