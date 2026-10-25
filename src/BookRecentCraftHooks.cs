using Terraria;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// Every craft the player or the book makes goes through Main.CraftItem (the book pulls nearby materials into the
// inventory first), so one hook records Recent.
public sealed class BookRecentCraftHooks : ModSystem
{
	// The local player's crafts: a dedicated server never loads it.
	public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

	public override void Load()
	{
		On_Main.CraftItem += AfterInventoryCraft;
	}

	public override void Unload()
	{
		On_Main.CraftItem -= AfterInventoryCraft;
	}

	private static void AfterInventoryCraft(On_Main.orig_CraftItem orig, Recipe recipe)
	{
		bool isAir = Main.mouseItem.IsAir;
		orig(recipe);
		if (isAir && recipe != null && !Main.mouseItem.IsAir && Main.mouseItem.type == recipe.createItem.type)
		{
			BookRecent.Record(recipe);
		}
	}
}
