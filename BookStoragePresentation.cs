namespace ModernRecipeBook;

internal static class BookStoragePresentation
{
	internal static string Connection(bool installed, bool supported, bool connected, bool ru, string version = "")
	{
		if (installed)
		{
			if (supported)
			{
				if (!connected)
				{
					if (!ru)
					{
						return "Not connected";
					}
					return "Не подключено";
				}
				if (!ru)
				{
					return "Connected";
				}
				return "Подключено";
			}
			if (!ru)
			{
				return "Magic Storage " + version + " is not supported yet";
			}
			return "Magic Storage " + version + " пока не поддерживается";
		}
		if (!ru)
		{
			return "Magic Storage is not installed";
		}
		return "Magic Storage не установлен";
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
				return $"X {x}, Y {y}";
			}
			return $"X {x}, Y {y}";
		}
		if (!ru)
		{
			return "Position unknown";
		}
		return "Место неизвестно";
	}
}
