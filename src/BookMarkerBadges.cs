using Terraria.ID;

namespace ModernRecipeBook;

// Marker badges, drawn from vanilla item sprites: distinct silhouettes first, colour second, so each one reads at
// minimap size and never by colour alone. The index is saved with every marker: append, never reorder.
// Picker rows: places and people, then threats and resources.
internal static class BookMarkerBadges
{
	// Name: the localization key of the badge's name.
	internal readonly record struct Badge(int Item, string Name);

	internal static readonly Badge[] All = new Badge[12]
	{
		new Badge(ItemID.Bed, "Badges.Home"),
		new Badge(ItemID.GoldCoin, "Badges.Shop"),
		new Badge(ItemID.TeleportationPylonPurity, "Badges.Pylon"),
		new Badge(ItemID.LifeCrystal, "Badges.Shrine"),
		new Badge(ItemID.FallenStar, "Badges.PointOfInterest"),
		new Badge(ItemID.Sign, "Badges.Note"),
		new Badge(ItemID.Skull, "Badges.Boss"),
		new Badge(ItemID.Bomb, "Badges.Danger"),
		new Badge(ItemID.IronPickaxe, "Badges.Ore"),
		new Badge(ItemID.Chest, "Badges.Loot"),
		new Badge(ItemID.Sunflower, "Badges.Farm"),
		new Badge(ItemID.Bass, "Badges.Fishing")
	};

	internal const int Count = 12;

	// A new marker starts as a point of interest.
	internal const byte Default = 4;

	internal const int Columns = 6;

	internal static Badge Get(byte index)
	{
		return All[index < Count ? index : Default];
	}
}
