using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace ModernRecipeBook;

public sealed class BookStorage : ModSystem
{
	private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static Type PlayerType;

	private static Type UIType;

	private static Type CraftType;

	private static Type StorageType;

	private static readonly Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();

	private static readonly Dictionary<int, bool> RecipeCache = new Dictionary<int, bool>();

	internal static readonly List<Item> Items = new List<Item>();

	internal static bool UseForCrafting;

	private static readonly BookStorageSnapshot DisplayMaterials = new BookStorageSnapshot();

	private static bool AutoSuppressed;

	private static bool AutoSourcePending;

	private static int AutoDelay;

	internal static string Message = "";

	private static bool SnapshotDirty = true;

	private static bool Hooked;

	private static bool WasBusy;

	private static bool CraftCacheReady;

	private static bool NativeUpdated;

	private static int RefreshRevision;

	private static int PendingRevision;

	private static bool LogNextRefresh;

	private static int TickCount;

	private static int Cooldown;

	private static Recipe PendingCraft;

	private static int PendingBatches = 1;

	private static bool PendingRu;

	private static int PendingAge;

	private static Action RefreshedHandler;

	private static EventInfo RefreshEvent;

	private static Point16 LastAccess = Point16.NegativeOne;

	// The access point the book opened by itself. Closing the book closes that session again, so MS's own window
	// does not pop up; sessions the player opened, and "Open Magic Storage", are left alone.
	private static Point16 BookOpened = Point16.NegativeOne;

	private static Mod OwnerMod;

	private static object LastHeart;

	internal static bool Installed { get; private set; }

	internal static bool Supported { get; private set; }

	internal static bool Connected { get; private set; }

	internal static bool CraftingConnected { get; private set; }

	internal static int DataVersion { get; private set; }

	internal static int ItemsVersion { get; private set; }

	private static bool Ru => Language.ActiveCulture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

