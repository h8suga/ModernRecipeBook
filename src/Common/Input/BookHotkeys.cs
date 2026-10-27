using Microsoft.Xna.Framework.Input;

namespace ModernRecipeBook.Common.Input;

internal static class BookHotkeys
{
	internal enum Action
	{
		None,
		Help,
		Tab1,
		Tab2,
		Tab3,
		Tab4,
		Tab5,
		Tab6,
		NextTab,
		PreviousTab,
		ResetFilters,
		Craft,
		CraftMax,
		ToggleTrack,
		ToggleFavorite,
		HowToGet,
		ToggleUsedIn,
		Stage1,
		Stage2,
		Stage3,
		Stage4,
		Stage5,
		MoveLeft,
		MoveRight,
		MoveUp,
		MoveDown,
		QuickStack,
		Restock,
		Refresh,
		Back,
		Forward,
		PagePrevious,
		PageNext,
		FirstPage,
		LastPage,
		TakeSelected
	}

		// arrows: the arrow keys are not bound to a game control, so they can move the grid selection.
	internal static Action Read(KeyboardState keys, KeyboardState previous, bool typing, bool blocked, bool nativeInput, bool help, bool menu, bool storage, bool arrows)
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
			if (Press(Keys.D6))
			{
				return Action.Tab6;
			}
			if (Press(Keys.Tab))
			{
				if (!shift)
				{
					return Action.NextTab;
				}
				return Action.PreviousTab;
			}
			// Works while typing too: "Nothing found · Ctrl+Backspace resets filters" shows with the search field focused.
			if (!menu && Press(Keys.Back))
			{
				return Action.ResetFilters;
			}
		}
		if (typing | blocked | menu)
		{
			return Action.None;
		}
		if (ctrl && !alt)
		{
			if (Press(Keys.Enter))
			{
				return storage ? (shift ? Action.None : Action.TakeSelected) : (shift ? Action.CraftMax : Action.Craft);
			}
			if (storage)
			{
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
		// Alt+letter acts on the item in the card; Alt+1..5 picks the stage (5: before the next boss). Plain letters would also reach the
		// game (T throws the held item), Alt keeps them to the book.
		if (alt && !ctrl && !shift)
		{
			if (Press(Keys.T))
			{
				return Action.ToggleTrack;
			}
			if (Press(Keys.F))
			{
				return Action.ToggleFavorite;
			}
			if (Press(Keys.G))
			{
				return Action.HowToGet;
			}
			if (Press(Keys.U))
			{
				return Action.ToggleUsedIn;
			}
			if (Press(Keys.D1))
			{
				return Action.Stage1;
			}
			if (Press(Keys.D2))
			{
				return Action.Stage2;
			}
			if (Press(Keys.D3))
			{
				return Action.Stage3;
			}
			if (Press(Keys.D4))
			{
				return Action.Stage4;
			}
			if (Press(Keys.D5))
			{
				return Action.Stage5;
			}
		}
		if (arrows && !ctrl && !alt && !shift)
		{
			if (Press(Keys.Left))
			{
				return Action.MoveLeft;
			}
			if (Press(Keys.Right))
			{
				return Action.MoveRight;
			}
			if (Press(Keys.Up))
			{
				return Action.MoveUp;
			}
			if (Press(Keys.Down))
			{
				return Action.MoveDown;
			}
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

	// Ctrl+F, the quick search: anywhere in the game, the book open or not (RecipeBookSystem.UpdateQuickSearch decides
	// where it may open). Ctrl+Shift+F counts too; with Alt it is not the chord.
	internal static bool QuickSearch(KeyboardState keys, KeyboardState previous)
	{
		bool ctrl = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
		bool alt = keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
		return ctrl && !alt && keys.IsKeyDown(Keys.F) && !previous.IsKeyDown(Keys.F);
	}
}
