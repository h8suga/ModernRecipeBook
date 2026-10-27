namespace ModernRecipeBook.Sources;

// OtherMode: a drop rule whose difficulty or seed gate fails in this world, or a branch this world is past
// (BookEnemyDrop.OtherMode, Past). Drop: a drop's or a container's rule as the mob card reads it, for the row that adds
// rules up as the card does (BookAcquisitionGroup.Pick); null for every other kind.
internal sealed record BookAcquisitionEntry(string Name, string Body, BookAcquisitionKind Kind, string Chance = "", string Amount = "", int ItemType = 0, int NpcNetId = 0, bool Conditional = false, int RecipeIndex = -1, string Owner = "Terraria", string ConditionLabel = "", string ReportKey = "", int TileType = -1, int FrameX = 0, int FrameY = 0, bool OtherMode = false, BookEnemyDrop Drop = null);
