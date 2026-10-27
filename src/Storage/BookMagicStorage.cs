using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ModernRecipeBook.Storage;

// Magic Storage through reflection (MS is a weak reference). The book never opens an MS session. It reads the hearts
// whose access tiles are near the player, the stations in their Crafting Interfaces and the network the player has
// open in MS's own window. Withdrawals and deposits run where MS keeps its truth (the server, or the single-player
// game) through TEStorageHeart.Withdraw / DepositItem, the calls MS's own packet handler makes
// (docs/nearby-storage.md §5). Three member groups bind separately: reading (both sides), the server's writes, and
// hiding a window the player opened. A group that fails is off on its own; chests never depend on any of them.
public sealed class BookMagicStorage : ModSystem
{
	internal sealed class Network
	{
		internal object Heart;

		internal Point16 Position;

		// Squared pixels from the player to the nearest access tile of this network; the open session reads 0.
		internal float Distance;

		internal string Name = "";

		// How it was found: an access tile within the radius, the network open in Magic Storage's own window, a carried
		// Portable Access. A network with no access tile in range is remote, and the Storage tab says so.
		internal bool Nearby;

		internal bool ViaWindow;

		// The carried Portable Access that reaches it (its item type; 0 none): the radius line names it, a link to its card.
		internal int Carrier;

		internal bool ViaCarried => Carrier > 0;

		internal bool Remote => !Nearby;
	}

	private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static Mod Owner;

	internal static bool Installed { get; private set; }

	internal static bool Readable { get; private set; }

	internal static bool Writable { get; private set; }

	private static bool CanHide;

	// Storage cells (TEStorageUnit.Capacity / NumItems): a group of its own, so a rename there leaves the rest working.
	private static bool Countable;

	// A Decrafting Interface is an access like the others: MS opens the network from it (Components.DecraftingAccess is a
	// StorageAccess), so one within the radius reaches its network too.
	private static readonly HashSet<string> AccessTiles = new HashSet<string> { "StorageHeart", "StorageAccess", "CraftingAccess", "RemoteAccess", "DecraftingAccess" };

	// Shimmering through a network's Decrafting Interface (BookShimmer): which networks have one. A group of its own on
	// both sides. The shimmering itself is vanilla's (BookNearbyServer.Shimmer), not MS's ShimmerMetrics, which checks
	// Item.CanShimmer on a one-item sample and so never decrafts what a recipe makes several of (arrows, bullets).
	internal static bool Shimmers { get; private set; }

	private static MethodInfo ManagerOf;

	private static MethodInfo DecraftingOf;

	// The Decrafting Interface item: the reasons and the card name it, a link to its card (0 when MS has none by that name).
	internal static int DecraftingItem { get; private set; }

	// Reading.
	private static Type HeartType;

	private static Type CraftingType;

	private static Type CreativeType;

	private static MethodInfo AccessHeart;

	private static MethodInfo UnitsOf;

	private static MethodInfo ItemsOf;

	private static PropertyInfo InactiveOf;

	private static FieldInfo NetworkOf;

	private static MethodInfo CanAccessNetwork;

	private static FieldInfo StationsOf;

	private static FieldInfo NameOf;

	private static PropertyInfo LocalStoragePlayer;

	private static MethodInfo SessionHeart;

	private static MethodInfo SessionCrafting;

	private static int CraftingTile = -1;

	private static int SnowEmulator = -1;

	private static int BiomeGlobe = -1;

	// The Storage Heart item: a network's link in the Storage tab opens its card (0 when MS has none by that name).
	internal static int HeartItem { get; private set; }

	private static Type UnitType;

	private static PropertyInfo CapacityOf;

	private static PropertyInfo NumItemsOf;

	// A carried Portable Access (any tier, the crafting ones too), checked by MS's own members: a group of its own, on
	// both sides, since the server checks it again before a pull or a deposit.
	private static bool Portable;

	private static Type PortableType;

	private static PropertyInfo LocationOf;

	private static MethodInfo RangeOf;

	private static MethodInfo ContextOf;

	private static MethodInfo HeartFromAccess;

	private static MethodInfo NearAccess;

	private static MethodInfo PylonsNear;

	private static MethodInfo PylonNearStorage;

	// Why a carried, bound Portable Access does not count (BookSources' "not counted" line). Locked networks count as
	// "password" with the access tiles.
	internal enum CarriedSkip : byte
	{
		OutOfRange,
		NoPylon,
		Missing,
		Loading
	}

	// Multiplayer client: when each far location's sections were last asked for (BookMessage.MapSections).
	private static readonly Dictionary<Point16, ulong> SectionsAsked = new Dictionary<Point16, ulong>();

	private const int SectionRetryTicks = 300;

	// Multiplayer client: the location last sent to the server per inventory slot (0..57; 58 is the cursor copy).
	private static readonly Point16[] SentLocations = new Point16[58];

	// The server's writes.
	private static MethodInfo WithdrawOf;

	private static MethodInfo DepositOf;

	private static FieldInfo UsingOf;

	private static FieldInfo AccessingOf;

	private static MethodInfo RefreshOf;

	private static MethodInfo AuditWithdrawOf;

	private static MethodInfo AuditDepositOf;

	// Hiding the window the player opened while the book is open.
	private static MethodInfo StorageOpen;

	private static MethodInfo CraftingOpen;

	private static FieldInfo InterfaceField;

