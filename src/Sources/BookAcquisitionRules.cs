using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ModernRecipeBook.Sources;

internal static class BookAcquisitionRules
{
	internal const int PageSize = 6;

	internal static int PageCount(int count)
	{
		if (count > 0)
		{
			return 1 + (count - 1) / 6;
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
			where x > 0
			select x).Distinct().ToArray();
	}

	internal static string Chance(float rate)
	{
		if (!float.IsFinite(rate) || rate < 0f || rate > 1f)
		{
			return "?";
		}
		if (rate > 0f && rate < 0.0001f)
		{
			return "<0.01%";
		}
		return (rate * 100f).ToString("0.##", CultureInfo.CurrentCulture) + "%";
	}

	internal static string Stack(int min, int max)
	{
		if (min != max)
		{
			return min.ToString(CultureInfo.CurrentCulture) + "–" + max.ToString(CultureInfo.CurrentCulture);
		}
		return min.ToString(CultureInfo.CurrentCulture);
	}
}
