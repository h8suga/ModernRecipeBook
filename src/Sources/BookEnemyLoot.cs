using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Sources;

internal sealed class BookEnemyLoot
{
	internal int ItemType { get; }

	internal BookEnemyDrop[] Reports { get; }

	internal BookEnemyDrop[] Rules { get; }

	internal string Amount { get; }

	// Only another difficulty or seed drops it, or only a branch this world is already past: the mob never drops it here.
	internal bool OtherWorld { get; }

	// What the row shows of this world's rules (BookEnemyRules.Pick); null: it varies by condition.
	internal BookDropPick Pick { get; }

	// What the conditions of its reports name (BookEnemyDrop.Mentions).
	internal IEnumerable<BookMention> Mentions => Reports.SelectMany((BookEnemyDrop x) => x.Mentions ?? System.Array.Empty<BookMention>());

	// Every rule is one this world is past (Past), none another difficulty's or seed's.
	internal bool PastOnly => OtherWorld && Reports.All((BookEnemyDrop x) => x.Past && !x.OtherMode);

	// The boss's Treasure Bag it comes out of in this world when the boss itself drops it only in Classic: the rules are
	// then the bag's (RecipeBookSystem.ReadMobLoot); 0 for the mob's own loot.
	internal int Bag { get; }

	internal BookEnemyLoot(IEnumerable<BookEnemyDrop> reports, int bag = 0)
	{
		Bag = bag;
		Reports = reports.ToArray();
		ItemType = Reports[0].ItemType;
		// The rules of this world: an item only another mode drops (a Master relic in Expert) or only a branch already past
		// (a first kill made) keeps its own. Two rules alike stay two when they are separate rules (they roll each), and one
		// rule's reports alike are one (BookEnemyRules.Merge).
		BookEnemyDrop[] here = Reports.Where((BookEnemyDrop x) => !x.OtherMode && !x.Past).ToArray();
		OtherWorld = here.Length == 0;
		Rules = (from x in OtherWorld ? Reports : here
			group x by (BookAcquisitionGrouping.DropRuleKey(x.Chance, x.Min, x.Max, x.ConditionKey), Conditions: x.Conditions, x.Rule) into x
			select BookEnemyRules.Merge(x.ToList())).ToArray();
		Pick = OtherWorld ? (Rules.Length == 1 ? BookEnemyRules.Pick(Rules) : null) : BookEnemyRules.Pick(Rules);
		Amount = Pick != null ? ("×" + BookAcquisitionRules.Stack(Pick.Min, Pick.Max)) : "…";
	}
}
