namespace ModernRecipeBook;

internal enum BookSortMode
{
	Name,
	ItemId,
	Mod,
	// Bestiary tab only: earliest stage first.
	Stage,
	// Bestiary tab only: by when the player first met the mob.
	Met,
	// Items and Recipes: by the strongest station the easiest recipe needs.
	Station,
	// Items, Recipes and Storage: every list (Rarity, Value), then what the listed category has (RecipeBookSystem.Sorts).
	Rarity,
	Value,
	Damage,
	Speed,
	Crit,
	Knockback,
	Defense,
	FlightTime,
	HealLife,
	HealMana,
	BuffTime,
	PickPower,
	AxePower,
	HammerPower,
	FishingPower,
	BaitPower,
	// Storage only: when the stack's kind appeared or its total last changed (BookSources.ChangedAt). Appended last:
	// UI state stores the mode by name, and the masks by value.
	Changed
}
