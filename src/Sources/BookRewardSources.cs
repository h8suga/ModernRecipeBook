using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;

namespace ModernRecipeBook.Sources;

// Items the game hands out in code (a BookCurated area): the Angler's quest rewards, the Travelling Merchant's stock that
// tModLoader's shop entries leave out, items that turn into others when right-clicked, shimmered or worn, and one-off
// gifts and events. Also the placeholders no player can ever hold, which the catalogue hides. Every table cites the
// code it was read from (tModLoader 2025.7, Calamity 2.2.4, Magic Storage 0.7.0.11).
internal static class BookRewardSources
{
	// Angler --------------------------------------------------------------------------------------------------

	// Player.GetAnglerReward_MainReward's fixed rewards by quest count (Player.anglerQuestsFinished, which Main counts
	// up before the reward, so 5 is the fifth quest).
	private static readonly (int Count, int Item)[] AnglerMilestones = { (5, ItemID.FuzzyCarrot), (10, ItemID.AnglerHat), (15, ItemID.AnglerVest), (20, ItemID.AnglerPants), (25, ItemID.BottomlessBucket), (30, ItemID.GoldenFishingRod) };

	// Calamity answers these counts before the game does (ILChanges.AddMoreGuaranteedAnglerRewards). Lacking: only to a
	// player with none of it in inventory, equipment or banks (Player.DropAnglerAccByMissing), anyone else gets the
	// game's reward; at 10 the Vest and Pants come on top of the game's Angler Hat.
	private static readonly (int Count, int Item, bool Lacking)[] CalamityAnglerMilestones = { (3, ItemID.HighTestFishingLine, true), (6, ItemID.FishermansGuide, true), (10, ItemID.AnglerVest, false), (10, ItemID.AnglerPants, false), (11, ItemID.WeatherRadio, true), (14, ItemID.Sextant, true), (15, ItemID.TackleBox, true), (20, ItemID.AnglerEarring, true), (26, ItemID.Sundial, false), (28, ItemID.GoldenBugNet, false) };

	// The random main reward of any other quest, rolled in this order until one wins: 1 in (int)(Base * the count's
	// Player.GetAnglerRewardRarityMultiplier), only after After quests, Hardmode ones only in hardmode. The Seashell
	// Hairpin and the Fish Costume Mask bring the rest of their sets.
	private static readonly (int Item, int Base, int After, bool Hardmode)[] AnglerRolls = { (ItemID.GoldenFishingRod, 250, 75, false), (ItemID.HotlineFishingHook, 100, 25, true), (ItemID.FinWings, 70, 10, true), (ItemID.BottomlessBucket, 70, 10, false), (ItemID.SuperAbsorbantSponge, 70, 10, false), (ItemID.GoldenBugNet, 80, 0, false), (ItemID.FishHook, 60, 0, false), (ItemID.FishMinecart, 60, 0, false), (ItemID.SeashellHairpin, 80, 0, false), (ItemID.FishCostumeMask, 80, 0, false) };

	private static readonly Dictionary<int, int[]> AnglerSets = new Dictionary<int, int[]>
	{
		[ItemID.SeashellHairpin] = new int[] { ItemID.MermaidAdornment, ItemID.MermaidTail },
		[ItemID.FishCostumeMask] = new int[] { ItemID.FishCostumeShirt, ItemID.FishCostumeFinskirt }
	};

	// When no roll wins: 0.8 of the time one of these accessories, picked among those the player lacks (all seven when
	// none is missing), else one of the potions, 2 to 5. A Bumblebee Tuna quest first gives honey gear: half the time
	// before hardmode, always in hardmode, either piece alike.
	private static readonly int[] AnglerTackle = { ItemID.HighTestFishingLine, ItemID.AnglerEarring, ItemID.TackleBox, ItemID.FishermansGuide, ItemID.WeatherRadio, ItemID.Sextant, ItemID.FishingBobber };

	private static readonly int[] AnglerPotions = { ItemID.FishingPotion, ItemID.SonarPotion, ItemID.CratePotion };

	private static readonly int[] AnglerHoney = { ItemID.BottomlessHoneyBucket, ItemID.HoneyAbsorbantSponge };

	// Player.GetAnglerReward_Decoration: on top of the reward, one of these at a chance growing from 1 - multiplier to
	// certain at 100 quests.
	private static readonly int[] AnglerDecorations = { ItemID.LifePreserver, ItemID.ShipsWheel, ItemID.CompassRose, ItemID.WallAnchor, ItemID.PillaginMePixels, ItemID.TreasureMap, ItemID.GoldfishTrophy, ItemID.BunnyfishTrophy, ItemID.SwordfishTrophy, ItemID.SharkteethTrophy, ItemID.ShipInABottle, ItemID.SeaweedPlanter, ItemID.NotSoLostInParadise, ItemID.Crustography, ItemID.Fangs, ItemID.WhatLurksBelow, ItemID.CouchGag, ItemID.SilentFish, ItemID.TheDuke };

