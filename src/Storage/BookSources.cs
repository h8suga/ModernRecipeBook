using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace ModernRecipeBook.Storage;

// The book's one source set (docs/nearby-storage.md §2). It contains:
// - the inventory, the open container and a carried Void Bag;
// - banks within the radius;
// - unlocked world chests within the radius that nobody has open (the server's mirror);
// - every Magic Storage network with an access tile within the radius, and the one open in MS's window.
// Card counts, craftable, the tracker and the Storage tab all read one aggregate. The aggregate is rebuilt only
// when an input signature changes, never per frame.
public sealed class BookSources : ModSystem
{
	// The local player's view: a dedicated server never loads it.
	public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

	internal readonly record struct Stock(int Type, int Prefix, int Count);

	// Used: occupied slots of the chest's Chest.maxItems (the Entries are merged per kind and cannot tell).
	internal sealed record MirrorChest(int Index, short X, short Y, Stock[] Entries, int Used);

	private sealed class Tally
	{
		internal long Total;

		// What Recipe.Create itself consumes from: inventory, open container, Void Bag, modded providers.
		internal long Craft;

		// Banks within the radius; the book moves these into the inventory before a craft.
		internal long Banks;

		// World chests and Magic Storage; pulled through BookNearbyServer.
		internal long Remote;

		internal long Inventory;

		internal long Open;

		internal long Chests;

		internal long Storage;

		internal readonly long[] Bank = new long[4];

		internal int ChestCount;

		internal int StorageCount;

		internal int LastChest = int.MinValue;

		internal int LastStorage = -1;

		// A listed kind's part outside the remote containers (the open container, a carried Void Bag, the banks).
		internal long Local => Total - Remote - Inventory;

		internal void Reset()
		{
			Total = Craft = Banks = Remote = Inventory = Open = Chests = Storage = 0;
			Array.Clear(Bank);
			ChestCount = StorageCount = 0;
			LastChest = int.MinValue;
			LastStorage = -1;
		}

		// A stack in a local container: the open one (bank -1) or a bank (0..3, the Void Bag as the Void Vault).
		internal void AddLocal(long n, int bank, bool craft)
		{
			Total += n;
			if (craft)
			{
				Craft += n;
			}
			else
			{
				Banks += n;
			}
			if (bank < 0)
			{
				Open += n;
			}
			else
			{
				Bank[bank] += n;
			}
		}

		internal void AddChest(long n, int chest)
		{
			Total += n;
			Remote += n;
			Chests += n;
			if (LastChest != chest)
			{
				LastChest = chest;
				ChestCount++;
			}
		}

		internal void AddStorage(long n, int network)
		{
			Total += n;
			Remote += n;
			Storage += n;
			if (LastStorage != network)
			{
				LastStorage = network;
				StorageCount++;
			}
		}
	}

	private enum Purpose : byte
	{
		Take,
		Restock,
		Craft,
		Deposit
	}

	// Where taken items go: the inventory (Shift+click, Restock, a craft), the cursor (click, right-click, a deposit's
	// leftover) or the trash slot (Ctrl+click).
	private enum Sink : byte
	{
		Inventory,
		Cursor,
		Trash
	}

	private sealed class Pending
	{
		internal Purpose Purpose;

		internal int Age;

		internal long Asked;

		internal Recipe Recipe;

		internal int Batches;

		internal int Requested;

		internal Sink Sink;

		// A held right-click's batch.
		internal bool Held;

		// A deposit's coins (BankCoins).
		internal BankedCoins Coins;

		// A restock's amounts per type so far: what the local containers gave, before the server's part arrives.
		internal Dictionary<int, long> Topped;
	}

	// Where a deposit's coins went: no coins or no Piggy Bank, all into the Piggy Bank, or some on to storage because
	// the Piggy Bank filled up.
	private enum BankedCoins : byte
	{
		None,
		All,
		Full
	}

	private const int ScanTicks = 30;

	private const int LocalTicks = 10;

	private const int TimeoutTicks = 600;

	private const int StayTicks = 120;

	// Inputs.
	private static readonly Dictionary<int, MirrorChest> Mirror = new Dictionary<int, MirrorChest>();

	private static readonly List<int> Banks = new List<int>();

	private static readonly List<BookMagicStorage.Network> Networks = new List<BookMagicStorage.Network>();

	private static readonly List<List<Item>> NetworkItems = new List<List<Item>>();

	private static readonly List<bool> NetworkCreative = new List<bool>();

	private static readonly List<(int Total, int Used, int Inactive)> NetworkCells = new List<(int, int, int)>();

	// Cells of the other counted containers, set by Rebuild: [0] the open world chest, [1..4] Piggy Bank, Safe,
	// Defender's Forge, Void Vault or Bag; and the mirror's chests, summed.
	private static readonly int[] ContainerSlots = new int[5];

	private static readonly int[] ContainerUsed = new int[5];

	private static int MirrorChests;

	private static int MirrorSlots;

	private static int MirrorUsed;

	private static readonly List<List<Item>> Stations = new List<List<Item>>();

	private static int MirrorVersion;

	private static int ScanSignature;

	private static int LocalSignature;

	private static int AppliedInputs = int.MinValue;

	private static bool ScanDue = true;

	private static bool LocalDue = true;

	private static bool WasEnabled;

	private static bool Subscribed;

	private static int StayAround;

	private static int Ticks;

	// The aggregate.
	private static readonly Dictionary<int, Tally> Tallies = new Dictionary<int, Tally>();

	// The same parts per listed kind (the Storage tab's (type, prefix), containers outside the inventory only): a cell is
	// one prefix, and its breakdown adds up to it. Kept in KindPool between rebuilds, so a rebuild allocates nothing for
	// a kind seen before.
	private static readonly Dictionary<(int Type, int Prefix), Tally> Listed = new Dictionary<(int, int), Tally>();

	private static readonly Dictionary<(int Type, int Prefix), Tally> KindPool = new Dictionary<(int, int), Tally>();

	private static readonly Dictionary<(int Type, int Prefix), Item> Display = new Dictionary<(int, int), Item>();

	internal static readonly List<Item> Items = new List<Item>();

	private static readonly HashSet<Item> Seen = new HashSet<Item>(ReferenceEqualityComparer.Instance);

	internal static int Version { get; private set; }

	internal static int ItemsVersion { get; private set; }

	// A counted Creative Storage Unit: every type is there without end.
	internal static bool Infinite { get; private set; }

	internal static int SkippedLocked { get; private set; }

	internal static int SkippedInUse { get; private set; }

	internal static int SkippedPassword { get; private set; }

	// Subscribed in multiplayer and the first snapshot has not arrived; shown only after half a second.
	private static bool AwaitingSnapshot;

	private static int AwaitingTicks;

	internal static bool Syncing => AwaitingSnapshot && AwaitingTicks > 30;

	// The server keeps inventories itself (SSC): pulls cannot be delivered.
	internal static bool PullsOff { get; private set; }

	private static readonly Dictionary<int, Pending> Waiting = new Dictionary<int, Pending>();

	private static int NextId;

	// The last action's result, built when it is drawn (so it reads in the language shown now); it fades after five
	// seconds, like a toast.
	private static Func<string> MessageText;

	private static ulong MessageTick;

	internal static string Message => (MessageText != null && Main.GameUpdateCount - MessageTick < 300) ? MessageText() : "";

	// The items the result names ("Topped up: Torch +20", the Piggy Bank the coins went to): where the line shows one's
	// name, the Storage tab makes it a link to its card; a result about one item that words it otherwise (Russian declines
	// it, «монеты в копилке») is a link to it whole.
	internal static int[] MessageItems { get; private set; } = Array.Empty<int>();

	private static void Say(Func<string> text, int[] items = null)
	{
		MessageText = text;
		MessageItems = items ?? Array.Empty<int>();
		MessageTick = Main.GameUpdateCount;
	}

	// A piece of a line the Storage tab draws: its words, the item they name (0: none), which the tab makes a link to that
	// item's card, and what the link's hover explains above where its click goes.
	internal readonly record struct Piece(string Text, int Item = 0, string Tip = "");

	// Prefix -1: the whole type.
	private static (int Type, int Prefix, int Version, int Culture, string Text, Piece[][] Rows) BreakdownCache = (0, 0, -1, -1, "", Array.Empty<Piece[]>());

	// The details panel's texts, rebuilt only when what they show changed (they are read every frame).
	private static (int Version, int Culture, int Locked, int InUse, int Password, int Radius) TextsKey = (-1, -1, 0, 0, 0, 0);

	private static Piece[] SummaryCounts = Array.Empty<Piece>();

	private static int[] SummaryBankItems = Array.Empty<int>();

	private static Piece[] NotCountedLine = Array.Empty<Piece>();

	private static Piece[] RadiusPieces = Array.Empty<Piece>();


	internal static bool Enabled => BookNearbyConfig.Enabled && !PullsOff;

	// The stations of counted Crafting Interfaces, or null when the feature is off (vanilla stations only).
	internal static List<List<Item>> CraftStations => (Enabled && Stations.Count > 0) ? Stations : null;

	internal static bool HasStorage => Enabled && BookMagicStorage.Writable && ServerWritable && Networks.Count > 0;

	// The server's Magic Storage writes work (its mirror flags; always so in single player, where they are this game's).
	private static bool ServerWritable = true;

	// A take or restock in flight, or the inventory locked; deposits may overlap them (a click that swaps sends both).
	internal static bool Busy => Locked || Waiting.Values.Any((Pending p) => p.Purpose is Purpose.Take or Purpose.Restock);

	// On a client, vanilla's Quick Stack locks the slots it sent until the server sends each back, and while any is locked
	// the server's copy of a slot replaces the client's (TML:Terraria/MessageBuffer.cs:397-400, 523-537): an item put
	// into such a slot then would be overwritten and lost. Vanilla crafts nothing then (Main.cs:40540); the book takes
	// nothing and keeps the deliveries that arrive until the lock is over (Arrived).
	private static bool Locked => Main.LocalPlayer.HasLockedInventory();

	internal static bool CraftPending => AnyWaiting(craft: true, shownOnly: false);

	// Waits show only after half a second, so a quick reply never blinks.
	internal static bool CraftPendingShown => AnyWaiting(craft: true, shownOnly: true);

	internal static bool TakingShown => AnyWaiting(craft: false, shownOnly: true);

	private static bool AnyWaiting(bool craft, bool shownOnly)
	{
		foreach (Pending pending in Waiting.Values)
		{
			if ((pending.Purpose == Purpose.Craft) == craft && (!shownOnly || pending.Age > 30))
			{
				return true;
			}
		}
		return false;
	}

	public override void PostUpdateEverything()
	{
		if (Main.dedServ || Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active)
		{
			return;
		}
		Tick();
	}

	public override void OnWorldUnload()
	{
		// The Changed times go to the player's save data first (they are written with the next player save).
		if (!Main.dedServ && Main.LocalPlayer != null && Main.LocalPlayer.active)
		{
			Main.LocalPlayer.GetModPlayer<BookPlayer>().KeepChanged();
		}
		ChangedAt.Clear();
		ChangedWorld = null;
		Clear();
	}

	internal static void Clear()
	{
		Mirror.Clear();
		Banks.Clear();
		Networks.Clear();
		NetworkItems.Clear();
		NetworkCells.Clear();
		Stations.Clear();
		Carried.Clear();
		Tallies.Clear();
		Listed.Clear();
		Display.Clear();
		Items.Clear();
		Waiting.Clear();
		Arrived.Clear();
		HeldOwed = 0;
		ServerWritable = true;
		Subscribed = AwaitingSnapshot = PullsOff = Infinite = false;
		SkippedLocked = SkippedInUse = SkippedPassword = 0;
		StayAround = Ticks = AwaitingTicks = 0;
		ScanDue = LocalDue = true;
		AppliedInputs = int.MinValue;
		ListStale = true;
		HaveBaseline = false;
		LastTotals.Clear();
		MessageText = null;
		Version++;
		ItemsVersion++;
	}

