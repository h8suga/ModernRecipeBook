using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria.GameContent.ItemDropRules;

namespace ModernRecipeBook.Sources;

// What a row of an item's rules shows: the one rule (Shown, its index), or several taken as one (Shown -1, Count of them).
internal sealed record BookDropPick(int Shown, float Chance, int Min, int Max, int Count, string Conditions);

internal static class BookEnemyRules
{
	internal static int[] Variants(int primary, IEnumerable<int> alternatives)
	{
		return (from x in new int[1] { primary }.Concat(alternatives ?? Array.Empty<int>())
			where x != 0
			select x).Distinct().ToArray();
	}

	internal static List<BookEnemyLoot> Group(IEnumerable<BookEnemyDrop> reports)
	{
		return (from x in reports
			where x.ItemType > 0
			group x by x.ItemType into x
			select new BookEnemyLoot(x)).ToList();
	}

	// Reports alike from one rule are alternatives of one roll, their chances added up (a OneFromRulesRule naming one rule
	// three times: Calamity's AbyssalTreasure gives its Silver Coin rule in 3 of 7).
	internal static BookEnemyDrop Merge(IReadOnlyList<BookEnemyDrop> alike)
	{
		return alike.Count == 1 ? alike[0] : alike[0] with { Chance = Math.Min(1f, alike.Sum((BookEnemyDrop x) => x.Chance)) };
	}

	// Whether every condition of a rule holds now: any not yet says not yet, else any undecided says undecided.
	internal static bool? Both(bool? a, bool? b)
	{
		return (a == false || b == false) ? false : ((a == null || b == null) ? null : true);
	}

	// One row for this world's rules of an item (the mob card, Contents, How to Get): the only rule that can apply now (the
	// others not yet: a drop after a boss next to one before it, Crawdad's Craw Carapace in Hardmode); else rules of their
	// own that all apply now, which the game rolls each (ItemDropResolver.TryDropping tries every rule: Calamity's bag
	// weapons next to vanilla's it meant to replace), as one chance 1 − Π(1 − p) and the amounts added up (the rules that
	// always drop add their least, else the least of one); else, while none applies yet, the first. Null when what applies
	// depends on something the world does not decide (the kill, an event): "Varies by condition".
	internal static BookDropPick Pick(IReadOnlyList<BookEnemyDrop> rules)
	{
		if (rules.Count == 0)
		{
			return null;
		}
		// A drop on top of another (OnTop) only follows it: no rule of its own, its amount added to the other's.
		int extra = rules.Where((BookEnemyDrop r) => r.OnTop && r.Now != false).Sum((BookEnemyDrop r) => r.Max);
		List<int> now = Enumerable.Range(0, rules.Count).Where((int i) => rules[i].Now != false && !rules[i].OnTop).ToList();
		if (now.Count <= 1)
		{
			int at = now.Count == 1 ? now[0] : 0;
			BookEnemyDrop shown = rules[at];
			return extra > 0 ? new BookDropPick(-1, shown.Chance, shown.Min, shown.Max + extra, 1, shown.Conditions) : new BookDropPick(at, shown.Chance, shown.Min, shown.Max, 1, shown.Conditions);
		}
		List<BookEnemyDrop> each = now.Select((int i) => rules[i]).ToList();
		if (each.Any((BookEnemyDrop r) => r.Now == null) || each.Select((BookEnemyDrop r) => r.Rule).Distinct().Count() != each.Count)
		{
			return null;
		}
		float none = each.Aggregate(1f, (float p, BookEnemyDrop r) => p * (1f - r.Chance));
		List<BookEnemyDrop> always = each.Where((BookEnemyDrop r) => r.Chance >= 1f).ToList();
		int min = always.Count > 0 ? always.Sum((BookEnemyDrop r) => r.Min) : each.Min((BookEnemyDrop r) => r.Min);
		string[] conditions = each.Select((BookEnemyDrop r) => r.Conditions).Distinct(StringComparer.Ordinal).ToArray();
		return new BookDropPick(-1, 1f - none, min, each.Sum((BookEnemyDrop r) => r.Max) + extra, each.Count, conditions.Length == 1 ? conditions[0] : "");
	}

	// Which of a rule's reports are a second drop of an item on top of its first: a drop of the item whose success chain
	// drops it once more (Chains.TryIfSucceeded rolls only after its rule dropped). Calamity's Abyssal and Sulphuric
	// Treasure give a Healing Potion, and in Expert one more at 1/3 (AbyssalTreasure.ModifyItemLoot:
	// itemDropRule2.OnSuccess(expert).OnSuccess(188, 3)); the second is reported under the first's conditions and more,
	// at a lower chance.
	internal static bool[] OnTop(IItemDropRule rule, List<DropRateInfo> rates)
	{
		bool[] top = new bool[rates.Count];
		if (rates.Select((DropRateInfo r) => r.itemId).Distinct().Count() == rates.Count)
		{
			return top;
		}
		HashSet<int> again = new HashSet<int>();
		HashSet<IItemDropRule> path = new HashSet<IItemDropRule>(ReferenceEqualityComparer.Instance);
		Walk(rule, new HashSet<int>(), 0);
		for (int i = 0; i < rates.Count; i++)
		{
			DropRateInfo rate = rates[i];
			List<IItemDropRuleCondition> mine = rate.conditions ?? new List<IItemDropRuleCondition>();
			top[i] = again.Contains(rate.itemId) && rates.Any((DropRateInfo first) => first.itemId == rate.itemId && first.dropRate > rate.dropRate && (first.conditions?.Count ?? 0) < mine.Count && (first.conditions ?? new List<IItemDropRuleCondition>()).All(mine.Contains));
		}
		return top;
		void Walk(IItemDropRule node, HashSet<int> above, int depth)
		{
			if (node == null || depth > 32 || !path.Add(node))
			{
				return;
			}
			int own = (node as CommonDrop)?.itemId ?? 0;
			if (own > 0 && above.Contains(own))
			{
				again.Add(own);
			}
			// The rules a rule holds (OneFromRulesRule.options, DropBasedOnExpertMode's two, SequentialRulesRule.rules...).
			foreach (FieldInfo field in node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				object value = field.GetValue(node);
				foreach (IItemDropRule inner in value as IEnumerable<IItemDropRule> ?? (value is IItemDropRule one ? new[] { one } : Array.Empty<IItemDropRule>()))
				{
					Walk(inner, above, depth + 1);
				}
			}
			foreach (IItemDropRuleChainAttempt chain in node.ChainedRules)
			{
				Walk(chain.RuleToChain, (chain is Chains.TryIfSucceeded && own > 0) ? new HashSet<int>(above) { own } : above, depth + 1);
			}
			path.Remove(node);
		}
	}
}
