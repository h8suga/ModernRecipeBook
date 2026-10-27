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

	private static readonly HashSet<string> CalamityHardmodeNpcs = Set("AnthozoanCrab AquaticAberration AquaticScourgeBody AquaticScourgeBodyAlt AquaticScourgeHead AquaticScourgeTail Archmage Aries Astraglomerate Glomerling AstralachneaGround AstralachneaWall AstralProbe AstralSlime AstrumAureus AstrumDeusBody AstrumDeusHead AstrumDeusTail Atlas AureusSpawn AuroraSpirit BelchingCoral BlindedAngler Bohldohr Brimling BrimstoneElemental CalamitasClone ChaoticPuffer CloudElemental CragmawMire CrawlerCrystal Cryogen Cryon CryoSlime EarthElemental Eidolist FlakCrab FusionFeeder GiantSquid GreatSandShark GulperEelBody GulperEelBodyAlt GulperEelHead GulperEelTail Hadarian IceClasper InfernalCongealment IrradiatedSlime Mantis MantisShrimp Melter Nova OarfishBody OarfishHead OarfishTail Orthocera OverloadedSoldier PerennialSlime PestilentSlime PlaguebringerGoliath PlaguebringerMiniboss PlagueCharger PlagueChargerLarge Plagueshell RavagerBody RavagerClawLeft RavagerClawRight RavagerHead RavagerHead2 RavagerLegLeft RavagerLegRight RenegadeWarlock SeaSerpent1 SeaSerpent2 SeaSerpent3 SeaSerpent4 SeaSerpent5 ShockstormShuttle SightseerCollider SightseerSpitter StellarCulex SulphurousSkater Trilobite Twinkler Viruling");

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

	// Vanilla NPCs that come after a particular boss: the Dungeon's after Skeletron (until then NPC.SpawnNPC sends only
	// the Dungeon Guardian at a player inside), the post-Plantera Dungeon and the Temple (its key drops from Plantera); the
	// Solar Eclipse after any mechanical boss, its Reaper after all three and the rest of it after Plantera
	// (NPC.SpawnNPC); the Pumpkin and Frost Moons (their medallions need Ectoplasm); Martian Madness (the probe waits for
	// Golem); town NPCs by Main.UpdateTime_SpawnTownNPCs; the Lunatic Devotees and Cultist Archers once Golem and Skeletron
	// are down (CultistRitual.CheckRitual: hardMode, downedGolemBoss, downedBoss3). The Lunar Events and the Old One's Army
	// tiers go by name (NpcTerm).
	private static readonly Dictionary<string, (string Term, BookStage Stage)> VanillaNpcTerms = Terms(
		(BookStage.PreHardmode, "Dryad=EyeofCthulhu;EaterofWorldsHead;SkeletronHead DD2Bartender=EaterofWorldsHead WitchDoctor=QueenBee Clothier=SkeletronHead Mechanic=SkeletronHead AngryBones=SkeletronHead AngryBonesBig=SkeletronHead AngryBonesBigMuscle=SkeletronHead AngryBonesBigHelmet=SkeletronHead DarkCaster=SkeletronHead CursedSkull=SkeletronHead DungeonSlime=SkeletronHead"),
		(BookStage.Hardmode, "DungeonSpirit=Plantera RustyArmoredBonesAxe=Plantera RustyArmoredBonesFlail=Plantera RustyArmoredBonesSword=Plantera RustyArmoredBonesSwordNoArmor=Plantera BlueArmoredBones=Plantera BlueArmoredBonesMace=Plantera BlueArmoredBonesNoPants=Plantera BlueArmoredBonesSword=Plantera HellArmoredBones=Plantera HellArmoredBonesSpikeShield=Plantera HellArmoredBonesMace=Plantera HellArmoredBonesSword=Plantera RaggedCaster=Plantera RaggedCasterOpenCoat=Plantera Necromancer=Plantera NecromancerArmored=Plantera DiabolistRed=Plantera DiabolistWhite=Plantera SkeletonCommando=Plantera SkeletonSniper=Plantera TacticalSkeleton=Plantera Paladin=Plantera GiantCursedSkull=Plantera BoneLee=Plantera "
			+ "Lihzahrd=Plantera LihzahrdCrawler=Plantera FlyingSnake=Plantera "
			+ "Eyezor=mechs:any Frankenstein=mechs:any SwampThing=mechs:any Vampire=mechs:any VampireBat=mechs:any CreatureFromTheDeep=mechs:any Fritz=mechs:any ThePossessed=mechs:any Reaper=mechs:all Mothron=Plantera MothronEgg=Plantera MothronSpawn=Plantera Butcher=Plantera DeadlySphere=Plantera DrManFly=Plantera Nailhead=Plantera Psycho=Plantera "
			+ "Scarecrow1=Plantera Scarecrow2=Plantera Scarecrow3=Plantera Scarecrow4=Plantera Scarecrow5=Plantera Scarecrow6=Plantera Scarecrow7=Plantera Scarecrow8=Plantera Scarecrow9=Plantera Scarecrow10=Plantera Splinterling=Plantera Hellhound=Plantera Poltergeist=Plantera HeadlessHorseman=Plantera MourningWood=Plantera Pumpking=Plantera PumpkingBlade=Plantera "
			+ "ZombieElf=Plantera ZombieElfBeard=Plantera ZombieElfGirl=Plantera ElfArcher=Plantera GingerbreadMan=Plantera Nutcracker=Plantera NutcrackerSpinning=Plantera Yeti=Plantera ElfCopter=Plantera Krampus=Plantera Flocko=Plantera Everscream=Plantera SantaNK1=Plantera IceQueen=Plantera PresentMimic=Plantera "
			+ "MartianProbe=Golem BrainScrambler=Golem GrayGrunt=Golem RayGunner=Golem MartianOfficer=Golem MartianEngineer=Golem MartianTurret=Golem MartianDrone=Golem GigaZapper=Golem ScutlixRider=Golem Scutlix=Golem MartianWalker=Golem MartianSaucer=Golem MartianSaucerCannon=Golem MartianSaucerTurret=Golem MartianSaucerCore=Golem "
			+ "Steampunker=mechs:any Cyborg=Plantera CultistDevote=Golem CultistArcherBlue=Golem CultistArcherWhite=Golem"));

	// A vanilla boss's minions and loose parts, which only exist in its fight, as the boss's step: the Eye's servants
	// (NPC.cs:14964), the Brain's Creepers (:27227), the Wall's Hungry and Leech (:20456, :20783, :86256) and eyes,
	// Skeletron's hands (:16486), the Destroyer's Probes (:14583), Prime's arms (:22410-22426), Plantera's hooks and
	// tentacles (:26572, :26831), Golem's fists and free head (:14307-14309, :83836), Duke Fishron's Sharkrons and bubbles
	// (:43567), the Queen Slime's minions, the Lunatic Cultist's dragons, visions, clones and lights (:58482-58784) and
	// Moon Lord's True Eyes and leech clots.
	private static readonly Dictionary<string, string> VanillaMinions = Pairs("ServantofCthulhu=EyeofCthulhu Creeper=EaterofWorldsHead TheHungry=Hardmode TheHungryII=Hardmode LeechHead=Hardmode LeechBody=Hardmode LeechTail=Hardmode WallofFleshEye=Hardmode SkeletronHand=SkeletronHead Probe=TheDestroyer PrimeCannon=SkeletronPrime PrimeSaw=SkeletronPrime PrimeVice=SkeletronPrime PrimeLaser=SkeletronPrime PlanterasHook=Plantera PlanterasTentacle=Plantera GolemFistLeft=Golem GolemFistRight=Golem GolemHeadFree=Golem Sharkron=DukeFishron Sharkron2=DukeFishron DetonatingBubble=DukeFishron QueenSlimeMinionBlue=QueenSlimeBoss QueenSlimeMinionPink=QueenSlimeBoss QueenSlimeMinionPurple=QueenSlimeBoss CultistDragonHead=CultistBoss CultistDragonBody1=CultistBoss CultistDragonBody2=CultistBoss CultistDragonBody3=CultistBoss CultistDragonBody4=CultistBoss CultistDragonTail=CultistBoss AncientCultistSquidhead=CultistBoss CultistBossClone=CultistBoss AncientLight=CultistBoss AncientDoom=CultistBoss MoonLordFreeEye=PostHardmode MoonLordLeechBlob=PostHardmode");

	// A mod boss's minions: the mod's NPC types the boss's own class names through ModContent.NPCType<T>() (Signus's
	// Cosmic Lanterns and Mines, Supreme Calamitas's brothers and Sepulcher, Astrum Aureus's spawn), by the earliest boss
	// that names one. Read once (BookSteps' NPCs are fixed after loading).
	private static Dictionary<int, int> modMinions;

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
		step = MinionStep(type);
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

	// The step of the boss whose fight a minion belongs to (VanillaMinions, ModMinions); -1 for any other NPC. A mod NPC
	// that also spawns on its own (the spawn probe saw it: the Hive Mind's Dank Creepers roam the Corruption) or with the
	// Acid Rain is none.
	internal static int MinionStep(int type)
	{
		ModNPC modNpc = NPCLoader.GetNPC(type);
		if (modNpc == null)
		{
			return NPCID.Search.TryGetName(type, out string name) && VanillaMinions.TryGetValue(name, out string boss) ? BookSteps.Resolve(boss) : -1;
		}
		return BookSpawnGates.Of(type) == null && BookBestiary.AcidRainOf(type) == null && ModMinions().TryGetValue(type, out int step) ? step : -1;
	}

	private static Dictionary<int, int> ModMinions()
	{
		if (modMinions != null)
		{
			return modMinions;
		}
		modMinions = new Dictionary<int, int>();
		MethodInfo npcType = typeof(ModContent).GetMethods(BindingFlags.Public | BindingFlags.Static).First((MethodInfo m) => m.Name == nameof(ModContent.NPCType) && m.IsGenericMethodDefinition);
		Dictionary<Type, int> types = new Dictionary<Type, int>();
		foreach (ModNPC npc in ModContent.GetContent<ModNPC>())
		{
			types.TryAdd(npc.GetType(), npc.Type);
		}
		for (int k = 1; k < BookSteps.All.Count; k++)
		{
			foreach (int boss in BookSteps.All[k].Npcs)
			{
				if (NPCLoader.GetNPC(boss) is not ModNPC modBoss)
				{
					continue;
				}
				foreach (MethodInfo method in Methods(modBoss.GetType()))
				{
					foreach ((System.Reflection.Emit.OpCode _, MemberInfo member) in BookItemTraits.IlMembers(method, (System.Reflection.Emit.OpCode op) => op == System.Reflection.Emit.OpCodes.Call))
					{
						if (member is MethodInfo { IsGenericMethod: true } call && call.GetGenericMethodDefinition() == npcType && types.TryGetValue(call.GetGenericArguments()[0], out int minion) && BookSteps.Npc(minion) < 0 && BookSteps.MiniBoss(minion) < 0 && !ContentSamples.NpcsByNetId[minion].townNPC)
						{
							modMinions[minion] = Math.Min(modMinions.GetValueOrDefault(minion, k), k);
						}
					}
				}
			}
		}
		return modMinions;
		// A class's own methods and those of its nested types (lambdas, local closures).
		static IEnumerable<MethodInfo> Methods(Type type)
		{
			return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(Methods));
		}
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

	// Opaque drop conditions no progression of this world meets, by what does: the step one comes before (true with
	// nothing down, false from that step on: Calamity's "!NPC.downedBoss2" Ashen Stalactite), or the other world one
	// belongs to (OtherWorlds: Supreme Calamitas's Levi in Death mode).
	private static Dictionary<object, int> before = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);

	private static Dictionary<object, (string Key, string Arg)[]> otherWorld = new Dictionary<object, (string, string)[]>(ReferenceEqualityComparer.Instance);

	internal static void Clear()
	{
		calamityDropGates = null;
		probed = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
		before = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
		otherWorld = new Dictionary<object, (string, string)[]>(ReferenceEqualityComparer.Instance);
	}

	// The secret seeds by the Main flag each sets, as the seed is typed (UIWorldCreation.ProcessSeed; the drunk world's
	// is its date).
	private static readonly (string Field, string Seed)[] Seeds = { ("drunkWorld", "05162020"), ("getGoodWorld", "for the worthy"), ("tenthAnniversaryWorld", "celebrationmk10"), ("dontStarveWorld", "the constant"), ("notTheBeesWorld", "not the bees"), ("remixWorld", "don't dig up"), ("noTrapsWorld", "no traps"), ("zenithWorld", "get fixed boi") };

	// Vanilla's seed drop conditions, which have no text of their own: the seed and whether they need it (true) or its
	// absence (false); the remix seed's two split by hardmode (Conditions.RemixSeedEasymode/Hardmode).
	private static readonly Dictionary<string, (string Field, bool With, string Key)> VanillaSeedConditions = new Dictionary<string, (string, bool, string)>(StringComparer.Ordinal)
	{
		["RemixSeed"] = ("remixWorld", true, null), ["NotRemixSeed"] = ("remixWorld", false, null),
		["RemixSeedEasymode"] = ("remixWorld", true, "KillConditions.SeedBeforeHardmode"), ["RemixSeedHardmode"] = ("remixWorld", true, "KillConditions.SeedInHardmode"),
		["TenthAnniversaryIsUp"] = ("tenthAnniversaryWorld", true, null), ["TenthAnniversaryIsNotUp"] = ("tenthAnniversaryWorld", false, null),
		["DontStarveIsUp"] = ("dontStarveWorld", true, null), ["DontStarveIsNotUp"] = ("dontStarveWorld", false, null),
		["DrunkWorldIsUp"] = ("drunkWorld", true, null), ["DrunkWorldIsNotUp"] = ("drunkWorld", false, null),
		["ForTheWorthyIsUp"] = ("getGoodWorld", true, null), ["ForTheWorthyIsNotUp"] = ("getGoodWorld", false, null),
		["BeesSeed"] = ("notTheBeesWorld", true, null), ["NotBeesSeed"] = ("notTheBeesWorld", false, null),
		["NoTrapsSeed"] = ("noTrapsWorld", true, null), ["NotNoTrapsSeed"] = ("noTrapsWorld", false, null),
		["ZenithSeedIsUp"] = ("zenithWorld", true, null), ["ZenithSeedIsNotUp"] = ("zenithWorld", false, null)
	};

	// The label of a vanilla seed condition (key and the seed), null for any other.
	internal static (string Key, string Arg)? SeedLabel(IItemDropRuleCondition condition)
	{
		if (condition == null || condition.GetType().Assembly != typeof(Main).Assembly || !VanillaSeedConditions.TryGetValue(condition.GetType().Name, out var seed))
		{
			return null;
		}
		string name = Seeds.First(((string Field, string Seed) s) => s.Field == seed.Field).Seed;
		return (seed.Key ?? (seed.With ? "KillConditions.WithSeed" : "KillConditions.WithoutSeed"), name);
	}

	// The worlds an opaque drop condition (a mod's lambda reading Main.expertMode, a seed flag or Calamity's difficulty)
	// is asked in besides this one, one flag at a time and each put back: the other difficulties (Main.GameMode),
	// Calamity's Revengeance and Death (CalamityWorld.revenge and .death), every seed flipped, single player or
	// multiplayer (Calamity's StarterBag and AbyssalTreasure: Main.netMode == 1), the world's copper or tin (StarterBag:
	// WorldGen.SavedOreTiers.Copper == 166), its evil outside a drunk world (StuffedFish: !WorldGen.crimson; a drunk world
	// turns it over every dawn, EventGate) and a Hardcore character (DevourerofGodsBag's Cosmic Plushie:
	// info.player.difficulty == 2). Each with the label of what the condition then needs.
	private static List<(Action Apply, Action Restore, string Key, string Arg)> OtherWorlds()
	{
		List<(Action, Action, string, string)> worlds = new List<(Action, Action, string, string)>();
		int mode = Main.GameMode;
		foreach ((int other, string key) in new[] { ((int)GameModeID.Normal, "Common.Classic"), ((int)GameModeID.Expert, "Common.ExpertMaster"), ((int)GameModeID.Master, "Common.Master") })
		{
			if (other != mode)
			{
				worlds.Add((() => Main.GameMode = other, () => Main.GameMode = mode, key, null));
			}
		}
		Type calamity = ModLoader.TryGetMod("CalamityMod", out Mod mod) ? mod.Code.GetType("CalamityMod.World.CalamityWorld") : null;
		IEnumerable<(FieldInfo Field, string On, string Off, string Arg)> flags = new[] { ("revenge", "KillConditions.Revengeance", "KillConditions.WithoutRevengeance"), ("death", "KillConditions.Death", "KillConditions.WithoutDeath") }
			.Select(((string Name, string On, string Off) x) => (calamity?.GetField(x.Name, BindingFlags.Public | BindingFlags.Static), x.On, x.Off, (string)null))
			.Concat(Seeds.Select(((string Field, string Seed) x) => (typeof(Main).GetField(x.Field, BindingFlags.Public | BindingFlags.Static), "KillConditions.WithSeed", "KillConditions.WithoutSeed", x.Seed)));
		foreach ((FieldInfo field, string on, string off, string arg) in flags)
		{
			if (field?.FieldType == typeof(bool) && field.GetValue(null) is bool now)
			{
				worlds.Add((() => field.SetValue(null, !now), () => field.SetValue(null, now), now ? off : on, arg));
			}
		}
		int net = Main.netMode;
		worlds.Add((() => Main.netMode = net == NetmodeID.MultiplayerClient ? NetmodeID.SinglePlayer : NetmodeID.MultiplayerClient, () => Main.netMode = net, net == NetmodeID.MultiplayerClient ? "BestiaryLoot.InSinglePlayer" : "BestiaryLoot.InMultiplayer", null));
		int copper = WorldGen.SavedOreTiers.Copper;
		int otherCopper = copper == TileID.Tin ? TileID.Copper : TileID.Tin;
		worlds.Add((() => WorldGen.SavedOreTiers.Copper = otherCopper, () => WorldGen.SavedOreTiers.Copper = copper, otherCopper == TileID.Tin ? "BestiaryLoot.TinWorld" : "BestiaryLoot.CopperWorld", null));
		if (!Main.drunkWorld)
		{
			bool crimson = WorldGen.crimson;
			worlds.Add((() => WorldGen.crimson = !crimson, () => WorldGen.crimson = crimson, crimson ? "BestiaryLoot.CorruptionWorld" : "BestiaryLoot.CrimsonWorld", null));
		}
		Player player = Main.LocalPlayer;
		byte difficulty = player.difficulty;
		worlds.Add((() => player.difficulty = difficulty == PlayerDifficultyID.Hardcore ? PlayerDifficultyID.SoftCore : PlayerDifficultyID.Hardcore, () => player.difficulty = difficulty, difficulty == PlayerDifficultyID.Hardcore ? "BestiaryLoot.NotHardcoreCharacter" : "BestiaryLoot.HardcoreCharacter", null));
		return worlds;
	}

	// Probes every condition the tables cannot read: drop conditions with a sample of the NPC that drops them (bags
	// and crates get a blank one), recipe and shop conditions as they are. Returns how many got a step.
	internal static int ProbeOpaque(IEnumerable<(IItemDropRuleCondition Condition, NPC Npc)> drops, IEnumerable<Condition> conditions)
	{
		probed = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
		before = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
		otherWorld = new Dictionary<object, (string, string)[]>(ReferenceEqualityComparer.Instance);
		List<object> keys = new List<object>();
		List<Func<bool>> tests = new List<Func<bool>>();
		HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
		foreach ((IItemDropRuleCondition condition, NPC npc) in drops)
		{
			if (condition != null && seen.Add(condition) && ReadDropConditionStep(condition) == null)
			{
				// Built at each call: a flipped difficulty is read from the attempt too (IsExpertMode).
				NPC sample = npc ?? new NPC();
				keys.Add(condition);
				tests.Add(() => condition.CanDrop(new DropAttemptInfo { player = Main.LocalPlayer, npc = sample, IsExpertMode = Main.expertMode, IsMasterMode = Main.masterMode, IsInSimulation = true, rng = Main.rand }));
			}
		}
		int drop = keys.Count;
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
		// The drop conditions no step of this world opens: a step they come before (the negation's answer, when the
		// condition itself held with nothing down), else the first other world in which some step opens them or they
		// hold before one.
		List<int> left = Enumerable.Range(0, drop).Where((int i) => answers[i] == null).ToList();
		string[] negations = BookSpawnGates.ProbeSteps(left.Select((int i) => (Func<bool>)(() => !tests[i]())).ToList());
		for (int n = 0; n < left.Count; n++)
		{
			if (!string.IsNullOrEmpty(negations[n]) && BookSteps.Resolve(negations[n]) is int step && step > 0)
			{
				before[keys[left[n]]] = step;
			}
		}
		left = left.Where((int i) => !before.ContainsKey(keys[i])).ToList();
		// One flip at a time, then two together for what is left (Death mode and a Hardcore character).
		List<(Action Apply, Action Restore, string Key, string Arg)> worlds = OtherWorlds();
		IEnumerable<int[]> flips = Enumerable.Range(0, worlds.Count).Select((int a) => new[] { a })
			.Concat(Enumerable.Range(0, worlds.Count).SelectMany((int a) => Enumerable.Range(a + 1, worlds.Count - a - 1).Select((int b) => new[] { a, b })));
		foreach (int[] flip in flips)
		{
			if (left.Count == 0)
			{
				break;
			}
			string[] there, thereBefore;
			try
			{
				foreach (int w in flip)
				{
					worlds[w].Apply();
				}
				List<Func<bool>> asked = left.Select((int i) => tests[i]).ToList();
				there = BookSpawnGates.ProbeSteps(asked);
				thereBefore = BookSpawnGates.ProbeSteps(asked.Select((Func<bool> test) => (Func<bool>)(() => !test())).ToList());
			}
			finally
			{
				foreach (int w in Enumerable.Reverse(flip))
				{
					worlds[w].Restore();
				}
			}
			for (int n = 0; n < left.Count; n++)
			{
				if (there[n] != null || !string.IsNullOrEmpty(thereBefore[n]))
				{
					otherWorld[keys[left[n]]] = flip.Select((int w) => (worlds[w].Key, worlds[w].Arg)).ToArray();
				}
			}
			left = left.Where((int i) => !otherWorld.ContainsKey(keys[i])).ToList();
		}
		return placed;
	}

	// World events a drop lambda may wait for, asked with the flag on and off and put back: the label of the state it
	// needs, null when no event changes it. Calamity's Eidolist drops its Eidolon Tablet only while no Lunar Event is on
	// (Eidolist.ModifyNPCLoot: "!NPC.LunarApocalypseIsUp").
	// So is a drunk world's evil, which it turns over every dawn (Main.UpdateTime_StartDay): Calamity's StuffedFish gives
	// Corrupt Seeds (AddIf "!WorldGen.crimson") on its Corruption days.
	private static readonly (FieldInfo Flag, string On, string Off)[] Events = { (typeof(NPC).GetField(nameof(NPC.LunarApocalypseIsUp)), "KillConditions.DuringLunarEvents", "KillConditions.NotDuringLunarEvents") };

	private static readonly (FieldInfo Flag, string On, string Off) DrunkEvil = (typeof(WorldGen).GetField(nameof(WorldGen.crimson)), "BestiaryLoot.OnCrimsonDays", "BestiaryLoot.OnCorruptionDays");

	internal static string EventGate(Func<bool> holds)
	{
		foreach ((FieldInfo flag, string on, string off) in Main.drunkWorld ? Events.Append(DrunkEvil) : Events)
		{
			object now = flag.GetValue(null);
			bool withOn, withOff;
			try
			{
				flag.SetValue(null, true);
				withOn = holds();
				flag.SetValue(null, false);
				withOff = holds();
			}
			finally
			{
				flag.SetValue(null, now);
			}
			if (withOn != withOff)
			{
				return withOn ? on : off;
			}
		}
		return null;
	}

	// The step an opaque drop condition comes before (BookSpawnGates' probe of its negation), else 0.
	internal static int BeforeStep(IItemDropRuleCondition condition)
	{
		return condition != null && before.TryGetValue(condition, out int step) ? step : 0;
	}

	// The other world an opaque drop condition belongs to, as the label keys and arguments of what it needs (one flip, or
	// two together); null for this world's.
	internal static (string Key, string Arg)[] OtherWorldOf(IItemDropRuleCondition condition)
	{
		return condition != null && otherWorld.TryGetValue(condition, out var world) ? world : null;
	}

	// Whether only the world's progression decides a drop condition, so that asking it now says whether it holds in this
	// world now: a step it comes before (BeforeStep), one the tables or the probe found (a probed mod lambda, a wrapped
	// Condition the tables know, Calamity's ore gates), vanilla's boss flags, and the failed branch of any of them. Not a
	// gate whose answer also needs the kill, the player's place, a season or a moon's wave (keys, souls, Halloween, the
	// moons' wave odds: their Conditions.*.CanDrop ask !info.IsInSimulation or roll).
	internal static bool FollowsProgression(IItemDropRuleCondition condition)
	{
		if (condition == null || BeforeStep(condition) > 0 || CalamityDropGate(condition) != null)
		{
			return true;
		}
		if (condition is BookItemSources.NotCondition not)
		{
			return Probed(not) != null || FollowsProgression(not.Inner);
		}
		if (condition is SimpleItemDropRuleCondition simple)
		{
			string key = WrappedKey(simple);
			bool known = key != null && (VanillaConditions.ContainsKey(key) || (key.StartsWith(CalamityConditionPrefix, StringComparison.Ordinal) && CalamityConditions.TryGetValue(key.Substring(CalamityConditionPrefix.Length), out var calamity) && calamity.Term.Length > 0));
			return known || Probed(condition) != null;
		}
		if (condition.GetType().Assembly == typeof(Main).Assembly)
		{
			return condition.GetType().Name is "IsHardmode" or "DownedAllMechBosses" or "DownedPlantera" or "FirstTimeKillingPlantera" or "BeatAnyMechBoss";
		}
		return Probed(condition) != null;
	}

	// Calamity's ore gates as their mechanical-boss term ("mechs:any", "mechs:two", "mechs:all": DropHelper.MythrilCondition,
	// AdamantiteCondition, HallowedBarsCondition read NPC.downedMechBossAny or two or three of the flags), also through
	// the failed branch (negated); null for any other condition and with the Early Hardmode Progression Rework off.
	internal static string MechGate(IItemDropRuleCondition condition, out bool negated)
	{
		negated = condition is BookItemSources.NotCondition;
		string gate = CalamityDropGate(condition is BookItemSources.NotCondition not ? not.Inner : condition);
		return string.IsNullOrEmpty(gate) ? null : gate;
	}

	// A seed Condition.ToDropCondition wraps (Calamity's DropHelper.GFB: "Conditions.WorldZenith"; NotRemix:
	// "Conditions.WorldNotRemix"; Terraria.Condition keys them "Conditions.World[Not]<seed>") as the seed typed and
	// whether it needs it; null for any other key.
	internal static (string Seed, bool With)? WrappedSeed(string key)
	{
		if (key == null || !key.StartsWith("Conditions.World", StringComparison.Ordinal))
		{
			return null;
		}
		string name = key.Substring("Conditions.World".Length);
		bool with = name == "NotTheBees" || !name.StartsWith("Not", StringComparison.Ordinal);
		string field = (with ? name : name.Substring(3)) switch
		{
			"Drunk" => "drunkWorld",
			"Remix" => "remixWorld",
			"NotTheBees" => "notTheBeesWorld",
			"ForTheWorthy" => "getGoodWorld",
			"Anniversary" => "tenthAnniversaryWorld",
			"DontStarve" => "dontStarveWorld",
			"NoTraps" => "noTrapsWorld",
			"Zenith" => "zenithWorld",
			_ => null
		};
		return field == null ? null : (Seeds.First(((string Field, string Seed) s) => s.Field == field).Seed, with);
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
	internal static int? NpcFromNameKey(string key)
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
	// uses it for Zenith-world and Revengeance drops), which is read by its key. A condition that tests the dying NPC
	// (BookKillConditions: Astrum Deus's split worms) is no progression gate unless the probe found one in it too, and
	// neither is one that comes before a step (BeforeStep): both are there from the start.
	internal static int? DropConditionStep(IItemDropRuleCondition condition)
	{
		return ReadDropConditionStep(condition) ?? (BookKillConditions.TestsNpc(condition) || BeforeStep(condition) > 0 ? 0 : null);
	}

	private static int? ReadDropConditionStep(IItemDropRuleCondition condition)
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

	// Vanilla's Bestiary leaves out a drop any of whose conditions answers CanShowItemDropInUI false
	// (ItemDropBestiaryInfoElement.ShouldShowItem). The book lists on purpose what that hides only while it does not hold:
	// another world's rule (NotExpert in Expert) and loot that waits for a boss (ShowItemDropInUI.WhenConditionSatisfied).
	// So a drop is left out of the card, How to Get and the stages when one of its conditions answers false for good
	// (ShowItemDropInUI.Never; Calamity's "ui: false", a bool its CanShowItemDropInUI returns: Morshu's loot, Bohldohr's NO,
	// Supreme Calamitas's Coldheart Icicle) or is neither of those. A rule only another world has is listed anyway, as that
	// world's (Bohldohr's NO needs a Zenith world): it is no source here, and the progression marks it so (DropFloor).
	internal static bool HiddenFromUI(DropRateInfo rate)
	{
		return (rate.conditions?.Any(HiddenFromUI) ?? false) && !rate.conditions.Any(ModeBlocked);
	}

	private static bool HiddenFromUI(IItemDropRuleCondition condition)
	{
		try
		{
			if (condition is BookItemSources.NotCondition not)
			{
				return HiddenFromUI(not.Inner);
			}
			if (condition == null || condition.CanShowItemDropInUI())
			{
				return false;
			}
			if (condition is SimpleItemDropRuleCondition { ShowItemDropInUI: ShowItemDropInUI.Never })
			{
				return true;
			}
			// An answer made of the condition's own fields only (no call, no static flag) never changes.
			MethodInfo show = condition.GetType().GetMethod(nameof(IItemDropRuleCondition.CanShowItemDropInUI), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
			if (show != null && BookItemTraits.IlMembers(show, (System.Reflection.Emit.OpCode _) => true).All(((System.Reflection.Emit.OpCode Op, MemberInfo Member) x) => x.Member is FieldInfo { IsStatic: false }))
			{
				return true;
			}
			// Vanilla's own answers that change are all this world's difficulty, seed or evil (Conditions.NotExpert,
			// IsCrimson... ), which the book reads itself, even where it cannot fix them (the evil of a drunk world).
			return condition.GetType().Assembly != typeof(Main).Assembly && WorldGate(condition) == null && !(DropConditionStep(condition) > 0);
		}
		catch (Exception)
		{
			return false;
		}
	}

	// A difficulty gate this world fails, or an opaque condition only another world meets (OtherWorldOf).
	internal static bool ModeBlocked(IItemDropRuleCondition condition)
	{
		return ModeGate(condition) == false || OtherWorldOf(condition) != null;
	}

	// Drop conditions that read only the world's seed, which never changes, or its evil and difficulty (the Eye of
	// Cthulhu's ore and seeds, in Classic and in its bag; EitherEvil in a drunk world): judged by asking them.
	private static readonly HashSet<string> SeedGates = Set("DontStarveIsUp DontStarveIsNotUp DrunkWorldIsUp DrunkWorldIsNotUp ForTheWorthyIsUp ForTheWorthyIsNotUp TenthAnniversaryIsUp TenthAnniversaryIsNotUp ZenithSeedIsUp ZenithSeedIsNotUp BeesSeed NotBeesSeed NoTrapsSeed NotNoTrapsSeed RemixSeed NotRemixSeed IsCorruption IsCrimson IsCorruptionAndNotExpert IsCrimsonAndNotExpert");

	// For the loot lists: whether a condition fixed for this world (difficulty or seed) holds; null when it is not fixed.
	// The remix seed's hardmode/pre-hardmode drops fail outside a remix world and are a hardmode condition inside one, and
	// so does Mechdusa's kill outside a remix For the Worthy world (Conditions.MechdusaKill). An opaque condition only
	// another world meets (OtherWorldOf) fails here too. Progression keeps its own reading of seeds (DropConditionStep).
	internal static bool? WorldGate(IItemDropRuleCondition condition)
	{
		if (ModeGate(condition) is bool mode)
		{
			return mode;
		}
		if (OtherWorldOf(condition) != null)
		{
			return false;
		}
		// A seed or the world's evil wrapped by Condition.ToDropCondition (Calamity's DropHelper.Remix, NotRemix and GFB;
		// Condition.RemixWorld and its kin are keyed "Conditions.World...").
		if (condition is SimpleItemDropRuleCondition simple && WrappedKey(simple) is string key && key.StartsWith("Conditions.World", StringComparison.Ordinal))
		{
			return key is "Conditions.WorldCrimson" or "Conditions.WorldCorrupt" ? EitherEvil(simple.Predicate) : simple.Predicate();
		}
		if (condition == null || condition.GetType().Assembly != typeof(Main).Assembly)
		{
			return null;
		}
		string name = condition.GetType().Name;
		if (SeedGates.Contains(name))
		{
			Func<bool> holds = () => condition.CanDrop(new DropAttemptInfo { player = Main.LocalPlayer, IsInSimulation = true, rng = Main.rand });
			return name.Contains("Corruption", StringComparison.Ordinal) || name.Contains("Crimson", StringComparison.Ordinal) ? EitherEvil(holds) : holds();
		}
		return ((name is "RemixSeedEasymode" or "RemixSeedHardmode") && !Main.remixWorld) || (name == "MechdusaKill" && !(Main.remixWorld && Main.getGoodWorld)) ? false : null;
	}

	// A condition on the world's evil: fixed outside a drunk world. A drunk world has both evils and turns WorldGen.crimson
	// over every dawn (Main.UpdateTime_StartDay), so there it is a gate only when both evils agree (Classic loot in Expert).
	private static bool? EitherEvil(Func<bool> holds)
	{
		if (!Main.drunkWorld)
		{
			return holds();
		}
		bool now = WorldGen.crimson;
		bool crimson, corrupt;
		try
		{
			WorldGen.crimson = true;
			crimson = holds();
			WorldGen.crimson = false;
			corrupt = holds();
		}
		finally
		{
			WorldGen.crimson = now;
		}
		return crimson == corrupt ? crimson : null;
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
	internal static string WrappedKey(SimpleItemDropRuleCondition simple)
	{
		return ((simple.Description?.BoundArgs?.FirstOrDefault() as LocalizedText) ?? simple.Description)?.Key;
	}

	private static HashSet<string> Set(string names)
	{
		return new HashSet<string>(names.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
	}

	// "name=value name=value".
	private static Dictionary<string, string> Pairs(string pairs)
	{
		return pairs.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select((string p) => p.Split('=')).ToDictionary((string[] p) => p[0], (string[] p) => p[1], StringComparer.Ordinal);
	}

	private static Dictionary<string, BookStage> Stages(string pre, string hard, string post)
	{
		return new[] { (pre, BookStage.PreHardmode), (hard, BookStage.Hardmode), (post, BookStage.PostHardmode) }
			.SelectMany(t => t.Item1.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(name => (name, t.Item2)))
			.ToDictionary(t => t.name, t => t.Item2, StringComparer.Ordinal);
	}
}
