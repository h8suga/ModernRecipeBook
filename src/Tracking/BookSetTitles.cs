using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Tracking;

// The words a set goal is named by, from its pieces' names. Leading: what every name starts with ("Molten Helmet /
// Molten Breastplate / Molten Greaves": "Molten"); Trailing: what every name ends with («Шлем туманности /
// Нагрудник туманности / Ботинки туманности»: «туманности»). Russian adjectives agree with each piece's noun
// («Святой шлем / Святой нагрудник / Святые ботинки»), so in Russian leading words match by stem and come back in
// the feminine form «броня» takes: «Святая». Vanilla's own Russian names follow these two patterns.
internal static class BookSetTitles
{
	internal readonly record struct Words(string Leading, string Trailing);

	// Nominative endings of Russian adjectives: masculine, feminine, neuter, plural; ий/ья/ье/ьи also the possessive
	// kind (Паучий, Паучья, Паучьи).
	private static readonly string[] AdjectiveEndings = { "ый", "ой", "ий", "ая", "яя", "ья", "ое", "ее", "ье", "ые", "ие", "ьи" };

	internal static Words Find(IReadOnlyList<string> names, bool russian)
	{
		string[][] words = names.Select((string n) => n.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
		if (words.Length == 0 || words.Any((string[] w) => w.Length < 2))
		{
			return new Words("", "");
		}
		int shortest = words.Min((string[] w) => w.Length);
		// Every piece keeps at least one word of its own (its noun).
		int lead = 0;
		while (lead < shortest - 1 && words.All((string[] w) => russian ? SameAdjective(w[lead], words[0][lead]) : w[lead] == words[0][lead]))
		{
			lead++;
		}
		int trail = 0;
		while (trail < shortest - 1 - lead && words.All((string[] w) => w[^(trail + 1)] == words[0][^(trail + 1)]))
		{
			trail++;
		}
		string leading = string.Join(" ", Enumerable.Range(0, lead).Select((int i) => russian ? Feminine(words.Select((string[] w) => w[i]).ToArray()) : words[0][i]));
		return new Words(leading, string.Join(" ", words[0].Skip(words[0].Length - trail)));
	}

	private static bool SameAdjective(string a, string b)
	{
		return a.Length > 4 && b.Length > 4 && IsAdjective(a) && IsAdjective(b) && string.Equals(a[..^2], b[..^2], StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsAdjective(string word)
	{
		return AdjectiveEndings.Any((string e) => word.EndsWith(e, StringComparison.Ordinal));
	}

	// One adjective's forms across the pieces → its feminine: a piece that already has it (Золотая корона), else built
	// from the stem: -ой/-ый → -ая (Святой → Святая); possessive -ий (plural -ьи) → -ья (Жучий → Жучья); -ий after
	// г к х ж ш щ ч → -ая (Жуткий → Жуткая); other -ий → -яя (Древний → Древняя).
	private static string Feminine(string[] forms)
	{
		string ready = forms.FirstOrDefault((string f) => f.EndsWith("ая", StringComparison.Ordinal) || f.EndsWith("яя", StringComparison.Ordinal) || f.EndsWith("ья", StringComparison.Ordinal));
		if (ready != null)
		{
			return ready;
		}
		string stem = forms[0][..^2];
		if (forms.Any((string f) => f.EndsWith("ьи", StringComparison.Ordinal) || f.EndsWith("ье", StringComparison.Ordinal)))
		{
			return stem + "ья";
		}
		if (!forms[0].EndsWith("ий", StringComparison.Ordinal) || "гкхжшщч".Contains(char.ToLowerInvariant(stem[^1])))
		{
			return stem + "ая";
		}
		return stem + "яя";
	}
}
