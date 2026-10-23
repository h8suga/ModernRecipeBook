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

namespace ModernRecipeBook;

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
	}

	private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static Mod Owner;

	internal static bool Installed { get; private set; }

	internal static bool Readable { get; private set; }

	internal static bool Writable { get; private set; }

	private static bool CanHide;

	private static readonly HashSet<string> AccessTiles = new HashSet<string> { "StorageHeart", "StorageAccess", "CraftingAccess", "RemoteAccess" };

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

	// The server's writes.
	private static MethodInfo WithdrawOf;

	private static MethodInfo DepositOf;

	private static FieldInfo UsingOf;

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
			Mod.Logger.Info($"Magic Storage {mod.Version}{checkedVersion}: read={Readable}, write={Writable}, hide={CanHide}.");
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
	}

	private static void BindWriting()
	{
		WithdrawOf = Method(HeartType, "Withdraw", typeof(Item), typeof(bool));
		DepositOf = Method(HeartType, "DepositItem", typeof(Item));
		UsingOf = Field(HeartType, "clientUsingHeart");
		RefreshOf = Method(Get("NetHelper"), "SendRefreshNetworkItems", typeof(Point16), typeof(bool), typeof(IEnumerable<int>));
		Type audit = Get("Common.Systems.Auditing.AuditSystem");
		AuditWithdrawOf = Method(audit, "ReportItemWithdraw", typeof(int), HeartType, typeof(Item));
		AuditDepositOf = Method(audit, "ReportItemDeposit", typeof(int), HeartType, typeof(Item));
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
		else
		{
			CanHide = false;
		}
	}

	// One pass over the tiles within `tiles` of the player: bank tiles (-2..-5, once each) and Magic Storage access
	// tiles. Each heart is listed once with its nearest access tile; a heart the player may not use counts in
	// `locked`. `stations` gets the station lists of counted Crafting Interfaces. The network the player has open in
	// MS (or, on the server, has locked in MS) is added however far it is.
	internal static void Scan(Player player, int tiles, List<int> banks, List<Network> networks, List<List<Item>> stations, out int locked)
	{
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
			Add(networks, heart).Distance = 0f;
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
				Add(networks, entity).Distance = 0f;
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

	internal static object HeartAt(Point16 position)
	{
		if (!Readable || !TileEntity.ByPosition.TryGetValue(position, out var entity) || !HeartType.IsInstanceOfType(entity))
		{
			return null;
		}
		return entity;
	}

	// Server and single player only. Prefix-exact, like MS's own withdraw (TEStorageUnit.WithdrawFromItemCollection).
	internal static Item Withdraw(object heart, Item lookFor)
	{
		return (Item)WithdrawOf.Invoke(heart, new object[2] { lookFor, false });
	}

	// Leaves what did not fit in `item`.
	internal static void Deposit(object heart, Item item)
	{
		DepositOf.Invoke(heart, new object[1] { item });
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
						alchemy = true;
						break;
					case 85:
						graveyard = true;
						break;
					}
					TileLoader.AdjTiles(player, tile);
					// MS lets a table or work bench stand for a chair as well.
					if (tiles[18] || tiles[14] || tiles[469])
					{
						tiles[15] = true;
					}
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

	// Stand: c call p=ModernRecipeBook.BookMagicStorage.AddStation a=<x> a=<y> a=<item type> puts a station item into the
	// Crafting Interface whose top-left tile is (x, y), as MS's station slots do.
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
		Hooked = Installed = Readable = Writable = CanHide = false;
		Owner = null;
		SavedTiles = null;
		MergedTiles = Array.Empty<bool>();
	}
}
