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

namespace ModernRecipeBook;

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

	internal sealed record MirrorChest(int Index, short X, short Y, Stock[] Entries);

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
	}

	private enum Purpose : byte
	{
		Take,
		Restock,
		Craft,
		Deposit
	}

	private sealed class Pending
	{
		internal Purpose Purpose;

		internal int Age;

		internal long Asked;

		internal Recipe Recipe;

		internal int Batches;

		internal int Requested;

		internal bool ToCursor;
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

	private static readonly Dictionary<(int Type, int Prefix), (long Local, long Remote)> Listed = new Dictionary<(int, int), (long, long)>();

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

	private static void Say(Func<string> text)
	{
		MessageText = text;
		MessageTick = Main.GameUpdateCount;
	}

	private static (int Type, int Version, bool Ru, string Text, string[] Rows) BreakdownCache = (0, -1, false, "", Array.Empty<string>());

	// The details panel's texts, rebuilt only when what they show changed (they are read every frame).
	private static (int Version, bool Ru, int Locked, int InUse, int Password, int Radius) TextsKey = (-1, false, 0, 0, 0, 0);

	private static string SummaryText = "";

	private static string NotCountedText = "";

	private static string RadiusText = "";

	private static bool Ru => Language.ActiveCulture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

	private static string T(string ru, string en)
	{
		return Ru ? ru : en;
	}

	internal static bool Enabled => BookNearbyConfig.Enabled && !PullsOff;

	// The stations of counted Crafting Interfaces, or null when the feature is off (vanilla stations only).
	internal static List<List<Item>> CraftStations => (Enabled && Stations.Count > 0) ? Stations : null;

	internal static bool HasStorage => Enabled && BookMagicStorage.Writable && Networks.Count > 0;

	internal static bool Busy => AnyWaiting(craft: false, shownOnly: false);

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
		Clear();
	}

	internal static void Clear()
	{
		Mirror.Clear();
		Banks.Clear();
		Networks.Clear();
		NetworkItems.Clear();
		Stations.Clear();
		Tallies.Clear();
		Listed.Clear();
		Display.Clear();
		Items.Clear();
		Waiting.Clear();
		Subscribed = AwaitingSnapshot = PullsOff = Infinite = false;
		SkippedLocked = SkippedInUse = SkippedPassword = 0;
		StayAround = Ticks = AwaitingTicks = 0;
		ScanDue = LocalDue = true;
		AppliedInputs = int.MinValue;
		MessageText = null;
		Version++;
		ItemsVersion++;
	}

	private static void Tick()
	{
		bool need = RecipeBookSystem.Open || RecipeBookSystem.HasTrackedGoals;
		bool enabled = BookNearbyConfig.Enabled;
		StayAround = (need && enabled) ? StayTicks : (StayAround - 1);
		SetSubscribed(enabled && StayAround > 0);
		if (AwaitingSnapshot)
		{
			AwaitingTicks++;
		}
		AgePending();
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
		if (inputs != AppliedInputs)
		{
			AppliedInputs = inputs;
			Rebuild();
		}
	}

	internal static void OnBookOpened()
	{
		ScanDue = LocalDue = true;
		if (Enabled)
		{
			StayAround = StayTicks;
			SetSubscribed(on: true);
		}
		Refresh(force: true);
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
			ModPacket packet = BookNearbyServer.NewPacket(BookNearbyServer.Packet.Subscribe);
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
			BookMagicStorage.Scan(player, BookNearbyConfig.Radius, Banks, Networks, Stations, out password);
		}
		else
		{
			Banks.Clear();
			Networks.Clear();
			Stations.Clear();
		}
		SkippedPassword = password;
		while (NetworkItems.Count < Networks.Count)
		{
			NetworkItems.Add(new List<Item>());
		}
		NetworkCreative.Clear();
		int signature = HashCode.Combine(Networks.Count, Stations.Count);
		foreach (int bank in Banks)
		{
			signature = HashCode.Combine(signature, bank);
		}
		for (int n = 0; n < Networks.Count; n++)
		{
			List<Item> items = NetworkItems[n];
			items.Clear();
			bool creative = BookMagicStorage.CollectItems(Networks[n].Heart, items);
			NetworkCreative.Add(creative);
			signature = HashCode.Combine(signature, Networks[n].Position, creative, items.Count);
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

	private static Tally TallyOf(int type)
	{
		if (!Tallies.TryGetValue(type, out var tally))
		{
			tally = new Tally();
			Tallies[type] = tally;
		}
		return tally;
	}

	private static void List(int type, int prefix, long local, long remote)
	{
		(long Local, long Remote) known = Listed.GetValueOrDefault((type, prefix));
		Listed[(type, prefix)] = (known.Local + local, known.Remote + remote);
	}

	private static void Rebuild()
	{
		Tallies.Clear();
		Listed.Clear();
		Seen.Clear();
		Infinite = false;
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
				foreach (Stock stock in pair.Value.Entries)
				{
					Tally tally = TallyOf(stock.Type);
					tally.Total += stock.Count;
					tally.Remote += stock.Count;
					tally.Chests += stock.Count;
					if (tally.LastChest != pair.Key)
					{
						tally.LastChest = pair.Key;
						tally.ChestCount++;
					}
					List(stock.Type, stock.Prefix, 0, stock.Count);
				}
			}
			for (int n = 0; n < Networks.Count; n++)
			{
				foreach (Item item in NetworkItems[n])
				{
					Tally tally = TallyOf(item.type);
					tally.Total += item.stack;
					tally.Remote += item.stack;
					tally.Storage += item.stack;
					if (tally.LastStorage != n)
					{
						tally.LastStorage = n;
						tally.StorageCount++;
					}
					List(item.type, item.prefix, 0, item.stack);
				}
			}
			Infinite = NetworkCreative.Contains(true);
		}
		RebuildItems();
		Version++;
	}

	private static void AddContainer(Item[] items, int bank, bool craft)
	{
		foreach (Item item in items)
		{
			if (item == null || item.IsAir || !Seen.Add(item))
			{
				continue;
			}
			Tally tally = TallyOf(item.type);
			tally.Total += item.stack;
			if (craft)
			{
				tally.Craft += item.stack;
			}
			else
			{
				tally.Banks += item.stack;
			}
			if (bank < 0)
			{
				tally.Open += item.stack;
			}
			else
			{
				tally.Bank[bank] += item.stack;
			}
			List(item.type, item.prefix, item.stack, 0);
		}
	}

	// One display Item per (type, prefix) outside the inventory, reused between rebuilds; stack = the total.
	private static void RebuildItems()
	{
		Items.Clear();
		foreach (KeyValuePair<(int Type, int Prefix), (long Local, long Remote)> pair in Listed)
		{
			if (!Display.TryGetValue(pair.Key, out var item))
			{
				item = new Item(pair.Key.Type, 1, pair.Key.Prefix);
				Display[pair.Key] = item;
			}
			item.stack = (int)Math.Min(int.MaxValue, pair.Value.Local + pair.Value.Remote);
			Items.Add(item);
		}
		Items.Sort((Item a, Item b) =>
		{
			int order = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
			return (order != 0) ? order : ((a.type != b.type) ? a.type.CompareTo(b.type) : a.prefix.CompareTo(b.prefix));
		});
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

	// "Inventory: 12 · Open chest: 40 · Piggy Bank: 5 · Chests ×3: 120 · Magic Storage: 40"
	internal static string Breakdown(int type)
	{
		bool ru = Ru;
		if (BreakdownCache.Type == type && BreakdownCache.Version == Version && BreakdownCache.Ru == ru)
		{
			return BreakdownCache.Text;
		}
		List<string> parts = new List<string>();
		if (Tallies.TryGetValue(type, out var tally))
		{
			if (tally.Inventory > 0)
			{
				parts.Add(T("Инвентарь: ", "Inventory: ") + Count(tally.Inventory));
			}
			if (tally.Open > 0)
			{
				parts.Add(T("Открытый сундук: ", "Open chest: ") + Count(tally.Open));
			}
			for (int bank = 0; bank < 4; bank++)
			{
				if (tally.Bank[bank] > 0)
				{
					parts.Add(BankName(bank) + ": " + Count(tally.Bank[bank]));
				}
			}
			if (tally.Chests > 0)
			{
				parts.Add(T("Сундуки", "Chests") + ((tally.ChestCount > 1) ? (" ×" + tally.ChestCount) : "") + ": " + Count(tally.Chests));
			}
			if (tally.Storage > 0 && !Infinite)
			{
				parts.Add("Magic Storage" + ((tally.StorageCount > 1) ? (" ×" + tally.StorageCount) : "") + ": " + Count(tally.Storage));
			}
		}
		if (Infinite)
		{
			parts.Add("Magic Storage: ∞");
		}
		string text = string.Join(" · ", parts);
		BreakdownCache = (type, Version, ru, text, parts.ToArray());
		return text;
	}

	// The same, one source per row (the details panel).
	internal static string[] BreakdownRows(int type)
	{
		Breakdown(type);
		return BreakdownCache.Rows;
	}

	private static void UpdateTexts()
	{
		var key = (Version, Ru, SkippedLocked, SkippedInUse, SkippedPassword, BookNearbyConfig.Radius);
		if (key == TextsKey)
		{
			return;
		}
		TextsKey = key;
		SummaryText = BuildSummary();
		NotCountedText = BuildNotCounted();
		RadiusText = BuildRadiusLine();
	}

	private static string Count(long value)
	{
		return value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
	}

	private static string BankName(int bank)
	{
		return Lang.GetItemNameValue(bank switch
		{
			0 => ItemID.PiggyBank,
			1 => ItemID.Safe,
			2 => ItemID.DefendersForge,
			_ => ItemID.VoidVault,
		});
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

	private static int BanksShown
	{
		get
		{
			Player player = Main.LocalPlayer;
			bool bag = player.useVoidBag() && player.chest != -5;
			return Banks.Count((int bank) => bank != player.chest && (bank != -5 || !bag));
		}
	}

	// "7 chests · 2 storages · 1 bank", or "Nothing nearby".
	internal static string Summary
	{
		get
		{
			UpdateTexts();
			return SummaryText;
		}
	}

	private static string BuildSummary()
	{
		bool ru = Ru;
		List<string> parts = new List<string>();
		if (ChestsShown > 0)
		{
			parts.Add(BookSourceRules.Count(ChestsShown, ru, "chest", "chests", "сундук", "сундука", "сундуков"));
		}
		if (Networks.Count > 0)
		{
			parts.Add(BookSourceRules.Count(Networks.Count, ru, "storage", "storages", "хранилище", "хранилища", "хранилищ"));
		}
		if (BanksShown > 0)
		{
			parts.Add(BookSourceRules.Count(BanksShown, ru, "bank", "banks", "банк", "банка", "банков"));
		}
		return (parts.Count == 0) ? T("Рядом ничего нет", "Nothing nearby") : string.Join(" · ", parts);
	}

	internal static string SummaryTip
	{
		get
		{
			Player player = Main.LocalPlayer;
			bool bag = player.useVoidBag() && player.chest != -5;
			List<string> lines = new List<string>();
			List<string> banks = Banks.Where((int bank) => bank != player.chest && (bank != -5 || !bag)).Select((int bank) => BankName(-2 - bank)).ToList();
			if (banks.Count > 0)
			{
				lines.Add(string.Join(", ", banks));
			}
			if (Networks.Count > 0)
			{
				List<BookMagicStorage.Network> byPlace = Networks.OrderBy((BookMagicStorage.Network n) => n.Position.X).ThenBy((BookMagicStorage.Network n) => n.Position.Y).ToList();
				lines.Add("Magic Storage: " + string.Join(", ", Networks.Select((BookMagicStorage.Network n) => (n.Name.Length > 0) ? n.Name : (T("Сеть ", "Network ") + (byPlace.IndexOf(n) + 1)))));
			}
			return string.Join("\n", lines);
		}
	}

	internal static string RadiusLine
	{
		get
		{
			UpdateTexts();
			return RadiusText;
		}
	}

	private static string BuildRadiusLine()
	{
		string within = T("В радиусе ", "Within ") + BookSourceRules.Count(BookNearbyConfig.Radius, Ru, "tile", "tiles", "блока", "блоков", "блоков");
		return (Main.netMode == NetmodeID.MultiplayerClient) ? (within + T(" · задаёт хост", " · set by the host")) : within;
	}

	// "Not counted: 2 locked · 1 in use · 1 password", or "" when nothing was skipped.
	internal static string NotCounted
	{
		get
		{
			UpdateTexts();
			return NotCountedText;
		}
	}

	private static string BuildNotCounted()
	{
		List<string> parts = new List<string>();
		if (SkippedLocked > 0)
		{
			parts.Add(SkippedLocked + T(" " + BookSourceRules.Plural(SkippedLocked, "заперт", "заперты", "заперты"), " locked"));
		}
		if (SkippedInUse > 0)
		{
			parts.Add(SkippedInUse + T(" " + BookSourceRules.Plural(SkippedInUse, "занят", "заняты", "заняты"), " in use"));
		}
		if (SkippedPassword > 0)
		{
			parts.Add(SkippedPassword + T(" с паролем", " password"));
		}
		return (parts.Count == 0) ? "" : (T("Не учтены: ", "Not counted: ") + string.Join(" · ", parts));
	}

	internal static string NotCountedTip => T("Запертые сундуки и сундуки, открытые другим игроком, пропускаются.\nХранилище с паролем учтётся, когда вы войдёте в его сеть в Magic Storage.", "Locked chests and chests another player has open are skipped.\nA storage with a password counts once you join it in Magic Storage.");

	// "From: inventory, 2 chests, Magic Storage" for a craft that needs more than the inventory has.
	internal static string CraftSources(IReadOnlyDictionary<int, long> banks, IReadOnlyDictionary<int, long> remote)
	{
		bool usesBanks = banks.Values.Any((long n) => n > 0);
		bool usesRemote = remote.Values.Any((long n) => n > 0);
		if (!usesBanks && !usesRemote)
		{
			return "";
		}
		List<string> parts = new List<string> { T("инвентарь", "inventory") };
		if (usesBanks)
		{
			parts.Add(BookSourceRules.Count(BanksShown, Ru, "bank", "banks", "банк", "банка", "банков"));
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
				parts.Add(BookSourceRules.Count(holding, Ru, "chest", "chests", "сундук", "сундука", "сундуков"));
			}
			if (storage)
			{
				parts.Add("Magic Storage");
			}
		}
		return T("Откуда: ", "From: ") + string.Join(", ", parts);
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

	// Moves up to `amount` of a kind from the local containers into the inventory. What does not fit stays where it
	// was. A world chest the player has open is synced the way Recipe.Create syncs it (msg 32 per slot).
	private static int MoveLocal(int type, int prefix, int amount, bool craft, bool banks, ref bool full)
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
				Item rest = player.GetItem(Main.myPlayer, piece, GetItemSettings.InventoryUIToInventorySettingsShowAsNew);
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

	// Storage tab: right-click takes one, Shift+click a stack. Local containers first, the rest from the server.
	internal static void Take(Item shown, bool single)
	{
		if (!Enabled || shown == null || shown.IsAir || Busy)
		{
			return;
		}
		if (!Main.mouseItem.IsAir)
		{
			Say(() => T("Сначала уберите предмет с курсора", "Clear the cursor first"));
			return;
		}
		if (!Listed.TryGetValue((shown.type, shown.prefix), out var have))
		{
			Say(() => T("Этого уже нет рядом", "No longer nearby"));
			return;
		}
		int want = single ? 1 : (int)Math.Min(Math.Max(1, shown.maxStack), have.Local + have.Remote);
		bool full = false;
		int moved = MoveLocal(shown.type, shown.prefix, want, craft: true, banks: true, ref full);
		int rest = want - moved;
		if (!full && rest > 0 && have.Remote > 0)
		{
			Send(Purpose.Take, new List<BookNearbyServer.Entry> { new BookNearbyServer.Entry(shown.type, shown.prefix, (int)Math.Min(rest, have.Remote)) }, want, moved);
		}
		else
		{
			Say(() => full ? T("Инвентарь полон", "Inventory full") : ((moved > 0) ? T("Забрано в инвентарь", "Taken to inventory") : T("Этого уже нет рядом", "No longer nearby")));
		}
		Refresh(force: true);
	}

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
		bool full = false;
		int moved = 0;
		foreach (KeyValuePair<(int, int), int> pair in deficits)
		{
			if (!Listed.TryGetValue(pair.Key, out var have))
			{
				continue;
			}
			int got = MoveLocal(pair.Key.Item1, pair.Key.Item2, pair.Value, craft: true, banks: true, ref full);
			moved += got;
			int rest = pair.Value - got;
			if (rest > 0 && have.Remote > 0 && remote.Count < BookNearbyServer.MaxEntries)
			{
				remote.Add(new BookNearbyServer.Entry(pair.Key.Item1, pair.Key.Item2, (int)Math.Min(rest, have.Remote)));
			}
		}
		if (remote.Count > 0)
		{
			Send(Purpose.Restock, remote, remote.Sum((BookNearbyServer.Entry e) => e.Amount), moved);
		}
		else
		{
			Say(() => (moved > 0) ? T("Стопки пополнены", "Stacks topped up") : T("Нечего пополнять", "Nothing to top up"));
		}
		Refresh(force: true);
	}

	// The inventory's own Quick Stack to Nearby Chests (vanilla radius; Magic Storage extends it to its networks).
	internal static void QuickStack()
	{
		Player player = Main.LocalPlayer;
		if (player.HasLockedInventory())
		{
			return;
		}
		player.QuickStackAllChests();
		Say(() => T("Сложено в сундуки рядом", "Stacked into nearby chests"));
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

	internal static string DepositHint => T("Shift+клик по предмету в инвентаре или перенос сюда — сложить в хранилище", "Shift+Click an inventory item or drop it here: put into storage");

	// Vanilla's chest shift-click (ItemSlot.OverrideHover / OverrideLeftClick): inventory, coin and ammo slots, never a
	// favorite. A player with a chest of their own open uses that chest, as in vanilla.
	internal static bool CanShiftDeposit(Player player, Item[] inventory, int context, int slot)
	{
		return player.whoAmI == Main.myPlayer && RecipeBookSystem.StorageTabOpen && player.chest == -1 && inventory == player.inventory && context is 0 or 1 or 2 && slot >= 0 && slot < inventory.Length && inventory[slot] != null && !inventory[slot].IsAir && !inventory[slot].favorited && CanDeposit;
	}

	// The whole stack, at once.
	internal static bool ShiftDeposit(Player player, Item[] inventory, int context, int slot)
	{
		if (!CanShiftDeposit(player, inventory, context, slot))
		{
			return false;
		}
		SendDeposit(new List<Item> { TakeOut(inventory, slot) }, toCursor: false);
		SoundEngine.PlaySound(in SoundID.Grab);
		return true;
	}

	// The cursor stack dropped onto the Storage grid, as onto a chest slot. Like there, a favorite loses its mark
	// (ItemSlot.LeftClick clears it for container slots).
	internal static void DepositCursor()
	{
		if (Main.mouseItem.IsAir)
		{
			return;
		}
		if (!CanDeposit)
		{
			Say(() => Enabled ? T("Рядом некуда складывать", "Nothing nearby to deposit into") : T("Хранилища рядом выключены", "Nearby storage is off"));
			return;
		}
		Item item = Main.mouseItem.Clone();
		item.favorited = false;
		Main.mouseItem.TurnToAir();
		SendDeposit(new List<Item> { item }, toCursor: true);
		SoundEngine.PlaySound(in SoundID.Grab);
	}

	// Vanilla's chest Deposit All (ChestUI.DepositAll): slots 10..49 except favorites, and coins from every other slot
	// only when a target already holds coins (ChestUI.MoveCoins). Coins are not exchanged into higher ones.
	internal static void DepositAll()
	{
		Player player = Main.LocalPlayer;
		if (!CanDeposit || player.HasLockedInventory())
		{
			return;
		}
		bool coins = false;
		for (int coin = ItemID.CopperCoin; coin <= ItemID.PlatinumCoin; coin++)
		{
			coins |= Tallies.TryGetValue(coin, out var tally) && (tally.Chests > 0 || tally.Storage > 0);
		}
		List<Item> items = new List<Item>();
		for (int k = 0; k < 58; k++)
		{
			Item item = player.inventory[k];
			if (item != null && !item.IsAir && !item.favorited && ((k >= 10 && k < 50) || (coins && ItemID.Sets.CommonCoin[item.type])))
			{
				items.Add(TakeOut(player.inventory, k));
			}
		}
		if (items.Count == 0)
		{
			Say(() => T("Нечего складывать", "Nothing to deposit"));
			return;
		}
		SendDeposit(items, toCursor: false);
		SoundEngine.PlaySound(in SoundID.Grab);
	}

	private static Item TakeOut(Item[] inventory, int slot)
	{
		Item item = inventory[slot].Clone();
		inventory[slot].TurnToAir();
		return item;
	}

	// As in Magic Storage, the items leave their slots when sent; what does not fit comes back (to the cursor for a
	// dropped stack). Deposits may overlap: nothing but the reply depends on an earlier one.
	private static void SendDeposit(List<Item> items, bool toCursor)
	{
		int id = ++NextId;
		Waiting[id] = new Pending
		{
			Purpose = Purpose.Deposit,
			Asked = items.Sum((Item i) => (long)i.stack),
			ToCursor = toCursor
		};
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			List<Item> leftovers = new List<Item>();
			BookNearbyServer.Status status = BookNearbyServer.Deposit(Main.myPlayer, id, items, leftovers);
			OnGive(id, status, leftovers);
			return;
		}
		ModPacket packet = BookNearbyServer.NewPacket(BookNearbyServer.Packet.Deposit);
		packet.Write(id);
		packet.Write((byte)items.Count);
		foreach (Item item in items)
		{
			ItemIO.Send(item, packet, writeStack: true);
		}
		packet.Send();
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

	private static void Send(Purpose purpose, List<BookNearbyServer.Entry> entries, long asked, int movedLocally, Recipe recipe = null, int batches = 0, int requested = 0)
	{
		int id = ++NextId;
		Waiting[id] = new Pending
		{
			Purpose = purpose,
			Asked = asked,
			Recipe = recipe,
			Batches = batches,
			Requested = requested
		};
		LocallyMoved = movedLocally;
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			List<Item> items = new List<Item>();
			BookNearbyServer.Status status = BookNearbyServer.Pull(Main.myPlayer, id, entries, items);
			OnGive(id, status, items);
			return;
		}
		ModPacket packet = BookNearbyServer.NewPacket(BookNearbyServer.Packet.Pull);
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
				if (pending != null && pending.ToCursor && Main.mouseItem.IsAir)
				{
					Main.mouseItem = item;
					continue;
				}
				Item rest;
				try
				{
					rest = player.GetItem(Main.myPlayer, item, GetItemSettings.InventoryUIToInventorySettingsShowAsNew);
				}
				catch (Exception error)
				{
					// A mod's pickup hook failed: the stack lands at the player's feet rather than nowhere.
					ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Could not put " + item.Name + " into the inventory.", error);
					rest = item;
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
				ModPacket packet = BookNearbyServer.NewPacket(BookNearbyServer.Packet.Ack);
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
		case Purpose.Take:
		{
			long total = got + LocallyMoved;
			long asked = pending.Asked + LocallyMoved;
			Say(() => status switch
			{
				BookNearbyServer.Status.Off => T("Хранилища рядом выключены", "Nearby storage is off"),
				BookNearbyServer.Status.Busy => T("Слишком часто: подождите", "Too fast: wait a moment"),
				_ => spilled ? T("Инвентарь полон: остаток на курсоре", "Inventory full: rest on cursor") : ((total == 0) ? T("Этого уже нет рядом", "No longer nearby") : ((status == BookNearbyServer.Status.Partial) ? (T("Взято ", "Took ") + total + T(" из ", " of ") + asked + T(": остального уже нет", ": the rest is gone")) : T("Забрано в инвентарь", "Taken to inventory"))),
			});
			break;
		}
		case Purpose.Restock:
			long topped = got + LocallyMoved;
			Say(() => (status == BookNearbyServer.Status.Busy) ? T("Слишком часто: подождите", "Too fast: wait a moment") : ((topped > 0) ? T("Стопки пополнены", "Stacks topped up") : T("Нечего пополнять", "Nothing to top up")));
			break;
		case Purpose.Deposit:
			Say(() => status switch
			{
				BookNearbyServer.Status.Off => T("Хранилища рядом выключены", "Nearby storage is off"),
				BookNearbyServer.Status.Busy => T("Слишком часто: подождите", "Too fast: wait a moment"),
				_ => (got == 0) ? T("Сложено рядом", "Stored nearby") : (T("Рядом нет места: осталось ", "Nearby storage is full: ") + Count(got) + T("", " left")),
			});
			break;
		case Purpose.Craft:
			BookCrafting.ContinueCraft(pending.Recipe, pending.Requested, pending.Batches, status, got);
			break;
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
		Say(() => T("Сервер не отвечает: повторите", "No reply from the server: try again"));
		BookCrafting.Say("Сервер не отвечает: повторите", "No reply from the server: try again");
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
		PullsOff = (flags & 4) != 0;
		MirrorVersion++;
	}

	internal static void HandlePacket(BookNearbyServer.Packet type, BinaryReader reader)
	{
		switch (type)
		{
		case BookNearbyServer.Packet.ChestSync:
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
				Stock[] entries = new Stock[reader.ReadByte()];
				for (int e = 0; e < entries.Length; e++)
				{
					entries[e] = new Stock(reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt());
				}
				upserts.Add(new MirrorChest(index, x, y, entries));
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
		case BookNearbyServer.Packet.Give:
		{
			int id = reader.ReadInt32();
			BookNearbyServer.Status status = (BookNearbyServer.Status)reader.ReadByte();
			int count = reader.ReadUInt16();
			List<Item> items = new List<Item>(count);
			for (int i = 0; i < count; i++)
			{
				items.Add(ItemIO.Receive(reader, readStack: true));
			}
			OnGive(id, status, items);
			break;
		}
		}
	}
}
