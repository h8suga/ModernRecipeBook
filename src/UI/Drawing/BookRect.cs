namespace ModernRecipeBook.UI.Drawing;

internal readonly record struct BookRect(float X, float Y, float Width, float Height)
{
	internal BookRect InsetIcon(float inset, float size)
	{
		return new BookRect(X + inset, Y + (Height - size) / 2f, size, size);
	}

	internal bool Contains(float x, float y)
	{
		if (x >= X && y >= Y && x < X + Width)
		{
			return y < Y + Height;
		}
		return false;
	}
}
