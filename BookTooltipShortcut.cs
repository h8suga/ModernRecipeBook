using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace ModernRecipeBook;

public sealed class BookTooltipShortcut : GlobalItem
{
	public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
	{
		BookInventoryShortcut.TryOpenTooltip(item);
	}
}
