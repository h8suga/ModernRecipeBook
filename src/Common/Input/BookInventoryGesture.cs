namespace ModernRecipeBook.Common.Input;

internal sealed class BookInventoryGesture
{
	private bool captured;

	internal void Poll(bool middleDown)
	{
		if (!middleDown)
		{
			captured = false;
		}
	}

	internal void Reset()
	{
		captured = false;
	}

	internal bool Handle(bool eligible, bool middleDown, bool freshPress, out bool open)
	{
		open = false;
		if (captured)
		{
			return true;
		}
		if (!eligible || !middleDown || !freshPress)
		{
			return false;
		}
		captured = true;
		open = true;
		return true;
	}
}