	// Player.GetAnglerReward_Bait: bait, 1 to 7, when Next((int)(100 * multiplier)) is at most 50; Master 1 in 15,
	// Journeyman 1 in 5, else Apprentice. Calamity replaces it (ILChanges.ImproveAnglerBaitReward): always, 2 to 7,
	// Grand Marquis Bait 1 in 15, Master 1 in 10, Journeyman 1 in 5, else Apprentice.
	private static readonly int[] AnglerBait = { ItemID.MasterBait, ItemID.JourneymanBait, ItemID.ApprenticeBait };

	// Travelling Merchant -------------------------------------------------------------------------------------

	// Chest.SetupTravelShop_AddToShop adds these along with the item the shop rolled (Dynasty Wood's shingles, the rest
	// of a vanity set; the White Team Block slot becomes one of six colours with its platform), and
	// SetupTravelShop_GetPainting fills a painting slot; tModLoader's TravellingMerchantShop info entries list neither.
	// Conditions as the painting rolls check them (the Moon Lord and Martian ones also check hardmode, which they imply).
	private static readonly (int Item, Condition Condition)[] TravellingStock =
	{
		(ItemID.RedDynastyShingles, null), (ItemID.BlueDynastyShingles, null), (ItemID.ChefShirt, null), (ItemID.ChefPants, null), (ItemID.GameMasterPants, null), (ItemID.StarPrincessDress, null), (ItemID.CelestialWand, null), (ItemID.LincolnsHoodie, null), (ItemID.LincolnsPants, null), (ItemID.PrettyPinkDressSkirt, null), (ItemID.PrettyPinkDressPants, null),
		(ItemID.TeamBlockWhitePlatform, null), (ItemID.TeamBlockRed, null), (ItemID.TeamBlockRedPlatform, null), (ItemID.TeamBlockGreen, null), (ItemID.TeamBlockGreenPlatform, null), (ItemID.TeamBlockBlue, null), (ItemID.TeamBlockBluePlatform, null), (ItemID.TeamBlockYellow, null), (ItemID.TeamBlockYellowPlatform, null), (ItemID.TeamBlockPink, null), (ItemID.TeamBlockPinkPlatform, null),
		(ItemID.PaintingWilson, null), (ItemID.PaintingWillow, null), (ItemID.PaintingWendy, null), (ItemID.PaintingWolfgang, null), (ItemID.KargohsSummon, null), (ItemID.BennyWarhol, null), (ItemID.Duality, null), (ItemID.HoplitePizza, null), (ItemID.ParsecPals, null), (ItemID.DoNotEattheVileMushroom, null), (ItemID.SunshineofIsrapony, null), (ItemID.YuumaTheBlueTiger, null),
		(ItemID.MoonLordPainting, Condition.DownedMoonLord), (ItemID.MoonmanandCompany, Condition.DownedMoonLord), (ItemID.PaintingCastleMarsberg, Condition.DownedMartians), (ItemID.PaintingMartiaLisa, Condition.DownedMartians), (ItemID.PaintingTheTruthIsUpThere, Condition.DownedMartians),
		(ItemID.PaintingAcorns, Condition.DownedFrostLegion), (ItemID.PaintingColdSnap, Condition.DownedFrostLegion), (ItemID.PaintingCursedSaint, Condition.DownedFrostLegion), (ItemID.PaintingSnowfellas, Condition.DownedFrostLegion), (ItemID.PaintingTheSeason, Condition.DownedFrostLegion)
	};

	// Items that turn into others ---------------------------------------------------------------------------

