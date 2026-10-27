namespace ModernRecipeBook.Sources;

// OtherMode: a drop rule whose difficulty or seed gate fails in this world, or a branch this world is past
// (BookEnemyDrop.OtherMode, Past). SourceItem: the item the row stands for (the bag, what shimmers or is used, what a trade
// takes, the chest it lies in), whose card a click opens; ItemType is only the row's icon. Drop: a drop's or a container's
// rule as the mob card reads it, for the row that adds rules up as the card does (BookAcquisitionGroup.Pick); null for
// every other kind. Mentions: what the row's text names besides bosses, places and bags (RecipeBookSystem.Links finds
// those by name): the rod, the pickaxe, the Angler, a condition's town NPC; each a link there.
internal sealed record BookAcquisitionEntry(string Name, string Body, BookAcquisitionKind Kind, string Chance = "", string Amount = "", int ItemType = 0, int NpcNetId = 0, bool Conditional = false, int RecipeIndex = -1, string Owner = "Terraria", string ConditionLabel = "", string ReportKey = "", int TileType = -1, int FrameX = 0, int FrameY = 0, bool OtherMode = false, int SourceItem = 0, BookEnemyDrop Drop = null, BookMention[] Mentions = null);

// A phrase of a row's text and what it stands for: an item's card, a mob's card or a Bestiary category (a tag key of
// BookBestiary); Lead is a line its link's tip starts with ("The first pickaxe with 210% power: Picksaw"). Whole: a
// condition's whole label, a link only where nothing it names is one already (the game's Russian declines the names).
internal readonly record struct BookMention(string Text, int Item = 0, int Npc = 0, string Place = null, string Lead = "", bool Whole = false);
