using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ModernRecipeBook.UI.Book;

// The quick search's look, a command palette in the book's own panels: a borderless field on top (the scope chip, the
// count, clear), the scope chips, the results on the left (light group headers, the typed words marked in the names, the
// reason a row is listed, each row's own button), a preview of the selected result on the right (what it is, what it
// says, where it comes from, everything it can do) and the keys in the footer. Motion is the book's: the panel comes in
// with the backdrop (DrawModal, 150 ms), the selection and the scope pill glide to their new place in about 60 ms;
// nothing else moves (docs/quick-search.md).
public sealed partial class RecipeBookSystem
{
	// Book units. The panel leaves the book's frame showing around it; the list and the preview split it 58 / 42.
	private static BookRect QuickPanel => new BookRect(40f, 32f, 1040f, 632f);

	private static BookRect QuickField => new BookRect(QuickPanel.X + 12f, QuickPanel.Y + 8f, QuickPanel.Width - 24f, 48f);

	private static BookRect QuickClear => new BookRect(QuickPanel.X + QuickPanel.Width - 48f, QuickPanel.Y + 18f, 28f, 28f);

	private static BookRect QuickChipRow => new BookRect(QuickPanel.X + 16f, QuickPanel.Y + 70f, QuickPanel.Width - 32f, 28f);

	private static BookRect QuickList => new BookRect(QuickPanel.X + 8f, QuickPanel.Y + 112f, 592f, QuickPanel.Height - 112f - 52f);

	// The rows, the scroll bar's room taken off.
	private static BookRect QuickListRows => new BookRect(QuickList.X, QuickList.Y, QuickList.Width - 10f, QuickList.Height);

	private static BookRect QuickPreview => new BookRect(QuickPanel.X + 612f, QuickPanel.Y + 112f, QuickPanel.Width - 612f - 12f, QuickPanel.Height - 112f - 60f);

	private static float QuickFooterY => QuickPanel.Y + QuickPanel.Height - 44f;

	// The selected row's fill: between Raised and SelectedFill, so the accent bar carries the selection.
	private static readonly Color QuickSelectedFill = new Color(43, 61, 49);

	// The typed words in names and descriptions.
	private static readonly Color QuickMark = new Color(150, 236, 172);

	// Quiet labels (counts, hints): 5:1 on the panel, as small text needs.
	private static readonly Color QuickFaint = new Color(132, 156, 139);

	// What was drawn last frame and takes a click in the next update: buttons (the row they act on), scope chips.
	private static readonly List<(BookRect Rect, QuickAction Action, int Row)> QuickButtons = new List<(BookRect, QuickAction, int)>();

	private static readonly List<(BookRect Rect, QuickScope Scope)> QuickChips = new List<(BookRect, QuickScope)>();

	// The marks of a drawn text, read once per query (RunQuickSearch clears it).
	private static readonly Dictionary<string, List<(int Start, int Length)>> QuickMarkCache = new Dictionary<string, List<(int, int)>>(StringComparer.Ordinal);

	private static readonly Dictionary<(string Text, int Width), List<string>> QuickWrapCache = new Dictionary<(string, int), List<string>>();

	// The gliding selection and scope pill: where they are drawn now, and the last frame's clock.
	private static float QuickGlideY = float.NaN;

	private static float QuickGlideHeight;

	private static float QuickPillX = float.NaN;

	private static float QuickPillWidth;

	private static long QuickGlideTick;

