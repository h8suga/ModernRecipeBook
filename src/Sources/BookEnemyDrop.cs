namespace ModernRecipeBook.Sources;

// OtherMode: a difficulty or seed gate fails in this world (the Classic chance of a Normal vs Expert drop in an Expert
// world, a Constant-seed drop elsewhere).
internal sealed record BookEnemyDrop(int ItemType, float Chance, int Min, int Max, string Conditions, string ConditionKey, bool OtherMode = false);
