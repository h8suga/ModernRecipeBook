using System.Collections.Generic;

namespace ModernRecipeBook;

// Back/forward list of item types, like a browser history: a new step drops everything after the cursor.
internal sealed class BookItemHistory
{
	private readonly List<int> Items = new List<int>();

	private int cursor = -1;

	internal int Count => cursor + 1;

	internal bool CanForward => cursor + 1 < Items.Count;

	internal int this[int index] => Items[index];

	internal void Clear()
	{
		Items.Clear();
		cursor = -1;
	}

	internal void Push(int type)
	{
		if (type <= 0 || (cursor >= 0 && Items[cursor] == type))
		{
			return;
		}
		if (CanForward)
		{
			Items.RemoveRange(cursor + 1, Items.Count - cursor - 1);
		}
		int existing = Items.IndexOf(type);
		if (existing >= 0)
		{
			Items.RemoveRange(existing + 1, Items.Count - existing - 1);
			cursor = existing;
			return;
		}
		if (Items.Count >= 64)
		{
			Items.RemoveAt(0);
		}
		Items.Add(type);
		cursor = Items.Count - 1;
	}

	internal int Forward()
	{
		if (!CanForward)
		{
			return 0;
		}
		return Items[++cursor];
	}

	internal int GoTo(int index)
	{
		if (index < 0 || index >= Count)
		{
			return 0;
		}
		int result = Items[index];
		cursor = index;
		return result;
	}
}
