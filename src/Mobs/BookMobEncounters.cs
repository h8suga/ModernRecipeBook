using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;


namespace ModernRecipeBook.Mobs;

// When the player first and last met each Bestiary entry, saved per character. "Met" is the game's own rule for
// marking a critter as seen: the NPC's hitbox inside the player's box grown by 300 x 200 px. Entries are Bestiary
// entries (a zombie skin counts as Zombie). Town NPCs are left out: they are always around the base.
// Feeds the Bestiary's "Recently seen" category (the last ones met), its "Met" sort and the card's Met line.
internal sealed class BookMobEncounters
{
	internal const int RecentCount = 24;

	private const int ScanInterval = 30;

	private readonly Dictionary<string, (long First, long Last)> Met = new Dictionary<string, (long, long)>(StringComparer.Ordinal);

	private readonly List<string> Recent = new List<string>();

	private int timer;

	internal int Version { get; private set; }

	internal IReadOnlyList<string> Newest => Recent;

	internal bool TryGet(string credit, out (long First, long Last) met)
	{
		return Met.TryGetValue(credit, out met);
	}

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
		foreach (string credit in seen)
		{
			Met[credit] = (Met.TryGetValue(credit, out var met) ? met.First : now, now);
		}
		// The mobs met now go first, in the order they already had among themselves, so a crowd standing still
		// does not reshuffle the list on every scan.
		List<string> before = new List<string>(Recent);
		seen.Sort((string a, string b) => Rank(before, a).CompareTo(Rank(before, b)));
		Recent.RemoveAll(seen.Contains);
		Recent.InsertRange(0, seen);
		if (Recent.Count > RecentCount)
		{
			Recent.RemoveRange(RecentCount, Recent.Count - RecentCount);
		}
		if (!Recent.SequenceEqual(before))
		{
			Version++;
		}
	}

	private static int Rank(List<string> list, string credit)
	{
		int index = list.IndexOf(credit);
		return (index < 0) ? int.MaxValue : index;
	}

	// "credit|first|last", most recently met first.
	internal List<string> Snapshot()
	{
		return Met.OrderByDescending((KeyValuePair<string, (long First, long Last)> pair) => Recent.Contains(pair.Key) ? (long.MaxValue - Recent.IndexOf(pair.Key)) : pair.Value.Last).Select((KeyValuePair<string, (long First, long Last)> pair) => pair.Key + "|" + pair.Value.First + "|" + pair.Value.Last).ToList();
	}

	internal void Load(IList<string> saved)
	{
		Met.Clear();
		Recent.Clear();
		foreach (string line in saved ?? Array.Empty<string>())
		{
			string[] parts = line.Split('|');
			if (parts.Length >= 2 && parts[0].Length > 0 && long.TryParse(parts[^1], out long last))
			{
				long first = (parts.Length >= 3 && long.TryParse(parts[^2], out long f)) ? f : last;
				string credit = string.Join("|", parts.Take(parts.Length - ((parts.Length >= 3) ? 2 : 1)));
				Met[credit] = (first, last);
				if (Recent.Count < RecentCount)
				{
					Recent.Add(credit);
				}
			}
		}
		Version++;
	}

	// Real time, so it also counts time away from the game.
	internal static string Ago(long utc)
	{
		return BookText.Ago(TimeSpan.FromTicks(Math.Max(0, DateTime.UtcNow.Ticks - utc)));
	}

	// "Oct 12" / "12 окт." (with the year when it is not this one), and the full form with the time for tooltips; the
	// patterns are localized (Time.DateShort, Time.DateLong), the month names come from the game's culture.
	internal static string Date(long utc, bool full)
	{
		DateTime local = new DateTime(utc, DateTimeKind.Utc).ToLocalTime();
		string day = L((local.Year == DateTime.Now.Year && !full) ? "Time.DateShort" : "Time.DateLong");
		return local.ToString(full ? (day + ", HH:mm") : day, Terraria.Localization.Language.ActiveCulture.CultureInfo);
	}
}