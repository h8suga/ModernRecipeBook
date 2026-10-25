using System;
using Terraria;

namespace ModernRecipeBook.Catalogue.Categories;

// Text gives the label in the current language: an item category reads its localization key, a Bestiary tag the
// game's own name for it.
internal sealed record BookCategory(string Key, Func<string> Text, int IconType, Func<Item, bool> Matches)
{
	internal string Label()
	{
		return Text();
	}
}
