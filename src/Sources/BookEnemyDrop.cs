namespace ModernRecipeBook.Sources;

// OtherMode: a difficulty or seed gate fails in this world (the Classic chance of a Normal vs Expert drop in an Expert
// world, a Constant-seed drop elsewhere). Past: a branch this world is already past for good (a drop "before" a boss
// that is down, a first kill already made); both are another world's rule. Now: whether its conditions hold in this
// world now (false: not yet), null when the kill or an event decides (a supercharged robot, a moon's wave). Rule: the
// top-level rule it comes from; two of them roll on their own (ItemDropResolver.TryDropping tries every rule).
internal sealed record BookEnemyDrop(int ItemType, float Chance, int Min, int Max, string Conditions, string ConditionKey, bool OtherMode = false, bool Past = false, bool? Now = true, int Rule = 0);