	private static FieldInfo CurrentMouseField;

	private static FieldInfo OldMouseField;

	private static MethodInfo Focused;

	private static MethodInfo Unfocus;

	private static PropertyInfo ActorProperty;

	// MS's own Deposit All puts coins into the Piggy Bank first (DepositAllCoinsFirst).
	private static bool CoinsFirst;

	private static MethodInfo GuiHeart;

	private static MethodInfo HasItemOf;

	private static bool Hooked;

	internal static string FailureText { get; private set; } = "";

	public override void PostSetupContent()
	{
		if (!ModLoader.TryGetMod("MagicStorage", out var mod))
		{
			return;
		}
		Installed = true;
		Owner = mod;
		Readable = TryBind("read", BindReading);
		Writable = Readable && TryBind("write", BindWriting);
		CanHide = !Main.dedServ && Readable && TryBind("hide", BindHiding);
		Countable = !Main.dedServ && Readable && TryBind("cells", BindCells);
		Portable = Readable && TryBind("portable", BindPortable);
		CoinsFirst = !Main.dedServ && Readable && TryBind("coins", BindCoins);
		Shimmers = Readable && TryBind("shimmer", BindShimmer);
		if (CanHide)
		{
			On_UserInterface.Update += UpdateNativeInterface;
			On_ItemSlot.LeftClick_ItemArray_int_int += BlockLeftClick;
			On_ItemSlot.RightClick_ItemArray_int_int += BlockRightClick;
			Hooked = true;
		}
		if (Readable)
		{
			string checkedVersion = (mod.Version == new Version(0, 7, 0, 11)) ? "" : " (unverified; checked against 0.7.0.11)";
			Mod.Logger.Info($"Magic Storage {mod.Version}{checkedVersion}: read={Readable}, write={Writable}, hide={CanHide}, shimmer={Shimmers}.");
		}
	}

	private static bool TryBind(string group, Action bind)
	{
		try
		{
			bind();
			return true;
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Magic Storage support (" + group + ") is off; chests keep working.", error);
			FailureText = Owner?.Version.ToString() ?? "";
			return false;
		}
	}

	private static Type Get(string name)
	{
		return Owner.Code.GetType("MagicStorage." + name, throwOnError: true);
	}

	private static MethodInfo Method(Type type, string name, params Type[] args)
	{
		return type.GetMethod(name, All, null, args, null) ?? throw new MissingMethodException(type.FullName, name);
	}

	private static FieldInfo Field(Type type, string name)
	{
		return type.GetField(name, All) ?? throw new MissingFieldException(type.FullName, name);
	}

	private static PropertyInfo Property(Type type, string name)
	{
		return type.GetProperty(name, All) ?? throw new MissingMemberException(type.FullName, name);
	}

	private static void BindReading()
	{
		HeartType = Get("Components.TEStorageHeart");
		CraftingType = Get("Components.TECraftingAccess");
		CreativeType = Get("Components.TECreativeStorageUnit");
		Type unit = Get("Components.TEAbstractStorageUnit");
		Type component = Get("Components.TEStorageComponent");
		AccessHeart = Method(Get("Components.StorageAccess"), "GetHeart", typeof(int), typeof(int));
		UnitsOf = Method(HeartType, "GetStorageUnits");
		ItemsOf = Method(unit, "GetItems");
		InactiveOf = Property(unit, "Inactive");
		NetworkOf = Field(component, "assignedNetwork");
		CanAccessNetwork = Method(Get("Common.Systems.SecuritySystem"), "CanPlayerAccessImmediately", typeof(Player), typeof(int));
		StationsOf = Field(CraftingType, "stations");
		NameOf = Field(HeartType, "storageName");
		Type player = Get("StoragePlayer");
		LocalStoragePlayer = Property(player, "LocalPlayer");
		SessionHeart = Method(player, "GetStorageHeart");
		SessionCrafting = Method(player, "GetCraftingAccess");
		CraftingTile = Owner.TryFind<ModTile>("CraftingAccess", out var crafting) ? crafting.Type : -1;
		SnowEmulator = Owner.TryFind<ModItem>("SnowBiomeEmulator", out var snow) ? snow.Type : -1;
		BiomeGlobe = Owner.TryFind<ModItem>("BiomeGlobe", out var globe) ? globe.Type : -1;
		HeartItem = Owner.TryFind<ModItem>("StorageHeart", out var heart) ? heart.Type : 0;
	}

	private static void BindCells()
	{
		UnitType = Get("Components.TEStorageUnit");
		CapacityOf = Property(UnitType, "Capacity");
		NumItemsOf = Property(UnitType, "NumItems");
	}

	private static void BindPortable()
	{
		PortableType = Get("Items.PortableAccess");
		LocationOf = Property(Get("Items.Locator"), "Location");
		RangeOf = Method(PortableType, "GetEffectiveRange", typeof(float).MakeByRefType(), typeof(int).MakeByRefType());
		ContextOf = Method(PortableType, "OpenContext", typeof(int).MakeByRefType(), typeof(string).MakeByRefType(), typeof(string).MakeByRefType(), typeof(bool).MakeByRefType());
		Type utility = Get("Utility");
		HeartFromAccess = Method(utility, "GetHeartFromAccess", typeof(Point16));
		NearAccess = Method(utility, "PlayerIsNearAccess", typeof(Player), typeof(Point16), typeof(float));
		PylonsNear = Method(utility, "NearbyPylons", typeof(Player), typeof(float));
		PylonNearStorage = Method(utility, "StorageSystemHasNearbyPylon", typeof(Player), HeartType, typeof(int));
	}

