using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;

namespace ModernRecipeBook.Catalogue.Categories;

// Where an item sits in the sidebar, for its card: the most specific category saying what it is, the one listing the
// fewest items (Whips rather than All weapons, Bars rather than Furniture), a tag row (Tags) only when nothing else
// lists it, after the row heading its group when the item is in that one too ("Weapons › Whips"). An item only All
// items lists has none.
// The categories and their sizes do not change once the game has loaded, so each is counted once.
internal static class BookCategoryPath
{
	// The rows under each group's heading row in the sidebar (BookCategories), and Wings, Minecarts, Ores, Bars and
	// Gems under the row they narrow.
	private static readonly Dictionary<string, string> Parents = new Dictionary<string, string>
	{
		["swords"] = "weapons", ["yoyos"] = "weapons", ["bows"] = "weapons", ["guns"] = "weapons", ["launchers"] = "weapons", ["summon"] = "weapons", ["whips"] = "weapons", ["sentries"] = "weapons",
		["picks"] = "tools", ["axes"] = "tools", ["hammers"] = "tools",
		["head"] = "armor", ["body"] = "armor", ["legs"] = "armor",
		["wings"] = "accessories",
		["arrows"] = "ammo", ["bullets"] = "ammo", ["rockets"] = "ammo",
		["blocks"] = "tiles", ["platforms"] = "tiles", ["furniture"] = "tiles", ["stations"] = "tiles", ["chests"] = "tiles", ["doors"] = "tiles", ["chairs"] = "tiles", ["tables"] = "tiles", ["lights"] = "tiles", ["torches"] = "tiles", ["wiring"] = "tiles",
		["healing"] = "potions", ["mana"] = "potions", ["buffs"] = "potions",
		["carts"] = "mounts",
		["ores"] = "materials", ["bars"] = "materials", ["gems"] = "materials"
	};

	// The rows that repeat a tooltip's tag line (Material, Consumable, Vanity, Expert, Master) or say what the item goes
	// in (Extractinator), not what it is: the card names one only when no other row lists the item, so Worm Scarf is
	// an accessory before an Expert item and a Master Mode pet a pet.
	private static readonly HashSet<string> Tags = new HashSet<string> { "materials", "consumables", "vanity", "expert", "master", "extractinator" };

	// A heading row names its group in a path: Weapons, not All weapons.
	private static readonly Dictionary<string, string> GroupNames = new Dictionary<string, string>
	{
		["weapons"] = "CardCategory.Weapons",
		["tools"] = "CardCategory.Tools",
		["armor"] = "CardCategory.Armor",
		["ammo"] = "CardCategory.Ammo",
		["tiles"] = "CardCategory.Placeable",
		["potions"] = "CardCategory.Potions"
	};

	private static List<BookCategory> Categories;

	// The catalogue's items (RecipeBookSystem.Rebuild), and how many of them each category lists, -1 until an item
	// in it asks: counting all of them at once took 165 ms with Calamity, one category takes a few.
	private static Item[] Listed;

	private static int[] Sizes;

	private static readonly Dictionary<int, IReadOnlyList<BookCategory>> Paths = new Dictionary<int, IReadOnlyList<BookCategory>>();

	// The group first, the category last; the categories keep their sidebar keys.
	internal static IReadOnlyList<BookCategory> Of(int itemType)
	{
		if (Paths.TryGetValue(itemType, out IReadOnlyList<BookCategory> path))
		{
			return path;
		}
		if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item item) || item.IsAir)
		{
			return Array.Empty<BookCategory>();
		}
		Build();
		int best = -1;
		for (int c = 0; c < Categories.Count; c++)
		{
			if (Categories[c].Key != "all" && Categories[c].Matches(item) && (best < 0 || Before(c, best)))
			{
				best = c;
			}
		}
		List<BookCategory> result = new List<BookCategory>();
		if (best >= 0)
		{
			BookCategory group = Parents.TryGetValue(Categories[best].Key, out string parent) ? Categories.Find((BookCategory category) => category.Key == parent) : null;
			if (group != null && group.Matches(item))
			{
				result.Add(Named(group));
			}
			result.Add(Named(Categories[best]));
		}
		Paths[itemType] = result;
		return result;
	}

	// A category under its group's heading, as a card names it: "Potions › Buff potions" (the quick search's reason).
	internal static string Path(BookCategory category, IReadOnlyList<BookCategory> categories)
	{
		BookCategory group = Parents.TryGetValue(category.Key, out string parent) ? categories.FirstOrDefault((BookCategory c) => c.Key == parent) : null;
		return (group == null) ? Named(category).Label() : (Named(group).Label() + " › " + Named(category).Label());
	}

	private static BookCategory Named(BookCategory category)
	{
		if (!GroupNames.TryGetValue(category.Key, out string key))
		{
			return category;
		}
		return category with { Text = () => L(key) };
	}

	// The book's own lists (RecipeBookSystem.Rebuild): Stations from the enabled recipes, the items with a name.
	private static void Build()
	{
		if (Categories != null)
		{
			return;
		}
		HashSet<int> stations = new HashSet<int>(from recipe in Main.recipe.Take(Recipe.numRecipes)
			where recipe?.createItem != null && !recipe.createItem.IsAir && !recipe.Disabled
			from tile in recipe.requiredTile
			where tile >= 0
			select tile);
		Listed = ContentSamples.ItemsByType.Values.Where((Item item) => item != null && !item.IsAir && !string.IsNullOrWhiteSpace(item.Name) && !BookCurated.Hidden(item)).ToArray();
		Categories = BookCategories.Create(stations);
		Sizes = Enumerable.Repeat(-1, Categories.Count).ToArray();
	}

	// What the item is before a tag, then the one listing fewer items.
	private static bool Before(int category, int than)
	{
		bool tag = Tags.Contains(Categories[category].Key);
		if (tag != Tags.Contains(Categories[than].Key))
		{
			return !tag;
		}
		return Size(category) < Size(than);
	}

	private static int Size(int category)
	{
		if (Sizes[category] < 0)
		{
			Sizes[category] = Listed.Count(Categories[category].Matches);
		}
		return Sizes[category];
	}
}
