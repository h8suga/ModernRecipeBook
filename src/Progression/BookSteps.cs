using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook.Progression;

// Progression steps: the bosses in Boss Checklist's order, read the way Boss Checklist reads them and without it. Step
// 0 is the start; step k means "boss k is down". Every item, recipe and mob gets the earliest step it can be had at
// (BookProgression), and the three stages are buckets of steps: below the Wall of Flesh's step is pre-hardmode, from it
// to Moon Lord's hardmode, from Moon Lord's on post-Moon Lord. "Before: X" shows everything below X's step: what can be
// had with every earlier boss down and X not.
//
// Boss Checklist hardcodes the vanilla bosses with fixed values (BossTracker: King Slime 1 ... Moon Lord 18) and gets
// the mods' from the mods themselves, which call it with "LogBoss". Calamity does that in
// WeakReferenceSupport.AddCalamityBosses(Mod bossChecklist, Mod calamity); the book calls that method with itself as
// the receiver (ModernRecipeBook.Call) and keeps what arrives: Calamity's own values, downed checks, NPCs, names and
// availability. The vanilla rows and, if that call fails, Calamity's are the built-in table below.
internal static class BookSteps
{
	// Key: Boss Checklist's ("Terraria Plantera", "CalamityMod AstrumAureus"), kept in saves. Npcs: every part that may
	// carry the loot. Available: false for the other world evil's boss (Calamity's Hive Mind in a crimson world).
	internal sealed record Step(string Key, string Mod, int[] Npcs, Func<bool> Downed, Func<string> Name, Func<bool> Available);

	// "<mod> <entry> <value> <npc,npc...> <flag>": Boss Checklist's vanilla values (BossTracker) and Calamity's
	// (WeakReferenceSupport.BossChecklistProgressionValues, 2.2.4); the flag is a static bool of NPC, Main or DD2Event,
	// or Calamity's DownedBossSystem field (read only). Calamity's rows stand in when its own registration cannot be
	// read, and add parts it does not list (Draedon for the Exo Mechs).
	private static readonly string[] Table =
	{
		"Terraria KingSlime 1 KingSlime downedSlimeKing",
		"CalamityMod DesertScourge 1.6 DesertScourgeHead,DesertScourgeBody,DesertScourgeTail _downedDesertScourge",
		"Terraria EyeofCthulhu 2 EyeofCthulhu downedBoss1",
		"CalamityMod Crabulon 2.7 Crabulon _downedCrabulon",
		"Terraria EaterofWorldsHead 3 EaterofWorldsHead,EaterofWorldsBody,EaterofWorldsTail,BrainofCthulhu downedBoss2",
		"CalamityMod HiveMind 3.98 HiveMind _downedHiveMind",
		"CalamityMod Perforators 3.99 PerforatorHive _downedPerforator",
		"Terraria QueenBee 4 QueenBee downedQueenBee",
		"Terraria SkeletronHead 5 SkeletronHead downedBoss3",
		"Terraria Deerclops 6 Deerclops downedDeerclops",
		"CalamityMod SlimeGod 6.7 SlimeGodCore,EbonianPaladin,CrimulanPaladin,SplitEbonianPaladin,SplitCrimulanPaladin _downedSlimeGod",
		"Terraria WallofFlesh 7 WallofFlesh hardMode",
		"Terraria QueenSlimeBoss 8 QueenSlimeBoss downedQueenSlime",
		"CalamityMod Cryogen 8.5 Cryogen _downedCryogen",
		"Terraria TheTwins 9 Retinazer,Spazmatism downedMechBoss2",
		"CalamityMod AquaticScourge 9.5 AquaticScourgeHead,AquaticScourgeBody,AquaticScourgeBodyAlt,AquaticScourgeTail _downedAquaticScourge",
		"Terraria TheDestroyer 10 TheDestroyer,TheDestroyerBody,TheDestroyerTail downedMechBoss1",
		"CalamityMod BrimstoneElemental 10.5 BrimstoneElemental _downedBrimstoneElemental",
		"Terraria SkeletronPrime 11 SkeletronPrime downedMechBoss3",
		"CalamityMod CalamitasClone 11.7 CalamitasClone,Cataclysm,Catastrophe _downedCalamitasClone",
		"Terraria Plantera 12 Plantera downedPlantBoss",
		"CalamityMod Leviathan 12.8 Leviathan,Anahita _downedLeviathan",
		"CalamityMod AstrumAureus 12.81 AstrumAureus _downedAstrumAureus",
		"Terraria Golem 13 Golem,GolemHead downedGolemBoss",
		"Terraria DukeFishron 14 DukeFishron downedFishron",
		"CalamityMod PlaguebringerGoliath 14.5 PlaguebringerGoliath _downedPlaguebringer",
		"Terraria HallowBoss 15 HallowBoss downedEmpressOfLight",
		"Terraria DD2Betsy 16 DD2Betsy DownedInvasionT3",
		"CalamityMod Ravager 16.5 RavagerBody,RavagerClawLeft,RavagerClawRight,RavagerHead,RavagerLegLeft,RavagerLegRight _downedRavager",
		"Terraria CultistBoss 17 CultistBoss downedAncientCultist",
		"CalamityMod AstrumDeus 17.5 AstrumDeusHead,AstrumDeusBody,AstrumDeusTail _downedAstrumDeus",
		"Terraria MoonLord 18 MoonLordCore,MoonLordHead,MoonLordHand downedMoonlord",
		"CalamityMod ProfanedGuardians 18.5 ProfanedGuardianCommander,ProfanedGuardianDefender,ProfanedGuardianHealer _downedGuardians",
		"CalamityMod Dragonfolly 18.6 Dragonfolly _downedDragonfolly",
		"CalamityMod Providence 19 Providence _downedProvidence",
		"CalamityMod CeaselessVoid 19.6 CeaselessVoid _downedCeaselessVoid",
		"CalamityMod StormWeaver 19.61 StormWeaverHead,StormWeaverBody,StormWeaverTail _downedStormWeaver",
		"CalamityMod Signus 19.62 Signus _downedSignus",
		"CalamityMod Polterghast 20 Polterghast _downedPolterghast",
		"CalamityMod OldDuke 20.5 OldDuke _downedBoomerDuke",
		"CalamityMod DevourerofGods 21 DevourerofGodsHead,DevourerofGodsBody,DevourerofGodsTail _downedDoG",
		"CalamityMod Yharon 22 Yharon _downedYharon",
		"CalamityMod ExoMechs 22.99 Draedon,Apollo,Artemis,AresBody,ThanatosHead _downedExoMechs",
		"CalamityMod Calamitas 23 SupremeCalamitas _downedCalamitas"
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

	// The built-in rows' world-evil bosses, as Calamity registers them ("availability"; Main.drunkWorld has both evils).
	private static readonly Dictionary<string, Func<bool>> Availability = new Dictionary<string, Func<bool>>(StringComparer.Ordinal)
	{
		["CalamityMod HiveMind"] = () => !WorldGen.crimson || Main.drunkWorld,
		["CalamityMod Perforators"] = () => WorldGen.crimson || Main.drunkWorld
	};

	private static List<Step> steps = new List<Step>();

	private static Dictionary<int, int> byNpc = new Dictionary<int, int>();

	private static Dictionary<string, int> byKey = new Dictionary<string, int>(StringComparer.Ordinal);

	private static bool built;

	// Where the modded bosses came from: "Calamity" (its own registration), "table" (the built-in rows) or "" (none).
	internal static string Source { get; private set; } = "";

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
		Source = "";
	}

