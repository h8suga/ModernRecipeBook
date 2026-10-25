using System;
using Terraria;

namespace ModernRecipeBook;

internal sealed record BookCategory(string Key, string Russian, string English, int IconType, Func<Item, bool> Matches)
{
	internal string Label(bool russian)
	{
		if (!russian)
		{
			return English;
		}
		return Russian;
	}
}
