using System.Collections.Generic;

namespace ModernRecipeBook;

// Back/forward list like a browser's: a new entry drops everything after the cursor.
internal sealed class BookHistory<T>
{
	private readonly List<T> Entries = new List<T>();

	private readonly int Capacity;

	private int cursor = -1;

	internal BookHistory(int capacity)
	{
		Capacity = capacity;
	}

	internal bool IsEmpty => cursor < 0;

	internal bool CanBack => cursor > 0;

	internal bool CanForward => cursor + 1 < Entries.Count;

	internal T Current => Entries[cursor];

	internal T Previous => Entries[cursor - 1];

	internal void Clear()
	{
		Entries.Clear();
		cursor = -1;
	}

	internal void Push(T entry)
	{
		Entries.RemoveRange(cursor + 1, Entries.Count - cursor - 1);
		if (Entries.Count >= Capacity)
		{
			Entries.RemoveAt(0);
		}
		Entries.Add(entry);
		cursor = Entries.Count - 1;
	}

	internal void ReplaceCurrent(T entry)
	{
		if (cursor < 0)
		{
			Push(entry);
		}
		else
		{
			Entries[cursor] = entry;
		}
	}

	internal T Back()
	{
		return Entries[--cursor];
	}

	internal T Forward()
	{
		return Entries[++cursor];
	}
}