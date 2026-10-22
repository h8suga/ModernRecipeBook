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
	// Murasama lies in the Hell lab from world generation but only works after the Devourer of Gods.
	private static readonly HashSet<string> PostItems = Set("Murasama ExodiumCluster GalacticaSingularity UnholyEssence EffulgentFeather DivineGeode UelibloomOre UelibloomBar Bloodstone BloodstoneCore Phantoplasm RuinousSoul ReaperTooth ArmoredShell TwistingNether DarkPlasma CosmiliteBar EndothermicEnergy NightmareFuel DarksunFragment AscendantSpiritEssence YharonSoulFragment AuricOre AuricBar AshesofAnnihilation ExoPrism ShadowspecBar MiracleMatter DraedonsForge CosmicAnvil ProfanedCrucible CelestialOnion");

	// Draedon lab materials: lab turrets and power cell factories are reachable before hardmode, but the
	// items carry Calamity's DarkOrange rarity, which is used for every arsenal tier.
	private static readonly HashSet<string> PreItems = Set("DubiousPlating MysteriousCircuitry DraedonPowerCell");

	// A recorded music box is made by wearing a blank one while its track plays, so it is as late as the
	// track: hardmode biomes and bosses (vanilla Boss 4 is Golem/Lunatic Cultist; the other Boss tracks also
	// play for pre-hardmode bosses), post-Moon Lord bosses and the credits. Matched on the name after
	// "MusicBox" (vanilla) or before it (Calamity Music); everything else plays in pre-hardmode places.
	private static readonly string[] HardmodeTracks = "TheHallow UndergroundHallow OWHallow OWUndergroundHallow Boss4 Plantera OWPlantera Temple Eclipse PumpkinMoon FrostMoon LunarBoss OWMoonLord Martians Pirates Towers OWTowers QueenSlime EmpressOfLight DukeFishron Cryogen AquaticScourge BrimstoneElemental CalamitasClone Leviathan Anahita AstrumAureus PlaguebringerGoliath Ravager AstrumDeus".Split(' ');

	private static readonly string[] PostTracks = "Credits ProfanedGuardians Dragonfolly Providence CeaselessVoid StormWeaver Signus Polterghast OldDuke DevourerofGods Yharon ExoMechs DraedonExoSelect DraedonTalk CalamitasPhase CalamitasDefeat PrimordialWyrm BossRush AcidRainTier3".Split(' ');

	internal static BookStage TrackStage(Item box)
	{
		string track = (box.ModItem?.Name ?? ItemID.Search.GetName(box.type) ?? "").Replace("MusicBox", "");
		if (PostTracks.Any((string t) => track.StartsWith(t, StringComparison.Ordinal)))
		{
			return BookStage.PostHardmode;
		}
		return HardmodeTracks.Any((string t) => track.StartsWith(t, StringComparison.Ordinal)) ? BookStage.Hardmode : BookStage.PreHardmode;
	}

	// The Astral Infection lands with the hardmode meteor (WorldgenManagementSystem.ModifyHardmodeTasks), so
	// its blocks, fish, fruit and crates are hardmode despite White/Green rarity; same for a few other biome
	// catches and fruit the wiki marks hardmode-only.
	private static readonly HashSet<string> HardmodeItems = Set("AstralStone AstralGrassWall AstralDirt AstralClay AstralSand AstralSandstone HardenedAstralSand AstralIce AstralSnow AstralGrassSeeds AstralMonolith NovaeSlag CelestialRemains MonolithAmalgam MonolithCrate AldebaranAlewife ArcturusAstroidean Cometfruit CharredLasher FishofFlight Barberry Lotus Mangosteen");

	// Calamity's own rarities that only appear after Moon Lord (DarkOrange is excluded, see PreItems).
	private static readonly HashSet<string> PostRarities = Set("Turquoise PureGreen CosmicPurple BurnishedAuric ExoticRainbow HotPink CalamityRed");

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

	// Every other NPC (enemies, critters, town NPCs) that only appears in hardmode or after Moon Lord, from the
	// wikis' "Hardmode-only NPCs" and "Post-Moon Lord only NPCs" categories (terraria.wiki.gg, calamitymod.wiki.gg,
	// October 2026) matched to Calamity 2.2.4 internal names. Vanilla NPCs not listed appear before hardmode.
	private static readonly HashSet<string> VanillaHardmodeNpcs = Set("BloodJelly Frankenstein FungoFish Ghost AncientCultistSquidhead AncientDoom AncientLight AnglerFish AngryNimbus AngryTrapper Arapaima ArmoredSkeleton ArmoredViking BigMimicCorruption BigMimicCrimson BigMimicHallow BigMimicJungle BlackRecluse BlackRecluseWall BloodEelBody BloodEelHead BloodEelTail BloodFeeder BloodMummy BloodNautilus BloodSquid BoneLee BrainScrambler Butcher ChaosElemental ChatteringTeethBomb Clinger Clown Corruptor CorruptSlime CreatureFromTheDeep Crimslime CrimsonAxe CultistArcherBlue CultistArcherWhite CultistBoss CultistBossClone CultistDevote CultistDragonBody1 CultistDragonBody2 CultistDragonBody3 CultistDragonBody4 CultistDragonHead CultistDragonTail CursedHammer Cyborg DarkMummy DD2Betsy DD2DrakinT2 DD2DrakinT3 DD2KoboldFlyerT2 DD2KoboldFlyerT3 DD2KoboldWalkerT2 DD2KoboldWalkerT3 DD2LightningBugT3 DD2OgreT2 DD2OgreT3 DD2WitherBeastT2 DD2WitherBeastT3 DeadlySphere DemonTaxCollector Derpling DesertBeast DesertDjinn DesertGhoul DesertGhoulCorruption DesertGhoulCrimson DesertGhoulHallow DesertLamiaDark DesertLamiaLight DesertScorpionWalk DesertScorpionWall DetonatingBubble DiabolistRed DiabolistWhite DiggerBody DiggerHead DiggerTail DrManFly DukeFishron DuneSplicerBody DuneSplicerHead DuneSplicerTail DungeonSpirit ElfArcher ElfCopter EmpressButterfly EnchantedSword Everscream Eyezor FloatyGross Flocko Fritz Gastropod GiantBat GiantCursedSkull GiantFlyingFox GiantFungiBulb GiantTortoise GigaZapper GingerbreadMan GoblinShark GoblinSummoner Golem GrayGrunt GreenJellyfish HallowBoss HeadlessHorseman Hellhound Herpling HoppinJack IceElemental IceGolem IceMimic IceQueen IceTortoise IchorSticker IcyMerman IlluminantBat IlluminantSlime JungleCreeper JungleCreeperWall Krampus Lavabat LightMummy LightningBug LunarTowerNebula LunarTowerSolar LunarTowerStardust LunarTowerVortex MartianDrone MartianEngineer MartianOfficer MartianProbe MartianSaucer MartianSaucerCannon MartianSaucerCore MartianSaucerTurret MartianTurret MartianWalker Medusa Mimic MisterStabby MoonLordFreeEye MoonLordHead MoonLordLeechBlob MossHornet Moth Mothron MothronSpawn MourningWood Mummy Nailhead NebulaBeast NebulaBrain NebulaHeadcrab NebulaSoldier Necromancer NecromancerArmored Nutcracker NutcrackerSpinning Paladin Parrot PigronCorruption PigronCrimson PigronHallow Pirate PirateCaptain PirateCorsair PirateCrossbower PirateDeadeye PirateDeckhand PirateGhost PirateShip PirateShipCannon Pixie Plantera Poltergeist PossessedArmor PresentMimic Princess Probe Psycho Pumpking PumpkingBlade QueenSlimeBoss QueenSlimeMinionPurple RaggedCaster RaggedCasterOpenCoat RainbowSlime RayGunner Reaper RedDevil Retinazer RockGolem RuneWizard SandElemental SandShark SandsharkCorrupt SandsharkCrimson SandsharkHallow SantaClaus SantaNK1 Scarecrow1 Scarecrow10 Scarecrow2 Scarecrow3 Scarecrow4 Scarecrow5 Scarecrow6 Scarecrow7 Scarecrow8 Scarecrow9 Scutlix ScutlixRider SeekerBody SeekerHead SeekerTail ShadowFlameApparition Sharkron Sharkron2 SkeletonArcher SkeletonSniper SkeletronPrime Slimer SnowBalla SnowmanGangsta SolarCorite SolarCrawltipedeBody SolarCrawltipedeHead SolarCrawltipedeTail SolarDrakomire SolarDrakomireRider SolarFlare SolarGoop SolarSolenian SolarSpearman SolarSroller Spazmatism Splinterling StardustCellBig StardustJellyfishBig StardustSoldier StardustSpiderBig StardustWormHead Steampunker SwampThing TaxCollector TheDestroyer TheDestroyerBody TheDestroyerTail ThePossessed ToxicSludge Truffle TruffleWorm TruffleWormDigger Unicorn Vampire VampireBat VortexHornet VortexHornetQueen VortexLarva VortexRifleman VortexSoldier WanderingEye Werewolf Wizard Wolf Wraith WyvernBody WyvernBody2 WyvernBody3 WyvernHead WyvernLegs WyvernTail Yeti ZombieElf ZombieElfBeard ZombieElfGirl");

	private static readonly HashSet<string> CalamityHardmodeNpcs = Set("AnthozoanCrab AquaticAberration AquaticScourgeBody AquaticScourgeBodyAlt AquaticScourgeHead AquaticScourgeTail Archmage Aries Astraglomerate AstralachneaGround AstralachneaWall AstralProbe AstralSlime AstrumAureus AstrumDeusBody AstrumDeusHead AstrumDeusTail Atlas AureusSpawn AuroraSpirit BelchingCoral BlindedAngler Bohldohr Brimling BrimstoneElemental CalamitasClone ChaoticPuffer CloudElemental CragmawMire CrawlerCrystal Cryogen Cryon CryoSlime EarthElemental Eidolist FlakCrab FusionFeeder GiantSquid GreatSandShark GulperEelBody GulperEelBodyAlt GulperEelHead GulperEelTail Hadarian IceClasper InfernalCongealment IrradiatedSlime Mantis MantisShrimp Melter Nova OarfishBody OarfishHead OarfishTail Orthocera OverloadedSoldier PerennialSlime PestilentSlime PlaguebringerGoliath PlaguebringerMiniboss PlagueCharger PlagueChargerLarge Plagueshell RavagerBody RavagerClawLeft RavagerClawRight RavagerHead RavagerHead2 RavagerLegLeft RavagerLegRight RenegadeWarlock SeaSerpent1 SeaSerpent2 SeaSerpent3 SeaSerpent4 SeaSerpent5 ShockstormShuttle SightseerCollider SightseerSpitter StellarCulex SulphurousSkater Trilobite Twinkler Viruling");

	private static readonly HashSet<string> CalamityPostNpcs = Set("Bloatfish BloodwormFleeing BloodwormNormal BloomSlime BobbitWormHead BobbitWormSegment BrimstoneWitch CeaselessVoid ColossalSquid DevourerofGodsBody DevourerofGodsHead DevourerofGodsTail DraconicSwarmer Dragonfolly EidolonWyrmBody EidolonWyrmBodyAlt EidolonWyrmHead EidolonWyrmTail GammaSlime ImpiousImmolator Mauler NuclearTerror OldDuke PhantomSpirit PhantomSpiritL PhantomSpiritM PhantomSpiritS Polterghast PolterPhantom PrimordialWyrmBody PrimordialWyrmBodyAlt PrimordialWyrmHead PrimordialWyrmTail ProfanedEnergyBody ProfanedEnergyLantern Providence ReaperShark ScornEater SepulcherArm SepulcherBody SepulcherBodyEnergyBall SepulcherHead SepulcherTail Signus SoulSeeker SoulSeekerSupreme StormWeaverBody StormWeaverHead StormWeaverTail SupremeCalamitas THELORDE WildBumblebirb Yharon");

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

	// Curated world items (BookItemSources) are there from the start.
	internal static BookStage VanillaNaturalStage(string name)
	{
		if (name == null || !BookItemSources.VanillaWorldItems.Contains(name))
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
		if (HardmodeItems.Contains(item.ModItem.Name))
		{
			return BookStage.Hardmode;
		}
		return PreItems.Contains(item.ModItem.Name) ? BookStage.PreHardmode : BookStage.Unknown;
	}

	internal static BookStage WorldgenStage(Item item)
	{
		return BookItemSources.IsCalamityWorldgen(item) ? BookStage.PreHardmode : BookStage.Unknown;
	}

	// Stage an NPC appears at. Bosses first (their loot follows the boss's own progression), then the wiki
	// tables. Unlisted NPCs are Unknown and the caller infers them from their drops.
	internal static BookStage NpcStage(NPC npc)
	{
		return NpcStage(npc.type);
	}

	internal static BookStage NpcStage(int type)
	{
		ModNPC modNpc = NPCLoader.GetNPC(type);
		BookStage stage;
		if (modNpc == null)
		{
			string name = NPCID.Search.TryGetName(type, out var n) ? n : "";
			if (VanillaNpcs.TryGetValue(name, out stage))
			{
				return stage;
			}
			// The wiki category misses some (Armored Bones, Tactical Skeleton, Lihzahrd): those are inferred.
			return VanillaHardmodeNpcs.Contains(name) ? BookStage.Hardmode : BookStage.Unknown;
		}
		if (modNpc.Mod.Name != "CalamityMod")
		{
			return BookStage.Unknown;
		}
		if (CalamityNpcs.TryGetValue(modNpc.Name, out stage))
		{
			return stage;
		}
		return CalamityPostNpcs.Contains(modNpc.Name) ? BookStage.PostHardmode : (CalamityHardmodeNpcs.Contains(modNpc.Name) ? BookStage.Hardmode : BookStage.Unknown);
	}

	// Shops open when their town NPC moves in; unknown modded town NPCs are assumed to come early.
	internal static BookStage TownNpcStage(int type)
	{
		BookStage stage = NpcStage(type);
		return stage == BookStage.Unknown ? BookStage.PreHardmode : stage;
	}

	// Last resort when no recipe or known drop explains an item: its rarity tier. Calamity uses the vanilla
	// scale (0-3 pre-hardmode, 4-9 hardmode, 10-11 post-Moon Lord) and, except DarkOrange, its own rarities
	// only after Moon Lord. Treasure bags are Cyan whatever the boss, Expert/Master say nothing; Gray is
	// junk (fishing trash, used-up items) available from the start.
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
			ItemRarityID.Gray or ItemRarityID.Quest or (>= ItemRarityID.White and <= ItemRarityID.Orange) => BookStage.PreHardmode,
			>= ItemRarityID.LightRed and <= ItemRarityID.Cyan => BookStage.Hardmode,
			ItemRarityID.Red or ItemRarityID.Purple => BookStage.PostHardmode,
			_ => BookStage.Unknown
		};
	}

	// Conditions that carry their subject as a format argument: "another town NPC lives here" follows that
	// NPC's arrival, and the Zoologist's bestiary gates need hardmode creatures from 70% on (wiki-calibrated).
	internal static BookStage? ConditionFloor(Condition condition)
	{
		LocalizedText text = condition?.Description;
		object arg = text?.BoundArgs?.FirstOrDefault();
		switch (text?.Key)
		{
			case "Conditions.NpcIsPresent":
				return arg is LocalizedText npcName && NpcFromNameKey(npcName.Key) is int npc ? TownNpcStage(npc) : null;
			case "Conditions.BestiaryPercentage":
				return arg is int percent ? (percent >= 70 ? BookStage.Hardmode : BookStage.PreHardmode) : null;
			case "Conditions.BestiaryFull":
				return BookStage.Hardmode;
			case "Conditions.PlayerCarriesItem":
				return BookStage.PreHardmode;
		}
		return ConditionFloor(text?.Key, () => condition.IsMet());
	}

	// "The player carries item X": the caller turns X into an ingredient, so the entry waits for X's stage.
	internal static int? CarriedItem(Condition condition)
	{
		LocalizedText text = condition?.Description;
		if (text?.Key != "Conditions.PlayerCarriesItem" || text.BoundArgs?.FirstOrDefault() is not LocalizedText name)
		{
			return null;
		}
		if (name.Key.StartsWith("ItemName.", StringComparison.Ordinal))
		{
			return ItemID.Search.TryGetId(name.Key.Substring(9), out int id) ? id : null;
		}
		string[] parts = name.Key.Split('.');
		return parts.Length >= 5 && parts[0] == "Mods" && parts[^1] == "DisplayName" && ModContent.TryFind(parts[1], parts[^2], out ModItem modItem) ? modItem.Type : null;
	}

	// "NPCName.Pirate" or "Mods.CalamityMod.NPCs.<category...>.SeaKing.DisplayName" back to the NPC type.
	private static int? NpcFromNameKey(string key)
	{
		if (key == null)
		{
			return null;
		}
		if (key.StartsWith("NPCName.", StringComparison.Ordinal))
		{
			return NPCID.Search.TryGetId(key.Substring(8), out int id) ? id : null;
		}
		string[] parts = key.Split('.');
		return parts.Length >= 5 && parts[0] == "Mods" && parts[^1] == "DisplayName" && ModContent.TryFind(parts[1], parts[^2], out ModNPC modNpc) ? modNpc.Type : null;
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
			return ConditionFloor(WrappedKey(simple), simple.Predicate);
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
		bool? mode = VanillaModeGate(type.Name);
		if (mode.HasValue)
		{
			return mode.Value ? BookStage.PreHardmode : BookStage.Unknown;
		}
		return HardmodeDropConditions.Contains(type.Name) ? BookStage.Hardmode : BookStage.PreHardmode;
	}

	// True when a difficulty gate fails in the current world: the source exists, just not in this mode.
	internal static bool ModeBlocked(string key, Func<bool> isMet)
	{
		return key != null && ModeConditions.Contains(key) && !isMet();
	}

	internal static bool ModeBlocked(IItemDropRuleCondition condition)
	{
		if (condition is SimpleItemDropRuleCondition simple)
		{
			return ModeBlocked(WrappedKey(simple), simple.Predicate);
		}
		return condition != null && condition.GetType().Assembly == typeof(Main).Assembly && VanillaModeGate(condition.GetType().Name) == false;
	}

	private static bool? VanillaModeGate(string name)
	{
		return name switch
		{
			"IsExpert" or "LegacyHack_IsBossAndExpert" => Main.expertMode,
			"NotExpert" or "LegacyHack_IsBossAndNotExpert" => !Main.expertMode,
			"IsMasterMode" => Main.masterMode,
			"NotMasterMode" => !Main.masterMode,
			_ => null
		};
	}

	// The description is the "Drops: {0}" template; the wrapped Condition's text is its first argument.
	private static string WrappedKey(SimpleItemDropRuleCondition simple)
	{
		return ((simple.Description?.BoundArgs?.FirstOrDefault() as LocalizedText) ?? simple.Description)?.Key;
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
