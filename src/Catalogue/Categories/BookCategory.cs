using System;
using Terraria;

namespace ModernRecipeBook.Catalogue.Categories;

// Text gives the label in the current language: an item category reads its localization key, a Bestiary tag the
// game's own name for it.
// LabelKey: the localization key Text reads, "" for a Bestiary tag (the quick search reads the English label by it).
internal sealed record BookCategory(string Key, Func<string> Text, int IconType, Func<Item, bool> Matches, string LabelKey = "")
{
	internal string Label()
	{
		return Text();
	}
}
