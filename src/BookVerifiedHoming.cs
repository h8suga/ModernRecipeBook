using System.Collections.Generic;
using Terraria;
using Terraria.ID;

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
	internal readonly record struct Entry(BookTrait Traits, string NoteEn, string NoteRu)
	{
		internal string Note => RecipeBookSystem.T(NoteRu, NoteEn);
	}

	private static Entry Homes(string en, string ru)
	{
		return new Entry(BookTrait.Homing, en, ru);
	}

	private static readonly Dictionary<int, Entry> Vanilla = new Dictionary<int, Entry>
	{
		[ItemID.MagicMissile] = Homes("steered by the cursor while held; let go, it seeks an enemy within 800 px", "пока кнопка зажата, летит за курсором; отпущенная ищет врага в 800 px"),
		[ItemID.Flamelash] = Homes("steered by the cursor while held; let go, it seeks an enemy within 800 px", "пока кнопка зажата, летит за курсором; отпущенный ищет врага в 800 px"),
		[ItemID.RainbowRod] = Homes("steered by the cursor while held; let go, it seeks an enemy within 800 px", "пока кнопка зажата, летит за курсором; отпущенная ищет врага в 800 px"),
		[ItemID.BeeGun] = Homes("the bees chase an enemy within 800 px soon after launch", "пчёлы вскоре после выстрела летят к врагу в 800 px"),
		[ItemID.WaspGun] = Homes("the wasps chase an enemy within 800 px soon after launch", "осы вскоре после выстрела летят к врагу в 800 px"),
		[ItemID.BatScepter] = Homes("the bats chase an enemy within 800 px soon after launch", "летучие мыши вскоре после выстрела летят к врагу в 800 px"),
		[ItemID.PossessedHatchet] = Homes("the hatchet flies at an enemy within 800 px", "топорик летит к врагу в 800 px"),
		[ItemID.BouncingShield] = Homes("the shield flies at an enemy within 800 px", "щит летит к врагу в 800 px"),
		[ItemID.ChlorophyteBullet] = Homes("turns toward an enemy within 300 px in sight", "поворачивает к врагу в 300 px в прямой видимости"),
		[ItemID.BookofSkulls] = Homes("the skull turns gently toward an enemy within 300 px", "череп плавно поворачивает к врагу в 300 px"),
		[ItemID.SpectreStaff] = Homes("the lost soul flies at an enemy within 400 px", "потерянная душа летит к врагу в 400 px"),
		[ItemID.SnowmanCannon] = Homes("the rockets turn toward an enemy within 600 px", "ракеты поворачивают к врагу в 600 px"),
		[ItemID.Flairon] = Homes("the bubbles it spews home in within 650 px", "выпущенные пузыри наводятся на врага в 650 px"),
		[ItemID.RazorbladeTyphoon] = Homes("each wheel locks onto an enemy within 500 px in sight", "каждый диск выбирает врага в 500 px в прямой видимости"),
		[ItemID.VortexBeater] = Homes("only the rocket every seventh shot homes; the bullets fly straight", "наводится только ракета каждого седьмого выстрела; пули летят прямо"),
		[ItemID.NebulaArcanum] = Homes("the orb seeks an enemy within 800 px once it slows down", "сфера ищет врага в 800 px, когда замедлится"),
		[ItemID.NebulaBlaze] = Homes("turns toward an enemy within 250 px (the strong shot 500 px)", "поворачивает к врагу в 250 px (сильный выстрел в 500 px)"),
		[ItemID.SpiritFlame] = Homes("the flame flies at an enemy within 500 px", "пламя летит к врагу в 500 px"),
		[ItemID.Celeb2] = Homes("only one of its seven rocket kinds homes", "наводится только один из семи видов ракет"),
		[ItemID.FairyQueenMagicItem] = Homes("the shots turn toward an enemy within 800 px for most of their flight", "снаряды большую часть полёта поворачивают к врагу в 800 px"),
		[ItemID.WeatherPain] = Homes("the shot flies at an enemy within 400 px in sight", "снаряд летит к врагу в 400 px в прямой видимости")
	};

	private static readonly Dictionary<string, Entry> Modded = new Dictionary<string, Entry>
	{
		["CalamityMod/FrostyFlare"] = new Entry(BookTrait.StealthHoming, "the stealth flare drops phantom flares that home in within 400 px", "скрытая ракета сбрасывает призрачные копии, они наводятся на врага в 400 px")
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
