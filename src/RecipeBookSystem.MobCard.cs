using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using static ModernRecipeBook.BookText;

namespace ModernRecipeBook;

// The mob card's numbers. Drops are always there; life and the rest once the Bestiary would show them (per
// world): an enemy after its first kill, a town NPC after a chat, a critter once seen.
public sealed partial class RecipeBookSystem
{
	internal readonly record struct MobStats(int Life, int Defense, int Damage, float KnockbackTaken, int Value, float? DamageReduction);

	// The mobs pointed at in the world: their cards (also after Back) show that mob's numbers, not a fresh copy's.
	private static readonly Dictionary<int, MobStats> LiveMobStats = new Dictionary<int, MobStats>();

	private static MobStats EnemyStats;

	private static bool EnemyStatsLive;

	// Expert and Master give a boss more life per player in the world (NPC.ScaleStats); the card is read again when
	// someone joins or leaves (EnsureMobCard keys it on the player count).
	private static int EnemyStatsPlayers;

	private static void ReadEnemyStats(int netId)
	{
		EnemyStatsPlayers = NPC.GetActivePlayerCount();
		EnemyStatsLive = netId == LiveMob && LiveMobStats.TryGetValue(netId, out EnemyStats);
		if (!EnemyStatsLive)
		{
			EnemyStats = FreshMobStats(netId);
		}
	}

	// "For this world · Expert · 2 players": the player count only where it changes the numbers.
	private static string StatsScope()
	{
		string scope = (EnemyStatsLive ? L("MobCard.ThisMobInTheWorld") : L("MobCard.ForThisWorld")) + " · " + WorldModeName();
		if (Main.netMode == NetmodeID.MultiplayerClient && Main.expertMode && EnemyStatsPlayers > 1)
		{
			scope += " · " + L("MobCard.CountPlayers", EnemyStatsPlayers);
		}
		return scope;
	}

	// The Bestiary's way: a new NPC set up for this world, so the game mode, the player count, Calamity's modes and
	// every mod's SetDefaults apply. The samples the book has were made for Classic.
	private static MobStats FreshMobStats(int netId)
	{
		try
		{
			NPC npc = new NPC();
			npc.SetDefaults(netId);
			return ReadMobStats(npc);
		}
		catch (Exception)
		{
			EnemyReadErrors++;
			return ReadMobStats(ContentSamples.NpcsByNetId[netId]);
		}
	}

	private static MobStats ReadMobStats(NPC npc)
	{
		return new MobStats(npc.lifeMax, npc.defense, npc.damage, npc.knockBackResist, (int)Math.Min(npc.value, int.MaxValue), CalamityDamageReduction(npc));
	}

