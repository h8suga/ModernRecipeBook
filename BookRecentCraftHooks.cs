using System;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.ModLoader;

namespace ModernRecipeBook;

public sealed class BookRecentCraftHooks : ModSystem
{
	[ThreadStatic]
	private static Recipe StorageRecipe;

	[ThreadStatic]
	private static bool StorageOutputObserved;

	public override void Load()
	{
		if (!Main.dedServ)
		{
			On_Main.CraftItem += AfterInventoryCraft;
			On_AchievementsHelper.NotifyItemCraft += ObserveStorageOutput;
		}
	}

	public override void Unload()
	{
		On_Main.CraftItem -= AfterInventoryCraft;
		On_AchievementsHelper.NotifyItemCraft -= ObserveStorageOutput;
		StorageRecipe = null;
		StorageOutputObserved = false;
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

	private static void ObserveStorageOutput(On_AchievementsHelper.orig_NotifyItemCraft orig, Recipe recipe)
	{
		orig(recipe);
		if (recipe == StorageRecipe)
		{
			StorageOutputObserved = true;
		}
	}

	internal static void BeginStorageCraft(Recipe recipe)
	{
		StorageRecipe = recipe;
		StorageOutputObserved = false;
	}

	internal static void EndStorageCraft(bool completed)
	{
		Recipe recipe = StorageRecipe;
		bool observed = StorageOutputObserved;
		StorageRecipe = null;
		StorageOutputObserved = false;
		if ((completed & observed) && Main.netMode == 0)
		{
			BookRecent.Record(recipe);
		}
	}
}
