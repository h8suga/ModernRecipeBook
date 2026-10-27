using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using log4net;

namespace ModernRecipeBook.Progression;

// Earliest progression step (BookSteps: the bosses in Boss Checklist order) at which each item can be obtained:
// shortest path over recipes, drops, shops and mining, seeded by curated lists, bosses' and mobs' steps and, where
// nothing better is known, the item's rarity tier. The three stages are buckets of steps.
internal static class BookProgression
{
	private static Dictionary<int, int> items = new Dictionary<int, int>();

	private static Dictionary<int, int> recipes = new Dictionary<int, int>();

	private static Dictionary<int, int> recipeMinimums = new Dictionary<int, int>();

	private static Dictionary<int, string> reasons = new Dictionary<int, string>();

	// Why an item has its step: the route that placed it, else its seed's reason and source (an NPC net id).
	private static Dictionary<int, BookProgressionRules.Route> placedBy = new Dictionary<int, BookProgressionRules.Route>();

	private static Dictionary<int, int> seedSources = new Dictionary<int, int>();

	private static Dictionary<int, BookProgressionRules.Route> recipeRoutes = new Dictionary<int, BookProgressionRules.Route>();

	private static Dictionary<int, int> itemFloors = new Dictionary<int, int>();

	private static HashSet<int> modeExcludedItems = new HashSet<int>();

	private static HashSet<int> modeExcludedRecipes = new HashSet<int>();

	private static Dictionary<int, int> npcs = new Dictionary<int, int>();

	// Whether the last rebuild had the spawn gates (a multiplayer client probes once its tiles are in).
	internal static bool UsedGates { get; private set; }

	internal static bool Built { get; private set; }

	// Unknown items and recipes whose source exists, but not at the current world's difficulty (a boss bag in
	// Classic); every other Unknown has no regular way to be obtained.
	internal static bool ItemModeExcluded(int type)
	{
		return ItemStep(type) < 0 && modeExcludedItems.Contains(type);
	}

	internal static bool RecipeModeExcluded(int index)
	{
		return RecipeStep(index) < 0 && modeExcludedRecipes.Contains(index);
	}

	internal static int ItemStep(int type)
	{
		return items.TryGetValue(type, out int s) ? s : -1;
	}

	internal static int RecipeStep(int index)
	{
		return recipes.TryGetValue(index, out int s) ? s : -1;
	}

	internal static int RecipeMinimumStep(int index)
	{
		return recipeMinimums.GetValueOrDefault(index, 0);
	}

	internal static BookStage ItemStage(int type)
	{
		return BookSteps.StageOf(ItemStep(type));
	}

	internal static BookStage RecipeStage(int index)
	{
		return BookSteps.StageOf(RecipeStep(index));
	}

	// The step an NPC appears at: the curated tables, else what it always drops; anything left (critters, most town
	// NPCs, mods' enemies without loot) is about from the start. A gate in the mob's own spawn code (BookSpawnGates: a
	// Calamity mob that waits for Providence) can only make it later.
	internal static int NpcStep(int netId)
	{
		int type = ContentSamples.NpcsByNetId.TryGetValue(netId, out NPC npc) ? npc.type : netId;
		return Math.Max(npcs.TryGetValue(netId, out int s) && s >= 0 ? s : 0, GateStep(type));
	}

	internal static BookStage NpcStage(int netId)
	{
		return BookSteps.StageOf(NpcStep(netId));
	}

	private static int GateStep(int type)
	{
		return BookSteps.Gate(BookSpawnGates.Of(type));
	}

	// Dev audit for the test stand: one TSV line per item (type, mod, internal name, rarity, stage, reason,
	// display name, step, step's boss).
	internal static IEnumerable<string> Audit()
	{
		return from i in ContentSamples.ItemsByType.Values
			where i != null && !i.IsAir
			let step = ItemStep(i.type)
			select string.Join('\t', i.type, i.ModItem?.Mod.Name ?? "Terraria", ItemName(i), i.rare, ItemStage(i.type), reasons.GetValueOrDefault(i.type, ""), Lang.GetItemNameValue(i.type), step, StepKey(step));
	}

	// Dev audit: items and recipes per step, how many each reason placed, and the unknowns.
	internal static IEnumerable<string> StepAudit()
	{
		Item[] all = ContentSamples.ItemsByType.Values.Where((Item i) => i != null && !i.IsAir).ToArray();
		yield return $"steps={BookSteps.All.Count - 1} from={BookSteps.Source} hardmode={BookSteps.HardmodeStep} moonlord={BookSteps.MoonLordStep} gates={UsedGates}";
		for (int k = 0; k < BookSteps.All.Count; k++)
		{
			int[] here = all.Where((Item i) => ItemStep(i.type) == k).Select((Item i) => i.type).ToArray();
			int rarity = here.Count((int t) => reasons.GetValueOrDefault(t, "") == "rarity");
			yield return string.Join('\t', k, StepKey(k), BookSteps.StageOf(k), "items=" + here.Length, "rarity=" + rarity, "recipes=" + recipes.Count((KeyValuePair<int, int> r) => r.Key >= 0 && r.Value == k));
		}
		int[] unknown = all.Where((Item i) => ItemStep(i.type) < 0).Select((Item i) => i.type).ToArray();
		yield return string.Join('\t', -1, "unknown", "", "items=" + unknown.Length, "mode=" + unknown.Count(modeExcludedItems.Contains), "recipes=" + recipes.Count((KeyValuePair<int, int> r) => r.Key >= 0 && r.Value < 0));
	}

	private static string StepKey(int step)
	{
		return step < 0 ? "unknown" : (step == 0 ? "start" : (step < BookSteps.All.Count ? BookSteps.All[step].Key : "?"));
	}

	// Dev audit: every source considered for an item. Set TraceSources before the book rebuilds.
	internal static bool TraceSources;

	private static Dictionary<int, List<string>> trace = new Dictionary<int, List<string>>();

	private static List<BookProgressionRules.Route> lastRoutes = new List<BookProgressionRules.Route>();

	// "CalamityMod/ScoriaBar" or a vanilla internal name, for the stand.
	internal static IEnumerable<string> Explain(string name)
	{
		int type = name.Contains('/') ? (ModContent.TryFind(name, out ModItem item) ? item.Type : 0) : (ItemID.Search.TryGetId(name, out int id) ? id : 0);
		return Explain(type);
	}

