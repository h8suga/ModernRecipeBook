using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ModernRecipeBook.Common.Configs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.Common.Players;

// After a death, both off by default (BookConfig):
// - the minions the player died with come back, the same ones: each use that summoned them (its item and prefix,
//   BookMinionSource) is made again with a copy of that item, through the game's own shot (Player.ItemCheck_Shoot: every
//   mod's ModifyShootStats/Shoot, SpawnMinionOnCursor's originalDamage) and the item's buff as a use gives it
//   (Player.ItemCheck_Inner: AddBuff(item.buffType, item.buffTime)). The item does not have to be carried, and no mana is
//   taken: they are the minions the death took;
// - the Bewitching Table and War Table buffs come back with the time they had left. Death clears every buff that is not
//   Main.persistentBuff (Player.UpdateDead), and the tables give them only on a click (Player.TileInteractionsUse:
//   AddBuff(150, 108000) at tile 354, AddBuff(348, 108000) at tile 464).
public sealed class BookRespawnPlayer : ModPlayer
{
	private static readonly int[] TableBuffs = { BuffID.Bewitched, BuffID.WarTable };

	private static readonly MethodInfo Shoot = typeof(Player).GetMethod("ItemCheck_Shoot", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(Item), typeof(int) }, null);

	private readonly List<(int Type, int Prefix)> uses = new List<(int, int)>();

	private readonly List<(int Type, int Time)> buffs = new List<(int, int)>();

	public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
	{
		if (Player.whoAmI != Main.myPlayer)
		{
			return;
		}
		BookConfig config = ModContent.GetInstance<BookConfig>();
		uses.Clear();
		buffs.Clear();
		if (config.ResummonMinionsAfterDeath)
		{
			// Oldest use first, as they were made; the game keeps the newest when there are more than the slots allow.
			foreach ((int item, int prefix, uint _) in Main.projectile.Where((Projectile p) => p.active && p.owner == Player.whoAmI && p.minion).Select((Projectile p) => p.GetGlobalProjectile<BookMinionSource>()).Where((BookMinionSource s) => s.Item > 0).Select((BookMinionSource s) => (s.Item, s.Prefix, s.Tick)).Distinct().OrderBy(((int, int, uint Tick) u) => u.Tick))
			{
				uses.Add((item, prefix));
			}
		}
		if (config.KeepTableBuffsAfterDeath)
		{
			foreach (int buff in TableBuffs)
			{
				int at = Player.FindBuffIndex(buff);
				if (at >= 0)
				{
					buffs.Add((buff, Player.buffTime[at]));
				}
			}
		}
	}

	public override void OnRespawn()
	{
		if (Player.whoAmI != Main.myPlayer)
		{
			return;
		}
		foreach ((int type, int time) in buffs)
		{
			Player.AddBuff(type, time);
		}
		buffs.Clear();
	}

	// One use per update, so each keeps an update of its own (BookMinionSource tells uses apart by it) and the next death
	// counts them again.
	public override void PostUpdate()
	{
		if (Player.whoAmI != Main.myPlayer || Player.dead || uses.Count == 0)
		{
			return;
		}
		(int type, int prefix) = uses[0];
		uses.RemoveAt(0);
		if (Shoot == null || !ContentSamples.ItemsByType.ContainsKey(type))
		{
			return;
		}
		Item item = new Item(type, 1, prefix);
		if (item.buffType > 0)
		{
			Player.AddBuff(item.buffType, item.buffTime);
		}
		Shoot.Invoke(Player, new object[] { Player.whoAmI, item, Player.GetWeaponDamage(item) });
	}
}