	private static void Tick()
	{
		bool need = RecipeBookSystem.Open || RecipeBookSystem.QuickSearchOpen || RecipeBookSystem.HasTrackedGoals;
		bool enabled = BookNearbyConfig.Enabled;
		StayAround = (need && enabled) ? StayTicks : (StayAround - 1);
		SetSubscribed(enabled && StayAround > 0);
		if (AwaitingSnapshot)
		{
			AwaitingTicks++;
		}
		AgePending();
		FlushArrived();
		FlushQuickStack();
		FlushHeld();
		if (!need)
		{
			return;
		}
		Ticks++;
		if (enabled != WasEnabled)
		{
			WasEnabled = enabled;
			ScanDue = LocalDue = true;
		}
		Refresh(force: false);
	}

	// Brings the aggregate up to date. Scans run every 30 ticks, the local check every 10 ticks, and both run at once
	// when forced (the book opens, the book just moved items).
	internal static void Refresh(bool force)
	{
		if (Main.dedServ || Main.gameMenu)
		{
			return;
		}
		if (force || ScanDue || Ticks % ScanTicks == 0)
		{
			Scan();
		}
		if (force || LocalDue || Ticks % LocalTicks == 0)
		{
			CheckLocal();
		}
		int inputs = HashCode.Combine(LocalSignature, ScanSignature, MirrorVersion, Enabled, Main.LocalPlayer.chest);
		if (inputs != AppliedInputs || (ListStale && Listing))
		{
			AppliedInputs = inputs;
			Rebuild();
		}
	}

	// The Storage tab's list (Listed, Items) and the Changed times are built only while the book or the quick search is
	// open (its rows count and take stacks), or opening. With both closed the tracker is all that reads the aggregate,
	// and it needs the per-type totals alone; the list is built on the next open.
	private static bool Listing => RecipeBookSystem.Open || RecipeBookSystem.QuickSearchOpen || Opening;

	private static bool Opening;

	private static bool ListStale = true;

	internal static void OnBookOpened()
	{
		ScanDue = LocalDue = true;
		if (Enabled)
		{
			StayAround = StayTicks;
			SetSubscribed(on: true);
		}
		Opening = true;
		try
		{
			Refresh(force: true);
		}
		finally
		{
			Opening = false;
		}
	}

