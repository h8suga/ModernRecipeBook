using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ModernRecipeBook.Catalogue.Search;

// How well a quick-search entry matches: exact > prefix > word-prefix > contains, each a step higher when the name
// itself (the display name, the English one) matched rather than an internal name, an ID or the mod. Fields are
// normalized (BookRules.SearchText); the first `primary` of them are names. 0: no match.
internal static class BookQuickRank
{
	internal const int Exact = 4, Prefix = 3, WordPrefix = 2, Contains = 1;

	internal static int Score(string query, string[] terms, string[] fields, int primary)
	{
		if (terms.Length == 0)
		{
			return 0;
		}
		int best = 0;
		for (int i = 0; i < fields.Length; i++)
		{
			string field = fields[i];
			int tier = field == query ? Exact : (field.StartsWith(query, StringComparison.Ordinal) ? Prefix : 0);
			best = Math.Max(best, Value(tier, i < primary));
		}
		if (best >= Value(Prefix, false))
		{
			return best;
		}
		// Every term somewhere: the weakest term sets the tier, a term found only outside the names drops the bonus.
		int all = WordPrefix;
		bool names = true;
		foreach (string term in terms)
		{
			int tier = 0;
			bool name = false;
			string stem = Stem(term);
			for (int i = 0; i < fields.Length; i++)
			{
				int found = (StartsWord(fields[i], term) || (stem != term && StartsWord(fields[i], stem))) ? WordPrefix : (fields[i].Contains(term, StringComparison.Ordinal) ? Contains : 0);
				if (found > tier || (found == tier && found > 0 && i < primary && !name))
				{
					tier = found;
					name = i < primary;
				}
			}
			if (tier == 0)
			{
				return best;
			}
			all = Math.Min(all, tier);
			names &= name;
		}
		return Math.Max(best, Value(all, names));
	}

	internal static int Value(int tier, bool name) => tier == 0 ? 0 : tier * 2 + (name ? 1 : 0);

	// A Russian word without its ending, which starts a word as well as the word itself: "лунный лорд" finds «Тело лунного
	// лорда», "зелье" «Все зелья». Up to two final vowels (or й) go while three letters stay; ь stays, so "зелье" is not
	// "зел" and does not find everything green.
	internal static string Stem(string term)
	{
		int end = term.Length;
		while (end > 3 && term.Length - end < 2 && "аеёиоуыэюяй".Contains(term[end - 1]))
		{
			end--;
		}
		return term.Substring(0, end);
	}

	// A match at the start of a word: the field's start or after anything that is not a letter or digit.
	internal static bool StartsWord(string field, string term)
	{
		for (int at = field.IndexOf(term, StringComparison.Ordinal); at >= 0; at = field.IndexOf(term, at + 1, StringComparison.Ordinal))
		{
			if (at == 0 || !char.IsLetterOrDigit(field[at - 1]))
			{
				return true;
			}
		}
		return false;
	}

	// An internal name as words: "IronskinPotion" -> "Ironskin Potion", "CalamityMod/AstrumDeusHead" -> "CalamityMod/Astrum Deus Head".
	internal static string Words(string name)
	{
		return Regex.Replace(name ?? "", "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
	}

	// Stand: the ranking on made-up names.
	internal static string SelfCheck()
	{
		List<string> failures = new List<string>();
		Check("wood", new[] { "wood" }, 9);
		Check("wood", new[] { "wooden sword" }, 7);
		Check("sword", new[] { "wooden sword" }, 5);
		Check("ood", new[] { "wooden sword" }, 3);
		Check("1340", new[] { "flask of ichor", "", "flask of ichor", "1340" }, 8);
		Check("pick iron", new[] { "iron pickaxe" }, 5);
		Check("iron calamity", new[] { "iron pickaxe", "", "iron pickaxe", "1", "calamity mod" }, 4);
		Check("terraprisma", new[] { "призматика", "terraprisma" }, 9);
		Check("zzz", new[] { "wood" }, 0);
		Check("лунный лорд", new[] { "тело лунного лорда" }, 5);
		Check("зелье", new[] { "все зелья" }, 5);
		Check("лорд", new[] { "лунный лорд" }, 5);
		if (Words("IronskinPotion") != "Ironskin Potion" || Words("NPCKiller") != "NPC Killer")
		{
			failures.Add("words: " + Words("IronskinPotion") + " / " + Words("NPCKiller"));
		}
		return failures.Count == 0 ? "ok" : string.Join("; ", failures);
		void Check(string query, string[] fields, int expected)
		{
			string[] terms = BookRules.SearchTerms(query);
			int score = Score(string.Join(" ", terms), terms, fields, Math.Min(2, fields.Length));
			if (score != expected)
			{
				failures.Add($"{query} in {string.Join("|", fields)}: {score}, not {expected}");
			}
		}
	}
}
