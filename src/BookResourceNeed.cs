using System.Collections.Generic;

namespace ModernRecipeBook;

internal readonly record struct BookResourceNeed(long Quantity, IReadOnlyCollection<int> Types);
