using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using static ModernRecipeBook.BookText;

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
		return string.Join(", ", names.Take(names.Length - 1)) + L("Variants.Or") + names[^1];
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
			string shared = string.Join(", ", stems.Take(stems.Length - 1)) + L("Variants.Or") + stems[^1] + " " + last;
			if (TextWidth(shared, 0.85f) <= width)
			{
				return shared;
			}
		}
		string more = names[0] + L("Variants.Or2") + (names.Length - 1) + L("Variants.More");
		return (TextWidth(more, 0.85f) <= width) ? more : (names[0] + L("Variants.Or3"));
	}

	// The second line of a two-line "A or B" row: "or Shadow Scale", "or B, C", "or B and 3 more".
	private static string OthersLabel(int[] types, int shown, float width)
	{
		string[] others = Ordered(types, shown).Skip(1).Select((int type) => ContentSamples.ItemsByType[type].Name).ToArray();
		string all = L("Variants.Or4") + string.Join(", ", others);
		if (others.Length < 2 || TextWidth(all, 0.8f) <= width)
		{
			return all;
		}
		string first = L("Variants.Or4") + others[0] + L("Variants.And") + (others.Length - 1) + L("Variants.More");
		return (TextWidth(first, 0.8f) <= width) ? first : (L("Variants.Or5") + others.Length + L("Variants.More"));
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
			BookMaterialRules.Assessment assessment = BookMaterialRules.Evaluate(requirement.Ingredient.stack, batches, Owned(types), BookSources.IsInfinite(types), overlapping: false);
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
		return string.Join("\n", (rest.Count == 0) ? lines : lines.Append(L("Variants.Others") + rest.Count + ": " + rest[0].Ratio));
	}

	// Tracking follows the whole merged recipe; every update its alternative row counts the one kind you have most
	// of (NarrowAlternatives), so 5 Tissue + 5 Shadow Scale reads 5/10, as a craft takes 10 of one kind.
	private static List<MaterialRequirement> TrackedMaterials(Recipe recipe)
	{
		return MaterialRequirements(recipe);
	}

	private static List<MaterialRequirement> NarrowAlternatives(List<MaterialRequirement> materials, IReadOnlyDictionary<int, int> counts)
	{
		if (!materials.Any((MaterialRequirement m) => m.Alternatives != null))
		{
			return materials;
		}
		return materials.Select(delegate(MaterialRequirement m)
		{
			if (m.Alternatives == null)
			{
				return m;
			}
			int best = m.Alternatives.OrderByDescending((int type) => counts.TryGetValue(type, out int n) ? n : 0).First();
			if (m.ItemTypes.Count == 1 && m.ItemTypes.Contains(best))
			{
				return m;
			}
			Item icon = ContentSamples.ItemsByType[best].Clone();
			icon.stack = m.Ingredient.stack;
			return m with
			{
				Ingredient = icon,
				ItemTypes = new HashSet<int> { best }
			};
		}).ToList();
	}

	// One goal per merged recipe, whichever member is shown.
	private static string TrackKey(Recipe recipe)
	{
		return "recipe:" + BookRecipeVariants.Representative(recipe.RecipeIndex);
	}
}
