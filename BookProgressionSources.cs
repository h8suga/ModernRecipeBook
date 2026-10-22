using System;
using System.Collections.Generic;

namespace ModernRecipeBook;

internal static class BookProgressionSources
{
	private static readonly HashSet<string> PostItems = new HashSet<string>("ExodiumCluster GalacticaSingularity UnholyEssence EffulgentFeather DivineGeode UelibloomOre UelibloomBar Bloodstone BloodstoneCore Phantoplasm RuinousSoul ReaperTooth ArmoredShell TwistingNether DarkPlasma CosmiliteBar EndothermicEnergy NightmareFuel DarksunFragment AscendantSpiritEssence YharonSoulFragment AuricOre AuricBar AshesofAnnihilation ExoPrism ShadowspecBar MiracleMatter DraedonsForge CosmicAnvil ProfanedCrucible CelestialOnion".Split(' '), StringComparer.Ordinal);

	private static readonly HashSet<string> PostNpcs = new HashSet<string>("ProfanedGuardianCommander ProfanedGuardianDefender ProfanedGuardianHealer Bumblebirb Bumblefuck Dragonfolly Providence CeaselessVoid StormWeaverHead StormWeaverBody StormWeaverTail Signus Polterghast OldDuke DevourerofGodsHead DevourerofGodsBody DevourerofGodsTail Yharon SupremeCalamitas AresBody Artemis Apollo ThanatosHead ThanatosBody1 ThanatosBody2 ThanatosTail".Split(' '), StringComparer.Ordinal);

	private static readonly HashSet<string> PreWorldItems = new HashSet<string>("Wood BorealWood RichMahogany Ebonwood Shadewood PalmWood AshWood Bamboo StoneBlock DirtBlock ClayBlock SandBlock EbonsandBlock CrimsandBlock MudBlock SnowBlock IceBlock SiltBlock SlushBlock DesertFossil Cobweb Gel FallenStar Acorn CopperOre TinOre IronOre LeadOre SilverOre TungstenOre GoldOre PlatinumOre DemoniteOre CrimtaneOre Meteorite Hellstone Obsidian SturdyFossil Amethyst Topaz Sapphire Emerald Ruby Diamond Amber Mushroom GlowingMushroom Daybloom Moonglow Blinkroot Deathweed Waterleaf Fireblossom Shiverthorn GrassSeeds JungleGrassSeeds MushroomGrassSeeds CorruptSeeds CrimsonSeeds AshGrassSeeds VileMushroom ViciousMushroom Bone RottenChunk Vertebrae JungleSpores Stinger Vine Lens BlackLens AntlionMandible SharkFin Feather".Split(' '), StringComparer.Ordinal);

	internal static BookStage VanillaNaturalStage(string name)
	{
		if (name == null || !PreWorldItems.Contains(name))
		{
			return BookStage.Unknown;
		}
		return BookStage.PreHardmode;
	}

	internal static BookStage ItemStage(string mod, string name)
	{
		if (!(mod == "CalamityMod") || !PostItems.Contains(name))
		{
			return BookStage.Unknown;
		}
		return BookStage.PostHardmode;
	}

	internal static BookStage NpcStage(string mod, string name)
	{
		if (!(mod == "CalamityMod") || !PostNpcs.Contains(name))
		{
			return BookStage.Unknown;
		}
		return BookStage.PostHardmode;
	}

	private static bool AnyMarker(this string[] markers, string key)
	{
		foreach (string marker in markers)
		{
			if (key.Contains(marker, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	internal static BookStage ConditionStage(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return BookStage.Unknown;
		}
		if (new string[6] { "NotDowned", "NotHardmode", "NotInHardmode", "NotMoonLordDowned", "PreMoonLord", "BeforeMoonLord" }.AnyMarker(key))
		{
			return BookStage.Unknown;
		}
		if (key.StartsWith("Mods.CalamityMod.", StringComparison.Ordinal) && new string[12]
		{
			"PostMoonLord", "PostProvidence", "DownedProvidence", "DownedPolterghast", "PostPolterghast", "DownedDoG", "DownedDevourerofGods", "PostDoG", "DownedYharon", "PostYharon",
			"DownedExoMechs", "DownedSupremeCalamitas"
		}.AnyMarker(key))
		{
			return BookStage.PostHardmode;
		}
		if (key.Contains("DownedMoonLord", StringComparison.OrdinalIgnoreCase) || key.Contains("MoonLordDowned", StringComparison.OrdinalIgnoreCase))
		{
			return BookStage.PostHardmode;
		}
		if ((key.Contains("Hardmode", StringComparison.OrdinalIgnoreCase) && !key.Contains("PreHardmode", StringComparison.OrdinalIgnoreCase)) || key.Contains("DownedMech", StringComparison.OrdinalIgnoreCase) || key.Contains("DownedPlantera", StringComparison.OrdinalIgnoreCase) || key.Contains("DownedGolem", StringComparison.OrdinalIgnoreCase) || key.Contains("DownedCultist", StringComparison.OrdinalIgnoreCase))
		{
			return BookStage.Hardmode;
		}
		return BookStage.Unknown;
	}
}
