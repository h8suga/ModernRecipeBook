using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ModernRecipeBook;

internal static class BookAcquisitionGrouping
{
	internal static string DropRuleKey(float chance, int min, int max, string conditions)
	{
		return $"{BitConverter.SingleToInt32Bits(chance).ToString(CultureInfo.InvariantCulture)}:{min.ToString(CultureInfo.InvariantCulture)}:{max.ToString(CultureInfo.InvariantCulture)}:{conditions}";
	}

	internal static List<BookAcquisitionGroup> Build(IEnumerable<BookAcquisitionEntry> reports)
	{
		return (from x in reports
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
}
