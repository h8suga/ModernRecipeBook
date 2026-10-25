using System;
using System.Globalization;

namespace ModernRecipeBook;

internal static class BookTypography
{
	internal readonly record struct Line(string Value, float Scale, float X, float Y, bool Truncated);

	internal static Line FixedLine(string text, float width, float height, float size, float textScale, Func<string, (float Width, float Height)> measure, float referenceTop, float referenceHeight)
	{
		Line line = Layout(text, width, height, size, textScale, (string value) => (Width: measure(value).Item1, Height: referenceHeight), centered: true);
		return line with
		{
			X = 0f,
			Y = line.Y - referenceTop * line.Scale
		};
	}

	internal static float StackTop(float containerHeight, float firstHeight, float secondHeight, float gap)
	{
		return (containerHeight - firstHeight - gap - secondHeight) / 2f;
	}

	internal static Line Layout(string text, float width, float height, float size, float textScale, Func<string, (float Width, float Height)> measure, bool centered = false)
	{
		if (text == null)
		{
			text = "";
		}
		width = Math.Max(0f, width);
		float scale = size * Math.Clamp(textScale, 0.9f, 1.15f);
		(float, float) full = measure(text);
		if (height > 0f && full.Item2 > 0f)
		{
			scale = Math.Min(scale, height / full.Item2);
		}
		string value = text;
		bool truncated = full.Item1 * scale > width;
		if (truncated)
		{
			value = "";
			if (measure("...").Item1 * scale <= width)
			{
				int[] boundaries = StringInfo.ParseCombiningCharacters(text);
				int lo = 0;
				int hi = boundaries.Length;
				while (lo < hi)
				{
					int mid = (lo + hi + 1) / 2;
					if (measure(text[..((mid == boundaries.Length) ? text.Length : boundaries[mid])] + "...").Item1 * scale <= width)
					{
						lo = mid;
					}
					else
					{
						hi = mid - 1;
					}
				}
				value = text[..((lo == boundaries.Length) ? text.Length : boundaries[lo])] + "...";
			}
		}
		(float, float) actual = measure(value);
		return new Line(value, scale, centered ? Math.Max(0f, (width - actual.Item1 * scale) / 2f) : 0f, (centered && height > 0f) ? Math.Max(0f, (height - actual.Item2 * scale) / 2f) : 0f, truncated);
	}
}
