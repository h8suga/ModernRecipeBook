using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.Progression;

// Mods gate many mobs inside ModNPC.SpawnChance (Calamity's Plague Charger: the hardmode jungle once Golem is down),
// which no Bestiary tag shows. As BookItemClasses runs items on a throwaway player, this asks SpawnChance itself: a
// throwaway player stands in each place while the world's progression flags open step by step in Boss Checklist
// order; the first step after which the mob may spawn is its gate. Closing steps one at a time then tells which it
// really needs: mods test OR and count conditions (Cryo Slime: Cryogen and any two mechanical bosses). Once per world,
// in single player and on multiplayer clients, on the main thread between two updates; every flag it sets is put back
// before it returns.
internal static class BookSpawnGates
{
	internal enum Layer
	{
		Space,
		Surface,
		Dirt,
		Rock,
		Hell
	}

	// A place the probe stands in: its name (a key of RecipeBookSystem.PlaceName or a ModBiome), the name the card
	// groups it under, its height and the tile under the spawn point. Traits: the Player.Zone* flags and mods' biomes
	// ("Mod/Biome") that hold there, "sea" (by the world's edge, where the ocean is), "water" (tried in water too),
	// "sky", "temple" and "cave" (NPCSpawnInfo's Sky, Lihzahrd and DesertCave).
	internal sealed record Place(string Key, string Family, Layer Height, int Tile, string Traits);

	// A gated mob: what it waits for, the stage of the step after which it first spawns, where it may spawn once
	// everything is open, and what every try that worked had in common (Day null: day and night both work). Each term
	// is one requirement: one way or several (";"), a way being one step's boss (Steps' first word) or several
	// together ("+"), "?" for a way the probe could not name; or "mechs:any", "mechs:two", "mechs:all".
	internal sealed record Gate(string[] Terms, BookStage Stage, Place[] Places, bool? Day, bool Rain, bool Water);

	private static readonly HashSet<string> Mechs = new HashSet<string>(StringComparer.Ordinal) { "Enemies.TheTwins", "TheDestroyer", "SkeletronPrime" };

	// Progression in Boss Checklist order (its vanilla values; Calamity's from WeakReferenceSupport.
	// BossChecklistProgressionValues in 2.2.4): the boss that names the step ("A|B": the corruption's and the
	// crimson's; Hardmode and PostHardmode for the Wall of Flesh and Moon Lord, which have words), then the flags it sets: NPC.downed* fields, Main.hardMode and Calamity's DownedBossSystem fields
	// (its properties' setters would also start a Lantern Night). Events and the pillars only open in the final,
	// everything-on state, so a mob only they gate keeps its Bestiary tags.
	private static readonly string[] Steps =
	{
		"KingSlime downedSlimeKing",
		"CalamityMod/DesertScourgeHead _downedDesertScourge",
		"CalamityMod/GiantClam _downedCLAM",
		"EyeofCthulhu downedBoss1",
		"CalamityMod/Crabulon _downedCrabulon",
		"EaterofWorldsHead|BrainofCthulhu downedBoss2",
		"CalamityMod/HiveMind _downedHiveMind",
		"CalamityMod/PerforatorHive _downedPerforator",
		"QueenBee downedQueenBee",
		"SkeletronHead downedBoss3",
		"Deerclops downedDeerclops",
		"CalamityMod/SlimeGodCore _downedSlimeGod",
		"Hardmode hardMode",
		"QueenSlimeBoss downedQueenSlime",
		"CalamityMod/Cryogen _downedCryogen",
		"Enemies.TheTwins downedMechBoss2",
		"CalamityMod/AquaticScourgeHead _downedAquaticScourge",
		"CalamityMod/CragmawMire _downedCragmawMire",
		"TheDestroyer downedMechBoss1",
		"CalamityMod/BrimstoneElemental _downedBrimstoneElemental",
		"SkeletronPrime downedMechBoss3",
		"CalamityMod/CalamitasClone _downedCalamitasClone",
		"Plantera downedPlantBoss",
		"CalamityMod/GreatSandShark _downedGSS",
		"CalamityMod/Leviathan _downedLeviathan",
		"CalamityMod/AstrumAureus _downedAstrumAureus",
		"Golem downedGolemBoss",
		"DukeFishron downedFishron",
		"CalamityMod/PlaguebringerGoliath _downedPlaguebringer",
		"HallowBoss downedEmpressOfLight",
		"CalamityMod/RavagerBody _downedRavager",
		"CultistBoss downedAncientCultist",
		"CalamityMod/AstrumDeusHead _downedAstrumDeus",
		"PostHardmode downedMoonlord",
		"CalamityMod/ProfanedGuardianCommander _downedGuardians",
		"CalamityMod/Dragonfolly _downedDragonfolly",
		"CalamityMod/Providence _downedProvidence",
		"CalamityMod/CeaselessVoid _downedCeaselessVoid",
		"CalamityMod/StormWeaverHead _downedStormWeaver",
		"CalamityMod/Signus _downedSignus _downedSecondSentinels",
		"CalamityMod/Polterghast _downedPolterghast",
		"CalamityMod/Mauler _downedMauler",
		"CalamityMod/NuclearTerror _downedNuclearTerror",
		"CalamityMod/OldDuke _downedBoomerDuke",
		"CalamityMod/DevourerofGodsHead _downedDoG",
		"CalamityMod/Yharon _downedYharon",
		"CalamityMod/Draedon _downedExoMechs _downedAres _downedThanatos _downedArtemisAndApollo",
		"CalamityMod/SupremeCalamitas _downedCalamitas"
	};

