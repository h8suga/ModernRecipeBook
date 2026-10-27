using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.Sources;

// Drop conditions that test the dying NPC, not the world: Calamity's DropHelper.If((DropAttemptInfo info) => info.npc...)
// lambdas (LambdaDropRuleCondition, ...2 and ...3 keep the predicate in conditionLambda), vanilla's LegacyHack_IsABoss
// and MissingTwin. Found in the IL of what decides (the predicate delegates the condition holds, else its CanDrop, and the
// methods they hand the DropAttemptInfo to) by a read of DropAttemptInfo.npc; nothing runs. Such a condition is no
// progression gate, and the world cannot tell whether it holds: only the kill can.
internal static class BookKillConditions
{
	private static readonly FieldInfo NpcField = typeof(DropAttemptInfo).GetField(nameof(DropAttemptInfo.npc));

	private static readonly FieldInfo NpcTypeField = typeof(NPC).GetField(nameof(NPC.type));

	// Gates every normal kill of the boss meets, though its blank sample fails them. Astrum Deus drops only from the worms
	// it splits into in phase 2 (AstrumDeusHead.ShouldNotDropThings: newAI[0] != 0, and in Death mode or Boss Rush the last
	// of them), and the Eater of Worlds' last segment is made a boss right before its loot (NPC.DropEoWLoot), which vanilla's
	// LegacyHack_IsABoss and Calamity's info.npc.boss lambdas read. The NPCs, a member their predicate reads.
	private static readonly (string Npcs, string Reads)[] KillMet =
	{
		("CalamityMod/AstrumDeusHead", "AstrumDeusHead.ShouldNotDropThings"),
		("EaterofWorldsHead EaterofWorldsBody EaterofWorldsTail", "NPC.boss")
	};

	// Where the code says plainly what the kill needs, a label of its own (KillConditions area); else OnCertainKills. The
	// NPCs (null: any), a member the predicate reads, the label. The Wulfrum robots drop an Energy Core only while a Wulfrum
	// Amplifier supercharges them (WulfrumDrone/Gyrator/Hovercraft/Rover.ModifyNPCLoot: Supercharged, SuperchargeTimer > 0);
	// Nova drops Glorious End when it blows itself up (Nova.Explode sets ai[3] = -20000 and kills it; the loot asks for
	// ai[3] <= -10000); Providence gives the extra 75-90 Divine Geodes when the Profaned Core summoned it in a For the Worthy
	// world (ProfanedCore.UseItem sets hasBeenGivenFullPower only under Main.getGoodWorld); vanilla's Bloody Tear asks for a
	// Blood Moon and a mob not from a statue (Conditions.IsBloodMoonAndNotFromStatue, never in the drop simulation).
	private static readonly (string Npcs, string Reads, string Key)[] Labels =
	{
		(null, "WulfrumDrone.get_Supercharged WulfrumGyrator.get_Supercharged WulfrumHovercraft.get_Supercharged WulfrumRover.get_Supercharged", "KillConditions.WhileSupercharged"),
		("CalamityMod/Nova", "NPC.ai", "KillConditions.IfItBlowsItselfUp"),
		(null, "Providence.hasBeenGivenFullPower", "KillConditions.ForTheWorthySummon"),
		(null, "Main.bloodMoon", "KillConditions.BloodMoonNotFromStatue")
	};

	// What each condition's predicate reads and calls, when it reads the NPC (null when it does not), by reference.
	private static Dictionary<IItemDropRuleCondition, HashSet<MemberInfo>> reads = new Dictionary<IItemDropRuleCondition, HashSet<MemberInfo>>(ReferenceEqualityComparer.Instance);

	private static Dictionary<IItemDropRuleCondition, string[]> names = new Dictionary<IItemDropRuleCondition, string[]>(ReferenceEqualityComparer.Instance);

