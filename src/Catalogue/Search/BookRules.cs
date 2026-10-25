using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ModernRecipeBook.Catalogue.Search;

internal static class BookRules
{
	internal static string PlainText(string value)
	{
		return Regex.Replace(value ?? "", "\\[c/[0-9a-fA-F]{6}:([^\\]]+)\\]", "$1");
	}

	private static string Normalize(string value)
	{
		return PlainText(value).Normalize(NormalizationForm.FormKC).ToLowerInvariant().Replace('ё', 'е');
	}

	// A filter pass splits the query once and normalizes an entry's fields only when there is something to match.
	internal static string[] SearchTerms(string query)
	{
		return Normalize(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
	}

	internal static string SearchText(params string[] fields)
	{
		return Normalize(string.Join(" ", fields));
	}

	internal static bool Search(string[] terms, string text)
	{
		foreach (string term in terms)
		{
			if (!text.Contains(term))
			{
				return false;
			}
		}
		return true;
	}

	// Each term in the name fields or in the description ("flask rogue": the name has one, the text the other).
	internal static bool Search(string[] terms, string text, string description)
	{
		foreach (string term in terms)
		{
			if (!text.Contains(term) && !description.Contains(term))
			{
				return false;
			}
		}
		return true;
	}

	// Tooltip chat tags: a colour tag keeps its text (PlainText), an item or glyph icon ([i:ID], [i/s5:ID], [g:4]) has none.
	internal static string TagFree(string value)
	{
		return Regex.Replace(PlainText(value), "\\[[ig](/[^:\\]]*)?:[^\\]]*\\]", "");
	}

	// Stand: the search rules on made-up texts.
	internal static string SearchSelfCheck()
	{
		List<string> failures = new List<string>();
		string name = SearchText("Flask of Ichor", "Terraria/1340");
		string text = SearchText(TagFree("[c/ff8800:Melee], Whip and Rogue attacks [i:1332] inflict Ichor"));
		if (text != "melee, whip and rogue attacks  inflict ichor")
		{
			failures.Add("tags: " + text);
		}
		if (Search(SearchTerms("rogue"), name) || !Search(SearchTerms("rogue"), name, text))
		{
			failures.Add("found only by text");
		}
		if (!Search(SearchTerms("Flask ROGUE"), name, text))
		{
			failures.Add("terms split across name and text");
		}
		if (Search(SearchTerms("flask summon"), name, text) || !Search(SearchTerms("ichor"), name))
		{
			failures.Add("every term must match");
		}
		return (failures.Count == 0) ? "ok" : string.Join("; ", failures);
	}

	internal static bool RecipeMatchesSample(int sampleType, int resultType, bool uses, IEnumerable<int> ingredientTypes, Func<int, int, bool> acceptsGroup)
	{
		if (sampleType <= 0)
		{
			return true;
		}
		if (!uses)
		{
			return sampleType == resultType;
		}
		return ingredientTypes.Any((int type) => type == sampleType || acceptsGroup(sampleType, type));
	}

	internal static bool ChainRecipeVisible(bool craftableOnly, bool nativelyAvailable)
	{
		return !craftableOnly | nativelyAvailable;
	}

	internal static bool AvailabilityChanged(HashSet<int> previous, HashSet<int> next)
	{
		return !previous.SetEquals(next);
	}

	internal static bool CraftAllowed(bool recipeExists, bool playerAllowed, bool guideEmpty, bool cursorEmpty, bool nativelyAvailable)
	{
		return recipeExists & playerAllowed & guideEmpty & cursorEmpty & nativelyAvailable;
	}
}
