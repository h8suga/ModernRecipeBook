namespace ModernRecipeBook;

internal readonly record struct BookResourceProgress(BookResourceNeed Need, long Owned, long Allocated, bool Unknown, bool Infinite)
{
	internal bool Met
	{
		get
		{
			if (!Unknown)
			{
				return Allocated >= Need.Quantity;
			}
			return false;
		}
	}
}
