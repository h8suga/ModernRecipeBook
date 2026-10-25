namespace ModernRecipeBook;

// OtherMode: a drop rule whose difficulty or seed gate fails in this world (BookEnemyDrop.OtherMode).
internal sealed record BookAcquisitionEntry(string Name, string Body, BookAcquisitionKind Kind, string Chance = "", string Amount = "", int ItemType = 0, int NpcNetId = 0, bool Conditional = false, int RecipeIndex = -1, string Owner = "Terraria", string ConditionLabel = "", string ReportKey = "", int TileType = -1, int FrameX = 0, int FrameY = 0, bool OtherMode = false);
