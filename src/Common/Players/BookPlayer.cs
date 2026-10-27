using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.UI;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernRecipeBook.Common.Players;

public sealed class BookPlayer : ModPlayer
{
	private const string CalamityDashKey = "CalamityMod/DashDoubleTapOverride";

	internal readonly HashSet<string> Favorites = new HashSet<string>(StringComparer.Ordinal);

	private BookUiState SavedUiState = new BookUiState();

	internal readonly BookRecentHistory RecentRecipes = new BookRecentHistory();

	internal readonly BookMobEncounters SeenMobs = new BookMobEncounters();

	// The Storage tab's Changed times (BookSources.ChangedAt) per world unique id, as BookSources.SnapshotChanged
	// writes them; the current world's are kept here whenever the player saves or leaves the world.
	private readonly Dictionary<string, List<string>> StorageChanged = new Dictionary<string, List<string>>(StringComparer.Ordinal);

	private const int MaxChangedWorlds = 10;

	internal void KeepChanged()
	{
		if (BookSources.ChangedWorld != null)
		{
			StorageChanged[BookSources.ChangedWorld] = BookSources.SnapshotChanged();
		}
	}

	// SnapshotChanged writes the newest first: its first line's ticks.
	private static long NewestTicks(List<string> lines)
	{
		string first = lines[0];
		return long.TryParse(first.AsSpan(first.LastIndexOf('|') + 1), out long ticks) ? ticks : 0;
	}

