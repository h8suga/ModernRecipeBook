using System;

namespace ModernRecipeBook;

// F1 cheat sheet: three columns by task (moving around, the item card, finding things); every row is a keycap
// and a short action. A row with Shown is listed only while it returns true (a feature the config can turn off).
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

	internal static readonly Section[] Sections = new Section[5]
	{
		new Section(0, "Help.SectionMOVINGAROUND", new Entry[9]
		{
			new Entry("Help.KeyCtrl16", "Help.GoToATab"),
			new Entry("Help.KeyCtrlTab", "Help.NextTab"),
			new Entry("Help.KeyMouse4Alt", "Help.Back"),
			new Entry("Help.KeyMouse5Alt", "Help.Forward"),
			new Entry("Help.KeyArrows", "Help.MoveTheSelection"),
			new Entry("Help.KeyPgUpPgDn", "Help.TurnPages"),
			new Entry("Help.KeyHomeEnd", "Help.FirstLastPage"),
			new Entry("Help.KeyEsc", "Help.CloseTheTopWindow"),
			new Entry("Help.KeyF1", "Help.ThisHelp")
		}),
		new Section(0, "Help.SectionFULLMAPMOUSE3REBINDABLE", new Entry[3]
		{
			new Entry("Help.KeyMouse3Middle", "Help.TeleportThere", () => BookMapTeleport.Enabled),
			new Entry("Help.KeyRMB", "Help.PlaceAMarker", () => BookMapMarkers.Enabled),
			new Entry("Help.KeyRMBOnAMarker", "Help.EditOrDeleteIt", () => BookMapMarkers.Enabled)
		}),
		new Section(1, "Help.SectionTHEITEMCARD", new Entry[11]
		{
			new Entry("Help.KeyCtrlEnter", "Help.Craft"),
			new Entry("Help.KeyCtrlShiftEnter", "Help.CraftTheMost"),
			new Entry("Help.KeyAltT", "Help.Track"),
			new Entry("Help.KeyAltF", "Help.Favorite"),
			new Entry("Help.KeyAltG", "Help.HowToGet"),
			new Entry("Help.KeyAltU", "Help.RecipeUsedIn"),
			new Entry("Help.KeyAltU", "Help.MobLootStats"),
			new Entry("Help.KeyLMB", "Help.OpenAnIngredient"),
			new Entry("Help.KeyShiftLMB", "Help.WhereItSUsed"),
			new Entry("Help.KeyAltLMB", "Help.FavoriteInTheGrid"),
			new Entry("Help.KeyMouse3", "Help.LookUpItemOrMob")
		}),
		new Section(2, "Help.SectionFINDINGTHINGS", new Entry[5]
		{
			new Entry("Help.KeyCtrlF", "Help.Search"),
			new Entry("Help.KeyCtrlBackspace", "Help.ResetFilters"),
			new Entry("Help.KeyAlt15", "Help.Stage5BeforeTheNextBoss"),
			new Entry("Help.KeyClassButtons", "Help.FilterByClass"),
			new Entry("Help.KeyF5", "Help.Refresh")
		}),
		new Section(2, "Help.SectionSTORAGE", new Entry[8]
		{
			new Entry("Help.KeyLMB", "Help.TakeAStack", Nearby),
			new Entry("Help.KeyRMBHold", "Help.OneAtATime", Nearby),
			new Entry("Help.KeyShiftLMB", "Help.StackToInventory", Nearby),
			new Entry("Help.KeyCtrlLMB", "Help.ToTheTrash", Nearby),
			new Entry("Help.KeyCtrlEnter", "Help.SelectedToInventory", Nearby),
			new Entry("Help.KeyCtrlQCtrlR", "Help.QuickStackRestock", Nearby),
			new Entry("Help.KeyShiftLMBInInv", "Help.PutAStackIn", Nearby),
			new Entry("Help.KeyLMBHoldingItem", "Help.PutTheHeldItemIn", Nearby)
		})
	};

	// The Storage rows go while the host has nearby storage off.
	private static bool Nearby() => BookNearbyConfig.Enabled;

	internal static BookRect Column(int column)
	{
		BookRect panel = BookGeometry.HelpPanel;
		float width = (panel.Width - 48f - 32f * (Columns - 1)) / Columns;
		return new BookRect(panel.X + 24f + (float)column * (width + 32f), panel.Y + 112f, width, panel.Height - 136f);
	}
}