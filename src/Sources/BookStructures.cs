using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.Sources;

// What world generation puts in the Dungeon, the Jungle Temple and the Underworld and nothing else gives: furniture,
// paintings, banners, lights and traps, which drop their item when broken, and the loot of their locked chests. Read
// from WorldGen: MakeDungeon and its _GroundFurniture, _Lights, _Banners, _Pictures and _Traps parts, makeTemple and
// templePart2 (Calamity replaces both with CustomTemple.NewJungleTemple and NewJungleTemplePart2, which place the same
// pieces), AddHellHouses and the Hellforge pass. A placed style drops the item TileLoader.GetItemDropFromTypeAndStyle
// names, as the Check*Wall and furniture drop code does, except that every 4x3 painting drops the Catacomb
// (Check4x3Wall).
//
// When: the Dungeon after Skeletron (until then NPC.SpawnNPC sends only the Dungeon Guardian at whoever is inside, and
// its walls do not break, WorldGen.KillWall_CheckFailure); the Temple after Plantera, whose Temple Key opens its locked
// door (Lihzahrd bricks need pickaxe power 210); the Underworld from the start. A key or a pickaxe power the piece also
// needs is a route input.
internal static class BookStructures
{
	// Row (the How to Get row), tile, the styles world generation places, which of them a world has ("color": the
	// furniture of one dungeon color, as MakeDungeon builds the Dungeon in blue, green or pink bricks and walls and the
	// furniture, chandeliers and colored doors follow its wall; "three:N": three of the N kinds per world; "random":
	// paintings picked at random), and the pickaxe power the tile needs (Player.GetPickaxeDamage).
	private static readonly (string Row, int Tile, int[] Styles, string Note, int Pick)[] Placed =
	{
		// MakeDungeon_GroundFurniture, per wall 7/8/9 (blue, green, pink); the Gothic chair, table, work bench and
		// bookcase stand on the tiled and slab walls 94-105 that every dungeon has.
		("DungeonFurniture", TileID.Chairs, new[] { 13, 14, 15 }, "color", 0),
		("DungeonFurniture", TileID.Tables, new[] { 10, 11, 12 }, "color", 0),
		("DungeonFurniture", TileID.WorkBenches, new[] { 11, 12, 13 }, "color", 0),
		("DungeonFurniture", TileID.Candles, new[] { 1, 2, 3 }, "color", 0),
		("DungeonFurniture", TileID.Statues, new[] { 46, 47, 48 }, "color", 0),
		("DungeonFurniture", TileID.Bookcases, new[] { 1, 2, 3 }, "color", 0),
		("DungeonFurniture", TileID.Beds, new[] { 5, 6, 7 }, "color", 0),
		("DungeonFurniture", TileID.Pianos, new[] { 11, 12, 13 }, "color", 0),
		("DungeonFurniture", TileID.Dressers, new[] { 5, 6, 7 }, "color", 0),
		("DungeonFurniture", TileID.Benches, new[] { 6, 7, 8 }, "color", 0),
		("DungeonFurniture", TileID.Bathtubs, new[] { 21, 22, 23 }, "color", 0),
		("DungeonFurniture", TileID.Candelabras, new[] { 22, 23, 24 }, "color", 0),
		("DungeonFurniture", TileID.Lamps, new[] { 24, 25, 26 }, "color", 0),
		("DungeonFurniture", TileID.GrandfatherClocks, new[] { 30, 31, 32 }, "color", 0),
		("DungeonFurniture", TileID.Chairs, new[] { 17 }, "", 0),
		("DungeonFurniture", TileID.Tables, new[] { 14 }, "", 0),
		("DungeonFurniture", TileID.WorkBenches, new[] { 15 }, "", 0),
		("DungeonFurniture", TileID.Bookcases, new[] { 5 }, "", 0),
		// MakeDungeon's room doors: style 13 (also DungeonEnt's at the entrance), one in three in the dungeon's color.
		("DungeonFurniture", TileID.ClosedDoor, new[] { 13 }, "", 0),
		("DungeonFurniture", TileID.ClosedDoor, new[] { 16, 17, 18 }, "color", 0),
		// MakeDungeon_Lights: chandeliers by the dungeon's wall, lanterns of three distinct styles of seven.
		("DungeonLights", TileID.Chandeliers, new[] { 27, 28, 29 }, "color", 0),
		("DungeonLights", TileID.HangingLanterns, new[] { 0, 1, 2, 3, 4, 5, 6 }, "three:7", 0),
		// MakeDungeon_Banners: two styles per wall of the three, so every dungeon has all six.
		("DungeonBanners", TileID.Banners, new[] { 10, 11, 12, 13, 14, 15 }, "", 0),
		// MakeDungeon_Pictures: RandPictureTile on the dungeon's own wall, RandBonePicture on the other two.
		("DungeonPaintings", TileID.Painting3X3, new[] { 12, 13, 14, 15, 18, 19, 23, 16, 17 }, "random", 0),
		("DungeonPaintings", TileID.Painting4X3, new[] { 0 }, "random", 0),
		("DungeonPaintings", TileID.Painting6X4, new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15, 16, 30 }, "random", 0),
		// Spikes along the halls and in DungeonPitTrap; MakeDungeon_Traps' dart traps (placeTrap type 0).
		("DungeonTraps", TileID.Spikes, new[] { 0 }, "", 0),
		("DungeonTraps", TileID.Traps, new[] { 0 }, "", 0),
		// templePart2: work benches, the Lihzahrd statues (43-45) and one painting style 88; makeTemple's wooden spikes;
		// mayanTrap's pressure plates (style 6) and its four traps (styles 1-4, which need 210 like the altar).
		("Temple", TileID.WorkBenches, new[] { 10 }, "", 0),
		("Temple", TileID.Statues, new[] { 43, 44, 45 }, "", 0),
		("Temple", TileID.Painting3X3, new[] { 88 }, "", 0),
		("Temple", TileID.WoodenSpikes, new[] { 0 }, "", 0),
		("Temple", TileID.PressurePlates, new[] { 6 }, "", 0),
		("Temple", TileID.Traps, new[] { 1, 2, 3, 4 }, "", 210),
		("Temple", TileID.LihzahrdAltar, new[] { 0 }, "", 210),
		// AddHellHouses: the obsidian furniture, HellFort's doors (style 19), chandeliers and lanterns (32), banners of three
		// distinct styles of 16-21, and RandHellPicture's paintings.
		("Underworld", TileID.Chairs, new[] { 16 }, "", 0),
		("Underworld", TileID.Tables, new[] { 13 }, "", 0),
		("Underworld", TileID.WorkBenches, new[] { 14 }, "", 0),
		("Underworld", TileID.Statues, new[] { 49 }, "", 0),
		("Underworld", TileID.Bookcases, new[] { 4 }, "", 0),
		("Underworld", TileID.Beds, new[] { 8 }, "", 0),
		("Underworld", TileID.Pianos, new[] { 15 }, "", 0),
		("Underworld", TileID.Dressers, new[] { 9 }, "", 0),
		("Underworld", TileID.Benches, new[] { 10 }, "", 0),
		("Underworld", TileID.GrandfatherClocks, new[] { 17 }, "", 0),
		("Underworld", TileID.Candelabras, new[] { 25 }, "", 0),
		("Underworld", TileID.Candles, new[] { 25 }, "", 0),
		("Underworld", TileID.Lamps, new[] { 23 }, "", 0),
		("Underworld", TileID.Bathtubs, new[] { 25 }, "", 0),
		("Underworld", TileID.ClosedDoor, new[] { 19 }, "", 0),
		("Underworld", TileID.Chandeliers, new[] { 32 }, "", 0),
		("Underworld", TileID.HangingLanterns, new[] { 32 }, "", 0),
		("Underworld", TileID.Banners, new[] { 16, 17, 18, 19, 20, 21 }, "three:6", 0),
		("Underworld", TileID.Painting3X3, new[] { 27, 29, 30, 31, 32 }, "random", 0),
		("Underworld", TileID.Painting6X4, new[] { 14 }, "random", 0),
		("Underworld", TileID.Painting2X3, new[] { 1, 2, 4 }, "random", 0),
		("Underworld", TileID.Painting3X2, new[] { 0, 16, 17 }, "random", 0),
		// The Hellforge pass sets them on the floors of the hell houses (walls 13 and 14); below the Underworld's top
		// one wants pickaxe power 65.
		("Underworld", TileID.Hellforge, new[] { 0 }, "", 65)
	};

	// Locked chests: row, chest tile and style, the item inside (0: the chest itself, once opened, emptied and broken).
	// Chest.chestItemSpawn names the chest's item, Chest.chestTypeToIcon the key a locked style shows, and Chest.Unlock
	// opens the Biome Chests only once Plantera is down. MakeDungeon puts one of each Biome Chest in the Dungeon, the
	// Corruption's in a corrupt world and the Crimson's in a crimson one (both in a drunk world), and its room chests
	// are Locked Gold Chests; AddBuriedChest makes the Underworld's Locked Shadow Chests.
	private static readonly (string Row, int Tile, int Style, int Item, string Note)[] Chests =
	{
		("DungeonBiomeChest", TileID.Containers, 23, ItemID.PiranhaGun, ""),
		("DungeonBiomeChest", TileID.Containers, 24, ItemID.ScourgeoftheCorruptor, "corruption"),
		("DungeonBiomeChest", TileID.Containers, 25, ItemID.VampireKnives, "crimson"),
		("DungeonBiomeChest", TileID.Containers, 26, ItemID.RainbowGun, ""),
		("DungeonBiomeChest", TileID.Containers, 27, ItemID.StaffoftheFrostHydra, ""),
		("DungeonBiomeChest", TileID.Containers2, 13, ItemID.StormTigerStaff, ""),
		("DungeonGoldChest", TileID.Containers, 2, 0, ""),
		("UnderworldChest", TileID.Containers, 4, 0, "")
	};

	internal static IEnumerable<BookItemSource> Sources()
	{
		foreach (var (row, tile, styles, note, pick) in Placed)
		{
			foreach (int style in styles)
			{
				int item = TileLoader.GetItemDropFromTypeAndStyle(tile, style);
				if (item > 0)
				{
					yield return new BookItemSource(BookAcquisitionKind.Structure, item) { From = Brick(row), Where = new[] { row }, Note = note, NoteValue = pick };
				}
			}
		}
		foreach (var (row, tile, style, contains, note) in Chests)
		{
			bool second = tile == TileID.Containers2;
			int chest = second ? Chest.chestItemSpawn2[style] : Chest.chestItemSpawn[style];
			int key = second ? Chest.chestTypeToIcon2[style] : Chest.chestTypeToIcon[style];
			yield return new BookItemSource(BookAcquisitionKind.Structure, contains > 0 ? contains : chest) { From = chest, FromAny = new[] { key }, Where = new[] { row }, Note = note };
		}
	}

	internal static IEnumerable<(int Item, int Step, string Why)> Steps()
	{
		foreach (BookItemSource s in Sources())
		{
			int floor = Floor(s);
			if (Inputs(s).Length == 0 && floor >= 0)
			{
				yield return (s.Item, floor, "structure:" + s.Where[0]);
			}
		}
	}

	internal static IEnumerable<(int Item, int[][] Inputs, int Floor, string Why)> Routes()
	{
		foreach (BookItemSource s in Sources())
		{
			int[][] inputs = Inputs(s);
			int floor = Floor(s);
			if (inputs.Length > 0 && floor >= 0 && inputs.All((int[] i) => i.Length > 0))
			{
				yield return (s.Item, inputs, floor, "structure:" + s.Where[0]);
			}
		}
	}

	// Title, tooltip and gate: where it is and since when, how to take it, which worlds have it.
	internal static BookAcquisitionEntry Entry(BookItemSource s)
	{
		if (s.Kind != BookAcquisitionKind.Structure)
		{
			return null;
		}
		string row = s.Where[0];
		string skeletron = Lang.GetNPCNameValue(NPCID.SkeletronHead);
		string plantera = Lang.GetNPCNameValue(NPCID.Plantera);
		string guardian = Lang.GetNPCNameValue(NPCID.DungeonGuardian);
		string key = s.FromAny.Length > 0 ? Lang.GetItemNameValue(s.FromAny[0]) : "";
		string chest = Lang.GetItemNameValue(s.From);
		(string name, string body) = row switch
		{
			"DungeonFurniture" => (L("Structures.DungeonFurniture"), L("Structures.DungeonBody", skeletron, guardian)),
			"DungeonLights" => (L("Structures.DungeonLights"), L("Structures.DungeonBody", skeletron, guardian)),
			"DungeonBanners" => (L("Structures.DungeonBanners"), L("Structures.DungeonBody", skeletron, guardian)),
			"DungeonPaintings" => (L("Structures.DungeonPaintings"), L("Structures.DungeonBody", skeletron, guardian)),
			"DungeonTraps" => (L("Structures.DungeonTraps"), L("Structures.DungeonBody", skeletron, guardian)),
			"DungeonBiomeChest" => (L("Structures.DungeonChest", chest), L("Structures.BiomeChestBody", key, plantera)),
			"DungeonGoldChest" => (L("Structures.DungeonChest", chest), L("Structures.LockedChestBody", key)),
			"UnderworldChest" => (L("Structures.UnderworldChest", chest), L("Structures.LockedChestBody", key)),
			"Temple" => (L("Common.JungleTemple"), L("Structures.TempleBody", Lang.GetItemNameValue(ItemID.TempleKey), plantera)),
			_ => (L("Structures.UnderworldHouses"), L("Structures.UnderworldBody"))
		};
		string which = s.Note switch
		{
			"color" => L("Structures.OneColor"),
			"random" => L("Structures.RandomPainting"),
			"corruption" => L("Structures.CorruptionWorld"),
			"crimson" => L("Structures.CrimsonWorld"),
			_ when s.Note.StartsWith("three:", System.StringComparison.Ordinal) => L("Structures.ThreeKinds", 3, s.Note.Substring(6)),
			_ => ""
		};
		List<string> label = new List<string>();
		string gate = Gate(row);
		if (gate.Length > 0)
		{
			label.Add(L("Common.After", gate == "Plantera" ? plantera : skeletron));
		}
		if (row is "DungeonGoldChest" or "UnderworldChest")
		{
			label.Add(L("HowToGet.With", key));
		}
		if (s.NoteValue > 0)
		{
			label.Add(L("HowToGet.Pickaxe", s.NoteValue));
		}
		return new BookAcquisitionEntry(name, which.Length > 0 ? body + "\n" + which : body, s.Kind, "", "×1", s.From, 0, label.Count > 0, -1, s.Owner, string.Join(" · ", label));
	}

	internal static string KindName(BookAcquisitionKind kind)
	{
		return kind == BookAcquisitionKind.Structure ? L("Structures.Kind") : null;
	}

	// The boss a row waits for, as BookSteps.Resolve reads it.
	private static string Gate(string row)
	{
		return row switch
		{
			"DungeonBiomeChest" or "Temple" => "Plantera",
			"Underworld" or "UnderworldChest" => "",
			_ => "SkeletronHead"
		};
	}

	private static int Floor(BookItemSource s)
	{
		return BookSteps.Resolve(Gate(s.Where[0]));
	}

	// The key that opens it, and any pickaxe strong enough for it.
	private static int[][] Inputs(BookItemSource s)
	{
		List<int[]> inputs = new List<int[]>();
		if (s.FromAny.Length > 0)
		{
			inputs.Add(s.FromAny);
		}
		if (s.NoteValue > 0)
		{
			inputs.Add(ContentSamples.ItemsByType.Values.Where((Item i) => i != null && i.pick >= s.NoteValue).Select((Item i) => i.type).ToArray());
		}
		return inputs.ToArray();
	}

	// The row's icon: the structure's brick (the hell houses are of obsidian or hellstone bricks; the latter shows up).
	private static int Brick(string row)
	{
		return row switch
		{
			"Temple" => ItemID.LihzahrdBrick,
			"Underworld" => ItemID.HellstoneBrick,
			_ => ItemID.BlueBrick
		};
	}
}
