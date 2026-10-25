namespace ModernRecipeBook.Common.Input;

internal static class BookInventoryShortcutRules
{
	internal readonly record struct State(bool Enabled, bool LocalClient, bool InventoryOpen, bool Alt, bool OtherModifier, bool Keyboard, bool AllowedContainer, bool AllowedContext, int Slot, int Length, bool HasItem, int SlotLimit = 58);

	internal static bool CanOpen(State state)
	{
		if (state.Enabled && state.LocalClient && state.InventoryOpen && !state.Alt && !state.OtherModifier && state.Keyboard && state.AllowedContainer && state.AllowedContext && state.Slot >= 0 && state.Slot < state.SlotLimit && state.Slot < state.Length)
		{
			return state.HasItem;
		}
		return false;
	}
}
