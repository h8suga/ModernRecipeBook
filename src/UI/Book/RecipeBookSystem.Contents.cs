using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook.UI.Book;

// The item card's Contents: what a Treasure Bag, a crate, a present or a geode holds, from the item's own loot rules
// (ItemDropDatabase.RegisterBossBags, ModItem.ModifyItemLoot). The rows are the mob card's loot rows, read through the
// same ReadLoot, so a drop and the same item in its bag say the same.
public sealed partial class RecipeBookSystem
{
	// What the list was read for: once per item shown and language, and again in the next world (ResetView); its lines
	// again when the "Not in this world" row is opened or folded.
	private static (int Type, string Culture, bool Open) ContentsKey;

	private static List<BookEnemyLoot> ContentsLoot = new List<BookEnemyLoot>();

	private static List<LootLine> ContentsLines = new List<LootLine>();

	private const float ContentsRowStep = 46f;

	// An inventory swap the game registers as loot (Capricorn Hooves and Tail) holds nothing.
	private static bool HasContents(int type)
	{
		return type > 0 && Main.ItemDropsDB.GetRulesForItemID(type).Count > 0 && !BookItemSources.IsLootSwap(type);
	}

	// A step to an item opens a container on what it holds (a Treasure Bag on the mob card, a bag in a bag).
	private static BookCardMode CardModeFor(int type)
	{
		return HasContents(type) ? BookCardMode.Contents : BookCardMode.Recipe;
	}

	// The lines Contents lists (LootLines): this world's rows, then the "Not in this world" row.
	private static List<LootLine> Contents(int type)
	{
		(int, string, bool) key = (type, Language.ActiveCulture.Name, OtherWorldsOpen);
		if (ContentsKey.Type == type && ContentsKey.Culture == key.Item2 && ContentsKey.Open != OtherWorldsOpen)
		{
			ContentsKey = key;
			ContentsLines = LootLines(ContentsLoot, new List<BookEnemyLoot>());
		}
		if (ContentsKey != key)
		{
			ContentsKey = key;
			// A container's rule is not checked against a mob (LootCondition): opening a bag has none
			// (Player.DropFromItem leaves DropAttemptInfo.npc empty).
			int mob = LootNpc;
			LootNpc = 0;
			EnemyReadErrors = 0;
			ContentsLoot = OrderLoot(ReadLoot(Main.ItemDropsDB.GetRulesForItemID(type)));
			ContentsLines = LootLines(ContentsLoot, new List<BookEnemyLoot>());
			LootNpc = mob;
			if (EnemyReadErrors > 0)
			{
				ModContent.GetInstance<ModernRecipeBook>().Logger.Warn($"Contents of item {type}: {EnemyReadErrors} reports could not be read.");
			}
		}
		return ContentsLines;
	}

	// The mob card's loot row (BookGeometry.MobLootRow) under the section label.
	private static BookRect ContentsRow(float top, int slot)
	{
		return new BookRect(812f, top + 32f + slot * ContentsRowStep, 284f, 42f);
	}

	// A lock box's key (BookCodeSources.KeyOf) takes a line above the list, which starts below it.
	private static BookRect ContentsKeyRow(float top)
	{
		return new BookRect(812f, top + 2f, 284f, 28f);
	}

	private static float ContentsListTop(int type, float top)
	{
		return BookCodeSources.KeyOf(type) != null ? top + 32f : top;
	}

	private static void DrawContentsKey(SpriteBatch sb, int type, float top)
	{
		if (BookCodeSources.KeyOf(type) is not (int key, bool usedUp))
		{
			return;
		}
		BookRect rect = ContentsKeyRow(top);
		bool hover = !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
		Item item = ContentSamples.ItemsByType[key];
		Box(sb, rect, hover ? HoverFill : Raised, border: true, 8f);
		Icon(sb, item, rect.X + 5f, rect.Y + 2f, 24f, tooltip: false);
		FixedLabel(sb, L(usedUp ? "ItemsSources.OpensWithUsedUp" : "ItemsSources.OpensWithKept", item.Name), rect.X + 36f, rect.Y + 4f, TextColor, 0.8f, rect.Width - 44f, 20f);
		if (hover)
		{
			HoverItemRow(item, L(usedUp ? "ItemsSources.KeyUsedUpBody" : "ItemsSources.KeyKeptBody", item.Name, ContentSamples.ItemsByType[type].Name) + "\n" + L("Bestiary.ClickTheItemSCard"));
		}
	}

	private static bool HandleContentsKeyClick(int type, float top)
	{
		if (BookCodeSources.KeyOf(type) is (int key, _) && ContentsKeyRow(top).Contains(Mouse.X, Mouse.Y))
		{
			StepCard(key, CardModeFor(key));
			return true;
		}
		return false;
	}

	private static void DrawContents(SpriteBatch sb, Item chosen, float top)
	{
		List<LootLine> loot = Contents(chosen.type);
		SectionLabel(sb, L("Contents.Items", ContentsLoot.Count), 812f, top + 3f, 284f);
		if (loot.Count == 0)
		{
			EmptyState(sb, new BookRect(812f, top + 32f, 284f, BookGeometry.DetailListBottom - top - 32f), L("Contents.NothingListed"), L("Contents.TheGameDoesNotSay"));
			return;
		}
		int size = CardListPageSize(top, loot.Count, ContentsRowStep);
		int pages = (loot.Count + size - 1) / size;
		DetailsPage = Math.Clamp(DetailsPage, 0, pages - 1);
		for (int slot = 0; slot < size && DetailsPage * size + slot < loot.Count; slot++)
		{
			DrawLootLine(sb, ContentsRow(top, slot), loot[DetailsPage * size + slot], 0, ContentsLoot.Count((BookEnemyLoot x) => x.OtherWorld));
		}
		if (pages > 1)
		{
			ChevronPager(sb, BookGeometry.DetailListPrevious, BookGeometry.DetailListNext, BookGeometry.DetailListPager, DetailsPage, pages);
			if (!MenuOpen && BookGeometry.DetailListPrevious.Contains(Mouse.X, Mouse.Y) && DetailsPage > 0)
			{
				MouseTip(L("Bestiary.PreviousWheel"));
			}
			if (!MenuOpen && BookGeometry.DetailListNext.Contains(Mouse.X, Mouse.Y) && DetailsPage < pages - 1)
			{
				MouseTip(L("Bestiary.NextWheel"));
			}
		}
	}

	private static bool HandleContentsClick(int type, float top)
	{
		List<LootLine> loot = Contents(type);
		int size = CardListPageSize(top, loot.Count, ContentsRowStep);
		int pages = Math.Max(1, (loot.Count + size - 1) / size);
		if (pages > 1 && BookGeometry.DetailListPrevious.Contains(Mouse.X, Mouse.Y))
		{
			DetailsPage = Math.Max(0, DetailsPage - 1);
			return true;
		}
		if (pages > 1 && BookGeometry.DetailListNext.Contains(Mouse.X, Mouse.Y))
		{
			DetailsPage = Math.Min(pages - 1, DetailsPage + 1);
			return true;
		}
		for (int slot = 0; slot < size && DetailsPage * size + slot < loot.Count; slot++)
		{
			if (ContentsRow(top, slot).Contains(Mouse.X, Mouse.Y))
			{
				LootLine line = loot[DetailsPage * size + slot];
				if (!ToggleLootFold(line) && line.Loot != null && LootOpens(line.Loot.ItemType))
				{
					StepCard(line.Loot.ItemType, CardModeFor(line.Loot.ItemType));
				}
				return true;
			}
		}
		return false;
	}
}