	// Right-click in the inventory (ItemSlot.TryItemSwap) or while held (Player.ItemCheck's alternate use): the Shellphone
	// cycles Home, Ocean, Underworld, Spawn (a crafted one is the Dummy, which becomes Home, Item.OnCreated). Also the
	// Minecart Upgrade Kit gives a Mechanical Cart when used (Player.ItemCheck_UseMinecartPowerUp), and a Gas Trap kept in
	// a chest goes off when the chest is opened and stays behind used (Projectile.UseGasTrapInChest).
	private static readonly (int From, int To)[] Swaps =
	{
		(ItemID.VoidLens, ItemID.ClosedVoidBag), (ItemID.ClosedVoidBag, ItemID.VoidLens), (ItemID.CapricornLegs, ItemID.CapricornTail), (ItemID.CapricornTail, ItemID.CapricornLegs),
		(ItemID.RubblemakerSmall, ItemID.RubblemakerMedium), (ItemID.RubblemakerMedium, ItemID.RubblemakerLarge), (ItemID.RubblemakerLarge, ItemID.RubblemakerSmall),
		(ItemID.EncumberingStone, ItemID.UncumberingStone), (ItemID.UncumberingStone, ItemID.EncumberingStone),
		(ItemID.DontHurtComboBook, ItemID.DontHurtComboBookInactive), (ItemID.DontHurtComboBookInactive, ItemID.DontHurtComboBook), (ItemID.DontHurtCrittersBook, ItemID.DontHurtCrittersBookInactive), (ItemID.DontHurtCrittersBookInactive, ItemID.DontHurtCrittersBook), (ItemID.DontHurtNatureBook, ItemID.DontHurtNatureBookInactive), (ItemID.DontHurtNatureBookInactive, ItemID.DontHurtNatureBook),
		(ItemID.ShellphoneDummy, ItemID.Shellphone), (ItemID.Shellphone, ItemID.ShellphoneOcean), (ItemID.ShellphoneOcean, ItemID.ShellphoneHell), (ItemID.ShellphoneHell, ItemID.ShellphoneSpawn), (ItemID.ShellphoneSpawn, ItemID.Shellphone),
		(ItemID.MinecartPowerup, ItemID.MinecartMech), (ItemID.GasTrap, ItemID.UsedGasTrap)
	};

	// Item.GetShimmered's own cases, ahead of ItemID.Sets.ShimmerTransformToItem: these only once Moon Lord is down.
	private static readonly (int From, int To)[] MoonLordShimmers = { (ItemID.RodofDiscord, ItemID.RodOfHarmony), (ItemID.Clentaminator, ItemID.Clentaminator2), (ItemID.BottomlessBucket, ItemID.BottomlessShimmerBucket), (ItemID.BottomlessShimmerBucket, ItemID.BottomlessBucket) };

	// Player.ApplyWilsonBeard: equipped as an accessory, the beard grows after 43200 ticks, 1 in 540 each tick after.
	private static readonly (int From, int To)[] Beards = { (ItemID.WilsonBeardShort, ItemID.WilsonBeardLong), (ItemID.WilsonBeardLong, ItemID.WilsonBeardMagnificent) };

	// NPC.AI_065_Butterflies: a butterfly rolls Next(100) once for its kind (its catch item, 1994 + kind); the rest, 25,
	// are Monarchs, which the bestiary already gives.
	private static readonly (int Item, int Share)[] Butterflies = { (ItemID.TreeNymphButterfly, 1), (ItemID.PurpleEmperorButterfly, 2), (ItemID.RedAdmiralButterfly, 6), (ItemID.JuliaButterfly, 10), (ItemID.UlyssesButterfly, 15), (ItemID.ZebraSwallowtailButterfly, 19), (ItemID.SulphurButterfly, 22) };

	// Player.ExtractinatorUse: Silt and Slush give an Amber Mosquito when the coin roll (1 in 2) fails and then 1 in 5000;
	// a Desert Fossil when the Sturdy Fossil roll (1 in 10) and the coins fail, then 1 in 1666. A glowing moss in a
	// Chlorophyte Extractinator (tile 642) gives one of the five glowing mosses 1 time in 10.
	private static readonly (int From, float Chance)[] AmberMosquito = { (ItemID.SiltBlock, 0.5f / 5000f), (ItemID.SlushBlock, 0.5f / 5000f), (ItemID.DesertFossil, 0.9f * 0.5f / (5000 / 3)) };

	private static readonly int[] GlowingMosses = { ItemID.LavaMoss, ItemID.ArgonMoss, ItemID.KryptonMoss, ItemID.VioletMoss, ItemID.XenonMoss };

	// Magic Storage: the Storage Extractor used on a Storage Unit takes its items out as that tier's core
	// (TEStorageUnit.RemoveItemsAndSpawnCore) and leaves an empty unit, which drops as the Empty Storage Unit
	// (StorageUnit.ItemType of TierEmpty). Core, unit.
	private static readonly (string Core, string Unit)[] StorageCores = { ("StorageCore", "StorageUnit"), ("StorageCoreDemonite", "StorageUnitDemonite"), ("StorageCoreCrimtane", "StorageUnitCrimtane"), ("StorageCoreHellstone", "StorageUnitHellstone"), ("StorageCoreHallowed", "StorageUnitHallowed"), ("StorageCoreBlueChlorophyte", "StorageUnitBlueChlorophyte"), ("StorageCoreLuminite", "StorageUnitLuminite"), ("StorageCoreTerra", "StorageUnitTerra") };

	// Placeholders ---------------------------------------------------------------------------------------------