	private int DashTapTimer;

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		ModKeybind toggleBook = ModernRecipeBook.ToggleBook;
		// Both keys are Mouse3 by default: on the open map the press is the teleport's (no book, no mob or item lookup).
		if (toggleBook != null && toggleBook.JustPressed && !Main.gameMenu && !BookMapTeleport.TakesPress)
		{
			BookInventoryShortcut.RequestToggle();
		}
		RecipeBookSystem.QueueCardNavigation(ModernRecipeBook.ChainBack?.JustPressed ?? false, ModernRecipeBook.ChainForward?.JustPressed ?? false);
		// Unbound by default; mod keys are read here only while no text field takes the keyboard (TriggersSet.CopyInto).
		if (ModernRecipeBook.QuickSearch?.JustPressed == true && !Main.gameMenu)
		{
			RecipeBookSystem.RequestQuickSearch();
		}
	}

	public override bool HoverSlot(Item[] inventory, int context, int slot)
	{
		// Shift over an inventory item with the Storage tab open: vanilla's "into the chest" cursor.
		if (ItemSlot.ShiftInUse && BookSources.CanShiftDeposit(Player, inventory, context, slot))
		{
			Main.cursorOverride = 9;
			return true;
		}
		return BookInventoryShortcut.TryOpen(inventory, context, slot);
	}

	// Shift+click with the Storage tab open moves the whole stack into nearby storage, as into an open chest.
	public override bool ShiftClickSlot(Item[] inventory, int context, int slot)
	{
		return BookSources.ShiftDeposit(Player, inventory, context, slot);
	}

	public override void SetControls()
	{
		// Right before vanilla's inventory toggle (Player.Update): a press GuardInventoryKey kept from the inventory
		// must not toggle it, whatever trigger state reached the player.
		if (Player.whoAmI == Main.myPlayer && InventoryPressBlocked)
		{
			Player.controlInv = false;
			Player.releaseInventory = false;
		}
		if (RecipeBookSystem.BlockTrackerPress)
		{
			Player.controlUseItem = (Player.controlUseTile = false);
			Player.mouseInterface = true;
		}
		// The quick search is modal over the game too: no key or click reaches the player under it.
		if (RecipeBookSystem.QuickSearchOpen)
		{
			Player.controlUseItem = (Player.controlUseTile = (Player.controlThrow = false));
			Player.controlUp = (Player.controlDown = (Player.controlLeft = (Player.controlRight = (Player.controlJump = false))));
			Player.mouseInterface = true;
			return;
		}
		if (!RecipeBookSystem.Open)
		{
			return;
		}
		if (RecipeBookSystem.MouseOver || RecipeBookSystem.Typing)
		{
			Player.controlUseItem = false;
			Player.controlUseTile = false;
			Player.mouseInterface = true;
		}
		if (!RecipeBookSystem.CaptureKeyboard)
		{
			return;
		}
		Player.controlUseItem = (Player.controlUseTile = false);
		Player.controlUp = (Player.controlDown = (Player.controlLeft = (Player.controlRight = false)));
		Player.controlJump = (Player.controlHook = (Player.controlMount = (Player.controlThrow = false)));
		Player.controlInv = (Player.controlQuickHeal = (Player.controlQuickMana = (Player.controlCreativeMenu = false)));
	}

	// Keys the book uses come out of this update's triggers right after the game reads input: some actions
	// (quick buff, loadout switch) run before SetControls, so clearing them there is too late.
	internal static void GuardTriggers()
	{
		GuardInventoryKey();
		GuardHeldKeys();
		if (Main.gameMenu || !RecipeBookSystem.Open)
		{
			return;
		}
		// Ctrl+Enter crafts in the book (and Enter ticks a row of the station or traits menu); vanilla would open the chat on the same Enter (it waits for chatRelease,
		// which it sets again only once Enter is let go). Runs right before vanilla's chat toggle.
		KeyboardState keys = Keyboard.GetState();
		if ((keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl) || RecipeBookSystem.StationMenuOpen || RecipeBookSystem.TraitMenuOpen) && keys.IsKeyDown(Keys.Enter))
		{
			Main.chatRelease = false;
		}
		if (RecipeBookSystem.CaptureKeyboard)
		{
			BlockTriggers(CapturedTriggers);
			BlockHotbarTriggers();
		}
	}

	// Keys the book took reach no game control until they are let go, a mod's keybind included: every binding, vanilla's
	// and mods' ("CalamityMod/DashDoubleTapOverride"), is a trigger of the same set (TriggersSet.KeyStatus), so blocking by
	// key covers what a list of names cannot. Taken: a key pressed while the book holds the keyboard (a Ctrl or Alt chord,
	// a menu, the search field, How to Get), a key the book acted on (F1, the page keys: HoldNewKeys), and everything
	// while the quick search is open and still held when it closes. Without it Ctrl+Tab closed the book through Inventory
	// on Tab, Alt+F dashed through Calamity's F and Ctrl+1 picked a hotbar slot. The game reads the keyboard an update
	// late (PlayerInput.GetPressedKeys reads Main.keyState, refreshed after this hook), so a key counts as held while
	// either the live keyboard or the game's shows it. The book's own keybinds stay usable.
	private static readonly HashSet<string> HeldKeys = new HashSet<string>(StringComparer.Ordinal);

	private static KeyboardState LastLiveKeys;

	// Smart Cursor as it was before its key (Ctrl by default) went down, for RestoreSmartCursor.
	private static bool SmartRestMouse;

	private static bool SmartRestGamePad;

	private static void GuardHeldKeys()
	{
		if (!PlayerInput.Triggers.Current.SmartCursor)
		{
			SmartRestMouse = Main.SmartCursorWanted_Mouse;
			SmartRestGamePad = Main.SmartCursorWanted_GamePad;
		}
		KeyboardState live = Keyboard.GetState();
		if (RecipeBookSystem.QuickSearchOpen)
		{
			HoldPressedKeys();
		}
		else if (!Main.gameMenu && RecipeBookSystem.Open && RecipeBookSystem.CaptureKeyboard)
		{
			HoldNewKeys(live, LastLiveKeys);
		}
		LastLiveKeys = live;
		if (HeldKeys.Count == 0)
		{
			return;
		}
		MouseState mouse = Mouse.GetState();
		HeldKeys.RemoveWhere((string name) => !Down(name));
		if (HeldKeys.Count == 0 || !PlayerInput.CurrentProfile.InputModes.TryGetValue(InputMode.Keyboard, out var mode))
		{
			return;
		}
		List<string> bound = new List<string>();
		foreach (KeyValuePair<string, List<string>> pair in mode.KeyStatus)
		{
			if (!pair.Key.StartsWith("ModernRecipeBook/", StringComparison.Ordinal) && pair.Value.Exists(HeldKeys.Contains))
			{
				bound.Add(pair.Key);
			}
		}
		BlockTriggers(bound.ToArray());
		bool Down(string name)
		{
			return name switch
			{
				"Mouse1" => mouse.LeftButton == ButtonState.Pressed,
				"Mouse2" => mouse.RightButton == ButtonState.Pressed,
				"Mouse3" => mouse.MiddleButton == ButtonState.Pressed,
				"Mouse4" => mouse.XButton1 == ButtonState.Pressed,
				"Mouse5" => mouse.XButton2 == ButtonState.Pressed,
				_ => Enum.TryParse(name, out Keys key) && (live.IsKeyDown(key) || Main.keyState.IsKeyDown(key))
			};
		}
	}

	// Every key and mouse button down now (the quick search, and what is still held as it closes).
	internal static void HoldPressedKeys()
	{
		foreach (Keys key in Keyboard.GetState().GetPressedKeys())
		{
			HeldKeys.Add(key.ToString());
		}
		MouseState mouse = Mouse.GetState();
		foreach ((ButtonState state, string name) in new (ButtonState, string)[5]
		{
			(mouse.LeftButton, "Mouse1"),
			(mouse.RightButton, "Mouse2"),
			(mouse.MiddleButton, "Mouse3"),
			(mouse.XButton1, "Mouse4"),
			(mouse.XButton2, "Mouse5")
		})
		{
			if (state == ButtonState.Pressed)
			{
				HeldKeys.Add(name);
			}
		}
	}

	// The keys pressed since `previous`, not the modifiers: a chord's Ctrl or Alt keeps doing what the game binds to it,
	// and a key held from before (walking) is not taken.
	internal static void HoldNewKeys(KeyboardState keys, KeyboardState previous)
	{
		foreach (Keys key in keys.GetPressedKeys())
		{
			if (!previous.IsKeyDown(key) && key is not (Keys.LeftControl or Keys.RightControl or Keys.LeftAlt or Keys.RightAlt or Keys.LeftShift or Keys.RightShift or Keys.LeftWindows or Keys.RightWindows))
			{
				HeldKeys.Add(key.ToString());
			}
		}
	}

	// Ctrl+F: the smart-cursor key that began the shortcut toggled Smart Cursor as it went down (Player.TryToToggleSmartCursor
	// on controlSmart && releaseSmart in toggle mode; in hold mode it follows the key), so it goes back to what it was. The
	// key then stays held away from the game (GuardHeldKeys) until let go, so releasing it toggles nothing either.
	internal static void RestoreSmartCursor()
	{
		if (PlayerInput.Triggers.Current.SmartCursor)
		{
			Main.SmartCursorWanted_Mouse = SmartRestMouse;
			Main.SmartCursorWanted_GamePad = SmartRestGamePad;
		}
	}

	private static readonly string[] InventoryTrigger = new string[1] { "Inventory" };

	// The inventory key (Esc by default) closes the book the way Esc does: an open layer (help, a menu, typing,
	// How to Get) takes the press and the inventory stays; otherwise the book closes and the inventory with it.
	// With the inventory closed the press only closes the book, so it never opens the inventory instead.
	// Decided on the book as the player saw it before the press (RecipeBookSystem.OpenAtUpdate): when the key is Esc
	// the book has already acted on it; another key reaches the book on the next update through InventoryKeyPressed,
	// except while typing.
	// A blocked press stays blocked until the key is let go: vanilla toggles on the first update it sees the key down
	// after seeing it up (Player.releaseInventory), so blocking only the first update would toggle on the second.
	private static void GuardInventoryKey()
	{
		if (!PlayerInput.Triggers.Current.Inventory)
		{
			InventoryPressBlocked = false;
		}
		bool pressed = !Main.gameMenu && RecipeBookSystem.OpenAtUpdate && PlayerInput.Triggers.JustPressed.Inventory;
		// The keyboard the triggers were read from (Main.keyState is refreshed after this hook).
		bool escape = Main.keyState.IsKeyDown(Keys.Escape);
		// With Ctrl or Alt down the press is one of the book's chords (Ctrl+Tab with Inventory on Tab), not Esc.
		bool chord = Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl) || Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt);
		RecipeBookSystem.InventoryKeyPressed = pressed && !escape && !chord && !RecipeBookSystem.TypingAtUpdate;
		if (pressed && (chord || RecipeBookSystem.LayerAtUpdate || !Main.playerInventory))
		{
			InventoryPressBlocked = true;
		}
		if (InventoryPressBlocked)
		{
			BlockTriggers(InventoryTrigger);
		}
	}

	private static bool InventoryPressBlocked;


	private static readonly string[] CapturedTriggers = new string[7] { "QuickHeal", "QuickMana", "QuickBuff", "QuickMount", "Grapple", "Throw", "Inventory" };

	private static void BlockTriggers(string[] names)
	{
		foreach (TriggersSet triggers in new TriggersSet[2] { PlayerInput.Triggers.Current, PlayerInput.Triggers.JustPressed })
		{
			foreach (string name in names)
			{
				if (triggers.KeyStatus.ContainsKey(name))
				{
					triggers.KeyStatus[name] = false;
				}
			}
		}
	}

	private static void BlockHotbarTriggers()
	{
		foreach (TriggersSet triggers in new TriggersSet[2] { PlayerInput.Triggers.Current, PlayerInput.Triggers.JustPressed })
		{
			for (int slot = 1; slot <= 10; slot++)
			{
				if (triggers.KeyStatus.ContainsKey("Hotbar" + slot))
				{
					triggers.KeyStatus["Hotbar" + slot] = false;
				}
			}
		}
	}

	// Calamity turns off double-tap dashing while its dash key is bound. On a double tap of left/right this presses
	// that key for the tick: Calamity's dash (its PostUpdateRunSpeeds) and vanilla dashes (DashMovement, through its
	// DoCommonDashHandle hook) run later in this same Player.Update. Here every SetControls (book keyboard capture)
	// and the frozen/webbed/stoned reset have already cleared controls, so swallowed presses never count as taps.
	// Cooldowns stay Calamity's; Triggers.Update recomputes JustPressed next tick.
	public override void PostUpdateMiscEffects()
	{
		// Runs for every player on every client and on the server; only the local player's own keys count.
		if (Player.whoAmI != Main.myPlayer)
		{
			return;
		}
		Dictionary<string, bool> justPressed = PlayerInput.Triggers.JustPressed.KeyStatus;
		if (!ModContent.GetInstance<BookConfig>().DoubleTapDash || !justPressed.ContainsKey(CalamityDashKey) || !PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus.TryGetValue(CalamityDashKey, out List<string> keys) || keys.Count == 0)
		{
			return;
		}
		// Vanilla DoCommonDashHandle timing: a tap arms 15 ticks toward its side, a second tap that way inside them dashes.
		DashTapTimer -= Math.Sign(DashTapTimer);
		int tap = (Player.controlRight && Player.releaseRight) ? 1 : ((Player.controlLeft && Player.releaseLeft) ? (-1) : 0);
		if (tap == 0)
		{
			return;
		}
		if (Math.Sign(DashTapTimer) != tap)
		{
			DashTapTimer = 15 * tap;
			return;
		}
		DashTapTimer = 0;
		justPressed[CalamityDashKey] = true;
	}

	public override void SaveData(TagCompound tag)
	{
		tag["FavoriteItems"] = Favorites.OrderBy((string x) => x).ToList();
		tag["RecentCraftedRecipes"] = RecentRecipes.Snapshot();
		tag["RecentCraftedMade"] = RecentRecipes.MadeSnapshot();
		tag["RecentCraftedTicks"] = RecentRecipes.TicksSnapshot();
		tag["MobEncounters"] = SeenMobs.Snapshot();
		if (Player.whoAmI == Main.myPlayer && !Main.dedServ)
		{
			KeepChanged();
		}
		// The worlds with the newest times first, at most MaxChangedWorlds.
		tag["StorageChanged"] = StorageChanged.Where((KeyValuePair<string, List<string>> pair) => pair.Value.Count > 0).OrderByDescending((KeyValuePair<string, List<string>> pair) => NewestTicks(pair.Value)).Take(MaxChangedWorlds).Select((KeyValuePair<string, List<string>> pair) => new TagCompound
		{
			["World"] = pair.Key,
			["Kinds"] = pair.Value
		}).ToList();
		if (Player.whoAmI == Main.myPlayer && RecipeBookSystem.UiStateInitialized)
		{
			SavedUiState = RecipeBookSystem.CapturePlayerUiState();
		}
		tag["ModernRecipeBookUI"] = RecipeBookSystem.WritePlayerUiState(SavedUiState);
	}

	public override void LoadData(TagCompound tag)
	{
		Favorites.Clear();
		foreach (string value in tag.GetList<string>("FavoriteItems"))
		{
			Favorites.Add(value);
		}
		RecentRecipes.Load(tag.GetList<string>("RecentCraftedRecipes"), tag.GetList<long>("RecentCraftedMade"), tag.GetList<long>("RecentCraftedTicks"));
		SeenMobs.Load(tag.GetList<string>("MobEncounters"));
		StorageChanged.Clear();
		foreach (TagCompound world in tag.GetList<TagCompound>("StorageChanged"))
		{
			string key = world.GetString("World");
			if (key.Length > 0)
			{
				StorageChanged[key] = world.GetList<string>("Kinds").ToList();
			}
		}
		SavedUiState = RecipeBookSystem.ReadPlayerUiState(tag);
	}

	public override void PostUpdate()
	{
		if (Player.whoAmI != Main.myPlayer || Main.gameMenu)
		{
			return;
		}
		if (RecipeBookSystem.UiStateInitialized)
		{
			SavedUiState = RecipeBookSystem.CapturePlayerUiState();
		}
		SeenMobs.Scan(Player);
	}

	public override void OnEnterWorld()
	{
		if (Player.whoAmI == Main.myPlayer)
		{
			RecipeBookSystem.ResetView();
			RecipeBookSystem.QueuePlayerUiRestore(SavedUiState);
			BookMapMarkers.RequestList();
			BookSpawnGates.OnEnterWorld();
			BookSources.LoadChanged(StorageChanged.GetValueOrDefault(BookSources.WorldKey));
		}
	}
}
