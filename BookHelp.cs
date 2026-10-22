namespace ModernRecipeBook;

internal static class BookHelp
{
	internal readonly record struct Entry(string KeyRu, string KeyEn, string Ru, string En);

	internal readonly record struct Section(BookRect Bounds, string Ru, string En, Entry[] Rows, int Step);

	internal const int KeyWidth = 176;

	internal const int KeyHeight = 24;

	internal static readonly Section[] Sections = new Section[3]
	{
		new Section(new BookRect(BookGeometry.HelpPanel.X + 24f, BookGeometry.HelpPanel.Y + 64f, 468f, 288f), "НАВИГАЦИЯ И ПОИСК", "NAVIGATION & SEARCH", new Entry[9]
		{
			new Entry("Ctrl+F", "Ctrl+F", "Поиск", "Search"),
			new Entry("Ctrl+1…6", "Ctrl+1…6", "Переключить вкладку", "Switch tab"),
			new Entry("Mouse3", "Mouse3", "Предмет → цепочки", "Item → chains"),
			new Entry("Ctrl+Tab", "Ctrl+Tab", "Следующая вкладка", "Next tab"),
			new Entry("PgUp / PgDn", "PgUp / PgDn", "Листать страницы", "Change page"),
			new Entry("Home / End", "Home / End", "Первая / последняя", "First / last page"),
			new Entry("Ctrl+Backspace", "Ctrl+Backspace", "Сброс поиска и фильтров*", "Reset search & filters*"),
			new Entry("Alt+ЛКМ", "Alt+LMB", "Предмет в избранное", "Toggle item favorite"),
			new Entry("F5", "F5", "Обновить", "Refresh")
		}, 28),
		new Section(new BookRect(BookGeometry.HelpPanel.X + 24f, BookGeometry.HelpPanel.Y + 360f, 468f, 176f), "РЕЦЕПТЫ И ЦЕПОЧКИ", "RECIPES & CHAINS", new Entry[5]
		{
			new Entry("×1/×5/×10/Макс", "×1/×5/×10/Max", "Выбрать число партий", "Choose batch count"),
			new Entry("Ctrl+Enter", "Ctrl+Enter", "Создать выбранные партии", "Craft chosen batches"),
			new Entry("Shift+ЛКМ", "Shift+LMB", "Рецепт → цепочка", "Recipe → chain"),
			new Entry("ЛКМ", "LMB", "Материал → цепочка", "Ingredient → chain"),
			new Entry("Mouse4 / Mouse5", "Mouse4 / Mouse5", "Назад / вперёд в цепочке", "Back / forward in chain")
		}, 28),
		new Section(new BookRect(BookGeometry.HelpPanel.X + 508f, BookGeometry.HelpPanel.Y + 64f, 468f, 472f), "ХРАНИЛИЩЕ", "STORAGE", new Entry[6]
		{
			new Entry("Shift+ЛКМ", "Shift+LMB", "Стопка в инвентарь", "Stack to inventory"),
			new Entry("ПКМ", "RMB", "Один предмет в инвентарь", "One item to inventory"),
			new Entry("Ctrl+D", "Ctrl+D", "Положить с курсора", "Deposit cursor item"),
			new Entry("Ctrl+Q", "Ctrl+Q", "Быстрый стак", "Quick stack"),
			new Entry("Ctrl+Shift+D", "Ctrl+Shift+D", "Сложить всё", "Deposit all"),
			new Entry("Ctrl+R", "Ctrl+R", "Пополнить стопки", "Restock stacks")
		}, 40)
	};

	internal static BookRect KeyRect(Section section, int row)
	{
		return new BookRect(section.Bounds.X + 16f, section.Bounds.Y + 36f + (float)(row * section.Step), 176f, 24f);
	}

	internal static BookRect LabelRect(Section section, int row)
	{
		BookRect key = KeyRect(section, row);
		return new BookRect(key.X + key.Width + 12f, key.Y, section.Bounds.Width - 176f - 44f, 24f);
	}
}
