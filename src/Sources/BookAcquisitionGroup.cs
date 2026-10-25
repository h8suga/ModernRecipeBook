using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Sources;

internal sealed class BookAcquisitionGroup
{
	internal BookAcquisitionEntry Source { get; }

	internal BookAcquisitionRule[] Rules { get; }

	internal int VariantCount { get; }

	// Every rule needs another difficulty or seed (BookEnemyLoot.OtherWorld).
	internal bool OtherWorld { get; }

	internal bool CanExpand
	{
		get
		{
			if (Source.Kind != BookAcquisitionKind.Craft)
			{
				return Source.Kind != BookAcquisitionKind.Unknown;
			}
			return false;
		}
	}

	internal string Amount
	{
		get
		{
			if (Rules.Select((BookAcquisitionRule x) => x.Source.Amount).Distinct(StringComparer.Ordinal).Count() != 1)
			{
				return "…";
			}
			return Rules[0].Source.Amount;
		}
	}

	internal BookAcquisitionGroup(IEnumerable<BookAcquisitionEntry> reports)
	{
		BookAcquisitionEntry[] entries = reports.ToArray();
		// This world's difficulty and seed only, as BookEnemyLoot; a source only another world has keeps its rules.
		OtherWorld = entries.All((BookAcquisitionEntry x) => x.OtherMode);
		if (!OtherWorld)
		{
			entries = entries.Where((BookAcquisitionEntry x) => !x.OtherMode).ToArray();
		}
		Source = entries.OrderBy((BookAcquisitionEntry x) => (x.NpcNetId <= 0) ? 1 : 0).First();
		VariantCount = entries.Select((BookAcquisitionEntry x) => (ItemType: x.ItemType, NpcNetId: x.NpcNetId)).Distinct().Count();
		Rules = (from x in entries
			group x by (ReportKey: x.ReportKey, Chance: x.Chance, Amount: x.Amount, ConditionLabel: x.ConditionLabel, Body: x.Body, Conditional: x.Conditional) into x
			select new BookAcquisitionRule(x)).ToArray();
	}
}
