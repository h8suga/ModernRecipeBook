using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.Catalogue.Recipes;

// Recipes of one item that are the same but for one ingredient (Shadow Scale or Tissue Sample, Copper or Tin Bar)
// are one variant of it: same result stack, stations, conditions and recipe groups, the other ingredients in the
// same amounts, and the same amount of the differing one. Exact duplicates join too. An ingredient taken through a
// recipe group ("Any Wood") is never the differing one: the game already shows that as one recipe. Rebuilt with
// the recipe index.
internal static class BookRecipeVariants
{
	internal sealed class Variant
	{
		// Recipe indexes, ascending; the first one stands for the variant in lists.
		internal int[] Members;

		// Per member: the requiredItem slot of the differing ingredient, -1 when there is no alternative.
		internal int[] Slots;

		// The differing ingredient's item types, distinct, in member order; empty when there is no alternative.
		internal int[] Alternatives;

		internal int Representative => Members[0];
	}

	private static readonly List<Variant> None = new List<Variant>();

	private static readonly Dictionary<int, Variant> byRecipe = new Dictionary<int, Variant>();

	private static readonly Dictionary<int, List<Variant>> byItem = new Dictionary<int, List<Variant>>();

	internal static Variant Of(int recipe)
	{
		return byRecipe.GetValueOrDefault(recipe);
	}

	internal static int Representative(int recipe)
	{
		return Of(recipe)?.Representative ?? recipe;
	}

	// Variants of an item in the order of their first recipe. Never modify the returned list.
	internal static List<Variant> For(int type)
	{
		return byItem.GetValueOrDefault(type) ?? None;
	}

	// The alternatives a member takes in its differing slot, or null, and which slot that is.
	internal static int[] Alternatives(int recipe, out int slot)
	{
		Variant variant = Of(recipe);
		int member = (variant == null) ? (-1) : Array.IndexOf(variant.Members, recipe);
		slot = (member < 0) ? (-1) : variant.Slots[member];
		if (slot < 0)
		{
			return null;
		}
		return variant.Alternatives;
	}

	internal static void Clear()
	{
		byRecipe.Clear();
		byItem.Clear();
	}

	internal static void Build(Dictionary<int, List<int>> recipesByItem)
	{
		Stopwatch timer = Stopwatch.StartNew();
		Clear();
		int failures = 0;
		foreach (KeyValuePair<int, List<int>> pair in recipesByItem)
		{
			List<Variant> variants;
			try
			{
				variants = Group(pair.Value);
			}
			catch
			{
				variants = pair.Value.Select(Single).ToList();
				failures++;
			}
			byItem[pair.Key] = variants;
			foreach (Variant variant in variants)
			{
				foreach (int member in variant.Members)
				{
					byRecipe[member] = variant;
				}
			}
		}
		Audit(timer.ElapsedMilliseconds, failures);
	}

	private static Variant Single(int recipe)
	{
		return new Variant
		{
			Members = new int[1] { recipe },
			Slots = new int[1] { -1 },
			Alternatives = Array.Empty<int>()
		};
	}

	private static List<Variant> Group(List<int> recipes)
	{
		Dictionary<int, Variant> assigned = new Dictionary<int, Variant>();
		if (recipes.Count > 1)
		{
			// Every way to read a recipe as "the rest + one ingredient": recipes with the same rest and the same amount
			// of that ingredient land in one bucket.
			Dictionary<string, List<(int Recipe, int Slot, int Type)>> buckets = new Dictionary<string, List<(int, int, int)>>(StringComparer.Ordinal);
			foreach (int index in recipes)
			{
				Recipe recipe = Main.recipe[index];
				string common = Common(recipe);
				int[] slots = (from slot in Enumerable.Range(0, recipe.requiredItem.Count)
					where !recipe.requiredItem[slot].IsAir
					select slot).ToArray();
				foreach (int slot in slots)
				{
					Item ingredient = recipe.requiredItem[slot];
					if (recipe.acceptedGroups.Any((int id) => RecipeGroup.recipeGroups.TryGetValue(id, out var group) && group.ValidItems.Contains(ingredient.type)))
					{
						continue;
					}
					string rest = string.Join(",", from other in slots
						where other != slot
						select recipe.requiredItem[other].type + "x" + recipe.requiredItem[other].stack into entry
						orderby entry
						select entry);
					string key = common + "|" + rest + "|" + ingredient.stack;
					if (!buckets.TryGetValue(key, out var bucket))
					{
						bucket = (buckets[key] = new List<(int, int, int)>());
					}
					if (!bucket.Any(((int Recipe, int Slot, int Type) e) => e.Recipe == index))
					{
						bucket.Add((index, slot, ingredient.type));
					}
				}
			}
			// Real alternatives first, then exact duplicates; a recipe joins one variant only.
			foreach (List<(int Recipe, int Slot, int Type)> bucket in from b in buckets.Values
				where b.Count > 1
				orderby b.Select(((int Recipe, int Slot, int Type) e) => e.Type).Distinct().Count() descending, b.Count descending, b[0].Recipe
				select b)
			{
				List<(int Recipe, int Slot, int Type)> free = bucket.Where(((int Recipe, int Slot, int Type) e) => !assigned.ContainsKey(e.Recipe)).ToList();
				if (free.Count < 2)
				{
					continue;
				}
				int[] alternatives = free.Select(((int Recipe, int Slot, int Type) e) => e.Type).Distinct().ToArray();
				bool alternative = alternatives.Length > 1;
				Variant variant = new Variant
				{
					Members = free.Select(((int Recipe, int Slot, int Type) e) => e.Recipe).ToArray(),
					Slots = free.Select(((int Recipe, int Slot, int Type) e) => alternative ? e.Slot : (-1)).ToArray(),
					Alternatives = (alternative ? alternatives : Array.Empty<int>())
				};
				foreach ((int Recipe, int Slot, int Type) e in free)
				{
					assigned[e.Recipe] = variant;
				}
			}
		}
		List<Variant> result = new List<Variant>();
		foreach (int index in recipes)
		{
			if (!assigned.TryGetValue(index, out var variant))
			{
				result.Add(Single(index));
			}
			else if (variant.Representative == index)
			{
				result.Add(variant);
			}
		}
		return result;
	}

