using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.Sources;

// What world generation puts into chests (the BookCurated area "SourceRows"), vanilla's and Calamity 2.2.4's, one row per
// item and chest place, with the chance and stack per chest. Vanilla fills almost every chest in WorldGen.AddBuriedChest
// (tModLoader's WorldGen.cs:31860-33233): an item the caller or the chest's kind sets, else a random one of its depth band
// (surface, underground, caverns, deepest; :32086/32345/32589/32875), then the band's extra rolls and the chest kind's
// own extras (:33091-33217). Calamity fills its labs, shrines and other structures itself (CalamityMod.World) and changes
// a few vanilla chests afterwards (WorldgenManagementSystem.PostWorldGen, ILChanges' sky island houses). Locked chests
// and the Dungeon wait for their key and boss; everything else is there from world creation. The Dungeon's Biome Chest
// weapons and the locked chests themselves stay with BookStructures. Left out: seed-only loot (For the Worthy, remix,
// Don't Starve, tenth anniversary, getfixedboi), except the drunk world's.
internal static class BookChestLoot
{
	// A chest place: title and tooltip keys (title {0}: the chest's name; tooltip {0}: the boss, {1}: the key), the chest
	// item (the row's icon and the card it opens), the boss after which it opens (a BookSteps term), the keys that open
	// it (any one of them), a note every row of it has (see Loot) and whether Calamity makes it.
	private sealed record Place(string Title, string Body, string Chest, string Gate = "", string Keys = "", string Note = "", bool Calamity = false);

	private static readonly Dictionary<string, Place> Places = new Dictionary<string, Place>(StringComparer.Ordinal)
	{
		// Vanilla. Surface: the "Surface Chests" pass's wooden chests and cabins above the surface (WorldGen.cs:12538-12605,
		// HouseBuilder.cs:366-369); the band's loot also goes to the pyramid's Gold Chest (flag9, :32086; Pyramid :20141-20162).
		["SurfaceWooden"] = new Place("WorldLoot.SurfaceChests", "WorldLoot.SurfaceChestsBody", "Chest"),
		["Surface"] = new Place("WorldLoot.SurfaceChests", "SourceRows.SurfaceBody", "Chest"),
		["Pyramid"] = new Place("SourceRows.Pyramid", "SourceRows.PyramidBody", "GoldChest"),
		// The other bands take every chest whatever its kind (Gold Chests of caves and cabins, biome, sky island, water and
		// Dungeon chests); the band's random item only goes to a chest nothing else filled: caves' and cabins' Gold Chests,
		// mushroom cabins, a quarter of the mahogany trees' (Buried Chests :12457-12533, HouseBuilder.cs:357-441).
		["Underground"] = new Place("WorldLoot.UndergroundChests", "SourceRows.UndergroundBody", "GoldChest"),
		["UndergroundGold"] = new Place("WorldLoot.UndergroundChests", "SourceRows.UndergroundGoldBody", "GoldChest"),
		["Cavern"] = new Place("SourceRows.Cavern", "SourceRows.CavernBody", "GoldChest"),
		["CavernGold"] = new Place("SourceRows.Cavern", "SourceRows.CavernGoldBody", "GoldChest"),
		["Deep"] = new Place("SourceRows.Deep", "SourceRows.DeepBody", "ShadowChest"),
		["DeepGold"] = new Place("SourceRows.Deep", "SourceRows.DeepGoldBody", "GoldChest"),
		// The Underworld's Shadow Chests, locked (Chest.cs:551), take GenVars.hellChestItem in turn (:7711-7724, :32002).
		["Shadow"] = new Place("SourceRows.Shadow", "SourceRows.ShadowBody", "ShadowChest", Keys: "ShadowKey"),
		["Ice"] = new Place("WorldLoot.IceChests", "SourceRows.IceBody", "IceChest"),
		["Desert"] = new Place("WorldLoot.DesertChests", "SourceRows.DesertBody", "DesertChest"),
		["Mushroom"] = new Place("WorldLoot.MushroomChests", "WorldLoot.MushroomChestsBody", "MushroomChest"),
		["JungleShrine"] = new Place("SourceRows.JungleShrine", "SourceRows.JungleShrineBody", "IvyChest"),
		["Mahogany"] = new Place("SourceRows.Mahogany", "SourceRows.MahoganyBody", "IvyChest"),
		["Water"] = new Place("WorldLoot.Underwater", "SourceRows.WaterBody", "WaterChest"),
		["OceanCave"] = new Place("SourceRows.OceanCave", "SourceRows.OceanCaveBody", "WaterChest"),
		["LivingTree"] = new Place("WorldLoot.LivingTrees", "WorldLoot.LivingTreesBody", "LivingWoodChest"),
		["Spider"] = new Place("WorldLoot.SpiderCaves", "WorldLoot.WebSlingerBody", "WebCoveredChest"),
		["Sky"] = new Place("WorldLoot.SkyIslands", "SourceRows.SkyBody", "SkywareChest"),
		// The temple's door opens with the Temple Key Plantera drops (Lihzahrd Chests are not locked, Chest.cs:343).
		["Temple"] = new Place("SourceRows.Temple", "SourceRows.TempleBody", "LihzahrdChest", "Plantera", "TempleKey"),
		// MakeDungeon's rooms (:28487-28548): Locked Gold Chests (a Golden Key opens them, Chest.cs:546) and the wooden chest
		// with the Golden Key; its Biome Chests (Plantera and their own key, Chest.cs:555-584).
		["DungeonGold"] = new Place("Structures.DungeonChest", "SourceRows.DungeonGoldBody", "GoldChest", "SkeletronHead", "GoldenKey"),
		["DungeonWooden"] = new Place("Structures.DungeonChest", "SourceRows.DungeonWoodenBody", "Chest", "SkeletronHead"),
		["BiomeChests"] = new Place("SourceRows.BiomeChests", "SourceRows.BiomeChestsBody", "JungleChest", "Plantera", "JungleKey CorruptionKey CrimsonKey HallowedKey FrozenKey DungeonDesertKey"),
		["DeadMan"] = new Place("WorldLoot.TrappedChests", "WorldLoot.DeadMansSweaterBody", "DeadMansChest"),
		["Drunk"] = new Place("WorldLoot.AnyChests", "SourceRows.DrunkBody", "Chest", Note: "drunk"),
		// Calamity's Draedon structures (DraedonStructures.cs; chests per world from their schematics).
		["Workshop"] = new Place("SourceRows.Workshop", "SourceRows.WorkshopBody", "AgedSecurityChest", Calamity: true),
		["Outpost"] = new Place("SourceRows.Outpost", "SourceRows.OutpostBody", "AgedSecurityChest", Calamity: true),
		["HellLab"] = new Place("HowToGet.ChestHellLab", "SourceRows.HellLabBody", "SecurityChest", Calamity: true),
		["SunkenLab"] = new Place("SourceRows.SunkenLab", "SourceRows.LabBody", "SecurityChest", Calamity: true),
		["IceLab"] = new Place("SourceRows.IceLab", "SourceRows.LabBody", "SecurityChest", Calamity: true),
		["PlagueLab"] = new Place("SourceRows.PlagueLab", "SourceRows.LabBody", "SecurityChest", Calamity: true),
		["PlanetoidLab"] = new Place("SourceRows.PlanetoidLab", "SourceRows.LabBody", "SecurityChest", Calamity: true),
		["CavernLab"] = new Place("SourceRows.CavernLab", "SourceRows.LabBody", "SecurityChest", Calamity: true),
		// UndergroundShrines.cs: one shrine of each kind per world, the evil ones in their world (drunk: both).
		["CorruptionShrine"] = new Place("SourceRows.CorruptionShrine", "SourceRows.ShrineBody", "LesionChest", Note: "corruption", Calamity: true),
		["CrimsonShrine"] = new Place("SourceRows.CrimsonShrine", "SourceRows.ShrineBody", "FleshChest", Note: "crimson", Calamity: true),
		["DesertShrine"] = new Place("SourceRows.DesertShrine", "SourceRows.ShrineBody", "DesertChest", Calamity: true),
		["GraniteShrine"] = new Place("SourceRows.GraniteShrine", "SourceRows.ShrineBody", "GraniteChest", Calamity: true),
		["IceShrine"] = new Place("SourceRows.IceShrine", "SourceRows.ShrineBody", "IceChest", Calamity: true),
		["MarbleShrine"] = new Place("SourceRows.MarbleShrine", "SourceRows.ShrineBody", "MarbleChest", Calamity: true),
		["MushroomShrine"] = new Place("SourceRows.MushroomShrine", "SourceRows.ShrineBody", "MushroomChest", Calamity: true),
		["SurfaceShrine"] = new Place("SourceRows.SurfaceShrine", "SourceRows.ShrineBody", "BambooChest", Calamity: true),
		["AbyssShrine"] = new Place("SourceRows.AbyssShrine", "SourceRows.AbyssShrineBody", "VoidChest", Calamity: true),
		// Abyss.AbyssChest, locked until Skeletron's first defeat (AbyssTreasureChest.UnlockChest, CalamityGlobalNPC:
		// UnlockAllAbyssChests); AstralChestGeneration's chest in the Dungeon opens after Astrum Aureus (AstralChestLocked).
		["AbyssChests"] = new Place("CalamityLoot.AbyssChests", "SourceRows.AbyssChestsBody", "AbyssTreasureChest", "SkeletronHead", Calamity: true),
		["Astral"] = new Place("HowToGet.ChestAstralChest", "HowToGet.ChestAstralChestBody", "AstralChest", "CalamityMod/AstrumAureus", Calamity: true),
		["Sulphur"] = new Place("SourceRows.Sulphur", "SourceRows.SulphurBody", "RustyChest", Calamity: true),
		["Crag"] = new Place("SourceRows.Crag", "SourceRows.CragBody", "AshenChest", Calamity: true),
		["Honey"] = new Place("SourceRows.Honey", "SourceRows.HoneyBody", "HoneyChest", Calamity: true),
		["MudPlanet"] = new Place("SourceRows.MudPlanet", "SourceRows.MudPlanetBody", "WaterChest", Calamity: true),
		["Vernal"] = new Place("SourceRows.Vernal", "SourceRows.VernalBody", "IvyChest", Calamity: true),
		["ShimmerShrine"] = new Place("SourceRows.ShimmerShrine", "SourceRows.ShrineBody", "GoldChest", Calamity: true),
		["MechanicShed"] = new Place("SourceRows.MechanicShed", "SourceRows.MechanicShedBody", "IceChest", Calamity: true),
		["Archive"] = new Place("SourceRows.Archive", "SourceRows.ArchiveBody", "BlueDungeonChest", "SkeletronHead", Calamity: true),
		["EvilIsland"] = new Place("SourceRows.EvilIsland", "SourceRows.EvilIslandBody", "CorruptionChest", Calamity: true)
	};

