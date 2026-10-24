using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// An item's tooltip as the game shows it on hover: vanilla's lines, then every mod's ModifyTooltips (Calamity rewrites
// many vanilla ones: Monk's Shirt names rogue only there). The search reads the descriptive part, the class audit all of it.
internal static class BookItemTooltip
{
	// While the book builds a tooltip for itself: its own ModifyTooltips (the Storage lines, the hover shortcut) stays out.
	internal static bool Building { get; private set; }

	internal static List<TooltipLine> Lines(Item sample)
	{
		Item item = sample.Clone();
		// Vanilla's Journey search sizes the arrays so: its own lines plus room for every stat line.
		int size = 30 + (item.ToolTip?.Lines ?? 0);
		int yoyoLogo = -1;
		int researchLine = -1;
		int oneDropLogo = -1;
		int numLines = 1;
		string[] text = new string[size];
		bool[] modifier = new bool[size];
		bool[] badModifier = new bool[size];
		string[] names = new string[size];
		Building = true;
		try
		{
			Main.MouseText_DrawItemTooltip_GetLinesInfo(item, ref yoyoLogo, ref researchLine, item.knockBack, ref numLines, text, modifier, badModifier, names, out var prefixLine);
			return ItemLoader.ModifyTooltips(item, ref numLines, names, ref text, ref modifier, ref badModifier, ref oneDropLogo, out _, prefixLine);
		}
		finally
		{
			Building = false;
		}
	}

	internal static string Full(Item sample)
	{
		return string.Join("\n", from line in Lines(sample)
			select line.Text);
	}

	// What the item says about itself: its own lines (Tooltip0..n, as every mod left them) and every line a mod adds.
	// Vanilla's stat and label lines stay out ("50 rogue damage", "Equipable", "Material", prices, prefixes): they read
	// the same on hundreds of items, and the class and category filters cover them.
	internal static bool Describes(TooltipLine line)
	{
		return line.Mod != "Terraria" || line.Name.StartsWith("Tooltip", StringComparison.Ordinal);
	}

	// The search's text: the descriptive lines, normalized; "" when a mod's tooltip code throws.
	internal static string Description(Item sample)
	{
		try
		{
			return BookRules.SearchText(BookRules.TagFree(string.Join("\n", from line in Lines(sample)
				where Describes(line)
				select line.Text)));
		}
		catch
		{
			return "";
		}
	}

	// Stand: every line with what the search keeps (+) and leaves out (-).
	internal static string Explain(int type)
	{
		return string.Join("\n", from line in Lines(ContentSamples.ItemsByType[type])
			select (Describes(line) ? "+ " : "- ") + line.Mod + "/" + line.Name + ": " + line.Text);
	}
}
