using System.Collections.Generic;

namespace ModernRecipeBook;

internal readonly record struct BookBatchNeed(int Quantity, IReadOnlyCollection<int> AcceptedTypes);