	// Places (one or more), items (one row each), chance per chest (0: no single roll), stack, notes (comma-separated):
	// "ore" the world's bar of that pair (GenVars.copperBar and the others, WorldGen.cs:7729-7760), "crimson" or
	// "corruption" a world of that evil, "drunk" a drunk world; "one" in one chest of the structure, "others" in each of
	// its other chests; "ug", "cav", "lava" only above the cavern layer, in it, below the lava line; "gold" only Gold Chests;
	// "set" the whole set together; "turns" handed out in turn; "upper"/"lower" the upper or deeper underground desert;
	// "eye"/"eyekept" Calamity's loot in place of the Suspicious Looking Eye; "sky:N" the first N sky houses each get
	// another; "use:<boss>" works only after that boss; "break" the chest itself, emptied and broken; "calamity" or
	// "!calamity" only with or without Calamity.
	private static readonly (string Places, string Items, float Chance, int Min, int Max, string Note)[] Loot =
	{
		// Surface band: the random item of a chest nothing filled (:32124-32185), then the extras (:32188-32343). Calamity puts
		// a Kylie in place of the Portable Stool (WorldgenManagementSystem.cs:415-419).
		("SurfaceWooden", "Spear Blowpipe WoodenBoomerang Aglet ClimbingClaws Umbrella CordageGuide WandofSparking Radar", 0.1f, 1, 1, ""),
		("SurfaceWooden", "PortableStool", 0.1f, 1, 1, "!calamity"),
		("SurfaceWooden", "Kylie", 0.1f, 1, 1, "calamity"),
		("Surface", "Glowstick", 1f / 6f, 40, 75, ""),
		("Surface", "ThrowingKnife", 1f / 6f, 150, 300, ""),
		// 1, one more 1 in 5, up to two more 1 in 10 (:32202-32229).
		("Surface", "HerbBag CanOfWorms", 1f / 6f, 1, 4, ""),
		("Surface", "Grenade", 1f / 3f, 3, 5, ""),
		("Surface", "CopperBar TinBar IronBar LeadBar", 0.25f, 3, 10, "ore"),
		("Surface", "Rope", 0.5f, 50, 100, ""),
		("Surface", "WoodenArrow Shuriken", 1f / 3f, 25, 50, ""),
		("Surface", "LesserHealingPotion", 0.5f, 3, 5, ""),
		("Surface", "RecallPotion", 2f / 3f, 3, 5, ""),
		("Surface", "IronskinPotion ShinePotion NightOwlPotion SwiftnessPotion MiningPotion BuilderPotion", 1f / 9f, 1, 2, ""),
		("Surface", "Torch Bottle", 0.25f, 10, 20, ""),
		("Surface", "SilverCoin", 0.5f, 10, 29, ""),
		("Surface", "Wood", 0.5f, 50, 99, ""),
		// The pyramid picks Next(3) twice over a 0 (:20141-20162); the mask comes with its robe (:32095-32098).
		("Pyramid", "SandstorminaBottle FlyingCarpet", 4f / 9f, 1, 1, ""),
		("Pyramid", "PharaohsMask PharaohsRobe", 1f / 9f, 1, 1, "set"),
		// Underground band (:32394-32587): one of six, an Extractinator 1 in 20, else a Flare Gun with flares 1 in 20.
		("UndergroundGold", "BandofRegeneration MagicMirror CloudinaBottle HermesBoots Mace ShoeSpikes", 1f / 6f, 1, 1, ""),
		("UndergroundGold", "Extractinator", 0.05f, 1, 1, ""),
		("UndergroundGold", "FlareGun", 0.0475f, 1, 1, ""),
		("UndergroundGold", "Flare", 0.0475f, 25, 50, ""),
		("Underground", "Bomb", 1f / 3f, 10, 19, ""),
		("Underground", "AngelStatue", 0.2f, 1, 1, ""),
		("Underground", "Rope", 1f / 3f, 50, 100, ""),
		("Underground", "IronBar LeadBar SilverBar TungstenBar", 0.25f, 5, 14, "ore"),
		("Underground", "WoodenArrow Shuriken", 0.25f, 25, 49, ""),
		("Underground", "LesserHealingPotion", 0.5f, 3, 5, ""),
		("Underground", "RegenerationPotion ShinePotion NightOwlPotion SwiftnessPotion ArcheryPotion GillsPotion HunterPotion MiningPotion TrapsightPotion", 2f / 27f, 1, 2, ""),
		("Underground", "RecallPotion", 2f / 3f, 2, 4, ""),
		("Underground", "Torch", 0.5f, 10, 20, ""),
		("Underground", "SilverCoin", 0.5f, 50, 89, ""),
		// Cavern band (:32589-32873): a Lava Charm 1 in 20 below the lava line, else an Extractinator 1 in 15, else one of
		// seven (here as above the lava line), the Flare Gun with flares.
		("CavernGold", "BandofRegeneration MagicMirror CloudinaBottle HermesBoots Mace ShoeSpikes FlareGun", 2f / 15f, 1, 1, ""),
		("CavernGold", "Flare", 2f / 15f, 25, 50, ""),
		("CavernGold", "Extractinator", 1f / 15f, 1, 1, ""),
		("CavernGold", "LavaCharm", 0.05f, 1, 1, "lava"),
		("Cavern", "SuspiciousLookingEye", 0.2f, 1, 1, "!calamity"),
		("Cavern", "Dynamite", 1f / 3f, 1, 1, ""),
		("Cavern", "JestersArrow FlamingArrow ThrowingKnife", 0.25f, 25, 50, ""),
		("Cavern", "GoldBar PlatinumBar SilverBar TungstenBar", 0.25f, 3, 10, "ore"),
		("Cavern", "HealingPotion", 0.5f, 3, 5, ""),
		("Cavern", "SpelunkerPotion FeatherfallPotion NightOwlPotion WaterWalkingPotion ArcheryPotion GravitationPotion", 1f / 9f, 1, 2, ""),
		("Cavern", "ThornsPotion InvisibilityPotion HunterPotion TrapsightPotion TeleportationPotion TitanPotion", 1f / 18f, 1, 2, ""),
		("Cavern", "RecallPotion", 0.5f, 2, 4, ""),
		("Cavern", "Torch Glowstick", 0.25f, 15, 29, ""),
		("Cavern", "GoldCoin", 0.5f, 1, 2, ""),
		// Calamity takes the eye out of most cavern chests (WorldgenManagementSystem.cs:366-411): an Enchanted Knife Staff in
		// Gold Chests, a Shroomerang in mushroom chests, else Shine 1 in 5, Builder 1 in 5 or Mining Potions 3 in 5.
		("Cavern", "SuspiciousLookingEye", 0.2f, 1, 1, "calamity,eyekept"),
		("Cavern", "EnchantedKnifeStaff", 0.2f, 1, 1, "calamity,gold"),
		("Cavern", "ShinePotion", 0.04f, 5, 9, "calamity,eye"),
		("Cavern", "BuilderPotion", 0.04f, 2, 3, "calamity,eye"),
		("Cavern", "MiningPotion", 0.12f, 3, 5, "calamity,eye"),
		// Deepest band (:32875-33089): the Shadow Chests' own item, or one of four in a Gold Chest just above them.
		("DeepGold", "BandofRegeneration MagicMirror CloudinaBottle HermesBoots", 0.25f, 1, 1, ""),
		("Deep", "Dynamite", 1f / 3f, 1, 1, ""),
		("Deep", "MeteoriteBar", 0.25f, 15, 29, ""),
		("Deep", "GoldBar PlatinumBar", 0.25f, 15, 29, "ore"),
		("Deep", "HellfireArrow", 0.25f, 50, 74, ""),
		("Deep", "SilverBullet TungstenBullet", 0.25f, 50, 74, "ore"),
		("Deep", "RestorationPotion", 0.5f, 15, 20, ""),
		("Deep", "SpelunkerPotion FeatherfallPotion ManaRegenerationPotion MagicPowerPotion InvisibilityPotion HunterPotion HeartreachPotion", 3f / 32f, 1, 2, ""),
		("Deep", "GravitationPotion ThornsPotion WaterWalkingPotion BattlePotion TeleportationPotion InfernoPotion LifeforcePotion", 1f / 12f, 1, 2, ""),
		// In both potion rolls: 1 - (29/32)(11/12).
		("Deep", "ObsidianSkinPotion", 65f / 384f, 1, 2, ""),
		("Deep", "RecallPotion PotionOfReturn", 1f / 6f, 1, 2, ""),
		("Deep", "Torch Glowstick", 0.25f, 15, 29, ""),
		("Deep", "GoldCoin", 0.5f, 2, 4, ""),
		("Shadow", "DarkLance Sunfury FlowerofFire Flamelash HellwingBow", 0.2f, 1, 1, "turns"),
		("Shadow", "TreasureMagnet", 0.2f, 1, 1, ""),
		("Shadow", "HellMinecart OrnateShadowKey HellCake", 0.1f, 1, 1, ""),
		// Frozen Chests (style 11, :31967-31987): one of six, an Extractinator 1 in 20, a Fish 1 in 50; Ice Mirror 1 in 5 in the
		// caverns (:32596), Ice Machine 1 in 7 (:33098), ice torches for the band's torches (:32571, :32852).
		("Ice", "IceBoomerang IceBlade IceSkates SnowballCannon BlizzardinaBottle FlurryBoots", 931f / 6000f, 1, 1, ""),
		("Ice", "Extractinator", 0.049f, 1, 1, ""),
		("Ice", "Fish", 0.02f, 1, 1, ""),
		("Ice", "IceMirror", 0.2f, 1, 1, "cav"),
		("Ice", "IceMachine", 1f / 7f, 1, 1, ""),
		("Ice", "IceTorch", 0.5f, 10, 20, "ug"),
		("Ice", "IceTorch", 0.25f, 15, 29, "cav"),
		// Sandstone Chests (tile 467 style 10, :31956-31961): by depth in the desert hive; Scarab Bombs for the band's bombs
		// (:32454), the cavern extras (:32603-32612). Calamity adds a Desert Medallion or The Comb (WorldgenManagementSystem.cs:424-451).
		("Desert", "AncientChisel SandBoots MysticCoilSnake MagicConch", 0.25f, 1, 1, "upper"),
		("Desert", "ThunderSpear ThunderStaff CatBast", 1f / 3f, 1, 1, "lower"),
		("Desert", "ScarabBomb", 1f / 3f, 10, 19, "ug"),
		("Desert", "EncumberingStone", 1f / 7f, 1, 1, "cav"),
		("Desert", "DesertMinecart", 1f / 15f, 1, 1, "cav"),
		("Desert", "DesertMedallion TheComb", 0.2f, 1, 1, "calamity"),
		// Mushroom Chests (style 32): a Shroom Minecart or the Mushroom set (:32439-32452, :32706-32721); Calamity's
		// Shroomerang in place of the eye (WorldgenManagementSystem.cs:384).
		("Mushroom", "ShroomMinecart", 0.5f, 1, 1, ""),
		("Mushroom", "MushroomHat MushroomVest MushroomPants", 1f / 3f, 1, 1, "ug,set"),
		("Mushroom", "MushroomHat MushroomVest MushroomPants", 0.5f, 1, 1, "cav,set"),
		("Mushroom", "Shroomerang", 0.2f, 1, 1, "calamity,cav"),
		// Ivy Chests: GetNextJungleChestItem (:6696-6728) cycles four items, then Seaweed 1 in 50, a Fiberglass Fishing Pole
		// 1 in 15, Flower Boots 1 in 20; a mahogany tree gives one 3 times in 4 (MahoganyTreeBiome.cs:88). The cavern
		// extras (:32614-32622), the Honey Dispenser 1 in 4 (:33093).
		("JungleShrine", "FeralClaws AnkletoftheWind StaffofRegrowth Boomstick", 0.2172f, 1, 1, "turns"),
		("JungleShrine", "Seaweed", 0.02f, 1, 1, ""),
		("JungleShrine", "FiberglassFishingPole", 49f / 750f, 1, 1, ""),
		("JungleShrine", "FlowerBoots", 343f / 7500f, 1, 1, ""),
		("JungleShrine", "LivingMahoganyWand LivingMahoganyLeafWand", 1f / 6f, 1, 1, "cav"),
		("JungleShrine", "BeeMinecart", 0.1f, 1, 1, "cav"),
		("Mahogany", "FeralClaws AnkletoftheWind StaffofRegrowth Boomstick", 0.1629f, 1, 1, "turns"),
		("Mahogany", "Seaweed", 0.015f, 1, 1, ""),
		("Mahogany", "FiberglassFishingPole", 0.049f, 1, 1, ""),
		("Mahogany", "FlowerBoots", 343f / 10000f, 1, 1, ""),
		("Mahogany", "LivingMahoganyWand LivingMahoganyLeafWand", 0.125f, 1, 1, "cav"),
		("Mahogany", "BeeMinecart", 0.075f, 1, 1, "cav"),
		("JungleShrine Mahogany", "HoneyDispenser", 0.25f, 1, 1, ""),
		// Water Chests: the "Water Chests" pass gives Water Walking Boots 1 in 10, else the next of four in turn (:12658-12729);
		// the ocean cave's one of five (:12643); Shark Bait and a Sandcastle Bucket 1 in 2 each (:32359-32366).
		("Water", "WaterWalkingBoots", 0.1f, 1, 1, ""),
		("Water", "BreathingReed FloatingTube Trident Flipper", 0.225f, 1, 1, "turns"),
		("OceanCave", "WaterWalkingBoots BreathingReed Trident Flipper FloatingTube", 0.2f, 1, 1, ""),
		("Water OceanCave", "SharkBait SandcastleBucket", 0.5f, 1, 1, ""),
		// The living tree's room (GrowLivingTreePassageRoom :21918-21950): a Living Wood Wand with a Leaf Wand, else a Baby
		// Bird Staff; a Sunflower or Ladybug Minecart 1 in 20 each (:32372-32386).
		("LivingTree", "LivingWoodWand LeafWand", 2f / 3f, 1, 1, "set"),
		("LivingTree", "BabyBirdStaff", 1f / 3f, 1, 1, ""),
		("LivingTree", "SunflowerMinecart LadybugMinecart", 0.05f, 1, 1, ""),
		// Spread.Spider (:479-482): every Web Covered Chest.
		("Spider", "WebSlinger", 1f, 1, 1, ""),
		// IslandHouse (:72373-72400): the first four houses one each, later ones one of four; Calamity's IL edit adds Fledgling
		// Wings as a fifth and drops the 1 in 40 (ILChanges.cs:2194-2209, 5491-5539). Sky Mill 1 in 3, one of six paintings,
		// clouds (:33103-33142).
		("Sky", "ShinyRedBalloon Starfury LuckyHorseshoe CelestialMagnet", 0f, 1, 1, "!calamity,sky:4"),
		("Sky", "ShinyRedBalloon Starfury LuckyHorseshoe CelestialMagnet CreativeWings", 0f, 1, 1, "calamity,sky:5"),
		("Sky", "CreativeWings", 0.025f, 1, 1, "!calamity"),
		("Sky", "SkyMill", 1f / 3f, 1, 1, ""),
		("Sky", "SeeTheWorldForWhatItIs HighPitch BlessingfromTheHeavens Constellation LoveisintheTrashSlot SunOrnament", 1f / 6f, 1, 1, ""),
		("Sky", "Cloud", 1f, 50, 100, ""),
		// templePart2 (:26908-26914) and the Lihzahrd Chest's extras (:33143-33170); Calamity's temple fills them the same way
		// (CustomTemple.cs:854).
		("Temple", "LihzahrdPowerCell LihzahrdFurnace", 1f, 1, 1, ""),
		("Temple", "SolarTablet", 0.2f, 1, 1, ""),
		("Temple", "LunarTabletFragment", 0.8f, 3, 7, ""),
		// The rooms take seven items in turn with the Golden Key's wooden chest as the eighth (:28497-28535); the Shadow Key
		// 1 in 3 (:32387), a Bone Welder 1 in 8 by a dungeon wall (:33148); Remnants of Devotion in half the Biome Chests (:33153).
		("DungeonGold", "Muramasa CobaltShield AquaScepter BlueMoon MagicMissile Valor Handgun", 1f / 7f, 1, 1, "turns"),
		("DungeonGold", "ShadowKey", 1f / 3f, 1, 1, ""),
		("DungeonGold DungeonWooden", "BoneWelder", 0.125f, 1, 1, ""),
		("DungeonWooden", "GoldenKey", 1f, 1, 1, ""),
		("BiomeChests", "RemnantsofDevotion", 0.5f, 1, 1, ""),
		// DeadMansChestBiome.cs:488-507; FinishDrunkGen (:18790-18812).
		("DeadMan", "DeadMansSweater", 1f / 3f, 1, 1, ""),
		("Drunk", "MoonLordLegs", 1f / 15f, 1, 1, ""),
		("Drunk", "RedPotion", 1f / 30f, 1, 1, ""),

		// Calamity's Draedon structures. Rusted Workshops (FillWorkshopChest, DraedonStructures.cs:53-88) and Research Outposts
		// (:156-191): a Lab Seeking Mechanism 1 in 2, else one of five colours.
		("Workshop Outpost", "LabSeekingMechanism", 0.5f, 1, 1, ""),
		("Workshop Outpost", "CyanSeekingMechanism GreenSeekingMechanism WhiteSeekingMechanism RedSeekingMechanism YellowSeekingMechanism", 0.1f, 1, 1, ""),
		("Workshop Outpost", "SuspiciousScrap", 1f, 1, 1, ""),
		// The labs' common fill (Hell :259-268, Sunken :353-362, Ice :444-453, Plague :557-566, Planetoid :670-685).
		("Workshop HellLab SunkenLab IceLab PlagueLab PlanetoidLab", "DubiousPlating", 1f, 8, 14, ""),
		("Workshop HellLab SunkenLab IceLab PlagueLab PlanetoidLab", "MysteriousCircuitry", 1f, 7, 12, ""),
		("Workshop HellLab SunkenLab IceLab PlagueLab PlanetoidLab", "Torch", 1f, 15, 29, ""),
		("Workshop HellLab SunkenLab IceLab PlagueLab PlanetoidLab", "GoldCoin", 1f, 5, 11, ""),
		("Workshop HellLab SunkenLab IceLab PlagueLab PlanetoidLab", "HealingPotion", 1f, 5, 7, ""),
		("Workshop HellLab SunkenLab IceLab PlagueLab PlanetoidLab", "Bomb", 1f, 6, 7, ""),
		("Workshop HellLab SunkenLab IceLab PlagueLab PlanetoidLab", "EndurancePotion GravitationPotion HeartreachPotion LifeforcePotion", 0.25f, 3, 5, ""),
		// Outposts and the cavern lab (FillCavernLaboratoryChest :721-731) fill more.
		("Outpost CavernLab", "DubiousPlating", 1f, 10, 17, ""),
		("Outpost CavernLab", "MysteriousCircuitry", 1f, 10, 15, ""),
		("Outpost CavernLab", "Torch", 1f, 20, 40, ""),
		("Outpost CavernLab", "GoldCoin", 1f, 8, 16, ""),
		("Outpost CavernLab", "HealingPotion", 1f, 7, 10, ""),
		("Outpost CavernLab", "Dynamite", 1f, 4, 6, ""),
		("Outpost CavernLab", "EndurancePotion GravitationPotion HeartreachPotion LifeforcePotion", 0.25f, 4, 7, ""),
		("CavernLab", "LabSeekingMechanism", 1f, 1, 1, ""),
		// Each lab's mechanism, and the log and schematic of one of its chests (the Hell lab's first chest also the Murasama,
		// which works after the Devourer of Gods, Murasama.CanUseItem).
		("HellLab", "WhiteSeekingMechanism", 1f, 1, 1, ""),
		("HellLab", "DraedonsLogHell EncryptedSchematicHell", 1f, 1, 1, "one"),
		("HellLab", "Murasama", 1f, 1, 1, "one,use:CalamityMod/DevourerofGodsHead"),
		("SunkenLab", "YellowSeekingMechanism", 1f, 1, 1, ""),
		("SunkenLab", "DraedonsLogSunkenSea EncryptedSchematicSunkenSea", 1f, 1, 1, "one"),
		("IceLab", "LabSeekingMechanism", 1f, 1, 1, ""),
		("IceLab", "DraedonsLogSnowBiome EncryptedSchematicIce", 1f, 1, 1, "one"),
		("PlagueLab", "RedSeekingMechanism", 1f, 1, 1, ""),
		("PlagueLab", "DraedonsLogJungle EncryptedSchematicJungle", 1f, 1, 1, "one"),
		("PlanetoidLab", "GreenSeekingMechanism", 1f, 1, 1, ""),
		("PlanetoidLab", "DraedonsLogPlanetoid EncryptedSchematicPlanetoid PlasmaDriveCore", 1f, 1, 1, "one"),
		// FillPlanetoidLaboratoryChest :673-704: herbs to grow up there (Thorium's planter box when Thorium is loaded).
		("PlanetoidLab", "HerbBag", 1f, 12, 17, ""),
		("PlanetoidLab", "BlinkrootPlanterBox FireBlossomPlanterBox MoonglowPlanterBox ShiverthornPlanterBox WaterleafPlanterBox ThoriumMod/MarineKelpPlanterBox", 1f, 5, 9, ""),
		("PlanetoidLab", "CorruptPlanterBox", 1f, 5, 9, "corruption"),
		("PlanetoidLab", "CrimsonPlanterBox", 1f, 5, 9, "crimson"),
		// UndergroundShrines.cs: each shrine's item, its biome's goods, torches, coins and potions (the getfixedboi fills
		// left out).
		("CorruptionShrine", "CorruptionEffigy CorruptionKey", 1f, 1, 1, ""),
		("CorruptionShrine", "RottenChunk", 1f, 24, 28, ""),
		("CorruptionShrine", "CorruptTorch", 1f, 100, 110, ""),
		("CrimsonShrine", "CrimsonEffigy CrimsonKey", 1f, 1, 1, ""),
		("CrimsonShrine", "Vertebrae", 1f, 24, 28, ""),
		("CrimsonShrine", "CrimsonTorch", 1f, 100, 110, ""),
		("CorruptionShrine CrimsonShrine", "BattlePotion HunterPotion TrapsightPotion", 1f / 3f, 10, 12, ""),
		("DesertShrine", "LuxorsGift DungeonDesertKey", 1f, 1, 1, ""),
		("DesertShrine", "PrismShard", 1f, 6, 8, ""),
		("DesertShrine", "DesertTorch", 1f, 100, 110, ""),
		("DesertShrine MushroomShrine", "ShinePotion MiningPotion BuilderPotion", 1f / 3f, 10, 12, ""),
		("GraniteShrine", "UnstableGraniteCore", 1f, 1, 1, ""),
		("GraniteShrine", "Geode", 1f, 6, 8, ""),
		("GraniteShrine", "BlueTorch", 1f, 100, 110, ""),
		("GraniteShrine", "Granite", 1f, 7, 15, ""),
		("IceShrine", "FrozenCube FrozenKey", 1f, 1, 1, ""),
		("IceShrine", "FlinxFur", 1f, 6, 8, ""),
		("IceShrine", "IceTorch", 1f, 100, 110, ""),
		("IceShrine", "IceCream", 1f, 10, 12, ""),
		// The marble shrine's bar is always Platinum (:620 compares an item id with a tile id).
		("MarbleShrine", "GladiatorsLocket", 1f, 1, 1, ""),
		("MarbleShrine", "PlatinumBar", 1f, 12, 15, ""),
		("MarbleShrine", "WhiteTorch", 1f, 100, 110, ""),
		("MarbleShrine", "Marble", 1f, 7, 15, ""),
		("GraniteShrine MarbleShrine", "EndurancePotion HeartreachPotion LifeforcePotion", 1f / 3f, 10, 12, ""),
		("MushroomShrine", "FungalSymbiote", 1f, 1, 1, ""),
		("MushroomShrine", "TruffleWorm", 1f, 3, 3, ""),
		("MushroomShrine", "MushroomTorch", 1f, 100, 110, ""),
		("CorruptionShrine CrimsonShrine DesertShrine GraniteShrine IceShrine MarbleShrine MushroomShrine AbyssShrine", "GoldCoin", 1f, 8, 10, ""),
		("CorruptionShrine CrimsonShrine DesertShrine GraniteShrine IceShrine MarbleShrine MushroomShrine", "HealingPotion", 1f, 10, 12, ""),
		("SurfaceShrine", "TrinketofChi", 1f, 1, 1, ""),
		("SurfaceShrine", "PinkGel", 1f, 12, 15, ""),
		("SurfaceShrine", "Torch", 1f, 50, 60, ""),
		("SurfaceShrine", "GoldCoin", 1f, 2, 4, ""),
		("SurfaceShrine", "LesserHealingPotion", 1f, 10, 12, ""),
		("SurfaceShrine", "RecallPotion CalmingPotion SwiftnessPotion", 1f / 3f, 10, 12, ""),
		("SurfaceShrine", "Mushroom", 1f, 5, 9, ""),
		("AbyssShrine", "Terminus", 1f, 1, 1, ""),
		("AbyssShrine", "AbyssShocker DepthCrusher InkBomb", 1f / 3f, 1, 1, ""),
		("AbyssShrine", "VoidTorch", 1f, 100, 110, ""),
		("AbyssShrine", "HadalStew", 1f, 10, 12, ""),
		("AbyssShrine", "PotionOfReturn LuckPotionGreater", 0.5f, 10, 12, ""),
		// Abyss.AbyssChest (:948-980): ten items shuffled over the islands, a potion of each of two fours, the rest always.
		("AbyssChests", "TorrentialTear IronBoots DepthCharm Archerfish AnechoicPlating BallOFugu StrangeOrb HerringStaff BlackAnurian Lionfish", 0.1f, 1, 1, "turns"),
		("AbyssChests", "ShinePotion GillsPotion TeleportationPotion LuckPotionLesser ThornsPotion FlipperPotion BattlePotion IronskinPotion", 0.25f, 1, 2, ""),
		("AbyssChests", "HealingPotion", 1f, 1, 2, ""),
		("AbyssChests", "ManaPotion", 1f, 2, 4, ""),
		("AbyssChests", "KelpTorch", 1f, 3, 11, ""),
		("AbyssChests", "GoldCoin", 1f, 2, 4, ""),
		// AstralChestGeneration.cs:24-43 and MiscWorldgenRoutines.cs:44-55 (a Platinum Coin when the coin roll passes 100).
		("Astral", "HeavenfallenStardisk", 1f, 1, 1, ""),
		("Astral", "StarblightSoot", 1f, 30, 80, ""),
		("Astral", "AureusCell", 1f, 10, 14, ""),
		("Astral", "ZergPotion", 1f, 8, 8, ""),
		("Astral", "ZenPotion", 1f, 3, 5, ""),
		("Astral", "FallenStar", 1f, 12, 30, ""),
		("Astral", "PlatinumCoin", 19f / 90f, 1, 1, ""),
		("Astral", "GoldCoin", 1f, 1, 100, ""),
		("Astral", "AstralChest", 1f, 1, 1, "break"),
		// The Sulphurous Sea's four Rusty Chests, each with its own item (SulphurousSea.cs:1122-1240), and the common fill
		// (MiscWorldgenRoutines.cs:57-68; a potion in each of two 1 in 2 rolls of five).
		("Sulphur", "EffigyOfDecay BrokenWaterFilter RustyBeaconPrototype ScionsCurio", 1f, 1, 1, "one"),
		("Sulphur", "Torch", 1f, 15, 29, ""),
		("Sulphur", "SulphurskinPotion", 0.5f, 4, 7, ""),
		("Sulphur", "HadalStew WaterWalkingPotion ShinePotion GillsPotion FlipperPotion", 0.19f, 1, 2, ""),
		("Sulphur", "Flipper", 1f / 3f, 1, 1, ""),
		("Sulphur", "GoldCoin", 1f, 2, 4, ""),
		// The Brimstone Crag bridge's two Ashen Chests (BrimstoneCrag.cs:464-480).
		("Crag", "ObsidianWaterWalkingBoots SlagfireDouser SlurperPole", 1f, 1, 1, "one"),
		("Crag", "HellstoneBar", 1f, 4, 5, ""),
		("Crag", "CoastalDemonfish", 1f, 2, 4, ""),
		("Crag", "HellfireArrow", 1f, 25, 49, ""),
		("Crag", "ObsidianSkinPotion BattlePotion InfernoPotion PotionOfReturn", 0.25f, 1, 2, ""),
		("Crag", "GoldCoin", 1f, 2, 11, ""),
		// Honey Chests of the Giant Hive and the mud planetoids (GiantHive.cs:688-742, MudPlanet.cs:112); the bars are always
		// Tungsten and Platinum (:738-742 compare item ids with tile ids).
		("Honey", "NaturesGift Bezoar FlowerBoots BeeMinecart", 0.25f, 1, 1, ""),
		("Honey", "TungstenBar PlatinumBar", 1f / 3f, 7, 14, ""),
		("Honey", "GoldCoin", 1f / 3f, 3, 4, ""),
		("Honey", "LifeforcePotion RegenerationPotion ManaRegenerationPotion HeartreachPotion PhotosynthesisPotion", 0.1f, 1, 3, ""),
		("Honey", "BottledHoney", 0.5f, 3, 6, ""),
		("Honey", "Stinger", 0.5f, 4, 5, ""),
		("Honey", "JungleSpores", 0.5f, 3, 4, ""),
		("Honey", "RecallPotion", 0.5f, 1, 3, ""),
		("Honey", "JungleTorch", 0.5f, 18, 35, ""),
		// A mud planetoid's Water Chest (MudPlanet.cs:153-193).
		("MudPlanet", "Flipper DivingHelmet Trident", 1f / 3f, 1, 1, ""),
		("MudPlanet", "CopperBar TinBar IronBar LeadBar", 1f / 3f, 7, 14, "ore"),
		("MudPlanet", "GoldCoin", 1f / 3f, 2, 3, ""),
		("MudPlanet", "FlipperPotion RegenerationPotion WaterWalkingPotion NightOwlPotion GravitationPotion EndurancePotion", 1f / 12f, 1, 3, ""),
		("MudPlanet", "HealingPotion", 0.5f, 3, 6, ""),
		("MudPlanet", "Shuriken ThrowingKnife", 0.25f, 50, 99, ""),
		("MudPlanet", "Grenade Bomb", 0.25f, 5, 9, ""),
		("MudPlanet", "RecallPotion", 0.5f, 1, 3, ""),
		("MudPlanet", "Glowstick", 0.5f, 18, 35, ""),
		// The Vernal Pass's five Ivy Chests (VernalPass.cs:51-78): the first holds the Feller of Evergreens, the others one of
		// four.
		("Vernal", "FellerofEvergreens", 1f, 1, 1, "one"),
		("Vernal", "StaffofRegrowth AnkletoftheWind FeralClaws BelladonnaSpiritStaff", 0.25f, 1, 1, "others"),
		("Vernal", "GoldBar PlatinumBar", 0.5f, 4, 6, ""),
		("Vernal", "JungleSpores", 1f, 4, 7, ""),
		("Vernal", "Stinger JungleTorch", 1f, 2, 4, ""),
		("Vernal", "ThornsPotion BattlePotion ShinePotion HunterPotion", 0.25f, 1, 3, ""),
		("Vernal", "GoldCoin", 1f, 1, 2, ""),
		// ShimmerShrine.cs:55-61.
		("ShimmerShrine", "AngelStatue", 1f, 1, 1, ""),
		("ShimmerShrine", "LifeCrystal ManaCrystal", 0.5f, 1, 1, ""),
		("ShimmerShrine", "GoldBar PlatinumBar", 1f, 5, 15, "ore"),
		("ShimmerShrine", "CanOfWorms", 1f, 3, 4, ""),
		("ShimmerShrine", "HealingPotion", 1f, 5, 10, ""),
		("ShimmerShrine", "LuckPotionGreater", 1f, 1, 2, ""),
		("ShimmerShrine", "GoldCoin", 1f, 2, 4, ""),
		// MechanicShed.cs:103-110.
		("MechanicShed", "Toolbox ActuationAccessory", 1f, 1, 1, ""),
		("MechanicShed", "BrickLayer ExtendoGrip PaintSprayer PortableCementMixer", 0.25f, 1, 1, ""),
		("MechanicShed", "BuilderPotion GoldCoin", 1f, 1, 2, ""),
		// The Forsaken Archive's two chests in the Dungeon (DungeonArchive.cs:96-128).
		("Archive", "ShadowKey TallyCounter", 1f, 1, 1, "one"),
		("Archive", "HealingPotion ManaPotion", 1f, 10, 19, "one"),
		("Archive", "SpellTome", 1f, 2, 2, "one"),
		("Archive", "Book", 1f, 12, 24, "one"),
		("Archive", "HunterPotion IronskinPotion ShinePotion SwiftnessPotion", 0.5f, 4, 7, ""),
		("Archive", "GoldCoin", 1f, 5, 9, ""),
		// The sky island of the other evil (WorldEvilIsland.cs:53-100): its chest's weapon; the rest is the underground band's.
		("EvilIsland", "ScourgeoftheCorruptor", 1f, 1, 1, "crimson"),
		("EvilIsland", "VampireKnives", 1f, 1, 1, "corruption")
	};

