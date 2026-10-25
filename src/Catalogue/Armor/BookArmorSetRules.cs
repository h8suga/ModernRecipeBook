using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Catalogue.Armor;

internal static class BookArmorSetRules
{
	internal static string Family(string name, int slot, bool vanilla)
	{
		string[] array;
		if (vanilla)
		{
			if (name.StartsWith("AncientCobalt", StringComparison.Ordinal))
			{
				return "Jungle";
			}
			array = new string[5] { "AncientShadow", "AncientHallowed", "AncientGold", "AncientIron", "AncientNecro" };
			foreach (string prefix in array)
			{
				if (name.StartsWith(prefix, StringComparison.Ordinal))
				{
					string text = name;
					name = text.Substring(7, text.Length - 7);
				}
			}
			if (name == "BeetleShell")
			{
				return "Beetle";
			}
			if (name == "UltrabrightHelmet")
			{
				return "Mining";
			}
		}
		// One head per class, named <Set>Head<Class> (Calamity's AerospecHeadMagic, ReaverHeadTank).
		int head = name.IndexOf("Head", StringComparison.Ordinal);
		if (slot == 0 && head > 0 && name.Length > head + 4 && char.IsUpper(name[head + 4]))
		{
			return name.Substring(0, head);
		}
		// Longer suffixes first where a shorter one would also match (Greathelm/Helm, Shelleggings/Leggings).
		array = slot switch
		{
			0 => new string[20]
			{
				"Greathelm", "Shellmet", "Headgear", "Headpiece", "Headdress", "Helmet", "Mask", "Hood", "Hat", "Helm",
				"Crown", "Visor", "Visage", "Skull", "Cap", "Casque", "Brows", "Cowl", "Wig", "Head"
			},
			1 => new string[27]
			{
				"Breastplate", "Chestplate", "Chainmail", "Scalemail", "Platemail", "ScaleMail", "ChainMail", "PlateMail", "Shellplate", "Shirt",
				"Robes", "Robe", "Coat", "Jacket", "Suit", "BodyArmor", "Body", "Tunic", "Jerkin", "Vest",
				"Plating", "Regalia", "Mantle", "Cloak", "Carapace", "Toga", "Armor"
			},
			_ => new string[14] { "Shelleggings", "Leggings", "Greaves", "Trousers", "Pants", "Boots", "Cuisses", "Legs", "Subligar", "Schynbaulds", "Tentacles", "Striders", "Pistons", "Overalls" },
		};
		foreach (string suffix in array)
		{
			if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && name.Length > suffix.Length)
			{
				string text = name;
				int length = suffix.Length;
				return text.Substring(0, text.Length - length);
			}
		}
		return "";
	}

	internal static int[] Choices(int[][] parts, int[][] combinations, int selected)
	{
		return combinations.FirstOrDefault((int[] c) => Enumerable.Contains(c, selected)) ?? parts.Select((int[] p) => (!Enumerable.Contains(p, selected)) ? p[0] : selected).ToArray();
	}

	internal static int Next(IReadOnlyList<int> choices, int selected, bool alreadyActive)
	{
		if (choices.Count == 0)
		{
			return 0;
		}
		int current = -1;
		for (int n = 0; n < choices.Count; n++)
		{
			if (choices[n] == selected)
			{
				current = n;
			}
		}
		return choices[(alreadyActive && current >= 0) ? ((current + 1) % choices.Count) : Math.Max(0, current)];
	}
}
