using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ModernRecipeBook.UI.Book;

// The item card's category (BookCategoryPath) after the item's mod in the header: "Terraria · Weapons › Whips". Each
// part is a link to that sidebar category on Items, wherever the card was opened from.
public sealed partial class RecipeBookSystem
{
	private const string CategorySeparator = " › ";

	// One piece of the line as drawn: where its ink starts, the room it has, the category it opens (null for the mod,
	// the separators and the stage) and whether it was cut short.
	private readonly record struct CardCategoryPiece(string Text, float X, float Width, BookCategory Category, bool Cut);

	// The line the path is on: the armor set's subtitle, before the stage; or the one under the item's name, whose
	// stage then takes the next line (DetailHeaderText).
	private static (BookRect Line, string Lead, string Tail) CardCategoryLine(Item chosen, Recipe r)
	{
		string mod = ModName(chosen.ModItem?.Mod.Name ?? "Terraria");
		string stage = StageName(chosen, (CardItemType > 0) ? null : r);
		if (BookArmorSets.Get(chosen.type) != null)
		{
			return (BookGeometry.ArmorSubtitle, mod, " · " + stage);
		}
		return (new BookRect(880f, DetailHeaderText(chosen, mod, stage).Top + 26f, 216f, 20f), mod, "");
	}

	// "<lead> · <group> › <category><tail>". The group goes first when the whole line does not fit; what still does
	// not is cut at the line's end.
	private static List<CardCategoryPiece> CardCategoryPieces(Item chosen, string lead, string tail, BookRect line)
	{
		List<BookCategory> path = BookCategoryPath.Of(chosen.type).ToList();
		if (path.Count > 1 && TextWidth(lead + " · " + string.Join(CategorySeparator, path.Select((BookCategory category) => category.Label())) + tail, 0.8f) > line.Width)
		{
			path.RemoveAt(0);
		}
		List<(string Text, BookCategory Category)> texts = new List<(string, BookCategory)> { ((path.Count > 0) ? (lead + " · ") : lead, null) };
		for (int i = 0; i < path.Count; i++)
		{
			if (i > 0)
			{
				texts.Add((CategorySeparator, null));
			}
			texts.Add((path[i].Label(), path[i]));
		}
		if (tail.Length > 0)
		{
			texts.Add((tail, null));
		}
		// Each piece's ink ends where the line's text up to it ends, as one FixedLabel would draw the whole line.
		List<CardCategoryPiece> pieces = new List<CardCategoryPiece>();
		string drawn = "";
		float end = line.X + line.Width;
		foreach ((string text, BookCategory category) in texts)
		{
			drawn += text;
			float right = line.X + TextWidth(drawn, 0.8f);
			float left = right - TextWidth(text, 0.8f);
			if (left >= end - 8f)
			{
				break;
			}
			pieces.Add(new CardCategoryPiece(text, left, end - left, category, right > end));
			if (right > end)
			{
				break;
			}
		}
		return pieces;
	}

	private static BookRect CardCategoryHit(CardCategoryPiece piece, BookRect line)
	{
		return new BookRect(piece.X - 4f, line.Y, Math.Min(TextWidth(piece.Text, 0.8f), piece.Width) + 8f, line.Height);
	}

	// True while the mouse is on a category: its tip replaces the line's own.
	private static bool DrawCardCategoryLine(SpriteBatch sb, Item chosen, string lead, string tail, BookRect line)
	{
		bool linkHover = false;
		foreach (CardCategoryPiece piece in CardCategoryPieces(chosen, lead, tail, line))
		{
			bool hover = false;
			if (piece.Category != null)
			{
				BookRect hit = CardCategoryHit(piece, line);
				hover = !MenuOpen && hit.Contains(Mouse.X, Mouse.Y);
				if (hover)
				{
					Box(sb, hit, HoverFill, border: false, 6f);
				}
			}
			FixedLabel(sb, piece.Text, piece.X, line.Y, (piece.Category != null) ? TextColor : Muted, 0.8f, piece.Width, line.Height);
			if (hover)
			{
				linkHover = true;
				MouseTip(CardCategoryTip(piece));
			}
		}
		return linkHover;
	}

	// What the sidebar calls the category, what a click does, and that the Items search goes when there is one.
	private static string CardCategoryTip(CardCategoryPiece piece)
	{
		BookCategory row = ItemCategoryList.Find((BookCategory category) => category.Key == piece.Category.Key) ?? piece.Category;
		string query = Catalogue ? Query : (TabFilterSlots.GetValueOrDefault(0)?.Query ?? "");
		return (piece.Cut ? (piece.Text + "\n") : "") + L("CardCategory.Show", row.Label()) + ((query.Length > 0) ? ("\n" + L("CardCategory.SearchCleared")) : "");
	}

	private static bool HandleCardCategoryClick()
	{
		Item chosen = SelectedItem;
		(BookRect line, string lead, string tail) = CardCategoryLine(chosen, SelectedRecipe);
		foreach (CardCategoryPiece piece in CardCategoryPieces(chosen, lead, tail, line))
		{
			if (piece.Category != null && CardCategoryHit(piece, line).Contains(Mouse.X, Mouse.Y))
			{
				ShowItemCategory(chosen.type, piece.Category.Key);
				return true;
			}
		}
		return false;
	}

	// Items with that category and no search, which would hide the rest of it; every other filter stays as the player
	// set it. The item stays in the card, selected and paged to in the grid when the filters show it. History records
	// the step, so Back returns to where the card was.
	private static void ShowItemCategory(int type, string key)
	{
		bool same = CardItemType == type;
		int variant = Variant;
		int details = DetailsPage;
		if (!Catalogue)
		{
			SwitchTab(0);
		}
		BookUiState.TabFilters filters = LiveTabFilters();
		filters.Query = "";
		filters.CategoryKey = key;
		LoadTabFilters(filters);
		SetTyping(value: false);
		ApplyFilters();
		ArmorFocus = 0;
		ResetCard();
		int position = Filtered.IndexOf(type);
		if (position >= 0)
		{
			Selected = type;
			Page = position / ActivePageSize;
		}
		else
		{
			FocusType = type;
		}
		Variant = same ? variant : 0;
		DetailsPage = same ? details : 0;
	}
}