	private static void BindWriting()
	{
		WithdrawOf = Method(HeartType, "Withdraw", typeof(Item), typeof(bool));
		DepositOf = Method(HeartType, "DepositItem", typeof(Item));
		UsingOf = Field(HeartType, "clientUsingHeart");
		AccessingOf = Field(Get("Common.Systems.SecuritySystem"), "accessingPlayer");
		RefreshOf = Method(Get("NetHelper"), "SendRefreshNetworkItems", typeof(Point16), typeof(bool), typeof(IEnumerable<int>));
		Type audit = Get("Common.Systems.Auditing.AuditSystem");
		AuditWithdrawOf = Method(audit, "ReportItemWithdraw", typeof(int), HeartType, typeof(Item));
		AuditDepositOf = Method(audit, "ReportItemDeposit", typeof(int), HeartType, typeof(Item));
	}

	// The hook goes in last, so a member that is missing leaves MS's button untouched. tModLoader removes it on unload.
	private static void BindCoins()
	{
		Type storageGui = Get("StorageGUI");
		MethodInfo depositAll = Method(storageGui, "TryDepositAll", typeof(bool));
		GuiHeart = Method(storageGui, "GetHeart");
		HasItemOf = Method(HeartType, "HasItem", typeof(Item), typeof(Player), typeof(bool));
		MonoModHooks.Add(depositAll, DepositAllCoinsFirst);
	}

	// MS's own Deposit All (StorageGUI.TryDepositAll, MS:MagicStorage/StorageGUI.cs:448-484) takes rows 10..49 but
	// favorites; in its quick-stack mode only the kinds the storage holds (TEStorageHeart.HasItem). The coins among them
	// go to the Piggy Bank first when it is available (BookSources.BankInventoryCoins); what does not fit stays for MS.
	private static bool DepositAllCoinsFirst(Func<bool, bool> orig, bool quickStack)
	{
		bool banked = false;
		if (CoinsFirst)
		{
			try
			{
				object heart = GuiHeart.Invoke(null, null);
				// HasItem checks security as a withdraw does (MS:MagicStorage.Components/TEStorageHeart.cs:1020-1024);
				// MS asks it inside its own context.
				banked = heart != null && BookSources.BankInventoryCoins((Item item) => !quickStack || As(Main.myPlayer, () => (bool)HasItemOf.Invoke(heart, new object[3] { item, Main.LocalPlayer, true })));
			}
			catch (Exception error)
			{
				Fail("coins", error);
			}
		}
		return orig(quickStack) | banked;
	}

	private static void BindShimmer()
	{
		PropertyInfo manager = Property(Get("Components.TEStorageCenter"), "ComponentManager");
		ManagerOf = manager.GetMethod ?? throw new MissingMethodException(manager.DeclaringType?.FullName, "get_ComponentManager");
		DecraftingOf = Method(manager.PropertyType, "GetDecraftingAccesses");
		DecraftingItem = Owner.TryFind<ModItem>("DecraftingAccess", out var item) ? item.Type : 0;
	}

	// Whether the heart's network has a Decrafting Interface (TEStorageCenter.ComponentManager.GetDecraftingAccesses): the
	// book shimmers only through one, as MS's own window does. Both sides: the components come with the heart's data.
	internal static bool HasDecrafting(object heart)
	{
		if (!Shimmers || heart == null)
		{
			return false;
		}
		try
		{
			return ((IEnumerable)DecraftingOf.Invoke(ManagerOf.Invoke(heart, null), null)).GetEnumerator().MoveNext();
		}
		catch (Exception error)
		{
			Fail("shimmer", error);
			return false;
		}
	}

	private static void BindHiding()
	{
		Type ui = Get("Common.Systems.MagicUI");
		Type storageGui = Get("StorageGUI");
		StorageOpen = Method(ui, "IsStorageUIOpen");
		CraftingOpen = Method(ui, "IsCraftingUIOpen");
		InterfaceField = Field(ui, "uiInterface");
		CurrentMouseField = Field(storageGui, "curMouse");
		OldMouseField = Field(storageGui, "oldMouse");
		if (!ModLoader.TryGetMod("SerousCommonLib", out var input))
		{
			throw new MissingMemberException("SerousCommonLib is missing");
		}
		Type tracker = input.Code.GetType("SerousCommonLib.API.Input.TextInputTracker", throwOnError: true);
		Type state = input.Code.GetType("SerousCommonLib.API.Input.TextInputState", throwOnError: true);
		Focused = Method(tracker, "GetFocusedState");
		Unfocus = Method(state, "Unfocus");
		ActorProperty = Property(state, "Actor");
	}

	// A member that bound but throws when used (an MS bug or a world MS did not expect) turns that group off.
	private static void Fail(string group, Exception error)
	{
		ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Magic Storage support (" + group + ") stopped; chests keep working.", error);
		if (group == "read")
		{
			Readable = Writable = false;
		}
		else if (group == "write")
		{
			Writable = false;
		}
		else if (group == "cells")
		{
			Countable = false;
		}
		else if (group == "portable")
		{
			Portable = false;
		}
		else if (group == "coins")
		{
			CoinsFirst = false;
		}
		else if (group == "shimmer")
		{
			Shimmers = false;
		}
		else
		{
			CanHide = false;
		}
	}

