using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ModernRecipeBook;

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
