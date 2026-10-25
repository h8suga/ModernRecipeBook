using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook.Progression;

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

	// Recipe and shop conditions (Condition.Description keys) as "key=term" (BookSteps.Resolve) by the stage they
	// belong to, which also stands in when the term names nothing loaded. Other "Conditions.*" keys are season,
	// biome, moon phase or other gates that add nothing. The moons' and Martians' bosses wait for their events
	// (the medallions need Ectoplasm, the Martian Probe Golem); the Old One's Army tiers open after the Eater of
	// Worlds (the Tavernkeep), a mechanical boss and Golem.
	private static readonly Dictionary<string, (string Term, BookStage Stage)> VanillaConditions = Terms(
		(BookStage.PreHardmode, "Conditions.DownedKingSlime=KingSlime Conditions.DownedEyeOfCthulhu=EyeofCthulhu Conditions.DownedBoss2=EaterofWorldsHead Conditions.DownedEaterOfWorlds=EaterofWorldsHead Conditions.DownedBrainOfCthulhu=EaterofWorldsHead Conditions.DownedQueenBee=QueenBee Conditions.DownedSkeletron=SkeletronHead Conditions.DownedDeerclops=Deerclops Conditions.DownedEarlygameBoss=EyeofCthulhu;EaterofWorldsHead;SkeletronHead;QueenBee;Hardmode Conditions.DownedOldOnesArmyAny=EaterofWorldsHead Conditions.DownedOldOnesArmyT1=EaterofWorldsHead"),
		(BookStage.Hardmode, "Conditions.InHardmode=Hardmode Conditions.SolarEclipse=mechs:any Conditions.DownedClown=Hardmode Conditions.DownedMechBossAny=mechs:any Conditions.DownedMechBossAll=mechs:all Conditions.DownedTwins=Retinazer Conditions.DownedDestroyer=TheDestroyer Conditions.DownedSkeletronPrime=SkeletronPrime Conditions.DownedPlantera=Plantera Conditions.DownedGolem=Golem Conditions.DownedDukeFishron=DukeFishron Conditions.DownedEmpressOfLight=HallowBoss Conditions.DownedQueenSlime=QueenSlimeBoss Conditions.DownedLunaticCultist=CultistBoss Conditions.DownedPirates=Hardmode Conditions.DownedMartians=Golem Conditions.DownedFrostLegion=Hardmode Conditions.DownedMourningWood=Plantera Conditions.DownedPumpking=Plantera Conditions.DownedEverscream=Plantera Conditions.DownedSantaNK1=Plantera Conditions.DownedIceQueen=Plantera Conditions.DownedSolarPillar=CultistBoss Conditions.DownedVortexPillar=CultistBoss Conditions.DownedNebulaPillar=CultistBoss Conditions.DownedStardustPillar=CultistBoss Conditions.DownedOldOnesArmyT2=mechs:any Conditions.DownedOldOnesArmyT3=Golem"),
		(BookStage.PostHardmode, "Conditions.DownedMoonLord=PostHardmode"));

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

	// CalamityConditions 2.2.4 by the flag each reads: the Acid Rain tiers follow the Eye of Cthulhu and Aquatic
	// Scourge, the Giant Clam spawns from the start (its hardmode loot waits for hardmode), the Great Sand Shark after
	// Plantera, "Hive Mind or Perforators" and "Calamitas Clone or Plantera" take the earlier. The Primordial Wyrm is
	// no step; its loot is put after Supreme Calamitas, where players meet it.
	private static readonly Dictionary<string, (string Term, BookStage Stage)> CalamityConditions = Terms(
		(BookStage.PreHardmode, "HasRogueArmor= HasWings= InCrag= InSulph= InSunken= Drops.DownedT1AR=EyeofCthulhu Drops.DownedDS=CalamityMod/DesertScourgeHead Drops.DownedCrabulon=CalamityMod/Crabulon Drops.DownedHM=CalamityMod/HiveMind Drops.DownedPerfs=CalamityMod/PerforatorHive Drops.DownedHMOrPerfs=CalamityMod/HiveMind;CalamityMod/PerforatorHive Drops.DownedSG=CalamityMod/SlimeGodCore Drops.DownedClam="),
		(BookStage.Hardmode, "InAstral=Hardmode Drops.DownedT2AR=CalamityMod/AquaticScourgeHead Drops.DownedCryo=CalamityMod/Cryogen Drops.DownedAS=CalamityMod/AquaticScourgeHead Drops.DownedBrim=CalamityMod/BrimstoneElemental Drops.DownedCal=CalamityMod/CalamitasClone Drops.DownedCalPlant=CalamityMod/CalamitasClone;Plantera Drops.DownedLevi=CalamityMod/Leviathan Drops.DownedAureus=CalamityMod/AstrumAureus Drops.DownedPBG=CalamityMod/PlaguebringerGoliath Drops.DownedRav=CalamityMod/RavagerBody Drops.DownedAD=CalamityMod/AstrumDeusHead Drops.DownedClamHM=Hardmode Drops.DownedGSS=Plantera Drops.DownedBetsy=DD2Betsy Drops.DownedDreadnautilus=Hardmode"),
		(BookStage.PostHardmode, "Drops.DownedGuard=CalamityMod/ProfanedGuardianCommander Drops.DownedBirb=CalamityMod/Dragonfolly Drops.DownedProv=CalamityMod/Providence Drops.DownedSig=CalamityMod/Signus Drops.DownedSW=CalamityMod/StormWeaverHead Drops.DownedCV=CalamityMod/CeaselessVoid Drops.DownedPolter=CalamityMod/Polterghast Drops.DownedOD=CalamityMod/OldDuke Drops.DownedDoG=CalamityMod/DevourerofGodsHead Drops.DownedYharon=CalamityMod/Yharon Drops.DownedExos=CalamityMod/Draedon Drops.DownedSCal=CalamityMod/SupremeCalamitas Drops.DownedAEW=CalamityMod/SupremeCalamitas"));

	// Vanilla item drop rule conditions (Terraria.GameContent.ItemDropRules.Conditions) that gate a drop to
	// hardmode, with the boss where they name one. Every other vanilla condition (difficulty, world evil, seeds,
	// statues, seasons) adds nothing.
	private static readonly Dictionary<string, (string Term, BookStage Stage)> HardmodeDropConditions = Terms((BookStage.Hardmode, "IsHardmode=Hardmode DownedAllMechBosses=mechs:all DownedPlantera=Plantera FirstTimeKillingPlantera=Plantera MechanicalBossesDummyCondition=Hardmode BeatAnyMechBoss=mechs:any MechdusaKill=Hardmode PirateMap=Hardmode SoulOfLight=Hardmode SoulOfNight=Hardmode LivingFlames=Hardmode HallowKeyCondition=Hardmode JungleKeyCondition=Hardmode CorruptKeyCondition=Hardmode CrimsonKeyCondition=Hardmode FrozenKeyCondition=Hardmode DesertKeyCondition=Hardmode YoyosAmarok=Hardmode YoyosYelets=Hardmode YoyosKraken=Hardmode YoyosHelFire=Hardmode IsPumpkinMoon=Plantera FrostMoonDropGatingChance=Plantera PumpkinMoonDropGatingChance=Plantera FrostMoonDropGateForTrophies=Plantera PumpkinMoonDropGateForTrophies=Plantera FromCertainWaveAndAbove=Plantera RemixSeedHardmode=Hardmode EmpressOfLightIsGenuinelyEnraged=HallowBoss"));

	// Vanilla NPCs that come after a particular boss: the post-Plantera Dungeon and the Temple (its key drops from
	// Plantera); the Solar Eclipse after any mechanical boss, its Reaper after all three and the rest of it after
	// Plantera (NPC.SpawnNPC); the Pumpkin and Frost Moons (their medallions need Ectoplasm); Martian Madness (the
	// probe waits for Golem); town NPCs by Main.UpdateTime_SpawnTownNPCs. The Lunar Events and the Old One's Army
	// tiers go by name (NpcTerm).
	private static readonly Dictionary<string, (string Term, BookStage Stage)> VanillaNpcTerms = Terms(
		(BookStage.PreHardmode, "Dryad=EyeofCthulhu;EaterofWorldsHead;SkeletronHead DD2Bartender=EaterofWorldsHead WitchDoctor=QueenBee Clothier=SkeletronHead Mechanic=SkeletronHead"),
		(BookStage.Hardmode, "DungeonSpirit=Plantera RustyArmoredBonesAxe=Plantera RustyArmoredBonesFlail=Plantera RustyArmoredBonesSword=Plantera RustyArmoredBonesSwordNoArmor=Plantera BlueArmoredBones=Plantera BlueArmoredBonesMace=Plantera BlueArmoredBonesNoPants=Plantera BlueArmoredBonesSword=Plantera HellArmoredBones=Plantera HellArmoredBonesSpikeShield=Plantera HellArmoredBonesMace=Plantera HellArmoredBonesSword=Plantera RaggedCaster=Plantera RaggedCasterOpenCoat=Plantera Necromancer=Plantera NecromancerArmored=Plantera DiabolistRed=Plantera DiabolistWhite=Plantera SkeletonCommando=Plantera SkeletonSniper=Plantera TacticalSkeleton=Plantera Paladin=Plantera GiantCursedSkull=Plantera BoneLee=Plantera "
			+ "Lihzahrd=Plantera LihzahrdCrawler=Plantera FlyingSnake=Plantera "
			+ "Eyezor=mechs:any Frankenstein=mechs:any SwampThing=mechs:any Vampire=mechs:any VampireBat=mechs:any CreatureFromTheDeep=mechs:any Fritz=mechs:any ThePossessed=mechs:any Reaper=mechs:all Mothron=Plantera MothronEgg=Plantera MothronSpawn=Plantera Butcher=Plantera DeadlySphere=Plantera DrManFly=Plantera Nailhead=Plantera Psycho=Plantera "
			+ "Scarecrow1=Plantera Scarecrow2=Plantera Scarecrow3=Plantera Scarecrow4=Plantera Scarecrow5=Plantera Scarecrow6=Plantera Scarecrow7=Plantera Scarecrow8=Plantera Scarecrow9=Plantera Scarecrow10=Plantera Splinterling=Plantera Hellhound=Plantera Poltergeist=Plantera HeadlessHorseman=Plantera MourningWood=Plantera Pumpking=Plantera PumpkingBlade=Plantera "
			+ "ZombieElf=Plantera ZombieElfBeard=Plantera ZombieElfGirl=Plantera ElfArcher=Plantera GingerbreadMan=Plantera Nutcracker=Plantera NutcrackerSpinning=Plantera Yeti=Plantera ElfCopter=Plantera Krampus=Plantera Flocko=Plantera Everscream=Plantera SantaNK1=Plantera IceQueen=Plantera PresentMimic=Plantera "
			+ "MartianProbe=Golem BrainScrambler=Golem GrayGrunt=Golem RayGunner=Golem MartianOfficer=Golem MartianEngineer=Golem MartianTurret=Golem MartianDrone=Golem GigaZapper=Golem ScutlixRider=Golem Scutlix=Golem MartianWalker=Golem MartianSaucer=Golem MartianSaucerCannon=Golem MartianSaucerTurret=Golem MartianSaucerCore=Golem "
			+ "Steampunker=mechs:any Cyborg=Plantera"));

	// Calamity's town NPCs (CanTownNPCSpawn): the Archmage after Cryogen, the Sea King after the Desert Scourge and
	// the Giant Clam, the Brimstone Witch after Supreme Calamitas. The Bohldohr lives in the Jungle Temple
	// (SpawnCondition.JungleTemple), which opens with Plantera's key like the Lihzahrds'.
	private static readonly Dictionary<string, (string Term, BookStage Stage)> CalamityNpcTerms = Terms(
		(BookStage.PreHardmode, "SeaKing=CalamityMod/DesertScourgeHead"),
		(BookStage.Hardmode, "Archmage=CalamityMod/Cryogen Bohldohr=Plantera"),
		(BookStage.PostHardmode, "BrimstoneWitch=CalamityMod/SupremeCalamitas"));

	// The Acid Rain's tiers (BookBestiary.AcidRainSpawn.Tier).
	private static readonly string[] AcidRainTerms = { "EyeofCthulhu", "CalamityMod/AquaticScourgeHead", "CalamityMod/Polterghast" };

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

	// The step an NPC appears at (-1 when the tables do not know it): a boss's own step; a mini-boss's; the curated
	// vanilla, Calamity and Acid Rain tables; else the first step of its curated stage. Spawn gates come on top
	// (BookProgression).
	internal static int NpcStep(int type)
	{
		int step = BookSteps.Npc(type);
		if (step >= 0)
		{
			return step;
		}
		step = BookSteps.MiniBoss(type);
		if (step >= 0)
		{
			return step;
		}
		BookStage stage = NpcStage(type);
		ModNPC modNpc = NPCLoader.GetNPC(type);
		string term = NpcTerm(modNpc, type, out BookStage termStage);
		if (term != null)
		{
			return Step(term, stage == BookStage.Unknown ? termStage : stage);
		}
		if (modNpc?.Mod.Name == "CalamityMod" && BookBestiary.AcidRainOf(type) is BookBestiary.AcidRainSpawn rain && rain.Tier >= 0 && rain.Tier < AcidRainTerms.Length)
		{
			return Step(AcidRainTerms[rain.Tier], stage == BookStage.Unknown ? BookStage.PreHardmode : stage);
		}
		return BookSteps.Of(stage);
	}

	private static string NpcTerm(ModNPC modNpc, int type, out BookStage stage)
	{
		stage = BookStage.Hardmode;
		if (modNpc != null)
		{
			if (modNpc.Mod.Name == "CalamityMod" && CalamityNpcTerms.TryGetValue(modNpc.Name, out var calamity))
			{
				stage = calamity.Stage;
				return calamity.Term;
			}
			return null;
		}
		string name = NPCID.Search.TryGetName(type, out string n) ? n : "";
		if (VanillaNpcTerms.TryGetValue(name, out var vanilla))
		{
			stage = vanilla.Stage;
			return vanilla.Term;
		}
		// The Lunar Events open with the Lunatic Cultist; the Old One's Army tiers after the Eater of Worlds (the
		// Tavernkeep), a mechanical boss and Golem.
		if (name.StartsWith("LunarTower", StringComparison.Ordinal) || name.StartsWith("Solar", StringComparison.Ordinal) || name.StartsWith("Vortex", StringComparison.Ordinal) || name.StartsWith("Nebula", StringComparison.Ordinal) || name.StartsWith("Stardust", StringComparison.Ordinal))
		{
			return "CultistBoss";
		}
		if (name.StartsWith("DD2", StringComparison.Ordinal))
		{
			stage = name.EndsWith("T1", StringComparison.Ordinal) ? BookStage.PreHardmode : BookStage.Hardmode;
			return name.EndsWith("T1", StringComparison.Ordinal) ? "EaterofWorldsHead" : (name.EndsWith("T2", StringComparison.Ordinal) ? "mechs:any" : (name.EndsWith("T3", StringComparison.Ordinal) ? "Golem" : null));
		}
		return null;
	}

	// Shops open when their town NPC moves in; unknown modded town NPCs are assumed to come early.
	internal static int TownNpcStep(int type)
	{
		return Math.Max(0, NpcStep(type));
	}

	// A term's step, never earlier than its stage, and the stage's first step when the term names nothing loaded.
	private static int Step(string term, BookStage stage)
	{
		int step = BookSteps.Resolve(term);
		return Math.Max(step, BookSteps.Of(stage));
	}

	private static Dictionary<string, (string Term, BookStage Stage)> Terms(params (BookStage Stage, string Pairs)[] groups)
	{
		Dictionary<string, (string, BookStage)> result = new Dictionary<string, (string, BookStage)>(StringComparer.Ordinal);
		foreach ((BookStage stage, string pairs) in groups)
		{
			foreach (string pair in pairs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				int eq = pair.IndexOf('=');
				result[pair.Substring(0, eq)] = (pair.Substring(eq + 1), stage);
			}
		}
		return result;
	}

	// Calamity's own drop conditions for hardmode ores in crates and the like (DropHelper.MythrilCondition, ...):
	// with its Early Hardmode Progression Rework (on by default) Mythril and Orichalcum wait for a mechanical boss,
	// Adamantite and Titanium for two, Hallowed bars for all three. Read by reference once per load.
	private static Dictionary<IItemDropRuleCondition, string> calamityDropGates;

	private static string CalamityDropGate(IItemDropRuleCondition condition)
	{
		if (calamityDropGates == null)
		{
			calamityDropGates = new Dictionary<IItemDropRuleCondition, string>(ReferenceEqualityComparer.Instance);
			Type helper = ModLoader.TryGetMod("CalamityMod", out Mod calamity) ? calamity.Code.GetType("CalamityMod.DropHelper") : null;
			bool rework = CalamityRework();
			foreach ((string field, string term) in new[] { ("MythrilCondition", "mechs:any"), ("AdamantiteCondition", "mechs:two"), ("HallowedBarsCondition", "mechs:all") })
			{
				if (helper?.GetField(field, BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is IItemDropRuleCondition gate)
				{
					calamityDropGates[gate] = rework ? term : "";
				}
			}
		}
		return calamityDropGates.GetValueOrDefault(condition);
	}

	// CalamityServerConfig.EarlyHardmodeProgressionRework (a server config, synced to clients).
	internal static bool CalamityRework()
	{
		try
		{
			Type config = ModLoader.TryGetMod("CalamityMod", out Mod calamity) ? calamity.Code.GetType("CalamityMod.CalamityServerConfig") : null;
			object instance = config?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) ?? config?.GetField("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
			return instance != null && config.GetProperty("EarlyHardmodeProgressionRework")?.GetValue(instance) is true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	// Mods' opaque conditions, asked once per rebuild with the world's flags opened step by step
	// (BookSpawnGates.ProbeSteps): their step, or -2 for a condition that waits for something else than progression.
	private static Dictionary<object, int> probed = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);

	internal static void Clear()
	{
		calamityDropGates = null;
		probed = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
	}

	// Probes every condition the tables cannot read: drop conditions with a sample of the NPC that drops them (bags
	// and crates get a blank one), recipe and shop conditions as they are. Returns how many got a step.
	internal static int ProbeOpaque(IEnumerable<(IItemDropRuleCondition Condition, NPC Npc)> drops, IEnumerable<Condition> conditions)
	{
		probed = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
		List<object> keys = new List<object>();
		List<Func<bool>> tests = new List<Func<bool>>();
		HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
		foreach ((IItemDropRuleCondition condition, NPC npc) in drops)
		{
			if (condition != null && seen.Add(condition) && DropConditionStep(condition) == null)
			{
				DropAttemptInfo info = new DropAttemptInfo
				{
					player = Main.LocalPlayer,
					npc = npc ?? new NPC(),
					IsExpertMode = Main.expertMode,
					IsMasterMode = Main.masterMode,
					IsInSimulation = true,
					rng = Main.rand
				};
				keys.Add(condition);
				tests.Add(() => condition.CanDrop(info));
			}
		}
		foreach (Condition condition in conditions)
		{
			if (condition != null && seen.Add(condition) && ConditionStep(condition) == null)
			{
				keys.Add(condition);
				tests.Add(() => condition.IsMet());
			}
		}
		string[] answers = BookSpawnGates.ProbeSteps(tests);
		int placed = 0;
		for (int i = 0; i < keys.Count; i++)
		{
			int step = answers[i] == null ? -2 : BookSteps.Resolve(answers[i]);
			probed[keys[i]] = step < 0 ? -2 : step;
			placed += step >= 0 ? 1 : 0;
		}
		return placed;
	}

	private static int? Probed(object condition)
	{
		return probed.TryGetValue(condition, out int step) && step >= 0 ? step : null;
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
	// Steps as in ConditionStep(key, isMet).
	internal static int? ConditionStep(Condition condition)
	{
		LocalizedText text = condition?.Description;
		object arg = text?.BoundArgs?.FirstOrDefault();
		switch (text?.Key)
		{
			case "Conditions.NpcIsPresent":
				return arg is LocalizedText npcName && NpcFromNameKey(npcName.Key) is int npc ? TownNpcStep(npc) : null;
			case "Conditions.BestiaryPercentage":
				return arg is int percent ? (percent >= 70 ? BookSteps.HardmodeStep : 0) : null;
			case "Conditions.BestiaryFull":
				return BookSteps.HardmodeStep;
			case "Conditions.PlayerCarriesItem":
				return 0;
		}
		return ConditionStep(text?.Key, () => condition.IsMet()) ?? (condition == null ? null : Probed(condition));
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

	// Floor added by a recipe/shop condition, as a step: -1 when the condition limits it to special-seed worlds or
	// to another difficulty than the current world's; null when the condition is opaque (another mod's custom gate)
	// and the caller has to fall back to the output's rarity.
	internal static int? ConditionStep(string key, Func<bool> isMet)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}
		if (SeedConditions.Contains(key))
		{
			return -1;
		}
		if (ModeConditions.Contains(key))
		{
			return isMet() ? 0 : -1;
		}
		if (VanillaConditions.TryGetValue(key, out var vanilla))
		{
			return Step(vanilla.Term, vanilla.Stage);
		}
		if (key.StartsWith("Conditions.", StringComparison.Ordinal))
		{
			return 0;
		}
		if (key.StartsWith(CalamityConditionPrefix, StringComparison.Ordinal) && CalamityConditions.TryGetValue(key.Substring(CalamityConditionPrefix.Length), out var calamity))
		{
			return Step(calamity.Term, calamity.Stage);
		}
		return null;
	}

	// Same for item drop rule conditions. Condition.ToDropCondition wraps a recipe-style Condition (Calamity
	// uses it for Zenith-world and Revengeance drops), which is read by its key.
	internal static int? DropConditionStep(IItemDropRuleCondition condition)
	{
		if (condition == null)
		{
			return 0;
		}
		if (condition is SimpleItemDropRuleCondition simple)
		{
			return ConditionStep(WrappedKey(simple), simple.Predicate) ?? Probed(condition);
		}
		if (CalamityDropGate(condition) is string gate)
		{
			return Step(gate, BookStage.Hardmode);
		}
		// "Not expert" and the like as a failed branch: a difficulty gate, judged against this world.
		if (condition is BookItemSources.NotCondition not && NegatedModeGate(not) is bool passes)
		{
			return passes ? 0 : -1;
		}
		Type type = condition.GetType();
		if (type.Assembly != typeof(Main).Assembly)
		{
			return Probed(condition);
		}
		if (SeedDropConditions.Contains(type.Name))
		{
			return -1;
		}
		bool? mode = VanillaModeGate(type.Name);
		if (mode.HasValue)
		{
			return mode.Value ? 0 : -1;
		}
		return HardmodeDropConditions.TryGetValue(type.Name, out var hard) ? Step(hard.Term, hard.Stage) : 0;
	}

	// True when a difficulty gate fails in the current world: the source exists, just not in this mode.
	internal static bool ModeBlocked(string key, Func<bool> isMet)
	{
		return key != null && ModeConditions.Contains(key) && !isMet();
	}

	internal static bool ModeBlocked(IItemDropRuleCondition condition)
	{
		return ModeGate(condition) == false;
	}

	// Drop conditions that read only the world's seed, which never changes: judged by asking them.
	private static readonly HashSet<string> SeedGates = Set("DontStarveIsUp DontStarveIsNotUp DrunkWorldIsUp DrunkWorldIsNotUp ForTheWorthyIsUp ForTheWorthyIsNotUp TenthAnniversaryIsUp TenthAnniversaryIsNotUp ZenithSeedIsUp ZenithSeedIsNotUp BeesSeed NotBeesSeed NoTrapsSeed NotNoTrapsSeed RemixSeed NotRemixSeed");

	// For the loot lists: whether a condition fixed for this world (difficulty or seed) holds; null when it is not fixed.
	// The remix seed's hardmode/pre-hardmode drops fail outside a remix world and are a hardmode condition inside one.
	// Progression keeps its own reading of seeds (DropConditionStep).
	internal static bool? WorldGate(IItemDropRuleCondition condition)
	{
		if (ModeGate(condition) is bool mode)
		{
			return mode;
		}
		if (condition == null || condition.GetType().Assembly != typeof(Main).Assembly)
		{
			return null;
		}
		string name = condition.GetType().Name;
		if (SeedGates.Contains(name))
		{
			return condition.CanDrop(new DropAttemptInfo { player = Main.LocalPlayer, IsInSimulation = true, rng = Main.rand });
		}
		return (name is "RemixSeedEasymode" or "RemixSeedHardmode") && !Main.remixWorld ? false : null;
	}

	// Whether a drop condition that is a difficulty gate holds in the current world; null when it is no difficulty gate.
	internal static bool? ModeGate(IItemDropRuleCondition condition)
	{
		if (condition is SimpleItemDropRuleCondition simple)
		{
			string key = WrappedKey(simple);
			return key != null && ModeConditions.Contains(key) ? simple.Predicate() : null;
		}
		if (condition is BookItemSources.NotCondition not)
		{
			return NegatedModeGate(not);
		}
		return condition != null && condition.GetType().Assembly == typeof(Main).Assembly ? VanillaModeGate(condition.GetType().Name) : null;
	}

	// Whether a failed-condition branch of a difficulty gate drops in this world; null when the gate is no difficulty.
	private static bool? NegatedModeGate(BookItemSources.NotCondition not)
	{
		if (not.Inner is SimpleItemDropRuleCondition simple)
		{
			string key = WrappedKey(simple);
			return key != null && ModeConditions.Contains(key) ? !simple.Predicate() : null;
		}
		return not.Inner.GetType().Assembly == typeof(Main).Assembly ? !VanillaModeGate(not.Inner.GetType().Name) : null;
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
