using System.Collections.Generic;

namespace ModernRecipeBook.Catalogue.Recipes;

internal readonly record struct BookBatchNeed(int Quantity, IReadOnlyCollection<int> AcceptedTypes);