	// One pass over the tiles within `tiles` of the player: banks (-2..-5, once each: their tiles and the projectiles that
	// open them) and Magic Storage access
	// tiles. Each heart is listed once with its nearest access tile; a heart the player may not use counts in
	// `locked`. `stations` gets the station lists of counted Crafting Interfaces. The network the player has open in
	// MS (or, on the server, has locked in MS) is added however far it is, and so is the network of each bound Portable
	// Access the player carries when MS itself would open it from here (AddCarried); `carried` gets the ones that would
	// not, with the reason.
	internal static void Scan(Player player, int tiles, List<int> banks, List<Network> networks, List<List<Item>> stations, out int locked, List<(int Type, bool Crafting, CarriedSkip Why)> carried = null)
	{
		carried?.Clear();
		locked = 0;
		banks?.Clear();
		networks.Clear();
		stations?.Clear();
		Vector2 center = player.Center;
		int px = (int)(center.X / 16f);
		int py = (int)(center.Y / 16f);
		bool readMs = Readable;
		HashSet<object> refused = null;
		for (int x = Math.Max(0, px - tiles - 1); x <= Math.Min(Main.maxTilesX - 1, px + tiles + 1); x++)
		{
			for (int y = Math.Max(0, py - tiles - 1); y <= Math.Min(Main.maxTilesY - 1, py + tiles + 1); y++)
			{
				Tile tile = Main.tile[x, y];
				if (!tile.HasTile)
				{
					continue;
				}
				int bank = BookSourceRules.BankOf(tile.TileType);
				if (bank != 0)
				{
					if (banks != null && !banks.Contains(bank) && BookSourceRules.InRange(center, BookSourceRules.TileCenter(x, y), tiles))
					{
						banks.Add(bank);
					}
				}
				else if (readMs && tile.TileType >= TileID.Count && tile.TileFrameX % 36 == 0 && tile.TileFrameY % 36 == 0)
				{
					ModTile modTile = TileLoader.GetTile(tile.TileType);
					if (modTile?.Mod == Owner && AccessTiles.Contains(modTile.Name))
					{
						Vector2 point = BookSourceRules.TileCenter(x, y, 2, 2);
						if (BookSourceRules.InRange(center, point, tiles))
						{
							try
							{
								AddAccess(player, modTile, x, y, Vector2.DistanceSquared(center, point), networks, stations, ref refused);
							}
							catch (Exception error)
							{
								Fail("read", error);
								readMs = false;
							}
						}
					}
				}
			}
		}
		// The Money Trough and Chester open the Piggy Bank, the Void Lens the Void Vault (Projectile.TryGetContainerIndex),
		// and vanilla's quick stack reaches them like bank tiles, measured to the nearest point of the hitbox
		// (TML:Terraria/Player.cs:20036-20056, 20115-20129). Anyone's: each opens the player's own bank.
		if (banks != null)
		{
			foreach (Projectile projectile in Main.ActiveProjectiles)
			{
				if (projectile.TryGetContainerIndex(out int bank) && !banks.Contains(bank) && BookSourceRules.InRange(center, projectile.Hitbox.ClosestPointInRect(center), tiles))
				{
					banks.Add(bank);
				}
			}
		}
		if (readMs && Portable)
		{
			try
			{
				AddCarried(player, networks, stations, ref refused, carried);
			}
			catch (Exception error)
			{
				Fail("portable", error);
			}
		}
		locked = refused?.Count ?? 0;
		if (readMs)
		{
			try
			{
				AddSession(player, networks, stations);
			}
			catch (Exception error)
			{
				Fail("read", error);
			}
		}
	}

	// A Magic Storage Locator or anything built on it (Portable Access, Portable Crafting Access): by type name, so it
	// holds even when a member group failed to bind.
	internal static bool IsLocator(Item item)
	{
		if (item?.ModItem == null || item.ModItem.Mod != Owner)
		{
			return false;
		}
		for (Type t = item.ModItem.GetType(); t != null; t = t.BaseType)
		{
			if (t.FullName == "MagicStorage.Items.Locator")
			{
				return true;
			}
		}
		return false;
	}

