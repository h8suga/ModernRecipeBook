using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook;

public sealed class BookInventoryShortcut : ModSystem
{
	private static readonly BookInventoryInputRouter Input = new BookInventoryInputRouter();

	private static bool pendingToggle;

	private static int pendingItem;

	internal static void ProcessRequests()
	{
		int item = pendingItem;
		bool toggle = pendingToggle;
		pendingItem = 0;
		pendingToggle = false;
		if (item <= 0 && !toggle)
		{
			return;
		}
		try
		{
			if (item > 0)
			{
				RecipeBookSystem.OpenForInventoryItem(item);
			}
			else if (!RecipeBookSystem.OpenForMob(BookMobTarget.UnderMouse()))
			{
				RecipeBookSystem.Toggle();
			}
		}
		catch (Exception error)
		{
			RecipeBookSystem.ReportUiFailure("open/update", error);
		}
	}

	public override void PostUpdateInput()
	{
		Input.Poll(Mouse.GetState().MiddleButton == ButtonState.Pressed);
	}

	internal static void RequestToggle()
	{
		if (Mouse.GetState().MiddleButton == ButtonState.Pressed && ModernRecipeBook.ToggleBook.GetAssignedKeys().Contains("Mouse3"))
		{
			Input.QueueToggle();
		}
		else
		{
			pendingToggle = true;
		}
	}

	public override void PostDrawInterface(SpriteBatch spriteBatch)
	{
		if (Input.EndDraw())
		{
			pendingToggle = true;
		}
	}

	private static bool CanSearchItem(Item item)
	{
		if (Main.dedServ || Main.gameMenu || item == null || item.IsAir)
		{
			return false;
		}
		Player player = Main.LocalPlayer;
		KeyboardState keys = Keyboard.GetState();
		return BookInventoryShortcutRules.CanOpen(new BookInventoryShortcutRules.State(ModContent.GetInstance<BookConfig>().InventoryAltRightClick, player != null && player.active && !player.dead && !player.ghost, InventoryOpen: true, keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt), keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl) || keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift), !PlayerInput.UsingGamepad, AllowedContainer: true, AllowedContext: true, 0, 1, ContentSamples.ItemsByType.ContainsKey(item.type), 1));
	}

	internal static bool IsTarget(Item[] inventory, int context, int slot)
	{
		if (inventory != null && slot >= 0 && slot < inventory.Length)
		{
			return CanSearchItem(inventory[slot]);
		}
		return false;
	}

	internal static bool TryOpen(Item[] inventory, int context, int slot)
	{
		if (!IsTarget(inventory, context, slot))
		{
			return false;
		}
		return TryQueueItem(inventory[slot]);
	}

	private static bool TryQueueItem(Item item, bool ownIcon = false)
	{
		if (!Input.TakeInventory(CanSearchItem(item) && (!RecipeBookSystem.Open || !RecipeBookSystem.MouseOver || ownIcon)))
		{
			return false;
		}
		pendingItem = item.type;
		pendingToggle = false;
		return true;
	}

	internal static void TryOpenIcon(Item item)
	{
		if (RecipeBookSystem.ItemIconSearchAllowed)
		{
			TryQueueItem(item, ownIcon: true);
		}
	}

	internal static void TryOpenTooltip(Item item)
	{
		if (Main.HoverItem != null && !Main.HoverItem.IsAir && Main.HoverItem.type == item?.type && !string.IsNullOrEmpty(Main.hoverItemName))
		{
			TryQueueItem(item);
		}
	}

	private static void Reset()
	{
		Input.Reset();
		pendingItem = 0;
		pendingToggle = false;
	}

	public override void OnWorldUnload()
	{
		Reset();
	}

	public override void Unload()
	{
		Reset();
	}
}
