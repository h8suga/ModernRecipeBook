namespace ModernRecipeBook;

internal sealed class BookInventoryInputRouter
{
	private bool previous;

	private bool fresh;

	private bool consumed;

	private bool pending;

	// The interface was drawn since the press. Item tooltips (the crafting list, other mods' windows) are drawn
	// after PostDrawInterface, so a press is resolved only at the next update, once the whole frame is drawn.
	private bool drawn;

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

	internal void EndDraw()
	{
		drawn = true;
	}

	// Called before Poll: true when a queued toggle reached a fully drawn frame and no item took the press.
	internal bool ResolveFrame()
	{
		if (!drawn)
		{
			return false;
		}
		drawn = false;
		bool result = pending && !consumed;
		pending = (fresh = false);
		return result;
	}

	internal void Reset()
	{
		previous = (fresh = (consumed = (pending = (drawn = false))));
	}
}
