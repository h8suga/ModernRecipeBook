using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;

namespace ModernRecipeBook.Sources;

// Vanilla world generation outside the Dungeon, the Jungle Temple and the Underworld houses: what it puts in chests and
// sets up in structures for items nothing else gives, read from tModLoader's WorldGen and Terraria.GameContent.Biomes.
// All of it is there from the start; the moss needs a Paint Scraper, Moon Lord Legs a drunk world. Kind WorldLoot: Where
// holds the place's title and tooltip keys, Note how the item is taken ("chest": it lies in the chest From; "break": it
// is the chest itself; "mine": an object to break; "scrape": long moss; "drunk": a drunk world's chests).
internal static class BookWorldLoot
{
	// Items, place title, tooltip, how, the chest it lies in (none: the item is the find), chance per chest (0: always,
	// or no single roll).
	private static readonly (string Items, string Place, string Body, string How, string Icon, float Chance)[] Finds =
	{
		// WorldGen.AddBuriedChest: a chest near the surface (style 0, above worldSurface + 25) with no set item gets one of
		// ten finds, the Spear and the Blowpipe 1 in 10 each ("Surface Chests" pass, wooden cabins above the surface).
		("Spear Blowpipe", "WorldLoot.SurfaceChests", "WorldLoot.SurfaceChestsBody", "chest", "Chest", 0.1f),
		// The chests themselves (Chest.chestItemSpawn by style): Gold Chests (style 1) are AddBuriedChest's chest below
		// worldSurface + 25, the "Buried Chests" pass's, the wooden cabins' (WoodHouseBuilder) and the pyramids'
		// (WorldGen.Pyramid).
		("GoldChest", "WorldLoot.UndergroundChests", "WorldLoot.GoldChestBody", "break", "", 0f),
		// Ivy Chests (style 10): the "Jungle Chests Placement" pass (jungle shrines) and MahoganyTreeBiome; below the cavern
		// layer AddBuriedChest adds a Bee Minecart 1 in 10.
		("IvyChest", "WorldLoot.JungleChests", "WorldLoot.IvyChestBody", "break", "", 0f),
		("BeeMinecart", "WorldLoot.JungleChests", "WorldLoot.JungleChestsBody", "chest", "IvyChest", 0.1f),
		// Web Covered Chests (style 15): the "Spider Caves" pass furnishes 1 spot in 15 with one, always with a Web Slinger.
		("WebCoveredChest", "WorldLoot.SpiderCaves", "WorldLoot.WebCoveredChestBody", "break", "", 0f),
		("WebSlinger", "WorldLoot.SpiderCaves", "WorldLoot.WebSlingerBody", "chest", "WebCoveredChest", 0f),
		// Water Chests (style 17): the "Water Chests" pass, in ocean caves and in deep water anywhere above the Underworld.
		("WaterChest", "WorldLoot.Underwater", "WorldLoot.WaterChestBody", "break", "", 0f),
		// DeadMansChestBiome ("Micro Biomes" pass): a few underground Gold Chests become Dead Man's Chests (tile 467 style
		// 4), wired to dart traps, boulders and explosives; 1 in 3 also gets a Dead Man's Sweater.
		("DeadMansChest", "WorldLoot.TrappedChests", "WorldLoot.DeadMansChestBody", "break", "", 0f),
		("DeadMansSweater", "WorldLoot.TrappedChests", "WorldLoot.DeadMansSweaterBody", "chest", "DeadMansChest", 1f / 3f),
		// AddBuriedChest's extra rolls below the cavern layer: Frozen Chests (style 11) an Ice Mirror 1 in 5, Sandstone
		// Chests (tile 467 style 10) a Desert Minecart 1 in 15.
		("IceMirror", "WorldLoot.IceChests", "WorldLoot.IceChestsBody", "chest", "IceChest", 0.2f),
		("DesertMinecart", "WorldLoot.DesertChests", "WorldLoot.DesertChestsBody", "chest", "DesertChest", 1f / 15f),
		// Living Wood Chests (style 12, WorldGen.GrowLivingTreePassageRoom), above the cavern layer: a Sunflower or a
		// Ladybug Minecart 1 in 10, 1 in 20 each.
		("LadybugMinecart SunflowerMinecart", "WorldLoot.LivingTrees", "WorldLoot.LivingTreesBody", "chest", "LivingWoodChest", 0.05f),
		// Mushroom Chests (style 32, MushroomHouseBuilder cabins): a Shroom Minecart 1 in 2; the Mushroom set 1 in 3 above
		// the cavern layer, below it whenever the minecart is not there (1 in 2).
		("ShroomMinecart", "WorldLoot.MushroomChests", "WorldLoot.MushroomChestsBody", "chest", "MushroomChest", 0.5f),
		("MushroomHat MushroomVest MushroomPants", "WorldLoot.MushroomChests", "WorldLoot.MushroomSetBody", "chest", "MushroomChest", 0f),
		// WorldGen.FinishDrunkGen: in a drunk world 1 chest in 15 that holds anything gets Moon Lord Legs.
		("MoonLordLegs", "WorldLoot.AnyChests", "WorldLoot.MoonLordLegsBody", "drunk", "Chest", 1f / 15f),
		// WorldGen.placeTrap ("Traps" pass, underground caves without a wall): dart traps (tile 137 style 0), below the lava
		// line geysers (tile 443) 5 times in 6; DeadMansChestBiome sets more dart traps by its chests.
		("DartTrap", "WorldLoot.Caves", "WorldLoot.DartTrapBody", "mine", "", 0f),
		("GeyserTrap", "WorldLoot.LavaCaves", "WorldLoot.GeyserBody", "mine", "", 0f),
		// MiningExplosivesBiome ("Micro Biomes" pass, the caverns): an explosive on an ore deposit, wired to a Detonator
		// (tile 411) a few tiles away.
		("Detonator", "WorldLoot.OreDeposits", "WorldLoot.DetonatorBody", "mine", "", 0f),
		// Banners (tile 91): WorldGen.Pyramid styles 4-6, WorldGen.IslandHouse styles 7-9.
		("AnkhBanner SnakeBanner OmegaBanner", "WorldLoot.Pyramids", "WorldLoot.PyramidsBody", "mine", "", 0f),
		("WorldBanner SunBanner GravityBanner", "WorldLoot.SkyIslands", "WorldLoot.SkyIslandsBody", "mine", "", 0f),
		// HouseBuilder.FillRooms hangs WorldGen.RandHousePicture in every underground cabin but the desert ones (tile 240
		// styles 20-22, 24-26, 28, 33-35, 76-79, 82; 245 styles 0, 3, 5, 6, 14-21; 246 styles 1, 30-36) and
		// RandHousePictureDesert in those (240 styles 63-68, 245 styles 7-8, 242 styles 37-42); the items are
		// TileLoader.GetItemDropFromTypeAndStyle's (245 styles 15-18 are all Strange Growth, WorldGen.Check2x3Wall).
		("Sunflowers TerrarianGothic GuidePicasso FatherofSomeone NurseLisa Discover OldMiner TheMerchant CrownoDevoursHisLunch RareEnchantment Outcast FairyGuides AHorribleNightforAlchemy MorningHunt CatSword Waldo Land AmericanExplosive GloriousNight SufficientlyAdvanced StrangeGrowth HappyLittleTree StrangeDeadFellows Secrets FindingGold Bioluminescence Wildflowers VikingVoyage Bifrost Heartlands ForestTroll AuroraBorealis", "WorldLoot.Cabins", "WorldLoot.CabinsBody", "mine", "", 0f),
		("AndrewSphinx WatchfulAntlion BurningSpirit JawsOfDeath TheSandsOfSlime SnakesIHateSnakes BandageBoy DivineEye LifeAboveTheSand Oasis PrehistoryPreserved AncientTablet Uluru VisitingThePyramids", "WorldLoot.DesertHouses", "WorldLoot.DesertHousesBody", "mine", "", 0f),
		// The "Final Cleanup" pass turns 3, 6 or 9 dirt blocks (small, medium, large world) into The Dirtiest Block (tile
		// 668, WorldGen.KillTile_GetItemDrops).
		("DirtiestBlock", "WorldLoot.Dirt", "WorldLoot.DirtiestBlockBody", "mine", "", 0f)
	};

