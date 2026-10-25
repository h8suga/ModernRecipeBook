using System.Collections.Generic;
using Terraria;
using Terraria.ID;

using static ModernRecipeBook.BookText;

namespace ModernRecipeBook;

// Weapons whose shots steer toward enemies in flight, each checked by hand in the decompiled code (tModLoader 2026.8 /
// Terraria 1.4.4 with Calamity 2.2.4 loaded; code locations in docs/weapon-traits.md). Listed because their description
// does not say it in every language (the Russian vanilla text says "attacks" for Bee Gun or Chlorophyte Bullet). Left
// out on purpose, with the reason in the doc: Chlorophyte Arrow and Nano Bullet (re-aim only off a wall), Crystal Dart,
// Vampire Knives, Hellwing Bow, Bee's Knees and Scourge of the Corruptor (their homing things appear only on a hit or
// when the shot dies), Piranha Gun (re-targets only after its enemy dies), Flower Pow, Terrarian, Magnet Sphere and
// Medusa Head (aimed once), Flying Knife (cursor only). The note is what the card shows.
internal static class BookVerifiedHoming
{
	// NoteKey: the localization key of the note.
	internal readonly record struct Entry(BookTrait Traits, string NoteKey)
	{
		internal string Note => NoteKey == null ? "" : L(NoteKey);
	}

	private static Entry Homes(string noteKey)
	{
		return new Entry(BookTrait.Homing, noteKey);
	}

	private static readonly Dictionary<int, Entry> Vanilla = new Dictionary<int, Entry>
	{
		[ItemID.MagicMissile] = Homes("VerifiedHoming.MagicMissile"),
		[ItemID.Flamelash] = Homes("VerifiedHoming.Flamelash"),
		[ItemID.RainbowRod] = Homes("VerifiedHoming.RainbowRod"),
		[ItemID.BeeGun] = Homes("VerifiedHoming.BeeGun"),
		[ItemID.WaspGun] = Homes("VerifiedHoming.WaspGun"),
		[ItemID.BatScepter] = Homes("VerifiedHoming.BatScepter"),
		[ItemID.PossessedHatchet] = Homes("VerifiedHoming.PossessedHatchet"),
		[ItemID.BouncingShield] = Homes("VerifiedHoming.BouncingShield"),
		[ItemID.ChlorophyteBullet] = Homes("VerifiedHoming.ChlorophyteBullet"),
		[ItemID.BookofSkulls] = Homes("VerifiedHoming.BookofSkulls"),
		[ItemID.SpectreStaff] = Homes("VerifiedHoming.SpectreStaff"),
		[ItemID.SnowmanCannon] = Homes("VerifiedHoming.SnowmanCannon"),
		[ItemID.Flairon] = Homes("VerifiedHoming.Flairon"),
		[ItemID.RazorbladeTyphoon] = Homes("VerifiedHoming.RazorbladeTyphoon"),
		[ItemID.VortexBeater] = Homes("VerifiedHoming.VortexBeater"),
		[ItemID.NebulaArcanum] = Homes("VerifiedHoming.NebulaArcanum"),
		[ItemID.NebulaBlaze] = Homes("VerifiedHoming.NebulaBlaze"),
		[ItemID.SpiritFlame] = Homes("VerifiedHoming.SpiritFlame"),
		[ItemID.Celeb2] = Homes("VerifiedHoming.Celeb2"),
		[ItemID.FairyQueenMagicItem] = Homes("VerifiedHoming.FairyQueenMagicItem"),
		[ItemID.WeatherPain] = Homes("VerifiedHoming.WeatherPain")
	};

	private static readonly Dictionary<string, Entry> Modded = new Dictionary<string, Entry>
	{
		["CalamityMod/FrostyFlare"] = new Entry(BookTrait.StealthHoming, "VerifiedHoming.FrostyFlare")
	};

	internal static Entry Get(Item item)
	{
		if (item.ModItem == null)
		{
			return Vanilla.TryGetValue(item.type, out var entry) ? entry : default(Entry);
		}
		return Modded.TryGetValue(item.ModItem.FullName, out var modded) ? modded : default(Entry);
	}
}
