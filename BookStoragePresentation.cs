namespace ModernRecipeBook;

internal static class BookStoragePresentation
{
	internal static string Connection(bool installed, bool supported, bool connected, bool ru)
	{
		if (installed)
		{
			if (supported)
			{
				if (!connected)
				{
					if (!ru)
					{
						return "No accessible network";
					}
					return "Нет доступной сети";
				}
				if (!ru)
				{
					return "Network connected";
				}
				return "Сеть подключена";
			}
			if (!ru)
			{
				return "Requires Magic Storage 0.7.0.11; see log";
			}
			return "Нужен Magic Storage 0.7.0.11; см. журнал";
		}
		if (!ru)
		{
			return "Magic Storage is not installed";
		}
		return "Нужен мод Magic Storage";
	}

	internal static string Access(string name, bool crafting, bool ru)
	{
		return name switch
		{
			"CraftingAccess" => ru ? "Интерфейс крафта" : "Crafting Interface", 
			"StorageHeart" => ru ? "Сердце хранилища" : "Storage Heart", 
			"StorageAccess" => ru ? "Точка доступа" : "Storage Access", 
			"RemoteAccess" => ru ? "Удалённая точка доступа" : "Remote Access", 
			_ => (!crafting) ? (ru ? "Точка доступа" : "Storage Access") : (ru ? "Интерфейс крафта" : "Crafting Interface"), 
		};
	}

	internal static string Coordinates(int x, int y, bool ru)
	{
		if (x >= 0 && y >= 0)
		{
			if (ru)
			{
				return $"Ячейка: X={x}, Y={y} (тайлы)";
			}
			return $"Tile: X={x}, Y={y}";
		}
		if (!ru)
		{
			return "Coordinates unavailable";
		}
		return "Координаты недоступны";
	}
}