	// Player.PlaceThing_PaintScrapper_LongMoss: a Paint Scraper (ItemID.Sets.IsPaintScraper) cutting long moss (tile 184)
	// drops its moss 1 in 9, by frameX / 22 in this order. World generation grows these ten in the caverns (the "Moss"
	// pass, lava moss by the lava, WorldGen.neonMossBiome); helium moss (frame 10) is never generated.
	private static readonly string[] Mosses = { "GreenMoss", "BrownMoss", "RedMoss", "BlueMoss", "PurpleMoss", "LavaMoss", "KryptonMoss", "XenonMoss", "ArgonMoss", "VioletMoss" };

	internal static IEnumerable<BookItemSource> Sources()
	{
		foreach (var (items, place, body, how, icon, chance) in Finds)
		{
			int chest = Id(icon);
			foreach (int type in items.Split(' ').Select(Id).Where((int t) => t > 0))
			{
				yield return new BookItemSource(BookAcquisitionKind.WorldLoot, type) { From = chest > 0 ? chest : type, Where = new[] { place, body }, Note = how, Chance = chance };
			}
		}
		int[] scrapers = ContentSamples.ItemsByType.Keys.Where((int t) => ItemID.Sets.IsPaintScraper[t]).ToArray();
		for (int i = 0; i < Mosses.Length; i++)
		{
			int type = Id(Mosses[i]);
			if (type > 0)
			{
				yield return new BookItemSource(BookAcquisitionKind.WorldLoot, type) { FromAny = scrapers, Tile = TileID.LongMoss, FrameX = i * 22, Where = new[] { "WorldLoot.CaveMoss", "WorldLoot.CaveMossBody" }, Note = "scrape", Chance = 1f / 9f };
			}
		}
	}

