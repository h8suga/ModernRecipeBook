using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using log4net;

namespace ModernRecipeBook;

// Earliest stage at which each item can be obtained: shortest path over recipes and drops, seeded by
// curated lists, staged boss drops and, where nothing better is known, the item's rarity tier.
internal static class BookProgression
{
	private static Dictionary<int, BookStage> items = new Dictionary<int, BookStage>();

	private static Dictionary<int, BookStage> recipes = new Dictionary<int, BookStage>();

	private static Dictionary<int, BookStage> recipeMinimums = new Dictionary<int, BookStage>();

	private static Dictionary<int, string> reasons = new Dictionary<int, string>();

	private static HashSet<int> modeExcludedItems = new HashSet<int>();

	private static HashSet<int> modeExcludedRecipes = new HashSet<int>();

	// Unknown items and recipes whose source exists, but not at the current world's difficulty (a boss bag in
	// Classic); every other Unknown has no regular way to be obtained.
	internal static bool ItemModeExcluded(int type)
	{
		return ItemStage(type) == BookStage.Unknown && modeExcludedItems.Contains(type);
	}

	internal static bool RecipeModeExcluded(int index)
	{
		return RecipeStage(index) == BookStage.Unknown && modeExcludedRecipes.Contains(index);
	}

	internal static BookStage ItemStage(int type)
	{
		if (!items.TryGetValue(type, out var s))
		{
			return BookStage.Unknown;
		}
		return s;
	}

	internal static BookStage RecipeStage(int index)
	{
		if (!recipes.TryGetValue(index, out var s))
		{
			return BookStage.Unknown;
		}
		return s;
	}

	internal static BookStage RecipeMinimumStage(int index)
	{
		return recipeMinimums.GetValueOrDefault(index, BookStage.PreHardmode);
	}

	// Dev audit for the test stand: one TSV line per item (type, mod, internal name, rarity, stage, reason,
	// display name).
	internal static IEnumerable<string> Audit()
	{
		return from i in ContentSamples.ItemsByType.Values
			where i != null && !i.IsAir
			select string.Join('\t', i.type, i.ModItem?.Mod.Name ?? "Terraria", ItemName(i), i.rare, ItemStage(i.type), reasons.GetValueOrDefault(i.type, ""), Lang.GetItemNameValue(i.type));
	}

	// Dev audit: every source considered for an item. Set TraceSources before the book rebuilds.
	internal static bool TraceSources;

	private static Dictionary<int, List<string>> trace = new Dictionary<int, List<string>>();

	private static List<BookProgressionRules.Route> lastRoutes = new List<BookProgressionRules.Route>();

	internal static IEnumerable<string> Explain(int type)
	{
		foreach (string seed in trace.GetValueOrDefault(type) ?? new List<string>())
		{
			yield return "seed " + seed;
		}
		foreach (BookProgressionRules.Route r in lastRoutes.Where((BookProgressionRules.Route route) => route.Output == type))
		{
			yield return $"route {r.Source ?? ("recipe:" + r.Key)} floor={r.Floor} uncertain={r.Uncertain} -> {RecipeStage(r.Key)}: " + string.Join(" + ", r.Ingredients.Select((int[] g) => string.Join("|", g.Take(4).Select((int t) => ItemName(ContentSamples.ItemsByType[t]) + "=" + ItemStage(t)))));
		}
	}

	// Dev audit: one TSV line per NPC (net id, mod, internal name, display name, curated stage).
	internal static IEnumerable<string> NpcAudit()
	{
		return from pair in ContentSamples.NpcsByNetId
			let npc = pair.Value
			select string.Join('\t', pair.Key, npc.ModNPC?.Mod.Name ?? "Terraria", npc.ModNPC?.Name ?? (NPCID.Search.TryGetName(npc.type, out var name) ? name : ""), npc.TypeName, BookProgressionSources.NpcStage(npc));
	}

