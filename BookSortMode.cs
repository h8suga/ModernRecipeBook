namespace ModernRecipeBook;

internal enum BookSortMode
{
	Name,
	ItemId,
	Mod,
	// Bestiary tab only: earliest stage first.
	Stage,
	// Bestiary tab only: by when the player first met the mob.
	Met
}
