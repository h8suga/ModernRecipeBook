using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace ModernRecipeBook;

public sealed class BookTooltipShortcut : GlobalItem
{
	// Yoyos with the vanilla yoyo logo line, which adds 24 px below the text (Main.MouseText_DrawItemTooltip).
	private static readonly HashSet<int> YoyoLogoItems = new HashSet<int> { 3262, 3282, 3283, 3284, 3285, 3286, 3316, 3315, 3317, 3291, 3389 };

	// Tooltips only, no item data or net sync: a dedicated server never loads it.
	public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

	public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
	{
		// The search reading every item's tooltip is not a hover.
		if (BookItemTooltip.Building)
		{
			return;
		}
		BookInventoryShortcut.TryOpenTooltip(item);
		// A Storage tab cell: where the item is ("Chests ×3: 120 · Magic Storage: 40").
		Item cell = RecipeBookSystem.StorageHover;
		if (cell != null && RecipeBookSystem.Open && item.type == cell.type && item.prefix == cell.prefix)
		{
			string where = BookSources.Breakdown(item.type);
			if (where.Length > 0)
			{
				tooltips.Add(new TooltipLine(Mod, "NearbySources", where)
				{
					OverrideColor = new Color(170, 200, 180)
				});
			}
			tooltips.Add(new TooltipLine(Mod, "NearbyTake", BookSources.TakeHint)
			{
				OverrideColor = new Color(150, 165, 155)
			});
			tooltips.Add(new TooltipLine(Mod, "NearbyDeposit", BookSources.DepositHint)
			{
				OverrideColor = new Color(150, 165, 155)
			});
		}
	}

	// Item tooltips over the book get the book's panel instead of vanilla's blue box: it is drawn right before this
	// hook with the same bounds, so the book's panel covers it exactly.
	public override bool PreDrawTooltip(Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y)
	{
		if (!Main.SettingsEnabled_OpaqueBoxBehindTooltips || !RecipeBookSystem.Open || !RecipeBookSystem.MouseOver)
		{
			return true;
		}
		Vector2 size = Vector2.Zero;
		foreach (TooltipLine line in lines)
		{
			Vector2 lineSize = ChatManager.GetStringSize(FontAssets.MouseText.Value, line.Text, Vector2.One);
			size.X = Math.Max(size.X, lineSize.X);
			size.Y += lineSize.Y;
		}
		if (YoyoLogoItems.Contains(item.type))
		{
			size.Y += 24f;
		}
		RecipeBookSystem.DrawTooltipPanel(new Rectangle(x - 14, y - 9, (int)size.X + 28, (int)size.Y + 13));
		return true;
	}
}