using System;
using Terraria.ModLoader;

namespace ModernRecipeBook.UI.Book;

// F1 cheat sheet: three columns by task (moving around and the map, the item card and the tracker cards, finding
// things and storage); every row is a keycap and a short action. A row with Shown is listed only while it returns
// true (a feature the config can turn off, a gesture that works only under a game setting).
internal static class BookHelp
{
	// Keycap and Action are localization keys (Help.*): keycaps translate too ("LMB").
	internal readonly record struct Entry(string Keycap, string Action, Func<bool> Shown = null)
	{
		internal bool Visible => Shown?.Invoke() ?? true;
	}

	internal readonly record struct Section(int Column, string Title, Entry[] Rows);

	internal const int Columns = 3;

	internal const float KeyWidth = 136f;

	internal const float KeyHeight = 22f;

	internal const float RowStep = 28f;

	internal const float HeaderStep = 28f;

	internal const float SectionGap = 16f;

	internal static readonly Section[] Sections = new Section[6]
	{
		new Section(0, "Help.SectionMOVINGAROUND", new Entry[12]
		{
			new Entry("Help.KeyCtrl16", "Help.GoToATab"),
			new Entry("Help.KeyCtrlTab", "Help.NextTab"),
			new Entry("UiPolish.KeyCtrlShiftTab", "UiPolish.PreviousTab"),
			new Entry("Help.KeyMouse4Alt", "Help.Back"),
			new Entry("Help.KeyMouse5Alt", "Help.Forward"),
			new Entry("Help.KeyArrows", "Help.MoveTheSelection"),
			// The wheel scrolls whatever list it is over: grid pages, categories, the card's rows, a menu.
			new Entry("UiPolish.KeyMouseWheel", "UiPolish.ScrollUnderTheCursor"),
			new Entry("Help.KeyPgUpPgDn", "Help.TurnPages"),
			new Entry("Help.KeyHomeEnd", "Help.FirstLastPage"),
			new Entry("Help.KeyEsc", "Help.CloseTheTopWindow"),
			new Entry("Help.KeyF5", "Help.Refresh"),
			new Entry("Help.KeyF1", "Help.ThisHelp")
		}),
		new Section(0, "Help.SectionFULLMAPMOUSE3REBINDABLE", new Entry[3]
		{
			new Entry("Help.KeyMouse3Middle", "Help.TeleportThere", () => BookMapTeleport.Enabled),
			new Entry("Help.KeyRMB", "Help.PlaceAMarker", () => BookMapMarkers.Enabled),
			new Entry("Help.KeyRMBOnAMarker", "Help.EditOrDeleteIt", () => BookMapMarkers.Enabled)
		}),
		new Section(1, "Help.SectionTHEITEMCARD", new Entry[12]
		{
			new Entry("Help.KeyCtrlEnter", "Help.Craft"),
			new Entry("Help.KeyCtrlShiftEnter", "Help.CraftTheMost"),
			new Entry("Help.KeyAltT", "Help.Track"),
			new Entry("Help.KeyAltF", "Help.Favorite"),
			new Entry("Help.KeyAltG", "Help.HowToGet"),
			new Entry("Help.KeyAltU", "Help.RecipeUsedIn"),
			new Entry("Help.KeyAltU", "Help.MobLootStats"),
			new Entry("Help.KeyLMB", "Help.OpenAnIngredient"),
			new Entry("Help.KeyAltLMB", "Help.FavoriteInTheGrid"),
			new Entry("Help.KeyMouse3", "Help.LookUpItemOrMob"),
			// A bright name in a line is a link (RecipeBookSystem.Links); a boss in the stage menu opens on Shift.
			new Entry("Links.HelpKeyLMBOnAName", "Links.HelpOpenWhatItNames"),
			new Entry("Help.KeyShiftLMB", "Links.HelpStageMenuBossCard")
		}),
		// The cards take the mouse only with the inventory open (RecipeBookSystem.TrackerInteractive).
		new Section(1, "UiPolish.SectionTRACKERCARDS", new Entry[3]
		{
			new Entry("UiPolish.KeyLMBOnACard", "UiPolish.ShowItsMaterials", Cards),
			new Entry("Help.KeyRMBHold", "Common.Untrack", Cards),
			new Entry("UiPolish.KeyDragTheTitle", "UiPolish.MoveTheCards", Cards)
		}),
		new Section(2, "Help.SectionFINDINGTHINGS", new Entry[6]
		{
			new Entry("Help.KeyCtrlF", "QuickSearch.HelpSearchEverything"),
			new Entry("Help.KeyCtrlBackspace", "Help.ResetFilters"),
			// A grid cell only: on a card's ingredient Shift does nothing.
			new Entry("UiPolish.KeyShiftLMBInGrid", "Help.WhereItSUsed"),
			new Entry("Help.KeyAlt15", "Help.Stage5BeforeTheNextBoss"),
			new Entry("Help.KeyClassButtons", "Help.FilterByClass"),
			// The station and trait menus (HandleStationMenuKeys, HandleTraitMenuKeys); Space ticks too.
			new Entry("UiPolish.KeyMenuArrowsEnter", "UiPolish.MoveTickInAMenu")
		}),
		new Section(2, "Help.SectionSTORAGE", new Entry[10]
		{
			new Entry("Help.KeyLMB", "Help.TakeAStack", Nearby),
			new Entry("Help.KeyRMBHold", "Help.OneAtATime", Nearby),
			new Entry("Help.KeyShiftLMB", "Help.StackToInventory", Nearby),
			// Only while vanilla's quick trash is on Control and no shop is open; otherwise Ctrl+Click is a plain take.
			new Entry("Help.KeyCtrlLMB", "Help.ToTheTrash", () => Nearby() && BookSources.QuickTrashOn),
			new Entry("Help.KeyCtrlEnter", "Help.SelectedToInventory", Nearby),
			new Entry("UiPolish.KeyCtrlQ", "Common.QuickStack", Nearby),
			new Entry("UiPolish.KeyCtrlR", "Common.Restock", Nearby),
			new Entry("Help.KeyShiftLMBInInv", "Help.PutAStackIn", Nearby),
			// On another kind's cell the held item swaps instead (BookSources.LeftClick).
			new Entry("Help.KeyLMBHoldingItem", "Help.PutTheHeldItemIn", Nearby),
			new Entry("UiPolish.KeyMouse3OnACell", "UiPolish.OpenItsCard", Nearby)
		})
	};

	// The Storage rows go while the host has nearby storage off.
	private static bool Nearby() => BookNearbyConfig.Enabled;

	// The card rows go while the overlay is set to the compact lines, which take no input.
	private static bool Cards() => !ModContent.GetInstance<BookConfig>().ResourceOnlyTracker;

	internal static BookRect Column(int column)
	{
		BookRect panel = BookGeometry.HelpPanel;
		float width = (panel.Width - 48f - 24f * (Columns - 1)) / Columns;
		return new BookRect(panel.X + 24f + (float)column * (width + 24f), panel.Y + 112f, width, panel.Height - 136f);
	}
}