	internal static void EnsureBuilt()
	{
		if (built)
		{
			return;
		}
		built = true;
		List<(float Value, Step Step)> table = FromTable();
		List<(float Value, Step Step)> calamity = null;
		Source = table.Any((x) => x.Step.Mod != "Terraria") ? "table" : "";
		string failure;
		try
		{
			calamity = FromCalamity(table, out failure);
		}
		catch (Exception error)
		{
			failure = (error.InnerException ?? error).Message;
		}
		if (failure != null)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Calamity's boss registration could not be read (" + failure + "); the book uses its own table for Calamity's bosses.");
		}
		if (calamity != null)
		{
			table = table.Where((x) => x.Step.Mod != "CalamityMod").Concat(calamity).ToList();
			Source = "Calamity";
		}
		// Boss Checklist's order: by value, the built-in order breaking ties.
		List<Step> list = table.OrderBy((x) => x.Value).Select((x) => x.Step).ToList();
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
		ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"Progression steps: {steps.Count - 1} bosses, mods' from " + Source switch
		{
			"Calamity" => "Calamity's own registration",
			"table" => "the built-in table",
			_ => "nowhere (vanilla only)"
		} + $"; Wall of Flesh at {HardmodeStep}, Moon Lord at {MoonLordStep}.");
	}

	// The built-in table, with the bosses of mods that are not loaded left out.
	private static List<(float, Step)> FromTable()
	{
		List<(float, Step)> list = new List<(float, Step)>();
		Type calamity = ModLoader.TryGetMod("CalamityMod", out Mod mod) ? mod.Code.GetType("CalamityMod.DownedBossSystem") : null;
		foreach (string row in Table)
		{
			string[] words = row.Split(' ');
			if (words[0] != "Terraria" && calamity == null)
			{
				continue;
			}
			int[] npcs = words[3].Split(',').Select((string name) => NpcType(words[0] == "Terraria" ? name : words[0] + "/" + name)).Where((int type) => type > 0).ToArray();
			FieldInfo flag = (words[0] == "Terraria" ? new[] { typeof(NPC), typeof(Main), typeof(DD2Event) } : new[] { calamity }).Select((Type t) => t.GetField(words[4], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)).FirstOrDefault((FieldInfo f) => f != null && f.FieldType == typeof(bool));
			if (npcs.Length == 0 || flag == null)
			{
				continue;
			}
			string key = words[0] + " " + words[1];
			// A compiled read: FieldInfo.GetValue would box the flag on every "next boss" check.
			Func<bool> downed = Expression.Lambda<Func<bool>>(Expression.Field(null, flag)).Compile();
			list.Add((float.Parse(words[2], CultureInfo.InvariantCulture), new Step(key, words[0], npcs, downed, TableName(words[0], words[1], npcs), Availability.GetValueOrDefault(key) ?? (() => true))));
		}
		return list;
	}

	private static Func<string> TableName(string mod, string entry, int[] npcs)
	{
		return (mod, entry) switch
		{
			("Terraria", "TheTwins") => () => Language.GetTextValue("Enemies.TheTwins"),
			("Terraria", "MoonLord") => () => Language.GetTextValue("Enemies.MoonLord"),
			// The world evil's boss; a drunk world has both evils and turns WorldGen.crimson over every dawn (Main.UpdateTime_StartDay).
			("Terraria", "EaterofWorldsHead") => () => Main.drunkWorld || Main.zenithWorld ? L("KillConditions.EitherEvilBoss", Lang.GetNPCNameValue(NPCID.EaterofWorldsHead), Lang.GetNPCNameValue(NPCID.BrainofCthulhu)) : Lang.GetNPCNameValue(WorldGen.crimson ? NPCID.BrainofCthulhu : NPCID.EaterofWorldsHead),
			("Terraria", _) => () => Lang.GetNPCNameValue(npcs[0]),
			_ => () => Language.GetTextValue("Mods." + mod + ".BossChecklistIntegration." + entry + ".EntryName") is string text && text != "Mods." + mod + ".BossChecklistIntegration." + entry + ".EntryName" ? text : Lang.GetNPCNameValue(npcs[0])
		};
	}

	// Calamity's bosses as it hands them to Boss Checklist: its LogBoss calls, caught by ModernRecipeBook.Call while its
	// WeakReferenceSupport.AddCalamityBosses runs with the book as the receiver (the mini-bosses it logs there are no
	// steps). Null when Calamity is not loaded or when the method or its calls are not what 2.2.4 has (failure says why).
	private static List<(float, Step)> FromCalamity(List<(float Value, Step Step)> table, out string failure)
	{
		failure = null;
		if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
		{
			return null;
		}
		MethodInfo register = calamity.Code.GetType("CalamityMod.WeakReferenceSupport")?.GetMethod("AddCalamityBosses", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Mod), typeof(Mod) }, null);
		if (register == null)
		{
			failure = "no WeakReferenceSupport.AddCalamityBosses(Mod, Mod)";
			return null;
		}
		List<object[]> logged = ModernRecipeBook.CaptureBossLogs(() => register.Invoke(null, new object[] { ModContent.GetInstance<ModernRecipeBook>(), calamity }));
		List<(float, Step)> list = new List<(float, Step)>();
		foreach (object[] args in logged.Where((object[] a) => (string)a[0] == "LogBoss"))
		{
			// LogBoss: hostMod, internal name, progression, downed, NPC id(s), extra info (Boss Checklist's Call).
			if (args.Length < 6 || args[1] is not Mod host || args[2] is not string name || args[4] is not Func<bool> downed)
			{
				failure = "a LogBoss call of another shape";
				return null;
			}
			List<int> logs = args[5] switch
			{
				int one => new List<int> { one },
				List<int> many => many,
				_ => new List<int>()
			};
			Dictionary<string, object> extra = args.Length > 6 ? args[6] as Dictionary<string, object> : null;
			string key = host.Name + " " + name;
			Step own = table.Select((x) => x.Step).FirstOrDefault((Step s) => s.Key == key);
			LocalizedText shown = extra?.GetValueOrDefault("displayName") as LocalizedText;
			Func<string> fallback = own?.Name ?? (() => logs.Count > 0 ? Lang.GetNPCNameValue(logs[0]) : name);
			// Calamity's text where it has one (Language.GetOrRegister gives a missing key back as its value).
			Func<string> named = shown == null ? fallback : () => shown.Value != shown.Key ? shown.Value : fallback();
			Func<bool> available = extra?.GetValueOrDefault("availability") as Func<bool> ?? own?.Available ?? (() => true);
			int[] npcs = logs.Concat(own?.Npcs ?? Array.Empty<int>()).Distinct().ToArray();
			list.Add((Convert.ToSingle(args[3], CultureInfo.InvariantCulture), new Step(key, host.Name, npcs, downed, named, available)));
		}
		if (list.Count == 0)
		{
			failure = "no LogBoss call arrived";
			return null;
		}
		return list;
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

	// A spawn gate's step: every requirement met (the latest), each the earliest of its ways. A mob open from the start has
	// no gate (its step comes from its drops, as for a mob the probe never saw).
	internal static int Gate(BookSpawnGates.Gate gate)
	{
		if (gate == null || gate.Terms.Length == 0)
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
