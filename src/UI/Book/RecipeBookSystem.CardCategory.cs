using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ModernRecipeBook.UI.Book;

// The item card's category (BookCategoryPath) after the item's mod in the header: "Terraria · Weapons › Whips". Each
// part is a link (RecipeBookSystem.Links): a category to that sidebar category on Items, the mod to the mod filter,
// wherever the card was opened from.
public sealed partial class RecipeBookSystem
{
	private const string CategorySeparator = " › ";

	// The line the path is on: the armor set's subtitle, before the stage; or the one under the item's name, whose
	// stage then takes the next line (DetailHeaderText).
	private static (BookRect Line, List<LinkSpan> Tail) CardCategoryLine(Item chosen, Recipe r)
	{
		if (BookArmorSets.Get(chosen.type) != null)
		{
			return (BookGeometry.ArmorSubtitle, ItemStageSpans(chosen, (CardItemType > 0) ? null : r).Prepend(new LinkSpan(" · ")).ToList());
		}
		return (new BookRect(880f, DetailHeaderText(chosen, ModName(chosen.ModItem?.Mod.Name ?? "Terraria"), StageName(chosen, (CardItemType > 0) ? null : r)).Top + 26f, 216f, 20f), new List<LinkSpan>());
	}

	// "Calamity" for Calamity Mod: the word says nothing in a mod's name ("Calamity Mod Music" reads "Calamity Music").
	private static string ShortModName(string mod)
	{
		return Regex.Replace(mod, @"(?<=\S)\s+Mod\b", "");
	}

	// "<mod> · <group> › <category><tail>". When the whole line does not fit, the mod's short name makes room for the
	// whole path, then the group goes (the mod's whole name back if that fits); what still does not is cut at the end.
	// The shortened mod's tip names it whole.
	private static List<LinkSpan> CardCategorySpans(Item chosen, List<LinkSpan> tail, float width)
	{
		string owner = chosen.ModItem?.Mod.Name ?? "Terraria";
		string lead = ModName(owner);
		IReadOnlyList<BookCategory> whole = BookCategoryPath.Of(chosen.type);
		IReadOnlyList<BookCategory> last = whole.TakeLast(1).ToList();
		string brief = ShortModName(lead);
		string tailText = string.Concat(tail.Select((LinkSpan s) => s.Text));
		(string Lead, IReadOnlyList<BookCategory> Path)[] tries = { (lead, whole), (brief, whole), (lead, last), (brief, last) };
		(string Lead, IReadOnlyList<BookCategory> Path) shown = tries[^1];
		foreach ((string Lead, IReadOnlyList<BookCategory> Path) attempt in tries)
		{
			string text = ((attempt.Path.Count > 0) ? (attempt.Lead + " · " + string.Join(CategorySeparator, attempt.Path.Select((BookCategory category) => category.Label()))) : attempt.Lead) + tailText;
			if (TextWidth(text, 0.8f) <= width)
			{
				shown = attempt;
				break;
			}
		}
		BookLink mod = ModLink(owner, mobs: false);
		List<LinkSpan> spans = new List<LinkSpan> { new LinkSpan(shown.Lead, (shown.Lead != lead) ? (mod with { Tip = lead + "\n" + mod.Tip }) : mod) };
		for (int i = 0; i < shown.Path.Count; i++)
		{
			spans.Add(new LinkSpan((i > 0) ? CategorySeparator : " · "));
			spans.Add(new LinkSpan(shown.Path[i].Label(), CategoryLink(shown.Path[i])));
		}
		spans.AddRange(tail);
		return spans;
	}

	// True while the mouse is on one of the line's links: its tip replaces the line's own.
	private static bool DrawCardCategoryLine(SpriteBatch sb, Item chosen, List<LinkSpan> tail, BookRect line)
	{
		return DrawLinkLine(sb, CardCategorySpans(chosen, tail, line.Width), line, 0.8f, Muted);
	}

	// What the sidebar calls the category, what a click does, and that the Items search goes when there is one.
	private static BookLink CategoryLink(BookCategory category)
	{
		BookCategory row = ItemCategoryList.Find((BookCategory c) => c.Key == category.Key) ?? category;
		string key = category.Key;
		return new BookLink(FilterTip(L("CardCategory.Show", row.Label()), 0), () => ShowItems((BookUiState.TabFilters f) => f.CategoryKey = key));
	}
}
