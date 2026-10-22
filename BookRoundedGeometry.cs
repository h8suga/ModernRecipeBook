using System;

namespace ModernRecipeBook;

internal static class BookRoundedGeometry
{
	internal readonly record struct Area(int X, int Y, int Width, int Height);

	internal readonly record struct Patch(Area Source, Area Destination);

	internal const int TextureSize = 96;

	internal const int SourceCorner = 32;

	internal static int ClampRadius(int width, int height, int radius)
	{
		return Math.Clamp(radius, 0, Math.Max(0, Math.Min(width, height) / 2));
	}

	internal static Patch Slice(int width, int height, int radius, int index)
	{
		if (width <= 0 || height <= 0 || index < 0 || index > 8)
		{
			throw new ArgumentOutOfRangeException();
		}
		int r = ClampRadius(width, height, radius);
		int col = index % 3;
		int row = index / 3;
		int x = col switch
		{
			1 => r, 
			0 => 0, 
			_ => width - r, 
		};
		int y = row switch
		{
			1 => r, 
			0 => 0, 
			_ => height - r, 
		};
		int w = ((col == 1) ? (width - r * 2) : r);
		int h = ((row == 1) ? (height - r * 2) : r);
		return new Patch(new Area(col * 32, row * 32, 32, 32), new Area(x, y, w, h));
	}
}
