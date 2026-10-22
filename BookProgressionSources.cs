using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook;

internal static class BookProgressionSources
{
	private static readonly HashSet<string> PostItems = Set("ExodiumCluster GalacticaSingularity UnholyEssence EffulgentFeather DivineGeode UelibloomOre UelibloomBar Bloodstone BloodstoneCore Phantoplasm RuinousSoul ReaperTooth ArmoredShell TwistingNether DarkPlasma CosmiliteBar EndothermicEnergy NightmareFuel DarksunFragment AscendantSpiritEssence YharonSoulFragment AuricOre AuricBar AshesofAnnihilation ExoPrism ShadowspecBar MiracleMatter DraedonsForge CosmicAnvil ProfanedCrucible CelestialOnion");

	// Draedon lab materials: lab turrets and power cell factories are reachable before hardmode, but the
	// items carry Calamity's DarkOrange rarity, which is used for every arsenal tier.
	private static readonly HashSet<string> PreItems = Set("DubiousPlating MysteriousCircuitry DraedonPowerCell");

	// Calamity's own rarities that only appear after Moon Lord (DarkOrange is excluded, see PreItems).
	private static readonly HashSet<string> PostRarities = Set("Turquoise PureGreen CosmicPurple BurnishedAuric ExoticRainbow HotPink CalamityRed");

	private static readonly HashSet<string> PreWorldItems = Set("Wood BorealWood RichMahogany Ebonwood Shadewood PalmWood AshWood Bamboo StoneBlock DirtBlock ClayBlock SandBlock EbonsandBlock CrimsandBlock MudBlock SnowBlock IceBlock SiltBlock SlushBlock DesertFossil Cobweb Gel FallenStar Acorn CopperOre TinOre IronOre LeadOre SilverOre TungstenOre GoldOre PlatinumOre DemoniteOre CrimtaneOre Meteorite Hellstone Obsidian SturdyFossil Amethyst Topaz Sapphire Emerald Ruby Diamond Amber Mushroom GlowingMushroom Daybloom Moonglow Blinkroot Deathweed Waterleaf Fireblossom Shiverthorn GrassSeeds JungleGrassSeeds MushroomGrassSeeds CorruptSeeds CrimsonSeeds AshGrassSeeds VileMushroom ViciousMushroom Bone RottenChunk Vertebrae JungleSpores Stinger Vine Lens BlackLens AntlionMandible SharkFin Feather");

	// Vanilla items whose rarity overstates when they can be obtained.
	private static readonly HashSet<string> PreRarityExceptions = Set("RedPotion SlimeStaff DogWhistle CompanionCube CelestialWand");

	// Boss and event NPCs whose drops inherit the stage. Calamity entries follow its own BossChecklist
	// progression (WeakReferenceSupport.BossChecklistProgressionValues in 2.2.4): below Wall of Flesh (7)
	// is pre-hardmode, up to Moon Lord (18) hardmode, above that post-Moon Lord. Wall of Flesh drops count
	// as hardmode, like their rarity does.
	private static readonly Dictionary<string, BookStage> VanillaNpcs = Stages(
		"KingSlime EyeofCthulhu EaterofWorldsHead EaterofWorldsBody EaterofWorldsTail BrainofCthulhu QueenBee SkeletronHead Deerclops DD2DarkMageT1",
		"WallofFlesh QueenSlimeBoss Retinazer Spazmatism TheDestroyer SkeletronPrime Plantera Golem DukeFishron HallowBoss CultistBoss DD2Betsy DD2DarkMageT3 DD2OgreT2 DD2OgreT3 MourningWood Pumpking Everscream SantaNK1 IceQueen MartianSaucerCore PirateShip Mothron BloodNautilus LunarTowerSolar LunarTowerVortex LunarTowerNebula LunarTowerStardust",
		"MoonLordCore");

