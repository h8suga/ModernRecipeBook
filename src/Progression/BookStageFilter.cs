namespace ModernRecipeBook.Progression;

// The global stage filter: every stage, one stage, everything before a boss (BookSteps; which boss is kept apart as
// its key), or what the progression model could not place.
internal enum BookStageFilter
{
	All,
	PreHardmode,
	Hardmode,
	PostHardmode,
	BeforeBoss,
	Unknown
}
