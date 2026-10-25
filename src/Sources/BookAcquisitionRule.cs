using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Sources;

internal sealed class BookAcquisitionRule
{
	internal BookAcquisitionEntry[] Reports { get; }

	internal BookAcquisitionEntry Source => Reports[0];

	internal int VariantCount => Reports.Select((BookAcquisitionEntry x) => (ItemType: x.ItemType, NpcNetId: x.NpcNetId)).Distinct().Count();

	internal BookAcquisitionRule(IEnumerable<BookAcquisitionEntry> reports)
	{
		Reports = reports.ToArray();
	}
}
