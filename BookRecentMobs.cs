using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace ModernRecipeBook;

// The Bestiary's "Recently seen": the last mobs that came near the player, newest first, saved per character.
// "Near" is the game's own rule for marking a critter as seen (the NPC's hitbox inside the player's box grown by
// 300 x 200 px). Entries are Bestiary entries (a zombie skin counts as Zombie); town NPCs are left out, they are
// always around the base and would push everything else out.
internal sealed class BookRecentMobs
{
	internal const int Capacity = 24;

	private const int ScanInterval = 30;

	private readonly List<(string Credit, long SeenUtc)> Entries = new List<(string, long)>();

	private int timer;

	internal int Version { get; private set; }

	internal IReadOnlyList<(string Credit, long SeenUtc)> Newest => Entries;

	internal void Scan(Player player)
	{
		if (++timer < ScanInterval)
		{
			return;
		}
		timer = 0;
		Rectangle near = player.HitboxForBestiaryNearbyCheck;
		long now = DateTime.UtcNow.Ticks;
		List<string> seen = new List<string>();
		for (int i = 0; i < Main.maxNPCs; i++)
		{
			NPC npc = Main.npc[i];
			if (!npc.active || npc.life <= 0 || npc.townNPC || !BookMobTarget.Recognizable(npc) || !npc.Hitbox.Intersects(near))
			{
				continue;
			}
			NPC head = (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].active) ? Main.npc[npc.realLife] : npc;
			if (ContentSamples.NpcBestiaryCreditIdsByNpcNetIds.TryGetValue(head.netID, out string credit) && !seen.Contains(credit))
			{
				seen.Add(credit);
			}
		}
		if (seen.Count == 0)
		{
			return;
		}
		// The mobs seen now go first, in the order they already had among themselves, so a crowd standing still
		// does not reshuffle the list on every scan.
		List<string> before = Entries.Select(((string Credit, long SeenUtc) e) => e.Credit).ToList();
		seen.Sort((string a, string b) => Rank(before, a).CompareTo(Rank(before, b)));
		Entries.RemoveAll(((string Credit, long SeenUtc) e) => seen.Contains(e.Credit));
		Entries.InsertRange(0, seen.Select((string credit) => (credit, now)));
		if (Entries.Count > Capacity)
		{
			Entries.RemoveRange(Capacity, Entries.Count - Capacity);
		}
		if (!Entries.Select(((string Credit, long SeenUtc) e) => e.Credit).SequenceEqual(before))
		{
			Version++;
		}
	}

	private static int Rank(List<string> list, string credit)
	{
		int index = list.IndexOf(credit);
		return (index < 0) ? int.MaxValue : index;
	}

	internal List<string> Snapshot()
	{
		return Entries.Select(((string Credit, long SeenUtc) e) => e.Credit + "|" + e.SeenUtc).ToList();
	}

	internal void Load(IList<string> saved)
	{
		Entries.Clear();
		foreach (string line in saved ?? Array.Empty<string>())
		{
			int bar = line.LastIndexOf('|');
			if (bar > 0 && long.TryParse(line.AsSpan(bar + 1), out long ticks) && Entries.Count < Capacity)
			{
				Entries.Add((line.Substring(0, bar), ticks));
			}
		}
		Version++;
	}

	// "just now", "5 min ago", "3 h ago", "2 d ago": real time, so it also counts time away from the game.
	internal static string Ago(long seenUtc, bool russian)
	{
		TimeSpan span = TimeSpan.FromTicks(Math.Max(0, DateTime.UtcNow.Ticks - seenUtc));
		if (span.TotalMinutes < 1)
		{
			return russian ? "только что" : "just now";
		}
		if (span.TotalHours < 1)
		{
			return russian ? ((int)span.TotalMinutes + " мин назад") : ((int)span.TotalMinutes + " min ago");
		}
		if (span.TotalDays < 1)
		{
			return russian ? ((int)span.TotalHours + " ч назад") : ((int)span.TotalHours + " h ago");
		}
		return russian ? ((int)span.TotalDays + " дн назад") : ((int)span.TotalDays + " d ago");
	}
}
