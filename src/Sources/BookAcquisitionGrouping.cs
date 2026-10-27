using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Terraria.ID;

namespace ModernRecipeBook.Sources;

internal static class BookAcquisitionGrouping
{
	internal static string DropRuleKey(float chance, int min, int max, string conditions)
	{
		return $"{BitConverter.SingleToInt32Bits(chance).ToString(CultureInfo.InvariantCulture)}:{min.ToString(CultureInfo.InvariantCulture)}:{max.ToString(CultureInfo.InvariantCulture)}:{conditions}";
	}

	internal static List<BookAcquisitionGroup> Build(IEnumerable<BookAcquisitionEntry> reports)
	{
		return (from x in Apart(reports.ToList())
			group x by x.Kind switch
			{
				BookAcquisitionKind.Drop => (Kind: x.Kind, Owner: x.Owner, x.Name, 0),
				BookAcquisitionKind.Container => (Kind: x.Kind, Owner: x.Owner, "", x.ItemType),
				BookAcquisitionKind.Shop => (Kind: x.Kind, Owner: x.Owner, "", x.NpcNetId),
				BookAcquisitionKind.Craft => (Kind: x.Kind, Owner: x.Owner, "", x.RecipeIndex),
				_ => (Kind: x.Kind, Owner: x.Owner, x.Name + "\n" + x.ConditionLabel, (x.ItemType != 0) ? x.ItemType : (-1 - x.TileType)),
			} into x
			select new BookAcquisitionGroup(x)).OrderBy((BookAcquisitionGroup x) => x.OtherWorld).ToList();
	}

	// Drop rows are by the mob's name, so that variants sharing their rules are one row (the Eater of Worlds' segments).
	// Mobs of one name with rules of their own are rows of their own, named apart: the Old One's Army's tiers by tier (the
	// Dark Mage gives the War Table at 100% in tier 1, 25% in tier 3: ItemDropDatabase.RegisterDD2EventDrops), others as
	// variants in net id order (Calamity's Plague Charger and its larger kin, PlagueCharger and PlagueChargerLarge).
	private static List<BookAcquisitionEntry> Apart(List<BookAcquisitionEntry> entries)
	{
		foreach (IGrouping<(string Owner, string Name), BookAcquisitionEntry> name in entries.Where((BookAcquisitionEntry e) => e.Kind == BookAcquisitionKind.Drop && e.NpcNetId != 0).GroupBy((BookAcquisitionEntry e) => (e.Owner, e.Name)).ToList())
		{
			List<int[]> kinds = (from npc in name.GroupBy((BookAcquisitionEntry e) => e.NpcNetId)
				orderby npc.Key
				group npc.Key by string.Join("\n", npc.Select((BookAcquisitionEntry e) => $"{e.ReportKey}|{e.Chance}|{e.Amount}|{e.ConditionLabel}|{e.OtherMode}").OrderBy((string s) => s, StringComparer.Ordinal)) into kind
				select kind.ToArray()).ToList();
			for (int k = 0; k < kinds.Count && kinds.Count > 1; k++)
			{
				string tier = Tier(kinds[k][0]);
				string named = (tier != null) ? L("BestiaryLoot.NameTier", name.Key.Name, tier) : L("BestiaryLoot.NameVariant", name.Key.Name, k + 1);
				for (int i = 0; i < entries.Count; i++)
				{
					if (entries[i].Kind == BookAcquisitionKind.Drop && entries[i].Name == name.Key.Name && kinds[k].Contains(entries[i].NpcNetId))
					{
						entries[i] = entries[i] with { Name = named };
					}
				}
			}
		}
		return entries;
	}

	// "1" for DD2DarkMageT1: the Old One's Army's tier in the NPC's name.
	private static string Tier(int netId)
	{
		return NPCID.Search.TryGetName(netId, out string name) && name.StartsWith("DD2", StringComparison.Ordinal) && name.Length > 2 && name[^2] == 'T' && char.IsDigit(name[^1]) ? name[^1].ToString() : null;
	}
}