	// Each bound Portable Access in the inventory, checked as PortableAccess.DoOpenStorage and OpenStorage check it when
	// used: the location still holds the item's tile (a heart, or a Crafting Interface for the crafting ones) at frame
	// 0,0; then, with a limited range, the player near the location, or else a valid pylon near the player and one near
	// the storage (Utility.PlayerIsNearAccess, NearbyPylons, StorageSystemHasNearbyPylon); then MS's security. Its network
	// ranks by the distance to the location, so networks in reach of the player stay first. A crafting one adds its
	// interface's stations. On a multiplayer client a location whose tiles have not arrived is asked for once.
	private static void AddCarried(Player player, List<Network> networks, List<List<Item>> stations, ref HashSet<object> refused, List<(int Type, bool Crafting, CarriedSkip Why)> carried)
	{
		for (int slot = 0; slot < SentLocations.Length; slot++)
		{
			Item item = player.inventory[slot];
			if (item == null || item.IsAir || item.ModItem == null || !PortableType.IsInstanceOfType(item.ModItem))
			{
				continue;
			}
			Point16 location = (Point16)LocationOf.GetValue(item.ModItem);
			if (location.X < 0 || location.Y < 0 || location.X >= Main.maxTilesX - 1 || location.Y >= Main.maxTilesY - 1)
			{
				continue;
			}
			// MS binds a locator on the client alone (StorageHeart.RightClick) and the slot is not resent for mod data, so
			// the server, which checks the same item before a pull, would keep the old location: send the slot once per
			// new location, as the game syncs any changed slot.
			if (Main.netMode == NetmodeID.MultiplayerClient && player.whoAmI == Main.myPlayer && SentLocations[slot] != location)
			{
				SentLocations[slot] = location;
				NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
			}
			object[] context = new object[4];
			ContextOf.Invoke(item.ModItem, context);
			bool crafting = (bool)context[3];
			if (!LocationLoaded(location))
			{
				carried?.Add((item.type, crafting, CarriedSkip.Loading));
				continue;
			}
			Tile tile = Main.tile[location.X, location.Y];
			object heart = (tile.HasTile && tile.TileType == (int)context[0] && tile.TileFrameX == 0 && tile.TileFrameY == 0) ? HeartFromAccess.Invoke(null, new object[1] { location }) : null;
			if (heart == null)
			{
				carried?.Add((item.type, crafting, CarriedSkip.Missing));
				continue;
			}
			object[] range = new object[2];
			float playerRange = ((bool)RangeOf.Invoke(item.ModItem, range) && (float)range[0] >= 0f) ? (float)range[0] : -1f;
			int pylonRange = (int)range[1];
			if (playerRange >= 0f && !(bool)NearAccess.Invoke(null, new object[3] { player, location, playerRange }))
			{
				if (!((IEnumerable)PylonsNear.Invoke(null, new object[2] { player, playerRange })).GetEnumerator().MoveNext())
				{
					carried?.Add((item.type, crafting, CarriedSkip.OutOfRange));
					continue;
				}
				if (pylonRange >= 0 && !(bool)PylonNearStorage.Invoke(null, new object[3] { player, heart, pylonRange }))
				{
					carried?.Add((item.type, crafting, CarriedSkip.NoPylon));
					continue;
				}
			}
			if (!CanAccess(player, heart))
			{
				(refused ??= new HashSet<object>(ReferenceEqualityComparer.Instance)).Add(heart);
				continue;
			}
			Network network = Add(networks, heart);
			network.Distance = Math.Min(network.Distance, Vector2.DistanceSquared(player.Center, location.ToWorldCoordinates(16f, 16f)));
			network.Carrier = item.type;
			if (crafting && stations != null && TileEntity.ByPosition.TryGetValue(location, out var entity) && CraftingType.IsInstanceOfType(entity) && StationsOf.GetValue(entity) is List<Item> list && !stations.Contains(list))
			{
				stations.Add(list);
			}
		}
	}

