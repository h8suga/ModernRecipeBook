using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook;

internal sealed class BookEnemyLoot
{
	internal int ItemType { get; }

	internal BookEnemyDrop[] Reports { get; }

	internal BookEnemyDrop[] Rules { get; }

	internal string Amount { get; }

	internal BookEnemyLoot(IEnumerable<BookEnemyDrop> reports)
	{
		Reports = reports.ToArray();
		ItemType = Reports[0].ItemType;
		Rules = (from x in Reports
			group x by (BookAcquisitionGrouping.DropRuleKey(x.Chance, x.Min, x.Max, x.ConditionKey), Conditions: x.Conditions) into x
			select x.First()).ToArray();
		Amount = ((Rules.Select((BookEnemyDrop x) => (Min: x.Min, Max: x.Max)).Distinct().Count() == 1) ? ("×" + BookAcquisitionRules.Stack(Rules[0].Min, Rules[0].Max)) : "…");
	}
}
