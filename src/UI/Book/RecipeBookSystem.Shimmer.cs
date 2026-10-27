using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;

namespace ModernRecipeBook.UI.Book;

// The item card's Shimmer: what the item turns into in Shimmer by vanilla's rules (BookShimmer.For, Item.GetShimmered,
// TML:Terraria/Item.cs:49561-49850), and doing it from the book through a Magic Storage network with a Decrafting
// Interface, as the Craft button crafts from nearby storage: the batch bar and the Shimmer button take the Craft button's
// place, the result goes to the status line. How to Get's "shimmer from X" rows get a Shimmer button of their own for X.
public sealed partial class RecipeBookSystem
{
	private const float ShimmerRowStep = 36f;

	// The mode's action is there only while a network with a Decrafting Interface is in reach; without one the card keeps
	// its Craft button and the mode is what the item does in Shimmer.
	private static bool ShimmerActionShown => ShownCardMode == BookCardMode.Shimmer && BookShimmer.InReach;

	// How to Get printed a row's shimmer result in its own footer (ReadAcquisitionSources forgets it).
	private static bool AcquisitionShimmered;

	private static string ShimmerLead(BookShimmer.Plan plan)
	{
		return plan.What switch
		{
			BookShimmer.Kind.Transform => L("ShimmerCard.TurnsInto"),
			BookShimmer.Kind.Decraft when plan.Unit > 1 => L("ShimmerCard.TurnBackIntoEvery", plan.Unit),
			BookShimmer.Kind.Decraft => L("ShimmerCard.TurnsBackInto"),
			BookShimmer.Kind.CoinLuck => L("ShimmerCard.EachCoinGives"),
			BookShimmer.Kind.Critter => L("ShimmerCard.SetsFree"),
			_ => L("ShimmerCard.BringsToTown")
		};
	}

	// The rows: the items one unit gives, or the coin luck, the critter or the slime that comes out of it.
	private static int ShimmerRowCount(BookShimmer.Plan plan)
	{
		return (plan.What is BookShimmer.Kind.Transform or BookShimmer.Kind.Decraft) ? plan.Results.Count : 1;
	}

	private static int ShimmerPageSize(float top, int count)
	{
		return CardListPageSize(top, count, ShimmerRowStep, BookGeometry.ShimmerListBottom, BookGeometry.ShimmerPagedBottom);
	}

	private static int ShimmerPages(int type, float top)
	{
		int count = (BookShimmer.For(type) is { } plan) ? ShimmerRowCount(plan) : 0;
		return Math.Max(1, (count + ShimmerPageSize(top, count) - 1) / ShimmerPageSize(top, count));
	}

	// The ingredient row's size and look (DrawDetailRow).
	private static BookRect ShimmerRow(float top, int slot)
	{
		return new BookRect(812f, top + 32f + slot * ShimmerRowStep, 284f, 32f);
	}

	private static void DrawShimmer(SpriteBatch sb, int type, float top)
	{
		BookShimmer.Plan plan = BookShimmer.For(type);
		if (plan == null)
		{
			return;
		}
		CenterLabel(sb, ShimmerLead(plan), 812f, top, 284f, 28f, Muted, 0.85f, rightAligned: false, leftAligned: true);
		int count = ShimmerRowCount(plan);
		int size = ShimmerPageSize(top, count);
		int pages = Math.Max(1, (count + size - 1) / size);
		DetailsPage = Math.Clamp(DetailsPage, 0, pages - 1);
		for (int slot = 0; slot < size && DetailsPage * size + slot < count; slot++)
		{
			BookRect row = ShimmerRow(top, slot);
			if (plan.What is BookShimmer.Kind.Critter or BookShimmer.Kind.TownSlime)
			{
				DrawShimmerNpcRow(sb, plan.Npc, row);
			}
			else if (plan.What == BookShimmer.Kind.CoinLuck)
			{
				DrawShimmerCoinLuck(sb, plan.CoinLuck, row);
			}
			else
			{
				DrawShimmerItemRow(sb, plan.Results[DetailsPage * size + slot], row);
			}
		}
		if (pages > 1)
		{
			ChevronPager(sb, BookGeometry.ShimmerPrevious, BookGeometry.ShimmerNext, BookGeometry.ShimmerPager, DetailsPage, pages);
			if (!MenuOpen && BookGeometry.ShimmerPrevious.Contains(Mouse.X, Mouse.Y) && DetailsPage > 0)
			{
				MouseTip(L("Bestiary.PreviousWheel"));
			}
			if (!MenuOpen && BookGeometry.ShimmerNext.Contains(Mouse.X, Mouse.Y) && DetailsPage < pages - 1)
			{
				MouseTip(L("Bestiary.NextWheel"));
			}
		}
		DrawShimmerAction(sb, type, plan);
	}

