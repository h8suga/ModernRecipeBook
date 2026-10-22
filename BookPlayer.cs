using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
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

	private int DashTapTimer;

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		ModKeybind toggleBook = ModernRecipeBook.ToggleBook;
		if (toggleBook != null && toggleBook.JustPressed && !Main.gameMenu)
		{
			BookInventoryShortcut.RequestToggle();
		}
		RecipeBookSystem.QueueChainNavigation(ModernRecipeBook.ChainBack?.JustPressed ?? false, ModernRecipeBook.ChainForward?.JustPressed ?? false);
	}

	public override bool HoverSlot(Item[] inventory, int context, int slot)
	{
		return BookInventoryShortcut.TryOpen(inventory, context, slot);
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
		TriggersSet[] array = new TriggersSet[2]
		{
			PlayerInput.Triggers.Current,
			PlayerInput.Triggers.JustPressed
		};
		foreach (TriggersSet triggers in array)
		{
			string[] array2 = new string[7] { "QuickHeal", "QuickMana", "QuickBuff", "QuickMount", "Grapple", "Throw", "Inventory" };
			foreach (string name in array2)
			{
				if (triggers.KeyStatus.ContainsKey(name))
				{
					triggers.KeyStatus[name] = false;
				}
			}
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
		SavedUiState = RecipeBookSystem.ReadPlayerUiState(tag);
	}

	public override void PostUpdate()
	{
		if (Player.whoAmI == Main.myPlayer && !Main.gameMenu && RecipeBookSystem.UiStateInitialized)
		{
			SavedUiState = RecipeBookSystem.CapturePlayerUiState();
		}
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
