using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using log4net;

namespace ModernRecipeBook;

internal static class BookProgression
{
	private static Dictionary<int, BookStage> items = new Dictionary<int, BookStage>();

	private static Dictionary<int, BookStage> recipes = new Dictionary<int, BookStage>();

	private static Dictionary<int, BookStage> recipeMinimums = new Dictionary<int, BookStage>();

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

	internal static void Clear()
	{
		items.Clear();
		recipes.Clear();
		recipeMinimums.Clear();
	}

	internal static void Rebuild(List<int> indexes, Dictionary<int, List<int>> recipesByItem)
	{
		Dictionary<int, BookStage> floors = new Dictionary<int, BookStage>();
		Dictionary<int, BookStage> seeds = new Dictionary<int, BookStage>();
		Dictionary<string, int[]> names = BookProgressionRules.BuildNameIndex(from i in ContentSamples.ItemsByType.Values
			where i != null && !i.IsAir && i.ModItem == null
			select new KeyValuePair<string, int>(ItemID.Search.GetName(i.type) ?? ("#" + i.type), i.type));
		Mark(BookStage.Hardmode, "CrystalBall PearlstoneBlock PearlsandBlock PinkIceBlock HallowedSeeds HallowHardenedSand HallowSandstone BlueSolution PurpleSolution RedSolution DarkBlueSolution LihzahrdFurnace FleshCloningVaat ZapinatorOrange LongRainbowTrailWings DemonHeart WallOfFleshBossBag WallOfFleshGoatMountItem LizardEgg DestroyerBossBag TwinsBossBag SkeletronPrimeBossBag PlanteraBossBag GolemBossBag FishronBossBag CultistBossBag FairyQueenBossBag QueenSlimeBossBag BossBagBetsy CobaltOre PalladiumOre MythrilOre OrichalcumOre AdamantiteOre TitaniumOre ChlorophyteOre CobaltBar PalladiumBar MythrilBar OrichalcumBar AdamantiteBar TitaniumBar ChlorophyteBar HallowedBar ShroomiteBar SpectreBar SoulofLight SoulofNight SoulofFlight SoulofMight SoulofSight SoulofFright CrystalShard PixieDust UnicornHorn CrystalBullet CursedFlame Ichor Ectoplasm TruffleWorm LifeFruit FragmentSolar FragmentVortex FragmentNebula FragmentStardust SolarTablet LihzahrdPowerCell MythrilAnvil OrichalcumAnvil AdamantiteForge TitaniumForge LunarCraftingStation FairyBell MagicalHarp Harp SpellTome EmptyDropper ExplosivePowder SteampunkBoiler BlendOMatic Autohammer CursedFlames GoldenShower CrystalStorm ClingerStaff RodofDiscord DaedalusStormbow FetidBaghnakhs FlyingKnife CrystalVileShard DartPistol DartRifle ChainGuillotines PutridScent FleshKnuckles TendonHook IlluminantHook WormHook IceSickle BeamSword Frostbrand IceBow Marrow Uzi SniperRifle TacticalShotgun RifleScope ChainGun RocketLauncher GrenadeLauncher ProximityMineLauncher CoinGun PirateStaff Cutlass PirateMap Amarok HelFire FormatC Gradient Chik Yelets Kraken TheEyeOfCthulhu DeathSickle BrokenHeroSword TerraBlade FlowerofFrost FrostStaff PoisonStaff VenomStaff SpiderFang AncientBattleArmorMaterial AncientBattleArmorMaterial FrostCore LightShard DarkShard BlackFairyDust TatteredBeeWing ButterflyDust GiantHarpyFeather IceFeather FireFeather SpookyTwig SpookyWood BeetleHusk ScalyTruffle AvengerEmblem WarriorEmblem RangerEmblem SorcererEmblem SummonerEmblem Pwnhammer ClockworkAssaultRifle BreakerBlade LaserRifle HallowedKey JungleKey CorruptionKey CrimsonKey FrozenKey DungeonDesertKey VampireKnives ScourgeoftheCorruptor RainbowGun StaffoftheFrostHydra PiranhaGun StormTigerStaff FlameWings LeafWings Jetpack SteampunkWings BoneWings FrozenWings ButterflyWings BeeWings QueenSlimeBossBag QueenSlimeCrystal Smolstar GelBalloon SparkleGuitar CrystalNinjaHelmet CrystalNinjaChestplate CrystalNinjaLeggings SanguineStaff SharpTears BloodLustCluster DripplerFlail BouncingShield BouncingShield FireWhip CoolWhip SwordWhip ScytheWhip RainbowWhip MaceWhip EmpressBlade FairyQueenMagicItem FairyQueenRangedItem FairyQueenBossBag RainbowWhip Seedler TheAxe WaspGun LeafBlower GrenadeLauncher VenusMagnum NettleBurst FlowerPow GolemFist PossessedHatchet Stynger StaffofEarth HeatRay EyeoftheGolem Picksaw TempestStaff Flairon RazorbladeTyphoon BubbleGun Tsunami ShrimpyTruffle BetsyWings DD2BetsyBow DD2SquireBetsySword ApprenticeStaffT3 MonkStaffT3 Razorpine BlizzardStaff NorthPole ChristmasTreeSword SnowmanCannon CandyCornRifle JackOLanternLauncher BatScepter RavenStaff TheHorsemansBlade StakeLauncher SpookyHook SpookyWings WitchBroom RainbowRod NeptunesShell FrozenTurtleShell TurtleShell MoonStone SunStone PaladinsShield AnkhShield DiscountCard LuckyCoin GoldRing PirateShipMountItem PygmyStaff TikiMask TikiShirt TikiPants CrystalSerpent ObsidianSwordfish Toxikarp BlessedApple CorruptFishingCrateHard CrimsonFishingCrateHard DungeonFishingCrateHard FloatingIslandFishingCrateHard FrozenCrateHard HallowedFishingCrate HallowedFishingCrateHard JungleFishingCrateHard LavaCrateHard OceanCrateHard OasisCrateHard GoldenCrateHard IronCrateHard WoodenCrateHard");
		Mark(BookStage.PostHardmode, "TeleportationPylonVictory MoonLordPetItem MoonLordMasterTrophy LunarOre LunarBar Meowmere StarWrath Terrarian SDMG LastPrism LunarFlareBook RainbowCrystalStaff MoonlordTurretStaff Celeb2 RainbowCrystalStaff MoonLordBossBag MoonLordTrophy BossMaskMoonlord GravityGlobe SuspiciousLookingTentacle PortalGun RodOfHarmony BottomlessShimmerBucket Clentaminator2 ");
		foreach (Item item in ContentSamples.ItemsByType.Values)
		{
			if (item == null || item.IsAir || item.type <= 0)
			{
				continue;
			}
			BookStage modStage = BookProgressionSources.ItemStage(item.ModItem?.Mod.Name, item.ModItem?.Name);
			if (modStage != BookStage.Unknown)
			{
				floors[item.type] = modStage;
			}
			if (floors.TryGetValue(item.type, out var stage))
			{
				seeds[item.type] = stage;
				continue;
			}
			if (item.ModItem == null)
			{
				BookStage natural = BookProgressionSources.VanillaNaturalStage(ItemID.Search.GetName(item.type));
				if (natural != BookStage.Unknown)
				{
					seeds[item.type] = natural;
					continue;
				}
			}
			if (item.ModItem != null || recipesByItem.ContainsKey(item.type))
			{
				continue;
			}
			int type = item.type;
			int value;
			if (!item.vanity && item.rare >= 4)
			{
				int type2 = item.type;
				if (type2 != 1309 && type2 != 678 && type2 != 1927 && type2 != 3628 && type2 != 4365)
				{
					value = 1;
					goto IL_01e4;
				}
			}
			value = 0;
			goto IL_01e4;
			IL_01e4:
			seeds[type] = (BookStage)value;
		}
		Dictionary<int, int[]> stationIndex = (from i in ContentSamples.ItemsByType.Values
			where i != null && !i.IsAir && i.createTile >= 0
			group i by i.createTile).ToDictionary((IGrouping<int, Item> g) => g.Key, (IGrouping<int, Item> g) => g.Select((Item i) => i.type).ToArray());
		List<BookProgressionRules.Route> routes = new List<BookProgressionRules.Route>();
		AddRegisteredDropSources(seeds, routes);
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
			bool uncertain = false;
			foreach (int tile in r.requiredTile.Where((int t) => t >= 0))
			{
				if (((uint)(tile - 133) <= 1u || tile == 412) ? true : false)
				{
					floor = (BookStage)Math.Max((int)floor, 1);
					continue;
				}
				int[] stationItems = stationIndex.GetValueOrDefault(tile) ?? Array.Empty<int>();
				if (stationItems.Length != 0)
				{
					inputs.Add(stationItems);
				}
				else if (tile >= TileID.Count)
				{
					uncertain = true;
				}
			}
			foreach (Condition condition in r.Conditions)
			{
				string key = condition?.Description?.Key;
				BookStage hint = BookProgressionSources.ConditionStage(key);
				if (hint != BookStage.Unknown)
				{
					floor = (BookStage)Math.Max((int)floor, (int)hint);
				}
				else if (string.IsNullOrEmpty(key) || !key.StartsWith("Conditions.", StringComparison.Ordinal))
				{
					uncertain = true;
				}
			}
			routes.Add(new BookProgressionRules.Route(index, r.createItem.type, floor, inputs.ToArray(), uncertain));
		}
		(Dictionary<int, BookStage> Items, Dictionary<int, BookStage> Recipes) tuple = BookProgressionRules.Resolve(seeds, floors, routes);
		items = tuple.Items;
		recipes = tuple.Recipes;
		recipeMinimums = routes.Where((BookProgressionRules.Route route) => route.Key >= 0).ToDictionary((BookProgressionRules.Route route) => route.Key, (BookProgressionRules.Route route) => BookProgressionRules.MinimumStage(route, items, floors));
		ILog logger = ModContent.GetInstance<ModernRecipeBook>().Logger;
		logger.Info("Progression audit items: " + Totals(from i in ContentSamples.ItemsByType.Values
			where i != null && !i.IsAir
			select ItemStage(i.type)));
		logger.Info("Progression audit recipes: " + Totals(indexes.Select(RecipeStage)));
		logger.Info("Progression audit Calamity items: " + Totals(from i in ContentSamples.ItemsByType.Values
			where i?.ModItem?.Mod.Name == "CalamityMod"
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

	private static void AddRegisteredDropSources(Dictionary<int, BookStage> seeds, List<BookProgressionRules.Route> routes)
	{
		try
		{
			foreach (KeyValuePair<int, NPC> pair in ContentSamples.NpcsByNetId)
			{
				NPC npc = pair.Value;
				BookStage stage = BookProgressionSources.NpcStage(npc.ModNPC?.Mod.Name, npc.ModNPC?.Name);
				if (stage == BookStage.Unknown)
				{
					continue;
				}
				foreach (DropRateInfo rate in Reports(Main.ItemDropsDB.GetRulesForNPCID(pair.Key, includeGlobalDrops: false)))
				{
					if (!seeds.TryGetValue(rate.itemId, out var old) || old > stage)
					{
						seeds[rate.itemId] = stage;
					}
				}
			}
			int sourceKey = -1;
			foreach (KeyValuePair<int, Item> pair2 in ContentSamples.ItemsByType)
			{
				if (pair2.Value == null || pair2.Value.IsAir)
				{
					continue;
				}
				foreach (DropRateInfo rate2 in Reports(Main.ItemDropsDB.GetRulesForItemID(pair2.Key)))
				{
					BookStage floor = BookStage.PreHardmode;
					bool uncertain = false;
					foreach (IItemDropRuleCondition item in rate2.conditions ?? new List<IItemDropRuleCondition>())
					{
						string name = item?.GetType().Name ?? "";
						BookStage hint = BookProgressionSources.ConditionStage(name);
						if (hint != BookStage.Unknown)
						{
							floor = (BookStage)Math.Max((int)floor, (int)hint);
							continue;
						}
						switch (name)
						{
						case "IsExpert":
						case "NotExpert":
						case "IsMasterMode":
						case "NotMasterMode":
							continue;
						}
						uncertain = true;
					}
					routes.Add(new BookProgressionRules.Route(sourceKey--, rate2.itemId, floor, new int[1][] { new int[1] { pair2.Key } }, uncertain));
				}
			}
		}
		catch (Exception)
		{
		}
		static IEnumerable<DropRateInfo> Reports(IEnumerable<IItemDropRule> rules)
		{
			List<DropRateInfo> rates = new List<DropRateInfo>();
			foreach (IItemDropRule rule in rules)
			{
				try
				{
					rule.ReportDroprates(rates, new DropRateInfoChainFeed(1f));
				}
				catch (Exception)
				{
				}
			}
			return rates.Where((DropRateInfo r) => r.itemId > 0 && r.dropRate > 0f);
		}
	}
}