	// Vanilla items nothing creates: the coloured presents of old (openable, but no code or rule makes one) and the
	// Lunatic Cultist's bag (no NPC rule drops it, Calamity's neither).
	private static readonly HashSet<int> NeverCreated = new HashSet<int> { ItemID.BluePresent, ItemID.GreenPresent, ItemID.YellowPresent, ItemID.CultistBossBag };

	// Magic Storage's own unobtainable items: the Tiny Storage Unit has no recipe and no tier upgrades into it
	// (TierTiny), the Creative Storage Unit no recipe; its config, error and shimmer icons are items in the
	// MagicStorage.Items.*Display namespaces.
	private static readonly HashSet<string> HiddenModItems = new HashSet<string>(StringComparer.Ordinal) { "MagicStorage/StorageUnitTiny", "MagicStorage/CreativeStorageUnit" };

	private static HashSet<int> unclaimedBanners;

	internal static IEnumerable<BookItemSource> Sources()
	{
		foreach (BookItemSource source in AnglerSources())
		{
			yield return source;
		}
		AbstractNPCShop travelling = NPCShopDatabase.AllShops.OfType<TravellingMerchantShop>().FirstOrDefault();
		if (travelling != null)
		{
			foreach (var (item, condition) in TravellingStock)
			{
				Condition[] conditions = condition == null ? Array.Empty<Condition>() : new[] { condition };
				yield return new BookItemSource(BookAcquisitionKind.Shop, item) { Npc = NPCID.TravellingMerchant, Shop = travelling, Entry = new NPCShop.Entry(ContentSamples.ItemsByType[item], conditions) };
			}
		}
		foreach (var (from, to) in Swaps)
		{
			yield return new BookItemSource(BookAcquisitionKind.Use, to) { From = from };
		}
		foreach (var (from, to) in MoonLordShimmers)
		{
			yield return new BookItemSource(BookAcquisitionKind.Event, to) { From = from, Note = "shimmer" };
		}
		foreach (var (from, to) in Beards)
		{
			yield return new BookItemSource(BookAcquisitionKind.Event, to) { From = from, Note = "beard" };
		}
		foreach (var (item, share) in Butterflies)
		{
			yield return new BookItemSource(BookAcquisitionKind.Event, item) { Npc = NPCID.Butterfly, Chance = share / 100f, Note = "butterfly" };
		}
		foreach (var (from, chance) in AmberMosquito)
		{
			yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.AmberMosquito) { From = from, FromAny = new int[] { ItemID.Extractinator, ItemID.ChlorophyteExtractinator }, Chance = chance, Note = "extractinator" };
		}
		foreach (int moss in GlowingMosses)
		{
			yield return new BookItemSource(BookAcquisitionKind.Event, moss) { From = ItemID.ChlorophyteExtractinator, FromAny = GlowingMosses.Append(ItemID.RainbowMoss).Where((int m) => m != moss).ToArray(), Chance = 0.1f / GlowingMosses.Length, Note = "mosses" };
		}
		// Player.TorchAttack: over 100 torches nearby below the surface start the Torch God's event (Player.UpdateTorchLuck_ConsumeCountersAndCalculate,
		// not while the favor is carried or once biome torches are unlocked); 95 attacks survived drop the favor.
		yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.TorchGodsFavor) { From = ItemID.Torch, Note = "torchgod" };
		// NPC.DoDeathEvents: the Old One's Army enemies drop Etherian Mana while DD2Event.ShouldDropCrystals (a set
		// number each wave).
		yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.DD2EnergyCrystal) { From = ItemID.DD2ElderCrystal, Note = "oldonesarmy" };
		// BirthdayParty: a genuine party (5 town NPCs or more) sets NPC.freeCake; the first player to talk to the Party
		// Girl then gets a slice (Player.Update).
		yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.SliceOfCake) { Npc = NPCID.PartyGirl, Note = "party" };
		// UICharacterCreation: a new character gets a Carrot where the Collector's Edition is installed
		// (Main.runningCollectorsEdition, Main.CheckBunny); tModLoader packs starting items beyond 50 in a Starting Bag
		// (PlayerLoader.SetStartInventory).
		yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.Carrot) { From = ItemID.Carrot, Note = "collectors" };
		yield return new BookItemSource(BookAcquisitionKind.Event, ModContent.ItemType<StartBag>()) { From = ModContent.ItemType<StartBag>(), Owner = "ModLoader", Note = "startbag" };
		// NPC.AI_003_Fighters: by day where the wind reaches, a Gnome turns into a Garden Gnome statue, which breaks into
		// the item (WorldGen.CheckGnome). Gnomes spawn from the start.
		yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.GardenGnome) { Npc = NPCID.Gnome, Note = "gnome" };
		// NPC.DoDeathEvents_DropBossPotionsAndHearts: the Eye of Cthulhu and the Wall of Flesh killed on the same day; the
		// second drops the hat.
		yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.BadgersHat) { Npc = NPCID.WallofFlesh, Note = "badger" };
		// WorldGen.TryGrowingAbigailsFlower: now and then on grass beside a tombstone (tile 85).
		yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.AbigailsFlower) { Tile = TileID.AbigailsFlower, Note = "tombstone" };
		// WorldGen.Check2x2Style: a fully grown pumpkin broken during Halloween, 1 in 200.
		yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.MagicalPumpkinSeed) { Tile = TileID.Pumpkins, FrameX = 144, Chance = 1f / 200f, Note = "pumpkin" };
		// Main.CanDryadPlayStardewAnimation: holding Joja Cola, ask the Dryad about the world; she takes it and the
		// Junimos leave a Stardrop (Projectile.AI_192_JuminoAnimation).
		yield return new BookItemSource(BookAcquisitionKind.Trade, ItemID.JunimoPetItem) { Npc = NPCID.Dryad, FromAny = new int[] { ItemID.JojaCola } };
		if (ModContent.TryFind("MagicStorage", "StorageExtractor", out ModItem extractor))
		{
			foreach (var (core, unit) in StorageCores)
			{
				if (ModContent.TryFind("MagicStorage", core, out ModItem coreItem) && ModContent.TryFind("MagicStorage", unit, out ModItem unitItem))
				{
					yield return new BookItemSource(BookAcquisitionKind.Event, coreItem.Type) { From = unitItem.Type, FromAny = new int[] { extractor.Type }, Owner = "MagicStorage", Note = "storagecore" };
				}
			}
			if (ModContent.TryFind("MagicStorage", "StorageUnitEmpty", out ModItem empty) && ModContent.TryFind("MagicStorage", "StorageUnit", out ModItem basic))
			{
				yield return new BookItemSource(BookAcquisitionKind.Event, empty.Type) { From = basic.Type, FromAny = new int[] { extractor.Type }, Owner = "MagicStorage", Note = "emptyunit" };
			}
		}
	}

	// The quest rewards as sources: Note the row ("at", "atifnone", "roll", "rollhardmode", "tackle", "potion", "honey",
	// "decoration", "bait"), NoteValue the quest count it names, Chance the chance on the first quest it can come.
	private static IEnumerable<BookItemSource> AnglerSources()
	{
		bool calamity = ModLoader.HasMod("CalamityMod");
		BookItemSource Quest(int item, string row, int count, float chance, string owner = "Terraria", int min = 1, int max = 1)
		{
			return new BookItemSource(BookAcquisitionKind.Quest, item) { Npc = NPCID.Angler, Note = row, NoteValue = count, Chance = chance, Owner = owner, Min = min, Max = max };
		}
		foreach (var (count, item) in AnglerMilestones)
		{
			// Calamity's accessory at 15 and 20 leaves the game's reward to those who have it; the Vest and Pants come at 10.
			if (!calamity || !CalamityAnglerMilestones.Any(c => c.Count == count && c.Lacking))
			{
				yield return Quest(item, "at", count, 1f);
			}
		}
		if (calamity)
		{
			foreach (var (count, item, lacking) in CalamityAnglerMilestones)
			{
				yield return Quest(item, lacking ? "atifnone" : "at", count, 1f, "CalamityMod");
			}
		}
		foreach (var (item, _, after, hardmode) in AnglerRolls)
		{
			string row = hardmode ? "rollhardmode" : "roll";
			float chance = AnglerChance(row, item, FirstRolled(after), hardmode);
			yield return Quest(item, row, after, chance);
			foreach (int piece in AnglerSets.GetValueOrDefault(item) ?? Array.Empty<int>())
			{
				yield return Quest(piece, row, after, chance) with { From = item };
			}
		}
		foreach (int item in AnglerTackle)
		{
			yield return Quest(item, "tackle", 0, AnglerChance("tackle", item, 1, false));
		}
		foreach (int item in AnglerPotions)
		{
			yield return Quest(item, "potion", 0, AnglerChance("potion", item, 1, false), min: 2, max: 5);
		}
		foreach (int item in AnglerHoney)
		{
			yield return Quest(item, "honey", 0, 0.25f);
		}
		foreach (int item in AnglerDecorations)
		{
			yield return Quest(item, "decoration", 0, AnglerChance("decoration", item, 1, false));
		}
		IEnumerable<int> bait = calamity && ModContent.TryFind("CalamityMod", "GrandMarquisBait", out ModItem marquis) ? AnglerBait.Prepend(marquis.Type) : AnglerBait;
		foreach (int item in bait)
		{
			yield return Quest(item, "bait", 0, AnglerChance("bait", item, 1, false), ContentSamples.ItemsByType[item].ModItem?.Mod.Name ?? (calamity ? "CalamityMod" : "Terraria"), calamity ? 2 : 1, 7);
		}
	}

	// The first quest count after After whose main reward is rolled, not fixed.
	private static int FirstRolled(int after)
	{
		bool calamity = ModLoader.HasMod("CalamityMod");
		int count = after + 1;
		while (AnglerMilestones.Any(m => m.Count == count) || (calamity && CalamityAnglerMilestones.Any(c => c.Count == count && !c.Lacking)))
		{
			count++;
		}
		return count;
	}

	// The chance a quest finished as the count-th gives the item, as Player.GetAnglerReward rolls it with the Angler's
	// prices at their base (a happy Angler lowers the multiplier, which raises every chance).
	private static float AnglerChance(string row, int item, int count, bool hardmode)
	{
		bool calamity = ModLoader.HasMod("CalamityMod");
		float multiplier = Player.GetAnglerRewardRarityMultiplier(count);
		float OneIn(float n) => 1f / Math.Max(1, (int)(n * multiplier));
		if (row == "decoration")
		{
			return MathHelper.Lerp(1f - multiplier, 1f, Math.Min(1f, count / 100f)) / AnglerDecorations.Length;
		}
		if (row == "bait")
		{
			float given = calamity ? 1f : Math.Min(1f, 51f / Math.Max(1, (int)(100f * multiplier)));
			if (calamity)
			{
				float marquis = OneIn(15f);
				if (ContentSamples.ItemsByType[item].ModItem != null)
				{
					return marquis;
				}
				given *= 1f - marquis;
			}
			float master = OneIn(calamity ? 10f : 15f), journeyman = OneIn(5f);
			return given * (item == ItemID.MasterBait ? master : (1f - master) * (item == ItemID.JourneymanBait ? journeyman : 1f - journeyman));
		}
		float left = 1f;
		foreach (var (roll, n, after, needsHardmode) in AnglerRolls)
		{
			if (count <= after || (needsHardmode && !hardmode))
			{
				continue;
			}
			float p = OneIn(n);
			if (roll == item && row.StartsWith("roll", StringComparison.Ordinal))
			{
				return left * p;
			}
			left *= 1f - p;
		}
		float none = (float)(Math.Pow(1f - OneIn(40f), 3) * Math.Pow(1f - OneIn(30f), 3)) * (1f - OneIn(25f));
		float tackle = (1f - none) * 0.8f;
		return row == "tackle" ? left * tackle / AnglerTackle.Length : left * (1f - tackle) / AnglerPotions.Length;
	}

	// An item available from that step on, for the progression. A source of its own kind follows its NPC or gate; the
	// conversions are routes (Routes).
	internal static IEnumerable<(int Item, int Step, string Why)> Steps()
	{
		foreach (BookItemSource s in Sources())
		{
			switch (s.Kind)
			{
			case BookAcquisitionKind.Quest:
				yield return (s.Item, Math.Max(BookProgressionSources.TownNpcStep(NPCID.Angler), s.Note == "rollhardmode" ? BookSteps.HardmodeStep : 0), "quest:Angler");
				break;
			case BookAcquisitionKind.Shop:
			{
				int step = BookProgressionSources.TownNpcStep(s.Npc);
				foreach (Condition condition in s.Entry.Conditions)
				{
					step = Math.Max(step, BookProgressionSources.ConditionStep(condition) ?? 0);
				}
				yield return (s.Item, step, "shop:TravellingMerchant");
				break;
			}
			case BookAcquisitionKind.Event:
				int? at = s.Note switch
				{
					"butterfly" or "torchgod" or "collectors" or "startbag" or "gnome" or "tombstone" or "pumpkin" => 0,
					"oldonesarmy" => BookProgressionSources.TownNpcStep(NPCID.DD2Bartender),
					"party" => BookProgressionSources.TownNpcStep(NPCID.PartyGirl),
					"badger" => BookSteps.HardmodeStep,
					_ => null
				};
				if (at is int value)
				{
					yield return (s.Item, value, "event:" + s.Note);
				}
				break;
			}
		}
	}

	// An item made from others: what turns into it, the machine or tool it needs, never before Floor.
	internal static IEnumerable<(int Item, int[][] Inputs, int Floor, string Why)> Routes()
	{
		foreach (BookItemSource s in Sources())
		{
			string why = (s.Kind == BookAcquisitionKind.Use ? "use" : s.Note) + ":" + (ContentSamples.ItemsByType.TryGetValue(s.From, out Item from) ? from.ModItem?.Name ?? ItemID.Search.GetName(s.From) : "");
			if (s.Kind == BookAcquisitionKind.Use || (s.Kind == BookAcquisitionKind.Event && s.Note is "beard" or "shimmer"))
			{
				yield return (s.Item, new[] { new[] { s.From } }, s.Note == "shimmer" ? BookSteps.MoonLordStep : 0, why);
			}
			else if (s.Kind == BookAcquisitionKind.Event && s.Note is "extractinator" or "mosses" or "storagecore" or "emptyunit")
			{
				// The input and the machine or tool it goes through.
				yield return (s.Item, new[] { new[] { s.From }, s.FromAny }, 0, why);
			}
			else if (s.Kind == BookAcquisitionKind.Trade)
			{
				yield return (s.Item, new[] { s.FromAny }, BookProgressionSources.TownNpcStep(s.Npc), "trade:Dryad");
			}
		}
	}

	internal static BookAcquisitionEntry Entry(BookItemSource s)
	{
		if (s.Kind == BookAcquisitionKind.Quest)
		{
			return QuestEntry(s);
		}
		if (s.Kind != BookAcquisitionKind.Event)
		{
			return null;
		}
		string chance = s.Chance > 0f ? BookAcquisitionRules.Chance(s.Chance) : "";
		string Npc(int type) => Lang.GetNPCNameValue(type);
		return s.Note switch
		{
			"shimmer" => Row(Name(s.From), "", BookAcquisitionKind.Shimmer, L("HowToGet.AfterMoonLord"), item: s.From),
			"beard" => Row(Name(s.From), L("Rewards.BeardBody"), BookAcquisitionKind.Use, L("Rewards.WhileEquipped"), item: s.From),
			"butterfly" => Row(Npc(s.Npc), "", BookAcquisitionKind.Catch, "", chance, npc: s.Npc),
			"extractinator" => Row(Name(s.From), L("Rewards.ExtractinatorBody"), BookAcquisitionKind.Use, L("Rewards.InAnExtractinator"), chance, item: s.From),
			"mosses" => Row(Name(s.From), L("Rewards.GlowingMossBody", string.Join(", ", s.FromAny.Select(Name))), BookAcquisitionKind.Use, L("Rewards.WithAnotherGlowingMoss"), chance, item: s.From),
			"torchgod" => Row(L("Rewards.TorchGod"), L("Rewards.TorchGodBody"), BookAcquisitionKind.Event, L("Rewards.TorchGodLabel"), item: s.From),
			"oldonesarmy" => Row(Language.GetTextValue("Bestiary_Invasions.OldOnesArmy"), L("Rewards.OldOnesArmyBody"), BookAcquisitionKind.Event, L("Rewards.DroppedByItsEnemies"), item: s.From),
			"party" => Row(Npc(s.Npc), L("Rewards.PartyBody"), BookAcquisitionKind.Event, L("Rewards.DuringAParty"), npc: s.Npc),
			"collectors" => Row(L("Rewards.NewCharacter"), L("Rewards.CollectorsEditionBody"), BookAcquisitionKind.Event, L("Rewards.CollectorsEdition"), item: s.From),
			"startbag" => Row(L("Rewards.NewCharacter"), L("Rewards.StartingBagBody"), BookAcquisitionKind.Event, L("Rewards.MoreThan50StartingItems"), item: s.From),
			"gnome" => Row(Npc(s.Npc), L("Rewards.GnomeBody"), BookAcquisitionKind.Event, L("Rewards.TurnsToStoneByDay"), npc: s.Npc),
			"badger" => Row(L("Rewards.EyeAndWall", Npc(NPCID.EyeofCthulhu), Npc(NPCID.WallofFlesh)), L("Rewards.SameDayBody"), BookAcquisitionKind.Event, L("Rewards.OnTheSameDay"), npc: s.Npc),
			"tombstone" => Row(L("Rewards.NextToTombstones"), L("Rewards.TombstoneBody"), BookAcquisitionKind.Plant, L("HowToGet.OnGrass"), tile: s.Tile),
			"pumpkin" => Row(L("Rewards.GrownPumpkin"), L("Rewards.PumpkinBody"), BookAcquisitionKind.Plant, L("HowToGet.Halloween"), chance, tile: s.Tile),
			"storagecore" => Row(Name(s.From), L("Rewards.StorageCoreBody"), BookAcquisitionKind.Use, L("HowToGet.With", Name(s.FromAny[0])), item: s.From),
			"emptyunit" => Row(Name(s.From), L("Rewards.EmptyUnitBody"), BookAcquisitionKind.Use, L("HowToGet.With", Name(s.FromAny[0])), item: s.From),
			_ => null
		};
		BookAcquisitionEntry Row(string name, string body, BookAcquisitionKind kind, string label, string rate = "", int item = 0, int npc = 0, int tile = -1)
		{
			return new BookAcquisitionEntry(name, body, kind, rate, "×1", item, npc, label.Length > 0, -1, s.Owner, label, "", tile, s.FrameX, s.FrameY);
		}
	}

	// The Angler's row: the quest count or roll it comes from, its chance on the first quest that can give it, and in the
	// tooltip how that chance grows (Player.GetAnglerRewardRarityMultiplier bottoms out after 150 quests).
	private static BookAcquisitionEntry QuestEntry(BookItemSource s)
	{
		string chance = BookAcquisitionRules.Chance(s.Chance);
		string best = BookAcquisitionRules.Chance(AnglerChance(s.Note, s.Item, 151, s.Note == "rollhardmode"));
		List<string> label = new List<string>();
		string body;
		switch (s.Note)
		{
		case "at":
		case "atifnone":
			label.Add(L("Rewards.AtQuests", s.NoteValue));
			body = L("Rewards.AtQuestsBody", s.NoteValue);
			if (s.Note == "atifnone")
			{
				label.Add(L("Rewards.IfYouHaveNone"));
				body += "\n" + L("Rewards.IfYouHaveNoneBody");
			}
			break;
		case "roll":
		case "rollhardmode":
			if (s.NoteValue > 0)
			{
				label.Add(L("Rewards.AfterQuests", s.NoteValue));
			}
			if (s.Note == "rollhardmode")
			{
				label.Add(L("HowToGet.Hardmode"));
			}
			if (s.From > 0)
			{
				label.Add(L("HowToGet.WithItsWholeSet"));
			}
			body = L("Rewards.ChanceBody", chance, best);
			break;
		case "tackle":
			label.Add(L("Rewards.OneOfTackle", AnglerTackle.Length));
			body = L("Rewards.TackleBody", chance, best);
			break;
		case "potion":
			label.Add(L("Rewards.IfNothingElse"));
			body = L("Rewards.PotionBody", chance, best);
			break;
		case "honey":
			label.Add(L("HowToGet.For", Name(ItemID.BumblebeeTuna)));
			body = L("Rewards.HoneyBody", chance, BookAcquisitionRules.Chance(0.5f));
			break;
		case "decoration":
			label.Add(L("Rewards.OneOfDecorations", AnglerDecorations.Length));
			body = L("Rewards.DecorationBody", chance, BookAcquisitionRules.Chance(1f / AnglerDecorations.Length));
			break;
		default:
			label.Add(L("Rewards.BaitOnTop"));
			body = L("Rewards.BaitBody", chance, best);
			break;
		}
		string text = string.Join(" · ", label);
		return new BookAcquisitionEntry(Lang.GetNPCNameValue(NPCID.Angler), body, BookAcquisitionKind.Quest, chance, "×" + BookAcquisitionRules.Stack(s.Min, s.Max), 0, NPCID.Angler, text.Length > 0, -1, s.Owner, text);
	}

	internal static string KindName(BookAcquisitionKind kind)
	{
		return kind switch
		{
			BookAcquisitionKind.Quest => L("Rewards.Quest"),
			BookAcquisitionKind.Event => L("Rewards.Event"),
			_ => null
		};
	}

	// Items no player can hold, kept out of the catalogue: pickups the game never lets into an inventory (hearts, stars,
	// their holiday forms, the Nebula boosters), vanilla leftovers with no name of their own (Fake_newchest1, BoringBow),
	// enemy banners of NPCs the game never made (Item.BannerToNPC is 0 for their banner), items nothing creates,
	// tModLoader's stand-in for an unloaded mod's item, and Magic Storage's placeholders.
	internal static bool Hidden(Item item)
	{
		if (ItemID.Sets.ItemsThatShouldNotBeInInventory[item.type])
		{
			return true;
		}
		if (item.ModItem == null)
		{
			unclaimedBanners ??= Enumerable.Range(1, Main.MaxBannerTypes - 1).Where((int b) => Item.BannerToNPC(b) == 0).Select(Item.BannerToItem).ToHashSet();
			return !Language.Exists("ItemName." + ItemID.Search.GetName(item.type)) || unclaimedBanners.Contains(item.type) || NeverCreated.Contains(item.type);
		}
		string space = item.ModItem.GetType().Namespace ?? "";
		return item.ModItem is UnloadedItem || HiddenModItems.Contains(item.ModItem.FullName) || (item.ModItem.Mod.Name == "MagicStorage" && space.StartsWith("MagicStorage.Items.", StringComparison.Ordinal) && space.EndsWith("Display", StringComparison.Ordinal));
	}

	private static string Name(int type)
	{
		return ContentSamples.ItemsByType.TryGetValue(type, out Item item) ? item.Name : "";
	}
}