	// Dev audit: an NPC's reported drops with their conditions and the floor each condition gives.
	internal static IEnumerable<string> DropAudit(int npcNetId)
	{
		List<DropRateInfo> rates = new List<DropRateInfo>();
		foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForNPCID(npcNetId, includeGlobalDrops: false))
		{
			rule.ReportDroprates(rates, new DropRateInfoChainFeed(1f));
		}
		return from r in rates
			let item = ContentSamples.ItemsByType.GetValueOrDefault(r.itemId)
			where item != null
			select string.Join('\t', ItemName(item), item.rare, item.maxStack, string.Join(", ", (r.conditions ?? new List<IItemDropRuleCondition>()).Select((IItemDropRuleCondition c) => c.GetType().Name + "=" + (BookProgressionSources.DropConditionFloor(c)?.ToString() ?? "opaque"))));
	}

	internal static void Clear()
	{
		items.Clear();
		recipes.Clear();
		recipeMinimums.Clear();
		reasons.Clear();
		modeExcludedItems.Clear();
		modeExcludedRecipes.Clear();
	}

	internal static void Rebuild(List<int> indexes, Dictionary<int, List<int>> recipesByItem)
	{
		Stopwatch timer = Stopwatch.StartNew();
		Dictionary<int, BookStage> floors = new Dictionary<int, BookStage>();
		Dictionary<int, BookStage> seeds = new Dictionary<int, BookStage>();
		Dictionary<int, string> why = new Dictionary<int, string>();
		Dictionary<int, List<string>> traced = TraceSources ? new Dictionary<int, List<string>>() : null;
		Dictionary<string, int[]> names =BookProgressionRules.BuildNameIndex(from i in ContentSamples.ItemsByType.Values
			where i != null && !i.IsAir && i.ModItem == null
			select new KeyValuePair<string, int>(ItemID.Search.GetName(i.type) ?? ("#" + i.type), i.type));
		Mark(BookStage.Hardmode, "CrystalBall PearlstoneBlock PearlsandBlock PinkIceBlock HallowedSeeds HallowHardenedSand HallowSandstone BlueSolution PurpleSolution RedSolution DarkBlueSolution LihzahrdFurnace FleshCloningVaat ZapinatorOrange LongRainbowTrailWings DemonHeart WallOfFleshBossBag WallOfFleshGoatMountItem LizardEgg DestroyerBossBag TwinsBossBag SkeletronPrimeBossBag PlanteraBossBag GolemBossBag FishronBossBag CultistBossBag FairyQueenBossBag QueenSlimeBossBag BossBagBetsy CobaltOre PalladiumOre MythrilOre OrichalcumOre AdamantiteOre TitaniumOre ChlorophyteOre CobaltBar PalladiumBar MythrilBar OrichalcumBar AdamantiteBar TitaniumBar ChlorophyteBar HallowedBar ShroomiteBar SpectreBar SoulofLight SoulofNight SoulofFlight SoulofMight SoulofSight SoulofFright CrystalShard PixieDust UnicornHorn CrystalBullet CursedFlame Ichor Ectoplasm TruffleWorm LifeFruit FragmentSolar FragmentVortex FragmentNebula FragmentStardust SolarTablet LihzahrdPowerCell MythrilAnvil OrichalcumAnvil AdamantiteForge TitaniumForge LunarCraftingStation FairyBell MagicalHarp Harp SpellTome EmptyDropper ExplosivePowder SteampunkBoiler BlendOMatic Autohammer CursedFlames GoldenShower CrystalStorm ClingerStaff RodofDiscord DaedalusStormbow FetidBaghnakhs FlyingKnife CrystalVileShard DartPistol DartRifle ChainGuillotines PutridScent FleshKnuckles TendonHook IlluminantHook WormHook IceSickle BeamSword Frostbrand IceBow Marrow Uzi SniperRifle TacticalShotgun RifleScope ChainGun RocketLauncher GrenadeLauncher ProximityMineLauncher CoinGun PirateStaff Cutlass PirateMap Amarok HelFire FormatC Gradient Chik Yelets Kraken TheEyeOfCthulhu DeathSickle BrokenHeroSword TerraBlade FlowerofFrost FrostStaff PoisonStaff VenomStaff SpiderFang AncientBattleArmorMaterial AncientBattleArmorMaterial FrostCore LightShard DarkShard BlackFairyDust TatteredBeeWing ButterflyDust IceFeather FireFeather SpookyTwig SpookyWood BeetleHusk ScalyTruffle AvengerEmblem WarriorEmblem RangerEmblem SorcererEmblem SummonerEmblem Pwnhammer ClockworkAssaultRifle BreakerBlade LaserRifle HallowedKey JungleKey CorruptionKey CrimsonKey FrozenKey DungeonDesertKey VampireKnives ScourgeoftheCorruptor RainbowGun StaffoftheFrostHydra PiranhaGun StormTigerStaff FlameWings LeafWings Jetpack SteampunkWings BoneWings FrozenWings ButterflyWings BeeWings QueenSlimeBossBag QueenSlimeCrystal Smolstar GelBalloon SparkleGuitar CrystalNinjaHelmet CrystalNinjaChestplate CrystalNinjaLeggings SanguineStaff SharpTears BloodLustCluster DripplerFlail BouncingShield BouncingShield FireWhip CoolWhip SwordWhip ScytheWhip RainbowWhip MaceWhip EmpressBlade FairyQueenMagicItem FairyQueenRangedItem FairyQueenBossBag RainbowWhip Seedler TheAxe WaspGun LeafBlower GrenadeLauncher VenusMagnum NettleBurst FlowerPow GolemFist PossessedHatchet Stynger StaffofEarth HeatRay EyeoftheGolem Picksaw TempestStaff Flairon RazorbladeTyphoon BubbleGun Tsunami ShrimpyTruffle BetsyWings DD2BetsyBow DD2SquireBetsySword ApprenticeStaffT3 MonkStaffT3 Razorpine BlizzardStaff NorthPole ChristmasTreeSword SnowmanCannon CandyCornRifle JackOLanternLauncher BatScepter RavenStaff TheHorsemansBlade StakeLauncher SpookyHook SpookyWings WitchBroom RainbowRod NeptunesShell FrozenTurtleShell TurtleShell MoonStone SunStone PaladinsShield AnkhShield DiscountCard LuckyCoin GoldRing PirateShipMountItem PygmyStaff TikiMask TikiShirt TikiPants CrystalSerpent ObsidianSwordfish Toxikarp BlessedApple CorruptFishingCrateHard CrimsonFishingCrateHard DungeonFishingCrateHard FloatingIslandFishingCrateHard FrozenCrateHard HallowedFishingCrate HallowedFishingCrateHard JungleFishingCrateHard LavaCrateHard OceanCrateHard OasisCrateHard GoldenCrateHard IronCrateHard WoodenCrateHard");
		Mark(BookStage.PostHardmode, "TeleportationPylonVictory MoonLordPetItem MoonLordMasterTrophy LunarOre LunarBar Meowmere StarWrath Terrarian SDMG LastPrism LunarFlareBook RainbowCrystalStaff MoonlordTurretStaff Celeb2 RainbowCrystalStaff MoonLordBossBag MoonLordTrophy BossMaskMoonlord GravityGlobe SuspiciousLookingTentacle PortalGun RodOfHarmony BottomlessShimmerBucket Clentaminator2 ");
		// Hardmode-only world tiles, plants, fish and pickups that carry White to Orange rarity (Hallow trees and
		// fish, the Jungle Temple and its traps, Strange Plants, Martian probes); checked against the wiki's
		// "Hardmode-only items" category.
		Mark(BookStage.Hardmode, "Pearlwood LihzahrdBrick LihzahrdAltar LihzahrdWallUnsafe MartianConduitPlating MartianSink Constellation MechdusaSummon StrangePlant1 StrangePlant2 StrangePlant3 StrangePlant4 Dragonfruit Starfruit PrincessFish Prismite FlameTrap SpearTrap SpikyBallTrap SuperDartTrap FleshBlock LesionBlock LivingFireBlock NebulaPickup1 NebulaPickup2 NebulaPickup3");
		List<Item> all = ContentSamples.ItemsByType.Values.Where((Item i) => i != null && !i.IsAir && i.type > 0).ToList();
		foreach (Item item in all)
		{
			BookStage curated = BookProgressionSources.ItemStage(item);
			if (curated != BookStage.Unknown)
			{
				floors[item.type] = Max(curated, floors.GetValueOrDefault(item.type, BookStage.PreHardmode));
			}
			if (floors.TryGetValue(item.type, out var stage))
			{
				Seed(item.type, stage, "list");
			}
			else if (item.ModItem == null)
			{
				Seed(item.type, BookProgressionSources.VanillaNaturalStage(ItemID.Search.GetName(item.type)), "list");
			}
			Seed(item.type, BookProgressionSources.WorldgenStage(item), "worldgen");
		}
		List<BookProgressionRules.Route> routes = new List<BookProgressionRules.Route>();
		HashSet<int> modeItems = new HashSet<int>();
		HashSet<int> modeRecipes = new HashSet<int>();
		HashSet<int> sourced = AddDropSources(routes, floors, modeItems, Seed);
		// Raw items: nothing crafts them and no known source drops them, so their rarity tier is the best guess.
		foreach (Item item in all)
		{
			if (!recipesByItem.ContainsKey(item.type) && !sourced.Contains(item.type))
			{
				Seed(item.type, BookProgressionSources.RarityStage(item), "rarity");
			}
		}
		Dictionary<int, int[]> stationIndex = (from i in all
			where i.createTile >= 0
			group i by i.createTile).ToDictionary((IGrouping<int, Item> g) => g.Key, (IGrouping<int, Item> g) => g.Select((Item i) => i.type).ToArray());
		foreach (int index in indexes)
		{
			Recipe r = Main.recipe[index];
			List<int[]> inputs = new List<int[]>();
			foreach (Item input in r.requiredItem.Where((Item i) => !i.IsAir))
			{
				HashSet<int> types = new HashSet<int> { input.type };
				foreach (int groupId in r.acceptedGroups)
				{
					if (RecipeGroup.recipeGroups.TryGetValue(groupId, out var group) && group.ValidItems.Contains(input.type))
					{
						types.UnionWith(group.ValidItems);
					}
				}
				inputs.Add(types.ToArray());
			}
			BookStage floor = BookStage.PreHardmode;
			bool opaque = false;
			foreach (int tile in r.requiredTile.Where((int t) => t >= 0))
			{
				if (tile == TileID.MythrilAnvil || tile == TileID.AdamantiteForge || tile == TileID.LunarCraftingStation)
				{
					floor = Max(floor, BookStage.Hardmode);
					continue;
				}
				int[] stationItems = stationIndex.GetValueOrDefault(tile) ?? Array.Empty<int>();
				if (stationItems.Length != 0)
				{
					inputs.Add(stationItems);
				}
				else if (tile >= TileID.Count)
				{
					opaque = true;
				}
			}
			// Special-seed worlds and other difficulties than the current one make the recipe unavailable.
			bool unavailable = false;
			foreach (Condition condition in r.Conditions)
			{
				BookStage? hint = BookProgressionSources.ConditionFloor(condition);
				if (BookProgressionSources.ModeBlocked(condition?.Description?.Key, () => condition.IsMet()))
				{
					modeRecipes.Add(index);
					modeItems.Add(r.createItem.type);
				}
				if (hint == BookStage.Unknown)
				{
					unavailable = true;
				}
				else if (hint.HasValue)
				{
					floor = Max(floor, hint.Value);
				}
				else
				{
					opaque = true;
				}
			}
			if (unavailable)
			{
				continue;
			}
			// Another mod's custom gate or a station nothing places: trust the output's rarity tier for it.
			bool uncertain = false;
			if (opaque)
			{
				BookStage hint2 = BookProgressionSources.RarityStage(r.createItem);
				if (hint2 == BookStage.Unknown)
				{
					uncertain = true;
				}
				else
				{
					floor = Max(floor, hint2);
				}
			}
			routes.Add(new BookProgressionRules.Route(index, r.createItem.type, floor, inputs.ToArray(), uncertain));
		}
		(items, recipes) = BookProgressionRules.Resolve(seeds, floors, routes, why);
		// Blocks dug out of the world need a pickaxe strong enough for their tile (ModTile.MinPick: Voidstone
		// 180, Astral Ore 210, Exodium 225), so they come no earlier than the first such pickaxe.
		var picks = (from i in all
			where i.pick > 0 && items.ContainsKey(i.type)
			select (Power: i.pick, Stage: items[i.type])).ToList();
		bool raised = false;
		foreach (Item item in all)
		{
			if (item.createTile < TileID.Count || TileLoader.GetTile(item.createTile) is not ModTile tile || tile.MinPick <= 0)
			{
				continue;
			}
			BookStage pick = picks.Where(p => p.Power >= tile.MinPick).Select(p => p.Stage).DefaultIfEmpty(BookStage.Unknown).Min();
			if (pick > floors.GetValueOrDefault(item.type, BookStage.PreHardmode))
			{
				floors[item.type] = pick;
				if (seeds.TryGetValue(item.type, out var mined) && mined < pick)
				{
					seeds[item.type] = pick;
					why[item.type] = "pickaxe:" + tile.MinPick;
				}
				raised = true;
			}
		}
		// Second pass: whatever the graph could not place gets its rarity tier, then everything made from it
		// is placed by running the graph again.
		int seeded = seeds.Count;
		foreach (Item item in all.Where((Item i) => !items.ContainsKey(i.type)))
		{
			Seed(item.type, BookProgressionSources.RarityStage(item), "rarity");
		}
		if (seeds.Count > seeded || raised)
		{
			(items, recipes) = BookProgressionRules.Resolve(seeds, floors, routes, why);
		}
		// Whatever is only reachable through something the difficulty rules out (expert-only bag contents in
		// Classic) is unavailable in this mode too, not sourceless.
		bool grew = true;
		while (grew)
		{
			grew = false;
			foreach (BookProgressionRules.Route route in routes)
			{
				if (!items.ContainsKey(route.Output) && route.Ingredients.Any((int[] group) => group.All(Blocked)) && modeItems.Add(route.Output))
				{
					grew = true;
				}
			}
		}
		foreach (BookProgressionRules.Route route in routes.Where((BookProgressionRules.Route r) => r.Key >= 0 && r.Ingredients.Any((int[] group) => group.All(Blocked))))
		{
			modeRecipes.Add(route.Key);
		}
		modeExcludedItems = modeItems;
		modeExcludedRecipes = modeRecipes;
		reasons = why;
		trace = traced ?? new Dictionary<int, List<string>>();
		lastRoutes = TraceSources ? routes : new List<BookProgressionRules.Route>();
		recipeMinimums =routes.Where((BookProgressionRules.Route route) => route.Key >= 0).ToDictionary((BookProgressionRules.Route route) => route.Key, (BookProgressionRules.Route route) => BookProgressionRules.MinimumStage(route, items, floors));
		ILog logger = ModContent.GetInstance<ModernRecipeBook>().Logger;
		logger.Info($"Progression audit ({timer.ElapsedMilliseconds} ms) items: " + Totals(all.Select((Item i) => ItemStage(i.type))));
		logger.Info("Progression audit recipes: " + Totals(indexes.Select(RecipeStage)));
		logger.Info("Progression audit Calamity items: " + Totals(from i in all
			where i.ModItem?.Mod.Name == "CalamityMod"
			select ItemStage(i.type)));
		void Mark(BookStage value2, string list)
		{
			string[] array = list.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			foreach (string name in array)
			{
				if (names.TryGetValue(name, out var ids))
				{
					int[] array2 = ids;
					foreach (int id in array2)
					{
						floors[id] = value2;
					}
				}
			}
		}
		bool Blocked(int type)
		{
			return !items.ContainsKey(type) && modeItems.Contains(type);
		}
		void Seed(int type, BookStage stage, string reason)
		{
			if (traced != null && stage != BookStage.Unknown)
			{
				(traced.TryGetValue(type, out var list) ? list : (traced[type] = new List<string>())).Add(reason + "=" + stage);
			}
			if (stage != BookStage.Unknown && floors.TryGetValue(type, out var minimum))
			{
				stage = Max(stage, minimum);
			}
			if (stage != BookStage.Unknown && (!seeds.TryGetValue(type, out var old) || stage < old))
			{
				seeds[type] = stage;
				why[type] = reason;
			}
		}
		static string Totals(IEnumerable<BookStage> stages)
		{
			Dictionary<BookStage, int> counts = (from result in stages
				group result by result).ToDictionary((IGrouping<BookStage, BookStage> g) => g.Key, (IGrouping<BookStage, BookStage> g) => g.Count());
			return string.Join(", ", new BookStage[4]
			{
				BookStage.PreHardmode,
				BookStage.Hardmode,
				BookStage.PostHardmode,
				BookStage.Unknown
			}.Select((BookStage key2) => key2.ToString() + "=" + counts.GetValueOrDefault(key2)));
		}
	}

	// Drops of staged bosses seed their stage directly; drops of any other NPC get the item's rarity tier;
	// bag, crate and present contents become routes from the container. Returns every item a staged boss
	// or a container drops, so the raw-item pass leaves those to the graph.
	private static HashSet<int> AddDropSources(List<BookProgressionRules.Route> routes, IReadOnlyDictionary<int, BookStage> floors, HashSet<int> modeItems, Action<int, BookStage, string> seed)
	{
		HashSet<int> sourced = new HashSet<int>();
		int sourceKey = -1;
		try
		{
			foreach (KeyValuePair<int, NPC> pair in ContentSamples.NpcsByNetId)
			{
				NPC npc = pair.Value;
				string npcName = npc.ModNPC?.Name ?? (NPCID.Search.TryGetName(npc.type, out var name) ? name : npc.type.ToString());
				var drops = (from rate in Reports(Main.ItemDropsDB.GetRulesForNPCID(pair.Key, includeGlobalDrops: false))
					where ContentSamples.ItemsByType.ContainsKey(rate.itemId)
					let drop = ContentSamples.ItemsByType[rate.itemId]
					let floor = DropFloor(rate, drop, modeItems)
					where floor != BookStage.Unknown
					select (Rate: rate, Drop: drop, Floor: floor)).ToList();
				// Ordinary enemies have no stage of their own: they appear as early as the latest thing they always
				// drop (Pixies drop Pixie Dust, Martians their gear), and their loot and banner share that stage.
				BookStage stage = BookProgressionSources.NpcStage(npc);
				string source = "drop:" + npcName;
				if (stage == BookStage.Unknown)
				{
					stage = InferEnemyStage(drops.Where(d => Unconditional(d.Rate)).Select(d => d.Drop));
					source = "enemy:" + npcName;
				}
				foreach (var (rate, drop, floor) in drops)
				{
					if (stage != BookStage.Unknown)
					{
						sourced.Add(rate.itemId);
						seed(rate.itemId, Max(stage, floor), source);
						continue;
					}
					BookStage tier = BookProgressionSources.RarityStage(drop);
					if (tier != BookStage.Unknown)
					{
						seed(rate.itemId, Max(tier, floor), "rarity");
					}
				}
				// A banner follows its own NPC, not helpers sharing it (Vile Spit for the Corruptor's banner).
				int banner = Item.NPCtoBanner(npc.BannerID());
				int bannerItem = banner > 0 && Item.BannerToNPC(banner) == npc.type ? Item.BannerToItem(banner) : 0;
				if (stage != BookStage.Unknown && bannerItem > 0)
				{
					sourced.Add(bannerItem);
					seed(bannerItem, stage, "banner:" + npcName);
				}
				// Critters caught with a bug net. Without a better clue a critter is about from the start
				// (Draedon lab Nanodroids carry the tier-less DarkOrange rarity).
				if (npc.catchItem > 0 && ContentSamples.ItemsByType.TryGetValue(npc.catchItem, out var critter))
				{
					BookStage tier4 = BookProgressionSources.RarityStage(critter);
					sourced.Add(critter.type);
					seed(critter.type, stage != BookStage.Unknown ? stage : (tier4 != BookStage.Unknown ? tier4 : BookStage.PreHardmode), "catch:" + npcName);
				}
			}
			// Global loot (biome essences and the like) applies to every NPC, so it is never boss-staged.
			List<IItemDropRule> specific = Main.ItemDropsDB.GetRulesForNPCID(NPCID.BlueSlime, includeGlobalDrops: false);
			foreach (DropRateInfo rate3 in Reports(Main.ItemDropsDB.GetRulesForNPCID(NPCID.BlueSlime, includeGlobalDrops: true).Except(specific)))
			{
				if (ContentSamples.ItemsByType.TryGetValue(rate3.itemId, out var drop3))
				{
					BookStage floor3 = DropFloor(rate3, drop3, modeItems);
					BookStage tier3 = BookProgressionSources.RarityStage(drop3);
					if (floor3 != BookStage.Unknown && tier3 != BookStage.Unknown)
					{
						seed(rate3.itemId, Max(tier3, floor3), "rarity");
					}
				}
			}
			foreach (KeyValuePair<int, Item> pair2 in ContentSamples.ItemsByType)
			{
				if (pair2.Value == null || pair2.Value.IsAir)
				{
					continue;
				}
				foreach (DropRateInfo rate2 in Reports(Main.ItemDropsDB.GetRulesForItemID(pair2.Key)))
				{
					if (!ContentSamples.ItemsByType.TryGetValue(rate2.itemId, out var drop2))
					{
						continue;
					}
					BookStage floor2 = DropFloor(rate2, drop2, modeItems);
					if (floor2 == BookStage.Unknown)
					{
						continue;
					}
					sourced.Add(rate2.itemId);
					routes.Add(new BookProgressionRules.Route(sourceKey--, rate2.itemId, floor2, new int[1][] { new int[1] { pair2.Key } }, Source: "drop:" + ItemName(pair2.Value)));
					// Crate, present and lock box loot is loot that chests, fishing and enemies give too, which no
					// database lists. Boss bag contents are the boss's own (masks are Blue but post-boss).
					BookStage tier2 = BookProgressionSources.RarityStage(drop2);
					if (!ItemID.Sets.BossBag[pair2.Key] && tier2 != BookStage.Unknown)
					{
						seed(rate2.itemId, Max(tier2, floor2), "rarity");
					}
				}
				// Shimmer (the Aether exists from world generation; late transmutations are curated floors).
				int shimmered = ItemID.Sets.ShimmerTransformToItem[pair2.Key];
				if (shimmered > 0)
				{
					sourced.Add(shimmered);
					routes.Add(new BookProgressionRules.Route(sourceKey--, shimmered, BookStage.PreHardmode, new int[1][] { new int[1] { pair2.Key } }, Source: "shimmer:" + ItemName(pair2.Value)));
				}
			}
			foreach (var (from, to) in BookProgressionSources.CalamityConversions)
			{
				if (ModContent.TryFind("CalamityMod", from, out ModItem fromItem) && ModContent.TryFind("CalamityMod", to, out ModItem toItem))
				{
					sourced.Add(toItem.Type);
					routes.Add(new BookProgressionRules.Route(sourceKey--, toItem.Type, BookStage.PreHardmode, new int[1][] { new int[1] { fromItem.Type } }, Source: "use:" + from));
				}
			}
			// Recorded music boxes come from a blank Music Box worn while the track plays (vanilla boxes place
			// the MusicBoxes tile; modded ones are registered with MusicLoader).
			var modBoxes = typeof(MusicLoader).GetField("itemToMusic", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Dictionary<int, int>;
			IEnumerable<int> recorded = (from i in ContentSamples.ItemsByType.Values
				where i != null && i.ModItem == null && i.createTile == TileID.MusicBoxes && i.type != ItemID.MusicBox
				select i.type).Concat(modBoxes?.Keys ?? Enumerable.Empty<int>());
			foreach (int box in recorded.Distinct().Where(ContentSamples.ItemsByType.ContainsKey))
			{
				sourced.Add(box);
				routes.Add(new BookProgressionRules.Route(sourceKey--, box, BookProgressionSources.TrackStage(ContentSamples.ItemsByType[box]), new int[1][] { new int[1] { ItemID.MusicBox } }, Source: "record:MusicBox"));
			}
			// Strange Plants traded to the Dye Trader.
			int[] plants = { ItemID.StrangePlant1, ItemID.StrangePlant2, ItemID.StrangePlant3, ItemID.StrangePlant4 };
			foreach (var (gate, dyes) in BookProgressionSources.DyeTraderRewards)
			{
				foreach (int dye in dyes)
				{
					sourced.Add(dye);
					routes.Add(new BookProgressionRules.Route(sourceKey--, dye, gate, new int[1][] { plants }, Source: "trade:DyeTrader"));
				}
			}
		}
		catch (Exception exception)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("MRB progression drop sources failed; using recipes and rarity only.", exception);
		}
		// Shops: the town NPC's arrival stage plus the entry's conditions (Calamity gates its stock on the
		// same CalamityConditions keys as its drops).
		try
		{
			foreach (AbstractNPCShop shop in NPCShopDatabase.AllShops)
			{
				BookStage townStage = BookProgressionSources.TownNpcStage(shop.NpcType);
				string source = "shop:" + (NPCLoader.GetNPC(shop.NpcType)?.Name ?? (NPCID.Search.TryGetName(shop.NpcType, out var townName) ? townName : shop.NpcType.ToString()));
				foreach (AbstractNPCShop.Entry entry in shop.ActiveEntries)
				{
					Item item = entry.Item;
					if (item == null || item.IsAir)
					{
						continue;
					}
					BookStage stage2 = townStage;
					List<int[]> carried = new List<int[]>();
					foreach (Condition condition in entry.Conditions)
					{
						if (BookProgressionSources.CarriedItem(condition) is int needed)
						{
							carried.Add(new int[1] { needed });
						}
						BookStage? hint = BookProgressionSources.ConditionFloor(condition);
						BookStage value = hint ?? BookProgressionSources.RarityStage(item);
						if (hint == BookStage.Unknown)
						{
							if (BookProgressionSources.ModeBlocked(condition?.Description?.Key, () => condition.IsMet()))
							{
								modeItems.Add(item.type);
							}
							stage2 = BookStage.Unknown;
							break;
						}
						if (value != BookStage.Unknown)
						{
							stage2 = Max(stage2, value);
						}
					}
					if (stage2 == BookStage.Unknown)
					{
						continue;
					}
					sourced.Add(item.type);
					if (carried.Count > 0)
					{
						// Stock that needs an item in the inventory (Blood Rune with Ice Barrage) is as late as it.
						routes.Add(new BookProgressionRules.Route(sourceKey--, item.type, stage2, carried.ToArray(), Source: source));
					}
					else
					{
						seed(item.type, stage2, source);
					}
				}
			}
		}
		catch (Exception exception2)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("MRB progression shop sources failed.", exception2);
		}
		return sourced;
		// When an enemy appears, judged by what it always drops: a curated material (Pixie Dust, shards) is
		// proof on its own; gear rarity is only trusted when two pieces agree, because Calamity moves single
		// accessories (Adhesive Bandage, Vitamins) onto pre-hardmode enemies. Food, potions, blocks and vanity
		// carry arbitrary rarities and say nothing.
		BookStage InferEnemyStage(IEnumerable<Item> unconditional)
		{
			List<Item> items = unconditional.DistinctBy((Item i) => i.type).ToList();
			BookStage curated = items.Where((Item i) => floors.ContainsKey(i.type)).Select((Item i) => floors[i.type]).DefaultIfEmpty(BookStage.Unknown).Max();
			List<BookStage> gear = (from i in items
				where !floors.ContainsKey(i.type) && i.maxStack == 1 && !i.vanity
				select BookProgressionSources.RarityStage(i) into s
				where s != BookStage.Unknown
				orderby s descending
				select s).ToList();
			return Max(curated, gear.Count >= 2 ? gear[1] : BookStage.Unknown);
		}
		static IEnumerable<DropRateInfo> Reports(IEnumerable<IItemDropRule> rules)
		{
			List<DropRateInfo> rates = new List<DropRateInfo>();
			foreach (IItemDropRule rule in rules)
			{
				try
				{
					Report(rule, rates);
				}
				catch (Exception)
				{
				}
			}
			return rates.Where((DropRateInfo r) => r.itemId > 0 && r.dropRate > 0f);
		}
		// Sequential rules report each entry as a failed roll of the previous one, so a condition on an early
		// entry (the Present's hardmode-only Snow Globe) leaks onto everything after it. Report entries alone.
		static void Report(IItemDropRule rule, List<DropRateInfo> rates)
		{
			IItemDropRule[] sequence = (rule as SequentialRulesNotScalingWithLuckRule)?.rules ?? (rule as SequentialRulesRule)?.rules;
			if (sequence == null)
			{
				rule.ReportDroprates(rates, new DropRateInfoChainFeed(1f));
				return;
			}
			foreach (IItemDropRule entry in sequence)
			{
				Report(entry, rates);
			}
			Chains.ReportDroprates(rule.ChainedRules, 1f, rates, new DropRateInfoChainFeed(1f));
		}
	}

	private static bool Unconditional(DropRateInfo rate)
	{
		return rate.conditions == null || rate.conditions.All((IItemDropRuleCondition c) => BookProgressionSources.DropConditionFloor(c) == BookStage.PreHardmode);
	}

	// Stage the conditions on a drop add. Opaque conditions (another mod's lambdas) fall back to the
	// dropped item's rarity tier; a NeverTrue, special-seed or other-difficulty rule makes the drop Unknown,
	// and the last case is remembered in modeBlocked.
	private static BookStage DropFloor(DropRateInfo rate, Item drop, HashSet<int> modeBlocked = null)
	{
		BookStage floor = BookStage.PreHardmode;
		foreach (IItemDropRuleCondition condition in rate.conditions ?? new List<IItemDropRuleCondition>())
		{
			if (BookProgressionSources.ModeBlocked(condition))
			{
				modeBlocked?.Add(drop.type);
				return BookStage.Unknown;
			}
			BookStage? hint = BookProgressionSources.DropConditionFloor(condition);
			if (hint == BookStage.Unknown)
			{
				return BookStage.Unknown;
			}
			BookStage value = hint ?? BookProgressionSources.RarityStage(drop);
			if (value != BookStage.Unknown)
			{
				floor = Max(floor, value);
			}
		}
		return floor;
	}

	private static BookStage Max(BookStage a, BookStage b)
	{
		return (BookStage)Math.Max((int)a, (int)b);
	}

	private static string ItemName(Item item)
	{
		return item.ModItem?.Name ?? ItemID.Search.GetName(item.type);
	}
}