	internal static readonly Place[] Places =
	{
		new Place("Forest", "Forest", Layer.Surface, TileID.Grass, ""),
		new Place("Underground", "Underground", Layer.Dirt, TileID.Dirt, ""),
		new Place("Caverns", "Underground", Layer.Rock, TileID.Stone, "water"),
		new Place("Jungle", "Jungle", Layer.Surface, TileID.JungleGrass, "ZoneJungle"),
		new Place("UndergroundJungle", "Jungle", Layer.Rock, TileID.JungleGrass, "ZoneJungle water"),
		new Place("Temple", "Temple", Layer.Rock, TileID.LihzahrdBrick, "ZoneJungle ZoneLihzhardTemple temple"),
		new Place("Snow", "Snow", Layer.Surface, TileID.SnowBlock, "ZoneSnow"),
		new Place("UndergroundSnow", "Snow", Layer.Rock, TileID.IceBlock, "ZoneSnow"),
		new Place("Desert", "Desert", Layer.Surface, TileID.Sand, "ZoneDesert"),
		new Place("UndergroundDesert", "Desert", Layer.Rock, TileID.HardenedSand, "ZoneDesert ZoneUndergroundDesert cave"),
		new Place("Ocean", "Ocean", Layer.Surface, TileID.Sand, "ZoneBeach sea water"),
		new Place("Dungeon", "Dungeon", Layer.Rock, TileID.BlueDungeonBrick, "ZoneDungeon"),
		new Place("Underworld", "Underworld", Layer.Hell, TileID.Ash, ""),
		new Place("Corruption", "Corruption", Layer.Surface, TileID.CorruptGrass, "ZoneCorrupt"),
		new Place("Crimson", "Crimson", Layer.Surface, TileID.CrimsonGrass, "ZoneCrimson"),
		new Place("Hallow", "Hallow", Layer.Surface, TileID.HallowedGrass, "ZoneHallow"),
		new Place("Sky", "Sky", Layer.Space, TileID.Cloud, "sky"),
		// Calamity's biomes as CalamityPlayer reads them (Player.InModBiome): the Sunken Sea is any of its parts, the
		// Astral Infection's three places are named after its Bestiary categories.
		new Place("CalamityMod/SulphurousSeaBiome", "CalamityMod/SulphurousSeaBiome", Layer.Surface, TileID.Sand, "CalamityMod/SulphurousSeaBiome sea water"),
		new Place("CalamityMod/SunkenSeaBiome", "CalamityMod/SunkenSeaBiome", Layer.Rock, TileID.Sand, "CalamityMod/SunkenSeaBiome CalamityMod/TimelessShoresBiome water"),
		new Place("CalamityMod/AbyssLayer1Biome", "Abyss", Layer.Dirt, TileID.Stone, "CalamityMod/AbyssLayer1Biome sea water"),
		new Place("CalamityMod/AbyssLayer2Biome", "Abyss", Layer.Rock, TileID.Stone, "CalamityMod/AbyssLayer2Biome sea water"),
		new Place("CalamityMod/AbyssLayer3Biome", "Abyss", Layer.Rock, TileID.Stone, "CalamityMod/AbyssLayer3Biome sea water"),
		new Place("CalamityMod/AbyssLayer4Biome", "Abyss", Layer.Rock, TileID.Stone, "CalamityMod/AbyssLayer4Biome sea water"),
		new Place("CalamityMod/AstralInfectionBiome", "CalamityMod/AstralInfectionBiome", Layer.Surface, TileID.Dirt, "CalamityMod/AstralInfectionBiome"),
		new Place("CalamityMod/AstralUnderground", "CalamityMod/AstralInfectionBiome", Layer.Rock, TileID.Stone, "CalamityMod/AstralInfectionBiome"),
		new Place("CalamityMod/AstralDesert", "CalamityMod/AstralInfectionBiome", Layer.Surface, TileID.Sand, "ZoneDesert CalamityMod/AstralInfectionBiome"),
		new Place("CalamityMod/BrimstoneCragsBiome", "CalamityMod/BrimstoneCragsBiome", Layer.Hell, TileID.Ash, "CalamityMod/BrimstoneCragsBiome")
	};

