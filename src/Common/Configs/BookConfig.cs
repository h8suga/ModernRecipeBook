using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ModernRecipeBook.Common.Configs;

public sealed class BookConfig : ModConfig
{
	[DefaultValue(90)]
	[Range(60, 150)]
	[Increment(5)]
	[Slider]
	public int WindowScalePercent = 90;

	[DefaultValue(100)]
	[Range(90, 115)]
	[Increment(5)]
	[Slider]
	public int TextScalePercent = 100;

	[DefaultValue(100)]
	[Range(60, 150)]
	[Increment(5)]
	[Slider]
	public int TrackerScalePercent = 100;

	[DefaultValue(90)]
	[Range(20, 100)]
	[Increment(5)]
	[Slider]
	public int TrackerOpacityPercent = 90;

	[DefaultValue(false)]
	public bool ResourceOnlyTracker;

	[DefaultValue(true)]
	public bool InventoryAltRightClick = true;

	// What Mouse3 on an item in a slot opens (RecipeBookSystem.OpenForInventoryItem).
	[DefaultValue(BookItemClickAction.OpenInBook)]
	[DrawTicks]
	public BookItemClickAction ItemClickAction = BookItemClickAction.OpenInBook;

	[Header("Movement")]
	[DefaultValue(false)]
	public bool DoubleTapDash;

	public override ConfigScope Mode => ConfigScope.ClientSide;
}

// OpenInBook: the item's card in the book; QuickSearch: the quick search with the item's name, the item first.
public enum BookItemClickAction
{
	OpenInBook,
	QuickSearch
}
