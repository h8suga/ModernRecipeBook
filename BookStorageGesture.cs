namespace ModernRecipeBook;

internal static class BookStorageGesture
{
	internal enum Transfer
	{
		None,
		One,
		Stack
	}

	internal static Transfer Read(bool storageView, bool hovered, bool menuOpen, bool leftPressed, bool rightPressed, bool shift)
	{
		if ((!storageView || !hovered) | menuOpen)
		{
			return Transfer.None;
		}
		if (rightPressed)
		{
			return Transfer.One;
		}
		if (!(leftPressed & shift))
		{
			return Transfer.None;
		}
		return Transfer.Stack;
	}
}
