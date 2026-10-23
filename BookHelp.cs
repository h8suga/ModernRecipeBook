using System;

namespace ModernRecipeBook;

// F1 cheat sheet: three columns by task (moving around, the item card, finding things); every row is a keycap
// and a short action. A row with Shown is listed only while it returns true (a feature the config can turn off).
internal static class BookHelp
{
	internal readonly record struct Entry(string KeyRu, string KeyEn, string Ru, string En, Func<bool> Shown = null)
	{
		internal bool Visible => Shown?.Invoke() ?? true;
	}

	internal readonly record struct Section(int Column, string Ru, string En, Entry[] Rows, bool StorageOnly = false);

	internal const int Columns = 3;

	internal const float KeyWidth = 136f;

	internal const float KeyHeight = 22f;

	internal const float RowStep = 28f;

	internal const float HeaderStep = 28f;

	internal const float SectionGap = 16f;

	internal static readonly Section[] Sections = new Section[5]
	{
		new Section(0, "НАВИГАЦИЯ", "MOVING AROUND", new Entry[9]
		{
			new Entry("Ctrl+1…6", "Ctrl+1…6", "Вкладка по номеру", "Go to a tab"),
			new Entry("Ctrl+Tab", "Ctrl+Tab", "Следующая вкладка", "Next tab"),
			new Entry("Mouse4 · Alt+←", "Mouse4 · Alt+←", "Назад", "Back"),
			new Entry("Mouse5 · Alt+→", "Mouse5 · Alt+→", "Вперёд", "Forward"),
			new Entry("← ↑ → ↓", "← ↑ → ↓", "Выбор в сетке", "Move the selection"),
			new Entry("PgUp · PgDn", "PgUp · PgDn", "Листать страницы", "Turn pages"),
			new Entry("Home · End", "Home · End", "Первая · последняя", "First · last page"),
			new Entry("Esc", "Esc", "Закрыть верхнее окно", "Close the top window"),
			new Entry("F1", "F1", "Эта справка", "This help")
		}),
		new Section(0, "КАРТА · КОЛЁСИКО МЕНЯЕТСЯ", "FULL MAP · MOUSE3 REBINDABLE", new Entry[3]
		{
			new Entry("Колёсико (Mouse3)", "Mouse3 / middle", "Телепорт туда", "Teleport there", () => BookMapTeleport.Enabled),
			new Entry("ПКМ", "RMB", "Поставить метку", "Place a marker", () => BookMapMarkers.Enabled),
			new Entry("ПКМ по метке", "RMB on a marker", "Изменить, удалить", "Edit or delete it", () => BookMapMarkers.Enabled)
		}),
		new Section(1, "ПРЕДМЕТ В КАРТОЧКЕ", "THE ITEM CARD", new Entry[11]
		{
			new Entry("Ctrl+Enter", "Ctrl+Enter", "Создать", "Craft"),
			new Entry("Ctrl+Shift+Enter", "Ctrl+Shift+Enter", "Создать максимум", "Craft the most"),
			new Entry("Alt+T", "Alt+T", "Отслеживать", "Track"),
			new Entry("Alt+F", "Alt+F", "В избранное", "Favorite"),
			new Entry("Alt+G", "Alt+G", "Где взять", "How to Get"),
			new Entry("Alt+U", "Alt+U", "Рецепт · применение", "Recipe · Used in"),
			new Entry("Alt+U", "Alt+U", "Добыча · параметры", "Mob: Loot · Stats"),
			new Entry("ЛКМ", "LMB", "Открыть материал", "Open an ingredient"),
			new Entry("Shift+ЛКМ", "Shift+LMB", "Где используется", "Where it's used"),
			new Entry("Alt+ЛКМ", "Alt+LMB", "Избранное из сетки", "Favorite in the grid"),
			new Entry("Mouse3", "Mouse3", "Предмет или моб", "Look up item or mob")
		}),
		new Section(2, "ПОИСК И ФИЛЬТРЫ", "FINDING THINGS", new Entry[5]
		{
			new Entry("Ctrl+F", "Ctrl+F", "Поиск", "Search"),
			new Entry("Ctrl+Backspace", "Ctrl+Backspace", "Сбросить фильтры", "Reset filters"),
			new Entry("Alt+1…4", "Alt+1…4", "Этап", "Stage"),
			new Entry("Кнопки класса", "Class buttons", "Фильтр по классу", "Filter by class"),
			new Entry("F5", "F5", "Обновить", "Refresh")
		}),
		new Section(2, "ХРАНИЛИЩЕ", "STORAGE", new Entry[6]
		{
			new Entry("Shift+ЛКМ", "Shift+LMB", "Взять стопку", "Take a stack"),
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
		float width = (panel.Width - 48f - 32f * (Columns - 1)) / Columns;
		return new BookRect(panel.X + 24f + (float)column * (width + 32f), panel.Y + 112f, width, panel.Height - 136f);
	}
}