	// What one unit gives: the item's own tooltip under the mouse, the row a link to its card.
	private static void DrawShimmerItemRow(SpriteBatch sb, (int Type, int Stack) result, BookRect row)
	{
		if (!ContentSamples.ItemsByType.TryGetValue(result.Type, out Item item))
		{
			return;
		}
		BookLink link = ItemLink(result.Type);
		bool hover = !MenuOpen && row.Contains(Mouse.X, Mouse.Y);
		Box(sb, row, hover ? HoverFill : Raised, border: false, 8f);
		Icon(sb, item, row.X + 6f, row.Y + (row.Height - 24f) / 2f, 24f, tooltip: false);
		string amount = "×" + Count(result.Stack);
		float width = TextWidth(amount, 0.85f) + 4f;
		float right = row.X + row.Width - 10f;
		CenterLabel(sb, amount, right - width, row.Y + 4f, width, row.Height - 8f, TextColor, 0.85f, rightAligned: true);
		CenterLabel(sb, item.Name, row.X + 38f, row.Y + 4f, right - width - 10f - row.X - 38f, row.Height - 8f, TextColor, 0.85f, rightAligned: false, leftAligned: true);
		RowLink(link, row);
		if (hover)
		{
			HoverItemRow(item, L("Common.ClickOpenThisItem"));
		}
	}

	// A coin's luck (ItemID.Sets.CoinLuckValue, Player.AddCoinLuck), a row with nothing to open, and what it does: the
	// player's coin luck adds to luck and wears off (Player.RecalculateLuck).
	private static void DrawShimmerCoinLuck(SpriteBatch sb, int luck, BookRect row)
	{
		Box(sb, row, Raised, border: false, 8f);
		string amount = "+" + Count(luck);
		float width = TextWidth(amount, 0.85f) + 4f;
		float right = row.X + row.Width - 10f;
		CenterLabel(sb, amount, right - width, row.Y + 4f, width, row.Height - 8f, TextColor, 0.85f, rightAligned: true);
		CenterLabel(sb, L("ShimmerCard.CoinLuck"), row.X + 10f, row.Y + 4f, right - width - 20f - row.X, row.Height - 8f, TextColor, 0.85f, rightAligned: false, leftAligned: true);
		CenterLabel(sb, L("ShimmerCard.CoinLuckBody"), row.X, row.Y + row.Height + 4f, row.Width, 24f, Muted, 0.8f, rightAligned: false, leftAligned: true);
	}

	// The critter let go or the Town Slime that arrives: the row a link to its Bestiary card.
	private static void DrawShimmerNpcRow(SpriteBatch sb, int npc, BookRect row)
	{
		BookLink link = MobLink(npc);
		bool hover = link != null && !MenuOpen && row.Contains(Mouse.X, Mouse.Y);
		Box(sb, row, hover ? HoverFill : Raised, border: false, 8f);
		DrawNpcIcon(sb, npc, new BookRect(row.X + 6f, row.Y + (row.Height - 24f) / 2f, 24f, 24f));
		CenterLabel(sb, NpcName(npc), row.X + 38f, row.Y + 4f, row.Width - 48f, row.Height - 8f, TextColor, 0.85f, rightAligned: false, leftAligned: true);
		RowLink(link, row);
		if (hover)
		{
			MouseTip(link.Tip);
		}
	}