	internal static IEnumerable<string> Explain(int type)
	{
		yield return $"step {ItemStep(type)} {StepKey(ItemStep(type))} ({ItemStage(type)}) reason={reasons.GetValueOrDefault(type, "")}";
		foreach (Link link in Chain(type))
		{
			yield return $"chain {ItemName(ContentSamples.ItemsByType[link.Item])} {link.Kind}:{link.Id} -> {link.Step} {StepKey(link.Step)}";
		}
		foreach (string seed in trace.GetValueOrDefault(type) ?? new List<string>())
		{
			yield return "seed " + seed;
		}
		foreach (BookProgressionRules.Route r in lastRoutes.Where((BookProgressionRules.Route route) => route.Output == type))
		{
			yield return $"route {r.Source ?? ("recipe:" + r.Key)} floor={r.Floor} uncertain={r.Uncertain} -> {(r.Key >= 0 ? RecipeStep(r.Key) : -2)}: " + string.Join(" + ", r.Ingredients.Select((int[] g) => string.Join("|", g.OrderBy(ItemStepOrLast).Take(4).Select((int t) => ItemName(ContentSamples.ItemsByType[t]) + "=" + ItemStep(t)))));
		}
	}

	// Dev audit: every item against its own sources (set TraceSources before the book rebuilds). A source counts at its
	// own step: a route (recipe, container, mining...) once every ingredient group and the station are in, a seed raised
	// to the item's floor; a rarity guess, or a route standing only as a post-Moon Lord lower bound, is no source. One
	// line per item that breaks the rule, with its chain: "early" sits below every real source (a guess undercut it),
	// "late" above its earliest source, "guess" is placed by a guess though it has routes that never open.
	internal static IEnumerable<string> ConsistencyAudit()
	{
		if (!TraceSources)
		{
			yield return "set ModernRecipeBook.Progression.BookProgression.TraceSources=true and call ModernRecipeBook.UI.Book.RecipeBookSystem.Rebuild first";
			yield break;
		}
		ILookup<int, BookProgressionRules.Route> byOutput = lastRoutes.ToLookup((BookProgressionRules.Route r) => r.Output);
		List<(string Kind, string Line)> found = new List<(string, string)>();
		foreach (Item item in ContentSamples.ItemsByType.Values.Where((Item i) => i != null && !i.IsAir))
		{
			int type = item.type, step = ItemStep(type), floor = itemFloors.GetValueOrDefault(type);
			List<(string Name, int Step, bool Real)> sources = (from seed in trace.GetValueOrDefault(type) ?? new List<string>()
				let at = seed.LastIndexOf('=')
				let name = seed.Substring(0, at)
				select (name, Max(int.Parse(seed.Substring(at + 1)), floor), name != "rarity")).ToList();
			foreach (BookProgressionRules.Route r in byOutput[type])
			{
				bool open = !r.Uncertain && r.Floor >= 0 && r.Ingredients.All((int[] g) => g.Any((int t) => ItemStep(t) >= 0));
				sources.Add((r.Source ?? ("recipe:" + r.Key), RecipeStep(r.Key), open));
			}
			(string Name, int Step, bool Real) real = sources.Where(s => s.Real && s.Step >= 0).OrderBy(s => s.Step).FirstOrDefault(((string)null, -1, false));
			int any = sources.Where(s => s.Step >= 0).Select(s => s.Step).DefaultIfEmpty(-1).Min();
			string kind = step < 0 ? (any >= 0 ? "late" : null)
				: real.Step >= 0 && step < real.Step ? "early"
				: real.Step < 0 && byOutput[type].Any() ? "guess"
				: step > any ? "late" : null;
			if (kind != null)
			{
				found.Add((kind, string.Join('\t', kind, type, item.ModItem?.Mod.Name ?? "Terraria", ItemName(item), step, StepKey(step), reasons.GetValueOrDefault(type, ""), real.Name == null ? "-" : real.Name + "=" + real.Step + " " + StepKey(real.Step), string.Join(" < ", Chain(type).Select((Link l) => $"{ItemName(ContentSamples.ItemsByType[l.Item])} {l.Kind}:{l.Id}={l.Step}")))));
			}
		}
		yield return "violations=" + found.Count + " " + string.Join(" ", found.GroupBy(f => f.Kind).Select(g => g.Key + "=" + g.Count()));
		foreach ((string _, string line) in found)
		{
			yield return line;
		}
	}

	private static int ItemStepOrLast(int type)
	{
		int step = ItemStep(type);
		return step < 0 ? int.MaxValue : step;
	}

	// One link of an item's "why": the item, how it was placed (a recipe, a route's source, a seed's reason, "gate"
	// for a route's own floor) and what that names (a recipe index, the next item, an NPC net id, a step).
	internal readonly record struct Link(int Item, string Kind, int Id, int Step);

	// From the item down to what fixes its step: the route that placed it, through the ingredient that came last,
	// until a seed or a route's own floor (a condition, a world gate) decides.
	// A recipe's chain starts from that recipe instead of whatever placed its item.
	internal static List<Link> Chain(int type, int recipe = -1, int depth = 6)
	{
		List<Link> links = new List<Link>();
		HashSet<int> seen = new HashSet<int>();
		BookProgressionRules.Route route = recipe >= 0 ? recipeRoutes.GetValueOrDefault(recipe) : null;
		while (links.Count < depth && seen.Add(type))
		{
			int step = route != null && route.Key >= 0 ? RecipeStep(route.Key) : ItemStep(type);
			if (route == null && !placedBy.TryGetValue(type, out route))
			{
				string reason = reasons.GetValueOrDefault(type, "");
				int colon = reason.IndexOf(':');
				links.Add(new Link(type, colon >= 0 ? reason.Substring(0, colon) : reason, seedSources.GetValueOrDefault(type), step));
				break;
			}
			// Route sources: "drop:<container>" is a bag or crate, "shop:" stock that needs a carried item.
			string kind = route.Key >= 0 ? "recipe" : (route.Source ?? "").Split(':')[0] switch
			{
				"drop" => "bag",
				"shop" => "carried",
				string other => other
			};
			int next = -1, nextStep = -1;
			foreach (int[] group in route.Ingredients)
			{
				int best = -1, bestStep = -1;
				foreach (int t in group)
				{
					int s = ItemStep(t);
					if (s >= 0 && (bestStep < 0 || s < bestStep))
					{
						best = t;
						bestStep = s;
					}
				}
				if (bestStep > nextStep)
				{
					next = best;
					nextStep = bestStep;
				}
			}
			int floor = Math.Max(route.Floor, itemFloors.GetValueOrDefault(route.Output));
			if (next < 0 || floor >= nextStep)
			{
				links.Add(new Link(type, kind, route.Key >= 0 ? route.Key : next, step));
				links.Add(new Link(type, "gate", floor, floor));
				break;
			}
			links.Add(new Link(type, kind, route.Key >= 0 ? route.Key : next, step));
			type = next;
			route = null;
		}
		return links;
	}

