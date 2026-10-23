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

	// Last known availability per recipe index, and which of them were checked since storage last changed. While MS
	// refreshes, the book keeps showing the last answer instead of flashing "unavailable".
	private static readonly Dictionary<int, bool> Availability = new Dictionary<int, bool>();

	private static readonly HashSet<int> Checked = new HashSet<int>();

	// Reflection resolved once in PostSetupContent; the two getters MS is asked about several times a frame are
	// delegates (no boxing).
	private static PropertyInfo LocalPlayerProperty;

	private static Func<bool> IsRefreshing;

	private static Func<bool> IsRefreshRequested;

	private static FieldInfo InterfaceField;

	private static FieldInfo CurrentMouseField;

	private static FieldInfo OldMouseField;

	private static PropertyInfo ActorProperty;

	private static FieldInfo InfiniteField;

	private static FieldInfo CreativeField;

	private static FieldInfo StorageNameField;

	private static FieldInfo CraftAmountField;

	// Updates in a row the adapter has been waiting on MS; the "Syncing" cue shows only past ShowSyncAfter, so the
	// short refreshes MS runs after every change never blink the status.
	private static int WaitingTicks;

	private const int ShowSyncAfter = 30;

	private static int ItemsSignature;

	internal static readonly List<Item> Items = new List<Item>();

	// Craft source: Magic Storage while a Crafting Interface is connected, the inventory otherwise. While connected the
	// player may pick the inventory instead; that lasts until the crafting connection drops or the book is reopened.
	private static bool PreferInventory;

	internal static bool UseForCrafting => CraftingConnected && !PreferInventory;

	internal static bool CanChooseSource => CraftingConnected;

	private static readonly BookStorageSnapshot DisplayMaterials = new BookStorageSnapshot();

	private static int AutoDelay;

	internal static string Message = "";

	private static bool SnapshotDirty = true;

	private static bool Hooked;

	private static bool CraftCacheReady;

	private static bool NativeUpdated;

	private static int RefreshRevision;

	private static bool LogNextRefresh;

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

	private static object Player => LocalPlayerProperty?.GetValue(null);

	private static bool Busy
	{
		get
		{
			if (Supported)
			{
				return IsRefreshing();
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
				return IsRefreshRequested();
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

	// Only until the first crafting counts arrive after connecting; later refreshes keep the previous counts.
	internal static bool CraftInitializing
	{
		get
		{
			if (CraftingConnected)
			{
				return !DisplayMaterials.HasValue;
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
						if (Updating || CraftInitializing)
						{
							return T("Обновление хранилища…", "Updating storage…");
						}
						if (!CraftingConnected)
						{
							return T("Для крафта нужен интерфейс крафта", "Needs a Crafting Interface to craft");
						}
						return T("Хранилище готово к крафту", "Storage ready for crafting");
					}
					return T("Рядом нет сети хранилища", "No storage network nearby");
				}
				return ConnectionStatus;
			}
			return T("Magic Storage не установлен", "Magic Storage is not installed");
		}
	}

	// Support off after it had worked (an error mid-game) reads differently from a version whose members don't bind.
	internal static string ConnectionStatus => (Installed && !Supported && Hooked) ? T("Связь с хранилищем отключена (client.log)", "Storage support is off (see client.log)") : BookStoragePresentation.Connection(Installed, Supported, Connected, Ru, OwnerMod?.Version.ToString() ?? "");

	// A busy cue for waits that last (half a second or more), never for MS's routine refreshes.
	internal static bool Updating
	{
		get
		{
			if (Connected)
			{
				return WaitingTicks > ShowSyncAfter;
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

	// The name the player gave the Storage Heart, if any.
	internal static string NetworkName
	{
		get
		{
			if (!Connected || LastHeart == null)
			{
				return "";
			}
			return ((StorageNameField.GetValue(LastHeart) as string) ?? "").Trim();
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
		Checked.Clear();
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
			IEnumerable<int> infinite = (IEnumerable<int>)InfiniteField.GetValue(inventory);
			bool allInfinite = (bool)CreativeField.GetValue(inventory);
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

	// The book's side of Magic Storage's UI: a dedicated server never loads it.
	public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

	public override void PostSetupContent()
	{
		if (!ModLoader.TryGetMod("MagicStorage", out var mod))
		{
			return;
		}
		Installed = true;
		OwnerMod = mod;
		try
		{
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
			ActorProperty = Property(inputState, "Actor", Instance);
			LocalPlayerProperty = Property(PlayerType, "LocalPlayer", Static);
			Bind("heart", PlayerType, "GetStorageHeart");
			Bind("permission", PlayerType, "CanAccessCurrentNetwork");
			Bind("access", PlayerType, "ViewingStorage");
			Bind("close", PlayerType, "CloseStorage");
			Bind("storageUI", UIType, "IsStorageUIOpen");
			Bind("craftUI", UIType, "IsCraftingUIOpen");
			Bind("refresh", UIType, "RefreshItems");
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
			InfiniteField = Field(inventoryType, "isItemInfinite", Instance);
			CreativeField = Field(inventoryType, "creativeUnitPresent", Instance);
			StorageNameField = Field(Get("Components.TEStorageHeart"), "storageName", Instance);
			CraftAmountField = Field(CraftType, "craftAmountTarget", Static);
			InterfaceField = Field(UIType, "uiInterface", Static);
			CurrentMouseField = Field(StorageType, "curMouse", Static);
			OldMouseField = Field(StorageType, "oldMouse", Static);
			IsRefreshing = Property(UIType, "CurrentlyRefreshing", Static).GetGetMethod(nonPublic: true).CreateDelegate<Func<bool>>();
			IsRefreshRequested = Property(UIType, "RefreshUI", Static).GetGetMethod(nonPublic: true).CreateDelegate<Func<bool>>();
			RefreshEvent = UIType.GetEvent("OnRefresh", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMemberException("MagicUI.OnRefresh");
			RefreshedHandler = OnRefreshed;
			RefreshEvent.AddEventHandler(null, RefreshedHandler);
			On_UserInterface.Update += UpdateNativeInterface;
			On_ItemSlot.LeftClick_ItemArray_int_int += BlockLeftClick;
			On_ItemSlot.RightClick_ItemArray_int_int += BlockRightClick;
			Hooked = (Supported = true);
			// Any version whose members all bind is accepted (a missing member throws above and turns support off);
			// only 0.7.0.11 has been checked by hand.
			if (mod.Version == new Version(0, 7, 0, 11))
			{
				Mod.Logger.Info("Magic Storage adapter enabled for Magic Storage 0.7.0.11.");
			}
			else
			{
				Mod.Logger.Warn($"Magic Storage adapter enabled for unverified Magic Storage version {mod.Version} (checked against 0.7.0.11).");
			}
		}
		catch (Exception error)
		{
			Fail(error);
		}
		Type Get(string name)
		{
			return mod.Code.GetType("MagicStorage." + name, throwOnError: true);
		}
		static FieldInfo Field(Type type, string name, BindingFlags flags)
		{
			return type.GetField(name, flags) ?? throw new MissingFieldException(type.FullName, name);
		}
		static PropertyInfo Property(Type type, string name, BindingFlags flags)
		{
			return type.GetProperty(name, flags) ?? throw new MissingMemberException(type.FullName, name);
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
		if (!HideNativeUI || self != InterfaceField.GetValue(null))
		{
			orig(self, time);
			return;
		}
		(int, int, bool, bool, bool, bool, int, int) mouse = (Main.mouseX, Main.mouseY, Main.mouseLeft, Main.mouseRight, Main.mouseLeftRelease, Main.mouseRightRelease, PlayerInput.MouseX, PlayerInput.MouseY);
		FieldInfo currentField = CurrentMouseField;
		FieldInfo oldField = OldMouseField;
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
			if (Cooldown > 0)
			{
				Cooldown--;
			}
			object player = Player;
			Point16 access = (Point16)Call("access", player);
			object heart = Call("heart", player);
			bool connected = !Main.gameMenu && Main.playerInventory && Main.LocalPlayer.active && !Main.LocalPlayer.dead && heart != null && (bool)Call("permission", player) && ((bool)Call("storageUI", null) || (bool)Call("craftUI", null));
			bool crafting = connected && (bool)Call("craftUI", null);
			if (connected != Connected || crafting != CraftingConnected || access != LastAccess || heart != LastHeart)
			{
				// Another access point of the same network keeps what was read from it (no blink while walking along
				// it); a new network or a lost connection starts over.
				bool sameStorage = connected && Connected && heart == LastHeart;
				bool keepCounts = sameStorage && crafting && CraftingConnected;
				Connected = connected;
				CraftingConnected = crafting;
				LastAccess = access;
				LastHeart = heart;
				if (!sameStorage)
				{
					Items.Clear();
					ItemsVersion++;
					ItemsSignature = 0;
					Message = "";
					Availability.Clear();
				}
				if (!keepCounts)
				{
					DisplayMaterials.Clear();
				}
				PendingCraft = null;
				PendingBatches = 1;
				CraftCacheReady = false;
				NativeUpdated = false;
				WaitingTicks = 0;
				PreferInventory &= crafting;
				Invalidate();
				if (connected)
				{
					// One ordinary refresh so the first counts arrive even when MS was opened (and refreshed) before
					// the book; after that MS refreshes by itself whenever the storage changes.
					LogNextRefresh = true;
					Call("setRefresh", null, false);
				}
			}
			if (!Connected)
			{
				return;
			}
			bool waiting = Busy || RefreshRequested || PendingCraft != null || CraftInitializing;
			WaitingTicks = (waiting ? (WaitingTicks + 1) : 0);
			object focused = Call("focused", null);
			object actor = ((focused == null) ? null : ActorProperty.GetValue(focused));
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
			if (PendingCraft != null)
			{
				if (++PendingAge > 600)
				{
					PendingCraft = null;
					PendingBatches = 1;
					BookCrafting.LastMessage = T("Хранилище не ответило; повторите", "Storage did not respond; try again");
				}
				else if (CraftReady && Cooldown == 0)
				{
					FinishCraft();
				}
			}
			if (SnapshotDirty && !Busy)
			{
				RebuildItems(heart);
			}
		}
		catch (Exception error)
		{
			Fail(error);
		}
	}

	// Rebuilt only after MS refreshed (SnapshotDirty). Holds MS's own Item objects (no clones): the grid shows live
	// stacks, and the signature says whether anything really changed before the list is re-sorted and re-filtered.
	private static void RebuildItems(object heart)
	{
		SnapshotDirty = false;
		List<Item> stored = new List<Item>();
		int signature = 0;
		foreach (Item item in (IEnumerable)Call("stored", heart))
		{
			if (item != null && !item.IsAir)
			{
				stored.Add(item);
				signature = HashCode.Combine(signature, item.type, item.prefix, item.stack);
			}
		}
		if (signature == ItemsSignature && stored.Count == Items.Count)
		{
			return;
		}
		ItemsSignature = signature;
		Items.Clear();
		Items.AddRange(stored.OrderBy((Item item) => item.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy((Item item) => item.type).ThenBy((Item item) => item.prefix));
		DataVersion++;
		ItemsVersion++;
	}

	// MS's answer is asked once per recipe after each storage change, and only while MS is ready; in between the
	// last answer stands, so the Craft button and the craftable filters stay put while MS refreshes.
	// Without recursion: the book's rows, Max and tracker count the direct ingredients, so "craftable" must mean
	// the same; MS's recursive crafting (when enabled in MS's config) stays in MS's own window.
	internal static bool IsAvailable(Recipe recipe)
	{
		if (!CraftingConnected || recipe == null || recipe.Disabled)
		{
			return false;
		}
		try
		{
			int index = recipe.RecipeIndex;
			if (CraftReady && Checked.Add(index))
			{
				Availability[index] = (bool)Call("available", null, recipe, false);
			}
			bool value;
			return Availability.TryGetValue(index, out value) && value;
		}
		catch (Exception error)
		{
			Fail(error);
			return false;
		}
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

	internal static Dictionary<int, int> PreviewCounts(IEnumerable<int> itemTypes)
	{
		Dictionary<int, int> counts = new Dictionary<int, int>();
		foreach (int type in itemTypes.Distinct())
		{
			counts[type] = DisplayMaterials.Quantity(new int[1] { type });
		}
		return counts;
	}

	// MS keeps its crafting cache current by itself, so the craft runs at once when MS is idle; during one of its
	// refreshes the craft waits in Tick and runs as soon as MS is ready.
	internal static void QueueCraft(Recipe recipe, bool ru, int batches = 1)
	{
		if (!CraftingConnected || PendingCraft != null || recipe == null || recipe.Disabled)
		{
			return;
		}
		PendingCraft = recipe;
		PendingBatches = Math.Clamp(batches, 1, 100);
		PendingRu = ru;
		PendingAge = 0;
		if (!CraftReady || Cooldown > 0)
		{
			BookCrafting.LastMessage = (ru ? "Ждём хранилище…" : "Waiting for storage…");
			return;
		}
		try
		{
			FinishCraft();
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
		int outputCount = (int)Math.Min(2147483647L, (long)recipe.createItem.stack * (long)batches);
		// With MS's recursive crafting on, Craft makes what MS simulates for craftAmountTarget, which
		// SetSelectedRecipe resets to 1 (x5 / x10 / Max made one batch). Set it before selecting, so the ingredient
		// list MS builds there fits the amount, and again after; the second reset drops the cached simulation.
		Call("reset", null);
		CraftAmountField.SetValue(null, outputCount);
		Call("select", null, recipe);
		CraftAmountField.SetValue(null, outputCount);
		Call("reset", null);
		if (!(bool)Call("selectedAvailable", null))
		{
			CraftAmountField.SetValue(null, 1);
			ModContent.GetInstance<ModernRecipeBook>().Logger.Info("Magic Storage rejected recipe " + recipe.RecipeIndex + ": " + CacheDiagnostic());
			BookCrafting.LastMessage = (ru ? "В хранилище не хватает материалов или станций" : "Storage lacks materials or stations");
			return;
		}
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
			CraftAmountField.SetValue(null, 1);
		}
		Cooldown = 30;
		Invalidate();
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
			Connected = (CraftingConnected = false);
			Items.Clear();
			ItemsVersion++;
			DisplayMaterials.Clear();
			PendingCraft = null;
			PendingBatches = 1;
			Invalidate();
		}
	}

	// MS access points (Storage Access, Heart, Remote Access, Crafting Interface) linked to a heart, within radius tiles
	// of the player. InReach is MS's own rule for keeping a session open (StoragePlayer.ResetEffects); Locked means
	// the network wants a password from this player.
	private static List<(ModTile Tile, int X, int Y, bool Craft, int Distance, bool InReach, bool Locked, object Heart)> AccessPoints(int radius)
	{
		Player player = Main.LocalPlayer;
		int px = (int)(player.Center.X / 16f);
		int py = (int)(player.Center.Y / 16f);
		int rx = Math.Clamp(player.lastTileRangeX, 0, 40);
		int ry = Math.Clamp(player.lastTileRangeY, 0, 40);
		List<(ModTile, int, int, bool, int, bool, bool, object)> points = new List<(ModTile, int, int, bool, int, bool, bool, object)>();
		for (int x = Math.Max(0, px - radius); x <= Math.Min(Main.maxTilesX - 1, px + radius); x++)
		{
			for (int y = Math.Max(0, py - radius); y <= Math.Min(Main.maxTilesY - 1, py + radius); y++)
			{
				Tile tile = Main.tile[x, y];
				if (!tile.HasTile || tile.TileFrameX % 36 != 0 || tile.TileFrameY % 36 != 0)
				{
					continue;
				}
				ModTile modTile = TileLoader.GetTile(tile.TileType);
				if (modTile?.Mod != OwnerMod || !(modTile.Name is "CraftingAccess" or "StorageAccess" or "StorageHeart" or "RemoteAccess"))
				{
					continue;
				}
				object heart = Call("accessHeart", modTile, x, y);
				if (heart != null)
				{
					bool inReach = px >= x - rx && px <= x + rx + 1 && py >= y - ry && py <= y + ry + 1;
					points.Add((modTile, x, y, modTile.Name == "CraftingAccess", (x + 1 - px) * (x + 1 - px) + (y + 1 - py) * (y + 1 - py), inReach, !(bool)Call("heartPermission", heart), heart));
				}
			}
		}
		return points;
	}

	internal readonly record struct NearbyPoint(string Name, int Tiles, bool InReach, bool Locked);

	private static NearbyPoint? NearestPoint;

	private static uint NearestUpdate;

	// What the disconnected Storage tab names: the point Connect would use (in reach, Crafting Interface first), else
	// the nearest one within 50 tiles. Rescanned at most twice a second, only while something asks.
	internal static NearbyPoint? Nearest
	{
		get
		{
			if (!Supported || Main.gameMenu)
			{
				return null;
			}
			if (Main.GameUpdateCount - NearestUpdate >= 30 || Main.GameUpdateCount < NearestUpdate)
			{
				NearestUpdate = Main.GameUpdateCount;
				try
				{
					var best = (from point in AccessPoints(50)
						orderby point.InReach descending, point.InReach && point.Craft descending, point.Distance
						select point).FirstOrDefault();
					NearestPoint = ((best.Tile == null) ? null : new NearbyPoint?(new NearbyPoint(BookStoragePresentation.Access(best.Tile.Name, best.Craft, Ru), (int)Math.Round(Math.Sqrt(best.Distance)), best.InReach, best.Locked)));
				}
				catch (Exception error)
				{
					Fail(error);
					NearestPoint = null;
				}
			}
			return NearestPoint;
		}
	}

	internal static bool CanConnect
	{
		get
		{
			if (!Connected)
			{
				return Nearest?.InReach ?? false;
			}
			return false;
		}
	}

	internal static string NearbyText
	{
		get
		{
			NearbyPoint? point = Nearest;
			return BookStoragePresentation.Nearby(point?.Name, point?.Tiles ?? 0, point?.InReach ?? false, point?.Locked ?? false, Ru);
		}
	}

	// The player turned automatic connection off in the config: the Storage tab shows Connect / Disconnect.
	internal static bool Manual => !(ModContent.GetInstance<BookStorageConfig>()?.AutoConnect ?? true);

	// Automatic connection waits while a chest, sign or NPC dialog is open (opening MS would close it), and after the
	// player closed the inventory (MS ends its session then; reopening it would force the inventory open again)
	// until the inventory or the book is opened again.
	internal static bool Paused { get; private set; }

	internal static bool InventoryClosed { get; private set; }

	// The only access point in reach belongs to a network that wants a password.
	internal static bool NeedsPassword
	{
		get
		{
			if (!Connected && (Nearest?.InReach ?? false))
			{
				return Nearest.Value.Locked;
			}
			return false;
		}
	}

	// Tiles from the player to the connected access point.
	internal static int ConnectedTiles
	{
		get
		{
			if (!Connected)
			{
				return 0;
			}
			// Same measure as AccessPoints: the player's tile to the access point's centre tile.
			int dx = LastAccess.X + 1 - (int)(Main.LocalPlayer.Center.X / 16f);
			int dy = LastAccess.Y + 1 - (int)(Main.LocalPlayer.Center.Y / 16f);
			return (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
		}
	}

	private const int ScanEvery = 20;

	private const int SwitchAfter = 40;

	private static int SwitchTicks;

	// One entry per network (Storage Heart) the player can use, with its best access point in reach (a Crafting
	// Interface first, then the nearest). Sorted the same way: the first entry is what auto-connect picks.
	internal readonly record struct NetworkChoice(object Heart, string Name, string Access, bool Craft, int Tiles, Point16 Point);

	private static readonly List<NetworkChoice> NetworkList = new List<NetworkChoice>();

	internal static IReadOnlyList<NetworkChoice> Networks => NetworkList;

	// The network the player picked in the Storage tab; it sticks while it stays in reach.
	private static object PickedHeart;

	internal static bool IsCurrent(NetworkChoice network)
	{
		if (Connected)
		{
			return network.Heart == LastHeart;
		}
		return false;
	}

	private static void ScanNetworks()
	{
		NetworkList.Clear();
		Player player = Main.LocalPlayer;
		int reach = Math.Max(Math.Clamp(player.lastTileRangeX, 0, 40), Math.Clamp(player.lastTileRangeY, 0, 40)) + 2;
		var best = (from point in AccessPoints(reach)
			where point.InReach && !point.Locked
			orderby point.Craft descending, point.Distance
			group point by point.Heart into network
			select network.First()).ToList();
		// Unnamed networks are numbered by where their heart is, so the numbers stay put while the player moves.
		List<object> byPlace = (from point in best
			orderby ((TileEntity)point.Heart).Position.X, ((TileEntity)point.Heart).Position.Y
			select point.Heart).ToList();
		foreach (var point in best.OrderByDescending(p => p.Craft).ThenBy(p => p.Distance))
		{
			string name = ((StorageNameField.GetValue(point.Heart) as string) ?? "").Trim();
			if (name.Length == 0)
			{
				name = T("Сеть ", "Network ") + (byPlace.IndexOf(point.Heart) + 1);
			}
			NetworkList.Add(new NetworkChoice(point.Heart, name, BookStoragePresentation.Access(point.Tile.Name, point.Craft, Ru), point.Craft, (int)Math.Round(Math.Sqrt(point.Distance)), new Point16(point.X, point.Y)));
		}
	}

	// Picking a network in the Storage tab connects to it at once; auto-connect then keeps it while it is in reach.
	internal static void PickNetwork(NetworkChoice network)
	{
		if (!Supported || !NetworkList.Contains(network))
		{
			return;
		}
		PickedHeart = network.Heart;
		SwitchTicks = 0;
		try
		{
			if (network.Heart != LastHeart || !Connected)
			{
				Open(network.Point.X, network.Point.Y);
				Tick();
			}
		}
		catch (Exception error)
		{
			Fail(error);
		}
	}

	// Best access point in reach: a Crafting Interface first (crafting from storage needs one), then the nearest.
	private static (int X, int Y, bool Craft, int Distance, object Heart)? BestInReach(bool allowLocked)
	{
		Player player = Main.LocalPlayer;
		int reach = Math.Max(Math.Clamp(player.lastTileRangeX, 0, 40), Math.Clamp(player.lastTileRangeY, 0, 40)) + 2;
		var best = (from point in AccessPoints(reach)
			where point.InReach && (allowLocked || !point.Locked)
			orderby point.Craft descending, point.Distance
			select point).FirstOrDefault();
		if (best.Tile == null)
		{
			return null;
		}
		return (best.X, best.Y, best.Craft, best.Distance, best.Heart);
	}

	// MS's own opening path (see "open" in PostSetupContent); the book remembers it opened this session.
	private static void Open(int x, int y)
	{
		Call("open", null, Main.LocalPlayer, x, y, false);
		BookOpened = new Point16(x, y);
	}

	internal static bool ConnectNearby(bool automatic = false)
	{
		if (!Supported || Main.gameMenu || !Main.LocalPlayer.active || Main.LocalPlayer.dead)
		{
			return false;
		}
		try
		{
			(int X, int Y, bool Craft, int Distance, object Heart)? choice = BestInReach(!automatic);
			if (!choice.HasValue)
			{
				if (!automatic)
				{
					Message = T("Рядом нет точки доступа", "No access point nearby");
				}
				return false;
			}
			if ((Point16)Call("access", Player) == new Point16(choice.Value.X, choice.Value.Y))
			{
				RefreshStorage();
				return false;
			}
			Open(choice.Value.X, choice.Value.Y);
			if (!automatic)
			{
				Message = T("Подключение… подтвердите в Magic Storage", "Connecting… approve in Magic Storage");
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
		PreferInventory = (InventoryClosed = false);
		AutoDelay = (SwitchTicks = 0);
		UpdateBookConnection();
	}

	internal static void ChooseCraftSource(bool storage)
	{
		CancelCraft();
		PreferInventory = !storage;
	}

	internal static void UpdateBookConnection()
	{
		if (Supported && RecipeBookSystem.Open && !Main.gameMenu && Main.LocalPlayer.active && !Main.LocalPlayer.dead && !Main.LocalPlayer.ghost)
		{
			if (!Manual)
			{
				AutoConnect();
			}
			else if (--AutoDelay <= 0)
			{
				AutoDelay = ScanEvery;
				try
				{
					ScanNetworks();
				}
				catch (Exception error)
				{
					Fail(error);
				}
			}
		}
		Tick();
	}

	// While the book is open it stays on a network in reach: the player's pick while it is in reach, otherwise the
	// first of Networks (one with a Crafting Interface in reach first, then the nearest). Access points of one
	// network are one network: the book only moves onto its Crafting Interface, or to another of its points when
	// MS closes the current one (out of reach). That happens in the same update, before Tick reads the state, so
	// walking along a network never shows "Not connected". Another network replaces the current one only when it
	// offers a Crafting Interface the current lacks or is at least 2 tiles closer, and only once that held for
	// SwitchAfter updates, so standing between two networks never flips. A session the player opened in the world
	// is left alone, and a chest, sign or NPC dialog pauses it all (opening MS would close them).
	private static void AutoConnect()
	{
		try
		{
			Point16 viewing = (Point16)Call("access", Player);
			bool lost = Connected && viewing.X < 0;
			InventoryClosed = !Main.playerInventory && (InventoryClosed || Connected || BookOpened.X >= 0);
			if (--AutoDelay > 0 && !lost)
			{
				return;
			}
			AutoDelay = ScanEvery;
			Player player = Main.LocalPlayer;
			Paused = player.chest != -1 || player.sign >= 0 || player.talkNPC >= 0 || InventoryClosed;
			ScanNetworks();
			if (PickedHeart != null && !NetworkList.Any((NetworkChoice n) => n.Heart == PickedHeart))
			{
				PickedHeart = null;
			}
			NetworkChoice target = ((PickedHeart != null) ? NetworkList.First((NetworkChoice n) => n.Heart == PickedHeart) : NetworkList.FirstOrDefault());
			if (Paused || (viewing.X >= 0 && viewing != BookOpened) || target.Heart == null || target.Point == viewing)
			{
				SwitchTicks = 0;
				return;
			}
			if (viewing.X < 0)
			{
				Open(target.Point.X, target.Point.Y);
				return;
			}
			bool upgrade = target.Craft && !CraftingConnected;
			bool better = (target.Heart == LastHeart) ? upgrade : (PickedHeart != null || upgrade || (target.Craft == CraftingConnected && target.Tiles + 2 <= ConnectedTiles));
			SwitchTicks = (better ? (SwitchTicks + ScanEvery) : 0);
			if (SwitchTicks >= SwitchAfter)
			{
				SwitchTicks = 0;
				Open(target.Point.X, target.Point.Y);
			}
		}
		catch (Exception error)
		{
			Fail(error);
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
		Cooldown = (WaitingTicks = (ItemsSignature = (SwitchTicks = 0)));
		PreferInventory = false;
		PickedHeart = null;
		NetworkList.Clear();
		CraftCacheReady = (NativeUpdated = (LogNextRefresh = false));
		RefreshRevision = 0;
		Availability.Clear();
		Connected = (CraftingConnected = false);
		LastAccess = (BookOpened = Point16.NegativeOne);
		LastHeart = null;
		Items.Clear();
		ItemsVersion++;
		DisplayMaterials.Clear();
		Message = "";
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
