namespace ModernRecipeBook.Tracking;

internal sealed class BookTrackerSignal
{
	private bool notified;

	internal void Reset()
	{
		notified = false;
	}

	internal bool Update(bool ready)
	{
		if (!ready || notified)
		{
			return false;
		}
		notified = true;
		return true;
	}
}
