using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook;

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
		array = slot switch
		{
			0 => new string[15]
			{
				"Headgear", "Headpiece", "Headdress", "Helmet", "Mask", "Hood", "Hat", "Helm", "Crown", "Visor",
				"Skull", "Cap", "Casque", "Brows", "Head"
			}, 
			1 => new string[17]
			{
				"Breastplate", "Chestplate", "Chainmail", "Scalemail", "Platemail", "ScaleMail", "ChainMail", "PlateMail", "Shirt", "Robe",
				"Coat", "Jacket", "Suit", "Body", "Tunic", "Jerkin", "Vest"
			}, 
			_ => new string[7] { "Leggings", "Greaves", "Trousers", "Pants", "Boots", "Cuisses", "Legs" }, 
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
