using System;

namespace ModernRecipeBook.UI.Drawing;

internal static class BookGlyphLayout
{
	internal readonly record struct Glyph(float Bearing, float Advance, float PadX, float PadY, int Width, int Height);

	internal readonly record struct Bounds(float X, float Y, float Width, float Height);

	internal static Bounds Measure(string text, float spacing, float lineSpacing, Func<char, Glyph> glyphFor)
	{
		float x = 0f;
		float y = 0f;
		float minX = float.PositiveInfinity;
		float minY = float.PositiveInfinity;
		float maxX = float.NegativeInfinity;
		float maxY = float.NegativeInfinity;
		bool first = true;
		foreach (char c in text)
		{
			switch (c)
			{
			case '\n':
				x = 0f;
				y += lineSpacing;
				first = true;
				continue;
			case '\r':
				continue;
			}
			Glyph g = glyphFor(c);
			x += (first ? Math.Max(0f, g.Bearing) : (spacing + g.Bearing));
			if (!char.IsWhiteSpace(c) && g.Width > 0 && g.Height > 0)
			{
				minX = Math.Min(minX, x + g.PadX);
				minY = Math.Min(minY, y + g.PadY);
				maxX = Math.Max(maxX, x + g.PadX + (float)g.Width);
				maxY = Math.Max(maxY, y + g.PadY + (float)g.Height);
			}
			x += g.Advance;
			first = false;
		}
		if (!float.IsPositiveInfinity(minX))
		{
			return new Bounds(minX, minY, maxX - minX, maxY - minY);
		}
		return new Bounds(0f, 0f, 0f, 0f);
	}
}
