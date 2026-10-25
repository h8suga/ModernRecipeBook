namespace ModernRecipeBook.Sources;

// The kinds of source How to Get names (BookItemSources builds them; Craft rows come from the recipes).
internal enum BookAcquisitionKind
{
	Craft,
	Drop,
	Container,
	Shop,
	Trade,
	Banner,
	Catch,
	Shimmer,
	Use,
	Record,
	Plant,
	Tree,
	Mining,
	Fishing,
	Chest,
	World,
	Machine,
	DevSet,
	Unknown
}