	// Dev audit: one TSV line per NPC (net id, mod, internal name, display name, curated stage, step, step's boss).
	internal static IEnumerable<string> NpcAudit()
	{
		return from pair in ContentSamples.NpcsByNetId
			let npc = pair.Value
			let step = NpcStep(pair.Key)
			select string.Join('\t', pair.Key, npc.ModNPC?.Mod.Name ?? "Terraria", npc.ModNPC?.Name ?? (NPCID.Search.TryGetName(npc.type, out var name) ? name : ""), npc.TypeName, BookProgressionSources.NpcStage(npc), step, StepKey(step));
	}

	// Dev audit: an NPC's reported drops with their conditions and the step each condition gives.
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
			select string.Join('\t', ItemName(item), item.rare, item.maxStack, string.Join(", ", (r.conditions ?? new List<IItemDropRuleCondition>()).Select((IItemDropRuleCondition c) => c.GetType().Name + "=" + (BookProgressionSources.DropConditionStep(c)?.ToString() ?? "opaque"))));
	}

	internal static void Clear()
	{
		items.Clear();
		recipes.Clear();
		recipeMinimums.Clear();
		reasons.Clear();
		placedBy.Clear();
		seedSources.Clear();
		recipeRoutes.Clear();
		itemFloors.Clear();
		modeExcludedItems.Clear();
		modeExcludedRecipes.Clear();
		npcs.Clear();
		Built = false;
		UsedGates = false;
		BookSteps.Clear();
		BookProgressionSources.Clear();
	}

	internal static void Rebuild(List<int> indexes, Dictionary<int, List<int>> recipesByItem)
	{
		Stopwatch timer = Stopwatch.StartNew();
		BookSteps.EnsureBuilt();
		// The spawn gates (modded mobs) and Calamity's Acid Rain tables place enemies on their steps; both need a world.
		try
		{
			BookBestiary.EnsureBuilt();
		}
		catch (Exception exception)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("MRB bestiary index failed; enemies keep their curated stages.", exception);
		}
		UsedGates = BookSpawnGates.Built;
		BookItemSources.EnsureBuilt();
		int probedCount = ProbeOpaqueConditions(indexes);
		int hardmode = BookSteps.HardmodeStep, post = BookSteps.MoonLordStep;
		Dictionary<int, int> floors = new Dictionary<int, int>();
		Dictionary<int, int> seeds = new Dictionary<int, int>();
		Dictionary<int, string> why = new Dictionary<int, string>();
		Dictionary<int, int> sources = new Dictionary<int, int>();
		Dictionary<int, List<string>> traced = TraceSources ? new Dictionary<int, List<string>>() : null;
		Dictionary<string, int[]> names = BookProgressionRules.BuildNameIndex(from i in ContentSamples.ItemsByType.Values
			where i != null && !i.IsAir && i.ModItem == null
			select new KeyValuePair<string, int>(ItemID.Search.GetName(i.type) ?? ("#" + i.type), i.type));
		// Floors: nothing comes earlier than its stage. They seed an item only when nothing else places it (below), so
		// the real sources set its step within the stage (Hallowed Bar: the first mechanical boss, not the Wall of Flesh).
		// None for what Calamity's chests hold before hardmode (BookChestLoot): Vampire Knives and the Scourge of the Corruptor
		// on the sky island of the other evil, the shrines' Corruption, Crimson, Frozen and Desert keys and Truffle Worms, the
		// Forsaken Archive's Spell Tomes; without Calamity their sources come in hardmode or later anyway.
		Mark(hardmode, "CrystalBall PearlstoneBlock PearlsandBlock PinkIceBlock HallowedSeeds HallowHardenedSand HallowSandstone BlueSolution PurpleSolution RedSolution DarkBlueSolution LihzahrdFurnace FleshCloningVaat ZapinatorOrange LongRainbowTrailWings DemonHeart WallOfFleshBossBag WallOfFleshGoatMountItem LizardEgg DestroyerBossBag TwinsBossBag SkeletronPrimeBossBag PlanteraBossBag GolemBossBag FishronBossBag CultistBossBag FairyQueenBossBag QueenSlimeBossBag BossBagBetsy CobaltOre PalladiumOre MythrilOre OrichalcumOre AdamantiteOre TitaniumOre ChlorophyteOre CobaltBar PalladiumBar MythrilBar OrichalcumBar AdamantiteBar TitaniumBar ChlorophyteBar HallowedBar ShroomiteBar SpectreBar SoulofLight SoulofNight SoulofFlight SoulofMight SoulofSight SoulofFright CrystalShard PixieDust UnicornHorn CrystalBullet CursedFlame Ichor Ectoplasm LifeFruit FragmentSolar FragmentVortex FragmentNebula FragmentStardust SolarTablet LihzahrdPowerCell MythrilAnvil OrichalcumAnvil AdamantiteForge TitaniumForge LunarCraftingStation FairyBell MagicalHarp Harp EmptyDropper ExplosivePowder SteampunkBoiler BlendOMatic Autohammer CursedFlames GoldenShower CrystalStorm ClingerStaff RodofDiscord DaedalusStormbow FetidBaghnakhs FlyingKnife CrystalVileShard DartPistol DartRifle ChainGuillotines PutridScent FleshKnuckles TendonHook IlluminantHook WormHook IceSickle BeamSword Frostbrand IceBow Marrow Uzi SniperRifle TacticalShotgun RifleScope ChainGun RocketLauncher GrenadeLauncher ProximityMineLauncher CoinGun PirateStaff Cutlass PirateMap Amarok HelFire FormatC Gradient Chik Yelets Kraken TheEyeOfCthulhu DeathSickle BrokenHeroSword TerraBlade FlowerofFrost FrostStaff PoisonStaff VenomStaff SpiderFang AncientBattleArmorMaterial AncientBattleArmorMaterial FrostCore LightShard DarkShard BlackFairyDust TatteredBeeWing ButterflyDust IceFeather FireFeather SpookyTwig SpookyWood BeetleHusk ScalyTruffle AvengerEmblem WarriorEmblem RangerEmblem SorcererEmblem SummonerEmblem Pwnhammer ClockworkAssaultRifle BreakerBlade LaserRifle HallowedKey JungleKey RainbowGun StaffoftheFrostHydra PiranhaGun StormTigerStaff FlameWings LeafWings Jetpack SteampunkWings BoneWings FrozenWings ButterflyWings BeeWings QueenSlimeBossBag QueenSlimeCrystal Smolstar GelBalloon SparkleGuitar CrystalNinjaHelmet CrystalNinjaChestplate CrystalNinjaLeggings SanguineStaff SharpTears BloodLustCluster DripplerFlail BouncingShield BouncingShield FireWhip CoolWhip SwordWhip ScytheWhip RainbowWhip MaceWhip EmpressBlade FairyQueenMagicItem FairyQueenRangedItem FairyQueenBossBag RainbowWhip Seedler TheAxe WaspGun LeafBlower GrenadeLauncher VenusMagnum NettleBurst FlowerPow GolemFist PossessedHatchet Stynger StaffofEarth HeatRay EyeoftheGolem Picksaw TempestStaff Flairon RazorbladeTyphoon BubbleGun Tsunami ShrimpyTruffle BetsyWings DD2BetsyBow DD2SquireBetsySword ApprenticeStaffT3 MonkStaffT3 Razorpine BlizzardStaff NorthPole ChristmasTreeSword SnowmanCannon CandyCornRifle JackOLanternLauncher BatScepter RavenStaff TheHorsemansBlade StakeLauncher SpookyHook SpookyWings WitchBroom RainbowRod NeptunesShell FrozenTurtleShell TurtleShell MoonStone SunStone PaladinsShield AnkhShield DiscountCard LuckyCoin GoldRing PirateShipMountItem PygmyStaff TikiMask TikiShirt TikiPants CrystalSerpent ObsidianSwordfish Toxikarp BlessedApple CorruptFishingCrateHard CrimsonFishingCrateHard DungeonFishingCrateHard FloatingIslandFishingCrateHard FrozenCrateHard HallowedFishingCrate HallowedFishingCrateHard JungleFishingCrateHard LavaCrateHard OceanCrateHard OasisCrateHard GoldenCrateHard IronCrateHard WoodenCrateHard");
		Mark(post, "TeleportationPylonVictory MoonLordPetItem MoonLordMasterTrophy LunarOre LunarBar Meowmere StarWrath Terrarian SDMG LastPrism LunarFlareBook RainbowCrystalStaff MoonlordTurretStaff Celeb2 RainbowCrystalStaff MoonLordBossBag MoonLordTrophy BossMaskMoonlord GravityGlobe SuspiciousLookingTentacle PortalGun RodOfHarmony BottomlessShimmerBucket Clentaminator2 ");
		// Hardmode-only world tiles, plants, fish and pickups that carry White to Orange rarity (Hallow trees and
		// fish, the Jungle Temple and its traps, Strange Plants, Martian probes); checked against the wiki's
		// "Hardmode-only items" category, but for the Constellation painting, which sky island chests hold from world
		// creation (WorldGen.cs:33108-33136).
		Mark(hardmode, "Pearlwood LihzahrdBrick LihzahrdAltar LihzahrdWallUnsafe MartianConduitPlating MartianSink MechdusaSummon StrangePlant1 StrangePlant2 StrangePlant3 StrangePlant4 Dragonfruit Starfruit PrincessFish Prismite FlameTrap SpearTrap SpikyBallTrap SuperDartTrap FleshBlock LesionBlock LivingFireBlock NebulaPickup1 NebulaPickup2 NebulaPickup3");
		List<Item> all = ContentSamples.ItemsByType.Values.Where((Item i) => i != null && !i.IsAir && i.type > 0).ToList();
		foreach (Item item in all)
		{
			BookStage curated = BookProgressionSources.ItemStage(item);
			if (curated != BookStage.Unknown)
			{
				floors[item.type] = Max(BookSteps.Of(curated), floors.GetValueOrDefault(item.type));
				// Calamity's pre-hardmode and hardmode lists name what is there at that stage's start (Draedon lab
				// materials, the Astral Infection's blocks); its post-Moon Lord list is a floor like the vanilla lists.
				if (curated != BookStage.PostHardmode)
				{
					Seed(item.type, floors[item.type], "list");
				}
			}
			if (!floors.ContainsKey(item.type) && item.ModItem == null)
			{
				Seed(item.type, BookSteps.Of(BookProgressionSources.VanillaNaturalStage(ItemID.Search.GetName(item.type))), "list");
			}
			Seed(item.type, BookSteps.Of(BookProgressionSources.WorldgenStage(item)), "worldgen");
		}
		List<BookProgressionRules.Route> routes = new List<BookProgressionRules.Route>();
		HashSet<int> modeItems = new HashSet<int>();
		HashSet<int> modeRecipes = new HashSet<int>();
		HashSet<int> sourced = AddDropSources(routes, floors, modeItems, recipesByItem, Seed);
		AddMining(all, routes, sourced);
		AddFishing(routes, sourced, Seed);
		AddGrown(sourced, Seed);
		// Raw items: nothing crafts them and no known source drops them, so their rarity tier is the best guess. What only
		// another difficulty or seed gives (Levi in Death mode, the Zenith world's Lava Chicken Broth) is no guess but
		// unavailable here.
		foreach (Item item in all)
		{
			if (recipesByItem.ContainsKey(item.type) || sourced.Contains(item.type) || modeItems.Contains(item.type))
			{
				continue;
			}
			if (OnlyOtherWorlds(item.type))
			{
				modeItems.Add(item.type);
				continue;
			}
			Seed(item.type, BookSteps.Of(BookProgressionSources.RarityStage(item)), "rarity");
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
			int floor = 0;
			bool opaque = false;
			foreach (int tile in r.requiredTile.Where((int t) => t >= 0))
			{
				// The hardmode anvils and forges and the Ancient Manipulator: at least hardmode, and as late as the
				// earliest item that places them (Calamity's rework puts Mythril after a mechanical boss).
				if (tile == TileID.MythrilAnvil || tile == TileID.AdamantiteForge || tile == TileID.LunarCraftingStation)
				{
					floor = Max(floor, hardmode);
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
				int? hint = BookProgressionSources.ConditionStep(condition);
				if (BookProgressionSources.ModeBlocked(condition?.Description?.Key, () => condition.IsMet()))
				{
					modeRecipes.Add(index);
					modeItems.Add(r.createItem.type);
				}
				if (hint < 0)
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
				int hint2 = BookSteps.Of(BookProgressionSources.RarityStage(r.createItem));
				if (hint2 < 0)
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
		Dictionary<int, BookProgressionRules.Route> via = new Dictionary<int, BookProgressionRules.Route>();
		(items, recipes) = BookProgressionRules.Resolve(seeds, floors, routes, via);
		// Blocks dug out of the world (mods' tiles, vanilla ores) are also floored at the first pickaxe strong enough
		// for them, as other sources only guess (Voidstone: a drop of an Abyss mob the tables cannot place, by its
		// White rarity; the 180 pickaxe makes it hardmode).
		var picks = (from i in all
			where i.pick > 0 && items.ContainsKey(i.type)
			select (Power: i.pick, Step: items[i.type])).ToList();
		bool raised = false;
		foreach (Item item in all)
		{
			int tile = item.createTile;
			int minPick = (tile >= TileID.Count || (tile >= 0 && BookWorldSources.Natural(tile))) ? BookWorldSources.PickPower(tile) : 0;
			if (minPick <= 0)
			{
				continue;
			}
			int pick = picks.Where(p => p.Power >= minPick).Select(p => p.Step).DefaultIfEmpty(-1).Min();
			if (pick > floors.GetValueOrDefault(item.type))
			{
				floors[item.type] = pick;
				if (seeds.TryGetValue(item.type, out int mined) && mined < pick)
				{
					seeds[item.type] = pick;
					why[item.type] = "pickaxe:" + minPick;
					sources[item.type] = minPick;
				}
				raised = true;
			}
		}
		if (raised)
		{
			(items, recipes) = BookProgressionRules.Resolve(seeds, floors, routes, via);
		}
		// Second pass: what the graph could not place gets a guess, its curated floor (Pearlwood) or else its rarity tier,
		// but only where none of its own sources can still open: an item without a route, or a ring of items made only of
		// one another whose real source the tables do not hold (Cactus with its platform and wall, the shimmered buckets).
		// What is made of such an item gets no guess: the graph runs again and its recipe sets its step (a Cactus
		// Bookcase is as late as its Book, a Solar Sink as its Solar Bricks), so a guess never undercuts a real source.
		// What only something the world's difficulty rules out makes (expert-only bag contents in Classic) stays out
		// as that too, not sourceless; listing it would open its loot.
		ILookup<int, BookProgressionRules.Route> producers = routes.Where((BookProgressionRules.Route r) => !r.Uncertain && r.Floor >= 0).ToLookup((BookProgressionRules.Route r) => r.Output);
		HashSet<int> hopeless = new HashSet<int>();
		while (true)
		{
			HashSet<int> open = all.Select((Item i) => i.type).Where((int t) => !items.ContainsKey(t) && !modeItems.Contains(t) && !hopeless.Contains(t)).ToHashSet();
			if (open.Count == 0)
			{
				break;
			}
			// What each waits for: the unplaced members of the ingredient groups that keep its routes shut (a route one of
			// whose groups can never fill, all of it excluded or hopeless, waits for nothing).
			Dictionary<int, int[]> waits = open.ToDictionary((int t) => t, (int t) => (from r in producers[t]
				let shut = r.Ingredients.Where((int[] g) => !g.Any(items.ContainsKey)).ToList()
				where shut.All((int[] g) => g.Any(open.Contains))
				from g in shut
				from m in g
				where open.Contains(m)
				select m).Distinct().ToArray());
			int seeded = seeds.Count;
			foreach (List<int> ring in BookProgressionRules.Rings(open, waits))
			{
				if (waits[ring[0]].Length == 0 && producers[ring[0]].Any((BookProgressionRules.Route r) => r.Ingredients.Any((int[] g) => g.All(Blocked))))
				{
					modeItems.Add(ring[0]);
					continue;
				}
				// A ring's members turn into one another, so they share a step: the latest guess among them (Pearlwood's
				// hardmode floor, not its wall's White rarity) goes to the members that carry it, the rest follow by route.
				int top = ring.Max(Guess);
				if (top < 0)
				{
					hopeless.UnionWith(ring);
					continue;
				}
				foreach (int type in ring.Where((int t) => Guess(t) == top))
				{
					Seed(type, top, floors.ContainsKey(type) ? "list" : "rarity");
				}
			}
			if (seeds.Count > seeded)
			{
				(items, recipes) = BookProgressionRules.Resolve(seeds, floors, routes, via);
			}
		}
		// What stayed unplaced (no guess to give) but has a route made only of excluded items is excluded too.
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
		foreach (KeyValuePair<int, BookProgressionRules.Route> placed in via)
		{
			why[placed.Key] = placed.Value.Source ?? ("recipe:" + placed.Value.Key);
		}
		reasons = why;
		placedBy = via;
		seedSources = sources;
		recipeRoutes = routes.Where((BookProgressionRules.Route route) => route.Key >= 0).ToDictionary((BookProgressionRules.Route route) => route.Key);
		itemFloors = floors;
		trace = traced ?? new Dictionary<int, List<string>>();
		lastRoutes = TraceSources ? routes : new List<BookProgressionRules.Route>();
		recipeMinimums = routes.Where((BookProgressionRules.Route route) => route.Key >= 0).ToDictionary((BookProgressionRules.Route route) => route.Key, (BookProgressionRules.Route route) => BookProgressionRules.MinimumStep(route, items, floors));
		Built = true;
		ILog logger = ModContent.GetInstance<ModernRecipeBook>().Logger;
		logger.Info($"Progression audit ({timer.ElapsedMilliseconds} ms, {BookSteps.All.Count - 1} steps, mods' from {BookSteps.Source}, gates {(UsedGates ? "used" : "not ready")}, {probedCount} opaque conditions placed by probing) items: " + Totals(all.Select((Item i) => ItemStage(i.type))));
		logger.Info("Progression audit recipes: " + Totals(indexes.Select(RecipeStage)));
		logger.Info("Progression audit Calamity items: " + Totals(from i in all
			where i.ModItem?.Mod.Name == "CalamityMod"
			select ItemStage(i.type)));
		void Mark(int step, string list)
		{
			foreach (string name in list.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				if (names.TryGetValue(name, out var ids))
				{
					foreach (int id in ids)
					{
						floors[id] = step;
					}
				}
			}
		}
		bool Blocked(int type)
		{
			return !items.ContainsKey(type) && modeItems.Contains(type);
		}
		// What the second pass gives an item no source places: its curated floor, else its rarity tier.
		int Guess(int type)
		{
			return floors.TryGetValue(type, out int listed) ? listed : BookSteps.Of(BookProgressionSources.RarityStage(ContentSamples.ItemsByType[type]));
		}
		void Seed(int type, int step, string reason, int source = 0)
		{
			if (traced != null && step >= 0)
			{
				(traced.TryGetValue(type, out var list) ? list : (traced[type] = new List<string>())).Add(reason + "=" + step);
			}
			if (step >= 0 && floors.TryGetValue(type, out var minimum))
			{
				step = Max(step, minimum);
			}
			if (step >= 0 && (!seeds.TryGetValue(type, out var old) || step < old))
			{
				seeds[type] = step;
				why[type] = reason;
				sources[type] = source;
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

	// Every source How to Get shows for the item is a drop or container rule that fails in this world (a difficulty, a seed
	// or an other-world lambda, BookProgressionSources.WorldGate): How to Get's "Not in this world" rows.
	private static bool OnlyOtherWorlds(int type)
	{
		List<BookItemSource> shown = BookItemSources.For(type).Where((BookItemSource s) => s.Rate is not DropRateInfo rate || !BookProgressionSources.HiddenFromUI(rate)).ToList();
		return shown.Count > 0 && shown.All((BookItemSource s) => s.Rate is DropRateInfo rate && (rate.conditions?.Any((IItemDropRuleCondition c) => BookProgressionSources.WorldGate(c) == false) ?? false));
	}

	// What grows in the world once a boss is down and has no tile item of its own: Life Fruit after a mechanical boss
	// (WorldGen.hardUpdateWorld).
	private static readonly (string Item, string Term)[] GrownItems = { ("LifeFruit", "mechs:any") };

	private static void AddGrown(HashSet<int> sourced, Action<int, int, string, int> seed)
	{
		foreach ((string name, string term) in GrownItems)
		{
			int step = BookSteps.Resolve(term);
			if (ItemID.Search.TryGetId(name, out int type) && step >= 0)
			{
				sourced.Add(type);
				seed(type, step, "grows", 0);
			}
		}
	}

	// Fishing (BookWorldSources' tables, read from the game's and Calamity's catch code): from the start unless the
	// row names a gate ("Hardmode", "boss:SkeletronHead" for the Abyss) or a rod, which it then waits for.
	private static void AddFishing(List<BookProgressionRules.Route> routes, HashSet<int> sourced, Action<int, int, string, int> seed)
	{
		int key = int.MinValue / 4;
		foreach (BookItemSource source in BookWorldSources.CatalogueSources())
		{
			if (source.Kind != BookAcquisitionKind.Fishing)
			{
				continue;
			}
			// The Hallow and Calamity's Astral Infection only exist from hardmode on, and the Dungeon is open after
			// Skeletron: a catch only they give waits for it.
			int step = source.Where.Length == 0 ? 0 : (source.Where.All((string where) => where.Contains("Hallow") || where.Contains("Astral")) ? BookSteps.HardmodeStep : (source.Where.All((string where) => where.EndsWith("TheDungeon", StringComparison.Ordinal)) ? Max(0, BookSteps.Resolve("SkeletronHead")) : 0)), rod = 0;
			foreach (string part in source.Note.Split('|', ','))
			{
				if (part.StartsWith("rod:", StringComparison.Ordinal))
				{
					rod = ModContent.TryFind(part.Substring(4), out ModItem item) ? item.Type : -1;
					continue;
				}
				int gate = part switch
				{
					"Hardmode" => BookSteps.HardmodeStep,
					"PostHardmode" => BookSteps.MoonLordStep,
					_ when part.StartsWith("boss:", StringComparison.Ordinal) => BookSteps.Resolve(part.Substring(5)),
					_ => 0
				};
				step = (gate < 0 || step < 0) ? -1 : Max(step, gate);
			}
			if (step < 0 || rod < 0)
			{
				continue;
			}
			sourced.Add(source.Item);
			if (rod > 0)
			{
				routes.Add(new BookProgressionRules.Route(key++, source.Item, step, new int[1][] { new int[1] { rod } }, Source: "rod:" + ItemName(ContentSamples.ItemsByType[rod])));
			}
			else
			{
				seed(source.Item, step, "fishing", 0);
			}
		}
	}

	// Mods' conditions the tables cannot read (Calamity's AddIf lambdas) are asked with the progression flags opened step
	// by step: every drop's, recipe's and shop entry's.
	private static int ProbeOpaqueConditions(List<int> indexes)
	{
		try
		{
			IEnumerable<(IItemDropRuleCondition, NPC)> drops = BookItemSources.Npcs.SelectMany((BookItemSources.NpcSource mob) => mob.Reports.SelectMany((DropRateInfo r) => (r.conditions ?? new List<IItemDropRuleCondition>()).Select((IItemDropRuleCondition c) => (c, mob.Npc))))
				.Concat(BookItemSources.GlobalReports.Concat(BookItemSources.Items.SelectMany((BookItemSources.ItemRoutes i) => i.Reports)).SelectMany((DropRateInfo r) => (r.conditions ?? new List<IItemDropRuleCondition>()).Select((IItemDropRuleCondition c) => (c, (NPC)null))));
			IEnumerable<Condition> conditions = indexes.SelectMany((int index) => Main.recipe[index].Conditions)
				.Concat(BookItemSources.Shops.SelectMany((BookItemSources.ShopSource shop) => shop.Entries.SelectMany((AbstractNPCShop.Entry entry) => entry.Conditions)));
			return BookProgressionSources.ProbeOpaque(drops, conditions);
		}
		catch (Exception exception)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("MRB could not probe mods' conditions; they fall back to rarity.", exception);
			return 0;
		}
	}

	// Ores and other natural blocks come out of the world: as early as the first pickaxe strong enough for the tile
	// (Chlorophyte 200: the Pickaxe Axe after the three mechanical bosses; Scoria 210: the Picksaw) and no earlier
	// than what the tile waits for (BookWorldSources.TileGate: Cryonic Ore after Cryogen). A route, not a floor, so a
	// pickaxe made of the ore cannot open it.
	private static void AddMining(List<Item> all, List<BookProgressionRules.Route> routes, HashSet<int> sourced)
	{
		int key = int.MinValue / 2;
		List<Item> picks = all.Where((Item i) => i.pick > 0).ToList();
		foreach (Item item in all)
		{
			int tile = item.createTile;
			if (tile < 0 || tile >= TileLoader.TileCount || !BookWorldSources.Natural(tile))
			{
				continue;
			}
			int power = BookWorldSources.PickPower(tile);
			string gate = BookWorldSources.TileGate(tile);
			if (power <= 0 && gate.Length == 0)
			{
				continue;
			}
			int floor = gate.Length == 0 ? 0 : BookSteps.Resolve(gate);
			int[][] ingredients = power > 0 ? new int[1][] { picks.Where((Item p) => p.pick >= power).Select((Item p) => p.type).ToArray() } : Array.Empty<int[]>();
			if (floor < 0 || (power > 0 && ingredients[0].Length == 0))
			{
				continue;
			}
			sourced.Add(item.type);
			routes.Add(new BookProgressionRules.Route(key++, item.type, floor, ingredients, Source: "mine:" + (TileLoader.GetTile(tile)?.Name ?? (TileID.Search.TryGetName(tile, out string name) ? name : tile.ToString()))));
		}
	}

	// Drops of bosses seed their step directly; drops of other NPCs the NPC's step (curated, spawn gate, or what it
	// always drops), else the item's rarity tier; bag, crate and present contents become routes from the container.
	// Returns every item a placed NPC or a container drops, so the raw-item pass leaves those to the graph.
	private static HashSet<int> AddDropSources(List<BookProgressionRules.Route> routes, IReadOnlyDictionary<int, int> floors, HashSet<int> modeItems, Dictionary<int, List<int>> recipesByItem, Action<int, int, string, int> seed)
	{
		HashSet<int> sourced = new HashSet<int>();
		int sourceKey = -1;
		npcs = new Dictionary<int, int>();
		BookItemSources.EnsureBuilt();
		try
		{
			foreach (BookItemSources.NpcSource mob in BookItemSources.Npcs)
			{
				NPC npc = mob.Npc;
				string npcName = mob.Name;
				var drops = (from rate in mob.Reports
					where ContentSamples.ItemsByType.ContainsKey(rate.itemId)
					let drop = ContentSamples.ItemsByType[rate.itemId]
					let floor = DropFloor(rate, drop, modeItems)
					where floor >= 0
					select (Rate: rate, Drop: drop, Floor: floor)).ToList();
				// Ordinary enemies without a curated step appear as early as the latest thing they always drop (Pixies
				// drop Pixie Dust, Martians their gear); a spawn gate in their own code can only make it later.
				int step = BookProgressionSources.NpcStep(npc.type);
				string source = "drop:" + npcName;
				if (step < 0)
				{
					step = InferEnemyStep(drops.Where(d => Unconditional(d.Rate)).Select(d => d.Drop));
					source = "enemy:" + npcName;
				}
				int gate = GateStep(npc.type);
				if (gate > step)
				{
					step = gate;
					source = "spawn:" + npcName;
				}
				npcs[mob.NetId] = step;
				foreach (var (rate, drop, floor) in drops)
				{
					if (step >= 0)
					{
						sourced.Add(rate.itemId);
						seed(rate.itemId, Max(step, floor), source, mob.NetId);
						continue;
					}
					// Sourced: the raw-item pass would guess the rarity again without the drop's own floor (Bloodstone and
					// the Guidelight of Oblivion from post-Providence Crag mobs the tables cannot place). The drop is a real
					// source whose time the rarity guesses ("rarity:<mob>", global loot's "rarity:any"; "drop:" where the
					// drop's own condition comes later and decides), unlike a sourceless item's plain "rarity"
					// (ConsistencyAudit).
					int tier = BookSteps.Of(BookProgressionSources.RarityStage(drop));
					if (tier >= 0)
					{
						sourced.Add(rate.itemId);
						seed(rate.itemId, Max(tier, floor), (floor > tier ? "drop:" : "rarity:") + npcName, mob.NetId);
					}
				}
				if (step >= 0 && mob.Banner > 0)
				{
					sourced.Add(mob.Banner);
					seed(mob.Banner, step, "banner:" + npcName, mob.NetId);
				}
				// Critters caught with a bug net. Without a better clue a critter is about from the start
				// (Draedon lab Nanodroids carry the tier-less DarkOrange rarity).
				if (mob.Critter > 0)
				{
					Item critter = ContentSamples.ItemsByType[mob.Critter];
					int tier4 = BookSteps.Of(BookProgressionSources.RarityStage(critter));
					sourced.Add(critter.type);
					seed(critter.type, step >= 0 ? step : (tier4 >= 0 ? tier4 : 0), "catch:" + npcName, mob.NetId);
				}
			}
			// Global loot (biome essences and the like) applies to every NPC, so it is never boss-staged.
			foreach (DropRateInfo rate3 in BookItemSources.GlobalReports)
			{
				if (ContentSamples.ItemsByType.TryGetValue(rate3.itemId, out var drop3))
				{
					int floor3 = DropFloor(rate3, drop3, modeItems);
					int tier3 = BookSteps.Of(BookProgressionSources.RarityStage(drop3));
					if (floor3 >= 0 && tier3 >= 0)
					{
						sourced.Add(rate3.itemId);
						seed(rate3.itemId, Max(tier3, floor3), floor3 > tier3 ? "drop:any" : "rarity:any", 0);
					}
				}
			}
			foreach (BookItemSources.ItemRoutes own in BookItemSources.Items)
			{
				KeyValuePair<int, Item> pair2 = new KeyValuePair<int, Item>(own.Item, ContentSamples.ItemsByType[own.Item]);
				foreach (DropRateInfo rate2 in own.Reports)
				{
					if (!ContentSamples.ItemsByType.TryGetValue(rate2.itemId, out var drop2))
					{
						continue;
					}
					int floor2 = DropFloor(rate2, drop2, modeItems);
					if (floor2 < 0)
					{
						continue;
					}
					sourced.Add(rate2.itemId);
					routes.Add(new BookProgressionRules.Route(sourceKey--, rate2.itemId, floor2, new int[1][] { new int[1] { pair2.Key } }, Source: "drop:" + ItemName(pair2.Value)));
					// The container is the way, never a rarity guess for its loot: the Necromantic Geode's Bloodstone waits for
					// Providence, the Golden Lock Box for the Dungeon. Chests that hold the same items are curated sources of
					// their own.
				}
				// Shimmer (the Aether exists from world generation; late transmutations are curated floors).
				int shimmered = own.Shimmer;
				if (shimmered > 0)
				{
					sourced.Add(shimmered);
					routes.Add(new BookProgressionRules.Route(sourceKey--, shimmered, 0, new int[1][] { new int[1] { pair2.Key } }, Source: "shimmer:" + ItemName(pair2.Value)));
				}
			}
			foreach (var (from, to) in BookItemSources.Conversions)
			{
				sourced.Add(to);
				routes.Add(new BookProgressionRules.Route(sourceKey--, to, 0, new int[1][] { new int[1] { from } }, Source: "use:" + ItemName(ContentSamples.ItemsByType[from])));
			}
			// Developer and patron sets come out of any hardmode boss bag.
			if (BookItemSources.HardmodeBags.Length > 0)
			{
				foreach (int item in BookItemSources.DevSetChances.Keys)
				{
					sourced.Add(item);
					routes.Add(new BookProgressionRules.Route(sourceKey--, item, 0, new int[1][] { BookItemSources.HardmodeBags }, Source: "devset"));
				}
			}
			// Curated sources of the areas in BookCurated.
			foreach (var (item, step, why) in BookCurated.Steps())
			{
				sourced.Add(item);
				seed(item, step, why, 0);
			}
			foreach (var (item, inputs, floor, why) in BookCurated.Routes())
			{
				sourced.Add(item);
				routes.Add(new BookProgressionRules.Route(sourceKey--, item, floor, inputs, Source: why));
			}
			// A machine's product comes with the machine.
			foreach (var (machine, product, _) in BookItemSources.Machines)
			{
				sourced.Add(product);
				routes.Add(new BookProgressionRules.Route(sourceKey--, product, 0, new int[1][] { new int[1] { machine } }, Source: "machine:" + ItemName(ContentSamples.ItemsByType[machine])));
			}
			// Recorded music boxes come from a blank Music Box worn while the track plays.
			foreach (int box in BookItemSources.RecordedBoxes)
			{
				sourced.Add(box);
				routes.Add(new BookProgressionRules.Route(sourceKey--, box, BookSteps.Of(BookProgressionSources.TrackStage(ContentSamples.ItemsByType[box])), new int[1][] { new int[1] { ItemID.MusicBox } }, Source: "record:MusicBox"));
			}
			// Strange Plants traded to the Dye Trader.
			foreach (BookItemSources.DyeTrade trade in BookItemSources.DyeTraderRewards)
			{
				foreach (int dye in trade.Dyes)
				{
					sourced.Add(dye);
					routes.Add(new BookProgressionRules.Route(sourceKey--, dye, BookSteps.Of(trade.Gate), new int[1][] { BookItemSources.StrangePlants }, Source: "trade:DyeTrader"));
				}
			}
		}
		catch (Exception exception)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("MRB progression drop sources failed; using recipes and rarity only.", exception);
		}
		// Shops: the town NPC's arrival step plus the entry's conditions (Calamity gates its stock on the
		// same CalamityConditions keys as its drops).
		try
		{
			foreach (BookItemSources.ShopSource stock in BookItemSources.Shops)
			{
				AbstractNPCShop shop = stock.Shop;
				int townStep = BookProgressionSources.TownNpcStep(shop.NpcType);
				string source = "shop:" + (NPCLoader.GetNPC(shop.NpcType)?.Name ?? (NPCID.Search.TryGetName(shop.NpcType, out var townName) ? townName : shop.NpcType.ToString()));
				foreach (AbstractNPCShop.Entry entry in stock.Entries)
				{
					Item item = entry.Item;
					int step2 = townStep;
					List<int[]> carried = new List<int[]>();
					foreach (Condition condition in entry.Conditions)
					{
						if (BookProgressionSources.CarriedItem(condition) is int needed)
						{
							carried.Add(new int[1] { needed });
						}
						int? hint = BookProgressionSources.ConditionStep(condition);
						int value = hint ?? BookSteps.Of(BookProgressionSources.RarityStage(item));
						if (hint < 0)
						{
							if (BookProgressionSources.ModeBlocked(condition?.Description?.Key, () => condition.IsMet()))
							{
								modeItems.Add(item.type);
							}
							step2 = -1;
							break;
						}
						if (value >= 0)
						{
							step2 = Max(step2, value);
						}
					}
					if (step2 < 0)
					{
						continue;
					}
					sourced.Add(item.type);
					if (carried.Count > 0)
					{
						// Stock that needs an item in the inventory (Blood Rune with Ice Barrage) is as late as it.
						routes.Add(new BookProgressionRules.Route(sourceKey--, item.type, step2, carried.ToArray(), Source: source));
					}
					else
					{
						seed(item.type, step2, source, shop.NpcType);
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
		int InferEnemyStep(IEnumerable<Item> unconditional)
		{
			List<Item> drops = unconditional.DistinctBy((Item i) => i.type).ToList();
			int curated = drops.Where((Item i) => floors.ContainsKey(i.type)).Select((Item i) => floors[i.type]).DefaultIfEmpty(-1).Max();
			List<int> gear = (from i in drops
				where !floors.ContainsKey(i.type) && i.maxStack == 1 && !i.vanity
				select BookSteps.Of(BookProgressionSources.RarityStage(i)) into s
				where s >= 0
				orderby s descending
				select s).ToList();
			return Max(curated, gear.Count >= 2 ? gear[1] : -1);
		}
	}

	// A drop without conditions, or only ones that keep it before hardmode.
	private static bool Unconditional(DropRateInfo rate)
	{
		return rate.conditions == null || rate.conditions.All((IItemDropRuleCondition c) => BookProgressionSources.DropConditionStep(c) is int s && BookSteps.StageOf(s) == BookStage.PreHardmode);
	}

	// Step the conditions on a drop add. Opaque conditions (another mod's lambdas) fall back to the dropped item's
	// rarity tier; a NeverTrue, special-seed or other-difficulty rule makes the drop unknown (-1), and the last case
	// is remembered in modeBlocked.
	private static int DropFloor(DropRateInfo rate, Item drop, HashSet<int> modeBlocked = null)
	{
		// What the game's Bestiary hides, How to Get does not list either (BookProgressionSources.HiddenFromUI).
		if (BookProgressionSources.HiddenFromUI(rate))
		{
			return -1;
		}
		int floor = 0;
		foreach (IItemDropRuleCondition condition in rate.conditions ?? new List<IItemDropRuleCondition>())
		{
			if (BookProgressionSources.ModeBlocked(condition))
			{
				modeBlocked?.Add(drop.type);
				return -1;
			}
			int? hint = BookProgressionSources.DropConditionStep(condition);
			if (hint < 0)
			{
				return -1;
			}
			int value = hint ?? BookSteps.Of(BookProgressionSources.RarityStage(drop));
			if (value >= 0)
			{
				floor = Max(floor, value);
			}
		}
		return floor;
	}

	private static int Max(int a, int b)
	{
		return Math.Max(a, b);
	}

	private static string ItemName(Item item)
	{
		return item.ModItem?.Name ?? ItemID.Search.GetName(item.type);
	}
}
