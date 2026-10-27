using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernRecipeBook.Storage;

// The host's two settings (BookServerConfig is ServerSide: clients get the host's values on joining). Read through
// here so a hand-edited config file can never ask the tile scans for a radius past 50.
internal static class BookNearbyConfig
{
	internal static bool Enabled => ModContent.GetInstance<BookServerConfig>()?.NearbyStorage ?? true;

	internal static int Radius => Math.Clamp(ModContent.GetInstance<BookServerConfig>()?.NearbyRadius ?? 30, 10, 50);
}

// The only place that removes items from world chests and Magic Storage: the server, or the single-player game.
// Clients get a per-chest mirror while they need counts and ask for (type, prefix, amount); this side picks the
// containers, removes atomically (packets run on the main thread) and answers with the stacks it took. Every stack is
// recorded the moment it leaves its container, so an exception or a disconnect puts it back
// (docs/nearby-storage.md §4).
public sealed class BookNearbyServer : ModSystem
{
	internal enum Status : byte
	{
		Done,
		Partial,
		Nothing,
		Off,
		Busy,
		Invalid
	}

	internal readonly record struct Entry(int Type, int Prefix, int Amount);

	// Prefix -1 in a pull entry: any prefix (crafting), plain items first.
	internal const int AnyPrefix = -1;

	private readonly record struct Origin(int Chest, short X, short Y, Point16 Heart);

	private sealed class Delivery
	{
		internal int Id;

		internal readonly List<(Item Item, Origin From)> Items = new List<(Item, Origin)>();
	}

	private sealed class Client
	{
		internal bool Subscribed;

		internal bool Reset;

		internal readonly Dictionary<int, ulong> Sent = new Dictionary<int, ulong>();

		internal float Tokens = Burst;

		internal ulong Refilled;

		internal readonly List<Delivery> InFlight = new List<Delivery>();

		internal int LastLocked = -1;

		internal int LastInUse = -1;

		internal int LastFlags = -1;

		// When the last subscription snapshot was sent (Subscribe).
		internal ulong Snapshot;
	}

	private const int CycleTicks = 30;

	private const float Burst = 32f;

	private const float RefillPerTick = 16f / 60f;

	private const int MaxInFlight = 8;

	internal const int MaxEntries = 32;

	internal const int MaxStacks = 256;

	// The main inventory and the coin slots: the most one Deposit All can send.
	internal const int MaxDeposit = 60;

	private static readonly Client[] Clients = new Client[256];

	private static readonly HashSet<int> OpenChests = new HashSet<int>();

	private static readonly List<int> Eligible = new List<int>();

	private static readonly HashSet<int> EligibleSet = new HashSet<int>();

	private static readonly List<int> Gone = new List<int>();

	private static readonly List<BookMagicStorage.Network> NetworkScratch = new List<BookMagicStorage.Network>();

	private static int Ticks;

	private static Client Of(int player)
	{
		return Clients[player] ??= new Client();
	}

	// The Player object in each slot when last seen. A client who leaves gets a new one in their slot
	// (RemoteClient.Reset, TML:Terraria/RemoteClient.cs:216), so a changed object means the slot changed hands, even
	// when no update ran while it was empty (a server whose only player left and came back).
	private static readonly Player[] SlotPlayers = new Player[255];

	public override void Load()
	{
		if (Main.dedServ)
		{
			On_WorldFile.SaveWorld_bool_bool += RestoreBeforeExitSave;
		}
	}

	public override void Unload()
	{
		if (Main.dedServ)
		{
			On_WorldFile.SaveWorld_bool_bool -= RestoreBeforeExitSave;
		}
	}