	internal static void Clear()
	{
		reads = new Dictionary<IItemDropRuleCondition, HashSet<MemberInfo>>(ReferenceEqualityComparer.Instance);
		names = new Dictionary<IItemDropRuleCondition, string[]>(ReferenceEqualityComparer.Instance);
	}

	internal static bool TestsNpc(IItemDropRuleCondition condition)
	{
		return Reads(condition) != null;
	}

	// Whether a normal kill of the NPC meets a condition that tests it: true when the NPC's sample meets it or KillMet says
	// so; false when the predicate reads nothing of the NPC but its type and the sample fails it (the drop is another NPC's
	// that shares the rules: Calamity's Exo Mech trophies, AresBody.DefineExoMechLoot); null when it depends on the kill.
	internal static bool? MetOnKill(IItemDropRuleCondition condition, int npcNetId)
	{
		HashSet<MemberInfo> members = Reads(condition);
		if (members == null || npcNetId == 0 || !ContentSamples.NpcsByNetId.TryGetValue(npcNetId, out NPC npc))
		{
			return null;
		}
		if (Passes(condition, npc))
		{
			return true;
		}
		string key = NpcKey(npc);
		if (condition is not BookItemSources.NotCondition && KillMet.Any(((string Npcs, string Reads) m) => Names(m.Npcs).Contains(key) && members.Any((MemberInfo r) => Name(r) == m.Reads)))
		{
			return true;
		}
		return members.All((MemberInfo m) => m == NpcField || m == NpcTypeField || (m is MethodInfo method && method.DeclaringType == typeof(ModContent) && method.Name == nameof(ModContent.NPCType))) ? false : null;
	}

	// A drop of another NPC that shares this one's rules (MetOnKill false on one of its conditions): not this NPC's loot.
	internal static bool OtherNpcs(DropRateInfo rate, int npcNetId)
	{
		return rate.conditions?.Any((IItemDropRuleCondition c) => MetOnKill(c, npcNetId) == false) ?? false;
	}

	internal static IEnumerable<string> LabelKeys => Labels.Select(((string Npcs, string Reads, string Key) x) => x.Key).Append("KillConditions.OnCertainKills");

	// The label of a condition that depends on the kill: its own where Labels has one, else the generic one.
	internal static string LabelKey(IItemDropRuleCondition condition, int npcNetId)
	{
		HashSet<MemberInfo> members = Reads(condition) ?? new HashSet<MemberInfo>();
		string key = ContentSamples.NpcsByNetId.TryGetValue(npcNetId, out NPC npc) && npcNetId != 0 ? NpcKey(npc) : "";
		foreach ((string npcs, string read, string label) in Labels)
		{
			if ((npcs == null || Names(npcs).Contains(key)) && members.Any((MemberInfo m) => Names(read).Contains(Name(m))))
			{
				return label;
			}
		}
		return "KillConditions.OnCertainKills";
	}

	private static readonly FieldInfo PlayerNameField = typeof(Player).GetField(nameof(Player.name));

	// The character names a condition asks for (Calamity's StarterBag gives its dev items by name:
	// "info.player.name == \"Dandy\"", "...ToLower() == \"big e\""), from the string literals of a predicate that reads
	// Player.name; empty for any other condition.
	internal static string[] PlayerNames(IItemDropRuleCondition condition)
	{
		if (condition == null || condition is BookItemSources.NotCondition)
		{
			return Array.Empty<string>();
		}
		if (!names.TryGetValue(condition, out string[] found))
		{
			try
			{
				List<MethodInfo> methods = Predicates(condition);
				found = methods.Any((MethodInfo m) => BookItemTraits.IlMembers(m, (OpCode op) => op == OpCodes.Ldfld).Any(((OpCode Op, MemberInfo Member) x) => x.Member == PlayerNameField))
					? methods.SelectMany(BookItemTraits.IlStrings).Distinct(StringComparer.Ordinal).ToArray()
					: Array.Empty<string>();
			}
			catch (Exception)
			{
				found = Array.Empty<string>();
			}
			names[condition] = found;
		}
		return found;
	}

