using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Sources;

internal sealed class BookAcquisitionGroup
{
	internal BookAcquisitionEntry Source { get; }

	internal BookAcquisitionRule[] Rules { get; }

	internal int VariantCount { get; }

	// Every rule needs another difficulty or seed, or is a branch this world is past (BookEnemyLoot.OtherWorld).
	internal bool OtherWorld { get; }

	// Every rule is a branch this world is past ("Only the first kill · already past"), none another world's.
	internal bool PastOnly { get; }

	// A drop's or container's several rules as the mob card's row shows them (BookEnemyRules.Pick, over the rules of the
	// row's own mob or container): the one that applies now, or rules that add up; null when they vary or for one rule.
	internal BookDropPick Pick { get; }

	// The rule the row shows: the one Pick names, else the first.
	private BookAcquisitionRule Shown { get; }

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

	// No one chance or amount: another world's rules, or several that vary by condition.
	internal bool Varies => OtherWorld || (Rules.Length > 1 && Pick == null);

	internal string Chance => (Pick != null) ? BookAcquisitionRules.Chance(Pick.Chance) : Shown.Source.Chance;

	internal string Amount
	{
		get
		{
			if (Pick != null)
			{
				return (Pick.Shown < 0) ? ("×" + BookAcquisitionRules.Stack(Pick.Min, Pick.Max)) : Shown.Source.Amount;
			}
			if (Rules.Select((BookAcquisitionRule x) => x.Source.Amount).Distinct(StringComparer.Ordinal).Count() != 1)
			{
				return "…";
			}
			return Rules[0].Source.Amount;
		}
	}

	// Rules that add up say what they share ("In Treasure Bag (Moon Lord)").
	internal string ConditionLabel => (Pick != null && Pick.Shown < 0) ? sharedLabel : Shown.Source.ConditionLabel;

	private readonly string sharedLabel = "";

	internal BookAcquisitionGroup(IEnumerable<BookAcquisitionEntry> reports)
	{
		BookAcquisitionEntry[] entries = reports.ToArray();
		// This world's difficulty and seed only, as BookEnemyLoot; a source only another world has keeps its rules.
		OtherWorld = entries.All((BookAcquisitionEntry x) => x.OtherMode);
		PastOnly = OtherWorld && entries.All((BookAcquisitionEntry x) => x.Drop is { Past: true, OtherMode: false });
		if (!OtherWorld)
		{
			entries = entries.Where((BookAcquisitionEntry x) => !x.OtherMode).ToArray();
		}
		Source = entries.OrderBy((BookAcquisitionEntry x) => (x.NpcNetId <= 0) ? 1 : 0).First();
		VariantCount = entries.Select((BookAcquisitionEntry x) => (ItemType: x.ItemType, NpcNetId: x.NpcNetId)).Distinct().Count();
		// Two rules alike stay two when they are separate rules of the mob (they roll each, BookEnemyDrop.Rule).
		Rules = (from x in entries
			group x by (ReportKey: x.ReportKey, Chance: x.Chance, Amount: x.Amount, ConditionLabel: x.ConditionLabel, Body: x.Body, Conditional: x.Conditional, Rule: x.Drop?.Rule ?? 0) into x
			select new BookAcquisitionRule(x)).ToArray();
		Shown = Rules[0];
		// Variants that share the rules are one row (BookAcquisitionGrouping): the rules are those of the row's own mob, one
		// rule's reports alike taken as one (BookEnemyRules.Merge), as on the card.
		BookAcquisitionEntry source = Source;
		List<(int Index, BookAcquisitionEntry[] Alike)> own = Rules.Select((BookAcquisitionRule r, int i) => (i, r.Reports.Where((BookAcquisitionEntry x) => x.NpcNetId == source.NpcNetId && x.ItemType == source.ItemType).ToArray())).Where(((int Index, BookAcquisitionEntry[] Alike) x) => x.Alike.Length > 0).ToList();
		if (!OtherWorld && own.Count > 0 && (Rules.Length > 1 || own.Any(((int Index, BookAcquisitionEntry[] Alike) x) => x.Alike.Length > 1)) && own.All(((int Index, BookAcquisitionEntry[] Alike) x) => x.Alike.All((BookAcquisitionEntry e) => e.Drop != null)))
		{
			Pick = BookEnemyRules.Pick(own.Select(((int Index, BookAcquisitionEntry[] Alike) x) => BookEnemyRules.Merge(x.Alike.Select((BookAcquisitionEntry e) => e.Drop).ToList())).ToList());
			string[] labels = own.Where(((int Index, BookAcquisitionEntry[] Alike) x) => !x.Alike[0].Drop.OnTop).Select(((int Index, BookAcquisitionEntry[] Alike) x) => x.Alike[0].ConditionLabel).Distinct(StringComparer.Ordinal).ToArray();
			sharedLabel = labels.Length == 1 ? labels[0] : "";
			Shown = (Pick != null && Pick.Shown >= 0) ? Rules[own[Pick.Shown].Index] : Rules[0];
		}
	}
}
