using System.Collections.Generic;
using Terraria;

namespace ModernRecipeBook.Sources;

// Curated sources no drop rule, shop or recipe names (world generation loot, quest rewards, mod mechanics). Each area
// keeps its tables in its own file and adds one line to each method here: Sources go into the catalogue
// (BookItemSources.Index), Steps and Routes place the items in the progression (BookProgression), Entry turns a source
// of the area's own kind into its How to Get row (null: not this area's), Hidden keeps placeholder items (a mod's UI
// icons, debug items) out of the catalogue.
internal static class BookCurated
{
	internal static IEnumerable<BookItemSource> Sources()
	{
		foreach (BookItemSource source in BookWorldLoot.Sources()) yield return source;
		yield break;
	}

	// An item available from that step on (BookSteps index), with the reason the progression audit shows.
	internal static IEnumerable<(int Item, int Step, string Why)> Steps()
	{
		foreach (var step in BookWorldLoot.Steps()) yield return step;
		yield break;
	}

	// An item that comes from any of each input group (one of every inner array), never before Floor.
	internal static IEnumerable<(int Item, int[][] Inputs, int Floor, string Why)> Routes()
	{
		foreach (var route in BookWorldLoot.Routes()) yield return route;
		yield break;
	}

	internal static BookAcquisitionEntry Entry(BookItemSource s)
	{
		return BookWorldLoot.Entry(s);
	}

	// The kind's word on a How to Get row ("Chest", "Quest"), for the areas' own kinds; null for any other kind.
	internal static string KindName(BookAcquisitionKind kind)
	{
		return BookWorldLoot.KindName(kind);
	}

	internal static bool Hidden(Item item)
	{
		return false;
	}
}
