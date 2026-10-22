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
			// Shown under the "MAGIC STORAGE" label, so "Version" is enough and the line fits the panel.
			if (!ru)
			{
				return "Version " + version + " is not supported yet";
			}
			return "Версия " + version + " не поддерживается";
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

	// "Crafting Interface · 7 tiles away" / "… · in reach" / "… · needs a password" / "Nothing in reach".
	internal static string Nearby(string name, int tiles, bool inReach, bool locked, bool ru)
	{
		if (name == null)
		{
			return ru ? "Рядом ничего нет" : "Nothing in reach";
		}
		string where = locked ? (ru ? "нужен пароль" : "needs a password") : (inReach ? (ru ? "рядом" : "in reach") : Distance(tiles, ru));
		return name + " · " + where;
	}

	internal static string Distance(int tiles, bool ru)
	{
		if (ru)
		{
			return "в " + tiles + " " + ((tiles % 10 == 1 && tiles % 100 != 11) ? "блоке" : "блоках");
		}
		return tiles + ((tiles == 1) ? " tile away" : " tiles away");
	}

	// "4 tiles" / "4 блока" (1 блок, 2-4 блока, 5-20 блоков, 21 блок ...).
	internal static string Tiles(int tiles, bool ru)
	{
		if (!ru)
		{
			return tiles + ((tiles == 1) ? " tile" : " tiles");
		}
		int last = tiles % 10;
		bool teens = tiles % 100 >= 11 && tiles % 100 <= 14;
		return tiles + ((last == 1 && !teens) ? " блок" : ((last >= 2 && last <= 4 && !teens) ? " блока" : " блоков"));
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
