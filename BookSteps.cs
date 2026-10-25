using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// Progression steps: the bosses in Boss Checklist order. Step 0 is the start; step k means "boss k is down". Every item,
// recipe and mob gets the earliest step it can be had at (BookProgression), and the three stages are buckets of steps:
// below the Wall of Flesh's step is pre-hardmode, from it to Moon Lord's hardmode, from Moon Lord's on post-Moon Lord.
// "Before: X" shows everything below X's step: what can be had with every earlier boss down and X not.
internal static class BookSteps
{
	// Key: Boss Checklist's ("Terraria Plantera", "CalamityMod AstrumAureus"), kept in saves. Npcs: every part that may
	// carry the loot. Available: false for the other world evil's boss (Calamity's Hive Mind in a crimson world).
	internal sealed record Step(string Key, string Mod, int[] Npcs, Func<bool> Downed, Func<string> Name, Func<bool> Available);

	// Built-in order when Boss Checklist is not loaded: its vanilla values (BossTracker) merged with Calamity's
	// (WeakReferenceSupport.BossChecklistProgressionValues, 2.2.4). "<mod> <entry> <npc,npc...> <flag>": the flag is a
	// static bool of NPC, Main or DD2Event, or Calamity's DownedBossSystem field (read only).
	private static readonly string[] Table =
	{
		"Terraria KingSlime KingSlime downedSlimeKing",
		"CalamityMod DesertScourge DesertScourgeHead,DesertScourgeBody,DesertScourgeTail _downedDesertScourge",
		"Terraria EyeofCthulhu EyeofCthulhu downedBoss1",
		"CalamityMod Crabulon Crabulon _downedCrabulon",
		"Terraria EaterofWorldsHead EaterofWorldsHead,EaterofWorldsBody,EaterofWorldsTail,BrainofCthulhu downedBoss2",
		"CalamityMod HiveMind HiveMind _downedHiveMind",
		"CalamityMod Perforators PerforatorHive _downedPerforator",
		"Terraria QueenBee QueenBee downedQueenBee",
		"Terraria SkeletronHead SkeletronHead downedBoss3",
		"Terraria Deerclops Deerclops downedDeerclops",
		"CalamityMod SlimeGod SlimeGodCore,EbonianPaladin,CrimulanPaladin,SplitEbonianPaladin,SplitCrimulanPaladin _downedSlimeGod",
		"Terraria WallofFlesh WallofFlesh hardMode",
		"Terraria QueenSlimeBoss QueenSlimeBoss downedQueenSlime",
		"CalamityMod Cryogen Cryogen _downedCryogen",
		"Terraria TheTwins Retinazer,Spazmatism downedMechBoss2",
		"CalamityMod AquaticScourge AquaticScourgeHead,AquaticScourgeBody,AquaticScourgeBodyAlt,AquaticScourgeTail _downedAquaticScourge",
		"Terraria TheDestroyer TheDestroyer,TheDestroyerBody,TheDestroyerTail downedMechBoss1",
		"CalamityMod BrimstoneElemental BrimstoneElemental _downedBrimstoneElemental",
		"Terraria SkeletronPrime SkeletronPrime downedMechBoss3",
		"CalamityMod CalamitasClone CalamitasClone,Cataclysm,Catastrophe _downedCalamitasClone",
		"Terraria Plantera Plantera downedPlantBoss",
		"CalamityMod Leviathan Leviathan,Anahita _downedLeviathan",
		"CalamityMod AstrumAureus AstrumAureus _downedAstrumAureus",
		"Terraria Golem Golem,GolemHead downedGolemBoss",
		"Terraria DukeFishron DukeFishron downedFishron",
		"CalamityMod PlaguebringerGoliath PlaguebringerGoliath _downedPlaguebringer",
		"Terraria HallowBoss HallowBoss downedEmpressOfLight",
		"Terraria DD2Betsy DD2Betsy DownedInvasionT3",
		"CalamityMod Ravager RavagerBody,RavagerClawLeft,RavagerClawRight,RavagerHead,RavagerLegLeft,RavagerLegRight _downedRavager",
		"Terraria CultistBoss CultistBoss downedAncientCultist",
		"CalamityMod AstrumDeus AstrumDeusHead,AstrumDeusBody,AstrumDeusTail _downedAstrumDeus",
		"Terraria MoonLord MoonLordCore,MoonLordHead,MoonLordHand downedMoonlord",
		"CalamityMod ProfanedGuardians ProfanedGuardianCommander,ProfanedGuardianDefender,ProfanedGuardianHealer _downedGuardians",
		"CalamityMod Dragonfolly Dragonfolly _downedDragonfolly",
		"CalamityMod Providence Providence _downedProvidence",
		"CalamityMod CeaselessVoid CeaselessVoid _downedCeaselessVoid",
		"CalamityMod StormWeaver StormWeaverHead,StormWeaverBody,StormWeaverTail _downedStormWeaver",
		"CalamityMod Signus Signus _downedSignus",
		"CalamityMod Polterghast Polterghast _downedPolterghast",
		"CalamityMod OldDuke OldDuke _downedBoomerDuke",
		"CalamityMod DevourerofGods DevourerofGodsHead,DevourerofGodsBody,DevourerofGodsTail _downedDoG",
		"CalamityMod Yharon Yharon _downedYharon",
		"CalamityMod ExoMechs Draedon,Apollo,Artemis,AresBody,ThanatosHead _downedExoMechs",
		"CalamityMod Calamitas SupremeCalamitas _downedCalamitas"
	};