	// Above the Shimmer button: the batch bar when it can shimmer now, else why not (the boss it waits for, the Decrafting
	// Interface it needs, each a link). Without Magic Storage there is nothing to say.
	private static void DrawShimmerAction(SpriteBatch sb, int type, BookShimmer.Plan plan)
	{
		string reason = BookShimmer.Reason(type);
		if (plan.Ready && BookShimmer.InReach && reason.Length == 0)
		{
			BookRect[] rects = ShimmerBatchRects();
			Segmented(sb, rects, BatchLabels(), BatchChoice, 0.85f);
			int hovered = MenuOpen ? (-1) : SegmentAt(rects);
			if (hovered >= 0)
			{
				MouseTip((hovered == 3) ? L("ShimmerCard.AsManyAsThereAre") : L("ShimmerCard.ShimmerThisManyTimes"));
			}
			return;
		}
		if (plan.Ready && BookMagicStorage.DecraftingItem == 0)
		{
			return;
		}
		BookRect line = BookGeometry.ShimmerBatchBar;
		(List<LinkSpan> spans, string tip) = ShimmerReasonLine(plan, reason);
		if (!DrawLinkLine(sb, spans, line, 0.85f, Caution) && tip.Length > 0 && !MenuOpen && line.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(tip);
		}
	}

	// Why the item cannot be shimmered from the book (BookShimmer.Reason) in the book's words, as a line short enough for
	// the card, and what its tip adds (how shimmering from the book works). The boss it waits for, named by its step as
	// the book names bosses (a Calamity boss too), and the Decrafting Interface (Magic Storage's DecraftingAccess, by its
	// own name: "Storage Aether Interface" in 0.7) are links.
	private static (List<LinkSpan> Spans, string Tip) ShimmerReasonLine(BookShimmer.Plan plan, string reason)
	{
		if (!plan.Ready)
		{
			if (plan.GateNpc <= 0)
			{
				return (Plain(L("Shimmer.NotNow")), "");
			}
			int step = BookSteps.Npc(plan.GateNpc);
			string boss = (BookSteps.Name(step) is { Length: > 0 } name) ? name : Lang.GetNPCNameValue(plan.GateNpc);
			BookLink link = MobLink(StepMob(step)) ?? MobLink(plan.GateNpc);
			return (Linked(L("ShimmerCard.AfterBoss", boss), LinkNames.None, new[] { (boss, link) }), "");
		}
		int decrafting = BookMagicStorage.DecraftingItem;
		string how = L("ShimmerCard.HowItWorks", ItemName(decrafting));
		if (!BookShimmer.InReach || reason == L("Shimmer.NoInterface") || reason == L("Shimmer.NeedsMagicStorage"))
		{
			return (Linked(L("ShimmerCard.NeedsInterface", ItemName(decrafting)), LinkNames.None, new[] { (ItemName(decrafting), ItemLink(decrafting)) }), how);
		}
		if (reason == L("Shimmer.NoneInNetwork"))
		{
			return (Plain(L("ShimmerCard.NoneStored")), how);
		}
		return (Plain(reason), "");
	}

	// The same words as one text, for a tip and the status line; "" when it can be shimmered now.
	private static string ShimmerReasonText(int type)
	{
		string reason = BookShimmer.Reason(type);
		return (reason.Length == 0 || BookShimmer.For(type) is not { } plan) ? reason : string.Concat(ShimmerReasonLine(plan, reason).Spans.ConvertAll((LinkSpan s) => s.Text));
	}

	private static BookRect[] ShimmerBatchRects()
	{
		return SegmentRects(BookGeometry.ShimmerBatchBar, BatchLabels(), 0.85f, natural: false);
	}

	// The batch bar in units (×5: five times what one unit gives); Max: as many as the network holds.
	private static int ShimmerUnits(int type, BookShimmer.Plan plan)
	{
		return BatchChoice switch
		{
			0 => 1,
			1 => 5,
			2 => 10,
			_ => Math.Max(1, BookShimmer.Max(type) / plan.Unit)
		};
	}

	// Where the Craft button is (ShimmerActionShown): disabled with the reason when it cannot shimmer now.
	private static void DrawShimmerButton(SpriteBatch sb, int type)
	{
		BookShimmer.Plan plan = BookShimmer.For(type);
		string reason = BookShimmer.Reason(type);
		int units = ShimmerUnits(type, plan);
		ActionButton(sb, CraftRect, L("ShimmerCard.ShimmerTimes", units), primary: true, reason.Length == 0);
		if (!MenuOpen && CraftRect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip((reason.Length > 0) ? ShimmerReasonText(type) : ShimmerTakes(type, units * plan.Unit));
		}
	}

