using System;
using System.Collections.Generic;

namespace ModernRecipeBook.Catalogue.Traits;

// Does a description line say the shots home in? Tuned on the real lines of vanilla and Calamity 2.2.4 (English and the
// Russian translation mod): "homing", "home in", "seek", "chase", "track", "pursue", "lock onto"; «самонавод»,
// «наводятся», «преследуют». Not counted: a word that goes on to the cursor or the mouse ("seeks the cursor": the player
// steers it), "home" as a noun ("return home"), "track record", and flavor text (a line that starts or ends with a quote).
// A line about stealth strikes gives stealth homing.
internal static class BookHomingText
{
	private static readonly string[] Stems = new string[14] { "homing", "home", "homes", "homed", "seek", "chas", "track", "pursu", "самонавод", "наводящ", "наводят", "наводит", "преследу", "преследова" };

	// Whole words only: "home" is not "homeland".
	private static readonly HashSet<string> WholeWords = new HashSet<string>(StringComparer.Ordinal) { "home", "homes", "homed" };

	private static readonly HashSet<string> NounBefore = new HashSet<string>(StringComparer.Ordinal) { "a", "the", "my", "your", "our", "their", "his", "her", "its", "return", "returns", "returning", "you", "back", "at", "go", "come" };

	private static readonly string[] Steered = new string[4] { "cursor", "mouse", "курсор", "мыш" };

	// What appears only when a shot hits or kills ("Stardust beams release small stars that home on enemy hits") is not
	// the shot homing. Costs a few true lines that also mention a hit (Exoblade's "... and slice them on hit").
	private static readonly string[] OnHit = new string[22]
	{
		" on hit", "enemy hit", "on first hit", "on critical hit", "on dagger hit", "tile hit", "when it hits", "upon hit", "on death", "after impact", "on impact", "direct hit",
		"при попадан", "попадания по", "при убийств", "прямые удар", "прямых удар", "при столкновен", "после столкновен", "при ударе", "критические удар", "при каждом удар"
	};

	internal static BookTrait Classify(string line)
	{
		string plain = BookRules.PlainText(BookRules.TagFree(line ?? "")).Trim();
		if (plain.Length == 0 || Quote(plain[0]) || Quote(plain[plain.Length - 1]))
		{
			return BookTrait.None;
		}
		string text = plain.ToLowerInvariant().Replace('ё', 'е');
		List<string> words = Words(text);
		bool found = false;
		for (int i = 0; i < words.Count && !found; i++)
		{
			found = Homes(words, i);
		}
		// "Nanomachines aggressively lock onto nearby enemies".
		for (int i = 0; i + 1 < words.Count && !found; i++)
		{
			found = (words[i] == "lock" || words[i] == "locks") && (words[i + 1] == "onto" || words[i + 1] == "on") && !SteeredAfter(words, i + 1);
		}
		if (!found || Array.Exists(OnHit, (string phrase) => text.Contains(phrase, StringComparison.Ordinal)))
		{
			return BookTrait.None;
		}
		if (BookItemClasses.RogueShown && (text.Contains("stealth", StringComparison.Ordinal) || text.Contains("скрыт", StringComparison.Ordinal)))
		{
			return BookTrait.StealthHoming;
		}
		return BookTrait.Homing;
	}

	private static bool Homes(List<string> words, int i)
	{
		string word = words[i];
		string stem = Array.Find(Stems, (string s) => word.StartsWith(s, StringComparison.Ordinal));
		if (stem == null)
		{
			return false;
		}
		if ((stem == "home" || stem == "homes" || stem == "homed") && !WholeWords.Contains(word))
		{
			return false;
		}
		if (WholeWords.Contains(word) && i > 0 && NounBefore.Contains(words[i - 1]))
		{
			return false;
		}
		if (stem == "track" && i + 1 < words.Count && words[i + 1] == "record")
		{
			return false;
		}
		return !SteeredAfter(words, i);
	}

	// The next few words name the cursor: the player steers it.
	private static bool SteeredAfter(List<string> words, int i)
	{
		for (int k = i + 1; k < words.Count && k <= i + 4; k++)
		{
			string next = words[k];
			if (Array.Exists(Steered, (string s) => next.StartsWith(s, StringComparison.Ordinal)))
			{
				return true;
			}
		}
		return false;
	}

