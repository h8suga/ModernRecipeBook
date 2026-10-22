namespace ModernRecipeBook;

internal sealed record BookEnemyDrop(int ItemType, float Chance, int Min, int Max, string Conditions, string ConditionKey);
