using System.Collections.Generic;

namespace ModernRecipeBook;

internal readonly record struct BookResourceNeed(bool Storage, long Quantity, IReadOnlyCollection<int> Types);
