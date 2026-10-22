using Microsoft.Xna.Framework.Input;

namespace ModernRecipeBook;

internal static class BookHotkeys
{
	internal enum Action
	{
		None,
		Help,
		Search,
		Tab1,
		Tab2,
		Tab3,
		Tab4,
		Tab5,
		NextTab,
		PreviousTab,
		ResetFilters,
		Craft,
		DepositCursor,
		DepositAll,
		QuickStack,
		Restock,
		Refresh,
		Back,
		Forward,
		PagePrevious,
		PageNext,
		FirstPage,
		LastPage
	}

	internal static Action Read(KeyboardState keys, KeyboardState previous, bool typing, bool blocked, bool nativeInput, bool help, bool menu, bool storage)
	{
		bool ctrl = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
		bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
		bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
		if (nativeInput || (blocked && !typing))
		{
			return Action.None;
		}
		if (Press(Keys.F1))
		{
			return Action.Help;
		}
		if (help)
		{
			return Action.None;
		}
		if (ctrl && !alt)
		{
			if (Press(Keys.F))
			{
				return Action.Search;
			}
			// Ctrl+N opens the N-th visible tab, the number its tooltip shows.
			if (Press(Keys.D1))
			{
				return Action.Tab1;
			}
			if (Press(Keys.D2))
			{
				return Action.Tab2;
			}
			if (Press(Keys.D3))
			{
				return Action.Tab3;
			}
			if (Press(Keys.D4))
			{
				return Action.Tab4;
			}
			if (Press(Keys.D5))
			{
				return Action.Tab5;
			}
			if (Press(Keys.Tab))
			{
				if (!shift)
				{
					return Action.NextTab;
				}
				return Action.PreviousTab;
			}
		}
		if (typing | blocked | menu)
		{
			return Action.None;
		}
		if (ctrl && !alt)
		{
			if (Press(Keys.Back))
			{
				return Action.ResetFilters;
			}
			if (!storage && Press(Keys.Enter))
			{
				return Action.Craft;
			}
			if (storage)
			{
				if (Press(Keys.D))
				{
					if (!shift)
					{
						return Action.DepositCursor;
					}
					return Action.DepositAll;
				}
				if (Press(Keys.Q))
				{
					return Action.QuickStack;
				}
				if (Press(Keys.R))
				{
					return Action.Restock;
				}
			}
		}
		if (Press(Keys.F5))
		{
			return Action.Refresh;
		}
		if (alt && !ctrl && Press(Keys.Left))
		{
			return Action.Back;
		}
		if (alt && !ctrl && Press(Keys.Right))
		{
			return Action.Forward;
		}
		if (!ctrl && !alt)
		{
			if (Press(Keys.PageUp))
			{
				return Action.PagePrevious;
			}
			if (Press(Keys.PageDown))
			{
				return Action.PageNext;
			}
			if (Press(Keys.Home))
			{
				return Action.FirstPage;
			}
			if (Press(Keys.End))
			{
				return Action.LastPage;
			}
		}
		return Action.None;
		bool Press(Keys key)
		{
			if (keys.IsKeyDown(key))
			{
				return !previous.IsKeyDown(key);
			}
			return false;
		}
	}
}
