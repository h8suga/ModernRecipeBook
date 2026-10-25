using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace ModernRecipeBook;

// Traits filter (Items and Recipes): tick traits and only the weapons that have all of them stay (BookItemTraits says
// which). Global like the class row and saved with the UI state; only the player changes it. The button under the class
// buttons opens a menu like the station filter's: Show all, then a row per trait with how many listed items have it.
// The card names the item's traits; its hover says where each one comes from.
public sealed partial class RecipeBookSystem
{
	private static BookTrait TraitFilter;

	// What the traits filter alone hid last pass.
	private static int TraitHidden;

	private static bool TraitMenu;

	private static int TraitMenuTab = -1;

	// The keyboard's row in the menu (0 = Show all), -1 before the first arrow.
	private static int TraitFocus = -1;

	private static int TraitAppliedVersion = -1;

	// Item types the other filters list this pass: the menu counts traits among them.
	private static readonly List<int> TraitCandidates = new List<int>();

	private static int TraitCandidatesVersion;

	private static readonly int[] TraitCounts = new int[BookItemTraits.AllRows.Length];

	private static (int Candidates, int Traits) TraitCountsKey = (-1, -1);

	private static (BookTrait Filter, bool Ru) TraitLabelKey = (BookTrait.None, false);

	private static string TraitLabelText = "";

	private static (int Type, bool Ru, int Version) TraitLineKey = (-1, false, -1);

	private static string TraitLineText = "";

	private static bool TraitFilterTab => CurrentTab is 0 or 1;

	private static bool TraitFilterActive => TraitFilterTab && TraitFilter != BookTrait.None;

	internal static bool TraitMenuOpen => Open && TraitMenu;

	private static void ClearTraitPass()
	{
		TraitHidden = 0;
		TraitCandidates.Clear();
		TraitCandidatesVersion++;
	}

	// The filter pass: every ticked trait must be there.
	private static bool TraitAllowed(int type)
	{
		if (!TraitFilterTab)
		{
			return true;
		}
		TraitCandidates.Add(type);
		if (TraitFilter == BookTrait.None || (BookItemTraits.Get(type) & TraitFilter) == TraitFilter)
		{
			return true;
		}
		TraitHidden++;
		return false;
	}

	// Every update while the book is open.
	private static void UpdateTraits()
	{
		if (TraitMenu && (!TraitFilterTab || CurrentTab != TraitMenuTab || ModMenu || SortMenu || StageMenu || StationMenu || HelpOpen || AcquisitionOpen))
		{
			TraitMenu = false;
		}
		// The descriptions were all read (or are read again in another language): filter with the whole answer.
		if (TraitFilterActive && TraitAppliedVersion != BookItemTraits.Version)
		{
			TraitAppliedVersion = BookItemTraits.Version;
			ApplyFilters(clearMessage: false);
		}
	}

	private static string TraitButtonLabel()
	{
		if (TraitLabelKey == (TraitFilter, Ru) && TraitLabelText.Length > 0)
		{
			return TraitLabelText;
		}
		TraitLabelKey = (TraitFilter, Ru);
		List<string> names = new List<string>();
		foreach (BookTrait trait in BookItemTraits.Rows)
		{
			if ((TraitFilter & trait) != 0)
			{
				names.Add(BookItemTraits.Label(trait, Ru));
			}
		}
		float room = BookGeometry.TraitButton.Width - 40f;
		string text = (names.Count == 0) ? T("Свойства", "Traits") : string.Join(" + ", names);
		// Too long for the button: the first name and how many more, never a cut word.
		if (names.Count > 1 && TextWidth(text, 0.85f) > room)
		{
			text = names[0] + " +" + (names.Count - 1);
		}
		TraitLabelText = text;
		return text;
	}

