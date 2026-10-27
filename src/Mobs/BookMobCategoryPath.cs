using System;
using System.Collections.Generic;

namespace ModernRecipeBook.Mobs;

// Where a Bestiary entry sits in the Bestiary tab's sidebar, for the quick search's rows: what it is (its kind's row:
// Bosses, Enemies, Critters...) and where it lives (the biome listing the fewest entries: Sunken Sea rather than
// Underground; an event only when no biome lists it, so a Bunny lives on the surface, not at Halloween), as
// BookCategoryPath names an item's place. Either may be missing.
internal static class BookMobCategoryPath
{
	internal static IReadOnlyList<BookBestiary.Tag> Of(int mobIndex)
	{
		BookBestiary.Tag kind = null;
		BookBestiary.Tag place = null;
		foreach (BookBestiary.Tag tag in BookBestiary.Tags)
		{
			if (tag.Key == "all" || tag.Key == BookBestiary.RecentKey || !tag.Members.Contains(mobIndex))
			{
				continue;
			}
			if (tag.Key.StartsWith("kind:", StringComparison.Ordinal))
			{
				kind = tag;
			}
			else if (place == null || ((Biome(tag) != Biome(place)) ? Biome(tag) : (tag.Members.Count < place.Members.Count)))
			{
				place = tag;
			}
		}
		List<BookBestiary.Tag> path = new List<BookBestiary.Tag>();
		if (kind != null)
		{
			path.Add(kind);
		}
		if (place != null)
		{
			path.Add(place);
		}
		return path;
	}

	// BookBestiary's keys: the game's biomes ("tag:Bestiary_Biomes.Jungle") and mods' ("biome:CalamityMod/..."); the rest
	// are events and invasions.
	private static bool Biome(BookBestiary.Tag tag)
	{
		return tag.Key.StartsWith("tag:Bestiary_Biomes.", StringComparison.Ordinal) || tag.Key.StartsWith("biome:", StringComparison.Ordinal);
	}
}