	private static readonly Dictionary<string, BookStage> CalamityNpcs = Stages(
		"DesertScourgeHead DesertScourgeBody DesertScourgeTail GiantClam Crabulon HiveMind PerforatorHive SlimeGodCore EbonianPaladin CrimulanPaladin SplitEbonianPaladin SplitCrimulanPaladin",
		"Cryogen AquaticScourgeHead AquaticScourgeBody AquaticScourgeBodyAlt AquaticScourgeTail CragmawMire BrimstoneElemental CalamitasClone GreatSandShark Leviathan Anahita AstrumAureus PlaguebringerGoliath RavagerBody AstrumDeusHead AstrumDeusBody AstrumDeusTail",
		"ProfanedGuardianCommander ProfanedGuardianDefender ProfanedGuardianHealer Dragonfolly Providence CeaselessVoid StormWeaverHead StormWeaverBody StormWeaverTail Signus Polterghast Mauler NuclearTerror OldDuke DevourerofGodsHead DevourerofGodsBody DevourerofGodsTail Yharon Apollo Artemis AresBody ThanatosHead SupremeCalamitas PrimordialWyrmHead");

	// Town NPCs that move in only in hardmode or after Moon Lord; every other shop opens before hardmode.
	private static readonly Dictionary<string, BookStage> VanillaTownNpcs = Stages("", "Wizard Truffle Steampunker Cyborg Pirate SantaClaus TaxCollector", "");

	private static readonly Dictionary<string, BookStage> CalamityTownNpcs = Stages("", "Archmage", "BrimstoneWitch");

	// Recipe and shop conditions (Condition.Description keys). Other "Conditions.*" keys are season,
	// biome, moon phase or pre-hardmode gates and add nothing.
	private static readonly HashSet<string> HardmodeConditions = Set("Conditions.InHardmode Conditions.SolarEclipse Conditions.DownedClown Conditions.DownedMechBossAny Conditions.DownedMechBossAll Conditions.DownedTwins Conditions.DownedDestroyer Conditions.DownedSkeletronPrime Conditions.DownedPlantera Conditions.DownedGolem Conditions.DownedDukeFishron Conditions.DownedEmpressOfLight Conditions.DownedQueenSlime Conditions.DownedLunaticCultist Conditions.DownedPirates Conditions.DownedMartians Conditions.DownedFrostLegion Conditions.DownedMourningWood Conditions.DownedPumpking Conditions.DownedEverscream Conditions.DownedSantaNK1 Conditions.DownedIceQueen Conditions.DownedSolarPillar Conditions.DownedVortexPillar Conditions.DownedNebulaPillar Conditions.DownedStardustPillar Conditions.DownedOldOnesArmyT2 Conditions.DownedOldOnesArmyT3");

	private static readonly HashSet<string> PostConditions = Set("Conditions.DownedMoonLord");

	// Special-seed worlds (Zenith/"getfixedboi", drunk, remix...): content gated on them is not part of
	// normal progression, e.g. Calamity's Zenith-world boss drops.
	private static readonly HashSet<string> SeedConditions = Set("Conditions.WorldAnniversary Conditions.WorldDontStarve Conditions.WorldDrunk Conditions.WorldForTheWorthy Conditions.WorldNoTraps Conditions.WorldNotTheBees Conditions.WorldRemix Conditions.WorldZenith");

	private static readonly HashSet<string> SeedDropConditions = Set("NeverTrue RemixSeed RemixSeedEasymode RemixSeedHardmode TenthAnniversaryIsUp DontStarveIsUp DrunkWorldIsUp ForTheWorthyIsUp BeesSeed NoTrapsSeed ZenithSeedIsUp");

	// CalamityConditions keys ("Mods.CalamityMod.Condition." + suffix), staged like the bosses above.
	private const string CalamityConditionPrefix = "Mods.CalamityMod.Condition.";

	// Difficulty gates decide whether something exists in the world being played, not when: they are
	// evaluated against the current world (Classic/Expert/Master/Journey, Calamity's Revengeance; Death mode
	// gates no drops in 2.2.4). Calamity's "InRev" key covers both "Rev" and "Rev, not Master", so these are
	// always judged by their predicate.
	private static readonly HashSet<string> ModeConditions = Set("Conditions.InClassicMode Conditions.InExpertMode Conditions.InMasterMode Conditions.InJourneyMode Mods.CalamityMod.Condition.InRev Mods.CalamityMod.Condition.InRevOrMM");

	private static readonly HashSet<string> CalamityPreConditions = Set("HasRogueArmor HasWings InCrag InSulph InSunken Drops.DownedT1AR Drops.DownedDS Drops.DownedCrabulon Drops.DownedHM Drops.DownedPerfs Drops.DownedHMOrPerfs Drops.DownedSG Drops.DownedClam");