	private static List<string> Words(string text)
	{
		List<string> words = new List<string>();
		int start = -1;
		for (int i = 0; i <= text.Length; i++)
		{
			bool letter = i < text.Length && char.IsLetter(text[i]);
			if (letter && start < 0)
			{
				start = i;
			}
			else if (!letter && start >= 0)
			{
				words.Add(text.Substring(start, i - start));
				start = -1;
			}
		}
		return words;
	}

	private static bool Quote(char c)
	{
		return c is '\'' or '"' or '«' or '»' or '“' or '”' or '‘' or '’';
	}

	// Stand: the rule on real lines.
	internal static string SelfCheck()
	{
		(string Line, BookTrait Want)[] cases = new (string, BookTrait)[]
		{
			("Chases after your enemy", BookTrait.Homing),
			("Shoots bees that will chase your enemy", BookTrait.Homing),
			("Launches homing missiles", BookTrait.Homing),
			("Cycles between throwing 3 small homing spears and throwing a single piercing lance", BookTrait.Homing),
			("These water spirits will home in on enemies after traveling towards the player", BookTrait.Homing),
			("Wind slashes occasionally home, and always home if the javelin crits", BookTrait.Homing),
			("Nanomachines aggressively lock onto nearby enemies", BookTrait.Homing),
			("If the disk cannot find a new target to ricochet to, it will pursue the original target instead", BookTrait.Homing),
			("Fires a burst of four heat seeking missiles that rapidly accelerate after a short delay", BookTrait.Homing),
			("During the cooldown, buzzsaws home to the cursor and constantly release homing bolts while returning", BookTrait.Homing),
			("Fires a pulse orb that tracks nearby targets", BookTrait.Homing),
			("Стреляет самонаводящимися ракетами", BookTrait.Homing),
			("Наномашины агрессивно наводятся на ближайших противников", BookTrait.Homing),
			("Любые осколки после 10 сразу начинают преследовать врагов", BookTrait.Homing),
			("Stealth strikes fire three kunai that home in, stick to enemies, and explode", StealthWant()),
			("Fires a wired-guided rocket fist that seeks the cursor and accelerates over time", BookTrait.None),
			("Stealth strikes throw a kylie that weakly seeks out your cursor", BookTrait.None),
			("Fires a trinity of energy bolts that split into beams that track your cursor", BookTrait.None),
			("Выстрелите особо взрывчатым кулаком-ракетой, который следует за курсором и ускоряется со временем", BookTrait.None),
			("Gaze in the mirror to return home", BookTrait.None),
			("Teleports you home", BookTrait.None),
			("[c/5C95A1:However, they had a perfect track record of repelling any invasions, thanks to their insurmountable advantage in the water.']", BookTrait.None),
			("'Conjure masses of astral energy to chase down your foes'", BookTrait.None),
			("'...I see... So frightened by the Godseeker were my children they chose to defile death for a warden in my image.", BookTrait.None),
			("Summons a brimseeker to keep you company", BookTrait.None),
			("Tosses a red-hot ricocheting glaive", BookTrait.None),
			("Throw a flare that sticks to enemies and rains a hailstorm on them", BookTrait.None),
			("Throws a homeland flag", BookTrait.None),
			("Stardust beams release small stars that home on enemy hits", BookTrait.None),
			("Enemies explode into homing blood on death", BookTrait.None),
			("The shuriken shatters moments after impact into homing blades", BookTrait.None),
			("Лучи Звездной Пыли выпускают маленькие наводящиеся звезды при попадании", BookTrait.None),
			("The plasma pierces, but will stop tracking after hitting the first enemy", BookTrait.Homing),
			("Pulse orbs aggressively track nearby targets and track faster the more they hit", BookTrait.Homing),
			("Stealth strikes home in faster and hit at a faster rate", StealthWant())
		};
		List<string> failures = new List<string>();
		foreach (var (line, want) in cases)
		{
			BookTrait got = Classify(line);
			if (got != want)
			{
				failures.Add(got + " for \"" + line + "\"");
			}
		}
		if (failures.Count != 0)
		{
			return string.Join("; ", failures);
		}
		return "ok (" + cases.Length + " lines)";
		static BookTrait StealthWant()
		{
			return BookItemClasses.RogueShown ? BookTrait.StealthHoming : BookTrait.Homing;
		}
	}
}