	// What must match besides the ingredients: result stack, stations, conditions (as the player reads them) and
	// the recipe groups the recipe accepts.
	private static string Common(Recipe recipe)
	{
		return recipe.createItem.stack + "|" + string.Join(",", from tile in recipe.requiredTile
			where tile >= 0
			orderby tile
			select tile) + "|" + string.Join("\u001f", from condition in recipe.Conditions
			select condition.Description.Value into text
			orderby text
			select text) + "|" + string.Join(",", recipe.acceptedGroups.OrderBy((int id) => id));
	}

	private static void Audit(long milliseconds, int failures)
	{
		List<Variant> variants = byItem.Values.SelectMany((List<Variant> list) => list).ToList();
		List<Variant> merged = variants.Where((Variant v) => v.Members.Length > 1).ToList();
		int alternatives = merged.Count((Variant v) => v.Alternatives.Length > 1);
		// Self-check: every member differs from the first one in its own slot at most.
		int broken = merged.Count((Variant v) => !OneSlotApart(v));
		List<string> checks = new List<string>();
		foreach (int type in new int[4] { ItemID.ObsidianHelm, ItemID.ObsidianShirt, ItemID.ObsidianPants, ItemID.NightsEdge })
		{
			Variant found = For(type).FirstOrDefault((Variant v) => v.Alternatives.Length > 1);
			checks.Add(Lang.GetItemNameValue(type) + ((found == null) ? " NOT merged" : (" " + found.Members.Length + "→1 (" + string.Join(" | ", found.Alternatives.Select(Lang.GetItemNameValue)) + ")")));
		}
		Variant widest = merged.OrderByDescending((Variant v) => v.Alternatives.Length).FirstOrDefault();
		if (widest != null)
		{
			checks.Add("widest " + Main.recipe[widest.Representative].createItem.Name + " (" + string.Join(" | ", widest.Alternatives.Select(Lang.GetItemNameValue)) + ")");
		}
		// Recipes of one item kept apart although stations, conditions and the number of ingredients match: two or
		// more ingredients differ (versions that swap bars and scales together), or the differing amounts do.
		List<string> apart = (from pair in byItem
			where pair.Value.Count > 1
			let reps = pair.Value.Select((Variant v) => Main.recipe[v.Representative]).ToList()
			where reps.Skip(1).Any((Recipe r) => Common(r) == Common(reps[0]) && Count(r) == Count(reps[0]))
			select Lang.GetItemNameValue(pair.Key)).Take(4).ToList();
		ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"Recipe variants audit ({milliseconds} ms): {merged.Sum((Variant v) => v.Members.Length)} recipes merged into {merged.Count} variants ({alternatives} with alternatives, {merged.Count - alternatives} exact duplicates); {variants.Count} variants for {byRecipe.Count} recipes; {broken} broken, {failures} items failed. Spot checks: {string.Join("; ", checks)}. Kept apart (same stations, more differs): {((apart.Count == 0) ? "none" : string.Join(", ", apart))}.");
		static int Count(Recipe r)
		{
			return r.requiredItem.Count((Item i) => !i.IsAir);
		}
	}

	private static bool OneSlotApart(Variant variant)
	{
		Recipe first = Main.recipe[variant.Representative];
		for (int m = 1; m < variant.Members.Length; m++)
		{
			Recipe other = Main.recipe[variant.Members[m]];
			if (Common(other) != Common(first) || (variant.Slots[0] >= 0 && first.requiredItem[variant.Slots[0]].stack != other.requiredItem[variant.Slots[m]].stack))
			{
				return false;
			}
			List<string> left = Entries(first, variant.Slots[0]);
			List<string> right = Entries(other, variant.Slots[m]);
			if (!left.SequenceEqual(right))
			{
				return false;
			}
		}
		return true;
		static List<string> Entries(Recipe recipe, int skip)
		{
			return (from slot in Enumerable.Range(0, recipe.requiredItem.Count)
				where slot != skip && !recipe.requiredItem[slot].IsAir
				select recipe.requiredItem[slot].type + "x" + recipe.requiredItem[slot].stack into entry
				orderby entry
				select entry).ToList();
		}
	}
}
