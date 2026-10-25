using System;

namespace ModernRecipeBook.Tracking;

internal sealed class BookTrackerDismiss
{
	internal const int HoldTicks = 120;

	internal const int FadeTicks = 36;

	private int ticks = -1;

	internal bool Started => ticks >= 0;

	internal bool Finished => ticks >= 156;

	internal float Progress
	{
		get
		{
			float t = Math.Clamp((float)(ticks - 120) / 36f, 0f, 1f);
			return t * t * (3f - 2f * t);
		}
	}

	internal void Begin()
	{
		if (!Started)
		{
			ticks = 0;
		}
	}

	internal void Reset()
	{
		ticks = -1;
	}

	internal bool Tick()
	{
		if (Started && !Finished)
		{
			ticks++;
		}
		return Finished;
	}
}