	private static readonly Dictionary<int, Gate> gates = new Dictionary<int, Gate>();

	private static bool built;

	// Probed for this world (the progression steps of modded enemies read the gates).
	internal static bool Built => built;

	private static bool spotsRequested;

	// Tiles around a spot that SpawnCondition and the mods' checks read; the server sends 3x3 sections around it.
	private const int SpotReach = 10;

	// The audit line of the last probe, for the test stand.
	internal static string Summary = "";

	internal static void Clear()
	{
		gates.Clear();
		built = false;
		spotsRequested = false;
		Summary = "";
	}

	// Where the probe stands in a place: the map's outermost column for land (so SpawnCondition's wall checks read no
	// cabin or spider nest), inside the ocean's 250 tiles for the sea.
	private static Point Spot(Place place)
	{
		int x = place.Traits.Split(' ').Contains("sea") ? 100 : 41;
		int y = place.Height switch
		{
			Layer.Space => (int)(Main.worldSurface * 0.25),
			Layer.Surface => (int)Main.worldSurface - 30,
			Layer.Dirt => (int)((Main.worldSurface + Main.rockLayer) / 2.0),
			Layer.Rock => (int)Main.rockLayer + 100,
			_ => Main.maxTilesY - 100
		};
		return new Point(x, y);
	}

	// Each world is probed afresh: its height places the spots, and the probe reads its tiles there. A multiplayer client
	// asks for those tiles right away, so they are usually in before the book needs the gates.
	internal static void OnEnterWorld()
	{
		Clear();
		SpotsLoaded();
	}

