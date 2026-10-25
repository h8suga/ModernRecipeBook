using System.Collections.Generic;

namespace ModernRecipeBook;

internal sealed class BookRecentHistory
{
	internal const int Limit = 40;

	private readonly List<string> Keys = new List<string>();

	internal int Count => Keys.Count;

	internal int Version { get; private set; }

	internal bool Record(string key)
	{
		if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
		{
			return false;
		}
		int existing = Keys.IndexOf(key);
		if (existing == 0)
		{
			return false;
		}
		if (existing > 0)
		{
			Keys.RemoveAt(existing);
		}
		Keys.Insert(0, key);
		if (Keys.Count > 40)
		{
			Keys.RemoveRange(40, Keys.Count - 40);
		}
		Version++;
		return true;
	}

	internal void Load(IEnumerable<string> keys)
	{
		Keys.Clear();
		if (keys != null)
		{
			foreach (string key in keys)
			{
				if (!string.IsNullOrWhiteSpace(key) && key.Length <= 128 && !Keys.Contains(key))
				{
					Keys.Add(key);
					if (Keys.Count == 40)
					{
						break;
					}
				}
			}
		}
		Version++;
	}

	internal void Clear()
	{
		if (Keys.Count != 0)
		{
			Keys.Clear();
			Version++;
		}
	}

	internal List<string> Snapshot()
	{
		return new List<string>(Keys);
	}

	internal List<int> Resolve(IReadOnlyDictionary<string, int> currentRecipes)
	{
		List<int> result = new List<int>();
		foreach (string key in Keys)
		{
			if (currentRecipes.TryGetValue(key, out var recipeIndex))
			{
				result.Add(recipeIndex);
			}
		}
		return result;
	}
}