	private static object Player => PlayerType?.GetProperty("LocalPlayer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);

	private static bool Busy
	{
		get
		{
			if (Supported)
			{
				return (bool)UIType.GetProperty("CurrentlyRefreshing", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
			}
			return false;
		}
	}

	internal static bool Ready
	{
		get
		{
			if (Connected && !Busy && Cooldown == 0)
			{
				return PendingCraft == null;
			}
			return false;
		}
	}

	internal static bool HideNativeUI
	{
		get
		{
			if (RecipeBookSystem.Open)
			{
				return Connected;
			}
			return false;
		}
	}

	internal static bool IsPending => PendingCraft != null;

	private static bool RefreshRequested
	{
		get
		{
			if (Supported)
			{
				return (bool)UIType.GetProperty("RefreshUI", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
			}
			return false;
		}
	}

	internal static bool CraftReady
	{
		get
		{
			if (CraftingConnected && CraftCacheReady && !Busy)
			{
				return !RefreshRequested;
			}
			return false;
		}
	}

	internal static bool CraftInitializing
	{
		get
		{
			if (CraftingConnected)
			{
				return !CraftReady;
			}
			return false;
		}
	}

	internal static string Status
	{
		get
		{
			if (Installed)
			{
				if (Supported)
				{
					if (Connected)
					{
						if (Cooldown <= 0)
						{
							if (!Busy && !IsPending && !RefreshRequested && (!CraftingConnected || CraftCacheReady))
							{
								if (!CraftingConnected)
								{
									return T("Для крафта нужен интерфейс крафта", "Needs a Crafting Interface to craft");
								}
								return T("Хранилище готово к крафту", "Storage ready for crafting");
							}
							return T("Обновление хранилища…", "Updating storage…");
						}
						return T("Синхронизация…", "Syncing…");
					}
					return T("Рядом нет сети хранилища", "No storage network nearby");
				}
				return T("Нужен Magic Storage 0.7.0.11", "Needs Magic Storage 0.7.0.11");
			}
			return T("Magic Storage не установлен", "Magic Storage is not installed");
		}
	}

	internal static string ConnectionStatus => BookStoragePresentation.Connection(Installed, Supported, Connected, Ru);

	internal static bool Updating
	{
		get
		{
			if (Connected)
			{
				if (!Busy && !RefreshRequested && !IsPending && Cooldown <= 0)
				{
					return CraftInitializing;
				}
				return true;
			}
			return false;
		}
	}

	internal static string AccessName
	{
		get
		{
			if (!Connected)
			{
				return "";
			}
			string name = null;
			if (LastAccess.X >= 0 && LastAccess.Y >= 0 && LastAccess.X < Main.maxTilesX && LastAccess.Y < Main.maxTilesY)
			{
				Tile tile = Main.tile[LastAccess.X, LastAccess.Y];
				if (tile.HasTile)
				{
					ModTile access = TileLoader.GetTile(tile.TileType);
					if (access?.Mod == OwnerMod)
					{
						name = access.Name;
					}
				}
			}
			return BookStoragePresentation.Access(name, CraftingConnected, Ru);
		}
	}

	internal static string AccessCoordinates
	{
		get
		{
			if (!Connected)
			{
				return "";
			}
			return BookStoragePresentation.Coordinates(LastAccess.X, LastAccess.Y, Ru);
		}
	}

	internal static string ConnectionDetails
	{
		get
		{
			if (!Connected)
			{
				return ConnectionStatus + ((Message.Length > 0) ? ("\n" + Message) : "");
			}
			string text = ConnectionStatus + "\n" + AccessName + "\n" + AccessCoordinates;
			string network = LastHeart?.GetType().GetField("storageName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(LastHeart) as string;
			if (!string.IsNullOrWhiteSpace(network))
			{
				text = text + "\n" + T("Сеть: ", "Network: ") + network;
			}
			if (Message.Length > 0)
			{
				text = text + "\n" + Message;
			}
			return text;
		}
	}

	private static bool BlocksInventory
	{
		get
		{
			if (RecipeBookSystem.Open)
			{
				return RecipeBookSystem.MouseOver;
			}
			return false;
		}
	}

	private static string T(string ru, string en)
	{
		if (!Ru)
		{
			return en;
		}
		return ru;
	}

	private static void Bind(string key, Type type, string name, params Type[] args)
	{
		Methods[key] = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, args, null) ?? throw new MissingMethodException(type.FullName, name);
	}

	private static object Call(string key, object target = null, params object[] args)
	{
		return Methods[key].Invoke(target, args);
	}

	private static void Invalidate()
	{
		SnapshotDirty = true;
		RecipeCache.Clear();
		DataVersion++;
	}

	private static void OnRefreshed()
	{
		RefreshRevision++;
		CraftCacheReady = Connected && CraftingConnected && NativeUpdated && !Busy && !RefreshRequested;
		if (CraftCacheReady)
		{
			object inventory = Call("inventory", null, false);
			IEnumerable<(int, int)> counts = (IEnumerable<(int, int)>)Call("enumerate", inventory);
			IEnumerable<int> infinite = (IEnumerable<int>)inventory.GetType().GetField("isItemInfinite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(inventory);
			bool allInfinite = (bool)inventory.GetType().GetField("creativeUnitPresent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(inventory);
			DisplayMaterials.Publish(counts, infinite, allInfinite);
		}
		Invalidate();
		if (LogNextRefresh && CraftCacheReady)
		{
			LogNextRefresh = false;
			ModContent.GetInstance<ModernRecipeBook>().Logger.Info("Magic Storage craft cache initialized: " + CacheDiagnostic());
		}
	}

	private static string CacheDiagnostic()
	{
		int cachedTypes = (CraftType.GetField("itemCounts", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null) as IDictionary)?.Count ?? (-1);
		int physicalStacks = (Connected ? ((IEnumerable)Call("stored", Call("heart", Player))).Cast<Item>().Count((Item item) => item != null && !item.IsAir) : 0);
		return $"connected={Connected}, crafting={CraftingConnected}, nativeUpdated={NativeUpdated}, busy={Busy}, requested={RefreshRequested}, ready={CraftCacheReady}, revision={RefreshRevision}, storedStacks={physicalStacks}, cachedTypes={cachedTypes}";
	}

	public override void PostSetupContent()
	{
		if (Main.dedServ || !ModLoader.TryGetMod("MagicStorage", out var mod))
		{
			return;
		}
		Installed = true;
		OwnerMod = mod;
		try
		{
			if (mod.Version != new Version(0, 7, 0, 11))
			{
				throw new NotSupportedException($"MagicStorage {mod.Version}; adapter was checked against 0.7.0.11");
			}
			PlayerType = Get("StoragePlayer");
			UIType = Get("Common.Systems.MagicUI");
			CraftType = Get("CraftingGUI");
			StorageType = Get("StorageGUI");
			if (!ModLoader.TryGetMod("SerousCommonLib", out var inputMod))
			{
				throw new MissingMemberException("MagicStorage input dependency is missing");
			}
			Type inputTracker = inputMod.Code.GetType("SerousCommonLib.API.Input.TextInputTracker", throwOnError: true);
			Type inputState = inputMod.Code.GetType("SerousCommonLib.API.Input.TextInputState", throwOnError: true);
			Bind("focused", inputTracker, "GetFocusedState");
			Bind("unfocus", inputState, "Unfocus");
			if (inputState.GetProperty("Actor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null)
			{
				throw new MissingMemberException("TextInputState.Actor");
			}
			Bind("heart", PlayerType, "GetStorageHeart");
			Bind("permission", PlayerType, "CanAccessCurrentNetwork");
			Bind("access", PlayerType, "ViewingStorage");
			Bind("close", PlayerType, "CloseStorage");
			Bind("storageUI", UIType, "IsStorageUIOpen");
			Bind("craftUI", UIType, "IsCraftingUIOpen");
			Bind("refresh", UIType, "RefreshItems");
			Bind("check", UIType, "CheckRefresh");
			Bind("pulse", UIType, "PulseWatchdogs");
			Bind("setRefresh", UIType, "SetRefresh", typeof(bool));
			Bind("available", CraftType, "IsAvailable", typeof(Recipe), typeof(bool));
			Bind("select", CraftType, "SetSelectedRecipe", typeof(Recipe));
			Bind("reset", CraftType, "ResetRecentRecipeCache");
			Bind("selectedAvailable", CraftType, "IsCurrentRecipeFullyAvailable");
			Bind("craft", CraftType, "Craft", typeof(int));
			Bind("inventory", CraftType, "GetCurrentInventory", typeof(bool));
			Bind("deposit", StorageType, "TryDeposit", typeof(Item));
			Bind("depositAll", StorageType, "TryDepositAll", typeof(bool));
			Bind("restock", StorageType, "TryRestock");
			Bind("withdraw", StorageType, "DoWithdraw", typeof(Item), typeof(bool), typeof(bool));
			Bind("stored", Get("Components.TEStorageHeart"), "GetStoredItems");
			Bind("accessHeart", Get("Components.StorageAccess"), "GetHeart", typeof(int), typeof(int));
			// Where every access point's right-click ends up when the player holds no Locator; calling RightClick
			// itself would re-link (or use up) a held Locator / Portable Access instead of opening the storage.
			Bind("open", Get("Components.StorageAccess"), "OpenStorage", typeof(Player), typeof(int), typeof(int), typeof(bool));
			Bind("heartPermission", Get("Components.TEStorageComponent"), "CanLocalClientAccess");
			Type inventoryType = Get("Common.Systems.RecurrentRecipes.AvailableRecipeObjects");
			Bind("enumerate", inventoryType, "EnumerateInventory");
			if (inventoryType.GetField("isItemInfinite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null || inventoryType.GetField("creativeUnitPresent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null)
			{
				throw new MissingMemberException("Crafting inventory infinite item contract");
			}
			Bind("quantity", Get("Common.Systems.RecurrentRecipes.AvailableRecipeObjects"), "GetIngredientQuantity", typeof(int));
			if (UIType.GetField("uiInterface", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null || UIType.GetProperty("CurrentlyRefreshing", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null || UIType.GetProperty("RefreshUI", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null || StorageType.GetField("curMouse", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null || StorageType.GetField("oldMouse", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null)
			{
				throw new MissingMemberException("MagicUI interface contract changed");
			}
			RefreshEvent = UIType.GetEvent("OnRefresh", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMemberException("MagicUI.OnRefresh");
			RefreshedHandler = OnRefreshed;
			RefreshEvent.AddEventHandler(null, RefreshedHandler);
			On_UserInterface.Update += UpdateNativeInterface;
			On_ItemSlot.LeftClick_ItemArray_int_int += BlockLeftClick;
			On_ItemSlot.RightClick_ItemArray_int_int += BlockRightClick;
			Hooked = (Supported = true);
			Mod.Logger.Info("Magic Storage adapter enabled for supplied version 0.7.0.11.");
		}
		catch (Exception error)
		{
			Fail(error);
		}
		Type Get(string name)
		{
			return mod.Code.GetType("MagicStorage." + name, throwOnError: true);
		}
	}

	private static void Fail(Exception error)
	{
		ModContent.GetInstance<ModernRecipeBook>().Logger.Error("Magic Storage adapter disabled; ordinary book remains available.", error);
		Supported = (Connected = (CraftingConnected = false));
		PendingCraft = null;
		PendingBatches = 1;
		Items.Clear();
		ItemsVersion++;
		DisplayMaterials.Clear();
		Invalidate();
		Message = T("Связь с хранилищем отключена (client.log)", "Storage support is off (see client.log)");
	}

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
		if (!HideNativeUI || self != UIType.GetField("uiInterface", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null))
		{
			orig(self, time);
			return;
		}
		(int, int, bool, bool, bool, bool, int, int) mouse = (Main.mouseX, Main.mouseY, Main.mouseLeft, Main.mouseRight, Main.mouseLeftRelease, Main.mouseRightRelease, PlayerInput.MouseX, PlayerInput.MouseY);
		FieldInfo currentField = StorageType.GetField("curMouse", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		FieldInfo oldField = StorageType.GetField("oldMouse", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		object current = currentField.GetValue(null);
		object old = oldField.GetValue(null);
		MouseState quiet = new MouseState(-10000, -10000, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
		try
		{
			Main.mouseX = (Main.mouseY = (PlayerInput.MouseX = (PlayerInput.MouseY = -10000)));
			Main.mouseLeft = (Main.mouseRight = (Main.mouseLeftRelease = (Main.mouseRightRelease = false)));
			currentField.SetValue(null, quiet);
			oldField.SetValue(null, quiet);
			NativeUpdated = true;
			orig(self, time);
		}
		finally
		{
			(Main.mouseX, Main.mouseY, Main.mouseLeft, Main.mouseRight, Main.mouseLeftRelease, Main.mouseRightRelease, PlayerInput.MouseX, PlayerInput.MouseY) = mouse;
			currentField.SetValue(null, current);
			oldField.SetValue(null, old);
		}
	}

	internal static void Tick(bool manageFocus = true)
	{
		if (!Supported)
		{
			return;
		}
		try
		{
			if (Cooldown > 0 && --Cooldown == 0)
			{
				RecipeCache.Clear();
				DataVersion++;
			}
			bool busy = Busy;
			if (busy != WasBusy)
			{
				WasBusy = busy;
				RecipeCache.Clear();
				DataVersion++;
			}
			object player = Player;
			Point16 access = (Point16)Call("access", player);
			object heart = Call("heart", player);
			bool connected = !Main.gameMenu && Main.playerInventory && Main.LocalPlayer.active && !Main.LocalPlayer.dead && heart != null && (bool)Call("permission", player) && ((bool)Call("storageUI", null) || (bool)Call("craftUI", null));
			bool crafting = connected && (bool)Call("craftUI", null);
			if (connected != Connected || crafting != CraftingConnected || access != LastAccess || heart != LastHeart)
			{
				Connected = connected;
				CraftingConnected = crafting;
				LastAccess = access;
				LastHeart = heart;
				Items.Clear();
				ItemsVersion++;
				DisplayMaterials.Clear();
				PendingCraft = null;
				PendingBatches = 1;
				Message = "";
				CraftCacheReady = false;
				NativeUpdated = false;
				Invalidate();
				if (connected)
				{
					LogNextRefresh = true;
					Call("setRefresh", null, true);
				}
			}
			if (!Connected)
			{
				return;
			}
			object focused = Call("focused", null);
			object actor = focused?.GetType().GetProperty("Actor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(focused);
			if (manageFocus && actor?.GetType().Assembly == OwnerMod.Code)
			{
				bool num = RecipeBookSystem.Typing && Main.blockInput;
				Call("unfocus", focused);
				if (num)
				{
					Main.blockInput = true;
				}
			}
			Call("pulse", null);
			if (++TickCount % 120 == 0 && !Busy && !RefreshRequested && PendingCraft == null)
			{
				Call("setRefresh", null, true);
			}
			if (PendingCraft != null)
			{
				if (++PendingAge > 600)
				{
					PendingCraft = null;
					PendingBatches = 1;
					BookCrafting.LastMessage = T("Хранилище не ответило; повторите", "Storage did not respond; try again");
				}
				else if (CraftReady && RefreshRevision > PendingRevision)
				{
					FinishCraft();
				}
			}
			if (!Busy && (SnapshotDirty || TickCount % 60 == 0))
			{
				List<Item> snapshot = (from Item item in (IEnumerable)Call("stored", heart)
					where item != null && !item.IsAir
					select item.Clone()).OrderBy((Item item) => item.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy((Item item) => item.type).ThenBy((Item item) => item.prefix)
					.ToList();
				bool num2 = snapshot.Count != Items.Count || snapshot.Where((Item item, int i) => i >= Items.Count || item.type != Items[i].type || item.prefix != Items[i].prefix || item.stack != Items[i].stack || item.Name != Items[i].Name).Any();
				Items.Clear();
				Items.AddRange(snapshot);
				SnapshotDirty = false;
				if (num2)
				{
					DataVersion++;
					ItemsVersion++;
				}
			}
		}
		catch (Exception error)
		{
			Fail(error);
		}
	}

	internal static bool IsAvailable(Recipe recipe)
	{
		if (!CraftReady || PendingCraft != null || Cooldown > 0 || recipe == null || recipe.Disabled)
		{
			return false;
		}
		try
		{
			if (!RecipeCache.TryGetValue(recipe.RecipeIndex, out var value))
			{
				value = (RecipeCache[recipe.RecipeIndex] = (bool)Call("available", null, recipe, true));
			}
			return value;
		}
		catch (Exception error)
		{
			Fail(error);
			return false;
		}
	}

	internal static int Quantity(Item item, RecipeGroup group)
	{
		if (!CraftingConnected)
		{
			return 0;
		}
		BookStorageSnapshot displayMaterials = DisplayMaterials;
		IEnumerable<int> types;
		if (group != null)
		{
			IEnumerable<int> validItems = group.ValidItems;
			types = validItems;
		}
		else
		{
			IEnumerable<int> validItems = new int[1] { item.type };
			types = validItems;
		}
		return displayMaterials.Quantity(types);
	}

	internal static int Quantity(IEnumerable<int> itemTypes)
	{
		if (!CraftingConnected)
		{
			return -1;
		}
		return DisplayMaterials.Quantity(itemTypes);
	}

	internal static bool IsInfinite(IEnumerable<int> itemTypes)
	{
		if (CraftingConnected)
		{
			return DisplayMaterials.IsInfinite(itemTypes);
		}
		return false;
	}

	internal static bool IsInfinite(Item item, RecipeGroup group)
	{
		if (!CraftingConnected || item == null || item.IsAir)
		{
			return false;
		}
		BookStorageSnapshot displayMaterials = DisplayMaterials;
		IEnumerable<int> types;
		if (group != null)
		{
			IEnumerable<int> validItems = group.ValidItems;
			types = validItems;
		}
		else
		{
			IEnumerable<int> validItems = new int[1] { item.type };
			types = validItems;
		}
		return displayMaterials.IsInfinite(types);
	}

	internal static Dictionary<int, int> PreviewCounts(IEnumerable<int> itemTypes)
	{
		Dictionary<int, int> counts = new Dictionary<int, int>();
		foreach (int type in itemTypes.Distinct())
		{
			counts[type] = DisplayMaterials.Quantity(new int[1] { type });
		}
		return counts;
	}

	internal static void QueueCraft(Recipe recipe, bool ru, int batches = 1)
	{
		if (!Ready || !CraftReady || recipe == null || recipe.Disabled)
		{
			return;
		}
		try
		{
			PendingCraft = recipe;
			PendingBatches = Math.Clamp(batches, 1, 100);
			PendingRu = ru;
			PendingAge = 0;
			PendingRevision = RefreshRevision;
			CraftCacheReady = false;
			BookCrafting.LastMessage = (ru ? "Проверка материалов…" : "Checking materials…");
			RecipeCache.Clear();
			Call("setRefresh", null, true);
			Call("refresh", null);
		}
		catch (Exception error)
		{
			Fail(error);
		}
	}

	private static void FinishCraft()
	{
		Recipe recipe = PendingCraft;
		int batches = Math.Clamp(PendingBatches, 1, 100);
		PendingCraft = null;
		PendingBatches = 1;
		bool ru = PendingRu;
		Player player = Main.LocalPlayer;
		if (!UseForCrafting || !Connected || !CraftingConnected || !recipeIsSafe() || !Main.mouseItem.IsAir || !Main.guideItem.IsAir || !player.ItemTimeIsZero || player.itemAnimation != 0 || player.HasLockedInventory())
		{
			BookCrafting.LastMessage = (ru ? "Крафт отменён: условия изменились" : "Craft cancelled: conditions changed");
			return;
		}
		Call("reset", null);
		Call("select", null, recipe);
		Call("reset", null);
		if (!(bool)Call("selectedAvailable", null))
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Info("Magic Storage rejected recipe " + recipe.RecipeIndex + ": " + CacheDiagnostic());
			BookCrafting.LastMessage = (ru ? "В хранилище не хватает материалов или станций" : "Storage lacks materials or stations");
			return;
		}
		int outputCount = (int)Math.Min(2147483647L, (long)recipe.createItem.stack * (long)batches);
		bool craftReturned = false;
		BookRecentCraftHooks.BeginStorageCraft(recipe);
		try
		{
			Call("craft", null, outputCount);
			craftReturned = true;
		}
		finally
		{
			BookRecentCraftHooks.EndStorageCraft(craftReturned);
		}
		Cooldown = 30;
		Invalidate();
		Call("setRefresh", null, true);
		string lastMessage;
		if (Main.netMode != 1)
		{
			lastMessage = (ru ? $"Крафт ×{batches} в хранилище" : $"Crafting ×{batches} in storage");
		}
		else
		{
			lastMessage = (ru ? $"Крафт ×{batches} отправлен серверу" : $"Craft ×{batches} sent to server");
		}
		BookCrafting.LastMessage = lastMessage;
		bool recipeIsSafe()
		{
			if (recipe != null && !recipe.Disabled && player.active && !player.dead)
			{
				return !player.ghost;
			}
			return false;
		}
	}

	internal static void DepositCursor()
	{
		if (!Ready || Main.mouseItem.IsAir)
		{
			return;
		}
		try
		{
			bool changed = (bool)Call("deposit", null, Main.mouseItem);
			string message;
			if (Main.netMode == 1)
			{
				message = T("Отправлено серверу", "Sent to server");
			}
			else
			{
				message = (changed ? T("Положено в хранилище", "Stored") : T("Не положено: нет места", "Not stored: no room"));
			}
			Message = message;
			Cooldown = 20;
			Invalidate();
		}
		catch (Exception error)
		{
			Fail(error);
		}
	}

	internal static void InventoryAction(bool? quickStack)
	{
		if (!Ready)
		{
			return;
		}
		if (!Main.mouseItem.IsAir)
		{
			Message = T("Сначала уберите предмет с курсора", "Clear the cursor first");
		}
		else if (!Main.LocalPlayer.HasLockedInventory())
		{
			try
			{
				bool changed = (quickStack.HasValue ? ((bool)Call("depositAll", null, quickStack.Value)) : ((bool)Call("restock", null)));
				string message;
				if (Main.netMode == 1)
				{
					message = T("Отправлено серверу", "Sent to server");
				}
				else if (changed)
				{
					if (quickStack.HasValue)
					{
						message = (quickStack.Value ? T("Похожие предметы сложены", "Matching items stored") : T("Сложено, кроме панели и избранного", "Stored all but hotbar and favorites"));
					}
					else
					{
						message = T("Стопки пополнены", "Stacks topped up");
					}
				}
				else
				{
					message = T("Нечего перекладывать", "Nothing to move");
				}
				Message = message;
				Cooldown = 20;
				Invalidate();
				Call("setRefresh", null, true);
			}
			catch (Exception error)
			{
				Fail(error);
			}
		}
	}

	internal static void Withdraw(Item displayItem, bool single)
	{
		if (!Ready || displayItem == null || displayItem.IsAir)
		{
			return;
		}
		if (!Main.mouseItem.IsAir)
		{
			Message = T("Сначала уберите предмет с курсора", "Clear the cursor first");
			return;
		}
		try
		{
			Item request = displayItem.Clone();
			request.stack = (single ? 1 : Math.Min(displayItem.stack, displayItem.maxStack));
			Item result = (Item)Call("withdraw", null, request, true, false);
			bool received = result != null && !result.IsAir;
			if (received)
			{
				Main.mouseItem = Main.LocalPlayer.GetItem(Main.myPlayer, result, GetItemSettings.InventoryUIToInventorySettingsShowAsNew);
			}
			string message;
			if (Main.netMode == 1)
			{
				message = T("Отправлено серверу", "Sent to server");
			}
			else if (received)
			{
				message = (Main.mouseItem.IsAir ? T("Забрано в инвентарь", "Taken to inventory") : T("Инвентарь полон: остаток на курсоре", "Inventory full: rest on cursor"));
			}
			else
			{
				message = T("Предмета уже нет в хранилище", "Item is no longer stored");
			}
			Message = message;
			Cooldown = 4;
			Invalidate();
			Call("setRefresh", null, true);
		}
		catch (Exception error)
		{
			Fail(error);
		}
	}

	internal static void RefreshStorage()
	{
		if (!Connected || Busy)
		{
			return;
		}
		try
		{
			LogNextRefresh = true;
			CraftCacheReady = false;
			Invalidate();
			Call("setRefresh", null, true);
		}
		catch (Exception error)
		{
			Fail(error);
		}
	}

	internal static void Disconnect()
	{
		if (Supported)
		{
			try
			{
				Call("close", Player);
			}
			catch (Exception error)
			{
				Fail(error);
			}
			AutoSuppressed = true;
			Connected = (CraftingConnected = false);
			Items.Clear();
			ItemsVersion++;
			DisplayMaterials.Clear();
			PendingCraft = null;
			PendingBatches = 1;
			Invalidate();
		}
	}

	internal static bool ConnectNearby(bool automatic = false)
	{
		if (!Supported || Main.gameMenu || !Main.LocalPlayer.active || Main.LocalPlayer.dead)
		{
			return false;
		}
		try
		{
			Player localPlayer = Main.LocalPlayer;
			int px = (int)(localPlayer.Center.X / 16f);
			int py = (int)(localPlayer.Center.Y / 16f);
			int rx = Math.Clamp(localPlayer.lastTileRangeX, 0, 40);
			int ry = Math.Clamp(localPlayer.lastTileRangeY, 0, 40);
			List<(ModTile, int, int, bool, int)> points = new List<(ModTile, int, int, bool, int)>();
			for (int x = Math.Max(0, px - rx); x <= Math.Min(Main.maxTilesX - 1, px + rx); x++)
			{
				for (int y = Math.Max(0, py - ry); y <= Math.Min(Main.maxTilesY - 1, py + ry); y++)
				{
					Tile tile = Main.tile[x, y];
					if (!tile.HasTile || tile.TileFrameX % 36 != 0 || tile.TileFrameY % 36 != 0)
					{
						continue;
					}
					ModTile modTile = TileLoader.GetTile(tile.TileType);
					if (modTile?.Mod != OwnerMod)
					{
						continue;
					}
					string name = modTile.Name;
					bool flag;
					switch (name)
					{
					case "CraftingAccess":
					case "StorageAccess":
					case "StorageHeart":
					case "RemoteAccess":
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					if (flag && px >= x - rx && px <= x + rx + 1 && py >= y - ry && py <= y + ry + 1)
					{
						object candidateHeart = Call("accessHeart", modTile, x, y);
						if (candidateHeart != null && (!automatic || (bool)Call("heartPermission", candidateHeart)))
						{
							points.Add((modTile, x, y, name == "CraftingAccess", (x - px) * (x - px) + (y - py) * (y - py)));
						}
					}
				}
			}
			(ModTile, int, int, bool, int) choice = points.OrderByDescending(((ModTile Tile, int X, int Y, bool Craft, int Distance) point) => point.Craft).ThenBy(((ModTile Tile, int X, int Y, bool Craft, int Distance) point) => point.Distance).FirstOrDefault();
			if (choice.Item1 == null)
			{
				if (automatic)
				{
					return false;
				}
				Message = T("Рядом нет точки доступа", "No access point nearby");
				return false;
			}
			if ((Point16)Call("access", Player) == new Point16(choice.Item2, choice.Item3))
			{
				RefreshStorage();
				return false;
			}
			Call("open", null, Main.LocalPlayer, choice.Item2, choice.Item3, false);
			BookOpened = new Point16(choice.Item2, choice.Item3);
			Message = T("Подключение… подтвердите в Magic Storage", "Connecting… approve in Magic Storage");
			if (!automatic)
			{
				AutoSuppressed = false;
				Invalidate();
				Tick();
			}
			return true;
		}
		catch (Exception error)
		{
			Fail(error);
			return false;
		}
	}

	internal static void OnBookOpened()
	{
		AutoSuppressed = false;
		AutoSourcePending = true;
		AutoDelay = 0;
		UpdateBookConnection();
	}

	internal static void ChooseCraftSource(bool storage)
	{
		AutoSourcePending = false;
		CancelCraft();
		UseForCrafting = storage;
		if (storage)
		{
			RefreshStorage();
		}
	}

	internal static void UpdateBookConnection()
	{
		Tick();
		if (!Supported || !RecipeBookSystem.Open || Main.gameMenu || !Main.LocalPlayer.active || Main.LocalPlayer.dead || Main.LocalPlayer.ghost)
		{
			return;
		}
		if (!Connected && !AutoSuppressed && --AutoDelay <= 0)
		{
			AutoDelay = 60;
			Point16 point = (Point16)Call("access", Player);
			Player player = Main.LocalPlayer;
			if (point.X < 0 && player.chest == -1 && player.sign < 0 && player.talkNPC < 0 && ConnectNearby(automatic: true))
			{
				Tick();
			}
		}
		if (CraftingConnected && AutoSourcePending)
		{
			UseForCrafting = true;
			AutoSourcePending = false;
		}
	}

	// Runs after the UI and the player's key bindings and before drawing, so the session closes in the same update
	// the book does, whichever way it was closed.
	public override void PostUpdateEverything()
	{
		if (BookOpened.X < 0 || RecipeBookSystem.Open || !Supported)
		{
			return;
		}
		try
		{
			if ((Point16)Call("access", Player) == BookOpened)
			{
				Call("close", Player);
				Recipe.FindRecipes();
			}
		}
		catch (Exception error)
		{
			Fail(error);
		}
		BookOpened = Point16.NegativeOne;
	}

	// The player asked for MS's own window: keep the session the book opened.
	internal static void HandOff()
	{
		BookOpened = Point16.NegativeOne;
	}

	internal static void CancelCraft()
	{
		if (PendingCraft != null)
		{
			BookCrafting.LastMessage = T("Крафт отменён", "Craft cancelled");
		}
		PendingCraft = null;
		PendingBatches = 1;
	}

	internal static void ClearView()
	{
		PendingCraft = null;
		PendingBatches = 1;
		Cooldown = (TickCount = 0);
		UseForCrafting = false;
		CraftCacheReady = (NativeUpdated = (WasBusy = (LogNextRefresh = false)));
		RefreshRevision = (PendingRevision = 0);
		Connected = (CraftingConnected = false);
		LastAccess = (BookOpened = Point16.NegativeOne);
		LastHeart = null;
		Items.Clear();
		ItemsVersion++;
		DisplayMaterials.Clear();
		Message = "";
		AutoSuppressed = (AutoSourcePending = false);
		AutoDelay = 0;
		Invalidate();
	}

	public override void OnWorldUnload()
	{
		ClearView();
	}

	public override void Unload()
	{
		if (Hooked)
		{
			On_UserInterface.Update -= UpdateNativeInterface;
			On_ItemSlot.LeftClick_ItemArray_int_int -= BlockLeftClick;
			On_ItemSlot.RightClick_ItemArray_int_int -= BlockRightClick;
		}
		if (RefreshEvent != null && RefreshedHandler != null)
		{
			RefreshEvent.RemoveEventHandler(null, RefreshedHandler);
		}
		ClearView();
		Hooked = (Installed = (Supported = false));
		Methods.Clear();
		PlayerType = (UIType = (CraftType = (StorageType = null)));
		OwnerMod = null;
		RefreshEvent = null;
		RefreshedHandler = null;
	}
}
