using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook;

internal static class BookRecent
{
	private static readonly Dictionary<string, int> CurrentRecipes = new Dictionary<string, int>(StringComparer.Ordinal);

	internal static int Version => Main.LocalPlayer?.GetModPlayer<BookPlayer>().RecentRecipes.Version ?? 0;

	internal static void Record(Recipe recipe)
	{
		if (recipe != null && recipe.createItem != null && !recipe.createItem.IsAir && !Main.gameMenu && Main.LocalPlayer != null)
		{
			string key = Key(recipe);
			Main.LocalPlayer.GetModPlayer<BookPlayer>().RecentRecipes.Record(key);
		}
	}

	internal static void RefreshIndex()
	{
		CurrentRecipes.Clear();
		for (int index = 0; index < Recipe.numRecipes; index++)
		{
			Recipe recipe = Main.recipe[index];
			if (recipe != null && !recipe.Disabled && recipe.createItem != null && !recipe.createItem.IsAir)
			{
				CurrentRecipes.TryAdd(Key(recipe), index);
			}
		}
	}

	internal static List<int> ResolveRecipeIndexes()
	{
		if (Main.gameMenu || Main.LocalPlayer == null)
		{
			return new List<int>();
		}
		return Main.LocalPlayer.GetModPlayer<BookPlayer>().RecentRecipes.Resolve(CurrentRecipes);
	}

	internal static string Key(Recipe recipe)
	{
		IEnumerable<string> ingredients = from item in recipe.requiredItem
			where item != null && !item.IsAir
			select ItemKey(item) + "#" + item.stack.ToString(CultureInfo.InvariantCulture);
		IEnumerable<string> stations = from tile in recipe.requiredTile
			where tile >= 0
			select TileLoader.GetTile(tile)?.FullName ?? ("Terraria/Tile/" + tile.ToString(CultureInfo.InvariantCulture));
		IEnumerable<string> groups = recipe.acceptedGroups.Select((int id) => (!RecipeGroup.recipeGroups.TryGetValue(id, out var value)) ? "" : string.Join("\u001f", value.ValidItems.Select((int type) => TypeKey(type)).OrderBy((string result) => result, StringComparer.Ordinal)));
		IEnumerable<string> conditions = recipe.Conditions.Select((Condition condition) => condition.Description.Key);
		return BookRecentSignature.Create(recipe.Mod?.Name ?? recipe.createItem.ModItem?.Mod.Name ?? "Terraria", ItemKey(recipe.createItem), recipe.createItem.stack, ingredients, stations, groups, conditions);
		static string ItemKey(Item item)
		{
			return item.ModItem?.FullName ?? TypeKey(item.type);
		}
		static string TypeKey(int type)
		{
			if (!ContentSamples.ItemsByType.TryGetValue(type, out var sample) || sample.ModItem == null)
			{
				return ItemLoader.GetItem(type)?.FullName ?? ("Terraria/" + type.ToString(CultureInfo.InvariantCulture));
			}
			return sample.ModItem.FullName;
		}
	}
}
