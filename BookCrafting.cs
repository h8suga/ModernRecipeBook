using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace ModernRecipeBook;

internal static class BookCrafting
{
	private static readonly HashSet<int> Available = new HashSet<int>();

	private static readonly HashSet<int> NextAvailable = new HashSet<int>();

	private static int Ticks;

	private static int StorageVersion = -1;

	private static bool WasStorage;

	internal static string LastMessage = "";

	internal static int AvailabilityVersion { get; private set; }

	private static bool PlayerReady
	{
		get
		{
			if (Main.LocalPlayer.active && !Main.LocalPlayer.dead && !Main.LocalPlayer.ghost && Main.LocalPlayer.ItemTimeIsZero && Main.LocalPlayer.itemAnimation == 0)
			{
				return !Main.LocalPlayer.HasLockedInventory();
			}
			return false;
		}
	}

	internal static void Clear()
	{
		Available.Clear();
		NextAvailable.Clear();
		Ticks = 0;
		LastMessage = "";
		AvailabilityVersion++;
	}

	internal static void Refresh(bool force = false)
	{
		if (BookStorage.UseForCrafting)
		{
			if (force || !WasStorage || StorageVersion != BookStorage.DataVersion)
			{
				AvailabilityVersion++;
				StorageVersion = BookStorage.DataVersion;
			}
			WasStorage = true;
			return;
		}
		if (WasStorage)
		{
			WasStorage = false;
			force = true;
			AvailabilityVersion++;
		}
		if (!force && Ticks++ % 20 != 0)
		{
			return;
		}
		NextAvailable.Clear();
		if (!Main.gameMenu && !Main.dedServ && Main.LocalPlayer.active && !Main.LocalPlayer.dead && Main.guideItem.IsAir)
		{
			Main.LocalPlayer.AdjTiles();
			Recipe.FindRecipes();
			for (int i = 0; i < Main.numAvailableRecipes; i++)
			{
				NextAvailable.Add(Main.availableRecipe[i]);
			}
		}
		if (BookRules.AvailabilityChanged(Available, NextAvailable))
		{
			Available.Clear();
			Available.UnionWith(NextAvailable);
			AvailabilityVersion++;
		}
	}

	internal static bool IsAvailable(Recipe recipe)
	{
		if (recipe != null && !recipe.Disabled)
		{
			if (!BookStorage.UseForCrafting)
			{
				return Available.Contains(recipe.RecipeIndex);
			}
			return BookStorage.IsAvailable(recipe);
		}
		return false;
	}

	internal static bool CanCraft(Recipe recipe)
	{
		return BookRules.CraftAllowed(recipe != null && !recipe.Disabled, PlayerReady, Main.guideItem.IsAir, Main.mouseItem.IsAir, IsAvailable(recipe));
	}

	internal static string Reason(Recipe recipe, bool ru)
	{
		if (BookStorage.UseForCrafting && recipe != null && BookStorage.CraftInitializing)
		{
			return BookStorage.Status;
		}
		if (recipe == null)
		{
			if (!ru)
			{
				return "No crafting recipe";
			}
			return "Нет рецепта крафта";
		}
		if (!Main.guideItem.IsAir)
		{
			if (!ru)
			{
				return "Empty the Guide slot first";
			}
			return "Сначала освободите слот гида";
		}
		if (!Main.mouseItem.IsAir)
		{
			if (!ru)
			{
				return "Clear the cursor first";
			}
			return "Сначала уберите предмет с курсора";
		}
		if (!PlayerReady)
		{
			if (!ru)
			{
				return "Can't craft right now";
			}
			return "Сейчас крафт недоступен";
		}
		if (!IsAvailable(recipe))
		{
			if (!ru)
			{
				return "Missing materials, station or condition";
			}
			return "Не хватает материалов, станции или условия";
		}
		if (BookStorage.UseForCrafting)
		{
			if (!ru)
			{
				return "Ready · result goes to storage";
			}
			return "Можно создать · результат в хранилище";
		}
		if (!ru)
		{
			return "Ready to craft";
		}
		return "Можно создать";
	}