	// A multiplayer client has the world only around where it has been, and the sea spot by the map's edge reads as air
	// without it (Mantis Shrimp lost its gate). So it asks the server for the spots' sections once, as the map teleport
	// does, and the probe waits until they are in.
	private static bool SpotsLoaded()
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
		{
			return true;
		}
		bool loaded = true;
		foreach (Point spot in Places.Select(Spot).Distinct())
		{
			if (Main.sectionManager.TilesLoaded(Math.Max(0, spot.X - SpotReach), Math.Max(0, spot.Y - SpotReach), Math.Min(Main.maxTilesX - 1, spot.X + SpotReach), Math.Min(Main.maxTilesY - 1, spot.Y + SpotReach)))
			{
				continue;
			}
			loaded = false;
			if (!spotsRequested)
			{
				ModPacket packet = ModContent.GetInstance<ModernRecipeBook>().GetPacket();
				packet.Write((byte)BookMessage.MapSections);
				packet.Write((short)spot.X);
				packet.Write((short)spot.Y);
				packet.Send();
			}
		}
		spotsRequested = true;
		return loaded;
	}

	internal static Gate Of(int type)
	{
		return gates.GetValueOrDefault(type);
	}

	internal static BookStage Stage(int type)
	{
		return gates.TryGetValue(type, out Gate gate) ? gate.Stage : BookStage.Unknown;
	}

	// The probe sets world flags for a moment. They are local statics, nothing is sent, and packets are read on this
	// same thread, so a multiplayer client probes like single player once it has the tiles the probe stands on.
	internal static void EnsureBuilt()
	{
		if (built || Main.gameMenu || !SpotsLoaded())
		{
			return;
		}
		built = true;
		try
		{
			Probe();
		}
		catch (Exception error)
		{
			gates.Clear();
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Spawn gates could not be probed; mob cards keep the Bestiary's spawn tags.", error);
		}
	}

	// Dev audit for the test stand: one TSV line per gated mob (type, name, gate, stage, places, day, rain, water).
	internal static IEnumerable<string> Audit()
	{
		return from pair in gates
			orderby pair.Key
			let gate = pair.Value
			select string.Join('\t', pair.Key, NPCLoader.GetNPC(pair.Key)?.FullName, string.Join(" & ", gate.Terms), gate.Stage, string.Join(",", gate.Places.Select((Place p) => p.Key)), gate.Day, gate.Rain, gate.Water);
	}

	// The mechanical bosses a mob waits for, from the sets of them that let it spawn (bit s of sets: set s, whose bit b
	// is names[b]): the smallest such sets, in a word where there is one. Null when no set works.
	private static string MechTerm(int sets, string[] names)
	{
		List<int> least = Enumerable.Range(1, 7).Where((int s) => (sets & (1 << s)) != 0 && !Enumerable.Range(1, 7).Any((int t) => t != s && (t & s) == t && (sets & (1 << t)) != 0)).ToList();
		return string.Join(",", least) switch
		{
			"" => null,
			"1,2,4" => "mechs:any",
			"3,5,6" => "mechs:two",
			"7" => "mechs:all",
			_ => string.Join(";", least.Select((int s) => string.Join("+", Enumerable.Range(0, names.Length).Where((int b) => (s & (1 << b)) != 0).Select((int b) => names[b]))))
		};
	}

	// The world's progression flags (NPC.downed*, Main.hardMode, DD2Event.DownedInvasion*, Calamity's DownedBossSystem
	// fields) and Steps with the flags each sets, the steps of mods that are not loaded left out.
	private static (FieldInfo[] Flags, List<(string Boss, FieldInfo[] Flags, BookStage Stage)> Steps) ReadProgression()
	{
		Type downed = ModLoader.TryGetMod("CalamityMod", out Mod calamity) ? calamity.Code.GetType("CalamityMod.DownedBossSystem") : null;
		FieldInfo[] flags = typeof(NPC).GetFields(BindingFlags.Static | BindingFlags.Public).Where((FieldInfo f) => f.Name.StartsWith("downed", StringComparison.Ordinal))
			.Concat(typeof(DD2Event).GetFields(BindingFlags.Static | BindingFlags.Public).Where((FieldInfo f) => f.Name.StartsWith("DownedInvasion", StringComparison.Ordinal)))
			.Concat(downed?.GetFields(BindingFlags.Static | BindingFlags.NonPublic).Where((FieldInfo f) => f.Name.StartsWith("_downed", StringComparison.Ordinal)) ?? Array.Empty<FieldInfo>())
			.Append(typeof(Main).GetField(nameof(Main.hardMode)))
			.Where((FieldInfo f) => f.FieldType == typeof(bool))
			.ToArray();
		Dictionary<string, FieldInfo> byName = flags.ToDictionary((FieldInfo f) => f.Name);
		List<(string Boss, FieldInfo[] Flags, BookStage Stage)> steps = new List<(string, FieldInfo[], BookStage)>();
		BookStage reached = BookStage.PreHardmode;
		foreach (string line in Steps)
		{
			string[] words = line.Split(' ');
			string boss = words[0].Split('|')[0];
			int type = boss.Contains('/') ? (ModContent.TryFind(boss, out ModNPC modNpc) ? modNpc.Type : -1) : (NPCID.Search.TryGetId(boss, out int id) ? id : 0);
			if (type < 0)
			{
				// Its mod is not loaded.
				continue;
			}
			FieldInfo[] set = words.Skip(1).Select((string name) => byName.GetValueOrDefault(name) ?? throw new MissingFieldException("progression flag " + name)).ToArray();
			// Running maximum: a step the stage tables do not know (the Twins, Draedon) is as late as the one before.
			reached = (BookStage)Math.Max((int)reached, (int)(boss switch
			{
				"Hardmode" => BookStage.Hardmode,
				"PostHardmode" => BookStage.PostHardmode,
				_ => (type > 0) ? BookProgressionSources.NpcStage(type) : BookStage.Unknown
			}));
			steps.Add((words[0], set, reached));
		}
		return (flags, steps);
	}

	// Sets the named steps as on(k) says and every other flag (events, pillars) to rest; "any mechanical boss"
	// follows the three, as in the game.
	private static void OpenSteps(FieldInfo[] flags, List<(string Boss, FieldInfo[] Flags, BookStage Stage)> steps, Func<int, bool> on, bool rest)
	{
		foreach (FieldInfo flag in flags)
		{
			flag.SetValue(null, rest);
		}
		for (int k = 0; k < steps.Count; k++)
		{
			foreach (FieldInfo flag in steps[k].Flags)
			{
				flag.SetValue(null, on(k));
			}
		}
		NPC.downedMechBossAny = NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3;
	}

	// Mods' opaque conditions (Calamity's AddIf lambdas: "NPC.downedGolemBoss" for Scoria Ore from the Chaotic Puffer)
	// asked the same way: the flags open step by step in Steps' order, and a test's answer is the boss of the first
	// step after which it passes ("" when it passes with nothing down). Null for a test that also fails with everything
	// open (it waits for a biome, the time or an event, not progression) or throws. Every flag is put back.
	internal static string[] ProbeSteps(IReadOnlyList<Func<bool>> tests)
	{
		string[] results = new string[tests.Count];
		if (tests.Count == 0)
		{
			return results;
		}
		(FieldInfo[] flags, List<(string Boss, FieldInfo[] Flags, BookStage Stage)> steps) = ReadProgression();
		object[] saved = flags.Select((FieldInfo f) => f.GetValue(null)).ToArray();
		bool[] open = new bool[tests.Count];
		bool[] done = new bool[tests.Count];
		try
		{
			OpenSteps(flags, steps, (int k) => true, rest: true);
			for (int i = 0; i < tests.Count; i++)
			{
				open[i] = Passes(tests[i]);
			}
			for (int step = -1; step < steps.Count; step++)
			{
				OpenSteps(flags, steps, (int k) => k <= step, rest: false);
				for (int i = 0; i < tests.Count; i++)
				{
					if (open[i] && !done[i] && Passes(tests[i]))
					{
						done[i] = true;
						results[i] = step < 0 ? "" : steps[step].Boss;
					}
				}
			}
		}
		finally
		{
			for (int f = 0; f < flags.Length; f++)
			{
				flags[f].SetValue(null, saved[f]);
			}
		}
		return results;
		static bool Passes(Func<bool> test)
		{
			try
			{
				return test();
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	private static void Probe()
	{
		Stopwatch timer = Stopwatch.StartNew();
		ModernRecipeBook mod = ModContent.GetInstance<ModernRecipeBook>();
		// SpawnCondition.X.Chance, which most mods' SpawnChance return, is what NPCSpawnHelper.DoChecks worked out for
		// the spawn info last; NPCLoader.ChooseSpawn runs it before asking the mods.
		Type helper = typeof(ModNPC).Assembly.GetType("Terraria.ModLoader.Utilities.NPCSpawnHelper");
		Action reset = helper?.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic)?.CreateDelegate<Action>();
		Action<NPCSpawnInfo> check = helper?.GetMethod("DoChecks", BindingFlags.Static | BindingFlags.NonPublic)?.CreateDelegate<Action<NPCSpawnInfo>>();
		FieldInfo biomeFlags = typeof(Player).GetField("modBiomeFlags", BindingFlags.Instance | BindingFlags.NonPublic);
		if (reset == null || check == null || biomeFlags == null)
		{
			mod.Logger.Warn("Spawn gates: tModLoader's spawn helpers were not found; mob cards keep the Bestiary's spawn tags.");
			return;
		}
		(FieldInfo[] flags, List<(string Boss, FieldInfo[] Flags, BookStage Stage)> steps) = ReadProgression();
		List<(Place Place, Player Player, NPCSpawnInfo Info, int Variants)> stands = new List<(Place, Player, NPCSpawnInfo, int)>();
		foreach (Place place in Places)
		{
			HashSet<string> traits = place.Traits.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
			List<ModBiome> biomes = new List<ModBiome>();
			foreach (string name in traits.Where((string t) => t.Contains('/')))
			{
				biomes.Add(ModContent.TryFind(name, out ModBiome biome) ? biome : null);
			}
			if (biomes.Contains(null))
			{
				// Its mod is not loaded.
				continue;
			}
			// Nobody's index, as in BookItemClasses: code guarded by whoAmI == Main.myPlayer leaves it alone.
			Player p = new Player
			{
				whoAmI = Main.maxPlayers
			};
			BitArray inBiome = (BitArray)biomeFlags.GetValue(p);
			foreach (ModBiome biome in biomes)
			{
				inBiome[biome.Type] = true;
			}
			foreach (string zone in traits.Where((string t) => t.StartsWith("Zone", StringComparison.Ordinal)))
			{
				typeof(Player).GetProperty(zone).SetValue(p, true);
			}
			p.ZoneSkyHeight = place.Height == Layer.Space;
			p.ZoneOverworldHeight = place.Height == Layer.Surface;
			p.ZoneDirtLayerHeight = place.Height == Layer.Dirt;
			p.ZoneRockLayerHeight = place.Height == Layer.Rock;
			p.ZoneUnderworldHeight = place.Height == Layer.Hell;
			p.ZonePurity = p.InZonePurity();
			Point spot = Spot(place);
			int x = spot.X, y = spot.Y;
			p.position = new Vector2(x, y - 3) * 16f;
			NPCSpawnInfo info = new NPCSpawnInfo
			{
				Player = p,
				SpawnTileX = x,
				SpawnTileY = y,
				SpawnTileType = place.Tile,
				PlayerFloorX = x,
				PlayerFloorY = y,
				Sky = traits.Contains("sky"),
				Lihzahrd = traits.Contains("temple"),
				DesertCave = traits.Contains("cave")
			};
			stands.Add((place, p, info, traits.Contains("water") ? 8 : 4));
		}
		List<ModNPC> npcs = ModContent.GetContent<ModNPC>().Where((ModNPC n) => !ContentSamples.NpcsByNetId[n.Type].townNPC && n.GetType().GetMethod(nameof(ModNPC.SpawnChance), new Type[1] { typeof(NPCSpawnInfo) })?.DeclaringType != typeof(ModNPC)).ToList();
		// Per mob: -3 left out (threw, inconsistent or never spawns), -2 not placed yet, -1 open from the start, else the
		// step that opens it. Bit v of open[i, s] is set when variant v (v's bit 1: night, 2: rain, 4: water) of stand s
		// worked with everything open; again[i, s] is the same asked twice.
		const int Skip = -3, Unplaced = -2, Ungated = -1;
		int[] state = new int[npcs.Count];
		int[,] open = new int[npcs.Count, stands.Count];
		int[,] again = new int[npcs.Count, stands.Count];
		int errors = 0, inconsistent = 0, several = 0;
		// Per gated mob: the steps it requires, the other ways to its gate, and the sets of mechanical bosses that let it
		// spawn (bit s: set s, whose bit b is mechs[b]).
		List<int>[] required = new List<int>[npcs.Count];
		List<int>[] others = new List<int>[npcs.Count];
		int[] mechSets = new int[npcs.Count];
		int[] mechs = Enumerable.Range(0, steps.Count).Where((int k) => Mechs.Contains(steps[k].Boss)).ToArray();
		object[] saved = flags.Select((FieldInfo f) => f.GetValue(null)).ToArray();
		bool dayTime = Main.dayTime, raining = Main.raining, eclipse = Main.eclipse, bloodMoon = Main.bloodMoon, pumpkinMoon = Main.pumpkinMoon, snowMoon = Main.snowMoon;
		float cloudAlpha = Main.cloudAlpha;
		int invasionType = Main.invasionType;
		try
		{
			// No event running: the probe asks about the place and the progression, not tonight's moon.
			Main.eclipse = (Main.bloodMoon = (Main.pumpkinMoon = (Main.snowMoon = false)));
			Main.invasionType = 0;
			Open((int k) => true, rest: true);
			Run((int i, int s, int v) => state[i] != Skip, (int i, int s, int v) => open[i, s] |= 1 << v);
			Run((int i, int s, int v) => state[i] != Skip, (int i, int s, int v) => again[i, s] |= 1 << v);
			for (int i = 0; i < npcs.Count; i++)
			{
				bool spawns = false, same = true;
				for (int s = 0; s < stands.Count; s++)
				{
					spawns |= open[i, s] != 0;
					same &= open[i, s] == again[i, s];
				}
				if (state[i] != Skip && !same)
				{
					inconsistent++;
				}
				state[i] = (state[i] == Skip || !same || !spawns) ? Skip : Unplaced;
			}
			for (int step = -1; step < steps.Count && state.Contains(Unplaced); step++)
			{
				Open((int k) => k <= step, rest: false);
				int opened = (step < 0) ? Ungated : step;
				Run((int i, int s, int v) => state[i] == Unplaced && (open[i, s] & (1 << v)) != 0, (int i, int s, int v) => state[i] = opened);
			}
			// The walk names the step after which the mob first spawns in the usual order; that step need not be required.
			// A step is required when the mob no longer spawns with everything else open. Only steps up to the gate can be:
			// the walk saw it spawn with every later one closed.
			List<int> gated = Enumerable.Range(0, npcs.Count).Where((int i) => state[i] >= 0).ToList();
			foreach (int i in gated)
			{
				required[i] = new List<int>();
				others[i] = new List<int>();
			}
			for (int k = 0; k <= gated.Select((int i) => state[i]).DefaultIfEmpty(-1).Max(); k++)
			{
				Open((int j) => j != k, rest: true);
				HashSet<int> still = Spawning(gated.Where((int i) => state[i] >= k));
				foreach (int i in gated.Where((int i) => state[i] >= k && !still.Contains(i)))
				{
					required[i].Add(k);
				}
			}
			// The mechanical bosses come in any order (Cryo Slime wants any two): each of the 7 sets of them is tried with
			// every other step before the last of them open and the later ones closed.
			int latest = mechs.DefaultIfEmpty(-1).Max();
			List<int> byMechs = gated.Where((int i) => mechs.Contains(state[i])).ToList();
			for (int set = 1; set < 8 && byMechs.Count > 0; set++)
			{
				Open(delegate(int k)
				{
					int bit = Array.IndexOf(mechs, k);
					return (bit >= 0) ? ((set & (1 << bit)) != 0) : (k < latest);
				}, rest: false);
				foreach (int i in Spawning(byMechs))
				{
					mechSets[i] |= 1 << set;
				}
			}
			// Any other gate the mob also spawns without is one way of several (Eidolist: hardmode or Calamitas Clone):
			// the steps whose closing as well stops it are the other ways.
			foreach (IGrouping<int, int> loose in from i in gated
				where !mechs.Contains(state[i]) && !required[i].Contains(state[i])
				group i by state[i])
			{
				for (int j = 0; j < steps.Count; j++)
				{
					if (j == loose.Key)
					{
						continue;
					}
					Open((int k) => k != loose.Key && k != j, rest: true);
					HashSet<int> still = Spawning(loose);
					foreach (int i in loose.Where((int i) => state[i] >= 0 && !still.Contains(i)))
					{
						others[i].Add(j);
					}
				}
			}
		}
		finally
		{
			for (int f = 0; f < flags.Length; f++)
			{
				flags[f].SetValue(null, saved[f]);
			}
			Main.dayTime = dayTime;
			Main.raining = raining;
			Main.cloudAlpha = cloudAlpha;
			Main.eclipse = eclipse;
			Main.bloodMoon = bloodMoon;
			Main.pumpkinMoon = pumpkinMoon;
			Main.snowMoon = snowMoon;
			Main.invasionType = invasionType;
			reset();
		}
		for (int i = 0; i < npcs.Count; i++)
		{
			if (state[i] < 0)
			{
				continue;
			}
			int variants = 0;
			List<Place> places = new List<Place>();
			for (int s = 0; s < stands.Count; s++)
			{
				variants |= open[i, s];
				if (open[i, s] != 0)
				{
					places.Add(stands[s].Place);
				}
			}
			// Variant v is bit v: the day ones are 0, 2, 4, 6 (0x55), the rainy ones 2, 3, 6, 7 (0xCC), the wet ones 4-7 (0xF0).
			bool day = (variants & 0x55) != 0, night = (variants & 0xAA) != 0;
			// Terms in step order: the required steps, then the gate's own one when it is not simply required (the
			// mechanical bosses' word, or its other ways; "?" when none of them was found).
			int gate = state[i];
			bool byMech = mechs.Contains(gate);
			List<(int Order, string Term, BookStage Stage)> terms = (from k in required[i]
				where !byMech || !mechs.Contains(k)
				select (k, steps[k].Boss, steps[k].Stage)).ToList();
			if (byMech)
			{
				terms.Add((gate, MechTerm(mechSets[i], mechs.Select((int k) => steps[k].Boss).ToArray()) ?? (steps[gate].Boss + ";?"), BookStage.Hardmode));
			}
			else if (!required[i].Contains(gate))
			{
				// A later boss is no other way past Hardmode or Moon Lord: beating it means the world is there already
				// (Eidolist's "hardmode or Calamitas Clone" is just hardmode).
				bool word = steps[gate].Boss is "Hardmode" or "PostHardmode";
				int[] ways = others[i].Where((int k) => !word || k < gate).ToArray();
				if (word && others[i].Count > 0 && ways.Length == 0)
				{
					terms.Add((gate, steps[gate].Boss, steps[gate].Stage));
				}
				else
				{
					several++;
					terms.Add((gate, steps[gate].Boss + ((ways.Length > 0) ? string.Concat(ways.Select((int k) => ";" + steps[k].Boss)) : ";?"), steps[gate].Stage));
				}
			}
			// Hardmode goes without saying next to a hardmode boss.
			if (terms.Any(t => t.Term != "Hardmode" && t.Stage >= BookStage.Hardmode))
			{
				terms.RemoveAll(t => t.Term == "Hardmode");
			}
			gates[npcs[i].Type] = new Gate(terms.OrderBy(t => t.Order).Select(t => t.Term).ToArray(), steps[gate].Stage, places.ToArray(), (day == night) ? null : day, (variants & ~0xCC) == 0, (variants & ~0xF0) == 0);
		}
		Summary = $"Spawn gate audit ({timer.ElapsedMilliseconds} ms): probed {npcs.Count} NPCs in {stands.Count} places over {steps.Count} steps; {gates.Count} got a gate ({several} of them one way of several), {state.Count((int s) => s == Ungated)} open from the start, {errors} errors ({inconsistent} inconsistent).";
		mod.Logger.Info(Summary);
		void Open(Func<int, bool> on, bool rest)
		{
			OpenSteps(flags, steps, on, rest);
		}
		// Which of these gated mobs spawn as the flags stand, in any variant that worked with everything open.
		HashSet<int> Spawning(IEnumerable<int> mobs)
		{
			HashSet<int> asked = mobs.ToHashSet(), yes = new HashSet<int>();
			Run((int i, int s, int v) => asked.Contains(i) && !yes.Contains(i) && state[i] >= 0 && (open[i, s] & (1 << v)) != 0, (int i, int s, int v) => yes.Add(i));
			return yes;
		}
		// Every place and variant some mob wants, the spawn conditions worked out once for it, then each such mob asked.
		void Run(Func<int, int, int, bool> wants, Action<int, int, int> spawns)
		{
			List<int> asked = new List<int>();
			for (int s = 0; s < stands.Count; s++)
			{
				for (int v = 0; v < stands[s].Variants; v++)
				{
					asked.Clear();
					for (int i = 0; i < npcs.Count; i++)
					{
						if (wants(i, s, v))
						{
							asked.Add(i);
						}
					}
					if (asked.Count == 0)
					{
						continue;
					}
					Main.dayTime = (v & 1) == 0;
					Main.raining = (v & 2) != 0;
					Main.cloudAlpha = Main.raining ? 1f : 0f;
					stands[s].Player.ZoneRain = Main.raining && stands[s].Place.Height <= Layer.Surface;
					NPCSpawnInfo info = stands[s].Info;
					info.Water = (v & 4) != 0;
					reset();
					check(info);
					foreach (int i in asked)
					{
						try
						{
							if (npcs[i].SpawnChance(info) > 0f)
							{
								spawns(i, s, v);
							}
						}
						catch (Exception)
						{
							// It keeps today's card.
							if (state[i] != Skip)
							{
								errors++;
							}
							state[i] = Skip;
						}
					}
				}
			}
		}
	}
}