	// Names BookSpawnGates and the condition tables use for a step besides its NPCs' names.
	private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["Hardmode"] = "Terraria WallofFlesh",
		["PostHardmode"] = "Terraria MoonLord",
		["Enemies.TheTwins"] = "Terraria TheTwins",
		["CalamityMod/Draedon"] = "CalamityMod ExoMechs"
	};

	// Mini-bosses are no steps: they are had once they spawn. Calamity's: the Giant Clam from the start, the Great Sand
	// Shark after Plantera (CalamityNaturalSpawnBossNPC), the Acid Rain ones with their tier (BookBestiary.AcidRainOf).
	private static readonly Dictionary<string, string> MiniBosses = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["CalamityMod/GiantClam"] = "",
		["CalamityMod/GreatSandShark"] = "Plantera",
		["CalamityMod/CragmawMire"] = "CalamityMod/AquaticScourgeHead",
		["CalamityMod/Mauler"] = "CalamityMod/Polterghast",
		["CalamityMod/NuclearTerror"] = "CalamityMod/Polterghast"
	};

	// Boss Checklist's own availability is not in its dictionary; Calamity's two world-evil bosses are the only ones
	// in the table that need it (Main.drunkWorld has both evils).
	private static readonly Dictionary<string, Func<bool>> Availability = new Dictionary<string, Func<bool>>(StringComparer.Ordinal)
	{
		["CalamityMod HiveMind"] = () => !WorldGen.crimson || Main.drunkWorld,
		["CalamityMod Perforators"] = () => WorldGen.crimson || Main.drunkWorld
	};

	private static List<Step> steps = new List<Step>();

	private static Dictionary<int, int> byNpc = new Dictionary<int, int>();

	private static Dictionary<string, int> byKey = new Dictionary<string, int>(StringComparer.Ordinal);

	private static bool built;

	internal static bool FromBossChecklist { get; private set; }

	// The Wall of Flesh's and Moon Lord's steps: the stage buckets' edges.
	internal static int HardmodeStep { get; private set; } = 1;

	internal static int MoonLordStep { get; private set; } = 2;

	// Every step, the start first.
	internal static IReadOnlyList<Step> All
	{
		get
		{
			EnsureBuilt();
			return steps;
		}
	}

	internal static void Clear()
	{
		steps = new List<Step>();
		byNpc = new Dictionary<int, int>();
		byKey = new Dictionary<string, int>(StringComparer.Ordinal);
		built = false;
		FromBossChecklist = false;
	}

	internal static void EnsureBuilt()
	{
		if (built)
		{
			return;
		}
		built = true;
		List<Step> table = FromTable();
		List<Step> list = null;
		try
		{
			list = FromChecklist(table);
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Boss Checklist's entries could not be read; the book uses its own boss order.", error);
		}
		FromBossChecklist = list != null;
		list ??= table;
		list.Insert(0, new Step("start", "Terraria", Array.Empty<int>(), () => true, () => "", () => true));
		steps = list;
		byNpc = new Dictionary<int, int>();
		byKey = new Dictionary<string, int>(StringComparer.Ordinal);
		for (int k = 1; k < steps.Count; k++)
		{
			byKey.TryAdd(steps[k].Key, k);
			foreach (int npc in steps[k].Npcs)
			{
				byNpc.TryAdd(npc, k);
			}
		}
		HardmodeStep = byKey.GetValueOrDefault("Terraria WallofFlesh", 1);
		MoonLordStep = byKey.GetValueOrDefault("Terraria MoonLord", HardmodeStep + 1);
		ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"Progression steps: {steps.Count - 1} bosses from " + (FromBossChecklist ? "Boss Checklist" : "the built-in table") + $"; Wall of Flesh at {HardmodeStep}, Moon Lord at {MoonLordStep}.");
	}

	// The built-in table, with the bosses of mods that are not loaded left out.
	private static List<Step> FromTable()
	{
		List<Step> list = new List<Step>();
		Type calamity = ModLoader.TryGetMod("CalamityMod", out Mod mod) ? mod.Code.GetType("CalamityMod.DownedBossSystem") : null;
		foreach (string row in Table)
		{
			string[] words = row.Split(' ');
			if (words[0] != "Terraria" && calamity == null)
			{
				continue;
			}
			int[] npcs = words[2].Split(',').Select((string name) => NpcType(words[0] == "Terraria" ? name : words[0] + "/" + name)).Where((int type) => type > 0).ToArray();
			FieldInfo flag = (words[0] == "Terraria" ? new[] { typeof(NPC), typeof(Main), typeof(DD2Event) } : new[] { calamity }).Select((Type t) => t.GetField(words[3], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)).FirstOrDefault((FieldInfo f) => f != null && f.FieldType == typeof(bool));
			if (npcs.Length == 0 || flag == null)
			{
				continue;
			}
			string key = words[0] + " " + words[1];
			list.Add(new Step(key, words[0], npcs, () => (bool)flag.GetValue(null), TableName(words[0], words[1], npcs), Availability.GetValueOrDefault(key) ?? (() => true)));
		}
		return list;
	}

	private static Func<string> TableName(string mod, string entry, int[] npcs)
	{
		return (mod, entry) switch
		{
			("Terraria", "TheTwins") => () => Language.GetTextValue("Enemies.TheTwins"),
			("Terraria", "MoonLord") => () => Language.GetTextValue("Enemies.MoonLord"),
			("Terraria", "EaterofWorldsHead") => () => Lang.GetNPCNameValue(WorldGen.crimson ? NPCID.BrainofCthulhu : NPCID.EaterofWorldsHead),
			("Terraria", _) => () => Lang.GetNPCNameValue(npcs[0]),
			_ => () => Language.Exists("Mods." + mod + ".BossChecklistIntegration." + entry + ".EntryName") ? Language.GetTextValue("Mods." + mod + ".BossChecklistIntegration." + entry + ".EntryName") : Lang.GetNPCNameValue(npcs[0])
		};
	}

	// Boss Checklist's bosses (not its mini-bosses or events) in its order; entries with the same progression value are
	// one step (the Eater of Worlds and the Brain of Cthulhu). The built-in table adds parts Boss Checklist does not list
	// (Draedon for the Exo Mechs). Null when Boss Checklist is not loaded.
	private static List<Step> FromChecklist(List<Step> table)
	{
		if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist))
		{
			return null;
		}
		if (checklist.Call("GetBossInfoDictionary", ModContent.GetInstance<ModernRecipeBook>(), "1.6") is not Dictionary<string, Dictionary<string, object>> entries)
		{
			throw new InvalidOperationException("GetBossInfoDictionary returned no dictionary");
		}
		List<Step> list = new List<Step>();
		foreach (IGrouping<float, Dictionary<string, object>> group in from entry in entries.Values
			where entry.GetValueOrDefault("isBoss") is true && entry.GetValueOrDefault("progression") is float
			group entry by (float)entry["progression"] into g
			orderby g.Key
			select g)
		{
			List<Dictionary<string, object>> same = group.ToList();
			string key = (string)same[0]["key"];
			Func<bool>[] downed = same.Select((Dictionary<string, object> e) => e.GetValueOrDefault("downed") as Func<bool>).Where((Func<bool> f) => f != null).ToArray();
			LocalizedText[] names = same.Select((Dictionary<string, object> e) => e.GetValueOrDefault("displayName") as LocalizedText).Where((LocalizedText t) => t != null).ToArray();
			// The built-in row with the same NPCs: its key, so a saved "Before" boss survives Boss Checklist coming or
			// going (Boss Checklist names vanilla bosses its own way, "Terraria Skeleton").
			int[] listed = same.SelectMany((Dictionary<string, object> e) => e.GetValueOrDefault("npcIDs") as List<int> ?? new List<int>()).ToArray();
			Step own = table.FirstOrDefault((Step s) => s.Npcs.Intersect(listed).Any());
			int[] npcs = listed.Concat(own?.Npcs ?? Array.Empty<int>()).Distinct().ToArray();
			string mod = same[0].GetValueOrDefault("modSource") as string ?? key.Split(' ')[0];
			// The world's evil picks between two entries of one step.
			Func<string> name = (names.Length == 2 && key == "Terraria EaterofWorldsHead") ? () => names[WorldGen.crimson ? 1 : 0].Value : () => string.Join(" / ", names.Select((LocalizedText t) => t.Value));
			list.Add(new Step(own?.Key ?? key, mod, npcs, () => downed.Any((Func<bool> f) => f()), name, Availability.GetValueOrDefault(key) ?? (() => true)));
		}
		return list.Count > 0 ? list : null;
	}

	private static int NpcType(string name)
	{
		if (name.Contains('/'))
		{
			return ModContent.TryFind(name, out ModNPC npc) ? npc.Type : 0;
		}
		return NPCID.Search.TryGetId(name, out int id) ? id : 0;
	}

	internal static BookStage StageOf(int step)
	{
		if (step < 0)
		{
			return BookStage.Unknown;
		}
		return step < HardmodeStep ? BookStage.PreHardmode : (step < MoonLordStep ? BookStage.Hardmode : BookStage.PostHardmode);
	}

	// The first step of a stage.
	internal static int Of(BookStage stage)
	{
		return stage switch
		{
			BookStage.PreHardmode => 0,
			BookStage.Hardmode => HardmodeStep,
			BookStage.PostHardmode => MoonLordStep,
			_ => -1
		};
	}

	// The step a boss NPC's loot belongs to; -1 for any other NPC.
	internal static int Npc(int type)
	{
		EnsureBuilt();
		return byNpc.TryGetValue(type, out int step) ? step : -1;
	}

	internal static int Find(string key)
	{
		EnsureBuilt();
		return key != null && byKey.TryGetValue(key, out int step) ? step : -1;
	}

	// A requirement as the condition tables and BookSpawnGates write it, as a step (-1 when it names nothing loaded):
	// "" the start; a boss ("Plantera", "CalamityMod/Leviathan", "EaterofWorldsHead|BrainofCthulhu"); "Hardmode" and
	// "PostHardmode"; "mechs:any", "mechs:two", "mechs:all" (the first, second and last of the mechanical bosses in
	// this order); "A;B" either (the earlier), "A+B" both (the later). "?" is a way the probe could not name: unknown.
	internal static int Resolve(string term)
	{
		EnsureBuilt();
		if (string.IsNullOrEmpty(term))
		{
			return 0;
		}
		if (term.Contains(';'))
		{
			// The earliest way, among those this world has (the Perforators, not the Hive Mind, in a crimson world).
			int[] ways = term.Split(';').Select(Resolve).Where((int s) => s >= 0).ToArray();
			int[] here = ways.Where((int s) => s == 0 || steps[s].Available()).ToArray();
			return here.Length > 0 ? here.Min() : (ways.Length > 0 ? ways.Min() : -1);
		}
		if (term.Contains('+'))
		{
			int[] parts = term.Split('+').Select(Resolve).ToArray();
			return parts.Contains(-1) ? -1 : parts.Max();
		}
		if (term.StartsWith("mechs:", StringComparison.Ordinal))
		{
			int[] mechs = new[] { "Terraria TheTwins", "Terraria TheDestroyer", "Terraria SkeletronPrime" }.Select(Find).Where((int s) => s >= 0).OrderBy((int s) => s).ToArray();
			int count = term switch
			{
				"mechs:any" => 1,
				"mechs:two" => 2,
				_ => 3
			};
			return mechs.Length >= count ? mechs[count - 1] : -1;
		}
		if (term == "?")
		{
			return -1;
		}
		if (Aliases.TryGetValue(term, out string key))
		{
			return Find(key);
		}
		if (MiniBosses.TryGetValue(term, out string after))
		{
			return Resolve(after);
		}
		return Npc(NpcType(term.Split('|')[0]));
	}

	// A mini-boss NPC's step (see MiniBosses); -1 for any other NPC.
	internal static int MiniBoss(int type)
	{
		ModNPC npc = NPCLoader.GetNPC(type);
		return npc != null && MiniBosses.TryGetValue(npc.Mod.Name + "/" + npc.Name, out string after) ? Resolve(after) : -1;
	}

	// A spawn gate's step: every requirement met (the latest), each the earliest of its ways.
	internal static int Gate(BookSpawnGates.Gate gate)
	{
		if (gate == null)
		{
			return -1;
		}
		int step = 0;
		foreach (string term in gate.Terms)
		{
			int s = Resolve(term);
			// A way the probe could not name: the step it found the mob at is the best known.
			step = Math.Max(step, s >= 0 ? s : Of(gate.Stage));
		}
		return step;
	}

	// The first boss not yet down in this world (the other evil's boss skipped): what "Before: next boss" means.
	internal static int Next()
	{
		EnsureBuilt();
		for (int k = 1; k < steps.Count; k++)
		{
			if (steps[k].Available() && !SafeDowned(steps[k]))
			{
				return k;
			}
		}
		return steps.Count;
	}

	internal static bool SafeDowned(Step step)
	{
		try
		{
			return step.Downed();
		}
		catch (Exception)
		{
			return false;
		}
	}

	internal static string Name(int step)
	{
		EnsureBuilt();
		if (step <= 0 || step >= steps.Count)
		{
			return "";
		}
		try
		{
			return steps[step].Name();
		}
		catch (Exception)
		{
			return steps[step].Key;
		}
	}

	// The NPC whose map head stands for the step: the first part that has one, the world's evil picking for the
	// Eater of Worlds and the Brain of Cthulhu. 0 when none has a head (the card falls back to the sprite).
	internal static int HeadNpc(int step, out int head)
	{
		head = -1;
		if (step <= 0 || step >= All.Count)
		{
			return 0;
		}
		IEnumerable<int> npcs = steps[step].Npcs;
		if (WorldGen.crimson && npcs.Contains(NPCID.BrainofCthulhu))
		{
			npcs = npcs.OrderBy((int n) => n == NPCID.BrainofCthulhu ? 0 : 1);
		}
		foreach (int npc in npcs)
		{
			if (ContentSamples.NpcsByNetId.TryGetValue(npc, out NPC sample))
			{
				int index = sample.GetBossHeadTextureIndex();
				if (index >= 0 && index < Terraria.GameContent.TextureAssets.NpcHeadBoss.Length)
				{
					head = index;
					return npc;
				}
			}
		}
		return steps[step].Npcs.FirstOrDefault();
	}
}