	private static void SetSubscribed(bool on)
	{
		if (on == Subscribed)
		{
			return;
		}
		Subscribed = on;
		if (!on)
		{
			Mirror.Clear();
			MirrorVersion++;
			SkippedLocked = SkippedInUse = 0;
			AwaitingSnapshot = false;
		}
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			BookNearbyServer.Subscribe(Main.myPlayer, on);
		}
		else
		{
			AwaitingSnapshot = on;
			AwaitingTicks = 0;
			ModPacket packet = BookNearbyServer.NewPacket(BookMessage.NearbySubscribe);
			packet.Write(on);
			packet.Send();
		}
	}

	private static void Scan()
	{
		ScanDue = false;
		Player player = Main.LocalPlayer;
		int password = 0;
		if (Enabled)
		{
			BookMagicStorage.Scan(player, BookNearbyConfig.Radius, Banks, Networks, Stations, out password, Carried);
		}
		else
		{
			Banks.Clear();
			Networks.Clear();
			Stations.Clear();
			Carried.Clear();
		}
		SkippedPassword = password;
		while (NetworkItems.Count < Networks.Count)
		{
			NetworkItems.Add(new List<Item>());
		}
		NetworkCreative.Clear();
		NetworkCells.Clear();
		int signature = HashCode.Combine(Networks.Count, Stations.Count);
		foreach (int bank in Banks)
		{
			signature = HashCode.Combine(signature, bank);
		}
		foreach ((int Type, bool Crafting, BookMagicStorage.CarriedSkip Why) skip in Carried)
		{
			signature = HashCode.Combine(signature, skip.Type, skip.Crafting, skip.Why);
		}
		for (int n = 0; n < Networks.Count; n++)
		{
			List<Item> items = NetworkItems[n];
			items.Clear();
			bool creative = BookMagicStorage.CollectItems(Networks[n].Heart, items);
			NetworkCreative.Add(creative);
			// A unit added, upgraded or switched off changes the cells without changing the items.
			(int Total, int Used, int Inactive) cells = BookMagicStorage.Cells(Networks[n].Heart);
			NetworkCells.Add(cells);
			signature = HashCode.Combine(signature, Networks[n].Position, Networks[n].Name, creative, items.Count, cells);
			signature = HashCode.Combine(signature, Networks[n].Nearby, Networks[n].ViaWindow, Networks[n].Carrier);
			foreach (Item item in items)
			{
				signature = HashCode.Combine(signature, item.type, item.prefix, item.stack);
			}
		}
		foreach (List<Item> list in Stations)
		{
			foreach (Item item in list)
			{
				signature = HashCode.Combine(signature, item?.type ?? 0);
			}
		}
		ScanSignature = signature;
	}

	private static void CheckLocal()
	{
		LocalDue = false;
		Player player = Main.LocalPlayer;
		int signature = HashCode.Combine(player.chest, player.useVoidBag());
		for (int i = 0; i < 58; i++)
		{
			signature = Fold(signature, player.inventory[i]);
		}
		foreach (Item[] items in Containers(player, craft: true, banks: true))
		{
			foreach (Item item in items)
			{
				signature = Fold(signature, item);
			}
		}
		foreach (var (items, _) in PlayerLoader.GetModdedCraftingMaterials(player))
		{
			foreach (Item item in items)
			{
				signature = Fold(signature, item);
			}
		}
		LocalSignature = signature;
	}

	private static int Fold(int signature, Item item)
	{
		return (item == null || item.IsAir) ? signature : HashCode.Combine(signature, item.type, item.prefix, item.stack);
	}

	// The containers the book reads besides the inventory. "craft": the ones Recipe.Create consumes from by itself (the
	// open container and a carried Void Bag); "banks": the banks within the radius that are not one of those.
	private static IEnumerable<Item[]> Containers(Player player, bool craft, bool banks)
	{
		bool bag = player.useVoidBag() && player.chest != -5;
		if (craft)
		{
			if (player.chest >= 0 && player.chest < Main.maxChests && Main.chest[player.chest] != null)
			{
				yield return Main.chest[player.chest].item;
			}
			else if (player.chest <= -2 && player.chest >= -5)
			{
				yield return BankItems(player, player.chest);
			}
			if (bag)
			{
				yield return player.bank4.item;
			}
		}
		if (!banks || !Enabled)
		{
			yield break;
		}
		foreach (int bank in Banks)
		{
			if (bank != player.chest && (bank != -5 || !bag))
			{
				yield return BankItems(player, bank);
			}
		}
	}

	private static Item[] BankItems(Player player, int bank)
	{
		return bank switch
		{
			-2 => player.bank.item,
			-3 => player.bank2.item,
			-4 => player.bank3.item,
			_ => player.bank4.item,
		};
	}

	// Tallies are kept between rebuilds and zeroed (one allocation per type a session, not per rebuild); a type no longer
	// anywhere stays at 0, which every reader takes for absent.
	private static Tally TallyOf(int type)
	{
		if (!Tallies.TryGetValue(type, out var tally))
		{
			tally = new Tally();
			Tallies[type] = tally;
		}
		return tally;
	}

	private static bool ListingNow;

	// The kind's tally, or null while not listing.
	private static Tally KindOf(int type, int prefix)
	{
		if (!ListingNow)
		{
			return null;
		}
		if (!Listed.TryGetValue((type, prefix), out var tally))
		{
			if (!KindPool.TryGetValue((type, prefix), out tally))
			{
				tally = new Tally();
				KindPool[(type, prefix)] = tally;
			}
			tally.Reset();
			Listed[(type, prefix)] = tally;
		}
		return tally;
	}

	private static void Rebuild()
	{
		foreach (Tally tally in Tallies.Values)
		{
			tally.Reset();
		}
		ListingNow = Listing;
		Listed.Clear();
		Seen.Clear();
		Infinite = false;
		Array.Clear(ContainerSlots);
		Array.Clear(ContainerUsed);
		MirrorChests = MirrorSlots = MirrorUsed = 0;
		Player player = Main.LocalPlayer;
		bool bag = player.useVoidBag() && player.chest != -5;
		for (int i = 0; i < 58; i++)
		{
			Item item = player.inventory[i];
			if (item != null && !item.IsAir && Seen.Add(item))
			{
				Tally tally = TallyOf(item.type);
				tally.Total += item.stack;
				tally.Craft += item.stack;
				tally.Inventory += item.stack;
			}
		}
		// The open container: a world chest reads "Open chest", a bank its own name.
		if (player.chest >= 0 && player.chest < Main.maxChests && Main.chest[player.chest] != null)
		{
			AddContainer(Main.chest[player.chest].item, -1, craft: true);
		}
		else if (player.chest <= -2 && player.chest >= -5)
		{
			AddContainer(BankItems(player, player.chest), -2 - player.chest, craft: true);
		}
		if (bag)
		{
			AddContainer(player.bank4.item, 3, craft: true);
		}
		foreach (var (items, _) in PlayerLoader.GetModdedCraftingMaterials(player))
		{
			foreach (Item item in items)
			{
				if (item != null && !item.IsAir && Seen.Add(item))
				{
					Tally tally = TallyOf(item.type);
					tally.Total += item.stack;
					tally.Craft += item.stack;
				}
			}
		}
		if (Enabled)
		{
			foreach (int bank in Banks)
			{
				if (bank != player.chest && (bank != -5 || !bag))
				{
					AddContainer(BankItems(player, bank), -2 - bank, craft: false);
				}
			}
			foreach (KeyValuePair<int, MirrorChest> pair in Mirror)
			{
				if (pair.Key == player.chest)
				{
					continue;
				}
				MirrorChests++;
				MirrorSlots += Chest.maxItems;
				MirrorUsed += pair.Value.Used;
				foreach (Stock stock in pair.Value.Entries)
				{
					TallyOf(stock.Type).AddChest(stock.Count, pair.Key);
					KindOf(stock.Type, stock.Prefix)?.AddChest(stock.Count, pair.Key);
				}
			}
			for (int n = 0; n < Networks.Count; n++)
			{
				foreach (Item item in NetworkItems[n])
				{
					TallyOf(item.type).AddStorage(item.stack, n);
					KindOf(item.type, item.prefix)?.AddStorage(item.stack, n);
				}
			}
			Infinite = NetworkCreative.Contains(true);
		}
		if (ListingNow)
		{
			TrackChanges(SourceSet(player, bag));
			RebuildItems();
		}
		ListStale = !ListingNow;
		Version++;
	}

	// When each listed kind (the Storage tab's (type, prefix)) first appeared or its total last changed, in UTC ticks;
	// for the Changed sort (docs/storage-changed.md). Found by comparing totals at each rebuild, whatever moved them:
	// a deposit, a take, a craft, another player. A rebuild whose containers differ from the last one (the player walked
	// to another base, a network finished loading, a chest was opened) only takes a new baseline: what appears was not
	// changed, what leaves was not taken, and its time stays for when it is back. The very first rebuild is such a
	// baseline too, so the kinds there before have no time and sort as oldest.
	private static readonly Dictionary<(int Type, int Prefix), long> ChangedAt = new Dictionary<(int, int), long>();

	private static readonly Dictionary<(int Type, int Prefix), long> LastTotals = new Dictionary<(int, int), long>();

	private static int LastSources;

	private static bool HaveBaseline;

	// The containers in the set, the same whatever order the mirror and the scan list them in.
	private static int SourceSet(Player player, bool bag)
	{
		int set = HashCode.Combine(player.chest, bag, Enabled);
		if (!Enabled)
		{
			return set;
		}
		foreach (int bank in Banks)
		{
			set ^= HashCode.Combine(bank, 1);
		}
		foreach (int index in Mirror.Keys)
		{
			set ^= HashCode.Combine(index, 2);
		}
		foreach (BookMagicStorage.Network network in Networks)
		{
			set ^= HashCode.Combine(network.Position, 3);
		}
		return set;
	}

	private static void TrackChanges(int sources)
	{
		if (HaveBaseline && sources == LastSources)
		{
			long now = DateTime.UtcNow.Ticks;
			foreach (KeyValuePair<(int Type, int Prefix), Tally> pair in Listed)
			{
				if (!LastTotals.TryGetValue(pair.Key, out long before) || before != pair.Value.Total)
				{
					ChangedAt[pair.Key] = now;
				}
			}
			foreach ((int Type, int Prefix) kind in LastTotals.Keys)
			{
				if (!Listed.ContainsKey(kind))
				{
					ChangedAt.Remove(kind);
				}
			}
		}
		HaveBaseline = true;
		LastSources = sources;
		LastTotals.Clear();
		foreach (KeyValuePair<(int Type, int Prefix), Tally> pair in Listed)
		{
			LastTotals[pair.Key] = pair.Value.Total;
		}
	}

	// The Changed sort's key: seconds since 2000, 0 for a kind with no time (before tracking), which then sorts oldest.
	internal static int ChangedRank(Item item)
	{
		return (item != null && ChangedAt.TryGetValue((item.type, item.prefix), out long ticks)) ? (int)Math.Clamp((ticks - ChangedEpoch) / TimeSpan.TicksPerSecond, 1L, int.MaxValue - 1) : 0;
	}

	private static readonly long ChangedEpoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

	internal static bool TryGetChanged(Item item, out long ticks)
	{
		ticks = 0;
		return item != null && ChangedAt.TryGetValue((item.type, item.prefix), out ticks);
	}

	// Saved with the player per world (BookPlayer): "item|prefix|ticks" with stable names (vanilla ItemID / PrefixID
	// names, a mod's FullName), newest first, at most MaxChanged.
	internal const int MaxChanged = 2000;

	internal static List<string> SnapshotChanged()
	{
		List<string> lines = new List<string>(Math.Min(ChangedAt.Count, MaxChanged));
		foreach (KeyValuePair<(int Type, int Prefix), long> pair in ChangedAt.OrderByDescending((KeyValuePair<(int Type, int Prefix), long> p) => p.Value).Take(MaxChanged))
		{
			string item = (pair.Key.Type < ItemID.Count) ? ItemID.Search.GetName(pair.Key.Type) : ItemLoader.GetItem(pair.Key.Type)?.FullName;
			string prefix = (pair.Key.Prefix == 0) ? "" : ((pair.Key.Prefix < PrefixID.Count) ? PrefixID.Search.GetName(pair.Key.Prefix) : PrefixLoader.GetPrefix(pair.Key.Prefix)?.FullName);
			if (item != null && prefix != null)
			{
				lines.Add(item + "|" + prefix + "|" + pair.Value);
			}
		}
		return lines;
	}

	// The world the times belong to: its unique id, which a multiplayer client gets from the server.
	internal static string WorldKey => Main.ActiveWorldFileData?.UniqueId.ToString() ?? "";

	internal static string ChangedWorld { get; private set; }

	// On entering a world; an item or prefix whose mod is gone is dropped.
	internal static void LoadChanged(IEnumerable<string> lines)
	{
		ChangedWorld = WorldKey;
		ChangedAt.Clear();
		LastTotals.Clear();
		HaveBaseline = false;
		foreach (string line in lines ?? Array.Empty<string>())
		{
			string[] parts = line.Split('|');
			if (parts.Length != 3 || !long.TryParse(parts[2], out long ticks))
			{
				continue;
			}
			int type = ItemID.Search.TryGetId(parts[0], out int id) ? id : (ModContent.TryFind(parts[0], out ModItem modItem) ? modItem.Type : 0);
			int prefix = (parts[1].Length == 0) ? 0 : (PrefixID.Search.TryGetId(parts[1], out int p) ? p : (ModContent.TryFind(parts[1], out ModPrefix modPrefix) ? modPrefix.Type : -1));
			if (type > 0 && prefix >= 0)
			{
				ChangedAt[(type, prefix)] = ticks;
			}
		}
		ItemsVersion++;
	}

	private static void AddContainer(Item[] items, int bank, bool craft)
	{
		ContainerSlots[bank + 1] += items.Length;
		foreach (Item item in items)
		{
			if (item == null || item.IsAir)
			{
				continue;
			}
			ContainerUsed[bank + 1]++;
			if (!Seen.Add(item))
			{
				continue;
			}
			TallyOf(item.type).AddLocal(item.stack, bank, craft);
			KindOf(item.type, item.prefix)?.AddLocal(item.stack, bank, craft);
		}
	}

	// One display Item per (type, prefix) outside the inventory, reused between rebuilds; stack = the total.
	private static void RebuildItems()
	{
		Items.Clear();
		foreach (KeyValuePair<(int Type, int Prefix), Tally> pair in Listed)
		{
			if (!Display.TryGetValue(pair.Key, out var item))
			{
				item = new Item(pair.Key.Type, 1, pair.Key.Prefix);
				Display[pair.Key] = item;
			}
			item.stack = (int)Math.Min(int.MaxValue, pair.Value.Total);
			Items.Add(item);
		}
		// The grid sorts again in its own order (RecipeBookSystem.ApplyFilters); ties there fall to this order, by type
		// and then prefix, which is all a name sort here added. Integer keys: a name sort took a millisecond.
		Items.Sort((Item a, Item b) => (a.type != b.type) ? a.type.CompareTo(b.type) : a.prefix.CompareTo(b.prefix));
		ItemsVersion++;
	}

	// Reads (no allocation; called from drawing code).
	internal static int Quantity(HashSet<int> types)
	{
		if (Infinite && types.Count > 0)
		{
			return int.MaxValue;
		}
		long total = 0;
		foreach (int type in types)
		{
			if (Tallies.TryGetValue(type, out var tally))
			{
				total += tally.Total;
			}
		}
		return (int)Math.Min(int.MaxValue, total);
	}

	internal static int Quantity(int type)
	{
		if (Infinite)
		{
			return int.MaxValue;
		}
		return Tallies.TryGetValue(type, out var tally) ? (int)Math.Min(int.MaxValue, tally.Total) : 0;
	}

	internal static bool IsInfinite(HashSet<int> types)
	{
		return Infinite && types.Count > 0;
	}

	internal static bool IsInfinite(int type)
	{
		return Infinite && type > 0;
	}

	internal static Dictionary<int, int> CountsFor(IEnumerable<int> types)
	{
		Dictionary<int, int> counts = new Dictionary<int, int>();
		foreach (int type in types)
		{
			counts[type] = Quantity(type);
		}
		return counts;
	}

	// Per layer, for the craft plan: what Recipe.Create takes by itself, the banks to move, the remote to pull.
	internal static IReadOnlyDictionary<int, long>[] Layers(IEnumerable<int> types)
	{
		Dictionary<int, long> craft = new Dictionary<int, long>();
		Dictionary<int, long> banks = new Dictionary<int, long>();
		Dictionary<int, long> remote = new Dictionary<int, long>();
		foreach (int type in types)
		{
			if (Tallies.TryGetValue(type, out var tally))
			{
				craft[type] = tally.Craft;
				banks[type] = tally.Banks;
				remote[type] = tally.Remote;
			}
			if (Infinite)
			{
				remote[type] = long.MaxValue;
			}
		}
		return new IReadOnlyDictionary<int, long>[3] { craft, banks, remote };
	}

	// "Inventory: 12 · Open chest: 40 · Piggy Bank: 5 · Chests ×3: 120 · Magic Storage: 40", any prefix: card and
	// material rows, where every prefix counts.
	internal static string Breakdown(int type)
	{
		return BuildBreakdown(type, -1);
	}

	// The same for one Storage cell, its exact prefix; the inventory's row counts that prefix too.
	internal static string Breakdown(Item kind)
	{
		return BuildBreakdown(kind.type, kind.prefix);
	}

	private static string BuildBreakdown(int type, int prefix)
	{
		int culture = Culture;
		if (BreakdownCache.Type == type && BreakdownCache.Prefix == prefix && BreakdownCache.Version == Version && BreakdownCache.Culture == culture)
		{
			return BreakdownCache.Text;
		}
		List<Piece[]> parts = new List<Piece[]>();
		Tally tally;
		long inventory;
		if (prefix < 0)
		{
			Tallies.TryGetValue(type, out tally);
			inventory = tally?.Inventory ?? 0;
		}
		else
		{
			Listed.TryGetValue((type, prefix), out tally);
			inventory = 0;
			for (int i = 0; i < 58; i++)
			{
				Item item = Main.LocalPlayer.inventory[i];
				if (item != null && !item.IsAir && item.type == type && item.prefix == prefix)
				{
					inventory += item.stack;
				}
			}
		}
		// A bank's row and Magic Storage's start with the item they are (a link on the Storage tab); the inventory, the open
		// chest and the chests are no one item.
		if (inventory > 0)
		{
			parts.Add(new[] { new Piece(L("Storage.Inventory", Count(inventory))) });
		}
		if (tally != null)
		{
			if (tally.Open > 0)
			{
				parts.Add(new[] { new Piece(L("Storage.OpenChest", Count(tally.Open))) });
			}
			for (int bank = 0; bank < 4; bank++)
			{
				if (tally.Bank[bank] > 0)
				{
					parts.Add(new[] { new Piece(BankName(bank), BankItem(bank)), new Piece(": " + Count(tally.Bank[bank])) });
				}
			}
			if (tally.Chests > 0)
			{
				parts.Add(new[] { new Piece((tally.ChestCount > 1) ? L("Storage.ChestsTimes", tally.ChestCount, Count(tally.Chests)) : L("Storage.ChestsCount", Count(tally.Chests))) });
			}
			if (tally.Storage > 0 && !Infinite)
			{
				parts.Add(new[] { new Piece("Magic Storage", BookMagicStorage.HeartItem), new Piece(((tally.StorageCount > 1) ? (" ×" + tally.StorageCount) : "") + ": " + Count(tally.Storage)) });
			}
		}
		if (Infinite)
		{
			parts.Add(new[] { new Piece("Magic Storage", BookMagicStorage.HeartItem), new Piece(": ∞") });
		}
		string text = string.Join(" · ", parts.Select((Piece[] row) => string.Concat(row.Select((Piece p) => p.Text))));
		BreakdownCache = (type, prefix, Version, culture, text, parts.ToArray());
		return text;
	}

	// The cell's, one source per row (the details panel).
	internal static Piece[][] BreakdownRows(Item kind)
	{
		Breakdown(kind);
		return BreakdownCache.Rows;
	}

	private static void UpdateTexts()
	{
		var key = (Version, Culture, SkippedLocked, SkippedInUse, SkippedPassword, BookNearbyConfig.Radius);
		if (key == TextsKey)
		{
			return;
		}
		TextsKey = key;
		SummaryBankItems = ShownBanks.Select((int bank) => BankItem(-2 - bank)).ToArray();
		SummaryCounts = BuildSummary();
		NotCountedLine = BuildNotCounted();
		NotCountedTipText = BuildNotCountedTip();
		RadiusPieces = BuildRadiusLine();
		BuildCells();
	}

	private static string Count(long value)
	{
		return value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
	}

	private static string BankName(int bank)
	{
		return Lang.GetItemNameValue(BankItem(bank));
	}

	// A bank (0..3) as the item that names it; a carried Void Bag reads as the Void Vault it opens.
	private static int BankItem(int bank)
	{
		return bank switch
		{
			0 => ItemID.PiggyBank,
			1 => ItemID.Safe,
			2 => ItemID.DefendersForge,
			_ => ItemID.VoidVault,
		};
	}

	private static int ChestsShown
	{
		get
		{
			int chests = Mirror.Count;
			if (Mirror.ContainsKey(Main.LocalPlayer.chest))
			{
				chests--;
			}
			return chests;
		}
	}

	// The banks within the radius besides the open one and a carried Void Bag (those read as the inventory), -2..-5.
	private static IEnumerable<int> ShownBanks
	{
		get
		{
			Player player = Main.LocalPlayer;
			bool bag = player.useVoidBag() && player.chest != -5;
			return Banks.Where((int bank) => bank != player.chest && (bank != -5 || !bag));
		}
	}

	private static int BanksShown => ShownBanks.Count();

	// The summary: "7 chests · 2 storages", then the banks by name (SummaryBanks: the Storage tab lists them after it,
	// "Piggy Bank, Safe", as many as fit and "+N"), or "Nothing nearby". The storages are a link to the Storage Heart's
	// card with the networks named in its hover; each bank, to its own card.
	internal static Piece[] SummaryPieces
	{
		get
		{
			UpdateTexts();
			return SummaryCounts;
		}
	}

	internal static int[] SummaryBanks
	{
		get
		{
			UpdateTexts();
			return SummaryBankItems;
		}
	}

	private static Piece[] BuildSummary()
	{
		List<Piece> parts = new List<Piece>();
		if (ChestsShown > 0)
		{
			parts.Add(new Piece(L("Storage.CountChests", ChestsShown)));
		}
		// A network reached from afar (a carried Portable Access, Magic Storage's own window) is not "nearby": it is
		// counted apart, so the line never claims it sits within the radius.
		int remote = Networks.Count((BookMagicStorage.Network n) => n.Remote);
		if (Networks.Count - remote > 0)
		{
			parts.Add(new Piece(L("Storage.CountStorages", Networks.Count - remote), BookMagicStorage.HeartItem, NetworksLine));
		}
		if (remote > 0)
		{
			parts.Add(new Piece(L("Storage.CountRemoteStorages", remote), BookMagicStorage.HeartItem, NetworksLine));
		}
		if (parts.Count == 0 && SummaryBankItems.Length == 0)
		{
			return new[] { new Piece(L("Storage.NothingNearby")) };
		}
		return parts.SelectMany((Piece part, int i) => (i == 0) ? new[] { part } : new[] { new Piece(" · "), part }).ToArray();
	}

	internal static string SummaryTip
	{
		get
		{
			List<string> lines = new List<string>();
			List<string> banks = ShownBanks.Select((int bank) => BankName(-2 - bank)).ToList();
			if (banks.Count > 0)
			{
				lines.Add(string.Join(", ", banks));
			}
			if (Networks.Count > 0)
			{
				lines.Add(NetworksLine);
			}
			return string.Join("\n", lines);
		}
	}

	// "Magic Storage: Base, Network 2 (via Portable Access)".
	private static string NetworksLine => "Magic Storage: " + string.Join(", ", Networks.Select((BookMagicStorage.Network n) => NetworkLabel(n) + (n.Remote ? " (" + RemoteHow(n) + ")" : "")));

	// A network's name, or "Network N" counted left to right, then top to bottom.
	private static string NetworkLabel(BookMagicStorage.Network network)
	{
		if (network.Name.Length > 0)
		{
			return network.Name;
		}
		int place = 1;
		foreach (BookMagicStorage.Network other in Networks)
		{
			if (other.Position.X < network.Position.X || (other.Position.X == network.Position.X && other.Position.Y < network.Position.Y))
			{
				place++;
			}
		}
		return L("Storage.Network", place);
	}

	// "Cells: 1,234 / 3,200": the slots of every counted container besides the inventory (Magic Storage units, chests,
	// banks, the open container, a carried Void Bag) and how many hold something. Built with the other texts, once per
	// aggregate rebuild.
	private static string CellsText = "";

	private static string CellsTipText = "";

	internal static string Cells
	{
		get
		{
			UpdateTexts();
			return CellsText;
		}
	}

	internal static string CellsTip
	{
		get
		{
			UpdateTexts();
			return CellsTipText;
		}
	}

	// 90% full or more: the line turns Caution.
	internal static bool CellsAlmostFull { get; private set; }

	private static string Of(long used, long total)
	{
		return L("Storage.UsedOfTotal", Count(used), Count(total));
	}

	private static void BuildCells()
	{
		List<string> rows = new List<string>();
		long total = 0;
		long used = 0;
		int inactive = 0;
		for (int n = 0; n < Networks.Count && n < NetworkCells.Count; n++)
		{
			(int Total, int Used, int Inactive) cells = NetworkCells[n];
			total += cells.Total;
			used += cells.Used;
			inactive += cells.Inactive;
			rows.Add("Magic Storage · " + (Networks[n].Remote ? L("Storage.RemoteNetwork", NetworkLabel(Networks[n])) : NetworkLabel(Networks[n])) + ": " + Of(cells.Used, cells.Total));
		}
		if (MirrorChests > 0)
		{
			total += MirrorSlots;
			used += MirrorUsed;
			rows.Add((MirrorChests > 1) ? L("Storage.ChestsTimes", MirrorChests, Of(MirrorUsed, MirrorSlots)) : L("Storage.ChestsCount", Of(MirrorUsed, MirrorSlots)));
		}
		for (int i = 0; i < ContainerSlots.Length; i++)
		{
			if (ContainerSlots[i] > 0)
			{
				total += ContainerSlots[i];
				used += ContainerUsed[i];
				rows.Add(((i == 0) ? L("Storage.OpenChest2") : BankName(i - 1)) + ": " + Of(ContainerUsed[i], ContainerSlots[i]));
			}
		}
		if (inactive > 0)
		{
			rows.Add(L("Storage.NotCounted", L("Storage.CountInactiveUnits", inactive)));
		}
		if (Infinite)
		{
			rows.Add(L("Storage.UnlimitedNotInTheTotal", BookMagicStorage.CreativeUnitName));
		}
		CellsAlmostFull = total > 0 && used * 10 >= total * 9;
		CellsText = (total == 0 && !Infinite) ? "" : L("Storage.Cells", Of(used, total));
		CellsTipText = string.Join("\n", rows);
	}

	internal static Piece[] RadiusLine
	{
		get
		{
			UpdateTexts();
			return RadiusPieces;
		}
	}

	private static Piece[] BuildRadiusLine()
	{
		// A far network is not within any radius, so the line says apart how it is reached; the owner read "Within 30
		// tiles" right next to it as false. Alone it reads "Remote via Portable Access"; with chests, banks or another
		// network counted in the radius too, "Within 30 tiles · storage via Portable Access".
		BookMagicStorage.Network far = Networks.FirstOrDefault((BookMagicStorage.Network n) => n.Remote);
		string within = L("Storage.WithinTiles", BookNearbyConfig.Radius);
		within = (Main.netMode == NetmodeID.MultiplayerClient) ? L("Storage.SetByTheHost", within) : within;
		if (far == null)
		{
			return new[] { new Piece(within) };
		}
		bool local = ChestsShown > 0 || BanksShown > 0 || Networks.Any((BookMagicStorage.Network n) => !n.Remote);
		string line = local ? L("StorageFix.WithinAndRemote", within, RemoteHow(far)) : L("Storage.RemoteHow", RemoteHow(far));
		// "via Portable Access": the item's name in it, in any case, is a link to the card of the one carried.
		string name = L("Storage.PortableAccess");
		int at = far.ViaCarried ? line.IndexOf(name, StringComparison.OrdinalIgnoreCase) : -1;
		if (at < 0)
		{
			return new[] { new Piece(line) };
		}
		return new[] { new Piece(line[..at]), new Piece(line.Substring(at, name.Length), far.Carrier), new Piece(line[(at + name.Length)..]) }.Where((Piece p) => p.Text.Length > 0).ToArray();
	}

	// "via Portable Access" / "via the Magic Storage window", for the tooltips.
	private static string RemoteHow(BookMagicStorage.Network network)
	{
		return network.ViaCarried ? L("Storage.ViaPortableAccess") : L("Storage.ViaTheMagicStorageWindow");
	}

	// "Not counted: 2 locked · 1 in use · Portable Access — out of range", or nothing when nothing was skipped. A carried
	// Portable Access is a link to its own item's card (the line names the kind, the card the tier), its hover the rule
	// its reason comes from.
	internal static Piece[] NotCounted
	{
		get
		{
			UpdateTexts();
			return NotCountedLine;
		}
	}

	private static Piece[] BuildNotCounted()
	{
		List<Piece[]> parts = new List<Piece[]>();
		if (SkippedLocked > 0)
		{
			parts.Add(new[] { new Piece(L("Storage.CountLocked", SkippedLocked)) });
		}
		if (SkippedInUse > 0)
		{
			parts.Add(new[] { new Piece(L("Storage.CountInUse", SkippedInUse)) });
		}
		if (SkippedPassword > 0)
		{
			parts.Add(new[] { new Piece(L("Storage.CountPassword", SkippedPassword)) });
		}
		foreach ((int Type, bool Crafting, BookMagicStorage.CarriedSkip Why) skip in Carried)
		{
			parts.Add(new[]
			{
				new Piece(skip.Crafting ? L("Storage.PortableCraftingAccess") : L("Storage.PortableAccess"), skip.Type, CarriedTip(skip.Why)),
				new Piece(" — " + skip.Why switch
				{
					BookMagicStorage.CarriedSkip.OutOfRange => L("Storage.OutOfRange"),
					BookMagicStorage.CarriedSkip.NoPylon => L("Storage.NoPylonNearTheStorage"),
					BookMagicStorage.CarriedSkip.Missing => L("Storage.StorageMissing"),
					_ => L("Storage.Loading"),
				})
			});
		}
		if (parts.Count == 0)
		{
			return Array.Empty<Piece>();
		}
		// The parts inside the whole phrase ("Not counted: {0}"), wherever the language puts them.
		string[] around = L("Storage.NotCounted", "\u0001").Split('\u0001');
		List<Piece> line = new List<Piece> { new Piece(around[0]) };
		for (int i = 0; i < parts.Count; i++)
		{
			if (i > 0)
			{
				line.Add(new Piece(" · "));
			}
			line.AddRange(parts[i]);
		}
		line.Add(new Piece(around[^1]));
		return line.Where((Piece p) => p.Text.Length > 0).ToArray();
	}

	// A carried Portable Access the last scan did not count: its item, whether it is a crafting one, why
	// (BookMagicStorage.AddCarried).
	private static readonly List<(int Type, bool Crafting, BookMagicStorage.CarriedSkip Why)> Carried = new List<(int, bool, BookMagicStorage.CarriedSkip)>();

	// The line's explanation, only for what it lists (built with it).
	private static string NotCountedTipText = "";

	internal static string NotCountedTip
	{
		get
		{
			UpdateTexts();
			return NotCountedTipText;
		}
	}

	// One line per reason the line names. A carried Portable Access is explained by why it did not count: its range rule
	// for out of range and no pylon, binding it again for a storage that is gone (MS binds one with a right-click on a
	// Storage Heart or, the crafting one, a Crafting Interface: MS:MagicStorage.Components/StorageHeart.cs:32-70,
	// CraftingAccess.cs:59), the wait for one still loading.
	private static string BuildNotCountedTip()
	{
		List<string> lines = new List<string>();
		if (SkippedLocked > 0 || SkippedInUse > 0)
		{
			lines.Add(L("Storage.LockedChestsAndChestsAnotherPlayer"));
		}
		if (SkippedPassword > 0)
		{
			lines.Add(L("Storage.AStorageWithAPasswordCounts"));
		}
		lines.AddRange(Carried.Select(((int Type, bool Crafting, BookMagicStorage.CarriedSkip Why) c) => CarriedTip(c.Why)).Distinct());
		return string.Join("\n", lines);
	}

	// What a skipped Portable Access's reason comes from, as MS decides it when the item is used (AddCarried mirrors
	// PortableAccess.DoOpenStorage and OpenStorage, MS:MagicStorage.Items/PortableAccess.cs:68-90, 150-170): out of range
	// and no pylon fail its range rule (MS says "PortableAccessOutOfRange", "PortableAccessNoPylons"); missing, the tile it
	// was bound to is gone ("PortableAccessMissing"); loading, a far location the server has not sent yet.
	private static string CarriedTip(BookMagicStorage.CarriedSkip why)
	{
		return why switch
		{
			BookMagicStorage.CarriedSkip.OutOfRange or BookMagicStorage.CarriedSkip.NoPylon => L("Storage.APortableAccessInYourInventory"),
			BookMagicStorage.CarriedSkip.Missing => L("StorageFix.PortableAccessMissingTip"),
			_ => L("StorageFix.PortableAccessLoadingTip"),
		};
	}

	// "From: inventory, 2 chests, Magic Storage" for a craft that needs more than the inventory has. The inventory is
	// named only when the craft takes something from it (the layer Recipe.Create consumes by itself).
	internal static string CraftSources(IReadOnlyDictionary<int, long> craft, IReadOnlyDictionary<int, long> banks, IReadOnlyDictionary<int, long> remote)
	{
		bool usesBanks = banks.Values.Any((long n) => n > 0);
		bool usesRemote = remote.Values.Any((long n) => n > 0);
		if (!usesBanks && !usesRemote)
		{
			return "";
		}
		List<string> parts = new List<string>();
		if (craft.Values.Any((long n) => n > 0))
		{
			parts.Add(L("Storage.Inventory2"));
		}
		if (usesBanks)
		{
			parts.Add(L("Storage.CountBanks", BanksShown));
		}
		if (usesRemote)
		{
			bool chests = false;
			bool storage = false;
			foreach (KeyValuePair<int, long> pair in remote)
			{
				if (pair.Value > 0 && Tallies.TryGetValue(pair.Key, out var tally))
				{
					chests |= tally.Chests > 0;
					storage |= tally.Storage > 0 || Infinite;
				}
			}
			int holding = chests ? ChestsHolding(remote) : 0;
			if (holding > 0)
			{
				parts.Add(L("Storage.CountChests", holding));
			}
			if (storage)
			{
				parts.Add("Magic Storage");
			}
		}
		return L("Storage.From", string.Join(", ", parts));
	}

	// Chests in the set that hold any of the types a craft pulls (the server takes from the nearest of them).
	private static int ChestsHolding(IReadOnlyDictionary<int, long> remote)
	{
		int count = 0;
		foreach (KeyValuePair<int, MirrorChest> pair in Mirror)
		{
			if (pair.Key == Main.LocalPlayer.chest)
			{
				continue;
			}
			foreach (Stock stock in pair.Value.Entries)
			{
				if (remote.GetValueOrDefault(stock.Type) > 0)
				{
					count++;
					break;
				}
			}
		}
		return count;
	}

	// Moves up to `amount` of a kind from the local containers to the sink. What does not fit stays where it was. A
	// world chest the player has open is synced the way Recipe.Create syncs it (msg 32 per slot).
	private static int MoveLocal(int type, int prefix, int amount, bool craft, bool banks, ref bool full, Sink sink = Sink.Inventory)
	{
		Player player = Main.LocalPlayer;
		int moved = 0;
		foreach (Item[] items in Containers(player, craft, banks).ToList())
		{
			bool worldChest = player.chest >= 0 && Main.chest[player.chest]?.item == items;
			for (int slot = 0; slot < items.Length && moved < amount; slot++)
			{
				Item item = items[slot];
				if (item == null || item.IsAir || item.type != type || (prefix != BookNearbyServer.AnyPrefix && item.prefix != prefix))
				{
					continue;
				}
				int n = Math.Min(amount - moved, item.stack);
				Item piece = item.Clone();
				piece.stack = n;
				item.stack -= n;
				Item rest = Put(piece, sink);
				if (!rest.IsAir)
				{
					item.stack += rest.stack;
					full = true;
				}
				moved += n - (rest.IsAir ? 0 : rest.stack);
				if (item.stack <= 0)
				{
					item.TurnToAir();
				}
				if (worldChest && Main.netMode == NetmodeID.MultiplayerClient)
				{
					NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, player.chest, slot);
				}
				if (full)
				{
					return moved;
				}
			}
		}
		return moved;
	}

	// Storage tab takes, a vanilla chest slot's gestures with the cell as the slot (Terraria.UI/ItemSlot.cs): the slot
	// holds min(total, maxStack). Local containers first, the rest from the server.
	private static void Take(Item shown, int want, Sink sink)
	{
		if (!Enabled || shown == null || shown.IsAir || want <= 0 || Busy)
		{
			return;
		}
		if (!Listed.TryGetValue((shown.type, shown.prefix), out var have))
		{
			Say(() => L("Storage.NoLongerNearby"));
			return;
		}
		want = (int)Math.Min(want, have.Total);
		bool full = false;
		int moved = MoveLocal(shown.type, shown.prefix, want, craft: true, banks: true, ref full, sink);
		int rest = want - moved;
		if (!full && rest > 0 && have.Remote > 0)
		{
			Send(Purpose.Take, new List<BookNearbyServer.Entry> { new BookNearbyServer.Entry(shown.type, shown.prefix, (int)Math.Min(rest, have.Remote)) }, want, moved, sink: sink);
		}
		else if (sink == Sink.Inventory)
		{
			Say(() => full ? L("Storage.InventoryFull") : ((moved > 0) ? L("Storage.TakenToInventory") : L("Storage.NoLongerNearby")));
		}
		else if (moved == 0)
		{
			Say(() => L("Storage.NoLongerNearby"));
		}
		Refresh(force: true);
	}

	private static int SlotStack(Item shown)
	{
		return Listed.TryGetValue((shown.type, shown.prefix), out var have) ? (int)Math.Min(Math.Max(1, shown.maxStack), have.Total) : 0;
	}

	// Shift+click (ItemSlot.OverrideLeftClick, cursorOverride 8): the slot's stack into the inventory, whatever the
	// cursor holds. Ctrl+Enter does the same for the selected cell.
	internal static void TakeToInventory(Item shown)
	{
		if (shown != null)
		{
			Take(shown, SlotStack(shown), Sink.Inventory);
		}
	}

	// Left click (ItemSlot.LeftClick, action 0): an empty cursor picks the slot's stack up; the same kind swaps or
	// merges by vanilla's rule (BookSourceRules.CursorAfterLeftClick); another item swaps: it goes into storage and the
	// clicked stack comes onto the cursor.
	internal static void LeftClick(Item shown)
	{
		if (!Enabled || shown == null || shown.IsAir || Busy)
		{
			return;
		}
		Item cursor = Main.mouseItem;
		int slot = SlotStack(shown);
		if (!cursor.IsAir && !SameKind(cursor, shown))
		{
			if (CanDeposit)
			{
				DepositCursor(leftoverToCursor: false);
				Take(shown, slot, Sink.Cursor);
			}
			else
			{
				DepositCursor();
			}
			return;
		}
		int now = cursor.IsAir ? 0 : cursor.stack;
		int after = BookSourceRules.CursorAfterLeftClick(slot, now, shown.maxStack);
		if (after > now)
		{
			Take(shown, after - now, Sink.Cursor);
			SoundEngine.PlaySound(in SoundID.Grab);
		}
		else if (after < now)
		{
			DepositCursor(now - after);
		}
	}

	// Ctrl+click with vanilla's quick trash on (ItemSlot.LeftClick_SellOrTrash, SellOrTrash): the slot's stack replaces
	// what is in the trash slot.
	internal static bool QuickTrashOn => ItemSlot.Options.DisableLeftShiftTrashCan && !ItemSlot.Options.DisableQuickTrash && Main.npcShop <= 0;

	internal static void Trash(Item shown)
	{
		if (shown != null && QuickTrashOn)
		{
			Take(shown, SlotStack(shown), Sink.Trash);
			SoundEngine.PlaySound(in SoundID.Grab);
		}
	}

	// Held right-click: vanilla's rhythm (ItemSlot.RightClick with Main.stackSplit) asks for one at a time. Local
	// containers give at once; what the server holds is asked for in batches, one batch in flight and at most every
	// HeldTicks, so holding never meets the server's rate limit (16 a second).
	private const int HeldTicks = 6;

	private static (int Type, int Prefix) HeldKind;

	private static int HeldOwed;

	private static ulong HeldSent;

	internal static bool TakeOne(Item shown)
	{
		if (!Enabled || Locked || shown == null || shown.IsAir || !Listed.TryGetValue((shown.type, shown.prefix), out var have))
		{
			return false;
		}
		Item cursor = Main.mouseItem;
		if (!cursor.IsAir && !SameKind(cursor, shown))
		{
			return false;
		}
		(int, int) kind = (shown.type, shown.prefix);
		long inFlight = Waiting.Values.Where((Pending p) => p.Held).Sum((Pending p) => p.Asked);
		if ((HeldOwed > 0 || inFlight > 0) && HeldKind != kind)
		{
			return false;
		}
		if ((cursor.IsAir ? 0 : cursor.stack) + HeldOwed + inFlight >= Math.Max(1, shown.maxStack))
		{
			return false;
		}
		if (have.Local > 0)
		{
			bool full = false;
			return MoveLocal(shown.type, shown.prefix, 1, craft: true, banks: true, ref full, Sink.Cursor) > 0;
		}
		if (HeldOwed + inFlight >= have.Remote)
		{
			return false;
		}
		HeldKind = kind;
		HeldOwed++;
		FlushHeld();
		return true;
	}

	private static void FlushHeld()
	{
		if (HeldOwed <= 0 || Waiting.Values.Any((Pending p) => p.Held) || Main.GameUpdateCount - HeldSent < HeldTicks)
		{
			return;
		}
		int n = HeldOwed;
		HeldOwed = 0;
		HeldSent = Main.GameUpdateCount;
		if (Enabled)
		{
			Send(Purpose.Take, new List<BookNearbyServer.Entry> { new BookNearbyServer.Entry(HeldKind.Type, HeldKind.Prefix, n) }, n, 0, sink: Sink.Cursor, held: true);
			Refresh(force: true);
		}
	}

	private static bool SameKind(Item a, Item b)
	{
		return a.type == b.type && a.prefix == b.prefix && ItemLoader.CanStack(a, b);
	}

	// Puts a taken stack where the gesture sends it; returns what did not fit (air when all of it did).
	private static Item Put(Item item, Sink sink)
	{
		Player player = Main.LocalPlayer;
		switch (sink)
		{
		case Sink.Cursor:
			if (Main.mouseItem.IsAir)
			{
				Main.mouseItem = item;
				// With the inventory closed vanilla would put the cursor item away at once.
				Main.playerInventory = true;
				return new Item();
			}
			if (SameKind(Main.mouseItem, item))
			{
				ItemLoader.StackItems(Main.mouseItem, item, out var _);
			}
			return (item.stack <= 0) ? new Item() : item;
		case Sink.Trash:
			// The first piece replaces what was in the trash, as vanilla's SellOrTrash does; the next pieces of the
			// same take (several chests) join it, never past maxStack, since a take is at most one stack.
			if (!player.trashItem.IsAir && SameKind(player.trashItem, item) && player.trashItem.stack + item.stack <= player.trashItem.maxStack)
			{
				ItemLoader.StackItems(player.trashItem, item, out var _);
			}
			else
			{
				player.trashItem = item;
			}
			return new Item();
		default:
			return player.GetItem(Main.myPlayer, item, GetItemSettings.InventoryUIToInventorySettingsShowAsNew);
		}
	}

	internal static string TakeHint => L("Storage.ClickTakeRightClickTakeOne");

	// The cell's other gestures: Ctrl+Click only while vanilla's quick trash is on (QuickTrashOn), Mouse3 the card.
	internal static string MoreHint => QuickTrashOn ? L("StorageFix.CellMoreWithTrash") : L("StorageFix.CellMore");

	// Tops up every partial inventory stack (coins excepted) from what is nearby, like Magic Storage's Restock.
	internal static void Restock()
	{
		if (!Enabled || Busy)
		{
			return;
		}
		Player player = Main.LocalPlayer;
		Dictionary<(int, int), int> deficits = new Dictionary<(int, int), int>();
		for (int i = 0; i < 58; i++)
		{
			Item item = player.inventory[i];
			if (item != null && !item.IsAir && item.maxStack > 1 && item.stack < item.maxStack && !ItemID.Sets.CommonCoin[item.type])
			{
				deficits[(item.type, item.prefix)] = deficits.GetValueOrDefault((item.type, item.prefix)) + item.maxStack - item.stack;
			}
		}
		List<BookNearbyServer.Entry> remote = new List<BookNearbyServer.Entry>();
		Dictionary<int, long> topped = new Dictionary<int, long>();
		bool full = false;
		foreach (KeyValuePair<(int, int), int> pair in deficits)
		{
			if (!Listed.TryGetValue(pair.Key, out var have))
			{
				continue;
			}
			int got = MoveLocal(pair.Key.Item1, pair.Key.Item2, pair.Value, craft: true, banks: true, ref full);
			Topped(topped, pair.Key.Item1, got);
			int rest = pair.Value - got;
			if (rest > 0 && have.Remote > 0 && remote.Count < BookNearbyServer.MaxEntries)
			{
				remote.Add(new BookNearbyServer.Entry(pair.Key.Item1, pair.Key.Item2, (int)Math.Min(rest, have.Remote)));
			}
		}
		if (remote.Count > 0)
		{
			Send(Purpose.Restock, remote, remote.Sum((BookNearbyServer.Entry e) => e.Amount), (int)topped.Values.Sum(), topped: topped);
		}
		else
		{
			Say(() => ToppedUp(topped), ToppedFirst(topped));
		}
		Refresh(force: true);
	}

	private static void Topped(Dictionary<int, long> topped, int type, long n)
	{
		if (n > 0)
		{
			topped[type] = topped.GetValueOrDefault(type) + n;
		}
	}

	// Restock tops every partial stack up to its max stack, as vanilla's (ChestUI.Restock,
	// TML:Terraria.UI/ChestUI.cs:907-1022) and Magic Storage's (StorageGUI.TryRestock, MS:MagicStorage/StorageGUI.cs:
	// 489-516) do. From a whole network that can be a lot, so the result says what came out, largest first:
	// "Topped up: Torch +1,454 · Glowstick +1,918 · …".
	private static string ToppedUp(Dictionary<int, long> topped)
	{
		if (topped.Count == 0)
		{
			return L("Storage.NothingToTopUp");
		}
		IEnumerable<string> parts = ToppedFirst(topped).Select((int type) => L("StorageFix.ItemPlus", Lang.GetItemNameValue(type), Count(topped[type])));
		return L("StorageFix.ToppedUp", string.Join(" · ", parts) + ((topped.Count > 3) ? " · …" : ""));
	}

	// The three the result names, the most first.
	private static int[] ToppedFirst(Dictionary<int, long> topped)
	{
		return topped.OrderByDescending((KeyValuePair<int, long> p) => p.Value).Take(3).Select((KeyValuePair<int, long> p) => p.Key).ToArray();
	}

	// The inventory's own Quick Stack to Nearby Chests (vanilla radius; Magic Storage extends it to the networks near the
	// player), then the same into every network the book counts, remote ones included (FlushQuickStack).
	internal static void QuickStack()
	{
		Player player = Main.LocalPlayer;
		if (player.HasLockedInventory())
		{
			return;
		}
		QuickStackBefore = StackSignature(player);
		player.QuickStackAllChests();
		ScanDue = LocalDue = true;
		QuickStackQueued = true;
		FlushQuickStack();
	}

	// Waits for vanilla's own round trip: on a client its slots stay locked (HasLockedInventory) until the server sent
	// each back, and a slot the server sends after that is ignored. Only then does the result say whether vanilla's
	// part moved anything: a client sends every eligible slot whether or not a chest is near
	// (TML:Terraria/Player.cs:20179-20201), so the lock alone tells nothing.
	private static bool QuickStackQueued;

	private static int QuickStackBefore;

	// Whether vanilla's part of the last Quick Stack moved anything (OnStacked).
	private static bool QuickStackVanillaMoved;

	// Vanilla quick stack's slots: the main inventory and a Void Bag in use.
	private static int StackSignature(Player player)
	{
		bool bag = player.useVoidBag();
		int signature = bag ? 1 : 0;
		for (int k = 10; k < 50; k++)
		{
			signature = Fold(signature, player.inventory[k]);
		}
		if (bag)
		{
			foreach (Item item in player.bank4.item)
			{
				signature = Fold(signature, item);
			}
		}
		return signature;
	}

	private static void FlushQuickStack()
	{
		Player player = Main.LocalPlayer;
		if (!QuickStackQueued || player.HasLockedInventory())
		{
			return;
		}
		QuickStackQueued = false;
		bool stacked = StackSignature(player) != QuickStackBefore;
		QuickStackVanillaMoved = stacked;
		Say(() => stacked ? L("Storage.StackedIntoNearbyChests") : L("StorageFix.NothingToStack"));
		if (!HasStorage)
		{
			return;
		}
		// The types the counted networks hold, as this side sees them; the server checks again on its own copy.
		HashSet<int> held = new HashSet<int>();
		foreach (List<Item> items in NetworkItems.Take(Networks.Count))
		{
			foreach (Item item in items)
			{
				held.Add(item.type);
			}
		}
		List<int> slots = new List<int>();
		for (int k = 10; k < 50; k++)
		{
			if (QuickStackable(player.inventory[k], held))
			{
				slots.Add(PlayerItemSlotID.Inventory0 + k);
			}
		}
		int lastBag = -1;
		if (player.useVoidBag())
		{
			for (int m = 0; m < 40; m++)
			{
				if (QuickStackable(player.bank4.item[m], held))
				{
					slots.Add(PlayerItemSlotID.Bank4_0 + m);
					lastBag = m;
				}
			}
		}
		if (slots.Count == 0)
		{
			return;
		}
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			BookNearbyServer.Status status = BookNearbyServer.QuickStack(Main.myPlayer, slots, out int moved, out int remote);
			OnStacked(status, moved, remote);
			return;
		}
		// Sent and locked as vanilla sends and locks a slot for quick stacking (TML:Terraria/Player.cs:20179-20201): the
		// server works on its copy, which a pickup this tick has not reached yet, and its copy of each comes back into it.
		foreach (int slot in slots)
		{
			Item item = (slot < PlayerItemSlotID.Inventory0 + 50) ? player.inventory[slot - PlayerItemSlotID.Inventory0] : player.bank4.item[slot - PlayerItemSlotID.Bank4_0];
			NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot, item.prefix);
			if (slot < PlayerItemSlotID.Inventory0 + 50)
			{
				player.inventoryChestStack[slot - PlayerItemSlotID.Inventory0] = true;
			}
		}
		if (lastBag >= 0)
		{
			player.disableVoidBag = lastBag;
		}
		ModPacket packet = BookNearbyServer.NewPacket(BookMessage.NearbyQuickStack);
		packet.Write((byte)slots.Count);
		foreach (int slot in slots)
		{
			packet.Write((short)slot);
		}
		packet.Send();
	}

	private static bool QuickStackable(Item item, HashSet<int> held)
	{
		return BookNearbyServer.Stackable(item) && held.Contains(item.type);
	}

	// "Stacked 12 items · 3 into remote storage". Only slots whose type a counted network holds were sent, so nothing
	// moved means no room there.
	private static void OnStacked(BookNearbyServer.Status status, int moved, int remote)
	{
		if (moved > 0)
		{
			// The count goes in as a number: a plural form of these lines ({^0:item;items}) parses it, and a formatted
			// "1,234" makes tML's LocalizedText.Format throw every time the line is drawn.
			Say(() => (remote == moved) ? L("Storage.StackedRemote", moved) : ((remote > 0) ? L("Storage.StackedSomeRemote", moved, Count(remote)) : L("Storage.StackedLocal", moved)));
		}
		else
		{
			bool stacked = QuickStackVanillaMoved;
			Say(() => status switch
			{
				BookNearbyServer.Status.Busy => L("Storage.TooFastWaitAMoment"),
				BookNearbyServer.Status.Off => L("Storage.NearbyStorageIsOff"),
				_ => stacked ? L("Storage.StackedIntoNearbyChestsNoRoom") : L("StorageFix.NoRoomInStorage"),
			});
		}
		ScanDue = LocalDue = true;
	}

	// The counted network whose access tile is nearest to the player.
	internal static BookMagicStorage.Network NearestNetwork
	{
		get
		{
			BookMagicStorage.Network best = null;
			foreach (BookMagicStorage.Network network in Networks)
			{
				if (best == null || network.Distance < best.Distance)
				{
					best = network;
				}
			}
			return best;
		}
	}

	internal static string NearestNetworkName
	{
		get
		{
			string name = NearestNetwork?.Name ?? "";
			return (name.Length > 0) ? name : "Magic Storage";
		}
	}

	// Anything besides the inventory is counted right now (so an empty Storage tab reads "empty", not "nothing").
	internal static bool AnySource
	{
		get
		{
			Player player = Main.LocalPlayer;
			return Enabled && (ChestsShown > 0 || Networks.Count > 0 || BanksShown > 0 || player.chest != -1 || player.useVoidBag());
		}
	}

	// Somewhere to put items: a chest or a Magic Storage network in the set.
	internal static bool CanDeposit => Enabled && (ChestsShown > 0 || HasStorage);

	// Somewhere to put coins: that, or the Piggy Bank (SendDeposit). With the bank alone only coins go in.
	internal static bool CanBankCoins => Enabled && PiggyBankAvailable;

	internal static bool CanDepositAll => CanDeposit || CanBankCoins;

	private static bool CanTake(Item item)
	{
		return CanDeposit || (CanBankCoins && ItemID.Sets.CommonCoin[item.type]);
	}

	internal static string DepositHint => L("Storage.ShiftClickAnInventoryItemOr");

	// Vanilla's chest shift-click (ItemSlot.OverrideHover / OverrideLeftClick): inventory, coin and ammo slots, never a
	// favorite. A player with a chest of their own open uses that chest, as in vanilla.
	internal static bool CanShiftDeposit(Player player, Item[] inventory, int context, int slot)
	{
		return player.whoAmI == Main.myPlayer && RecipeBookSystem.StorageTabOpen && player.chest == -1 && inventory == player.inventory && context is 0 or 1 or 2 && slot >= 0 && slot < inventory.Length && inventory[slot] != null && !inventory[slot].IsAir && !inventory[slot].favorited && CanTake(inventory[slot]);
	}

	// The whole stack, at once; what nothing takes goes back into its slot.
	internal static bool ShiftDeposit(Player player, Item[] inventory, int context, int slot)
	{
		if (!CanShiftDeposit(player, inventory, context, slot))
		{
			return false;
		}
		SendDeposit(new List<Item> { TakeOut(inventory, slot) }, Sink.Inventory, new int[1] { slot });
		SoundEngine.PlaySound(in SoundID.Grab);
		return true;
	}

	// The cursor stack dropped onto the Storage grid, as onto a chest slot. Like there, a favorite loses its mark
	// (ItemSlot.LeftClick clears it for container slots). `amount` leaves the rest on the cursor (a click that merges
	// into a small cell); a swap sends the leftover to the inventory, since the cursor gets the clicked stack.
	internal static void DepositCursor(int amount = int.MaxValue, bool leftoverToCursor = true)
	{
		if (Main.mouseItem.IsAir || amount <= 0)
		{
			return;
		}
		if (!CanTake(Main.mouseItem))
		{
			Say(() => Enabled ? L("Storage.NothingNearbyToDepositInto") : L("Storage.NearbyStorageIsOff"));
			return;
		}
		Item item = Main.mouseItem.Clone();
		item.favorited = false;
		item.stack = Math.Min(amount, Main.mouseItem.stack);
		Main.mouseItem.stack -= item.stack;
		if (Main.mouseItem.stack <= 0)
		{
			Main.mouseItem.TurnToAir();
		}
		SendDeposit(new List<Item> { item }, leftoverToCursor ? Sink.Cursor : Sink.Inventory);
		SoundEngine.PlaySound(in SoundID.Grab);
	}

	// Vanilla's chest Deposit All (ChestUI.DepositAll): slots 10..49 except favorites, and coins from the coin slots
	// only when a target already holds coins (ChestUI.MoveCoins). With the Piggy Bank available the coin slots go too,
	// for the bank only (SendDeposit's `wallet`), and with the bank alone only coins go. The hotbar (0..9) is never
	// touched, coins in it included: the owner asked for the first row to stay. Coins that go to storage are not
	// exchanged into higher ones. Magic Storage locators stay too: a carried Portable Access is how the book reaches a
	// far network, and putting it away would cut that network off in the same click.
	// only: one kind of item (the quick search's Deposit), by the same rules.
	internal static void DepositAll(int only = 0)
	{
		Player player = Main.LocalPlayer;
		if (!CanDepositAll || player.HasLockedInventory())
		{
			return;
		}
		bool held = false;
		for (int coin = ItemID.CopperCoin; coin <= ItemID.PlatinumCoin; coin++)
		{
			held |= Tallies.TryGetValue(coin, out var tally) && (tally.Chests > 0 || tally.Storage > 0);
		}
		bool bank = CanBankCoins;
		List<Item> items = new List<Item>();
		List<int> slots = new List<int>();
		long[] wallet = new long[4];
		bool others = false;
		// The coin slots first, so coins that come back go there first (SendDeposit's `home`).
		foreach (int k in Enumerable.Range(50, 8).Concat(Enumerable.Range(10, 40)))
		{
			Item item = player.inventory[k];
			if (item == null || item.IsAir || item.favorited || BookMagicStorage.IsLocator(item) || (only > 0 && item.type != only))
			{
				continue;
			}
			bool coin = ItemID.Sets.CommonCoin[item.type];
			if (k >= 50 && !(coin && (held || bank)))
			{
				continue;
			}
			if (!CanTake(item))
			{
				others = true;
				continue;
			}
			if (k >= 50 && !held)
			{
				wallet[item.type - ItemID.CopperCoin] += item.stack;
			}
			slots.Add(k);
			items.Add(TakeOut(player.inventory, k));
		}
		if (items.Count == 0)
		{
			Say(() => others ? L("Storage.NothingNearbyToDepositInto") : L("Storage.NothingToDeposit"));
			return;
		}
		SendDeposit(items, Sink.Inventory, slots, BookSourceRules.CoinValue(wallet), others);
		SoundEngine.PlaySound(in SoundID.Grab);
	}

	private static Item TakeOut(Item[] inventory, int slot)
	{
		Item item = inventory[slot].Clone();
		inventory[slot].TurnToAir();
		return item;
	}

	// As in Magic Storage, the items leave their slots when sent; what does not fit comes back (to the cursor for a
	// dropped stack). Deposits may overlap: nothing but the reply depends on an earlier one. The coins go to the Piggy
	// Bank first when it is available (owner); only the rest is sent. What may not be sent comes back at once, into
	// `home` (the emptied slots it came from) first: everything when only the bank takes items, and with the bank full
	// up to `wallet` copper of the coins, the coin slots' share that Deposit All took for the bank alone. `restStays`:
	// items were left behind.
	private static void SendDeposit(List<Item> items, Sink leftoverSink, IReadOnlyList<int> home = null, long wallet = 0, bool restStays = false)
	{
		Player player = Main.LocalPlayer;
		BankedCoins coins = BankCoins(player, items);
		List<Item> back = new List<Item>();
		if (!CanDeposit)
		{
			back.AddRange(items);
			items.Clear();
		}
		else if (wallet > 0 && coins == BankedCoins.Full)
		{
			long left = CoinsWorth(items);
			long keep = Math.Min(wallet, left);
			items.RemoveAll((Item item) => ItemID.Sets.CommonCoin[item.type]);
			AddCoins(items, left - keep);
			AddCoins(back, keep);
		}
		GiveBack(back, home, leftoverSink);
		if (items.Count == 0)
		{
			restStays |= back.Count > 0;
			Say(() => coins switch
			{
				BankedCoins.Full => L("Coins.BankFullRestStays"),
				BankedCoins.All => restStays ? L("Coins.BankedRestStays") : L("Coins.Banked"),
				_ => L("Storage.NothingNearbyToDepositInto"),
			}, (coins == BankedCoins.None) ? null : new int[] { ItemID.PiggyBank });
			Refresh(force: true);
			return;
		}
		int id = ++NextId;
		Waiting[id] = new Pending
		{
			Purpose = Purpose.Deposit,
			Asked = items.Sum((Item i) => (long)i.stack),
			Sink = leftoverSink,
			Coins = coins
		};
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			List<Item> leftovers = new List<Item>();
			BookNearbyServer.Status status = BookNearbyServer.Deposit(Main.myPlayer, id, items, leftovers);
			OnGive(id, status, leftovers);
			return;
		}
		// The items have left their slots: a packet that cannot be built or sent (past 65,535 bytes with large mod data,
		// TML:Terraria.ModLoader/ModPacket.cs:81-84) gives them back here instead of losing them.
		try
		{
			ModPacket packet = BookNearbyServer.NewPacket(BookMessage.NearbyDeposit);
			packet.Write(id);
			packet.Write((byte)items.Count);
			foreach (Item item in items)
			{
				ItemIO.Send(item, packet, writeStack: true);
			}
			packet.Send();
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Nearby deposit could not be sent; the items went back.", error);
			Waiting.Remove(id);
			GiveBack(items, home, leftoverSink);
			Say(() => L("StorageFix.DepositNotSent"));
		}
	}

	// The Piggy Bank is open, or one is within the radius: its tile, a Money Trough or Chester (Scan's banks).
	internal static bool PiggyBankAvailable => Main.LocalPlayer.chest == -2 || (Enabled && Banks.Contains(-2));

	// The common coins among `items` join the Piggy Bank's own coins, combined into the fewest stacks as
	// ChestUI.MoveCoins combines them (BookSourceRules.CombineCoins), in the bank's coin slots and then its empty ones.
	// What does not fit stays in `items` (as the fewest stacks too). The bank is the player's own data: changed here, on
	// the client, and synced by vanilla, which sends each changed bank slot (msg 5, TML:Terraria/Main.cs:17167-17184),
	// as MoveBanks relies on.
	private static BankedCoins BankCoins(Player player, List<Item> items)
	{
		long[] coins = new long[4];
		bool any = false;
		foreach (Item item in items)
		{
			if (ItemID.Sets.CommonCoin[item.type])
			{
				coins[item.type - ItemID.CopperCoin] += item.stack;
				any = true;
			}
		}
		if (!any || !PiggyBankAvailable)
		{
			return BankedCoins.None;
		}
		Item[] bank = player.bank.item;
		List<int> slots = new List<int>();
		for (int i = 0; i < bank.Length; i++)
		{
			if (bank[i] != null && !bank[i].IsAir && ItemID.Sets.CommonCoin[bank[i].type])
			{
				coins[bank[i].type - ItemID.CopperCoin] += bank[i].stack;
				slots.Add(i);
			}
		}
		for (int i = 0; i < bank.Length; i++)
		{
			if (bank[i] == null || bank[i].IsAir)
			{
				slots.Add(i);
			}
		}
		List<(int Kind, int Stack)> stacks = BookSourceRules.CombineCoins(coins, CoinMaxStacks, slots.Count, out long[] rest);
		// Each kind goes back into a slot that held it first, as MoveCoins puts it back, the others in slot order.
		Dictionary<int, int> held = slots.ToDictionary((int i) => i, (int i) => bank[i]?.type ?? 0);
		foreach (int i in slots)
		{
			bank[i] = new Item();
		}
		List<(int Kind, int Stack)> later = new List<(int, int)>();
		foreach ((int kind, int stack) in stacks)
		{
			int n = slots.FindIndex((int i) => held[i] == ItemID.CopperCoin + kind);
			if (n < 0)
			{
				later.Add((kind, stack));
				continue;
			}
			bank[slots[n]] = new Item(ItemID.CopperCoin + kind, stack);
			slots.RemoveAt(n);
		}
		for (int n = 0; n < later.Count; n++)
		{
			bank[slots[n]] = new Item(ItemID.CopperCoin + later[n].Kind, later[n].Stack);
		}
		items.RemoveAll((Item item) => ItemID.Sets.CommonCoin[item.type]);
		AddCoins(items, BookSourceRules.CoinValue(rest));
		return (BookSourceRules.CoinValue(rest) == 0) ? BankedCoins.All : BankedCoins.Full;
	}

	private static int[] CoinMaxStacks => new int[4]
	{
		ContentSamples.ItemsByType[ItemID.CopperCoin].maxStack,
		ContentSamples.ItemsByType[ItemID.SilverCoin].maxStack,
		ContentSamples.ItemsByType[ItemID.GoldCoin].maxStack,
		ContentSamples.ItemsByType[ItemID.PlatinumCoin].maxStack
	};

	// The common coins among `items`, in copper.
	private static long CoinsWorth(List<Item> items)
	{
		long[] coins = new long[4];
		foreach (Item item in items.Where((Item i) => ItemID.Sets.CommonCoin[i.type]))
		{
			coins[item.type - ItemID.CopperCoin] += item.stack;
		}
		return BookSourceRules.CoinValue(coins);
	}

	// `value` copper as the fewest coin stacks, highest first.
	private static void AddCoins(List<Item> into, long value)
	{
		foreach ((int kind, int stack) in BookSourceRules.CombineCoins(new long[4] { value, 0, 0, 0 }, CoinMaxStacks, int.MaxValue, out _))
		{
			into.Add(new Item(ItemID.CopperCoin + kind, stack));
		}
	}

	// Items a deposit hands back: into the emptied `home` slots in order, else to the sink, then the inventory, then
	// the player's feet, so nothing is lost. The coin slots take only coins.
	private static void GiveBack(List<Item> items, IReadOnlyList<int> home, Sink sink)
	{
		Player player = Main.LocalPlayer;
		int next = 0;
		foreach (Item item in items)
		{
			while (home != null && next < home.Count && (!player.inventory[home[next]].IsAir || (home[next] >= 50 && !ItemID.Sets.CommonCoin[item.type])))
			{
				next++;
			}
			if (sink == Sink.Inventory && home != null && next < home.Count)
			{
				player.inventory[home[next++]] = item;
				continue;
			}
			Item rest = Put(item, sink);
			if (!rest.IsAir && sink != Sink.Inventory)
			{
				rest = Put(rest, Sink.Inventory);
			}
			if (!rest.IsAir)
			{
				player.QuickSpawnItem(new EntitySource_Misc("ModernRecipeBook:Overflow"), rest, rest.stack);
			}
		}
	}

	// Magic Storage's own Deposit All (BookMagicStorage.DepositAllCoinsFirst): the coins it would take from rows 10..49
	// go to the Piggy Bank first; what does not fit goes back into their slots for MS. The bank list is scanned afresh,
	// since the book may be closed. Returns whether any coin moved.
	internal static bool BankInventoryCoins(Func<Item, bool> takes)
	{
		Player player = Main.LocalPlayer;
		Refresh(force: true);
		if (!PiggyBankAvailable || player.HasLockedInventory())
		{
			return false;
		}
		List<int> slots = new List<int>();
		for (int k = 10; k < 50; k++)
		{
			Item item = player.inventory[k];
			if (!item.IsAir && !item.favorited && ItemID.Sets.CommonCoin[item.type] && takes(item))
			{
				slots.Add(k);
			}
		}
		if (slots.Count == 0)
		{
			return false;
		}
		List<Item> coins = slots.Select((int k) => TakeOut(player.inventory, k)).ToList();
		BankCoins(player, coins);
		GiveBack(coins, slots, Sink.Inventory);
		Refresh(force: true);
		return true;
	}

	// Moves the banks' share of a craft into the inventory; returns false when the inventory filled up first.
	internal static bool MoveBanks(IReadOnlyDictionary<int, long> banks)
	{
		bool full = false;
		foreach (KeyValuePair<int, long> pair in banks)
		{
			if (pair.Value > 0)
			{
				int moved = MoveLocal(pair.Key, BookNearbyServer.AnyPrefix, (int)Math.Min(int.MaxValue, pair.Value), craft: false, banks: true, ref full);
				if (moved < pair.Value)
				{
					Refresh(force: true);
					return false;
				}
			}
		}
		Refresh(force: true);
		return true;
	}

	internal static void RequestCraft(Recipe recipe, int requested, int batches, IReadOnlyDictionary<int, long> remote)
	{
		List<BookNearbyServer.Entry> entries = new List<BookNearbyServer.Entry>();
		foreach (KeyValuePair<int, long> pair in remote)
		{
			if (pair.Value > 0)
			{
				entries.Add(new BookNearbyServer.Entry(pair.Key, BookNearbyServer.AnyPrefix, (int)Math.Min(999999L, pair.Value)));
			}
		}
		Send(Purpose.Craft, entries, entries.Sum((BookNearbyServer.Entry e) => (long)e.Amount), 0, recipe, batches, requested);
	}

	private static void Send(Purpose purpose, List<BookNearbyServer.Entry> entries, long asked, int movedLocally, Recipe recipe = null, int batches = 0, int requested = 0, Sink sink = Sink.Inventory, bool held = false, Dictionary<int, long> topped = null)
	{
		int id = ++NextId;
		Waiting[id] = new Pending
		{
			Purpose = purpose,
			Asked = asked,
			Recipe = recipe,
			Batches = batches,
			Requested = requested,
			Sink = sink,
			Held = held,
			Topped = topped
		};
		LocallyMoved = movedLocally;
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			List<Item> items = new List<Item>();
			BookNearbyServer.Status status = BookNearbyServer.Pull(Main.myPlayer, id, entries, items);
			OnGive(id, status, items);
			return;
		}
		ModPacket packet = BookNearbyServer.NewPacket(BookMessage.NearbyPull);
		packet.Write(id);
		packet.Write((byte)entries.Count);
		foreach (BookNearbyServer.Entry entry in entries)
		{
			packet.Write7BitEncodedInt(entry.Type);
			packet.Write7BitEncodedInt(entry.Prefix + 1);
			packet.Write7BitEncodedInt(entry.Amount);
		}
		packet.Send();
	}

	private static int LocallyMoved;

	// What the server (or the single-player game) took for us, or what did not fit in storage.
	internal static void OnGive(int id, BookNearbyServer.Status status, List<Item> items)
	{
		Waiting.Remove(id, out var pending);
		Player player = Main.LocalPlayer;
		long got = 0;
		bool spilled = false;
		try
		{
			foreach (Item item in items)
			{
				if (item == null || item.IsAir)
				{
					continue;
				}
				got += item.stack;
				// Counted before it is put away: GetItem empties the stack it places.
				if (pending?.Topped != null)
				{
					Topped(pending.Topped, item.type, item.stack);
				}
				Item rest = item;
				if (pending != null && pending.Sink != Sink.Inventory)
				{
					rest = Put(item, pending.Sink);
					if (rest.IsAir)
					{
						continue;
					}
				}
				try
				{
					rest = player.GetItem(Main.myPlayer, rest, GetItemSettings.InventoryUIToInventorySettingsShowAsNew);
				}
				catch (Exception error)
				{
					// A mod's pickup hook failed: the stack lands at the player's feet rather than nowhere.
					ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Could not put " + item.Name + " into the inventory.", error);
				}
				if (rest.IsAir)
				{
					continue;
				}
				spilled = true;
				if (Main.mouseItem.IsAir)
				{
					Main.mouseItem = rest;
					Main.playerInventory = true;
				}
				else
				{
					player.QuickSpawnItem(new EntitySource_Misc("ModernRecipeBook:Overflow"), rest, rest.stack);
				}
			}
		}
		finally
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket packet = BookNearbyServer.NewPacket(BookMessage.NearbyAck);
				packet.Write(id);
				packet.Send();
			}
		}
		Refresh(force: true);
		if (pending == null)
		{
			return;
		}
		switch (pending.Purpose)
		{
		case Purpose.Take when pending.Sink != Sink.Inventory:
			// Onto the cursor or into the trash, as in vanilla: the result is on screen, only a failure is said.
			if (got == 0)
			{
				Say(() => status switch
				{
					BookNearbyServer.Status.Off => L("Storage.NearbyStorageIsOff"),
					BookNearbyServer.Status.Busy => L("Storage.TooFastWaitAMoment"),
					_ => L("Storage.NoLongerNearby"),
				});
			}
			break;
		case Purpose.Take:
		{
			long total = got + LocallyMoved;
			long asked = pending.Asked + LocallyMoved;
			Say(() => status switch
			{
				BookNearbyServer.Status.Off => L("Storage.NearbyStorageIsOff"),
				BookNearbyServer.Status.Busy => L("Storage.TooFastWaitAMoment"),
				_ => spilled ? L("Storage.InventoryFullRestOnCursor") : ((total == 0) ? L("Storage.NoLongerNearby") : ((status == BookNearbyServer.Status.Partial) ? L("Storage.TookOfRestGone", total, asked) : L("Storage.TakenToInventory"))),
			});
			break;
		}
		case Purpose.Restock:
		{
			Dictionary<int, long> topped = pending.Topped ?? new Dictionary<int, long>();
			Say(() => (status == BookNearbyServer.Status.Busy && topped.Count == 0) ? L("Storage.TooFastWaitAMoment") : ToppedUp(topped), ToppedFirst(topped));
			break;
		}
		case Purpose.Deposit:
		{
			BankedCoins coins = pending.Coins;
			Say(() => status switch
			{
				BookNearbyServer.Status.Off => L("Storage.NearbyStorageIsOff"),
				BookNearbyServer.Status.Busy => L("Storage.TooFastWaitAMoment"),
				_ when got == 0 => coins switch
				{
					BankedCoins.All => L("Coins.StoredBanked"),
					BankedCoins.Full => L("Coins.StoredBankFull"),
					_ => L("Storage.StoredNearby"),
				},
				_ => coins switch
				{
					BankedCoins.All => L("Coins.LeftBanked", Count(got)),
					BankedCoins.Full => L("Coins.LeftBankFull", Count(got)),
					_ => L("Storage.NearbyStorageFullLeft", Count(got)),
				},
			}, (status is BookNearbyServer.Status.Off or BookNearbyServer.Status.Busy || coins == BankedCoins.None || (coins == BankedCoins.Full && got > 0)) ? null : new int[] { ItemID.PiggyBank });
			break;
		}
		case Purpose.Craft:
			BookCrafting.ContinueCraft(pending.Recipe, pending.Requested, pending.Batches, status, got);
			break;
		}
	}

	// Deliveries that came while the inventory was locked (Locked), applied and acknowledged once the lock is over.
	private static readonly List<(int Id, BookNearbyServer.Status Status, List<Item> Items)> Arrived = new List<(int, BookNearbyServer.Status, List<Item>)>();

	private static void FlushArrived()
	{
		while (Arrived.Count > 0 && !Locked)
		{
			(int id, BookNearbyServer.Status status, List<Item> items) = Arrived[0];
			Arrived.RemoveAt(0);
			OnGive(id, status, items);
		}
	}

	private static void AgePending()
	{
		List<int> expired = null;
		foreach (KeyValuePair<int, Pending> pair in Waiting)
		{
			if (++pair.Value.Age > TimeoutTicks)
			{
				(expired ??= new List<int>()).Add(pair.Key);
			}
		}
		if (expired == null)
		{
			return;
		}
		foreach (int id in expired)
		{
			Waiting.Remove(id);
		}
		Say(() => L("Storage.NoReplyFromTheServerTry"));
		BookCrafting.Say("Storage.NoReplyFromTheServerTry");
	}

	internal static void CancelCraft()
	{
		foreach (int id in Waiting.Where((KeyValuePair<int, Pending> p) => p.Value.Purpose == Purpose.Craft).Select((KeyValuePair<int, Pending> p) => p.Key).ToList())
		{
			// The items still arrive and stay in the inventory; only the craft after them is dropped.
			Waiting[id].Recipe = null;
		}
	}

	// From BookNearbyServer: a mirror delta (single player calls this directly).
	internal static void ApplyChests(byte flags, int locked, int inUse, List<MirrorChest> upserts, List<int> removes)
	{
		if (!Subscribed)
		{
			return;
		}
		if ((flags & 1) != 0)
		{
			Mirror.Clear();
			AwaitingSnapshot = false;
		}
		if (upserts != null)
		{
			foreach (MirrorChest chest in upserts)
			{
				Mirror[chest.Index] = chest;
			}
		}
		if (removes != null)
		{
			foreach (int index in removes)
			{
				Mirror.Remove(index);
			}
		}
		SkippedLocked = locked;
		SkippedInUse = inUse;
		ServerWritable = (flags & 2) != 0;
		PullsOff = (flags & 4) != 0;
		MirrorVersion++;
	}

	internal static void HandlePacket(BookMessage type, BinaryReader reader)
	{
		switch (type)
		{
		case BookMessage.NearbyChestSync:
		{
			byte flags = reader.ReadByte();
			int locked = reader.ReadUInt16();
			int inUse = reader.ReadUInt16();
			int count = reader.ReadUInt16();
			List<MirrorChest> upserts = new List<MirrorChest>(count);
			for (int i = 0; i < count; i++)
			{
				int index = reader.ReadInt16();
				short x = reader.ReadInt16();
				short y = reader.ReadInt16();
				int used = reader.ReadByte();
				Stock[] entries = new Stock[reader.ReadByte()];
				for (int e = 0; e < entries.Length; e++)
				{
					entries[e] = new Stock(reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt());
				}
				upserts.Add(new MirrorChest(index, x, y, entries, used));
			}
			int gone = reader.ReadUInt16();
			List<int> removes = new List<int>(gone);
			for (int i = 0; i < gone; i++)
			{
				removes.Add(reader.ReadInt16());
			}
			ApplyChests(flags, locked, inUse, upserts, removes);
			break;
		}
		case BookMessage.NearbyStacked:
		{
			BookNearbyServer.Status status = (BookNearbyServer.Status)reader.ReadByte();
			int moved = reader.Read7BitEncodedInt();
			int remote = reader.Read7BitEncodedInt();
			OnStacked(status, moved, remote);
			break;
		}
		case BookMessage.NearbyGive:
		{
			int id = reader.ReadInt32();
			BookNearbyServer.Status status = (BookNearbyServer.Status)reader.ReadByte();
			int count = reader.ReadUInt16();
			List<Item> items = new List<Item>(count);
			for (int i = 0; i < count; i++)
			{
				items.Add(ItemIO.Receive(reader, readStack: true));
			}
			// Save & Exit has begun: the player is being saved on another thread (TML:Terraria/WorldGen.cs:3233-3255).
			// Unacknowledged, the delivery goes back to its container when the player leaves.
			if (Main.gameMenu)
			{
				break;
			}
			Arrived.Add((id, status, items));
			FlushArrived();
			break;
		}
		}
	}
}
