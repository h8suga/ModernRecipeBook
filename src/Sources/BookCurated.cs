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
		foreach (BookItemSource source in BookStructures.Sources()) yield return source;
		foreach (BookItemSource source in BookWorldLoot.Sources()) yield return source;
		foreach (BookItemSource source in BookChestLoot.Sources()) yield return source;
		foreach (BookItemSource source in BookCalamityLoot.Sources()) yield return source;
		foreach (BookItemSource source in BookRewardSources.Sources()) yield return source;
	}

	// An item available from that step on (BookSteps index), with the reason the progression audit shows.
	internal static IEnumerable<(int Item, int Step, string Why)> Steps()
	{
		foreach (var step in BookStructures.Steps()) yield return step;
		foreach (var step in BookWorldLoot.Steps()) yield return step;
		foreach (var step in BookChestLoot.Steps()) yield return step;
		foreach (var step in BookCalamityLoot.Steps()) yield return step;
		foreach (var step in BookRewardSources.Steps()) yield return step;
	}

	// An item that comes from any of each input group (one of every inner array), never before Floor.
	internal static IEnumerable<(int Item, int[][] Inputs, int Floor, string Why)> Routes()
	{
		foreach (var route in BookStructures.Routes()) yield return route;
		foreach (var route in BookWorldLoot.Routes()) yield return route;
		foreach (var route in BookChestLoot.Routes()) yield return route;
		foreach (var route in BookCalamityLoot.Routes()) yield return route;
		foreach (var route in BookRewardSources.Routes()) yield return route;
	}

	internal static BookAcquisitionEntry Entry(BookItemSource s)
	{
		return BookStructures.Entry(s) ?? BookWorldLoot.Entry(s) ?? BookChestLoot.Entry(s) ?? BookCalamityLoot.Entry(s) ?? BookRewardSources.Entry(s);
	}

	// The kind's word on a How to Get row ("Chest", "Quest"), for the areas' own kinds; null for any other kind.
	internal static string KindName(BookAcquisitionKind kind)
	{
		return BookStructures.KindName(kind) ?? BookWorldLoot.KindName(kind) ?? BookCalamityLoot.KindName(kind) ?? BookRewardSources.KindName(kind);
	}

	internal static bool Hidden(Item item)
	{
		return BookCalamityLoot.Hidden(item) || BookRewardSources.Hidden(item);
	}
}
