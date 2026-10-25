using Terraria.ID;

namespace ModernRecipeBook;

// Marker badges, drawn from vanilla item sprites: distinct silhouettes first, colour second, so each one reads at
// minimap size and never by colour alone. The index is saved with every marker: append, never reorder.
// Picker rows: places and people, then threats and resources.
internal static class BookMarkerBadges
{
	internal readonly record struct Badge(int Item, string Ru, string En);

	internal static readonly Badge[] All = new Badge[12]
	{
		new Badge(ItemID.Bed, "Дом", "Home"),
		new Badge(ItemID.GoldCoin, "Торговец", "Shop"),
		new Badge(ItemID.TeleportationPylonPurity, "Пилон", "Pylon"),
		new Badge(ItemID.LifeCrystal, "Святыня", "Shrine"),
		new Badge(ItemID.FallenStar, "Интересное", "Point of Interest"),
		new Badge(ItemID.Sign, "Заметка", "Note"),
		new Badge(ItemID.Skull, "Босс", "Boss"),
		new Badge(ItemID.Bomb, "Опасно", "Danger"),
		new Badge(ItemID.IronPickaxe, "Руда", "Ore"),
		new Badge(ItemID.Chest, "Добыча", "Loot"),
		new Badge(ItemID.Sunflower, "Ферма", "Farm"),
		new Badge(ItemID.Bass, "Рыбалка", "Fishing")
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