	// Calamity keeps damage reduction on its global NPC (CalamityGlobalNPC.DR, 0..1); read it without a reference.
	private static float? CalamityDamageReduction(NPC npc)
	{
		try
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity) || !calamity.TryFind("CalamityGlobalNPC", out GlobalNPC global) || !npc.TryGetGlobalNPC(global, out GlobalNPC instance))
			{
				return null;
			}
			return instance.GetType().GetProperty("DR", BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance) as float?;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static bool CalamityMode(string field)
	{
		try
		{
			return ModLoader.TryGetMod("CalamityMod", out Mod calamity) && calamity.Code?.GetType("CalamityMod.World.CalamityWorld")?.GetField(field, BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string WorldModeName()
	{
		string mode = Main.GameMode switch
		{
			GameModeID.Expert => L("MobCard.Expert"),
			GameModeID.Master => L("MobCard.Master"),
			GameModeID.Creative => L("MobCard.Journey"),
			_ => L("MobCard.Classic"),
		};
		if (CalamityMode("death"))
		{
			return mode + " · " + L("MobCard.Death");
		}
		return CalamityMode("revenge") ? (mode + " · " + L("MobCard.Revengeance")) : mode;
	}

	// The Bestiary entry's own unlock rule, so mods' NPCs follow theirs: town NPCs open with a chat, critters once
	// seen near the player, enemies with kills. An enemy's numbers here open with its first kill, before the Bestiary's.
	private static bool MobStatsUnlocked(int netId, NPC sample, int kills)
	{
		if (kills > 0 && !sample.CountsAsACritter && !sample.isLikeATownNPC)
		{
			return true;
		}
		return MobUnlockState(netId) >= BestiaryEntryUnlockState.CanShowStats_2;
	}

	// The mob's Bestiary entry; variants without an entry of their own (zombie skins) use the one they are credited to.
	private static BestiaryEntry MobEntry(int netId)
	{
		BestiaryEntry entry = Main.BestiaryDB.FindEntryByNPCID(netId);
		if (entry.UIInfoProvider == null && ContentSamples.NpcNetIdsByPersistentIds.TryGetValue(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[netId], out int credited))
		{
			entry = Main.BestiaryDB.FindEntryByNPCID(credited);
		}
		return entry;
	}

	private static BestiaryEntryUnlockState MobUnlockState(int netId)
	{
		return MobEntry(netId).UIInfoProvider?.GetEntryUICollectionInfo().UnlockState ?? BestiaryEntryUnlockState.NotKnownAtAll_0;
	}

	// The card's Stats view. The unlock and the banner are read every frame, so a kill, a chat or a sighting opens
	// it at once.
	private static void DrawMobStats(SpriteBatch sb, int netId)
	{
		BookRect box = BookGeometry.MobBody;
		NPC sample = ContentSamples.NpcsByNetId[netId];
		int kills = Main.BestiaryTracker.Kills.GetKillCount(ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[netId]);
		if (!MobStatsUnlocked(netId, sample, kills))
		{
			string hint = sample.CountsAsACritter ? L("MobCard.SeeItOnce") : (sample.isLikeATownNPC ? L("MobCard.TalkToThemOnce") : L("MobCard.KillItOnceToSeeThem"));
			DrawCardEmpty(sb, box, L("MobCard.Locked"), hint);
			if (box.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(L("MobCard.UnlockedAsInTheBestiary"));
			}
			return;
		}
		if (box.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(StatsScope());
		}
		MobStats stats = EnemyStats;
		int banner = Item.NPCtoBanner(sample.BannerID());
		int bannerItem = (banner > 0) ? Item.BannerToItem(banner) : 0;
		bool hasBanner = banner < NPC.killCount.Length && bannerItem > 0 && bannerItem < ItemID.Sets.KillsToBanner.Length && !sample.ExcludedFromDeathTally();
		int need = hasBanner ? Math.Max(1, ItemID.Sets.KillsToBanner[bannerItem]) : 1;
		int row = 0;
		Row(L("MobCard.Life"), Count(stats.Life));
		Row(L("MobCard.Defense"), Count(stats.Defense));
		Row(L("MobCard.Damage"), Count(stats.Damage));
		Row(L("MobCard.KnockbackResist"), Percent(1f - stats.KnockbackTaken), L("MobCard.HowMuchLessItIsKnocked"));
		BookRect coins = Row(L("MobCard.Coins"), stats.Value > 0 ? "" : "—");
		DrawCoins(sb, coins, stats.Value);
		Row(L("MobCard.Kills"), Count(kills), L("MobCard.KilledInThisWorld"));
		Row(L("MobCard.Banner"), hasBanner ? (Count(NPC.killCount[banner] % need) + "/" + Count(need)) : "—", hasBanner ? L("MobCard.KillsTowardTheNextBanner") : null);
		if (stats.DamageReduction.HasValue)
		{
			Row(L("MobCard.DamageReduction"), Percent(stats.DamageReduction.Value), L("MobCard.CalamityShareOfDamageItIgnores"));
		}
		BookRect Row(string name, string value, string tip = null)
		{
			BookRect rect = BookGeometry.MobStatRow(row++);
			float valueWidth = (value.Length > 0) ? (TextWidth(value, 0.95f) + 4f) : 0f;
			CenterLabel(sb, name, rect.X, rect.Y, rect.Width - valueWidth - 8f, rect.Height, Muted, 0.85f, rightAligned: false, leftAligned: true);
			AcquisitionBadge(sb, rect, value);
			if (tip != null && rect.Contains(Mouse.X, Mouse.Y))
			{
				MouseTip(tip);
			}
			return rect;
		}
	}

	private static string Percent(float fraction)
	{
		return (int)Math.Round(Math.Clamp(fraction, 0f, 1f) * 100f) + "%";
	}

	// Platinum, gold, silver and copper like the game shows them: each count followed by its coin, right-aligned.
	private static void DrawCoins(SpriteBatch sb, BookRect rect, int value)
	{
		float right = rect.X + rect.Width;
		(int Count, int Coin)[] parts = new (int, int)[4]
		{
			(value % 100, ItemID.CopperCoin),
			(value / 100 % 100, ItemID.SilverCoin),
			(value / 10000 % 100, ItemID.GoldCoin),
			(value / 1000000, ItemID.PlatinumCoin)
		};
		foreach ((int count, int coin) in parts.Where(((int Count, int Coin) p) => p.Count > 0))
		{
			Icon(sb, ContentSamples.ItemsByType[coin], right - 18f, rect.Y + (rect.Height - 18f) / 2f, 18f, tooltip: false);
			string number = count.ToString();
			float width = TextWidth(number, 0.95f) + 4f;
			CenterLabel(sb, number, right - 20f - width, rect.Y, width, rect.Height, TextColor, 0.95f, rightAligned: true);
			right -= 20f + width + 8f;
		}
	}
}