	private static readonly HashSet<string> CalamityHardmodeConditions = Set("InAstral Drops.DownedT2AR Drops.DownedCryo Drops.DownedAS Drops.DownedBrim Drops.DownedCal Drops.DownedCalPlant Drops.DownedLevi Drops.DownedAureus Drops.DownedPBG Drops.DownedRav Drops.DownedAD Drops.DownedClamHM Drops.DownedGSS Drops.DownedBetsy Drops.DownedDreadnautilus");

	private static readonly HashSet<string> CalamityPostConditions = Set("Drops.DownedGuard Drops.DownedBirb Drops.DownedProv Drops.DownedSig Drops.DownedSW Drops.DownedCV Drops.DownedPolter Drops.DownedOD Drops.DownedDoG Drops.DownedYharon Drops.DownedExos Drops.DownedSCal Drops.DownedAEW");

	// Vanilla item drop rule conditions (Terraria.GameContent.ItemDropRules.Conditions) that gate a drop to
	// hardmode. Every other vanilla condition (difficulty, world evil, seeds, statues, seasons) adds nothing.
	private static readonly HashSet<string> HardmodeDropConditions = Set("IsHardmode DownedAllMechBosses DownedPlantera FirstTimeKillingPlantera MechanicalBossesDummyCondition BeatAnyMechBoss MechdusaKill PirateMap SoulOfLight SoulOfNight LivingFlames HallowKeyCondition JungleKeyCondition CorruptKeyCondition CrimsonKeyCondition FrozenKeyCondition DesertKeyCondition YoyosAmarok YoyosYelets YoyosKraken YoyosHelFire IsPumpkinMoon FrostMoonDropGatingChance PumpkinMoonDropGatingChance FrostMoonDropGateForTrophies PumpkinMoonDropGateForTrophies FromCertainWaveAndAbove RemixSeedHardmode EmpressOfLightIsGenuinelyEnraged");

	internal static BookStage VanillaNaturalStage(string name)
	{
		if (name == null || !PreWorldItems.Contains(name))
		{
			return BookStage.Unknown;
		}
		return BookStage.PreHardmode;
	}

	// Curated stage for an item: a floor that recipes and drops can never go below.
	internal static BookStage ItemStage(Item item)
	{
		if (ItemID.Sets.IsFishingCrateHardmode[item.type])
		{
			return BookStage.Hardmode;
		}
		if (item.ModItem?.Mod.Name != "CalamityMod")
		{
			return BookStage.Unknown;
		}
		if (PostItems.Contains(item.ModItem.Name))
		{
			return BookStage.PostHardmode;
		}
		return PreItems.Contains(item.ModItem.Name) ? BookStage.PreHardmode : BookStage.Unknown;
	}

	internal static BookStage NpcStage(NPC npc)
	{
		BookStage stage;
		if (npc.ModNPC == null)
		{
			return VanillaNpcs.TryGetValue(NPCID.Search.GetName(npc.type) ?? "", out stage) ? stage : BookStage.Unknown;
		}
		return npc.ModNPC.Mod.Name == "CalamityMod" && CalamityNpcs.TryGetValue(npc.ModNPC.Name, out stage) ? stage : BookStage.Unknown;
	}

	internal static BookStage TownNpcStage(int type)
	{
		ModNPC modNpc = NPCLoader.GetNPC(type);
		BookStage stage;
		if (modNpc == null)
		{
			return NPCID.Search.TryGetName(type, out var name) && VanillaTownNpcs.TryGetValue(name, out stage) ? stage : BookStage.PreHardmode;
		}
		return modNpc.Mod.Name == "CalamityMod" && CalamityTownNpcs.TryGetValue(modNpc.Name, out stage) ? stage : BookStage.PreHardmode;
	}

