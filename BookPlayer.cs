using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ModernRecipeBook;

public sealed class BookPlayer : ModPlayer
{
	internal readonly HashSet<string> Favorites = new HashSet<string>(StringComparer.Ordinal);

	private BookUiState SavedUiState = new BookUiState();

	internal readonly BookRecentHistory RecentRecipes = new BookRecentHistory();

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
			DigitFromCtrl = false;
			return;
		}
		// Ctrl+1..6 switches book tabs. Vanilla picks a hotbar slot while a digit is held, so a digit pressed
		// with Ctrl stays blocked until it is released, even when Ctrl is let go first.
		KeyboardState keys = Keyboard.GetState();
		bool digit = HotbarDigits.Any(keys.IsKeyDown);
		DigitFromCtrl = digit && (DigitFromCtrl || keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl));
		if (DigitFromCtrl)
		{
			BlockHotbarTriggers();
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
		}
		BlockHotbarTriggers();
	}

	private static readonly Keys[] HotbarDigits = new Keys[10] { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0 };

	private static bool DigitFromCtrl;

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
