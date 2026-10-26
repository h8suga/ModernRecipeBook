using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Sources;

internal sealed class BookEnemyLoot
{
	internal int ItemType { get; }

	internal BookEnemyDrop[] Reports { get; }

	internal BookEnemyDrop[] Rules { get; }

	internal string Amount { get; }

	// Only another difficulty or seed drops it: the mob never drops it in this world.
	internal bool OtherWorld { get; }

	// The boss's Treasure Bag it comes out of in this world when the boss itself drops it only in Classic: the rules are
	// then the bag's (RecipeBookSystem.ReadMobLoot); 0 for the mob's own loot.
	internal int Bag { get; }

	internal BookEnemyLoot(IEnumerable<BookEnemyDrop> reports, int bag = 0)
	{
		Bag = bag;
		Reports = reports.ToArray();
		ItemType = Reports[0].ItemType;
		// The rules of this world's difficulty; an item only another mode drops (a Master relic in Expert) keeps its own.
		BookEnemyDrop[] here = Reports.Where((BookEnemyDrop x) => !x.OtherMode).ToArray();
		OtherWorld = here.Length == 0;
		Rules = (from x in OtherWorld ? Reports : here
			group x by (BookAcquisitionGrouping.DropRuleKey(x.Chance, x.Min, x.Max, x.ConditionKey), Conditions: x.Conditions) into x
			select x.First()).ToArray();
		Amount = ((Rules.Select((BookEnemyDrop x) => (Min: x.Min, Max: x.Max)).Distinct().Count() == 1) ? ("×" + BookAcquisitionRules.Stack(Rules[0].Min, Rules[0].Max)) : "…");
	}
}