	private static string TraitButtonTip()
	{
		StringBuilder tip = new StringBuilder(T("Свойства оружия", "Weapon traits"));
		if (TraitFilter == BookTrait.None)
		{
			tip.Append('\n').Append(T("Отметьте свойства: останется оружие, у которого есть все отмеченные", "Tick traits: only weapons that have all of them stay"));
		}
		else
		{
			foreach (BookTrait trait in BookItemTraits.Rows)
			{
				if ((TraitFilter & trait) != 0)
				{
					tip.Append("\n· ").Append(BookItemTraits.Label(trait, Ru));
				}
			}
			tip.Append('\n').Append(T("Скрыто: ", "Hidden: ")).Append(Count(TraitHidden));
		}
		if (!BookItemTraits.Ready)
		{
			tip.Append('\n').Append(ReadingLine());
		}
		return tip.ToString();
	}

	private static string ReadingLine()
	{
		return T("Читаю описания предметов… ", "Reading item descriptions… ") + (int)(BookItemTraits.Progress * 100f) + "%";
	}

	private static void DrawTraitButton(SpriteBatch sb)
	{
		BookRect rect = BookGeometry.TraitButton;
		Dropdown(sb, rect, TraitButtonLabel(), TraitMenu, enabled: true, TraitFilter != BookTrait.None);
		if (!MenuOpen && rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(TraitButtonTip());
		}
	}

	private static BookRect TraitMenuPanel
	{
		get
		{
			BookRect anchor = BookGeometry.TraitButton;
			float height = 8f + 28f + (BookItemTraits.Rows.Length + 1) * 32f + 28f;
			return new BookRect(anchor.X, anchor.Y + anchor.Height + 4f, 336f, height);
		}
	}

	private static BookRect TraitOption(int row)
	{
		BookRect panel = TraitMenuPanel;
		return new BookRect(panel.X + 4f, panel.Y + 36f + row * 32f, panel.Width - 8f, 32f);
	}

	// All rows: 0 is Show all, then the traits.
	private static int TraitRowCount => BookItemTraits.Rows.Length + 1;

	private static int TraitRowAt()
	{
		for (int row = 0; row < TraitRowCount; row++)
		{
			if (TraitOption(row).Contains(Mouse.X, Mouse.Y))
			{
				return row;
			}
		}
		return -1;
	}

	private static void OpenTraitMenu()
	{
		SetTyping(value: false);
		ModMenu = (SortMenu = (StageMenu = (StationMenu = false)));
		TraitMenu = true;
		TraitMenuTab = CurrentTab;
		TraitFocus = -1;
	}

	private static void ActivateTraitRow(int row)
	{
		if (row == 0)
		{
			if (TraitFilter == BookTrait.None)
			{
				return;
			}
			TraitFilter = BookTrait.None;
		}
		else
		{
			TraitFilter ^= BookItemTraits.Rows[row - 1];
		}
		Page = (DetailsPage = 0);
		ApplyFilters();
	}

	// A click: on the button it opens the menu; inside it a row ticks and the menu stays for more; elsewhere it closes.
	private static bool HandleTraitClick()
	{
		if (TraitMenu)
		{
			if (TraitMenuPanel.Contains(Mouse.X, Mouse.Y))
			{
				int row = TraitRowAt();
				if (row >= 0)
				{
					ActivateTraitRow(row);
				}
			}
			else
			{
				TraitMenu = false;
			}
			return true;
		}
		if (TraitFilterTab && BookGeometry.TraitButton.Contains(Mouse.X, Mouse.Y))
		{
			OpenTraitMenu();
			return true;
		}
		return false;
	}

	// Up/Down move the focus (it starts on the first trait), Enter or Space ticks it; Esc closes the menu (the book's Esc chain).
	private static bool HandleTraitMenuKeys(KeyboardState keys)
	{
		int step = (Press(Keys.Down) ? 1 : 0) - (Press(Keys.Up) ? 1 : 0);
		if (step != 0)
		{
			TraitFocus = (TraitFocus < 0) ? 1 : Math.Clamp(TraitFocus + step, 0, TraitRowCount - 1);
			return true;
		}
		if ((Press(Keys.Enter) || Press(Keys.Space)) && TraitFocus >= 0)
		{
			ActivateTraitRow(TraitFocus);
			return true;
		}
		return false;
		bool Press(Keys key)
		{
			return keys.IsKeyDown(key) && !LastKeys.IsKeyDown(key);
		}
	}

