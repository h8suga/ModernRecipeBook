using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace ModernRecipeBook.Catalogue.Traits;

// Where vanilla's projectile code overrides the defaults "pierces" and "through walls" read (penetrate, tileCollide).
// Vanilla runs one AI for every type, so these sets come from reading the decompiled Projectile.cs by hand; the lines
// are in docs/weapon-traits.md. A mod projectile that borrows a vanilla AI (ModProjectile.AIType) runs the same AI code,
// so the tileCollide set looks it up by that type; hit handling (Projectile.Damage) tests the real type and aiStyle.
internal static class BookVanillaShots
{
	// No flying shot: Vilethorn-like vines grow in place (aiStyle 4), the Ice Rod builds a block (22).
	private static readonly HashSet<int> NotShotAiStyles = new HashSet<int> { 4, 22 };

	// Not "keeps flying" after a hit: boomerangs turn back (3), hooks, chains and Flairon are pulled back (7, 13, 69),
	// explosives and Molotovs go off (16, 68), flares go harmless once stuck (33), the Piranha Gun latches (39), nails
	// stick (93), yoyos bounce off (99).
	private static readonly HashSet<int> StopAiStyles = new HashSet<int> { 3, 7, 13, 16, 33, 39, 68, 69, 93, 99 };

	// Types that stick, end, turn harmless or change penetrate on a hit or later: javelins that stick (598, 614, 636,
	// 971, 975), Hellfire Arrow (41, ends on a hit), Electrosphere missile and field (442, 443), Influx Waver (451),
	// Charged Blaster orb (459), Ichor Dart (479), Shadowflame Knife (497, bounces back), Meowmere (502), toxic clouds
	// (511-513), Ichor splash (524), Terrarian beam (604), Solar Eruption ray (607), Rainbow Crystal blast (644), Lunar
	// Flare (645), Sandnado (656), Spirit Flame (659, ends on a hit), Celebration Mk2 rockets (715, 717), Blood Thorn
	// (756), Smolstar (864), the 1.4.4 shots that go harmless partway (973, 985), and minion shots that toggle friendly
	// (307, 317, 356).
	private static readonly HashSet<int> StopTypes = new HashSet<int>
	{
		41, 307, 317, 356, 442, 443, 451, 459, 479, 497, 502, 511, 512, 513, 524, 598, 604, 607, 614, 636, 644, 645,
		656, 659, 715, 717, 756, 864, 971, 973, 975, 985
	};

	// Types whose tileCollide the AI switches in flight: falling stars that collide once below the cursor (9, 92, 503,
	// 723-726), Blizzard and Meteor Staff (337, 424-426), Lunar Flare (645), Phantasm arrows (631), Zapinator laser
	// (876), Pygmy spear (195), nails (514), toxic clouds (511-513), Molotov and Ale (399, 669), Influx Waver (451),
	// Piranha (190), Flairon (404), Flying Knife (491), sticking javelins, and the ones that are held or return anyway
	// (boomerangs, chains, flails, yoyos and counterweights).
	private static readonly HashSet<int> TileCollideTypes = new HashSet<int>
	{
		6, 9, 19, 23, 25, 26, 33, 35, 52, 63, 92, 106, 113, 154, 182, 190, 195, 247, 262, 271, 272, 273, 301, 320, 333,
		337, 383, 399, 404, 424, 425, 426, 451, 481, 491, 503, 511, 512, 513, 514, 534, 541, 542, 543, 544, 545, 546,
		547, 548, 549, 550, 551, 552, 553, 554, 555, 556, 557, 558, 559, 560, 561, 562, 563, 564, 598, 603, 614, 631,
		636, 645, 669, 723, 724, 725, 726, 757, 866, 867, 876, 902, 947, 948, 971, 975, 999, 1000
	};

	private static int AiType(Projectile p)
	{
		int ai = p.ModProjectile?.AIType ?? 0;
		if (ai <= 0)
		{
			return p.type;
		}
		return ai;
	}

	internal static bool NotAShot(Projectile p)
	{
		return NotShotAiStyles.Contains(p.aiStyle);
	}

	// Sticking and stopping happen in Projectile.Damage, which tests the real type: Calamity's Crystal Piercer borrows the
	// Bone Javelin's AI but does not stick.
	internal static bool StopsOnHit(Projectile p)
	{
		if (!StopAiStyles.Contains(p.aiStyle) && !ProjectileID.Sets.Explosive[p.type])
		{
			return StopTypes.Contains(p.type);
		}
		return true;
	}

	// Explosives also turn tileCollide off when they go off.
	internal static bool TileCollideChanges(Projectile p)
	{
		if (!TileCollideTypes.Contains(AiType(p)) && p.aiStyle != 16)
		{
			return ProjectileID.Sets.Explosive[p.type];
		}
		return true;
	}
}