	// From the start; a drunk world's find only in a drunk world (elsewhere the item keeps its rarity guess).
	internal static IEnumerable<(int Item, int Step, string Why)> Steps()
	{
		return from s in Sources()
			where s.Note != "scrape" && (s.Note != "drunk" || Main.drunkWorld)
			select (s.Item, 0, "worldgen:" + s.Where[0]);
	}

	// The moss comes with the first Paint Scraper.
	internal static IEnumerable<(int Item, int[][] Inputs, int Floor, string Why)> Routes()
	{
		return from s in Sources()
			where s.Note == "scrape" && s.FromAny.Length > 0
			select (s.Item, new[] { s.FromAny }, 0, "use:PaintScraper");
	}

	// Title: the place; detail: how it is taken; tooltip: where exactly and how; icon: the chest it lies in, else the item
	// (the long moss tuft for moss).
	internal static BookAcquisitionEntry Entry(BookItemSource s)
	{
		if (s.Kind != BookAcquisitionKind.WorldLoot)
		{
			return null;
		}
		string scraper = Lang.GetItemNameValue(ItemID.PaintScraper);
		string how = s.Note switch
		{
			"chest" => L("WorldLoot.InTheChest"),
			"break" => L("WorldLoot.BreakTheChest"),
			"scrape" => L("WorldLoot.CutWith", scraper),
			"drunk" => L("WorldLoot.DrunkWorld"),
			_ => L("WorldLoot.MineIt")
		};
		string chance = s.Chance > 0f && s.Chance < 1f ? BookAcquisitionRules.Chance(s.Chance) : "";
		bool drunk = s.Note == "drunk";
		return new BookAcquisitionEntry(L(s.Where[0]), L(s.Where[1], scraper), s.Kind, chance, "×1", s.Tile >= 0 ? 0 : s.From, 0, drunk, -1, s.Owner, how, "", s.Tile, s.FrameX, s.FrameY, OtherMode: drunk && !Main.drunkWorld);
	}

	internal static string KindName(BookAcquisitionKind kind)
	{
		return kind == BookAcquisitionKind.WorldLoot ? L("Common.World") : null;
	}

	private static int Id(string name)
	{
		return ItemID.Search.TryGetId(name, out int id) ? id : 0;
	}
}
