namespace ModernRecipeBook.Tracking;

// Fires when a tracked goal turns ready. The first state it sees is only remembered: a goal already ready when it is
// tracked (its materials lie in the base storage) is not a completion, so it neither chimes nor fades out.
internal sealed class BookTrackerSignal
{
	private bool? last;

	internal void Reset()
	{
		last = null;
	}

	internal bool Update(bool ready)
	{
		bool turned = ready && last == false;
		last = ready;
		return turned;
	}
}
