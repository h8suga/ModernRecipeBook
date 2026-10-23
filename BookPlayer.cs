using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.UI;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernRecipeBook;

public sealed class BookPlayer : ModPlayer
{
	private const string CalamityDashKey = "CalamityMod/DashDoubleTapOverride";

	internal readonly HashSet<string> Favorites = new HashSet<string>(StringComparer.Ordinal);

	private BookUiState SavedUiState = new BookUiState();

	internal readonly BookRecentHistory RecentRecipes = new BookRecentHistory();

	internal readonly BookMobEncounters SeenMobs = new BookMobEncounters();

	private int DashTapTimer;

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		ModKeybind toggleBook = ModernRecipeBook.ToggleBook;
		if (toggleBook != null && toggleBook.JustPressed && !Main.gameMenu)
		{
			BookInventoryShortcut.RequestToggle();
		}
		RecipeBookSystem.QueueCardNavigation(ModernRecipeBook.ChainBack?.JustPressed ?? false, ModernRecipeBook.ChainForward?.JustPressed ?? false);
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
		if (RecipeBookSystem.BlockTrackerPress)
		{
			Player.controlUseItem = (Player.controlUseTile = false);
			Player.mouseInterface = true;
		}
		if (!RecipeBookSystem.Open)
		{
			return;
		}
		Player.controlInv = false;
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
		if (Main.gameMenu || !RecipeBookSystem.Open)
		{
			DigitWithModifier = false;
			return;
		}
		KeyboardState keys = Keyboard.GetState();
		// Ctrl+1..5 (tabs) and Alt+1..4 (stage): vanilla picks a hotbar slot while a digit is held, so a digit
		// pressed with Ctrl or Alt stays blocked until it is released, even when the modifier is let go first.
		// The triggers can lag the live keyboard by an update, so the digit counts as held while either shows it.
		bool digit = HotbarDigits.Any(keys.IsKeyDown) || HotbarTriggerHeld();
		bool modifier = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl) || keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
		DigitWithModifier = digit && (DigitWithModifier || modifier);
		if (DigitWithModifier)
		{
			BlockHotbarTriggers();
		}
		// F1 is the book's help; what the game binds to F1 (Loadout 1 by default) waits until the book is closed.
		string[] f1 = F1Triggers();
		if (keys.IsKeyDown(Keys.F1) || f1.Any((string name) => PlayerInput.Triggers.Current.KeyStatus.TryGetValue(name, out var held) && held))
		{
			BlockTriggers(f1);
		}
		// Ctrl+Enter crafts in the book; vanilla would open the chat on the same Enter (it waits for chatRelease,
		// which it sets again only once Enter is let go). Runs right before vanilla's chat toggle.
		if ((keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl)) && keys.IsKeyDown(Keys.Enter))
		{
			Main.chatRelease = false;
		}
		if (RecipeBookSystem.CaptureKeyboard)
		{
			BlockTriggers(CapturedTriggers);
			BlockHotbarTriggers();
		}
	}

	private static readonly string[] CapturedTriggers = new string[7] { "QuickHeal", "QuickMana", "QuickBuff", "QuickMount", "Grapple", "Throw", "Inventory" };

	private static readonly Keys[] HotbarDigits = new Keys[10] { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0 };

	private static bool DigitWithModifier;

	private static string[] F1Triggers()
	{
		if (!PlayerInput.CurrentProfile.InputModes.TryGetValue(InputMode.Keyboard, out var mode))
		{
			return Array.Empty<string>();
		}
		return (from pair in mode.KeyStatus
			where pair.Value.Contains("F1")
			select pair.Key).ToArray();
	}

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

	private static bool HotbarTriggerHeld()
	{
		for (int slot = 1; slot <= 10; slot++)
		{
			if (PlayerInput.Triggers.Current.KeyStatus.TryGetValue("Hotbar" + slot, out var held) && held)
			{
				return true;
			}
		}
		return false;
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
		Dictionary<string, bool> justPressed = PlayerInput.Triggers.JustPressed.KeyStatus;
		if (Player.whoAmI != Main.myPlayer || !ModContent.GetInstance<BookConfig>().DoubleTapDash || !justPressed.ContainsKey(CalamityDashKey) || !PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus.TryGetValue(CalamityDashKey, out List<string> keys) || keys.Count == 0)
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
		tag["MobEncounters"] = SeenMobs.Snapshot();
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
		RecentRecipes.Load(tag.GetList<string>("RecentCraftedRecipes"));
		SeenMobs.Load(tag.GetList<string>("MobEncounters"));
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
		}
	}
}