	// The book is closed: the popup stands over the game where the book would open, on a dimmed screen. It comes in as the
	// book's modals do over the book: 150 ms, ease-out.
	private static void DrawQuickSearchOverGame(SpriteBatch sb)
	{
		if (!QuickSearchOpen)
		{
			return;
		}
		Layout();
		MouseOver = false;
		float t = Math.Min(1f, (float)((Stopwatch.GetTimestamp() - QuickOpenedAt) / (double)Stopwatch.Frequency / 0.15));
		float appear = 1f - (1f - t) * (1f - t) * (1f - t);
		sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth * 2, Main.screenHeight * 2), BackdropVeil * (0.55f * appear));
		DrawModal(sb, appear, DrawQuickSearch);
	}

	// Modal like Help: nothing under it hovers or shows a tooltip.
	private static void DrawQuickSearch(SpriteBatch sb)
	{
		long start = Stopwatch.GetTimestamp();
		PendingTip = null;
		Main.HoverItem = new Item();
		Main.hoverItemName = "";
		Main.LocalPlayer.mouseInterface = true;
		Main.LocalPlayer.cursorItemIconEnabled = false;
		ReadQuickText();
		FollowQuickVersions();
		QuickButtons.Clear();
		QuickChips.Clear();
		BookRect panel = QuickPanel;
		// A soft two-step shadow and the panel; the preview sits in it as an inset card.
		Box(sb, panel.X - 2f, panel.Y + 4f, panel.Width + 4f, panel.Height + 8f, new Color(0, 0, 0, 70), border: false, 18f);
		Box(sb, panel.X, panel.Y + 10f, panel.Width, panel.Height, new Color(0, 0, 0, 60), border: false, 16f);
		Box(sb, panel, Surface, border: true, 14f, new Color(70, 90, 76));
		DrawQuickField(sb);
		Box(sb, panel.X + 1f, panel.Y + 60f, panel.Width - 2f, 1f, Border, border: false, 0f);
		DrawQuickChips(sb);
		DrawQuickRows(sb);
		DrawQuickPreview(sb);
		DrawQuickFooter(sb);
		QuickFrameMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
		QuickFrameMaxMs = Math.Max(QuickFrameMaxMs, QuickFrameMs);
	}

	// The field ------------------------------------------------------------------------------------------------------

	private static void DrawQuickField(SpriteBatch sb)
	{
		BookRect field = QuickField;
		float middle = field.Y + field.Height / 2f;
		Magnifier(sb, field.X + 22f, middle, (QuickQuery.Length > 0) ? TextColor : Muted, 6.5f);
		float textX = field.X + 46f;
		float right = field.X + field.Width - 44f;
		// The scope as a chip at the start of the field, as a palette shows its filter token; Backspace takes it off.
		if (QuickScopeShown != QuickScope.All && !QuickCommandMode)
		{
			string scope = QuickScopeLabel(QuickScopeShown);
			float width = TextWidth(scope, 0.8f) + 20f;
			Box(sb, textX, middle - 13f, width, 26f, SelectedFill, border: false, 13f);
			CenterLabel(sb, scope, textX, middle - 13f, width, 26f, Accent, 0.8f);
			textX += width + 10f;
		}
		string count = QuickFieldCount();
		if (count.Length > 0)
		{
			float width = TextWidth(count, 0.8f) + 4f;
			right -= width + 12f;
			CenterLabel(sb, count, right + 12f, field.Y + 10f, width, field.Height - 20f, QuickFaint, 0.8f, rightAligned: true);
		}
		bool caret = (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0;
		if (QuickQuery.Length == 0)
		{
			if (caret)
			{
				Box(sb, textX, middle - 11f, 2f, 22f, TextColor, border: false, 1f);
			}
			string placeholder = (QuickScopeShown == QuickScope.All) ? L("QuickSearchDesign.Placeholder") : L("QuickSearchDesign.PlaceholderScoped", QuickScopeLabel(QuickScopeShown));
			CenterLabel(sb, placeholder, textX + 8f, field.Y + 4f, right - textX - 8f, field.Height - 8f, QuickFaint, 1.05f, rightAligned: false, leftAligned: true, fitWidth: true);
			return;
		}
		float queryWidth = Math.Min(right - textX, TextWidth(QuickQuery, 1.12f));
		if (QuickAllSelected)
		{
			Box(sb, textX - 3f, middle - 13f, queryWidth + 6f, 26f, Accent * 0.32f, border: false, 4f);
		}
		CenterLabel(sb, QuickQuery, textX, field.Y + 4f, right - textX, field.Height - 8f, TextColor, 1.12f, rightAligned: false, leftAligned: true);
		if (!QuickAllSelected && caret)
		{
			Box(sb, textX + queryWidth + 2f, middle - 11f, 2f, 22f, TextColor, border: false, 1f);
		}
		BookRect clear = QuickClear;
		bool over = clear.Contains(Mouse.X, Mouse.Y);
		if (over)
		{
			Box(sb, clear, HoverFill, border: false, 14f);
			MouseTip(L("Common.ClearSearch"));
		}
		Cross(sb, clear.X + clear.Width / 2f, clear.Y + clear.Height / 2f, 4f, over ? TextColor : Muted);
	}

	// "38 results" (the scope's), or what is still being read.
	private static string QuickFieldCount()
	{
		if (BookEnglishNames.Needed && !BookEnglishNames.Ready)
		{
			return L("QuickSearch.EnglishLoading");
		}
		if (QuickQuery.Trim().Length == 0 || QuickCommandMode)
		{
			return "";
		}
		return L("QuickSearchDesign.Results", QuickScopeCounts[(int)QuickScopeShown]);
	}

	// Scope chips ----------------------------------------------------------------------------------------------------

	private static void DrawQuickChips(SpriteBatch sb)
	{
		BookRect row = QuickChipRow;
		if (QuickCommandMode)
		{
			CenterLabel(sb, L("QuickSearchDesign.CommandsTitle"), row.X + 4f, row.Y, row.Width, row.Height, Muted, 0.8f, rightAligned: false, leftAligned: true);
			return;
		}
		float x = row.X;
		BookRect selected = default;
		List<(BookRect Rect, QuickScope Scope, string Label, string Count)> chips = new List<(BookRect, QuickScope, string, string)>();
		foreach (QuickScope scope in Enum.GetValues<QuickScope>())
		{
			if (!QuickScopeVisible(scope))
			{
				continue;
			}
			string label = QuickScopeLabel(scope);
			string count = (QuickQuery.Trim().Length > 0) ? Count(QuickScopeCounts[(int)scope]) : "";
			float width = TextWidth(label, 0.8f) + ((count.Length > 0) ? (TextWidth(count, 0.72f) + 8f) : 0f) + 24f;
			if (x + width > row.X + row.Width - 120f)
			{
				break;
			}
			BookRect rect = new BookRect(x, row.Y, width, row.Height);
			chips.Add((rect, scope, label, count));
			if (scope == QuickScopeShown)
			{
				selected = rect;
			}
			x += width + 4f;
		}
		// The pill glides to the chosen chip.
		Glide(ref QuickPillX, selected.X, ref QuickPillWidth, selected.Width, snap: chips.Count == 0);
		if (selected.Width > 0f)
		{
			Box(sb, QuickPillX, row.Y, QuickPillWidth, row.Height, SelectedFill, border: false, 14f);
		}
		foreach ((BookRect rect, QuickScope scope, string label, string count) in chips)
		{
			bool on = scope == QuickScopeShown;
			bool hover = !on && rect.Contains(Mouse.X, Mouse.Y);
			if (hover)
			{
				Box(sb, rect, HoverFill, border: false, 14f);
				MouseTip(L("QuickSearchDesign.ScopeTip", label));
			}
			float labelWidth = TextWidth(label, 0.8f) + 2f;
			CenterLabel(sb, label, rect.X + 12f, rect.Y, labelWidth, rect.Height, on ? Accent : (hover ? TextColor : Muted), 0.8f, rightAligned: false, leftAligned: true);
			if (count.Length > 0)
			{
				CenterLabel(sb, count, rect.X + 12f + labelWidth + 6f, rect.Y, TextWidth(count, 0.72f) + 2f, rect.Height, on ? Accent * 0.8f : QuickFaint, 0.72f, rightAligned: false, leftAligned: true);
			}
			QuickChips.Add((rect, scope));
		}
		// Which key switches them.
		string keys = L("QuickSearchDesign.KeyTab");
		float keyWidth = QuickKeyWidth(keys);
		float hintWidth = TextWidth(L("QuickSearchDesign.Scope"), 0.72f) + 2f;
		float hx = row.X + row.Width - keyWidth - 6f - hintWidth;
		HelpKeycap(sb, new BookRect(hx, row.Y + 3f, keyWidth, 22f), keys);
		CenterLabel(sb, L("QuickSearchDesign.Scope"), hx + keyWidth + 6f, row.Y, hintWidth, row.Height, QuickFaint, 0.72f, rightAligned: false, leftAligned: true);
	}

	private static string QuickScopeLabel(QuickScope scope)
	{
		return scope switch
		{
			QuickScope.All => L("QuickSearchDesign.ScopeAll"),
			QuickScope.Items => TabLabel(0),
			QuickScope.Storage => TabLabel(3),
			QuickScope.Recipes => TabLabel(1),
			QuickScope.Bestiary => TabLabel(2),
			QuickScope.Bosses => L("QuickSearchDesign.ScopeBosses"),
			QuickScope.Tracking => TabLabel(5),
			_ => L("QuickSearchDesign.ScopeCategories")
		};
	}

	// Moves a drawn value toward its target, about 60 ms to settle (critically damped: no overshoot); a jump of more than
	// a few rows, or the first frame, snaps.
	private static void Glide(ref float at, float target, ref float size, float targetSize, bool snap)
	{
		long now = Stopwatch.GetTimestamp();
		float dt = Math.Clamp((float)((now - QuickGlideTick) / (double)Stopwatch.Frequency), 0f, 0.1f);
		if (snap || float.IsNaN(at) || Math.Abs(target - at) > 180f)
		{
			at = target;
			size = targetSize;
			return;
		}
		float k = 1f - (float)Math.Exp(-dt / 0.02f);
		at += (target - at) * k;
		size += (targetSize - size) * k;
		if (Math.Abs(target - at) < 0.25f)
		{
			at = target;
		}
	}

	// Rows -----------------------------------------------------------------------------------------------------------

	private static void DrawQuickRows(SpriteBatch sb)
	{
		QuickSegments.Clear();
		QuickScroll = Math.Clamp(QuickScroll, 0, QuickMaxScroll());
		BookRect list = QuickList;
		Box(sb, QuickPanel.X + 1f, QuickPanel.Y + 106f, QuickPanel.Width - 2f, 1f, Border * 0.7f, border: false, 0f);
		List<(int Row, BookRect Rect)> visible = QuickVisible();
		int hovered = QuickRowAt();
		BookRect target = default;
		foreach ((int index, BookRect rect) in visible)
		{
			if (index == QuickSelected)
			{
				target = rect;
			}
		}
		// The selection glides between rows; it is drawn under them.
		Glide(ref QuickGlideY, target.Y, ref QuickGlideHeight, target.Height, snap: target.Height <= 0f);
		QuickGlideTick = Stopwatch.GetTimestamp();
		if (target.Height > 0f)
		{
			bool link = QuickRows[QuickSelected].Link != QuickLink.None;
			float inset = link ? 44f : 0f;
			Box(sb, target.X + inset, QuickGlideY + 2f, target.Width - inset, QuickGlideHeight - 4f, QuickSelectedFill, border: false, 9f);
			Box(sb, target.X + inset + 3f, QuickGlideY + 10f, 3f, Math.Max(6f, QuickGlideHeight - 20f), Accent, border: false, 1.5f);
		}
		foreach ((int index, BookRect rect) in visible)
		{
			DrawQuickRow(sb, index, rect, index == QuickSelected, index == hovered);
		}
		if (QuickRows.Count == 0)
		{
			if (QuickCommandMode)
			{
				EmptyState(sb, list, L("QuickSearchDesign.NoCommand", QuickQuery.TrimStart().Substring(1).Trim()), L("QuickSearchDesign.NoCommandHint"));
			}
			else
			{
				EmptyState(sb, list, L("QuickSearch.EmptyTitle"), L("QuickSearchDesign.EmptyHint"));
			}
		}
		else if (QuickRows[0].Kind is QuickKind.TextSearch or QuickKind.AllScopes)
		{
			float y = list.Y + visible.Sum(((int Row, BookRect Rect) v) => v.Rect.Height) + 24f;
			CenterLabel(sb, L("QuickSearch.NothingFound", QuickQuery.Trim()), list.X + 16f, y, list.Width - 32f, 28f, TextColor, 1f);
			CenterLabel(sb, L("QuickSearchDesign.NothingFoundHint"), list.X + 16f, y + 32f, list.Width - 32f, 22f, Muted, 0.8f);
		}
		else if (QuickQuery.Trim().Length == 0 && visible.Count > 0)
		{
			// The empty field: what it searches, under the recent picks.
			float y = list.Y + visible.Sum(((int Row, BookRect Rect) v) => v.Rect.Height) + 16f;
			foreach (string line in QuickWrap(L("QuickSearchDesign.EmptyHint"), list.Width - 48f, 0.78f))
			{
				if (y + 22f > list.Y + list.Height)
				{
					break;
				}
				CenterLabel(sb, line, list.X + 16f, y, list.Width - 32f, 20f, QuickFaint, 0.78f, rightAligned: false, leftAligned: true);
				y += 20f;
			}
		}
		// A thin scroll bar when the rows do not fit.
		int max = QuickMaxScroll();
		if (max > 0)
		{
			float track = list.Height - 8f;
			float thumb = Math.Max(28f, track * visible.Count / QuickRows.Count);
			float x = list.X + list.Width - 4f;
			Box(sb, x, list.Y + 4f, 3f, track, Raised, border: false, 1.5f);
			Box(sb, x, list.Y + 4f + (track - thumb) * QuickScroll / max, 3f, thumb, Border * 1.6f, border: false, 1.5f);
		}
	}

	private static void DrawQuickRow(SpriteBatch sb, int index, BookRect rect, bool selected, bool hovered)
	{
		QuickRow row = QuickRows[index];
		if (row.Kind == QuickKind.Header)
		{
			string title = QuickGroupName(row.Group);
			float width = TextWidth(title, 0.75f) + 2f;
			CenterLabel(sb, title, rect.X + 14f, rect.Y + 10f, width, 18f, Muted, 0.75f, rightAligned: false, leftAligned: true);
			if (row.Count > 0)
			{
				CenterLabel(sb, Count(row.Count), rect.X + 14f + width + 8f, rect.Y + 10f, 60f, 18f, QuickFaint, 0.72f, rightAligned: false, leftAligned: true);
			}
			return;
		}
		if (row.Kind is QuickKind.ShowAll or QuickKind.TextSearch or QuickKind.AllScopes || row.Link is QuickLink.AllSources or QuickLink.AllLoot)
		{
			DrawQuickMoreRow(sb, row, rect, selected);
			return;
		}
		if (row.Link != QuickLink.None)
		{
			DrawQuickLinkRow(sb, row, rect, selected);
			return;
		}
		bool description = row.Why == QuickWhy.Description;
		float iconSize = 28f;
		BookRect icon = new BookRect(rect.X + 14f, rect.Y + (description ? 8f : ((rect.Height - iconSize) / 2f)), iconSize, iconSize);
		DrawQuickIcon(sb, row, icon);
		float left = rect.X + 54f;
		float right = DrawQuickRowButtons(sb, index, row, rect, selected, hovered) - 12f;
		float lineY = description ? (rect.Y + 5f) : (rect.Y + 4f);
		float lineHeight = description ? 24f : (rect.Height - 8f);
		string name = QuickRowName(row);
		Color nameColor = (row.Kind == QuickKind.Command && !QuickCommandEnabled((QuickCommand)row.Id)) ? Muted : TextColor;
		float nameWidth = Math.Max(0f, Math.Min(TextWidth(name, 0.9f) + 2f, right - left));
		DrawQuickMarked(sb, name, left, lineY, nameWidth, lineHeight, nameColor, 0.9f);
		float at = left + nameWidth + 12f;
		if (description)
		{
			// The description's line that matched, under the name, the typed words marked.
			DrawQuickMarked(sb, row.Hint, left, rect.Y + 28f, right - left, 20f, Muted, 0.78f);
			return;
		}
		string detail = (row.Hint.Length > 0) ? row.Hint : QuickRowDetail(row);
		if (detail.Length > 0 && right - at > 40f)
		{
			Color color = (row.Why == QuickWhy.Concept) ? (Accent * 0.9f) : Muted;
			float width = Math.Min(TextWidth(detail, 0.78f) + 2f, right - at);
			DrawQuickMarked(sb, detail, at, rect.Y + 5f, width, rect.Height - 10f, color, 0.78f);
			return;
		}
		if (right - at <= 40f)
		{
			return;
		}
		// The item's place as its card names it (BookCategoryPath: group › category), a mob's kind and spawn tag; each
		// step opens its category.
		List<(string Label, int Category, bool Mobs)> path = QuickPath(row);
		for (int i = 0; i < path.Count && right - at > 40f; i++)
		{
			if (i > 0)
			{
				at = QuickSeparator(sb, path[i].Mobs ? "·" : "›", at, rect);
			}
			float width = Math.Min(TextWidth(path[i].Label, 0.78f) + 2f, right - at);
			BookRect segment = new BookRect(at, rect.Y + 8f, width, rect.Height - 16f);
			bool over = segment.Contains(Mouse.X, Mouse.Y);
			CenterLabel(sb, path[i].Label, at, rect.Y + 5f, width, rect.Height - 10f, over ? Accent : Muted, 0.78f, rightAligned: false, leftAligned: true);
			if (over)
			{
				Line(sb, at, rect.Y + rect.Height - 11f, at + width - 2f, rect.Y + rect.Height - 11f, Accent, 1f);
			}
			if (path[i].Category >= 0)
			{
				QuickSegments.Add((segment, path[i].Category, path[i].Mobs));
			}
			at += width;
		}
	}

	private static List<(string Label, int Category, bool Mobs)> QuickPath(QuickRow row)
	{
		List<(string, int, bool)> path = new List<(string, int, bool)>();
		if (row.Kind == QuickKind.Item)
		{
			path.AddRange(BookCategoryPath.Of(row.Id).Select((BookCategory c) => (c.Label(), ItemCategoryList.FindIndex((BookCategory listed) => listed.Key == c.Key), false)));
		}
		else if (row.Kind == QuickKind.Mob)
		{
			path.AddRange(BookMobCategoryPath.Of(row.Id).Select((BookBestiary.Tag t) => (BookBestiary.Label(t), BookBestiary.Tags.IndexOf(t), true)));
		}
		return path;
	}

	// The row's own button at its right end (filled on the selected or hovered row, outlined on the others), the other
	// actions as small buttons left of it while the row is hovered, and what the player has of the item. Returns the
	// left edge of all that.
	private static float DrawQuickRowButtons(SpriteBatch sb, int index, QuickRow row, BookRect rect, bool selected, bool hovered)
	{
		float right = rect.X + rect.Width - 8f;
		float middle = rect.Y + ((row.Why == QuickWhy.Description) ? 22f : (rect.Height / 2f));
		if (row.Kind == QuickKind.Command)
		{
			string key = QuickCommandShortcut((QuickCommand)row.Id);
			if (key.Length > 0)
			{
				float width = QuickKeyWidth(key);
				HelpKeycap(sb, new BookRect(right - width, middle - 11f, width, 22f), key);
				right -= width + 10f;
			}
			return right;
		}
		QuickAction primary = QuickPrimary(row);
		// Open is what a click on the row does already: its button shows only on the row in hand.
		if (primary != QuickAction.None && (primary != QuickAction.Open || selected || hovered))
		{
			string label = QuickActionLabel(row, primary);
			float width = Math.Max(56f, TextWidth(label, 0.75f) + 22f);
			BookRect button = new BookRect(right - width, middle - 12f, width, 24f);
			QuickRowButton(sb, button, label, primary, index, row, strong: selected || hovered, accent: primary == QuickAction.Craft);
			right = button.X - 6f;
		}
		if (hovered)
		{
			foreach (QuickAction action in QuickRowExtras(row, primary))
			{
				BookRect button = new BookRect(right - 24f, middle - 12f, 24f, 24f);
				bool over = button.Contains(Mouse.X, Mouse.Y);
				Box(sb, button, over ? HoverFill : Raised, border: false, 7f);
				QuickGlyph(sb, action, row, button, over ? TextColor : Muted);
				if (over)
				{
					MouseTip(QuickActionTip(row, action));
				}
				QuickButtons.Add((button, action, index));
				right = button.X - 4f;
			}
		}
		int type = (row.Kind == QuickKind.Item) ? row.Id : 0;
		int owned = (type > 0) ? QuickOwned(type) : 0;
		if (owned > 0 && !hovered)
		{
			// How many the player has (inventory and nearby storage); the tooltip says where.
			string text = (owned == int.MaxValue) ? "∞" : ("×" + Count(owned));
			float width = TextWidth(text, 0.75f) + 4f;
			CenterLabel(sb, text, right - width, middle - 10f, width, 20f, QuickFaint, 0.75f, rightAligned: true);
			if (Hit(right - width, middle - 10f, width, 20f))
			{
				MouseTip(L("QuickSearchDesign.InStorage", Count(owned)) + "\n" + BookSources.Breakdown(type));
			}
			right -= width + 8f;
		}
		if (row.Kind == QuickKind.Goal && row.Id < TrackedGoals.Count)
		{
			BookRect bar = new BookRect(right - 64f, middle - 3f, 64f, 6f);
			DrawProgressBar(sb, bar.X, bar.Y, bar.Width, bar.Height, TrackedGoals[row.Id]);
			right = bar.X - 10f;
		}
		return right;
	}

	private static void QuickRowButton(SpriteBatch sb, BookRect button, string label, QuickAction action, int index, QuickRow row, bool strong, bool accent)
	{
		bool over = button.Contains(Mouse.X, Mouse.Y);
		if (accent && strong)
		{
			Box(sb, button, over ? new Color(204, 236, 214) : Accent, border: false, 7f);
			DrawPlainLabel(sb, label, new BookRect(button.X, button.Y - 2f, button.Width, button.Height + 4f), Panel, 0.75f);
		}
		else
		{
			Box(sb, button, over ? HoverFill : (strong ? Raised : Surface), border: true, 7f, strong ? new Color(78, 99, 84) : Border);
			CenterLabel(sb, label, button.X + 8f, button.Y, button.Width - 16f, button.Height, (strong || over) ? TextColor : Muted, 0.75f);
		}
		if (over)
		{
			MouseTip(QuickActionTip(row, action));
		}
		QuickButtons.Add((button, action, index));
	}

	// Small glyphs for the hover buttons, drawn with the book's strokes and icons.
	private static void QuickGlyph(SpriteBatch sb, QuickAction action, QuickRow row, BookRect rect, Color color)
	{
		float cx = rect.X + rect.Width / 2f;
		float cy = rect.Y + rect.Height / 2f;
		switch (action)
		{
			case QuickAction.Favorite:
				Star(sb, rect.X + 4f, rect.Y + 4f, 16f, QuickFavorite(row));
				break;
			case QuickAction.HowToGet:
				Magnifier(sb, cx, cy, color, 4f);
				break;
			case QuickAction.UsedIn:
				Line(sb, cx - 6f, cy, cx + 5f, cy, color, 1.5f);
				Chevron(sb, cx + 3f, cy, 3.5f, 3, color);
				break;
			case QuickAction.Track:
				if (row.Kind == QuickKind.Goal || QuickTracked(QuickRecipe(QuickItemOf(row))))
				{
					Line(sb, cx - 5f, cy, cx - 1.5f, cy + 4f, Accent, 1.8f);
					Line(sb, cx - 2f, cy + 4f, cx + 5.5f, cy - 4.5f, Accent, 1.8f);
				}
				else
				{
					Plus(sb, cx, cy, 5f, color);
				}
				break;
			case QuickAction.Shimmer:
				// The Decrafting Interface that does it.
				QuickItemIcon(sb, BookMagicStorage.DecraftingItem, new BookRect(rect.X + 4f, rect.Y + 4f, 16f, 16f));
				break;
			case QuickAction.Stats:
				Box(sb, cx - 6f, cy + 1f, 3f, 5f, color, border: false, 1f);
				Box(sb, cx - 1.5f, cy - 3f, 3f, 9f, color, border: false, 1f);
				Box(sb, cx + 3f, cy - 6f, 3f, 12f, color, border: false, 1f);
				break;
		}
	}

	// A link under its match: an elbow from the parent's icon, the relation muted and the name it points to brighter.
	private static void DrawQuickLinkRow(SpriteBatch sb, QuickRow row, BookRect rect, bool selected)
	{
		float elbow = rect.X + 28f;
		Line(sb, elbow, rect.Y - 6f, elbow, rect.Y + rect.Height / 2f, Border, 1.5f);
		Line(sb, elbow, rect.Y + rect.Height / 2f, rect.X + 48f, rect.Y + rect.Height / 2f, Border, 1.5f);
		BookRect icon = new BookRect(rect.X + 54f, rect.Y + (rect.Height - 20f) / 2f, 20f, 20f);
		DrawQuickIcon(sb, row, icon);
		string target = QuickRowBaseName(row);
		string phrase = QuickRowName(row);
		int at = phrase.IndexOf(target, StringComparison.Ordinal);
		float x = rect.X + 82f;
		float limit = rect.X + rect.Width - 12f;
		Color relation = selected ? Muted : QuickFaint;
		Color name = selected ? TextColor : Muted;
		if (at < 0)
		{
			CenterLabel(sb, phrase, x, rect.Y + 3f, limit - x, rect.Height - 6f, name, 0.8f, rightAligned: false, leftAligned: true);
			return;
		}
		// The localized phrase keeps its own word order ("Dropped by {0}" / «Падает с: {0}»): only its parts change colour.
		foreach ((string text, Color color) in new (string, Color)[3]
		{
			(phrase.Substring(0, at), relation),
			(target, name),
			(phrase.Substring(at + target.Length), relation)
		})
		{
			if (text.Length == 0 || x >= limit)
			{
				continue;
			}
			float width = Math.Min(QuickAdvance(text, 0.8f), limit - x);
			CenterLabel(sb, text, x, rect.Y + 3f, width + 2f, rect.Height - 6f, color, 0.8f, rightAligned: false, leftAligned: true);
			x += width;
		}
	}

	// "All 11 on the Items tab ›", "Search item descriptions on the Items tab", "Search everything".
	private static void DrawQuickMoreRow(SpriteBatch sb, QuickRow row, BookRect rect, bool selected)
	{
		bool linked = row.Link != QuickLink.None;
		string more = QuickMoreText(row);
		float x = rect.X + (linked ? 82f : 54f);
		if (row.Kind is QuickKind.TextSearch or QuickKind.AllScopes)
		{
			Magnifier(sb, rect.X + 28f, rect.Y + rect.Height / 2f, selected ? Accent : Muted, 5f);
		}
		float width = Math.Min(TextWidth(more, 0.8f) + 2f, rect.Width - (x - rect.X) - 24f);
		CenterLabel(sb, more, x, rect.Y + 3f, width, rect.Height - 6f, selected ? TextColor : Accent, 0.8f, rightAligned: false, leftAligned: true);
		Chevron(sb, x + width + 10f, rect.Y + rect.Height / 2f, 3.5f, 3, selected ? TextColor : Accent);
	}

	private static string QuickMoreText(QuickRow row)
	{
		return row.Link switch
		{
			QuickLink.AllSources => L("QuickSearch.AllSources", Count(row.Count)),
			QuickLink.AllLoot => L("QuickSearch.AllLoot", Count(row.Count)),
			_ => row.Kind switch
			{
				QuickKind.TextSearch => (row.Group == QuickGroup.Mobs) ? L("QuickSearchDesign.SearchBestiary") : L("QuickSearch.SearchDescriptions"),
				QuickKind.AllScopes => L("QuickSearchDesign.SearchEverything", Count(row.Count)),
				_ => row.Group switch
				{
					QuickGroup.Items => L("QuickSearch.AllInItems", Count(row.Count)),
					QuickGroup.Mobs => L("QuickSearch.AllInBestiary", Count(row.Count)),
					QuickGroup.Storage => L("QuickSearchDesign.AllInStorage", Count(row.Count)),
					QuickGroup.Recipes => L("QuickSearchDesign.AllInRecipes", Count(row.Count)),
					_ => L("QuickSearch.More", Count(row.Count - QuickRows.Count((QuickRow r) => r.Group == row.Group && r.Kind != QuickKind.Header && r.Kind != QuickKind.ShowAll)))
				}
			}
		};
	}

	// Labels are placed by their ink, so a spaced " · " would lose its spaces: the gaps are explicit.
	private static float QuickSeparator(SpriteBatch sb, string glyph, float at, BookRect rect)
	{
		at += 6f;
		float width = TextWidth(glyph, 0.78f);
		CenterLabel(sb, glyph, at, rect.Y + 5f, width + 2f, rect.Height - 10f, QuickFaint, 0.78f, rightAligned: false, leftAligned: true);
		return at + width + 6f;
	}

	// How far a text advances the pen (its marks start where the text before them ends).
	private static float QuickAdvance(string text, float size)
	{
		return FontAssets.MouseText.Value.MeasureString(text).X * size * Math.Clamp(TextScale, 0.9f, 1.15f);
	}

	// A label with the typed words marked: the label laid out as CenterLabel does (left-aligned, cut with "..." when too
	// long), then drawn in pieces, the marked ones in QuickMark.
	private static void DrawQuickMarked(SpriteBatch sb, string text, float x, float y, float width, float height, Color color, float size)
	{
		text = BookRules.PlainText(text).Replace("\n", " ");
		BookGlyphLayout.Bounds reference = Ink(ReferenceGlyphs);
		BookTypography.Line line = BookTypography.FixedLine(text, width, height, size, TextScale, (string value) =>
		{
			BookGlyphLayout.Bounds bounds = Ink(value);
			return (Width: bounds.Width, Height: bounds.Height);
		}, reference.Y, reference.Height);
		if (!QuickMarkCache.TryGetValue(line.Value, out List<(int Start, int Length)> marks))
		{
			marks = (QuickQuery.Trim().Length == 0 || QuickCommandMode) ? new List<(int, int)>() : BookQuickRank.Marks(line.Value, BookRules.SearchTerms(QuickQuery));
			QuickMarkCache[line.Value] = marks;
		}
		BookGlyphLayout.Bounds ink = Ink(line.Value);
		Vector2 pos = Origin + new Vector2(x - ink.X * line.Scale, y + line.Y) * Scale;
		if (line.Truncated && Hit(x, y, width, height))
		{
			MouseTip(text);
		}
		if (marks.Count == 0)
		{
			DrawInkLine(sb, line.Value, pos, color, line.Scale);
			return;
		}
		DynamicSpriteFont font = FontAssets.MouseText.Value;
		int done = 0;
		foreach ((int start, int length) in marks)
		{
			int end = Math.Min(line.Value.Length, start + length);
			if (start >= end)
			{
				continue;
			}
			Piece(done, start, color);
			Piece(start, end, QuickMark);
			done = end;
		}
		Piece(done, line.Value.Length, color);
		void Piece(int from, int to, Color tint)
		{
			if (to > from)
			{
				float offset = font.MeasureString(line.Value.Substring(0, from)).X * line.Scale * Scale;
				DrawInkLine(sb, line.Value.Substring(from, to - from), pos + new Vector2(offset, 0f), tint, line.Scale);
			}
		}
	}

	// A vanilla texture nobody drew yet is read from disk on the first frame that asks (Main.LoadItem, several ms each:
	// the first query's rows took 50-90 ms). The row asks for it in the background and shows the icon once it is in, so
	// typing never waits for icons.
	private static bool QuickTextureReady(Asset<Texture2D>[] textures, int index)
	{
		if (index <= 0 || index >= textures.Length)
		{
			return false;
		}
		Asset<Texture2D> asset = textures[index];
		if (asset.State == AssetState.NotLoaded)
		{
			Main.Assets.Request<Texture2D>(asset.Name, AssetRequestMode.AsyncLoad);
		}
		return asset.IsLoaded;
	}

	private static void DrawQuickIcon(SpriteBatch sb, QuickRow row, BookRect icon)
	{
		switch (row.Kind)
		{
			case QuickKind.Item:
				QuickItemIcon(sb, row.Id, icon);
				break;
			case QuickKind.Goal:
				QuickItemIcon(sb, QuickItemOf(row), icon);
				break;
			case QuickKind.Mob:
			{
				int netId = BookBestiary.Mobs[row.Id].NetId;
				if (ContentSamples.NpcsByNetId.TryGetValue(netId, out NPC npc) && QuickTextureReady(TextureAssets.Npc, npc.type))
				{
					DrawNpcIcon(sb, netId, icon);
				}
				break;
			}
			case QuickKind.Stage:
				DrawBossHead(sb, row.Id, icon, 1f);
				break;
			case QuickKind.ItemCategory:
				QuickItemIcon(sb, ItemCategoryList[row.Id].IconType, icon);
				break;
			case QuickKind.MobCategory:
				if (MobCategories[row.Id].IconType > 0)
				{
					QuickItemIcon(sb, MobCategories[row.Id].IconType, icon);
				}
				else
				{
					DrawCategoryIcon(sb, MobCategories[row.Id], new BookRect(icon.X - 8f + (icon.Width - 20f) / 2f, icon.Y - 4f + (icon.Height - 20f) / 2f, icon.Width, icon.Height), mobs: true);
				}
				break;
			case QuickKind.Mod:
				DrawModIcon(sb, QuickMods[row.Id], icon);
				break;
			case QuickKind.Command:
				Box(sb, icon, Raised, border: false, 7f);
				Chevron(sb, icon.X + icon.Width / 2f, icon.Y + icon.Height / 2f, 4f, 3, Accent);
				break;
		}
	}

	private static void QuickItemIcon(SpriteBatch sb, int type, BookRect icon)
	{
		if (QuickTextureReady(TextureAssets.Item, type) && ContentSamples.ItemsByType.TryGetValue(type, out Item item))
		{
			Icon(sb, item, icon.X, icon.Y, icon.Width, tooltip: false);
		}
	}

	// The mod's icon.png; vanilla gets the Guide's book.
	private static void DrawModIcon(SpriteBatch sb, string mod, BookRect rect)
	{
		if (!QuickModIcons.TryGetValue(mod, out Asset<Texture2D> asset))
		{
			if (mod == "Terraria" || !ModContent.RequestIfExists(mod + "/icon", out asset, AssetRequestMode.AsyncLoad))
			{
				asset = null;
			}
			QuickModIcons[mod] = asset;
		}
		if (asset == null || !asset.IsLoaded)
		{
			QuickItemIcon(sb, ItemID.Book, rect);
			return;
		}
		Texture2D texture = asset.Value;
		float scale = Math.Min(rect.Width / texture.Width, rect.Height / texture.Height);
		sb.Draw(texture, Origin + new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f) * Scale, null, Color.White * GraphicsOpacity, 0f, texture.Size() / 2f, scale * Scale, SpriteEffects.None, 0f);
	}

	// What a row is, for the preview's badge and a mixed group's rows.
	private static string QuickKindText(QuickRow row)
	{
		return row.Kind switch
		{
			QuickKind.Item => L("QuickSearch.BadgeItem"),
			QuickKind.Mob => (BookBestiary.Mobs[row.Id].Kind == BookMobKind.Boss) ? L("QuickSearch.BadgeBoss") : L("QuickSearch.BadgeMob"),
			QuickKind.Stage => L("QuickSearch.BadgeStage"),
			QuickKind.ItemCategory or QuickKind.MobCategory => L("QuickSearch.BadgeCategory"),
			QuickKind.Mod => L("QuickSearch.BadgeMod"),
			QuickKind.Goal => TabLabel(5),
			_ => ""
		};
	}

	// The name a row points to, before a link's phrase is put around it.
	private static string QuickRowBaseName(QuickRow row)
	{
		return row.Kind switch
		{
			QuickKind.Item => ContentSamples.ItemsByType[row.Id].Name,
			QuickKind.Mob => BookBestiary.Mobs[row.Id].Sample.FullName,
			QuickKind.Stage => L("Stages.Before", BookSteps.Name(row.Id)),
			QuickKind.ItemCategory => ItemCategoryList[row.Id].Label(),
			QuickKind.MobCategory => BookBestiary.Label(BookBestiary.Tags[row.Id]),
			QuickKind.Mod => ModName(QuickMods[row.Id]),
			QuickKind.Goal => (row.Id < TrackedGoals.Count) ? GoalTitle(TrackedGoals[row.Id]) : "",
			QuickKind.Command => QuickCommandLabel((QuickCommand)row.Id),
			_ => L("QuickSearch.SearchDescriptions")
		};
	}

	private static string QuickRowName(QuickRow row)
	{
		string name = QuickRowBaseName(row);
		return row.Link switch
		{
			QuickLink.DroppedBy => L("QuickSearch.DroppedBy", name),
			QuickLink.FoundIn => L("QuickSearch.FoundIn", name),
			QuickLink.Drops => L("QuickSearch.Drops", name),
			_ => name
		};
	}

	// After the name: a stage's state, a category's or a mod's size, a goal's progress, a command's hint; in a group of
	// mixed kinds (opened recently) what the row is.
	private static string QuickRowDetail(QuickRow row)
	{
		switch (row.Kind)
		{
			case QuickKind.Stage:
			{
				BookSteps.Step boss = BookSteps.All[row.Id];
				return (row.Id == BookSteps.Next()) ? L("Stages.NextBoss") : ((!boss.Available()) ? L("Stages.NotInThisWorldTheOther") : (BookSteps.SafeDowned(boss) ? L("Stages.AlreadyDown") : L("Stages.NotDownYet")));
			}
			case QuickKind.ItemCategory:
				return (row.Id < QuickCategorySizes.Length) ? L("QuickSearch.ItemCount", QuickCategorySizes[row.Id]) : "";
			case QuickKind.MobCategory:
				return (BookBestiary.Tags[row.Id].Key == BookBestiary.RecentKey) ? "" : L("QuickSearch.MobCount", BookBestiary.Tags[row.Id].Members.Count);
			case QuickKind.Mod:
			{
				(int items, int mobs) = QuickModSizes.GetValueOrDefault(QuickMods[row.Id]);
				return (items > 0) ? L("QuickSearch.ItemCount", items) : L("QuickSearch.MobCount", mobs);
			}
			case QuickKind.Goal:
				return (row.Id < TrackedGoals.Count) ? L("QuickSearchDesign.GoalProgress", (int)Math.Round(GoalFraction(TrackedGoals[row.Id]) * 100f), CraftsRatio(TrackedGoals[row.Id])) : "";
			case QuickKind.Command:
				return QuickCommandTip((QuickCommand)row.Id);
			case QuickKind.Item or QuickKind.Mob when row.Group == QuickGroup.Opened:
				return QuickKindText(row);
			default:
				return "";
		}
	}

	private static string QuickGroupName(QuickGroup group)
	{
		return group switch
		{
			QuickGroup.Items => TabLabel(0),
			QuickGroup.Mobs => TabLabel(2),
			QuickGroup.Stages => L("QuickSearch.GroupStages"),
			QuickGroup.ItemCategories => L("QuickSearch.GroupItemCategories"),
			QuickGroup.MobCategories => L("QuickSearch.GroupMobCategories"),
			QuickGroup.Mods => L("QuickSearch.GroupMods"),
			QuickGroup.Goals or QuickGroup.Tracked => TabLabel(5),
			QuickGroup.Storage => TabLabel(3),
			QuickGroup.Recipes => TabLabel(1),
			QuickGroup.Bosses => L("QuickSearchDesign.ScopeBosses"),
			QuickGroup.Commands => L("QuickSearchDesign.CommandsTitle"),
			QuickGroup.Opened => L("QuickSearch.GroupOpened"),
			QuickGroup.Crafted => L("QuickSearch.GroupCrafted"),
			_ => L("QuickSearch.GroupSeen")
		};
	}

	// The preview -----------------------------------------------------------------------------------------------------

	private static void DrawQuickPreview(SpriteBatch sb)
	{
		BookRect pane = QuickPreview;
		Box(sb, pane, Panel, border: false, 12f);
		QuickRow? selected = (QuickSelected >= 0 && QuickSelected < QuickRows.Count) ? QuickRows[QuickSelected] : null;
		if (!selected.HasValue || selected.Value.Kind is QuickKind.Header or QuickKind.ShowAll or QuickKind.TextSearch or QuickKind.AllScopes)
		{
			DrawQuickTips(sb, pane);
			return;
		}
		QuickRow row = selected.Value;
		// A link previews what it points to; "All ways to get it" its item.
		QuickRow subject = row with { Link = QuickLink.None };
		float x = pane.X + 18f;
		float width = pane.Width - 36f;
		float y = pane.Y + 18f;
		// Header: a big icon in its tile, the name, what it is and where it sits.
		BookRect tile = new BookRect(x, y, 60f, 60f);
		Box(sb, tile, Raised, border: true, 12f, new Color(70, 90, 76));
		DrawQuickIcon(sb, subject, new BookRect(tile.X + 10f, tile.Y + 10f, 40f, 40f));
		float tx = x + 74f;
		float tw = width - 74f;
		FixedLabel(sb, QuickRowBaseName(subject), tx, y + 2f, TextColor, 1.02f, tw, 26f);
		string kind = QuickKindText(subject);
		float lineY = y + 30f;
		if (kind.Length > 0)
		{
			float badge = TextWidth(kind, 0.72f) + 16f;
			Box(sb, tx, lineY + 1f, badge, 18f, Raised, border: false, 9f);
			CenterLabel(sb, kind, tx, lineY + 1f, badge, 18f, Muted, 0.72f);
			string where = QuickPreviewWhere(subject);
			FixedLabel(sb, where, tx + badge + 8f, lineY, Muted, 0.78f, tw - badge - 8f, 20f);
		}
		string origin = QuickPreviewOrigin(subject);
		if (origin.Length > 0)
		{
			FixedLabel(sb, origin, tx, lineY + 20f, QuickFaint, 0.75f, tw, 18f);
		}
		y += 72f;
		// The player's own: in storage, can be crafted, tracked, a favorite.
		y = DrawQuickStatus(sb, subject, x, y, width);
		List<QuickAction> actions = QuickActionsOf(row);
		float actionsTop = QuickActionGridTop(pane, actions.Count);
		float bottom = actionsTop - 10f;
		y = DrawQuickPreviewBody(sb, row, subject, x, y + 6f, width, bottom);
		DrawQuickActionGrid(sb, row, actions, pane, actionsTop);
	}

	// "Weapons › Whips", "Bosses · Space", a category's size.
	private static string QuickPreviewWhere(QuickRow row)
	{
		List<(string Label, int Category, bool Mobs)> path = QuickPath(row);
		if (path.Count > 0)
		{
			return string.Join(path[0].Mobs ? " · " : " › ", path.Select(((string Label, int Category, bool Mobs) p) => p.Label));
		}
		return (row.Kind == QuickKind.Goal) ? "" : QuickRowDetail(row);
	}

	// "Calamity Mod · Hardmode": the mod, and an item's stage.
	private static string QuickPreviewOrigin(QuickRow row)
	{
		int type = QuickItemOf(row);
		if (type > 0)
		{
			Item item = ContentSamples.ItemsByType[type];
			return ModName(item.ModItem?.Mod.Name ?? "Terraria") + " · " + StageName(item, null);
		}
		if (row.Kind == QuickKind.Mob)
		{
			return ModName(BookBestiary.Mobs[row.Id].Mod);
		}
		return "";
	}

	private static float DrawQuickStatus(SpriteBatch sb, QuickRow row, float x, float y, float width)
	{
		// Tip: where they are (0), what the craft takes (1), the goal's crafts (2); read only while hovered.
		List<(string Text, Color Color, int Tip)> pills = new List<(string, Color, int)>();
		int type = QuickItemOf(row);
		Recipe craftable = null;
		TrackedGoal goal = null;
		if (type > 0)
		{
			int owned = QuickOwned(type);
			if (owned > 0)
			{
				pills.Add((L("QuickSearchDesign.InStorage", (owned == int.MaxValue) ? "∞" : Count(owned)), TextColor, 0));
			}
			craftable = QuickCraftable(type);
			if (craftable != null)
			{
				pills.Add((L("QuickSearchDesign.CanCraft", Count(Math.Max(1, CachedAvailableBatches(craftable)))), EnoughColor, 1));
			}
			goal = TrackedGoals.FirstOrDefault((TrackedGoal g) => g.Recipe?.createItem.type == type || g.Parts.Any((Item p) => p.type == type));
			if (goal != null)
			{
				pills.Add((L("QuickSearchDesign.Tracked", (int)Math.Round(GoalFraction(goal) * 100f)), Accent, 2));
			}
		}
		if (row.Kind is QuickKind.Item or QuickKind.Mob or QuickKind.Goal && QuickFavorite(row))
		{
			pills.Add((L("QuickSearchDesign.Favorite"), Caution, -1));
		}
		if (pills.Count == 0)
		{
			return y;
		}
		float px = x;
		foreach ((string text, Color color, int tip) in pills)
		{
			float w = TextWidth(text, 0.74f) + 20f;
			if (px + w > x + width)
			{
				break;
			}
			Box(sb, px, y, w, 22f, Raised, border: false, 11f);
			CenterLabel(sb, text, px, y, w, 22f, color, 0.74f);
			if (tip >= 0 && Hit(px, y, w, 22f))
			{
				MouseTip(tip switch
				{
					0 => BookSources.Breakdown(type),
					1 => BookCrafting.Sources(craftable, 1),
					_ => CraftsRatio(goal)
				});
			}
			px += w + 6f;
		}
		y += 30f;
		// Where the player's stacks are, as the Storage tab's details list them ("Inventory: 2 · Magic Storage: 15").
		if (type > 0 && QuickOwned(type) > 0)
		{
			FixedLabel(sb, BookSources.Breakdown(type), x, y - 4f, QuickFaint, 0.74f, width, 20f);
			y += 22f;
		}
		return y;
	}

	// What the result says and where it comes from, as far as the room above the actions allows.
	private static float DrawQuickPreviewBody(SpriteBatch sb, QuickRow row, QuickRow subject, float x, float y, float width, float bottom)
	{
		switch (subject.Kind)
		{
			case QuickKind.Item:
			case QuickKind.Goal:
			{
				int type = QuickItemOf(subject);
				ItemText text = ItemTextOf(type);
				string description = (text == null) ? "" : (text.Shown + ((text.BonusShown.Length > 0) ? ("\n" + L("QuickSearchDesign.SetBonus", text.BonusShown)) : ""));
				if (row.Why == QuickWhy.Description && text != null && text.Shown.Length == 0 && text.EnglishShown.Length > 0)
				{
					description = text.EnglishShown;
				}
				y = DrawQuickParagraph(sb, L("QuickSearchDesign.Description"), description, x, y, width, bottom - 110f);
				y = DrawQuickSources(sb, type, x, y, width, bottom);
				break;
			}
			case QuickKind.Mob:
				y = DrawQuickMobStats(sb, subject.Id, x, y, width);
				y = DrawQuickParagraph(sb, L("QuickSearchDesign.Flavor"), QuickFlavor[subject.Id].Shown, x, y, width, bottom - 110f);
				y = DrawQuickLoot(sb, subject.Id, x, y, width, bottom);
				break;
			case QuickKind.ItemCategory:
			case QuickKind.MobCategory:
				y = DrawQuickMembers(sb, subject, x, y, width, bottom);
				break;
			case QuickKind.Stage:
			{
				y = DrawQuickParagraph(sb, "", L("QuickSearchDesign.StageTip", BookSteps.Name(subject.Id)), x, y, width, bottom);
				int boss = QuickStageMob(subject.Id);
				if (boss >= 0)
				{
					y = DrawQuickMobStats(sb, boss, x, y, width);
					y = DrawQuickLoot(sb, boss, x, y, width, bottom);
				}
				break;
			}
			case QuickKind.Command:
			{
				y = DrawQuickParagraph(sb, "", QuickCommandTip((QuickCommand)subject.Id), x, y, width, bottom);
				string key = QuickCommandShortcut((QuickCommand)subject.Id);
				if (key.Length > 0)
				{
					// The book's own key for it, where it works without the search.
					string where = L("QuickSearchDesign.ShortcutInBook");
					float label = TextWidth(where, 0.75f) + 4f;
					CenterLabel(sb, where, x, y, label, 22f, QuickFaint, 0.75f, rightAligned: false, leftAligned: true);
					HelpKeycap(sb, new BookRect(x + label + 6f, y, QuickKeyWidth(key), 22f), key);
					y += 32f;
				}
				break;
			}
			case QuickKind.Mod:
				y = DrawQuickParagraph(sb, "", L("QuickSearchDesign.ModTip", ModName(QuickMods[subject.Id])), x, y, width, bottom);
				break;
		}
		return y;
	}

	// A titled block of wrapped text, the typed words marked; cut with "..." where the room ends.
	private static float DrawQuickParagraph(SpriteBatch sb, string title, string text, float x, float y, float width, float bottom)
	{
		if (string.IsNullOrWhiteSpace(text) || y + 40f > bottom)
		{
			return y;
		}
		if (title.Length > 0)
		{
			CenterLabel(sb, title, x, y, width, 18f, QuickFaint, 0.72f, rightAligned: false, leftAligned: true);
			y += 22f;
		}
		List<string> lines = QuickWrap(text, width, 0.78f);
		int room = Math.Max(1, (int)((bottom - y) / 20f));
		for (int i = 0; i < lines.Count && i < room; i++)
		{
			string line = (i == room - 1 && i < lines.Count - 1) ? (lines[i] + "...") : lines[i];
			DrawQuickMarked(sb, line, x, y, width + 40f, 20f, new Color(214, 226, 217), 0.78f);
			y += 20f;
		}
		return y + 10f;
	}

	// Words onto lines that fit the width; the text's own line breaks kept. Cached: the preview asks every frame.
	private static List<string> QuickWrap(string text, float width, float size)
	{
		if (QuickWrapCache.TryGetValue((text, (int)width), out List<string> lines))
		{
			return lines;
		}
		lines = new List<string>();
		foreach (string paragraph in text.Split('\n'))
		{
			string line = "";
			foreach (string word in paragraph.Split(' '))
			{
				string next = (line.Length == 0) ? word : (line + " " + word);
				if (line.Length > 0 && TextWidth(next, size) > width)
				{
					lines.Add(line);
					line = word;
				}
				else
				{
					line = next;
				}
			}
			if (line.Trim().Length > 0)
			{
				lines.Add(line);
			}
		}
		if (QuickWrapCache.Count > 64)
		{
			QuickWrapCache.Clear();
		}
		QuickWrapCache[(text, (int)width)] = lines;
		return lines;
	}

	// How to Get's first rows (the read waits until the selection rests; until then a quiet placeholder of its shape).
	private static float DrawQuickSources(SpriteBatch sb, int type, float x, float y, float width, float bottom)
	{
		if (y + 60f > bottom)
		{
			return y;
		}
		CenterLabel(sb, L("Common.HowToGet"), x, y, width, 18f, QuickFaint, 0.72f, rightAligned: false, leftAligned: true);
		y += 22f;
		if (!QuickFacts.TryGetValue((QuickKind.Item, type), out QuickFact facts))
		{
			for (int i = 0; i < 2 && y + 24f <= bottom; i++)
			{
				Box(sb, x, y + 6f, 18f, 14f, Raised, border: false, 4f);
				Box(sb, x + 28f, y + 8f, width * (0.55f - i * 0.15f), 10f, Raised, border: false, 5f);
				y += 26f;
			}
			return y + 6f;
		}
		int recipes = BookRecipeVariants.For(type).Count;
		float first = y;
		if (recipes > 0 && y + 24f <= bottom)
		{
			Recipe recipe = QuickRecipe(type);
			// The station the recipe needs (a recipe without one is made by hand), its item as the icon.
			int station = recipe?.requiredTile.Where((int t) => t >= 0).DefaultIfEmpty(-1).First() ?? (-1);
			int stationItem = (station >= 0) ? TileLoader.GetItemDropFromTypeAndStyle(station) : 0;
			QuickItemIcon(sb, (stationItem > 0) ? stationItem : ((recipe != null && recipe.requiredItem.Count > 0) ? recipe.requiredItem[0].type : type), new BookRect(x, y + 2f, 20f, 20f));
			FixedLabel(sb, (station >= 0) ? L("QuickSearchDesign.Crafted", StationName(station)) : L("QuickSearchDesign.ByHand"), x + 28f, y + 2f, TextColor * 0.9f, 0.78f, width - 28f, 20f);
			y += 24f;
		}
		foreach (BookAcquisitionEntry entry in facts.Sources)
		{
			if (y + 24f > bottom)
			{
				break;
			}
			DrawAcquisitionIcon(sb, entry, new BookRect(x, y + 2f, 20f, 20f));
			float chance = (entry.Chance.Length > 0) ? (TextWidth(entry.Chance, 0.75f) + 4f) : 0f;
			FixedLabel(sb, entry.Name, x + 28f, y + 2f, TextColor * 0.9f, 0.78f, width - 36f - chance, 20f);
			if (chance > 0f)
			{
				CenterLabel(sb, entry.Chance, x + width - chance, y + 2f, chance, 20f, Muted, 0.75f, rightAligned: true);
			}
			y += 24f;
			if (y > bottom - 24f || facts.Sources.IndexOf(entry) >= 2)
			{
				break;
			}
		}
		if (y == first)
		{
			FixedLabel(sb, L("Common.NoRegularSource"), x, y, Muted, 0.78f, width, 20f);
			y += 24f;
		}
		return y + 6f;
	}

	// Life, defense and damage once the Bestiary shows them, as the card's Stats; the unlock hint before that.
	private static float DrawQuickMobStats(SpriteBatch sb, int mob, float x, float y, float width)
	{
		if (!QuickFacts.TryGetValue((QuickKind.Mob, mob), out QuickFact facts))
		{
			return y;
		}
		// Locked as the card's Stats view is, with its hint: kill it, see it or talk to it once.
		NPC sample = BookBestiary.Mobs[mob].Sample;
		string text = facts.Stats.HasValue ? L("QuickSearchDesign.MobStats", Count(facts.Stats.Value.Life), Count(facts.Stats.Value.Defense), Count(facts.Stats.Value.Damage)) : (sample.CountsAsACritter ? L("MobCard.SeeItOnce") : (sample.isLikeATownNPC ? L("MobCard.TalkToThemOnce") : L("MobCard.KillItOnceToSeeThem")));
		CenterLabel(sb, L("QuickSearchDesign.Stats"), x, y, width, 18f, QuickFaint, 0.72f, rightAligned: false, leftAligned: true);
		FixedLabel(sb, text, x, y + 22f, facts.Stats.HasValue ? TextColor : Muted, 0.78f, width, 20f);
		return y + 50f;
	}

	private static float DrawQuickLoot(SpriteBatch sb, int mob, float x, float y, float width, float bottom)
	{
		if (y + 60f > bottom)
		{
			return y;
		}
		CenterLabel(sb, L("QuickSearchDesign.Loot"), x, y, width, 18f, QuickFaint, 0.72f, rightAligned: false, leftAligned: true);
		y += 22f;
		if (!QuickFacts.TryGetValue((QuickKind.Mob, mob), out QuickFact facts))
		{
			Box(sb, x + 28f, y + 8f, width * 0.5f, 10f, Raised, border: false, 5f);
			return y + 32f;
		}
		foreach (BookEnemyLoot loot in facts.Loot.Take(4))
		{
			if (y + 24f > bottom)
			{
				break;
			}
			QuickItemIcon(sb, loot.ItemType, new BookRect(x, y + 2f, 20f, 20f));
			string chance = BookAcquisitionRules.Chance(loot.Rules[0].Chance);
			float chanceWidth = TextWidth(chance, 0.75f) + 4f;
			FixedLabel(sb, ContentSamples.ItemsByType[loot.ItemType].Name, x + 28f, y + 2f, TextColor * 0.9f, 0.78f, width - 36f - chanceWidth, 20f);
			CenterLabel(sb, chance, x + width - chanceWidth, y + 2f, chanceWidth, 20f, Muted, 0.75f, rightAligned: true);
			y += 24f;
		}
		if (facts.Loot.Count == 0)
		{
			FixedLabel(sb, L("QuickSearchDesign.NoLoot"), x, y, Muted, 0.78f, width, 20f);
			y += 24f;
		}
		return y + 6f;
	}

	// A category's first members as a strip of icons.
	private static float DrawQuickMembers(SpriteBatch sb, QuickRow row, float x, float y, float width, float bottom)
	{
		IEnumerable<int> members;
		bool mobs = row.Kind == QuickKind.MobCategory;
		if (mobs)
		{
			members = BookBestiary.Tags[row.Id].Members;
		}
		else
		{
			members = AllItems.Where((int type) => ItemCategories.TryGetValue(type, out HashSet<int> c) && c.Contains(row.Id));
		}
		int perRow = (int)(width / 44f);
		int shown = 0;
		float top = y;
		foreach (int member in members.Take(perRow * 2))
		{
			BookRect cell = new BookRect(x + shown % perRow * 44f, top + shown / perRow * 44f, 38f, 38f);
			if (cell.Y + cell.Height > bottom)
			{
				break;
			}
			Box(sb, cell, Raised, border: false, 8f);
			if (mobs)
			{
				DrawNpcIcon(sb, BookBestiary.Mobs[member].NetId, new BookRect(cell.X + 5f, cell.Y + 5f, 28f, 28f));
			}
			else
			{
				QuickItemIcon(sb, member, new BookRect(cell.X + 7f, cell.Y + 7f, 24f, 24f));
			}
			shown++;
		}
		return top + ((shown + perRow - 1) / perRow) * 44f + 8f;
	}

	// Nothing to preview: what the field finds and how.
	private static void DrawQuickTips(SpriteBatch sb, BookRect pane)
	{
		float x = pane.X + 20f;
		float y = pane.Y + 20f;
		CenterLabel(sb, L("QuickSearchDesign.TipsTitle"), x, y, pane.Width - 40f, 24f, TextColor, 0.92f, rightAligned: false, leftAligned: true);
		y += 34f;
		foreach (string tip in new string[4]
		{
			L("QuickSearchDesign.TipNames"),
			L("QuickSearchDesign.TipConcepts"),
			L("QuickSearchDesign.TipDescriptions"),
			L("QuickSearchDesign.TipCommands")
		})
		{
			List<string> lines = QuickWrap(tip, pane.Width - 56f, 0.78f);
			Box(sb, x + 2f, y + 8f, 4f, 4f, QuickFaint, border: false, 2f);
			foreach (string line in lines)
			{
				CenterLabel(sb, line, x + 16f, y, pane.Width - 56f, 20f, Muted, 0.78f, rightAligned: false, leftAligned: true);
				y += 20f;
			}
			y += 10f;
		}
	}

	// The actions: the row's own first, across, then two columns, each with its key; at the bottom of the preview.
	private static float QuickActionGridTop(BookRect pane, int count)
	{
		int rows = (count <= 1) ? 1 : (1 + (Math.Min(count, 11) - 1 + 1) / 2);
		return pane.Y + pane.Height - 16f - rows * 34f + 6f;
	}

	private static void DrawQuickActionGrid(SpriteBatch sb, QuickRow row, List<QuickAction> actions, BookRect pane, float top)
	{
		float x = pane.X + 16f;
		float width = pane.Width - 32f;
		float half = (width - 8f) / 2f;
		for (int i = 0; i < actions.Count && i < 11; i++)
		{
			BookRect rect = (i == 0) ? new BookRect(x, top, width, 28f) : new BookRect(x + ((i - 1) % 2) * (half + 8f), top + (1 + (i - 1) / 2) * 34f, half, 28f);
			QuickAction action = actions[i];
			string label = QuickActionLabel(row, action);
			string key = QuickActionKey(row, action);
			bool over = rect.Contains(Mouse.X, Mouse.Y);
			if (i == 0)
			{
				Box(sb, rect, over ? new Color(204, 236, 214) : Accent, border: false, 8f);
				float keyWidth = (key.Length > 0) ? (TextWidth(key, 0.7f) + 8f) : 0f;
				DrawPlainLabel(sb, label, new BookRect(rect.X, rect.Y, rect.Width - keyWidth, rect.Height), Panel, 0.82f);
				if (key.Length > 0)
				{
					DrawPlainLabel(sb, key, new BookRect(rect.X + rect.Width - keyWidth - 12f, rect.Y, keyWidth + 8f, rect.Height), Panel * 0.7f, 0.7f);
				}
			}
			else
			{
				Box(sb, rect, over ? HoverFill : Raised, border: true, 8f, over ? new Color(78, 99, 84) : Border);
				float keyWidth = (key.Length > 0) ? (TextWidth(key, 0.66f) + 6f) : 0f;
				// The key gives way to the label (the tooltip still names it).
				if (keyWidth > 0f && TextWidth(label, 0.68f) > rect.Width - 20f - keyWidth)
				{
					key = "";
					keyWidth = 0f;
				}
				// A long label (Russian, a boss's name) steps down once before it is cut.
				float size = (TextWidth(label, 0.76f) > rect.Width - 20f - keyWidth) ? 0.68f : 0.76f;
				CenterLabel(sb, label, rect.X + 10f, rect.Y, rect.Width - 20f - keyWidth, rect.Height, TextColor, size, rightAligned: false, leftAligned: true);
				if (key.Length > 0)
				{
					CenterLabel(sb, key, rect.X + rect.Width - 8f - keyWidth, rect.Y, keyWidth, rect.Height, QuickFaint, 0.66f, rightAligned: true);
				}
			}
			if (over)
			{
				MouseTip(QuickActionTip(row, action));
			}
			QuickButtons.Add((rect, action, QuickSelected));
		}
	}

	// The footer: the keys for the selected row on the left, what the last action said (or how to reach the commands) on
	// the right.
	private static void DrawQuickFooter(SpriteBatch sb)
	{
		BookRect panel = QuickPanel;
		float y = QuickFooterY;
		Box(sb, panel.X + 1f, y - 8f, panel.Width - 2f, 1f, Border, border: false, 0f);
		float x = panel.X + 20f;
		QuickRow? selected = (QuickSelected >= 0 && QuickSelected < QuickRows.Count) ? QuickRows[QuickSelected] : null;
		List<(string Key, string Action)> keys = new List<(string, string)> { (L("QuickSearchDesign.KeyArrows"), L("QuickSearchDesign.Select")) };
		if (selected.HasValue && selected.Value.Kind != QuickKind.Header)
		{
			QuickRow row = selected.Value;
			keys.Add((L("QuickSearch.KeyEnter"), (row.Kind == QuickKind.Command) ? L("QuickSearchDesign.ActionRun") : QuickActionLabel(row, QuickAction.Open)));
			if (QuickItemOf(row) > 0)
			{
				QuickAction action = QuickPrimaryKeyAction(row);
				keys.Add((L("Help.KeyCtrlEnter"), (action == QuickAction.Craft) ? L("Help.Craft") : QuickActionLabel(row, action)));
				keys.Add((L("Help.KeyAltT"), L("Help.Track")));
				keys.Add((L("Help.KeyAltG"), L("Common.HowToGet")));
			}
		}
		keys.Add((L("Help.KeyEsc"), L("QuickSearch.Close")));
		string status = QuickStatus();
		float limit = panel.X + panel.Width - 20f - ((status.Length > 0) ? Math.Min(360f, TextWidth(status, 0.78f) + 8f) : (TextWidth(L("QuickSearchDesign.CommandsHint"), 0.75f) + 40f));
		foreach ((string key, string action) in keys)
		{
			float keyWidth = QuickKeyWidth(key);
			float actionWidth = TextWidth(action, 0.75f) + 2f;
			if (x + keyWidth + 6f + actionWidth > limit)
			{
				break;
			}
			HelpKeycap(sb, new BookRect(x, y + 2f, keyWidth, 22f), key);
			x += keyWidth + 6f;
			CenterLabel(sb, action, x, y, actionWidth, 26f, Muted, 0.75f, rightAligned: false, leftAligned: true);
			x += actionWidth + 14f;
		}
		float right = panel.X + panel.Width - 20f;
		if (status.Length > 0)
		{
			float width = Math.Min(360f, TextWidth(status, 0.78f) + 8f);
			CenterLabel(sb, status, right - width, y, width, 26f, Accent, 0.78f, rightAligned: true);
			return;
		}
		string hint = L("QuickSearchDesign.CommandsHint");
		float hintWidth = TextWidth(hint, 0.75f) + 2f;
		CenterLabel(sb, hint, right - hintWidth, y, hintWidth, 26f, QuickFaint, 0.75f, rightAligned: true);
		HelpKeycap(sb, new BookRect(right - hintWidth - 34f, y + 2f, 28f, 22f), ">");
	}

	// A keycap's width for its label (HelpKeycap draws it at 0.8 with 6 on each side).
	private static float QuickKeyWidth(string key)
	{
		return TextWidth(key, 0.8f) + 16f;
	}
}
