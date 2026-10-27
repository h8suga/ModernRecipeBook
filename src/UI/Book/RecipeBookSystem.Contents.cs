using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook.UI.Book;

// The item card's Contents: what a Treasure Bag, a crate, a present or a geode holds, from the item's own loot rules
// (ItemDropDatabase.RegisterBossBags, ModItem.ModifyItemLoot). The rows are the mob card's loot rows, read through the
// same ReadLoot, so a drop and the same item in its bag say the same.
public sealed partial class RecipeBookSystem
{
	// What the list was read for: once per item shown and language, and again in the next world (ResetView).
	private static (int Type, string Culture) ContentsKey;

	private static List<BookEnemyLoot> ContentsLoot = new List<BookEnemyLoot>();

	private const float ContentsRowStep = 46f;

	private static bool HasContents(int type)
	{
		return type > 0 && Main.ItemDropsDB.GetRulesForItemID(type).Count > 0;
	}

	// A step to an item opens a container on what it holds (a Treasure Bag on the mob card, a bag in a bag).
	private static BookCardMode CardModeFor(int type)
	{
		return HasContents(type) ? BookCardMode.Contents : BookCardMode.Recipe;
	}

	private static List<BookEnemyLoot> Contents(int type)
	{
		(int, string) key = (type, Language.ActiveCulture.Name);
		if (ContentsKey != key)
		{
			ContentsKey = key;
			// A container's rule is not checked against a mob (HiddenConditionLabel): opening a bag has none
			// (Player.DropFromItem leaves DropAttemptInfo.npc empty).
			int mob = LootNpc;
			LootNpc = 0;
			EnemyReadErrors = 0;
			ContentsLoot = OrderLoot(ReadLoot(Main.ItemDropsDB.GetRulesForItemID(type)));
			LootNpc = mob;
			if (EnemyReadErrors > 0)
			{
				ModContent.GetInstance<ModernRecipeBook>().Logger.Warn($"Contents of item {type}: {EnemyReadErrors} reports could not be read.");
			}
		}
		return ContentsLoot;
	}

	// The mob card's loot row (BookGeometry.MobLootRow) under the section label.
	private static BookRect ContentsRow(float top, int slot)
	{
		return new BookRect(812f, top + 32f + slot * ContentsRowStep, 284f, 42f);
	}

	private static void DrawContents(SpriteBatch sb, Item chosen, float top)
	{
		List<BookEnemyLoot> loot = Contents(chosen.type);
		SectionLabel(sb, L("Contents.Items", loot.Count), 812f, top + 3f, 284f);
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
			DrawLootRow(sb, ContentsRow(top, slot), loot[DetailsPage * size + slot], dim: false);
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
		List<BookEnemyLoot> loot = Contents(type);
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
				int item = loot[DetailsPage * size + slot].ItemType;
				StepCard(item, CardModeFor(item));
				return true;
			}
		}
		return false;
	}
}
