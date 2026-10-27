using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ModernRecipeBook.Recent;

// Every craft ends in RecipeLoader.OnCraft, which hands the result to OnCreated with the recipe
// (TML:Terraria.ModLoader/RecipeLoader.cs:220-224): vanilla's crafts (Main.cs:40835), the book's (which pull nearby
// materials into the inventory and craft through Main.CraftItem), a craft onto a stack already on the cursor, and Magic
// Storage's crafting window, which never calls Main.CraftItem (MS:MagicStorage/CraftingGUI.cs:1084-1115, 1191-1194).
// Crafts run on the crafting player's own game only, so each one here is the local player's. Only the recipe's own item
// counts as made: MS hands its extra drops to OnCraft with the same recipe (ExtraCraftItemsSystem,
// MS:MagicStorage/CraftingGUI.cs:1109-1112, 1188-1191).
public sealed class BookRecentCraftHooks : GlobalItem
{
	// The local player's crafts: a dedicated server never loads it.
	public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

	public override void OnCreated(Item item, ItemCreationContext context)
	{
		if (context is RecipeItemCreationContext craft)
		{
			BookRecent.Record(craft.Recipe, (item.type == craft.Recipe.createItem.type) ? item.stack : 0);
		}
	}
}
