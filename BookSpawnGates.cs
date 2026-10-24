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

namespace ModernRecipeBook;

// Mods gate many mobs inside ModNPC.SpawnChance (Calamity's Plague Charger: the hardmode jungle once Golem is down),
// which no Bestiary tag shows. As BookItemClasses runs items on a throwaway player, this asks SpawnChance itself: a
// throwaway player stands in each place while the world's progression flags open step by step in Boss Checklist
// order, and the first step after which the mob may spawn is its gate. Once per load, in single player only, on the
// main thread between two updates; every flag it sets is put back before it returns.
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

	// A gated mob: the step that opens it (Steps' first word) and that step's stage, where it may spawn once everything
	// is open, and what every try that worked had in common (Day null: day and night both work).
	internal sealed record Gate(string Boss, BookStage Stage, Place[] Places, bool? Day, bool Rain, bool Water);

	// Progression in Boss Checklist order (its vanilla values; Calamity's from WeakReferenceSupport.
	// BossChecklistProgressionValues in 2.2.4): the boss that names the step ("A|B": the corruption's and the
	// crimson's), then the flags it sets: NPC.downed* fields, Main.hardMode and Calamity's DownedBossSystem fields
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
		"CalamityMod/HiveMind|CalamityMod/PerforatorHive _downedHiveMind _downedPerforator",
		"QueenBee downedQueenBee",
		"SkeletronHead downedBoss3",
		"Deerclops downedDeerclops",
		"CalamityMod/SlimeGodCore _downedSlimeGod",
		"Hardmode hardMode",
		"QueenSlimeBoss downedQueenSlime",
		"CalamityMod/Cryogen _downedCryogen",
		"Enemies.TheTwins downedMechBoss2 downedMechBossAny",
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
		"MoonLordCore downedMoonlord",
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

	private static bool skipNoted;

	// The audit line of the last probe, for the test stand.
	internal static string Summary = "";

	internal static void Clear()
	{
		gates.Clear();
		built = false;
		skipNoted = false;
		Summary = "";
	}

	internal static Gate Of(int type)
	{
		return gates.GetValueOrDefault(type);
	}

	internal static BookStage Stage(int type)
	{
		return gates.TryGetValue(type, out Gate gate) ? gate.Stage : BookStage.Unknown;
	}

	// The probe sets world flags for a moment. They are local statics and nothing is sent, but a mod's SpawnChance
	// never runs on a multiplayer client in the game itself, so the client leaves it out and keeps the Bestiary's tags.
	internal static void EnsureBuilt()
	{
		if (built || Main.gameMenu)
		{
			return;
		}
		if (Main.netMode != NetmodeID.SinglePlayer)
		{
			if (!skipNoted)
			{
				skipNoted = true;
				ModContent.GetInstance<ModernRecipeBook>().Logger.Info("Spawn gates: not probed in multiplayer; mob cards keep the Bestiary's spawn tags.");
			}
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
			select string.Join('\t', pair.Key, NPCLoader.GetNPC(pair.Key)?.FullName, gate.Boss, gate.Stage, string.Join(",", gate.Places.Select((Place p) => p.Key)), gate.Day, gate.Rain, gate.Water);
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
			reached = (BookStage)Math.Max((int)reached, (int)(boss == "Hardmode" ? BookStage.Hardmode : (type > 0 ? BookProgressionSources.NpcStage(type) : BookStage.Unknown)));
			steps.Add((words[0], set, reached));
		}
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
			// The map's outermost column for land (so SpawnCondition's wall checks read no cabin or spider nest), inside the
			// ocean's 250 tiles for the sea.
			int x = traits.Contains("sea") ? 100 : 41;
			int y = place.Height switch
			{
				Layer.Space => (int)(Main.worldSurface * 0.25),
				Layer.Surface => (int)Main.worldSurface - 30,
				Layer.Dirt => (int)((Main.worldSurface + Main.rockLayer) / 2.0),
				Layer.Rock => (int)Main.rockLayer + 100,
				_ => Main.maxTilesY - 100
			};
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
		int errors = 0, inconsistent = 0;
		object[] saved = flags.Select((FieldInfo f) => f.GetValue(null)).ToArray();
		bool dayTime = Main.dayTime, raining = Main.raining, eclipse = Main.eclipse, bloodMoon = Main.bloodMoon, pumpkinMoon = Main.pumpkinMoon, snowMoon = Main.snowMoon;
		float cloudAlpha = Main.cloudAlpha;
		int invasionType = Main.invasionType;
		try
		{
			// No event running: the probe asks about the place and the progression, not tonight's moon.
			Main.eclipse = (Main.bloodMoon = (Main.pumpkinMoon = (Main.snowMoon = false)));
			Main.invasionType = 0;
			SetAll(true);
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
			SetAll(false);
			for (int step = -1; step < steps.Count && state.Contains(Unplaced); step++)
			{
				if (step >= 0)
				{
					foreach (FieldInfo flag in steps[step].Flags)
					{
						flag.SetValue(null, true);
					}
				}
				int opened = (step < 0) ? Ungated : step;
				Run((int i, int s, int v) => state[i] == Unplaced && (open[i, s] & (1 << v)) != 0, (int i, int s, int v) => state[i] = opened);
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
			gates[npcs[i].Type] = new Gate(steps[state[i]].Boss, steps[state[i]].Stage, places.ToArray(), (day == night) ? null : day, (variants & ~0xCC) == 0, (variants & ~0xF0) == 0);
		}
		Summary = $"Spawn gate audit ({timer.ElapsedMilliseconds} ms): probed {npcs.Count} NPCs in {stands.Count} places over {steps.Count} steps; {gates.Count} got a gate, {state.Count((int s) => s == Ungated)} open from the start, {errors} errors ({inconsistent} inconsistent).";
		mod.Logger.Info(Summary);
		void SetAll(bool value)
		{
			foreach (FieldInfo flag in flags)
			{
				flag.SetValue(null, value);
			}
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