	// Last resort when no recipe or known drop explains an item: its rarity tier. Calamity uses the vanilla
	// scale (0-3 pre-hardmode, 4-9 hardmode, 10-11 post-Moon Lord) and, except DarkOrange, its own rarities
	// only after Moon Lord. Treasure bags are Cyan whatever the boss, Expert/Master/Gray say nothing.
	internal static BookStage RarityStage(Item item)
	{
		if (ItemID.Sets.BossBag[item.type])
		{
			return BookStage.Unknown;
		}
		if (item.ModItem == null && PreRarityExceptions.Contains(ItemID.Search.GetName(item.type) ?? ""))
		{
			return BookStage.PreHardmode;
		}
		int rare = item.rare;
		if (rare >= ItemRarityID.Count)
		{
			ModRarity modRarity = RarityLoader.GetRarity(rare);
			return modRarity?.Mod.Name == "CalamityMod" && PostRarities.Contains(modRarity.Name) ? BookStage.PostHardmode : BookStage.Unknown;
		}
		return rare switch
		{
			ItemRarityID.Quest or (>= ItemRarityID.White and <= ItemRarityID.Orange) => BookStage.PreHardmode,
			>= ItemRarityID.LightRed and <= ItemRarityID.Cyan => BookStage.Hardmode,
			ItemRarityID.Red or ItemRarityID.Purple => BookStage.PostHardmode,
			_ => BookStage.Unknown
		};
	}

	// Floor added by a recipe/shop condition: a stage; Unknown when the condition limits it to special-seed
	// worlds or to another difficulty than the current world's; null when the condition is opaque (another
	// mod's custom gate) and the caller has to fall back to the output's rarity.
	internal static BookStage? ConditionFloor(string key, Func<bool> isMet)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}
		if (SeedConditions.Contains(key))
		{
			return BookStage.Unknown;
		}
		if (ModeConditions.Contains(key))
		{
			return isMet() ? BookStage.PreHardmode : BookStage.Unknown;
		}
		if (HardmodeConditions.Contains(key))
		{
			return BookStage.Hardmode;
		}
		if (PostConditions.Contains(key))
		{
			return BookStage.PostHardmode;
		}
		if (key.StartsWith("Conditions.", StringComparison.Ordinal))
		{
			return BookStage.PreHardmode;
		}
		if (key.StartsWith(CalamityConditionPrefix, StringComparison.Ordinal))
		{
			string suffix = key.Substring(CalamityConditionPrefix.Length);
			if (CalamityPostConditions.Contains(suffix))
			{
				return BookStage.PostHardmode;
			}
			if (CalamityHardmodeConditions.Contains(suffix))
			{
				return BookStage.Hardmode;
			}
			if (CalamityPreConditions.Contains(suffix))
			{
				return BookStage.PreHardmode;
			}
		}
		return null;
	}

	// Same for item drop rule conditions. Condition.ToDropCondition wraps a recipe-style Condition (Calamity
	// uses it for Zenith-world and Revengeance drops), which is read by its key.
	internal static BookStage? DropConditionFloor(IItemDropRuleCondition condition)
	{
		if (condition == null)
		{
			return BookStage.PreHardmode;
		}
		if (condition is SimpleItemDropRuleCondition simple)
		{
			// The description is the "Drops: {0}" template; the wrapped Condition's text is its first argument.
			LocalizedText inner = simple.Description?.BoundArgs?.FirstOrDefault() as LocalizedText;
			return ConditionFloor((inner ?? simple.Description)?.Key, simple.Predicate);
		}
		Type type = condition.GetType();
		if (type.Assembly != typeof(Main).Assembly)
		{
			return null;
		}
		if (SeedDropConditions.Contains(type.Name))
		{
			return BookStage.Unknown;
		}
		bool? mode = type.Name switch
		{
			"IsExpert" or "LegacyHack_IsBossAndExpert" => Main.expertMode,
			"NotExpert" or "LegacyHack_IsBossAndNotExpert" => !Main.expertMode,
			"IsMasterMode" => Main.masterMode,
			"NotMasterMode" => !Main.masterMode,
			_ => null
		};
		if (mode.HasValue)
		{
			return mode.Value ? BookStage.PreHardmode : BookStage.Unknown;
		}
		return HardmodeDropConditions.Contains(type.Name) ? BookStage.Hardmode : BookStage.PreHardmode;
	}

	private static HashSet<string> Set(string names)
	{
		return new HashSet<string>(names.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
	}

	private static Dictionary<string, BookStage> Stages(string pre, string hard, string post)
	{
		return new[] { (pre, BookStage.PreHardmode), (hard, BookStage.Hardmode), (post, BookStage.PostHardmode) }
			.SelectMany(t => t.Item1.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(name => (name, t.Item2)))
			.ToDictionary(t => t.name, t => t.Item2, StringComparer.Ordinal);
	}
}