	// How many of the items the other filters list have each trait; counted again only when the list or the traits changed.
	private static void CountTraits()
	{
		(int, int) key = (TraitCandidatesVersion, BookItemTraits.Version);
		if (TraitCountsKey == key)
		{
			return;
		}
		TraitCountsKey = key;
		Array.Clear(TraitCounts);
		BookTrait[] rows = BookItemTraits.AllRows;
		foreach (int type in TraitCandidates)
		{
			BookTrait has = BookItemTraits.Get(type);
			if (has == BookTrait.None)
			{
				continue;
			}
			for (int i = 0; i < rows.Length; i++)
			{
				if ((has & rows[i]) != 0)
				{
					TraitCounts[i]++;
				}
			}
		}
	}

	private static void DrawTraitMenu(SpriteBatch sb)
	{
		BookRect panel = TraitMenuPanel;
		if (panel.Contains(Mouse.X, Mouse.Y))
		{
			PendingTip = null;
			Main.HoverItem = new Item();
			Main.hoverItemName = "";
		}
		CountTraits();
		Box(sb, panel.X, panel.Y + 4f, panel.Width, panel.Height, new Color(0, 0, 0, 110), border: false, 12f);
		Box(sb, panel, Panel, border: true, 12f);
		SectionLabel(sb, T("Свойства оружия", "Weapon traits"), panel.X + 16f, panel.Y + 12f, panel.Width - 32f);
		int hovered = TraitRowAt();
		if (hovered >= 0)
		{
			TraitFocus = hovered;
		}
		BookTrait[] rows = BookItemTraits.Rows;
		for (int row = 0; row < TraitRowCount; row++)
		{
			BookRect rect = TraitOption(row);
			if (row == TraitFocus)
			{
				Box(sb, rect, HoverFill, border: false, 6f);
			}
			if (row == 0)
			{
				bool any = TraitFilter != BookTrait.None;
				CenterLabel(sb, T("Показать все", "Show all"), rect.X + 40f, rect.Y + 4f, rect.Width - 52f, rect.Height - 8f, any ? Accent : Muted, 0.9f, rightAligned: false, leftAligned: true);
				if (row == hovered)
				{
					MouseTip(any ? T("Снять все отметки", "Untick every trait") : T("Ничего не отмечено", "Nothing is ticked"));
				}
				continue;
			}
			BookTrait trait = rows[row - 1];
			bool on = (TraitFilter & trait) != 0;
			int count = TraitCounts[Array.IndexOf(BookItemTraits.AllRows, trait)];
			// A tick in a filled box, never colour alone.
			float boxX = rect.X + 12f;
			float boxY = rect.Y + 8f;
			if (on)
			{
				Box(sb, boxX, boxY, 16f, 16f, SelectedFill, border: true, 4f, Accent);
				Line(sb, boxX + 4f, boxY + 8.5f, boxX + 7f, boxY + 11.5f, Accent);
				Line(sb, boxX + 7f, boxY + 11.5f, boxX + 12.5f, boxY + 5f, Accent);
			}
			else
			{
				Box(sb, boxX, boxY, 16f, 16f, FieldFill, border: true, 4f, Border);
			}
			string name = BookItemTraits.Label(trait, Ru);
			CenterLabel(sb, name, rect.X + 40f, rect.Y + 4f, rect.Width - 40f - 56f, rect.Height - 8f, on ? Accent : ((count > 0) ? TextColor : Muted), 0.9f, rightAligned: false, leftAligned: true);
			CenterLabel(sb, Count(count), rect.X + rect.Width - 52f, rect.Y + 4f, 40f, rect.Height - 8f, Muted, 0.8f, rightAligned: true);
			if (row == hovered)
			{
				string listed = Catalogue ? T("Предметов в списке с этим свойством: ", "Listed items with it: ") : T("Рецептов в списке с этим свойством: ", "Listed recipes with it: ");
				MouseTip(name + "\n" + BookItemTraits.Tip(trait, Ru) + "\n" + listed + Count(count) + "\n" + (on ? T("Клик — снять", "Click: untick") : T("Клик — отметить; нужны все отмеченные", "Click: tick; every ticked trait must match")));
			}
		}
		string footer = BookItemTraits.Ready ? T("Нужны все отмеченные · Esc — закрыть", "All ticked must match · Esc: close") : ReadingLine();
		CenterLabel(sb, footer, panel.X + 16f, panel.Y + panel.Height - 26f, panel.Width - 32f, 20f, Muted, 0.75f, rightAligned: false, leftAligned: true);
	}