	internal static IEnumerable<BookItemSource> Sources()
	{
		bool calamity = ModLoader.HasMod("CalamityMod");
		foreach (var (places, items, chance, min, max, note) in Loot)
		{
			string[] notes = note.Split(',', StringSplitOptions.RemoveEmptyEntries);
			if (notes.Contains(calamity ? "!calamity" : "calamity"))
			{
				continue;
			}
			foreach (string id in places.Split(' '))
			{
				Place place = Places[id];
				int chest = Id(place.Chest);
				if ((place.Calamity && !calamity) || chest <= 0)
				{
					continue;
				}
				int[] keys = place.Keys.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Id).Where((int k) => k > 0).ToArray();
				string owner = place.Calamity || notes.Contains("calamity") ? "CalamityMod" : "Terraria";
				foreach (int type in items.Split(' ').Select(Id).Where((int t) => t > 0))
				{
					yield return new BookItemSource(BookAcquisitionKind.Chest, type) { From = chest, FromAny = keys, Where = new[] { id }, Note = string.Join(",", notes.Append(place.Note).Where((string n) => n.Length > 0)), Chance = chance, Min = min, Max = max, Owner = owner };
				}
			}
		}
	}

	// Without a key the item is there once the place opens; behind one it is a route through the key.
	internal static IEnumerable<(int Item, int Step, string Why)> Steps()
	{
		return from s in Sources()
			where s.FromAny.Length == 0 && ThisWorld(s)
			let step = Step(s)
			where step >= 0
			select (s.Item, step, "chest:" + s.Where[0]);
	}

	internal static IEnumerable<(int Item, int[][] Inputs, int Floor, string Why)> Routes()
	{
		return from s in Sources()
			where s.FromAny.Length > 0 && ThisWorld(s)
			let step = Step(s)
			where step >= 0
			select (s.Item, new[] { s.FromAny }, step, "chest:" + s.Where[0]);
	}

	// Title: the place; detail: its boss, key and the row's condition; tooltip: where, how many, what the chance means.
	internal static BookAcquisitionEntry Entry(BookItemSource s)
	{
		if (s.Kind != BookAcquisitionKind.Chest || s.Where.Length != 1 || !Places.TryGetValue(s.Where[0], out Place place))
		{
			return null;
		}
		string[] notes = s.Note.Split(',', StringSplitOptions.RemoveEmptyEntries);
		string boss = BossName(place.Gate), key = s.FromAny.Length > 0 ? Lang.GetItemNameValue(s.FromAny[0]) : "";
		List<string> label = new List<string>();
		List<string> body = new List<string> { L(place.Body, boss, key) };
		if (place.Gate.Length > 0)
		{
			label.Add(L("Common.After", boss));
		}
		if (s.FromAny.Length == 1)
		{
			label.Add(L("HowToGet.With", key));
		}
		foreach (string note in notes)
		{
			if (note.StartsWith("use:", StringComparison.Ordinal))
			{
				label.Add(L("Common.After", BossName(note.Substring(4))));
				body.Add(L("HowToGet.ChestHellLabBody", BossName(note.Substring(4))));
				continue;
			}
			if (note.StartsWith("sky:", StringComparison.Ordinal))
			{
				body.Add(L("SourceRows.SkyHouses", note.Substring(4)));
				continue;
			}
			switch (note)
			{
			case "drunk":
				label.Add(L("WorldLoot.DrunkWorld"));
				break;
			case "break":
				label.Add(L("WorldLoot.BreakTheChest"));
				break;
			case "crimson":
				body.Add(L("Structures.CrimsonWorld"));
				break;
			case "corruption":
				body.Add(L("Structures.CorruptionWorld"));
				break;
			case "eye":
				body.Add(L("SourceRows.InPlaceOfEye", Lang.GetItemNameValue(ItemID.SuspiciousLookingEye)));
				break;
			default:
				string text = NoteText(note);
				if (text.Length > 0)
				{
					body.Add(text);
				}
				break;
			}
		}
		if (s.Chance > 0f && s.Chance < 1f)
		{
			body.Add(L("SourceRows.PerChest"));
		}
		string chance = s.Chance > 0f ? BookAcquisitionRules.Chance(s.Chance) : "";
		string chestName = Lang.GetItemNameValue(s.From);
		// The key the chest opens with is a link on the row (RecipeBookSystem.Links).
		BookMention[] mentions = (s.FromAny.Length == 1) ? new[] { new BookMention(key, s.FromAny[0]) } : Array.Empty<BookMention>();
		return new BookAcquisitionEntry(L(place.Title, chestName), string.Join("\n", body), s.Kind, chance, "×" + BookAcquisitionRules.Stack(s.Min, s.Max), s.From, 0, label.Count > 0, -1, s.Owner, string.Join(" · ", label), OtherMode: !ThisWorld(s), SourceItem: s.From, Mentions: mentions);
	}

	// Dev audit for the test stand: names of the table that resolve to no item (a typo, or a mod not loaded).
	internal static IEnumerable<string> Unresolved()
	{
		return Loot.SelectMany(r => r.Items.Split(' ')).Concat(Places.Values.Select((Place p) => p.Chest)).Concat(Places.Values.SelectMany((Place p) => p.Keys.Split(' ', StringSplitOptions.RemoveEmptyEntries))).Distinct().Where((string n) => Id(n) <= 0);
	}

	private static string NoteText(string note)
	{
		return note switch
		{
			"ore" => L("SourceRows.WorldOre"),
			"one" => L("SourceRows.OneChest"),
			"others" => L("SourceRows.OtherChests"),
			"ug" => L("SourceRows.AboveCaverns"),
			"cav" => L("SourceRows.InCaverns"),
			"lava" => L("SourceRows.BelowLava"),
			"gold" => L("SourceRows.GoldOnly"),
			"set" => L("SourceRows.WholeSet"),
			"turns" => L("SourceRows.InTurn"),
			"upper" => L("SourceRows.UpperDesert"),
			"lower" => L("SourceRows.LowerDesert"),
			"eyekept" => L("SourceRows.EyeKept"),
			_ => ""
		};
	}

	// The place's boss and the row's own one ("use:"); -1 when this world never gets there.
	private static int Step(BookItemSource s)
	{
		int step = BookSteps.Resolve(Places[s.Where[0]].Gate);
		foreach (string note in s.Note.Split(','))
		{
			if (note.StartsWith("use:", StringComparison.Ordinal))
			{
				int use = BookSteps.Resolve(note.Substring(4));
				step = use < 0 || step < 0 ? -1 : Math.Max(step, use);
			}
		}
		return step;
	}

	// Whether this world has the row: its evil, its ore of the pair, a drunk world's loot.
	private static bool ThisWorld(BookItemSource s)
	{
		foreach (string note in s.Note.Split(','))
		{
			bool here = note switch
			{
				"crimson" => WorldGen.crimson || Main.drunkWorld,
				"corruption" => !WorldGen.crimson || Main.drunkWorld,
				"drunk" => Main.drunkWorld,
				"ore" => s.Item switch
				{
					ItemID.CopperBar => WorldGen.SavedOreTiers.Copper == TileID.Copper,
					ItemID.TinBar => WorldGen.SavedOreTiers.Copper == TileID.Tin,
					ItemID.IronBar => WorldGen.SavedOreTiers.Iron == TileID.Iron,
					ItemID.LeadBar => WorldGen.SavedOreTiers.Iron == TileID.Lead,
					ItemID.SilverBar or ItemID.SilverBullet => WorldGen.SavedOreTiers.Silver == TileID.Silver,
					ItemID.TungstenBar or ItemID.TungstenBullet => WorldGen.SavedOreTiers.Silver == TileID.Tungsten,
					ItemID.GoldBar => WorldGen.SavedOreTiers.Gold == TileID.Gold,
					ItemID.PlatinumBar => WorldGen.SavedOreTiers.Gold == TileID.Platinum,
					_ => true
				},
				_ => true
			};
			if (!here)
			{
				return false;
			}
		}
		return true;
	}

	// "SkeletronHead" or "CalamityMod/AstrumAureus" as the game names it.
	private static string BossName(string term)
	{
		if (term.Length == 0)
		{
			return "";
		}
		int type = term.Contains('/') ? (ModContent.TryFind(term, out ModNPC npc) ? npc.Type : 0) : (NPCID.Search.TryGetId(term, out int id) ? id : 0);
		return type > 0 ? Lang.GetNPCNameValue(type) : term;
	}

	// A vanilla item by its ItemID name, else Calamity's by its class name, or "Mod/Name".
	private static int Id(string name)
	{
		if (name.Contains('/'))
		{
			return ModContent.TryFind(name, out ModItem item) ? item.Type : 0;
		}
		return ItemID.Search.TryGetId(name, out int id) ? id : (ModContent.TryFind("CalamityMod", name, out ModItem calamity) ? calamity.Type : 0);
	}
}
