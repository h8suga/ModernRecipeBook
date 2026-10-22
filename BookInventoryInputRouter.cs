namespace ModernRecipeBook;

internal sealed class BookInventoryInputRouter
{
	private bool previous;

	private bool fresh;

	private bool consumed;

	private bool pending;

	internal void Poll(bool down)
	{
		if (down && !previous)
		{
			fresh = true;
			consumed = false;
		}
		if (!down)
		{
			fresh = false;
		}
		previous = down;
	}

	internal void QueueToggle()
	{
		pending = true;
	}

	internal bool TakeInventory(bool eligible)
	{
		if (!fresh || consumed || !eligible)
		{
			return false;
		}
		fresh = false;
		consumed = true;
		pending = false;
		return true;
	}

	internal bool EndDraw()
	{
		bool result = pending && !consumed;
		pending = (fresh = false);
		return result;
	}

	internal void Reset()
	{
		previous = (fresh = (consumed = (pending = false)));
	}
}
