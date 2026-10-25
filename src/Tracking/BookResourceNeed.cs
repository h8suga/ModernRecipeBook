using System.Collections.Generic;

namespace ModernRecipeBook.Tracking;

internal readonly record struct BookResourceNeed(long Quantity, IReadOnlyCollection<int> Types);