	// Single player and the server have every tile. A client asks the server for the 3x3 sections around a location it
	// lacks (the heart's units stand next to it), once, and again only after SectionRetryTicks while still missing.
	private static bool LocationLoaded(Point16 location)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient || Main.sectionManager.TilesLoaded(location.X, location.Y, location.X + 1, location.Y + 1))
		{
			return true;
		}
		if (!SectionsAsked.TryGetValue(location, out ulong asked) || Main.GameUpdateCount - asked >= SectionRetryTicks)
		{
			SectionsAsked[location] = Main.GameUpdateCount;
			ModPacket packet = ModContent.GetInstance<ModernRecipeBook>().GetPacket();
			packet.Write((byte)BookMessage.MapSections);
			packet.Write(location.X);
			packet.Write(location.Y);
			packet.Send();
		}
		return false;
	}

	public override void OnWorldUnload()
	{
		SectionsAsked.Clear();
		Array.Fill(SentLocations, Point16.NegativeOne);
	}

	private static void AddAccess(Player player, ModTile modTile, int x, int y, float distance, List<Network> networks, List<List<Item>> stations, ref HashSet<object> refused)
	{
		object heart = AccessHeart.Invoke(modTile, new object[2] { x, y });
		if (heart == null)
		{
			return;
		}
		if (!CanAccess(player, heart))
		{
			(refused ??= new HashSet<object>(ReferenceEqualityComparer.Instance)).Add(heart);
			return;
		}
		Network network = Add(networks, heart);
		network.Distance = Math.Min(network.Distance, distance);
		network.Nearby = true;
		if (stations != null && modTile.Name == "CraftingAccess" && TileEntity.ByPosition.TryGetValue(new Point16(x, y), out var entity) && CraftingType.IsInstanceOfType(entity) && StationsOf.GetValue(entity) is List<Item> list && !stations.Contains(list))
		{
			stations.Add(list);
		}
	}

	private static Network Add(List<Network> networks, object heart)
	{
		foreach (Network known in networks)
		{
			if (known.Heart == heart)
			{
				return known;
			}
		}
		Network network = new Network
		{
			Heart = heart,
			Position = ((TileEntity)heart).Position,
			Distance = float.MaxValue,
			Name = ((NameOf.GetValue(heart) as string) ?? "").Trim()
		};
		networks.Add(network);
		return network;
	}

	private static void AddSession(Player player, List<Network> networks, List<List<Item>> stations)
	{
		if (Main.netMode != NetmodeID.Server)
		{
			if (player.whoAmI != Main.myPlayer)
			{
				return;
			}
			object storagePlayer = LocalStoragePlayer.GetValue(null);
			object heart = SessionHeart.Invoke(storagePlayer, null);
			if (heart == null || !CanAccess(player, heart))
			{
				return;
			}
			Network session = Add(networks, heart);
			session.Distance = 0f;
			session.ViaWindow = true;
			if (stations != null && SessionCrafting.Invoke(storagePlayer, null) is object crafting && StationsOf.GetValue(crafting) is List<Item> list && !stations.Contains(list))
			{
				stations.Add(list);
			}
			return;
		}
		// The server knows which heart each client has open from MS's lock message (NetHelper.ReceiveStorageHeartUsage).
		if (UsingOf == null)
		{
			return;
		}
		foreach (TileEntity entity in TileEntity.ByID.Values)
		{
			if (HeartType.IsInstanceOfType(entity) && UsingOf.GetValue(entity) is bool[] users && player.whoAmI < users.Length && users[player.whoAmI] && CanAccess(player, entity))
			{
				Network session = Add(networks, entity);
				session.Distance = 0f;
				session.ViaWindow = true;
			}
		}
	}

	internal static bool CanAccess(Player player, object heart)
	{
		return (bool)CanAccessNetwork.Invoke(null, new object[2] { player, NetworkOf.GetValue(heart) });
	}

	// The heart's stored items, without the Creative Storage Unit, which would list every item in the game; it reports
	// `creative` instead (MS's own window lists inactive units too, so they count).
	internal static bool CollectItems(object heart, List<Item> into)
	{
		bool creative = false;
		try
		{
			foreach (object unit in (IEnumerable)UnitsOf.Invoke(heart, null))
			{
				if (CreativeType.IsInstanceOfType(unit))
				{
					creative |= !(bool)InactiveOf.GetValue(unit);
					continue;
				}
				foreach (Item item in (IEnumerable)ItemsOf.Invoke(unit, null))
				{
					if (item != null && !item.IsAir)
					{
						into.Add(item);
					}
				}
			}
		}
		catch (Exception error)
		{
			Fail("read", error);
		}
		return creative;
	}

	// A heart's cells as MS's own Capacity line counts them (BaseStorageUIAccessPage: NumItems / Capacity over the plain
	// TEStorageUnits, so the Creative Storage Unit is never in it), minus inactive units, which take no deposits.
	internal static (int Total, int Used, int Inactive) Cells(object heart)
	{
		int total = 0, used = 0, inactive = 0;
		if (!Countable)
		{
			return (0, 0, 0);
		}
		try
		{
			foreach (object unit in (IEnumerable)UnitsOf.Invoke(heart, null))
			{
				if (!UnitType.IsInstanceOfType(unit))
				{
					continue;
				}
				if ((bool)InactiveOf.GetValue(unit))
				{
					inactive++;
					continue;
				}
				total += (int)CapacityOf.GetValue(unit);
				used += (int)NumItemsOf.GetValue(unit);
			}
		}
		catch (Exception error)
		{
			Fail("cells", error);
		}
		return (total, used, inactive);
	}

	// In the game's language, as the player sees the item.
	internal static string CreativeUnitName => (Owner != null && Owner.TryFind<ModItem>("CreativeStorageUnit", out var item)) ? Lang.GetItemNameValue(item.Type) : "Creative Storage Unit";

	internal static object HeartAt(Point16 position)
	{
		if (!Readable || !TileEntity.ByPosition.TryGetValue(position, out var entity) || !HeartType.IsInstanceOfType(entity))
		{
			return null;
		}
		return entity;
	}

	// MS checks a network's security against SecuritySystem.accessingPlayer, a static that is 0 outside an access context
	// (MS:MagicStorage.Common.Systems/SecuritySystem.cs:118-127, 161, 1127-1133): a call outside one runs as player 0,
	// whoever asked. The context is a ref struct reflection cannot use (M6), so the field is set around the call as
	// AccessContext.Use sets it, to the player the operation is for, as MS's own packet handler does
	// (MS:MagicStorage/NetHelper.cs:767). -1 checks nobody (AccessibleFromContext).
	private static T As<T>(int player, Func<T> call)
	{
		if (AccessingOf == null)
		{
			return call();
		}
		object before = AccessingOf.GetValue(null);
		AccessingOf.SetValue(null, player);
		try
		{
			return call();
		}
		finally
		{
			AccessingOf.SetValue(null, before);
		}
	}

	// Server and single player only, as `player`. Prefix-exact, like MS's own withdraw
	// (TEStorageUnit.WithdrawFromItemCollection).
	internal static Item Withdraw(object heart, Item lookFor, int player)
	{
		return As(player, () => (Item)WithdrawOf.Invoke(heart, new object[2] { lookFor, false }));
	}

	// Leaves what did not fit in `item`. A restore passes -1: the items go back where they came from, and the player
	// they were for may have left.
	internal static void Deposit(object heart, Item item, int player)
	{
		As(player, () => DepositOf.Invoke(heart, new object[1] { item }));
	}

	// Server: MS clears a heart's "window open" flag when the client says so or the heart is mined
	// (MS:MagicStorage.Components/StorageHeart.cs:83); its disconnect hook reads the client's own state, which the server
	// does not have (MS:MagicStorage/StoragePlayer.cs:211-214). A player who left with the window open would leave the
	// flag to whoever takes the slot next, and AddSession would count that heart as theirs from anywhere.
	internal static void ForgetSession(int player)
	{
		if (UsingOf == null)
		{
			return;
		}
		try
		{
			foreach (TileEntity entity in TileEntity.ByID.Values)
			{
				if (HeartType.IsInstanceOfType(entity) && UsingOf.GetValue(entity) is bool[] users && player < users.Length)
				{
					users[player] = false;
				}
			}
		}
		catch (Exception error)
		{
			Fail("write", error);
		}
	}

	// Viewers' MS windows refresh and the server's MS audit log records the player, as for MS's own operations
	// (TEStorageHeart.processClientOperations). Both are no-ops in single player.
	internal static void AfterChange(object heart, int player, List<Item> withdrawn, List<Item> deposited, HashSet<int> types)
	{
		if (!Writable)
		{
			return;
		}
		try
		{
			if (withdrawn != null)
			{
				foreach (Item item in withdrawn)
				{
					AuditWithdrawOf.Invoke(null, new object[3] { player, heart, item });
				}
			}
			if (deposited != null)
			{
				foreach (Item item in deposited)
				{
					AuditDepositOf.Invoke(null, new object[3] { player, heart, item });
				}
			}
			RefreshOf.Invoke(null, new object[3] { ((TileEntity)heart).Position, false, types });
		}
		catch (Exception error)
		{
			Fail("write", error);
		}
	}

	// The stations of the counted Crafting Interfaces, applied to the player the way MS's crafting window does
	// (CraftingGUI.AnalyzeIngredients / ExecuteInCraftingGuiEnvironment): adjTile is swapped for a copy with the
	// stations added, the liquid, alchemy and biome flags are ORed in. Always paired with EndStations in a finally.
	private static bool[] SavedTiles;

	private static bool[] MergedTiles = Array.Empty<bool>();

	private static (bool Water, bool Lava, bool Honey, bool Shimmer, bool Alchemy, bool Snow, bool Graveyard) SavedFlags;

	internal static bool BeginStations(Player player, List<List<Item>> stations)
	{
		if (SavedTiles != null || stations == null || stations.Count == 0)
		{
			return false;
		}
		bool[] own = player.adjTile;
		if (MergedTiles.Length != own.Length)
		{
			MergedTiles = new bool[own.Length];
		}
		Array.Copy(own, MergedTiles, own.Length);
		SavedTiles = own;
		SavedFlags = (player.adjWater, player.adjLava, player.adjHoney, player.adjShimmer, player.alchemyTable, player.ZoneSnow, player.ZoneGraveyard);
		player.adjTile = MergedTiles;
		bool water = false, lava = false, honey = false, shimmer = false, alchemy = false, snow = false, graveyard = false;
		bool[] tiles = MergedTiles;
		foreach (List<Item> list in stations)
		{
			foreach (Item item in list)
			{
				if (item == null || item.IsAir)
				{
					continue;
				}
				int tile = item.createTile;
				if (tile >= 0 && tile < tiles.Length)
				{
					AddStation(player, tiles, tile);
					water |= TileID.Sets.CountsAsWaterSource[tile];
					lava |= TileID.Sets.CountsAsLavaSource[tile];
					honey |= TileID.Sets.CountsAsHoneySource[tile];
					shimmer |= TileID.Sets.CountsAsShimmerSource[tile];
					alchemy |= tiles[355];
					graveyard |= tiles[85];
				}
				switch (item.type)
				{
				case ItemID.WaterBucket:
				case ItemID.BottomlessBucket:
					water = true;
					break;
				case ItemID.LavaBucket:
				case ItemID.BottomlessLavaBucket:
					lava = true;
					break;
				case ItemID.HoneyBucket:
				case ItemID.BottomlessHoneyBucket:
					honey = true;
					break;
				}
				if (item.type == SnowEmulator)
				{
					snow = true;
				}
				else if (item.type == BiomeGlobe)
				{
					snow = graveyard = water = lava = honey = true;
					tiles[TileID.Campfire] = true;
					tiles[TileID.DemonAltar] = true;
				}
			}
		}
		if (CraftingTile >= 0 && CraftingTile < tiles.Length)
		{
			tiles[CraftingTile] = true;
		}
		player.adjWater |= water;
		player.adjLava |= lava;
		player.adjHoney |= honey;
		player.adjShimmer |= shimmer;
		player.alchemyTable |= alchemy;
		player.ZoneSnow |= snow;
		player.ZoneGraveyard |= graveyard;
		return true;
	}

	// One station's tile and what it counts as, as Player.AdjTiles adds a placed one (the forges, anvils and tables,
	// then mods through TileLoader.AdjTiles), plus MS's rule that a table or work bench stands for a chair. Mods write
	// through player.adjTile, so it must be `tiles` during the call.
	internal static void AddStation(Player player, bool[] tiles, int tile)
	{
		tiles[tile] = true;
		switch (tile)
		{
		case 77:
		case 302:
			tiles[17] = true;
			break;
		case 133:
			tiles[17] = true;
			tiles[77] = true;
			break;
		case 134:
			tiles[16] = true;
			break;
		case 354:
		case 469:
		case 487:
			tiles[14] = true;
			break;
		case 355:
			tiles[13] = true;
			tiles[14] = true;
			break;
		}
		TileLoader.AdjTiles(player, tile);
		if (tiles[18] || tiles[14] || tiles[469])
		{
			tiles[15] = true;
		}
	}

	// While BeginStations is in effect: the player's own adjacency and flags under the merged ones.
	internal static bool[] OwnTiles => SavedTiles;

	internal static (bool Water, bool Lava, bool Honey, bool Shimmer, bool Alchemy, bool Snow, bool Graveyard) OwnFlags => SavedFlags;

	internal static int CraftingAccessTile => CraftingTile;

	internal static void EndStations(Player player)
	{
		if (SavedTiles == null)
		{
			return;
		}
		player.adjTile = SavedTiles;
		SavedTiles = null;
		(player.adjWater, player.adjLava, player.adjHoney, player.adjShimmer, player.alchemyTable, player.ZoneSnow, player.ZoneGraveyard) = SavedFlags;
	}

	// A Magic Storage window the player opened stays open under the book: hidden (ModifyInterfaceLayers), without
	// mouse input and without keyboard focus, and back when the book closes.
	internal static bool HideNativeUI
	{
		get
		{
			if (!CanHide || !RecipeBookSystem.Open)
			{
				return false;
			}
			try
			{
				return SessionHeart.Invoke(LocalStoragePlayer.GetValue(null), null) != null && ((bool)StorageOpen.Invoke(null, null) || (bool)CraftingOpen.Invoke(null, null));
			}
			catch (Exception error)
			{
				Fail("hide", error);
				return false;
			}
		}
	}

	private static bool BlocksInventory => RecipeBookSystem.Open && RecipeBookSystem.MouseOver;

	private static void BlockLeftClick(On_ItemSlot.orig_LeftClick_ItemArray_int_int orig, Item[] inv, int context, int slot)
	{
		if (!BlocksInventory)
		{
			orig(inv, context, slot);
		}
	}

	private static void BlockRightClick(On_ItemSlot.orig_RightClick_ItemArray_int_int orig, Item[] inv, int context, int slot)
	{
		if (!BlocksInventory)
		{
			orig(inv, context, slot);
		}
	}

	private static void UpdateNativeInterface(On_UserInterface.orig_Update orig, UserInterface self, GameTime time)
	{
		if (!HideNativeUI || self != InterfaceField.GetValue(null))
		{
			orig(self, time);
			return;
		}
		(int, int, bool, bool, bool, bool, int, int) mouse = (Main.mouseX, Main.mouseY, Main.mouseLeft, Main.mouseRight, Main.mouseLeftRelease, Main.mouseRightRelease, PlayerInput.MouseX, PlayerInput.MouseY);
		object current = CurrentMouseField.GetValue(null);
		object old = OldMouseField.GetValue(null);
		MouseState quiet = new MouseState(-10000, -10000, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
		try
		{
			Main.mouseX = Main.mouseY = PlayerInput.MouseX = PlayerInput.MouseY = -10000;
			Main.mouseLeft = Main.mouseRight = Main.mouseLeftRelease = Main.mouseRightRelease = false;
			CurrentMouseField.SetValue(null, quiet);
			OldMouseField.SetValue(null, quiet);
			orig(self, time);
		}
		finally
		{
			(Main.mouseX, Main.mouseY, Main.mouseLeft, Main.mouseRight, Main.mouseLeftRelease, Main.mouseRightRelease, PlayerInput.MouseX, PlayerInput.MouseY) = mouse;
			CurrentMouseField.SetValue(null, current);
			OldMouseField.SetValue(null, old);
		}
	}

	// MS's search boxes keep the keyboard while its hidden window is open; the book takes it back.
	public override void PostUpdateInput()
	{
		if (!HideNativeUI)
		{
			return;
		}
		try
		{
			object focused = Focused.Invoke(null, null);
			object actor = (focused == null) ? null : ActorProperty.GetValue(focused);
			if (actor?.GetType().Assembly == Owner.Code)
			{
				bool typing = RecipeBookSystem.Typing && Main.blockInput;
				Unfocus.Invoke(focused, null);
				if (typing)
				{
					Main.blockInput = true;
				}
			}
		}
		catch (Exception error)
		{
			Fail("hide", error);
		}
	}

	// Stand: c call p=ModernRecipeBook.Storage.BookMagicStorage.AddStation a=<x> a=<y> a=<item type> puts a station item
	// into the Crafting Interface whose top-left tile is (x, y), as MS's station slots do.
	internal static string AddStation(int x, int y, int type)
	{
		if (!Readable || !TileEntity.ByPosition.TryGetValue(new Point16(x, y), out var entity) || !CraftingType.IsInstanceOfType(entity))
		{
			return "no Crafting Interface at " + x + "," + y;
		}
		List<Item> stations = (List<Item>)StationsOf.GetValue(entity);
		stations.Add(new Item(type));
		return stations.Count + " stations";
	}

	public override void Unload()
	{
		if (Hooked)
		{
			On_UserInterface.Update -= UpdateNativeInterface;
			On_ItemSlot.LeftClick_ItemArray_int_int -= BlockLeftClick;
			On_ItemSlot.RightClick_ItemArray_int_int -= BlockRightClick;
		}
		Hooked = Installed = Readable = Writable = CanHide = Countable = Portable = CoinsFirst = false;
		SectionsAsked.Clear();
		Owner = null;
		SavedTiles = null;
		MergedTiles = Array.Empty<bool>();
	}
}