	public override void PostUpdateEverything()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
		{
			return;
		}
		// Players who left: deliveries they never acknowledged go back, their subscription ends, and the Magic Storage
		// window they had open is no longer theirs or the next player's (main thread; the disconnect hook runs on the
		// network thread, Netplay.UpdateConnectedClients).
		for (int p = 0; p < 255; p++)
		{
			Player player = Main.player[p];
			bool replaced = Main.netMode == NetmodeID.Server && SlotPlayers[p] != null && SlotPlayers[p] != player;
			SlotPlayers[p] = player;
			if (replaced)
			{
				BookMagicStorage.ForgetSession(p);
			}
			Client client = Clients[p];
			if (client != null && (replaced || !player.active))
			{
				foreach (Delivery delivery in client.InFlight)
				{
					Restore(delivery.Items);
				}
				Clients[p] = null;
			}
		}
		if (++Ticks % CycleTicks == 0)
		{
			Cycle(-1);
		}
	}

	public override void OnWorldUnload()
	{
		RestoreInFlight();
		Array.Clear(Clients);
		Array.Clear(SlotPlayers);
		Ticks = 0;
	}

	private static void RestoreInFlight()
	{
		if (Main.netMode != NetmodeID.Server)
		{
			return;
		}
		foreach (Client client in Clients)
		{
			if (client != null)
			{
				foreach (Delivery delivery in client.InFlight)
				{
					Restore(delivery.Items, closing: true);
				}
				client.InFlight.Clear();
			}
		}
	}

	// A server that shuts down saves the world before OnWorldUnload (TML:Terraria/Main.cs:6019-6024): deliveries still in
	// flight go back first, so the last save has them. Host & Play shuts down this way when the host leaves
	// (NetMessage.EnsureLocalPlayerIsPresent), and no update runs after the last player left to restore theirs.
	private static void RestoreBeforeExitSave(On_WorldFile.orig_SaveWorld_bool_bool orig, bool useCloudSaving, bool resetTime)
	{
		if (Netplay.Disconnect)
		{
			RestoreInFlight();
		}
		orig(useCloudSaving, resetTime);
	}

	// A subscription takes a full snapshot of every chest in range. Asked for more often than once a cycle, the
	// snapshot waits for the next cycle, so a client that keeps asking costs no more than one that asks once.
	internal static void Subscribe(int player, bool on)
	{
		Client client = Of(player);
		client.Subscribed = on;
		client.Reset = true;
		client.Sent.Clear();
		if (on && Main.GameUpdateCount - client.Snapshot >= CycleTicks)
		{
			client.Snapshot = Main.GameUpdateCount;
			Cycle(player);
		}
	}

	// Chest mirror deltas for every subscriber (or one player): eligible chests whose contents changed, chests that
	// left the set. Nothing is sent when nothing changed.
	private static void Cycle(int only)
	{
		bool any = false;
		for (int p = 0; p < 255 && !any; p++)
		{
			any = Clients[p] != null && Clients[p].Subscribed;
		}
		if (!any)
		{
			return;
		}
		OpenChests.Clear();
		for (int p = 0; p < 255; p++)
		{
			if (Main.player[p].active && Main.player[p].chest >= 0)
			{
				OpenChests.Add(Main.player[p].chest);
			}
		}
		for (int p = 0; p < 255; p++)
		{
			if ((only < 0 || only == p) && Clients[p] != null && Clients[p].Subscribed && Main.player[p].active)
			{
				Sync(p, Clients[p]);
			}
		}
	}

	private static void Sync(int p, Client client)
	{
		Player player = Main.player[p];
		bool enabled = BookNearbyConfig.Enabled;
		int locked = 0;
		int inUse = 0;
		Eligible.Clear();
		if (enabled)
		{
			FindChests(player, BookNearbyConfig.Radius, Eligible, ref locked, ref inUse);
		}
		List<BookSources.MirrorChest> upserts = null;
		List<int> removes = null;
		EligibleSet.Clear();
		foreach (int index in Eligible)
		{
			EligibleSet.Add(index);
			Chest chest = Main.chest[index];
			ulong signature = Signature(chest);
			if (!client.Sent.TryGetValue(index, out var sent) || sent != signature)
			{
				client.Sent[index] = signature;
				(upserts ??= new List<BookSources.MirrorChest>()).Add(Snapshot(index, chest));
			}
		}
		Gone.Clear();
		foreach (int index in client.Sent.Keys)
		{
			if (!EligibleSet.Contains(index))
			{
				Gone.Add(index);
			}
		}
		if (Gone.Count > 0)
		{
			removes = new List<int>(Gone);
			foreach (int index in Gone)
			{
				client.Sent.Remove(index);
			}
		}
		bool reset = client.Reset;
		client.Reset = false;
		// Bit 2: Magic Storage writes work here (they stop for good when a member fails, BookMagicStorage.Fail).
		int state = (BookMagicStorage.Writable ? 2 : 0) | (Main.ServerSideCharacter ? 4 : 0);
		if (!reset && upserts == null && removes == null && locked == client.LastLocked && inUse == client.LastInUse && state == client.LastFlags)
		{
			return;
		}
		client.LastLocked = locked;
		client.LastInUse = inUse;
		client.LastFlags = state;
		byte flags = (byte)((reset ? 1 : 0) | state);
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			BookSources.ApplyChests(flags, locked, inUse, upserts, removes);
			return;
		}
		SendChests(p, flags, locked, inUse, upserts, removes);
	}

	// Chests and dressers within `tiles` of the player that a pull may use: not locked, open by nobody. The player's own
	// open chest is theirs to use directly and is left out silently; one another player has open counts as in use.
	private static void FindChests(Player player, int tiles, List<int> into, ref int locked, ref int inUse)
	{
		Vector2 center = player.Center;
		for (int index = 0; index < Main.maxChests; index++)
		{
			Chest chest = Main.chest[index];
			if (chest == null || chest.x < 0 || chest.y < 0 || chest.x >= Main.maxTilesX || chest.y >= Main.maxTilesY)
			{
				continue;
			}
			// A dresser's centre is 8 px further right: the cheap test first, the tile read only near the edge.
			if (!BookSourceRules.InRange(center, BookSourceRules.ChestCenter(chest.x, chest.y, dresser: false), tiles + 1))
			{
				continue;
			}
			bool dresser = TileID.Sets.BasicDresser[Main.tile[chest.x, chest.y].TileType];
			if (!BookSourceRules.InRange(center, BookSourceRules.ChestCenter(chest.x, chest.y, dresser), tiles))
			{
				continue;
			}
			if (Chest.IsLocked(chest.x, chest.y))
			{
				locked++;
			}
			else if (player.chest == index)
			{
			}
			else if (OpenChests.Contains(index))
			{
				inUse++;
			}
			else
			{
				into.Add(index);
			}
		}
	}

	private static ulong Signature(Chest chest)
	{
		ulong hash = 14695981039346656037uL;
		void Mix(int value)
		{
			hash = (hash ^ (uint)value) * 1099511628211uL;
		}
		Mix(chest.x);
		Mix(chest.y);
		for (int slot = 0; slot < chest.item.Length; slot++)
		{
			Item item = chest.item[slot];
			if (item != null && !item.IsAir)
			{
				Mix(slot);
				Mix(item.type);
				Mix(item.prefix);
				Mix(item.stack);
			}
		}
		return hash;
	}

	private static BookSources.MirrorChest Snapshot(int index, Chest chest)
	{
		Dictionary<(int, int), int> counts = new Dictionary<(int, int), int>();
		int used = 0;
		foreach (Item item in chest.item)
		{
			if (item != null && !item.IsAir)
			{
				used++;
				counts[(item.type, item.prefix)] = (int)Math.Min(int.MaxValue, (long)counts.GetValueOrDefault((item.type, item.prefix)) + item.stack);
			}
		}
		BookSources.Stock[] entries = new BookSources.Stock[counts.Count];
		int n = 0;
		foreach (KeyValuePair<(int, int), int> pair in counts)
		{
			entries[n++] = new BookSources.Stock(pair.Key.Item1, pair.Key.Item2, pair.Value);
		}
		return new BookSources.MirrorChest(index, (short)chest.x, (short)chest.y, entries, used);
	}

	private static bool Admit(int p, Client client, out Status refusal)
	{
		Player player = Main.player[p];
		refusal = Status.Off;
		if (!BookNearbyConfig.Enabled || Main.ServerSideCharacter)
		{
			return false;
		}
		refusal = Status.Invalid;
		if (!player.active || player.dead || player.ghost)
		{
			return false;
		}
		refusal = Status.Busy;
		ulong now = Main.GameUpdateCount;
		client.Tokens = Math.Min(Burst, client.Tokens + (now - client.Refilled) * RefillPerTick);
		client.Refilled = now;
		if (client.Tokens < 1f || client.InFlight.Count >= MaxInFlight)
		{
			return false;
		}
		client.Tokens -= 1f;
		return true;
	}

	// Takes what it can of each entry: chests nearest first, then Magic Storage networks nearest first. Positions are
	// the server's, with BookSourceRules.SlackTiles more than the radius.
	internal static Status Pull(int p, int id, IReadOnlyList<Entry> entries, List<Item> delivered)
	{
		Client client = Of(p);
		if (!Admit(p, client, out var refusal))
		{
			return refusal;
		}
		if (entries.Count == 0 || entries.Count > MaxEntries)
		{
			return Status.Invalid;
		}
		foreach (Entry entry in entries)
		{
			if (entry.Type <= 0 || entry.Type >= ItemLoader.ItemCount || entry.Amount <= 0 || entry.Amount > 999999 || entry.Prefix < AnyPrefix || entry.Prefix >= PrefixLoader.PrefixCount)
			{
				return Status.Invalid;
			}
		}
		Player player = Main.player[p];
		int tiles = BookNearbyConfig.Radius + BookSourceRules.SlackTiles;
		OpenChests.Clear();
		for (int i = 0; i < 255; i++)
		{
			if (Main.player[i].active && Main.player[i].chest >= 0)
			{
				OpenChests.Add(Main.player[i].chest);
			}
		}
		int locked = 0;
		int inUse = 0;
		List<int> chests = new List<int>();
		FindChests(player, tiles, chests, ref locked, ref inUse);
		Vector2 center = player.Center;
		chests.Sort((int a, int b) => Vector2.DistanceSquared(center, Center(a)).CompareTo(Vector2.DistanceSquared(center, Center(b))));
		List<BookMagicStorage.Network> networks = NetworkScratch;
		networks.Clear();
		if (BookMagicStorage.Writable)
		{
			BookMagicStorage.Scan(player, tiles, null, networks, null, out _);
			networks.Sort((BookMagicStorage.Network a, BookMagicStorage.Network b) => a.Distance.CompareTo(b.Distance));
		}
		Delivery delivery = new Delivery { Id = id };
		long asked = 0;
		long given = 0;
		try
		{
			foreach (Entry entry in entries)
			{
				asked += entry.Amount;
				int remaining = entry.Amount;
				foreach (int index in chests)
				{
					if (remaining <= 0 || delivery.Items.Count >= MaxStacks)
					{
						break;
					}
					TakeFromChest(index, entry, ref remaining, delivery.Items);
				}
				foreach (BookMagicStorage.Network network in networks)
				{
					if (remaining <= 0 || delivery.Items.Count >= MaxStacks)
					{
						break;
					}
					TakeFromHeart(p, network.Heart, entry, ref remaining, delivery.Items);
				}
				given += entry.Amount - remaining;
			}
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Nearby pull failed; everything taken so far went back.", error);
			Restore(delivery.Items);
			return Status.Invalid;
		}
		SafeReport(p, delivery.Items, deposited: null);
		foreach ((Item item, Origin _) in delivery.Items)
		{
			delivered.Add(item);
		}
		if (Main.netMode == NetmodeID.Server && delivery.Items.Count > 0)
		{
			client.InFlight.Add(delivery);
		}
		Cycle(p);
		return (given == 0) ? Status.Nothing : ((given < asked) ? Status.Partial : Status.Done);
	}

	private static Vector2 Center(int index)
	{
		Chest chest = Main.chest[index];
		return BookSourceRules.ChestCenter(chest.x, chest.y, TileID.Sets.BasicDresser[Main.tile[chest.x, chest.y].TileType]);
	}

	// Exact prefix, or for "any" plain items first and then the cheapest, so reforged gear is used last.
	private static void TakeFromChest(int index, Entry entry, ref int remaining, List<(Item, Origin)> taken)
	{
		Chest chest = Main.chest[index];
		List<int> slots = new List<int>();
		for (int slot = 0; slot < chest.item.Length; slot++)
		{
			Item item = chest.item[slot];
			if (item != null && !item.IsAir && item.type == entry.Type && (entry.Prefix == AnyPrefix || item.prefix == entry.Prefix))
			{
				slots.Add(slot);
			}
		}
		if (entry.Prefix == AnyPrefix && slots.Count > 1)
		{
			slots.Sort((int a, int b) => PlainFirst(chest.item[a], chest.item[b]));
		}
		Origin origin = new Origin(index, (short)chest.x, (short)chest.y, Point16.NegativeOne);
		foreach (int slot in slots)
		{
			if (remaining <= 0 || taken.Count >= MaxStacks)
			{
				break;
			}
			Item item = chest.item[slot];
			int n = Math.Min(remaining, item.stack);
			Item piece = item.Clone();
			piece.stack = n;
			item.stack -= n;
			taken.Add((piece, origin));
			if (item.stack <= 0)
			{
				item.TurnToAir();
			}
			remaining -= n;
		}
	}

	private static int PlainFirst(Item a, Item b)
	{
		int order = (a.prefix != 0).CompareTo(b.prefix != 0);
		return (order != 0) ? order : a.value.CompareTo(b.value);
	}

	// MS's Withdraw sums what every unit gives into one item (MS:MagicStorage.Components/TEStorageHeart.cs:766-806),
	// so one call could hand out a stack past maxStack: it is asked for one stack at a time.
	private static void TakeFromHeart(int p, object heart, Entry entry, ref int remaining, List<(Item, Origin)> taken)
	{
		List<int> prefixes = new List<int>();
		if (entry.Prefix != AnyPrefix)
		{
			prefixes.Add(entry.Prefix);
		}
		else
		{
			List<Item> stored = new List<Item>();
			bool creative = BookMagicStorage.CollectItems(heart, stored);
			stored.Sort(PlainFirst);
			foreach (Item item in stored)
			{
				if (item.type == entry.Type && !prefixes.Contains(item.prefix))
				{
					prefixes.Add(item.prefix);
				}
			}
			if (creative && !prefixes.Contains(0))
			{
				prefixes.Insert(0, 0);
			}
		}
		Origin origin = new Origin(-1, 0, 0, ((TileEntity)heart).Position);
		int maxStack = Math.Max(1, ContentSamples.ItemsByType[entry.Type].maxStack);
		foreach (int prefix in prefixes)
		{
			while (remaining > 0 && taken.Count < MaxStacks)
			{
				Item got = BookMagicStorage.Withdraw(heart, new Item(entry.Type, Math.Min(remaining, maxStack), prefix), p);
				if (got == null || got.IsAir)
				{
					break;
				}
				taken.Add((got, origin));
				remaining -= got.stack;
			}
		}
	}

	private static void ReportHearts(int p, List<(Item Item, Origin From)> withdrawn, List<(Item Item, Point16 Heart)> deposited)
	{
		Dictionary<Point16, (List<Item> Out, List<Item> In, HashSet<int> Types)> byHeart = new Dictionary<Point16, (List<Item>, List<Item>, HashSet<int>)>();
		(List<Item> Out, List<Item> In, HashSet<int> Types) For(Point16 heart)
		{
			if (!byHeart.TryGetValue(heart, out var lists))
			{
				lists = (new List<Item>(), new List<Item>(), new HashSet<int>());
				byHeart[heart] = lists;
			}
			return lists;
		}
		foreach ((Item item, Origin from) in withdrawn)
		{
			if (from.Chest < 0)
			{
				var lists = For(from.Heart);
				lists.Out.Add(item);
				lists.Types.Add(item.type);
			}
		}
		if (deposited != null)
		{
			foreach ((Item item, Point16 heart) in deposited)
			{
				var lists = For(heart);
				lists.In.Add(item);
				lists.Types.Add(item.type);
			}
		}
		foreach (KeyValuePair<Point16, (List<Item> Out, List<Item> In, HashSet<int> Types)> pair in byHeart)
		{
			object heart = BookMagicStorage.HeartAt(pair.Key);
			if (heart != null)
			{
				BookMagicStorage.AfterChange(heart, p, pair.Value.Out, pair.Value.In, pair.Value.Types);
			}
		}
	}

	// Deposit, the Storage tab used like one big chest (docs/nearby-storage.md §4.9). Per item:
	// (1) onto stacks of the same item, nearest container first: a chest that holds it (vanilla's quick-stack rule:
	//     fill its stacks, then a free slot of that chest), or a Magic Storage network that holds the type;
	// (2) the rest into the nearest network;
	// (3) else the nearest chest with a free slot.
	// Banks are not targets here: the client puts coins into the Piggy Bank before it sends (BookSources.SendDeposit).
	// Like MS's own deposit, the client took the items out of its slots when it sent them; what does not fit comes
	// back. Chests follow the pull rules: in range, not locked, open by nobody.
	internal static Status Deposit(int p, int id, List<Item> items, List<Item> leftovers)
	{
		Client client = Of(p);
		if (!Admit(p, client, out var refusal))
		{
			leftovers.AddRange(items);
			Hold(p, client, id, leftovers);
			return refusal;
		}
		// A stack past maxStack comes from no slot the client may have; it goes back whole.
		if (items.Count == 0 || items.Count > MaxDeposit || items.Any((Item item) => item != null && item.stack > Math.Max(1, item.maxStack)))
		{
			leftovers.AddRange(items);
			Hold(p, client, id, leftovers);
			return Status.Invalid;
		}
		Player player = Main.player[p];
		Vector2 center = player.Center;
		int tiles = BookNearbyConfig.Radius + BookSourceRules.SlackTiles;
		OpenChests.Clear();
		for (int i = 0; i < 255; i++)
		{
			if (Main.player[i].active && Main.player[i].chest >= 0)
			{
				OpenChests.Add(Main.player[i].chest);
			}
		}
		int locked = 0;
		int inUse = 0;
		List<int> chests = new List<int>();
		FindChests(player, tiles, chests, ref locked, ref inUse);
		List<float> chestDistance = new List<float>(chests.Count);
		chests.Sort((int a, int b) => Vector2.DistanceSquared(center, Center(a)).CompareTo(Vector2.DistanceSquared(center, Center(b))));
		foreach (int index in chests)
		{
			chestDistance.Add(Vector2.DistanceSquared(center, Center(index)));
		}
		List<BookMagicStorage.Network> networks = new List<BookMagicStorage.Network>();
		List<HashSet<int>> holds = new List<HashSet<int>>();
		if (BookMagicStorage.Writable)
		{
			BookMagicStorage.Scan(player, tiles, null, networks, null, out _);
			networks.Sort((BookMagicStorage.Network a, BookMagicStorage.Network b) => a.Distance.CompareTo(b.Distance));
			List<Item> stored = new List<Item>();
			foreach (BookMagicStorage.Network network in networks)
			{
				stored.Clear();
				BookMagicStorage.CollectItems(network.Heart, stored);
				holds.Add(new HashSet<int>(stored.Select((Item i) => i.type)));
			}
		}
		List<(Item, Point16)> deposited = new List<(Item, Point16)>();
		long placed = 0;
		int done = 0;
		try
		{
			for (; done < items.Count; done++)
			{
				Item item = items[done];
				if (item == null || item.IsAir)
				{
					continue;
				}
				int before = item.stack;
				Place(p, item, chests, chestDistance, networks, holds, deposited);
				placed += before - (item.IsAir ? 0 : item.stack);
				if (!item.IsAir)
				{
					leftovers.Add(item);
				}
			}
		}
		catch (Exception error)
		{
			// What was placed stays placed (it is in storage, not lost); the rest goes back to the player.
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Nearby deposit failed; the rest goes back to the player.", error);
			for (int i = done; i < items.Count; i++)
			{
				if (items[i] != null && !items[i].IsAir && !leftovers.Contains(items[i]))
				{
					leftovers.Add(items[i]);
				}
			}
		}
		SafeReport(p, new List<(Item, Origin)>(), deposited);
		Hold(p, client, id, leftovers);
		Cycle(p);
		return (placed == 0) ? Status.Nothing : ((leftovers.Count > 0) ? Status.Partial : Status.Done);
	}

	// Leftovers of a deposit, refused ones included, are not yet back in the client's inventory: until the client
	// acknowledges them, a disconnect drops them where the player stood. A client that never acknowledges gets them on
	// the ground at once past MaxHeld, so the list cannot grow without bound.
	private const int MaxHeld = 64;

	private static void Hold(int p, Client client, int id, List<Item> leftovers)
	{
		if (Main.netMode != NetmodeID.Server || leftovers.Count == 0)
		{
			return;
		}
		Point point = Main.player[p].Center.ToTileCoordinates();
		Delivery delivery = new Delivery { Id = id };
		foreach (Item item in leftovers)
		{
			delivery.Items.Add((item, new Origin(-2, (short)point.X, (short)point.Y, Point16.NegativeOne)));
		}
		if (client.InFlight.Count >= MaxHeld)
		{
			Restore(delivery.Items);
			leftovers.Clear();
			return;
		}
		client.InFlight.Add(delivery);
	}

	// Magic Storage's refresh and audit after a change: the items have already moved, so a failure here is only logged.
	private static void SafeReport(int p, List<(Item Item, Origin From)> withdrawn, List<(Item Item, Point16 Heart)> deposited)
	{
		try
		{
			ReportHearts(p, withdrawn, deposited);
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Magic Storage refresh after a nearby change failed.", error);
		}
	}

	private static void Place(int p, Item item, List<int> chests, List<float> chestDistance, List<BookMagicStorage.Network> networks, List<HashSet<int>> holds, List<(Item, Point16)> deposited)
	{
		// (1) Matching stacks, nearest container first.
		int c = 0;
		int n = 0;
		while (!item.IsAir && (c < chests.Count || n < networks.Count))
		{
			if (n >= networks.Count || (c < chests.Count && chestDistance[c] <= networks[n].Distance))
			{
				StackIntoChest(Main.chest[chests[c]], item);
				c++;
			}
			else
			{
				if (holds[n].Contains(item.type))
				{
					DepositInto(p, networks[n], item, deposited);
				}
				n++;
			}
		}
		// (2) The nearest network.
		if (!item.IsAir && networks.Count > 0)
		{
			DepositInto(p, networks[0], item, deposited);
		}
		// (3) The nearest chest with a free slot.
		for (int i = 0; i < chests.Count && !item.IsAir; i++)
		{
			Item[] slots = Main.chest[chests[i]].item;
			for (int k = 0; k < slots.Length; k++)
			{
				if (slots[k] == null || slots[k].IsAir)
				{
					slots[k] = item.Clone();
					item.TurnToAir();
					break;
				}
			}
		}
	}

	// Vanilla's per-chest rule (Chest.PutItemInNearbyChest): stack onto the same items, then, if the chest held the
	// item, the rest into one of its free slots.
	private static void StackIntoChest(Chest chest, Item item)
	{
		bool held = false;
		bool free = false;
		foreach (Item slot in chest.item)
		{
			if (slot == null || slot.IsAir)
			{
				free = true;
			}
			else if (slot.type == item.type && slot.netID == item.netID)
			{
				held = true;
				ItemLoader.TryStackItems(slot, item, out _);
				if (item.stack <= 0)
				{
					item.TurnToAir();
					return;
				}
			}
			else
			{
				free = true;
			}
		}
		if (!held || !free)
		{
			return;
		}
		for (int k = 0; k < chest.item.Length; k++)
		{
			if (chest.item[k] == null || chest.item[k].IsAir)
			{
				chest.item[k] = item.Clone();
				item.TurnToAir();
				return;
			}
		}
	}

	private static void DepositInto(int p, BookMagicStorage.Network network, Item item, List<(Item, Point16)> deposited)
	{
		int before = item.stack;
		BookMagicStorage.Deposit(network.Heart, item, p);
		int left = item.IsAir ? 0 : item.stack;
		if (left < before)
		{
			Item piece = item.Clone();
			piece.stack = before - left;
			deposited.Add((piece, network.Position));
		}
	}

	// Shimmer through a network's Decrafting Interface (BookShimmer, docs/shimmer.md). The network must be one the player
	// reaches as for a pull and have a Decrafting Interface, and the item must shimmer now (BookShimmer.For): the client's
	// view is checked again here. Up to `amount` (whole units) comes out of that network, shimmers the way the liquid
	// does it (ShimmerItems) and the results go into the same network; what does not fit drops at the player's feet
	// (`spilled`). What was taken and not shimmered goes back where it came from.
	internal static Status Shimmer(int p, Point16 at, int type, int amount, List<Item> results, out int done, out int spilled)
	{
		done = spilled = 0;
		Client client = Of(p);
		if (!Admit(p, client, out var refusal))
		{
			return refusal;
		}
		if (!BookMagicStorage.Writable || !BookMagicStorage.Shimmers)
		{
			return Status.Off;
		}
		BookShimmer.Plan plan = BookShimmer.For(type);
		if (plan == null || !plan.Ready || amount <= 0 || amount > BookShimmer.MaxItems)
		{
			return Status.Invalid;
		}
		Player player = Main.player[p];
		List<BookMagicStorage.Network> networks = new List<BookMagicStorage.Network>();
		BookMagicStorage.Scan(player, BookNearbyConfig.Radius + BookSourceRules.SlackTiles, null, networks, null, out _);
		BookMagicStorage.Network network = networks.Find((BookMagicStorage.Network n) => n.Position == at);
		if (network == null || !BookMagicStorage.HasDecrafting(network.Heart))
		{
			return Status.Invalid;
		}
		amount -= amount % plan.Unit;
		List<(Item Item, Origin From)> taken = new List<(Item, Origin)>();
		int remaining = amount;
		List<Item> made = new List<Item>();
		try
		{
			TakeFromHeart(p, network.Heart, new Entry(type, AnyPrefix, amount), ref remaining, taken);
			int got = amount - remaining;
			done = ShimmerItems(player, plan, type, got - got % plan.Unit, made);
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Shimmer from the book failed; everything taken went back.", error);
			Restore(taken);
			done = 0;
			return Status.Invalid;
		}
		ReturnUnused(taken, taken.Sum(((Item Item, Origin From) t) => t.Item.stack) - done);
		List<(Item, Point16)> deposited = new List<(Item, Point16)>();
		foreach (Item item in made)
		{
			results.Add(item.Clone());
			try
			{
				DepositInto(p, network, item, deposited);
			}
			catch (Exception error)
			{
				ModContent.GetInstance<ModernRecipeBook>().Logger.Error($"Shimmer result {item.Name} ×{item.stack} did not go into storage; it drops at the player.", error);
			}
			if (!item.IsAir)
			{
				spilled += item.stack;
				player.QuickSpawnItem(new EntitySource_Misc("ModernRecipeBook:Shimmer"), item, item.stack);
			}
		}
		SafeReport(p, taken, deposited);
		return (done == 0) ? Status.Nothing : ((done < amount) ? Status.Partial : Status.Done);
	}

	// The pieces taken past `count` items go back to their network, the last ones first (TakeFromHeart takes plain items
	// first, so a reforged one is what goes back); `taken` keeps only what was used, for the audit.
	private static void ReturnUnused(List<(Item Item, Origin From)> taken, int count)
	{
		List<(Item Item, Origin From)> back = new List<(Item, Origin)>();
		for (int i = taken.Count - 1; i >= 0 && count > 0; i--)
		{
			(Item item, Origin from) = taken[i];
			if (item.stack <= count)
			{
				count -= item.stack;
				back.Add((item, from));
				taken.RemoveAt(i);
				continue;
			}
			Item piece = item.Clone();
			piece.stack = count;
			item.stack -= count;
			back.Add((piece, from));
			count = 0;
		}
		Restore(back);
	}

	// Item.GetShimmered for `n` of `type` (whole units), at the player where vanilla uses the item's own spot
	// (TML:Terraria/Item.cs:49645-49850): coin luck, the item it turns into, a decraft's ingredients (or the recipe's own
	// shimmer results, less what the recipe's ConsumeIngredient keeps back), the rainbow Town Slime, released critters.
	// Item results go into `made`; returns how many items it used.
	private static int ShimmerItems(Player player, BookShimmer.Plan plan, int type, int n, List<Item> made)
	{
		if (n <= 0)
		{
			return 0;
		}
		Vector2 center = player.Center;
		EntitySource_Misc source = new EntitySource_Misc("ModernRecipeBook:Shimmer");
		switch (plan.What)
		{
		case BookShimmer.Kind.CoinLuck:
		{
			int luck = n * plan.CoinLuck;
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				player.AddCoinLuck(center, luck);
			}
			NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 1, (int)center.X, (int)center.Y, luck);
			return n;
		}
		case BookShimmer.Kind.Transform:
			AddStacks(made, plan.Results[0].Type, n);
			return n;
		case BookShimmer.Kind.Decraft:
		{
			Recipe recipe = Main.recipe[plan.Recipe];
			int crafts = n / plan.Unit;
			foreach (Item ingredient in (IEnumerable<Item>)(recipe.customShimmerResults ?? recipe.requiredItem))
			{
				if (ingredient.type <= 0)
				{
					break;
				}
				int amount = crafts * ingredient.stack;
				RecipeLoader.ConsumeIngredient(recipe, ingredient.type, ref amount, isDecrafting: true);
				AddStacks(made, ingredient.type, amount);
			}
			return crafts * plan.Unit;
		}
		case BookShimmer.Kind.TownSlime:
		{
			if (NPC.unlockedSlimeRainbowSpawn)
			{
				return 0;
			}
			NPC.unlockedSlimeRainbowSpawn = true;
			NetMessage.SendData(MessageID.WorldData);
			int npc = NPC.NewNPC(source, (int)center.X + 4, (int)center.Y, NPCID.TownSlimeRainbow);
			if (npc >= 0 && npc < Main.maxNPCs)
			{
				Main.npc[npc].netUpdate = true;
				Main.npc[npc].shimmerTransparency = 1f;
				NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 2, npc);
			}
			WorldGen.CheckAchievement_RealEstateAndTownSlimes();
			return 1;
		}
		case BookShimmer.Kind.Critter:
		{
			Item sample = ContentSamples.ItemsByType[type];
			int shimmered = NPCID.Sets.ShimmerTransformToNPC[sample.makeNPC];
			int tries = 50;
			int slots = NPC.GetAvailableAmountOfNPCsToSpawnUpToSlot(n, 200);
			int released = 0;
			while (tries > 0 && slots > 0 && released < n)
			{
				tries--;
				slots--;
				released++;
				int npc = (shimmered < 0) ? NPC.ReleaseNPC((int)center.X, (int)player.Bottom.Y, sample.makeNPC, sample.placeStyle, Main.myPlayer) : NPC.ReleaseNPC((int)center.X, (int)player.Bottom.Y, shimmered, 0, Main.myPlayer);
				if (npc >= 0)
				{
					Main.npc[npc].shimmerTransparency = 1f;
					NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 2, npc);
				}
			}
			return released;
		}
		default:
			return 0;
		}
	}

	// `amount` of `type` as whole stacks.
	private static void AddStacks(List<Item> made, int type, int amount)
	{
		int maxStack = Math.Max(1, ContentSamples.ItemsByType[type].maxStack);
		while (amount > 0)
		{
			int n = Math.Min(amount, maxStack);
			made.Add(new Item(type, n));
			amount -= n;
		}
	}

	// Stand (server harness): a pull for player p whose reply is never sent, as if the client vanished before it
	// arrived; and a burst of them, for the rate limit.
	internal static string TestPull(int p, int type, int amount, int prefix)
	{
		List<Item> items = new List<Item>();
		Status status = Pull(p, -1000 - Of(p).InFlight.Count, new List<Entry> { new Entry(type, prefix, amount) }, items);
		return $"{status} got={items.Sum((Item i) => i.stack)} inflight={Of(p).InFlight.Count}";
	}

	internal static string TestBurst(int p, int n, int type)
	{
		Dictionary<Status, int> counts = new Dictionary<Status, int>();
		for (int i = 0; i < n; i++)
		{
			List<Item> items = new List<Item>();
			Status status = Pull(p, -2000 - i, new List<Entry> { new Entry(type, AnyPrefix, 1) }, items);
			counts[status] = counts.GetValueOrDefault(status) + 1;
			Ack(p, -2000 - i);
			foreach (Item item in items)
			{
				Main.player[p].QuickSpawnItem(new EntitySource_Misc("ModernRecipeBook:Test"), item, item.stack);
			}
		}
		return string.Join(" ", counts.Select((KeyValuePair<Status, int> c) => c.Key + "=" + c.Value));
	}

	// Stand: a deposit for player p whose leftover reply is never sent; and how many of a type lie on the ground.
	internal static string TestDeposit(int p, int type, int stack)
	{
		List<Item> leftovers = new List<Item>();
		Status status = Deposit(p, -3000 - Of(p).InFlight.Count, new List<Item> { new Item(type, stack) }, leftovers);
		return $"{status} left={leftovers.Sum((Item i) => i.stack)} inflight={Of(p).InFlight.Count}";
	}

	internal static int OnGround(int type)
	{
		int count = 0;
		foreach (Item item in Main.item)
		{
			if (item.active && item.type == type)
			{
				count += item.stack;
			}
		}
		return count;
	}

	internal static int InFlight(int p)
	{
		return Clients[p]?.InFlight.Count ?? 0;
	}

	internal static bool Subscribed(int p)
	{
		return Clients[p]?.Subscribed ?? false;
	}

	internal static void Ack(int p, int id)
	{
		Clients[p]?.InFlight.RemoveAll((Delivery d) => d.Id == id);
	}

	// tML swallows exceptions from HandlePacket (ModNet.HandleModPacket): a reply that cannot be sent puts what it
	// carried back at once instead of leaving it in flight while the player stays.
	private static void Reply(int p, int id, Status status, List<Item> items)
	{
		try
		{
			SendGive(p, id, status, items);
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Nearby reply failed; its items went back.", error);
			Client client = Clients[p];
			if (client != null)
			{
				foreach (Delivery delivery in client.InFlight.Where((Delivery d) => d.Id == id).ToList())
				{
					Restore(delivery.Items);
					client.InFlight.Remove(delivery);
				}
			}
		}
	}

	// Back where it came from: the same chest if it is still there and nobody has it open (a player's open chest is their
	// client's copy, and their next change to a slot overwrites the server's; vanilla quick stack skips those too,
	// TML:Terraria/Chest.cs:400-403, 708-721), the same network, else on the ground at that spot. `closing`: the world
	// is being saved for good, no client edits a chest after it, and items on the ground are not saved, so an open chest
	// takes them too.
	private static void Restore(List<(Item Item, Origin From)> items, bool closing = false)
	{
		foreach ((Item item, Origin from) in items)
		{
			try
			{
				Vector2 spot;
				if (from.Chest == -2)
				{
					spot = new Point16(from.X, from.Y).ToWorldCoordinates();
				}
				else if (from.Chest >= 0)
				{
					Chest chest = Main.chest[from.Chest];
					spot = BookSourceRules.ChestCenter(from.X, from.Y, dresser: false);
					if (chest != null && chest.x == from.X && chest.y == from.Y && (closing || Chest.UsingChest(from.Chest) < 0))
					{
						PutInChest(chest, item);
					}
				}
				else
				{
					spot = from.Heart.ToWorldCoordinates(16f, 16f);
					object heart = BookMagicStorage.Writable ? BookMagicStorage.HeartAt(from.Heart) : null;
					if (heart != null)
					{
						BookMagicStorage.Deposit(heart, item, -1);
					}
				}
				if (!item.IsAir)
				{
					Item.NewItem(new EntitySource_Misc("ModernRecipeBook:Restore"), (int)spot.X - 8, (int)spot.Y - 8, 16, 16, item);
				}
			}
			catch (Exception error)
			{
				ModContent.GetInstance<ModernRecipeBook>().Logger.Error($"Could not restore {item?.Name} ×{item?.stack}.", error);
			}
		}
	}

	private static void PutInChest(Chest chest, Item item)
	{
		foreach (Item slot in chest.item)
		{
			if (item.IsAir)
			{
				return;
			}
			if (slot != null && !slot.IsAir && slot.type == item.type && slot.stack < slot.maxStack && ItemLoader.CanStack(slot, item))
			{
				ItemLoader.TryStackItems(slot, item, out _);
				if (item.stack <= 0)
				{
					item.TurnToAir();
				}
			}
		}
		for (int k = 0; k < chest.item.Length && !item.IsAir; k++)
		{
			if (chest.item[k] == null || chest.item[k].IsAir)
			{
				chest.item[k] = item.Clone();
				item.TurnToAir();
			}
		}
	}

	// Packets (multiplayer only; single player calls the functions above directly).
	internal static ModPacket NewPacket(BookMessage type)
	{
		ModPacket packet = ModContent.GetInstance<ModernRecipeBook>().GetPacket();
		packet.Write((byte)type);
		return packet;
	}

	private static void SendChests(int p, byte flags, int locked, int inUse, List<BookSources.MirrorChest> upserts, List<int> removes)
	{
		// Chunks stay well under the 65,535-byte packet limit; only the first carries Reset.
		int at = 0;
		do
		{
			ModPacket packet = NewPacket(BookMessage.NearbyChestSync);
			packet.Write((at == 0) ? flags : (byte)(flags & ~1));
			packet.Write((ushort)locked);
			packet.Write((ushort)inUse);
			int count = Math.Min(200, (upserts?.Count ?? 0) - at);
			packet.Write((ushort)Math.Max(0, count));
			for (int i = 0; i < count; i++)
			{
				BookSources.MirrorChest chest = upserts[at + i];
				packet.Write((short)chest.Index);
				packet.Write(chest.X);
				packet.Write(chest.Y);
				packet.Write((byte)chest.Used);
				packet.Write((byte)chest.Entries.Length);
				foreach (BookSources.Stock stock in chest.Entries)
				{
					packet.Write7BitEncodedInt(stock.Type);
					packet.Write7BitEncodedInt(stock.Prefix);
					packet.Write7BitEncodedInt(stock.Count);
				}
			}
			at += Math.Max(0, count);
			bool last = at >= (upserts?.Count ?? 0);
			List<int> gone = last ? removes : null;
			packet.Write((ushort)(gone?.Count ?? 0));
			if (gone != null)
			{
				foreach (int index in gone)
				{
					packet.Write((short)index);
				}
			}
			packet.Send(p);
			if (last)
			{
				break;
			}
		}
		while (true);
	}

	private static void SendGive(int p, int id, Status status, List<Item> items)
	{
		ModPacket packet = NewPacket(BookMessage.NearbyGive);
		packet.Write(id);
		packet.Write((byte)status);
		packet.Write((ushort)items.Count);
		foreach (Item item in items)
		{
			ItemIO.Send(item, packet, writeStack: true);
		}
		packet.Send(p);
	}

	// What each message cost on the side that received it: count and bytes, payload plus tML's 4-byte header (length,
	// message 250, mod id). Read on the stand: c call p=ModernRecipeBook.Storage.BookNearbyServer.Traffic
	private const int Kinds = BookMessage.NearbyShimmered - BookMessage.NearbySubscribe + 1;

	internal static readonly int[] Received = new int[Kinds];

	internal static readonly long[] ReceivedBytes = new long[Kinds];

	internal static string Traffic()
	{
		return string.Join(" ", Enumerable.Range(0, Kinds).Select((int i) => $"{(BookMessage)((int)BookMessage.NearbySubscribe + i)}={Received[i]}/{ReceivedBytes[i]}B"));
	}

	internal static string ResetTraffic()
	{
		Array.Clear(Received);
		Array.Clear(ReceivedBytes);
		return "ok";
	}

	// From ModernRecipeBook.HandlePacket, which has read the message byte.
	internal static void Receive(BookMessage type, BinaryReader reader, int whoAmI)
	{
		long start = reader.BaseStream.Position - 1;
		try
		{
			Handle(type, reader, whoAmI);
		}
		finally
		{
			int kind = type - BookMessage.NearbySubscribe;
			Received[kind]++;
			ReceivedBytes[kind] += reader.BaseStream.Position - start + 4;
		}
	}

	private static void Handle(BookMessage type, BinaryReader reader, int whoAmI)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
		{
			BookSources.HandlePacket(type, reader);
			return;
		}
		if (whoAmI < 0 || whoAmI >= 255)
		{
			return;
		}
		switch (type)
		{
		case BookMessage.NearbySubscribe:
			Subscribe(whoAmI, reader.ReadBoolean());
			break;
		case BookMessage.NearbyPull:
		{
			int id = reader.ReadInt32();
			int n = reader.ReadByte();
			List<Entry> entries = new List<Entry>(n);
			for (int i = 0; i < n; i++)
			{
				entries.Add(new Entry(reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt() - 1, reader.Read7BitEncodedInt()));
			}
			List<Item> items = new List<Item>();
			Status status = Pull(whoAmI, id, entries, items);
			Reply(whoAmI, id, status, items);
			break;
		}
		case BookMessage.NearbyDeposit:
		{
			int id = reader.ReadInt32();
			int n = reader.ReadByte();
			List<Item> items = new List<Item>(n);
			for (int i = 0; i < n; i++)
			{
				items.Add(ItemIO.Receive(reader, readStack: true));
			}
			List<Item> leftovers = new List<Item>();
			Status status = Deposit(whoAmI, id, items, leftovers);
			Reply(whoAmI, id, status, leftovers);
			break;
		}
		case BookMessage.NearbyAck:
			Ack(whoAmI, reader.ReadInt32());
			break;
		case BookMessage.NearbyQuickStack:
		{
			int n = Math.Min((int)reader.ReadByte(), MaxQuickStackSlots);
			List<int> slots = new List<int>(n);
			for (int i = 0; i < n; i++)
			{
				slots.Add(reader.ReadInt16());
			}
			Status status = QuickStack(whoAmI, slots, out int moved, out int remote);
			ModPacket packet = NewPacket(BookMessage.NearbyStacked);
			packet.Write((byte)status);
			packet.Write7BitEncodedInt(moved);
			packet.Write7BitEncodedInt(remote);
			packet.Send(whoAmI);
			break;
		}
		case BookMessage.NearbyShimmer:
		{
			Point16 at = new Point16(reader.ReadInt16(), reader.ReadInt16());
			int item = reader.Read7BitEncodedInt();
			int amount = reader.Read7BitEncodedInt();
			List<Item> results = new List<Item>();
			Status status = Shimmer(whoAmI, at, item, amount, results, out int done, out int spilled);
			BookShimmer.SendResult(whoAmI, status, item, done, results, spilled);
			break;
		}
		}
	}

	// The main inventory's 40 slots and a Void Bag's 40.
	internal const int MaxQuickStackSlots = 80;

	// The Storage tab's Quick Stack, after vanilla's own (docs/nearby-storage.md §4.10). Vanilla's quick-stack rules on
	// the given slots, which only the main inventory (10..49) and a Void Bag in use may be (Stackable). An item goes only
	// into a counted network that already holds its type, nearest first; what does not fit stays in its slot, with no
	// fallback to another network or a chest. Networks are found as for a pull, the carried Portable Access checked here
	// again. Like Chest.ServerPlaceItem, it works on the server's copy of the inventory and sends every given slot back,
	// changed or not: the client locked those slots and takes the server's copy of each while locked.
	internal static Status QuickStack(int p, IReadOnlyList<int> slots, out int moved, out int remote)
	{
		moved = remote = 0;
		Player player = Main.player[p];
		bool bag = player.useVoidBag();
		Status status = Status.Invalid;
		List<(Item, Point16)> deposited = new List<(Item, Point16)>();
		if (Admit(p, Of(p), out Status refusal) && BookMagicStorage.Writable)
		{
			List<BookMagicStorage.Network> networks = new List<BookMagicStorage.Network>();
			BookMagicStorage.Scan(player, BookNearbyConfig.Radius + BookSourceRules.SlackTiles, null, networks, null, out _);
			networks.Sort((BookMagicStorage.Network a, BookMagicStorage.Network b) => a.Distance.CompareTo(b.Distance));
			List<HashSet<int>> holds = new List<HashSet<int>>(networks.Count);
			List<Item> stored = new List<Item>();
			foreach (BookMagicStorage.Network network in networks)
			{
				stored.Clear();
				BookMagicStorage.CollectItems(network.Heart, stored);
				holds.Add(new HashSet<int>(stored.Select((Item i) => i.type)));
			}
			try
			{
				foreach (int slot in slots)
				{
					Item item = QuickStackSlot(player, slot);
					if (!Stackable(item) || (slot >= PlayerItemSlotID.Bank4_0 && !bag))
					{
						continue;
					}
					for (int n = 0; n < networks.Count && !item.IsAir; n++)
					{
						if (!holds[n].Contains(item.type))
						{
							continue;
						}
						int before = item.stack;
						DepositInto(p, networks[n], item, deposited);
						int got = before - (item.IsAir ? 0 : item.stack);
						moved += got;
						if (networks[n].Remote)
						{
							remote += got;
						}
					}
				}
			}
			catch (Exception error)
			{
				// What was placed stays placed; the slots below are sent as they are now, so nothing is lost or doubled.
				ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Nearby quick stack failed part way.", error);
			}
			SafeReport(p, new List<(Item, Origin)>(), deposited);
			status = (moved == 0) ? Status.Nothing : Status.Done;
		}
		else
		{
			status = BookMagicStorage.Writable ? refusal : Status.Off;
		}
		if (Main.netMode == NetmodeID.Server)
		{
			foreach (int slot in slots)
			{
				Item item = QuickStackSlot(player, slot);
				if (item != null)
				{
					NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, p, slot, item.prefix);
				}
			}
		}
		return status;
	}

	// Vanilla's quick-stack rule (no favorite, no coin, TML:Terraria/Player.cs:20179-20201), and never a Magic Storage
	// locator or the Void Bag itself: the bag gone, the server would take its Void Vault slots for unused
	// (Player.useVoidBag), and the client's lock on them (disableVoidBag) would wait for slots that never come back.
	internal static bool Stackable(Item item)
	{
		return item != null && !item.IsAir && !item.favorited && !item.IsACoin && item.type != ItemID.VoidLens && !BookMagicStorage.IsLocator(item);
	}

	// The item a quick-stack slot id names (PlayerItemSlotID), or null for any other slot. A Void Vault slot is named
	// whether or not the bag is carried, so it is always sent back.
	private static Item QuickStackSlot(Player player, int slot)
	{
		if (slot >= PlayerItemSlotID.Inventory0 + 10 && slot < PlayerItemSlotID.Inventory0 + 50)
		{
			return player.inventory[slot - PlayerItemSlotID.Inventory0];
		}
		if (slot >= PlayerItemSlotID.Bank4_0 && slot < PlayerItemSlotID.Bank4_0 + 40)
		{
			return player.bank4.item[slot - PlayerItemSlotID.Bank4_0];
		}
		return null;
	}
}
