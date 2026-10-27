namespace ModernRecipeBook.UI.Book;

// What the item card lists under its header, in the order of its switch. UI state stores the mode by name.
internal enum BookCardMode
{
	Recipe,
	// The recipes that take the item.
	UsedIn,
	// What the item holds: offered only for an item with loot rules of its own (a Treasure Bag, a crate, a geode).
	Contents
}
