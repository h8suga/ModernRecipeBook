namespace ModernRecipeBook;

// F1 cheat sheet: sections stack in two columns; every row is a keycap and a short action.
internal static class BookHelp
{
	internal readonly record struct Entry(string KeyRu, string KeyEn, string Ru, string En);

	internal readonly record struct Section(int Column, string Ru, string En, Entry[] Rows, bool StorageOnly = false);

	internal const float KeyWidth = 136f;

	internal const float KeyHeight = 22f;

	internal const float RowStep = 28f;

	internal const float HeaderStep = 28f;

	internal const float SectionGap = 16f;

	internal static readonly Section[] Sections = new Section[4]
	{
		new Section(0, "НАВИГАЦИЯ", "NAVIGATION", new Entry[9]
		{
			new Entry("Ctrl+F", "Ctrl+F", "Поиск", "Search"),
			new Entry("Ctrl+1…6", "Ctrl+1…6", "Вкладка по номеру", "Go to tab"),
			new Entry("Ctrl+Tab", "Ctrl+Tab", "Следующая вкладка", "Next tab"),
			new Entry("PgUp / PgDn", "PgUp / PgDn", "Листать страницы", "Change page"),
			new Entry("Home / End", "Home / End", "Первая / последняя", "First / last page"),
			new Entry("Ctrl+Backspace", "Ctrl+Backspace", "Сбросить фильтры", "Reset filters"),
			new Entry("F5", "F5", "Обновить", "Refresh"),
			new Entry("Alt+ЛКМ", "Alt+LMB", "В избранное", "Favorite"),
			new Entry("Mouse3", "Mouse3", "Цепочка предмета", "Item's chain")
		}),
		new Section(0, "ОБЩЕЕ", "GENERAL", new Entry[2]
		{
			new Entry("Esc", "Esc", "Закрыть или назад", "Close or go back"),
			new Entry("F1", "F1", "Эта справка", "This help")
		}),
		new Section(1, "РЕЦЕПТЫ И ЦЕПОЧКИ", "RECIPES & CHAINS", new Entry[4]
		{
			new Entry("Ctrl+Enter", "Ctrl+Enter", "Создать", "Craft"),
			new Entry("ЛКМ", "LMB", "Цепочка материала", "Ingredient's chain"),
			new Entry("Shift+ЛКМ", "Shift+LMB", "Цепочка рецепта", "Recipe's chain"),
			new Entry("Mouse4 / Mouse5", "Mouse4 / Mouse5", "Цепочка назад / вперёд", "Chain back / forward")
		}),
		new Section(1, "ХРАНИЛИЩЕ", "STORAGE", new Entry[6]
		{
			new Entry("Shift+ЛКМ", "Shift+LMB", "Взять стопку", "Take stack"),
			new Entry("ПКМ", "RMB", "Взять одну", "Take one"),
			new Entry("Ctrl+D", "Ctrl+D", "Положить с курсора", "Deposit held item"),
			new Entry("Ctrl+Q", "Ctrl+Q", "Быстрый стак", "Quick stack"),
			new Entry("Ctrl+Shift+D", "Ctrl+Shift+D", "Сложить всё", "Deposit all"),
			new Entry("Ctrl+R", "Ctrl+R", "Пополнить", "Restock")
		}, StorageOnly: true)
	};

	internal static BookRect Column(int column)
	{
		BookRect panel = BookGeometry.HelpPanel;
		float width = (panel.Width - 48f - 32f) / 2f;
		return new BookRect(panel.X + 24f + (float)column * (width + 32f), panel.Y + 96f, width, panel.Height - 120f);
	}
}
