using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ModernRecipeBook.Common.Players;

// The summon item and the update a minion came from (Projectile.OnSpawn's item-use source), for BookRespawnPlayer: the
// minions one use summoned share both, so a death can tell how many uses of which item the minions on screen took. A
// projectile spawned by another one takes its parent's: some staffs throw something that makes the minion where it lands
// (Fuel Cell Bundle's MK2FlaskSummon spawns PlaguebringerMK2 from itself).
public sealed class BookMinionSource : GlobalProjectile
{
	public override bool InstancePerEntity => true;

	internal int Item;

	internal int Prefix;

	internal uint Tick;

	public override void OnSpawn(Projectile projectile, IEntitySource source)
	{
		if (source is EntitySource_ItemUse use && use.Player.whoAmI == Main.myPlayer && !use.Item.IsAir)
		{
			Item = use.Item.type;
			Prefix = use.Item.prefix;
			Tick = Main.GameUpdateCount;
		}
		else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.TryGetGlobalProjectile(out BookMinionSource from))
		{
			(Item, Prefix, Tick) = (from.Item, from.Prefix, from.Tick);
		}
	}
}
