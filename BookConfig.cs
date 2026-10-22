using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ModernRecipeBook;

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

	public override ConfigScope Mode => ConfigScope.ClientSide;
}