	// "Takes 10 from Main Storage, 120 there" (what TryShimmer takes: whole units, at most Max) and what Shift+click takes.
	private static string ShimmerTakes(int type, int items)
	{
		(BookMagicStorage.Network network, long count) = BookShimmer.Best(type);
		int max = BookShimmer.Max(type);
		string name = (network?.Name is { Length: > 0 } named) ? named : "Magic Storage";
		return L("ShimmerCard.TakesFrom", Count(Math.Min(items, max)), name, (count == long.MaxValue) ? "∞" : Count(count)) + "\n" + L("ShimmerCard.ShiftClickAll", Count(max));
	}

	private static bool HandleShimmerClick(int type, float top)
	{
		BookShimmer.Plan plan = BookShimmer.For(type);
		if (plan == null)
		{
			return false;
		}
		int pages = ShimmerPages(type, top);
		if (pages > 1 && BookGeometry.ShimmerPrevious.Contains(Mouse.X, Mouse.Y))
		{
			DetailsPage = Math.Max(0, DetailsPage - 1);
			return true;
		}
		if (pages > 1 && BookGeometry.ShimmerNext.Contains(Mouse.X, Mouse.Y))
		{
			DetailsPage = Math.Min(pages - 1, DetailsPage + 1);
			return true;
		}
		int batch = (plan.Ready && BookShimmer.InReach && BookShimmer.Reason(type).Length == 0) ? SegmentAt(ShimmerBatchRects()) : (-1);
		if (batch >= 0)
		{
			BatchChoice = batch;
			BookCrafting.LastMessage = "";
			return true;
		}
		if (ShimmerActionShown && CraftRect.Contains(Mouse.X, Mouse.Y))
		{
			// Shift+click: as many as there are, as a Shift-click moves a whole stack. A disabled button says why.
			KeyboardState keys = Keyboard.GetState();
			ShimmerFromBook(type, (keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift)) ? BookShimmer.Max(type) : ShimmerUnits(type, plan) * plan.Unit);
			return true;
		}
		return false;
	}

	// The status line names the item and what came out of it (MessageSpans), or why nothing was done in the words of the
	// button's tip (TryShimmer would put the engine's).
	private static void ShimmerFromBook(int type, int items)
	{
		if (ShimmerReasonText(type) is { Length: > 0 } reason)
		{
			BookCrafting.LastMessage = reason;
			return;
		}
		LastCraftType = type;
		LastShimmer = BookShimmer.For(type);
		BookShimmer.TryShimmer(type, items);
	}

	// The item a How to Get row shimmers, when a Decrafting Interface is in reach: X of "shimmer from X" (BookItemSources,
	// BookRewardSources, the decraft of one item in BookCodeSources.DecraftEntry). A decraft row of several items ("Any of
	// 12 items") gets none: its icon and click stand for its first item only, which need not be the one stored.
	private static int ShimmerRowItem(BookAcquisitionGroup group)
	{
		int item = RowItem(group);
		return (item > 0 && group.Source.Kind == BookAcquisitionKind.Shimmer && group.Source.Name == ItemName(item) && BookShimmer.InReach && BookShimmer.For(item) != null) ? item : 0;
	}

	private static BookRect AcquisitionShimmerRect(int row)
	{
		return BookGeometry.AcquisitionShimmer(row, TextWidth(L("ShimmerCard.Shimmer"), 1f) + 24f);
	}

	// Click: one unit of X; Shift+click: all of it. Disabled with the reason when X cannot be shimmered now.
	private static void DrawShimmerRowButton(SpriteBatch sb, int type, BookRect rect)
	{
		string reason = BookShimmer.Reason(type);
		CardButton(sb, rect, L("ShimmerCard.Shimmer"), reason.Length == 0);
		if (!MenuOpen && rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip((reason.Length > 0) ? ShimmerReasonText(type) : ShimmerTakes(type, BookShimmer.For(type).Unit));
		}
	}
}