	internal static void TryCraft(Recipe recipe, bool ru, int batches = 1)
	{
		batches = Math.Clamp(batches, 1, 100);
		if (recipe == null)
		{
			LastMessage = Reason(null, ru);
			return;
		}
		if (BookStorage.UseForCrafting)
		{
			if (recipe == null || !PlayerReady || !Main.guideItem.IsAir || !Main.mouseItem.IsAir || !IsAvailable(recipe))
			{
				LastMessage = Reason(recipe, ru);
			}
			else
			{
				BookStorage.QueueCraft(recipe, ru, batches);
			}
			return;
		}
		Player player = Main.LocalPlayer;
		int crafted = 0;
		string stopReason = "";
		for (int batch = 0; batch < batches; batch++)
		{
			if (!SafeForNextBatch(recipe, player))
			{
				if (crafted == 0)
				{
					LastMessage = Reason(recipe, ru);
					return;
				}
				stopReason = (ru ? "Крафт остановлен: что-то изменилось" : "Crafting stopped: something changed");
				break;
			}
			Refresh(force: true);
			if (!CanCraft(recipe))
			{
				if (crafted == 0)
				{
					LastMessage = Reason(recipe, ru);
					return;
				}
				stopReason = (ru ? "Остановлено: кончились материалы" : "Stopped: out of materials");
				break;
			}
			Main.CraftItem(recipe);
			if (Main.mouseItem.IsAir || Main.mouseItem.type != recipe.createItem.type)
			{
				stopReason = (ru ? "Крафт не удался" : "Crafting failed");
				break;
			}
			crafted++;
			if (!Main.mouseItem.IsAir)
			{
				Main.mouseItem = player.GetItem(Main.myPlayer, Main.mouseItem, GetItemSettings.InventoryUIToInventorySettingsShowAsNew);
				bool flag = !Main.mouseItem.IsAir;
				if (flag)
				{
					Main.playerInventory = true;
				}
				if (flag)
				{
					stopReason = (ru ? "Инвентарь полон: результат на курсоре" : "Inventory full: result on cursor");
					break;
				}
			}
		}
		RecipeBookSystem.InvalidateBatchEstimate();
		Refresh(force: true);
		if (crafted == 0)
		{
			LastMessage = ((stopReason.Length > 0) ? stopReason : Reason(recipe, ru));
			return;
		}
		string completion = ((crafted == 1 && batches == 1) ? ((ru ? "Создано: " : "Crafted: ") + recipe.createItem.Name) : ((ru ? $"Создано {crafted}/{batches} · " : $"Crafted {crafted}/{batches} · ") + recipe.createItem.Name));
		LastMessage = ((stopReason.Length > 0) ? (completion + " · " + stopReason) : completion);
	}

	private static bool SafeForNextBatch(Recipe recipe, Player player)
	{
		int index = recipe?.RecipeIndex ?? (-1);
		if (!Main.gameMenu && !Main.dedServ && player == Main.LocalPlayer && player.active && !player.dead && !player.ghost && player.ItemTimeIsZero && player.itemAnimation == 0 && !player.HasLockedInventory() && Main.guideItem.IsAir && Main.mouseItem.IsAir && !BookStorage.UseForCrafting && recipe != null && !recipe.Disabled && index >= 0 && index < Recipe.numRecipes)
		{
			return Main.recipe[index] == recipe;
		}
		return false;
	}

	internal static IEnumerable<Item> Materials()
	{
		Player player = Main.LocalPlayer;
		foreach (Item item in player.inventory.Take(58))
		{
			yield return item;
		}
		if (player.useVoidBag() && player.chest != -5)
		{
			foreach (Item item2 in player.bank4.item.Take(40))
			{
				yield return item2;
			}
		}
		int chest = player.chest;
		Item[] array;
		if (chest < 0)
		{
			switch (chest)
			{
			case -2:
				break;
			case -3:
				goto IL_01ae;
			case -4:
				goto IL_01c2;
			case -5:
				goto IL_01d6;
			default:
				goto IL_01ea;
			}
			array = player.bank.item;
		}
		else
		{
			if (player.chest >= Main.chest.Length)
			{
				goto IL_01ea;
			}
			array = Main.chest[player.chest]?.item;
		}
		goto IL_01ed;
		IL_01ae:
		array = player.bank2.item;
		goto IL_01ed;
		IL_01d6:
		array = player.bank4.item;
		goto IL_01ed;
		IL_01c2:
		array = player.bank3.item;
		goto IL_01ed;
		IL_01ed:
		Item[] chest2 = array;
		if (chest2 != null)
		{
			foreach (Item item3 in chest2.Take(40))
			{
				yield return item3;
			}
		}
		foreach (var moddedCraftingMaterial in PlayerLoader.GetModdedCraftingMaterials(player))
		{
			foreach (Item item4 in moddedCraftingMaterial.Item1)
			{
				yield return item4;
			}
		}
		yield break;
		IL_01ea:
		array = null;
		goto IL_01ed;
	}
}
