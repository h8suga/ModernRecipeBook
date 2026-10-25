using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using static ModernRecipeBook.BookText;

namespace ModernRecipeBook;

// Craftable filter (Items and Recipes tabs): only what the player can craft right now, as the card's Used in
// "Craftable" toggle means it (BookCrafting.IsAvailable: materials from the inventory and nearby storage, stations in
// range). An item stays when one of its recipes is available, a merged recipe when one of its members is. Kept per tab.
public sealed partial class RecipeBookSystem
{
	private static bool CraftableOnly;

	// How many entries it alone hid last pass.
	private static int CraftableHidden;

	private static (int Version, int Availability, int Count) CraftableListed = (-1, -1, 0);

	// The tabs the station filter has.
	private static bool CraftableFilterTab => StationFilterTab;

	private static bool CraftableFilterActive => CraftableFilterTab && CraftableOnly;

	// `index` is what the tab lists: an item type on Items, a recipe index on Recipes.
	private static bool CraftableNow(int index)
	{
		if (Catalogue)
		{
			if (RecipesByItem.TryGetValue(index, out var recipes))
			{
				foreach (int recipe in recipes)
				{
					if (BookCrafting.IsAvailable(Main.recipe[recipe]))
					{
						return true;
					}
				}
			}
			return false;
		}
		BookRecipeVariants.Variant variant = BookRecipeVariants.Of(index);
		if (variant == null)
		{
			return BookCrafting.IsAvailable(Main.recipe[index]);
		}
		foreach (int member in variant.Members)
		{
			if (BookCrafting.IsAvailable(Main.recipe[member]))
			{
				return true;
			}
		}
		return false;
	}

	// With the filter off: how many listed entries it would keep; counted on hover, again only when the list or what
	// can be made changed.
	private static int CraftableInList()
	{
		(int, int) key = (StationCandidatesVersion, BookCrafting.AvailabilityVersion);
		if ((CraftableListed.Version, CraftableListed.Availability) != key)
		{
			int count = 0;
			foreach (int index in Filtered)
			{
				if (CraftableNow(index))
				{
					count++;
				}
			}
			CraftableListed = (key.Item1, key.Item2, count);
		}
		return CraftableListed.Count;
	}

	private static string CraftableButtonTip()
	{
		string title = L("Craftable.Craftable") + "\n" + L("Craftable.OnlyWhatYouCanCraftRight") + "\n";
		if (!CraftableOnly)
		{
			return title + L("Craftable.CraftableNowOf", Count(CraftableInList()), Count(Filtered.Count));
		}
		return title + L("Craftable.CraftableNowHidden", Count(Filtered.Count), Count(CraftableHidden)) + "\n" + L("Craftable.ClickAgainToShowEverything");
	}

	// A hammer, as on vanilla's crafting window toggle, beside the station filter's anvil.
	private static void DrawCraftableButton(SpriteBatch sb)
	{
		BookRect rect = BookGeometry.CraftableFilter;
		bool hover = IconButtonFrame(sb, rect, CraftableOnly);
		if (ContentSamples.ItemsByType.TryGetValue(ItemID.IronHammer, out var hammer))
		{
			Icon(sb, hammer, rect.X + 6f, rect.Y + 6f, 24f, tooltip: false);
		}
		if (hover)
		{
			MouseTip(CraftableButtonTip());
		}
	}
}
