using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;

namespace ModernRecipeBook;

// Merged recipes (BookRecipeVariants) in the card: one variant per group, its differing ingredient as one
// "A or B" row. The card shows and crafts one member at a time, so every number matches what one press does.
public sealed partial class RecipeBookSystem
{
	// The member the card shows and crafts: one that can be crafted now, then the one whose alternative you have
	// most of, then the first. Batches stay within that member (a craft cannot mix two kinds); when one alternative
	// runs out, the card moves on to the next by itself.
	private static Recipe ActiveMember(int index)
	{
		BookRecipeVariants.Variant variant = BookRecipeVariants.Of(index);
		if (variant == null || variant.Members.Length == 1)
		{
			return Main.recipe[index];
		}
		int best = 0;
		bool bestAvailable = false;
		int bestOwned = -1;
		for (int m = 0; m < variant.Members.Length; m++)
		{
			Recipe recipe = Main.recipe[variant.Members[m]];
			bool available = BookCrafting.IsAvailable(recipe);
			int owned = (variant.Slots[m] < 0) ? 0 : Owned(new HashSet<int> { recipe.requiredItem[variant.Slots[m]].type });
			if ((available && !bestAvailable) || (available == bestAvailable && owned > bestOwned))
			{
				best = m;
				bestAvailable = available;
				bestOwned = owned;
			}
		}
		return Main.recipe[variant.Members[best]];
	}

	// "Tissue Sample or Shadow Scale", "A, B or C", the shown one first.
	private static string AlternativesName(int[] types, int shown)
	{
		string[] names = Ordered(types, shown).Select((int type) => ContentSamples.ItemsByType[type].Name).ToArray();
		if (names.Length < 2)
		{
			return names.FirstOrDefault() ?? "";
		}
		return string.Join(", ", names.Take(names.Length - 1)) + T(" или ", " or ") + names[^1];
	}

	// The row label: every name when it fits, then names that end in the same word with that word once ("Demonite or
	// Crimtane Bar"), then "Tissue Sample or 1 more", then "Tissue Sample or...". The row tooltip has the full list.
	private static string AlternativesLabel(int[] types, int shown, float width)
	{
		string full = AlternativesName(types, shown);
		if (TextWidth(full, 0.85f) <= width)
		{
			return full;
		}
		string[] names = Ordered(types, shown).Select((int type) => ContentSamples.ItemsByType[type].Name).ToArray();
		string last = names[0].Split(' ')[^1];
		if (names.All((string name) => name.Contains(' ') && name.Split(' ')[^1] == last))
		{
			string[] stems = names.Select((string name) => name[..name.LastIndexOf(' ')]).ToArray();
			string shared = string.Join(", ", stems.Take(stems.Length - 1)) + T(" или ", " or ") + stems[^1] + " " + last;
			if (TextWidth(shared, 0.85f) <= width)
			{
				return shared;
			}
		}
		string more = names[0] + T(" или ещё ", " or ") + (names.Length - 1) + T("", " more");
		return (TextWidth(more, 0.85f) <= width) ? more : (names[0] + T(" или...", " or..."));
	}

	private static int[] Ordered(int[] types, int shown)
	{
		return new int[1] { shown }.Concat(types.Where((int type) => type != shown)).ToArray();
	}

	// One line per alternative with its own have/need. A long list (Seafood Dinner takes any of 22 fish) keeps
	// lines for the ones on hand and sums up the rest; the tooltip's first line already names them all.
	private static string AlternativesTooltip(MaterialRequirement requirement, int batches)
	{
		var rows = Ordered(requirement.Alternatives, requirement.Ingredient.type).Select(delegate(int type)
		{
			HashSet<int> types = new HashSet<int> { type };
			BookMaterialRules.Assessment assessment = BookMaterialRules.Evaluate(requirement.Ingredient.stack, batches, Owned(types), BookStorage.UseForCrafting && BookStorage.IsInfinite(types), overlapping: false);
			return (Line: ContentSamples.ItemsByType[type].Name + " " + MaterialSub(assessment), Owned: assessment.Infinite || assessment.Available > 0, Ratio: MaterialSub(assessment));
		}).ToList();
		if (rows.Count <= 4)
		{
			return string.Join("\n", rows.Select((row) => row.Line));
		}
		var rest = rows.Where((row) => !row.Owned).ToList();
		IEnumerable<string> lines = from row in rows
			where row.Owned
			select row.Line;
		return string.Join("\n", (rest.Count == 0) ? lines : lines.Append(T("Остальные ", "Others ") + rest.Count + ": " + rest[0].Ratio));
	}

	// Tracking follows the whole merged recipe: its alternative row counts every alternative.
	// ponytail: the count is the sum, so 5 + 5 of two kinds reads 10/10 for a 10-of-one-kind need; the Craft
	// button still tells the truth. Count whole crafts per kind if players trip over it.
	private static List<MaterialRequirement> TrackedMaterials(Recipe recipe)
	{
		return MaterialRequirements(recipe).Select((MaterialRequirement m) => (m.Alternatives == null) ? m : (m with
		{
			ItemTypes = m.Alternatives.ToHashSet()
		})).ToList();
	}

	// One goal per merged recipe, whichever member is shown.
	private static string TrackKey(Recipe recipe)
	{
		return "recipe:" + BookRecipeVariants.Representative(recipe.RecipeIndex);
	}
}