	// For the dev audits: the condition's type and the method that decides it.
	internal static string Describe(IItemDropRuleCondition condition)
	{
		if (condition is BookItemSources.NotCondition not)
		{
			return "not " + Describe(not.Inner);
		}
		if (condition == null)
		{
			return "<null>";
		}
		string methods = string.Join(", ", Predicates(condition).Select((MethodInfo m) => (m.DeclaringType?.FullName ?? "?") + "." + m.Name));
		return condition.GetType().Name + (methods.Length > 0 ? " -> " + methods : "");
	}

	private static HashSet<MemberInfo> Reads(IItemDropRuleCondition condition)
	{
		if (condition is BookItemSources.NotCondition not)
		{
			return Reads(not.Inner);
		}
		if (condition == null)
		{
			return null;
		}
		if (!reads.TryGetValue(condition, out HashSet<MemberInfo> members))
		{
			members = new HashSet<MemberInfo>();
			try
			{
				foreach (MethodInfo method in Predicates(condition))
				{
					Walk(method, members, 3);
				}
			}
			catch (Exception)
			{
				members.Clear();
			}
			members = reads[condition] = members.Contains(NpcField) ? members : null;
		}
		return members;
	}

	// The predicate delegates a condition holds (fields of a delegate type that takes the DropAttemptInfo), else its CanDrop.
	private static List<MethodInfo> Predicates(IItemDropRuleCondition condition)
	{
		List<MethodInfo> methods = new List<MethodInfo>();
		Type type = condition.GetType();
		for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
		{
			foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			{
				if (typeof(Delegate).IsAssignableFrom(field.FieldType) && field.GetValue(condition) is Delegate value)
				{
					methods.AddRange(value.GetInvocationList().Select((Delegate d) => d.Method).Where(TakesInfo));
				}
			}
		}
		if (methods.Count == 0)
		{
			InterfaceMapping map = type.GetInterfaceMap(typeof(IItemDropRuleCondition));
			int index = Array.FindIndex(map.InterfaceMethods, (MethodInfo m) => m.Name == nameof(IItemDropRuleCondition.CanDrop));
			if (index >= 0)
			{
				methods.Add(map.TargetMethods[index]);
			}
		}
		return methods;
	}

	// Every field and method the IL names, and what it reads in the methods it hands the DropAttemptInfo to.
	private static void Walk(MethodInfo method, HashSet<MemberInfo> members, int depth)
	{
		foreach ((OpCode op, MemberInfo member) in BookItemTraits.IlMembers(method, (OpCode _) => true))
		{
			if (members.Add(member) && depth > 0 && (op == OpCodes.Call || op == OpCodes.Callvirt) && member is MethodInfo callee && TakesInfo(callee))
			{
				Walk(callee, members, depth - 1);
			}
		}
	}

	private static bool TakesInfo(MethodInfo method)
	{
		return method.GetParameters().Any((ParameterInfo p) => (p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType) == typeof(DropAttemptInfo));
	}

	private static bool Passes(IItemDropRuleCondition condition, NPC npc)
	{
		try
		{
			return condition.CanDrop(new DropAttemptInfo { player = Main.LocalPlayer, npc = npc, IsExpertMode = Main.expertMode, IsMasterMode = Main.masterMode, IsInSimulation = true, rng = Main.rand });
		}
		catch (Exception)
		{
			return false;
		}
	}

	// "CalamityMod/AstrumDeusHead" for a mod's NPC, the NPCID name for vanilla's.
	private static string NpcKey(NPC npc)
	{
		return npc.ModNPC?.FullName ?? (NPCID.Search.TryGetName(npc.type, out string name) ? name : "");
	}

	private static string Name(MemberInfo member)
	{
		return (member.DeclaringType?.Name ?? "") + "." + member.Name;
	}

	private static string[] Names(string names)
	{
		return names.Split(' ', StringSplitOptions.RemoveEmptyEntries);
	}
}