	// The card's line: the item's traits in order, "" when it has none.
	private static string TraitLine(Item item)
	{
		if (item == null || item.IsAir)
		{
			return "";
		}
		(int, bool, int) key = (item.type, Ru, BookItemTraits.Version);
		if (TraitLineKey == key)
		{
			return TraitLineText;
		}
		TraitLineKey = key;
		BookTrait has = BookItemTraits.Get(item.type);
		List<string> names = new List<string>();
		foreach (BookTrait trait in BookItemTraits.Rows)
		{
			if ((has & trait) != 0)
			{
				names.Add(BookItemTraits.Label(trait, Ru));
			}
		}
		// As many names as fit, then "+N": the hover lists them all.
		string text = string.Join(" · ", names);
		for (int shown = names.Count - 1; shown >= 1 && TextWidth(text, 0.8f) > BookGeometry.DetailHeaderSub.Width; shown--)
		{
			text = string.Join(" · ", names.GetRange(0, shown)) + " +" + (names.Count - shown);
		}
		TraitLineText = text;
		return TraitLineText;
	}

	private static void DrawTraitLine(SpriteBatch sb, Item item, string line, BookRect rect)
	{
		FixedLabel(sb, line, rect.X, rect.Y, Accent, 0.8f, rect.Width, rect.Height);
		if (!MenuOpen && rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(TraitExplanation(item.type));
		}
	}

	// Each trait with where it comes from: the description's own words, the projectile's numbers, or the checked list.
	private static string TraitExplanation(int type)
	{
		StringBuilder tip = new StringBuilder(T("Свойства", "Traits"));
		BookTrait has = BookItemTraits.Get(type);
		foreach (BookTrait trait in BookItemTraits.Rows)
		{
			if ((has & trait) == 0)
			{
				continue;
			}
			BookItemTraits.Why why = BookItemTraits.Reason(type, trait);
			tip.Append("\n· ").Append(BookItemTraits.Label(trait, Ru)).Append(" — ");
			switch (trait)
			{
			case BookTrait.Homing:
			case BookTrait.StealthHoming:
				tip.Append(BookItemTraits.SourceLabel(why.Source, Ru)).Append(": ").Append((why.Source == BookItemTraits.Source.Verified) ? why.Detail : T("«" + why.Detail + "»", "\"" + why.Detail + "\""));
				break;
			case BookTrait.Pierces:
				tip.Append((why.Detail == "-1") ? T("попаданий без предела (penetrate -1)", "no hit limit (penetrate -1)") : (T("до ", "up to ") + why.Detail + T(" попаданий (penetrate)", " hits (penetrate)")));
				break;
			case BookTrait.ThroughWalls:
				tip.Append(T("снаряд не сталкивается с блоками (tileCollide)", "the shot ignores blocks (tileCollide)"));
				break;
			case BookTrait.AutoUse:
				tip.Append(T("autoReuse у предмета", "the item's autoReuse"));
				break;
			default:
				tip.Append(T("флаг снаряда", "the projectile's flag"));
				break;
			}
		}
		if (!BookItemTraits.Ready)
		{
			tip.Append('\n').Append(ReadingLine());
		}
		return tip.ToString();
	}
}
