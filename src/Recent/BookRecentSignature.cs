using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ModernRecipeBook.Recent;

internal static class BookRecentSignature
{
	internal static string Create(string owner, string outputItem, int outputStack, IEnumerable<string> ingredients, IEnumerable<string> stations, IEnumerable<string> acceptedGroups, IEnumerable<string> conditions)
	{
		StringBuilder canonical = new StringBuilder();
		Field(canonical, "schema", "1");
		Field(canonical, "owner", owner ?? "");
		Field(canonical, "output", outputItem ?? "");
		Field(canonical, "output-stack", outputStack.ToString(CultureInfo.InvariantCulture));
		Collection(canonical, "ingredient", ingredients);
		Collection(canonical, "station", stations);
		Collection(canonical, "group", acceptedGroups);
		Collection(canonical, "condition", conditions);
		return "v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
	}

	private static void Collection(StringBuilder target, string name, IEnumerable<string> values)
	{
		foreach (string value in (values ?? Enumerable.Empty<string>()).Where((string text) => text != null).OrderBy((string result) => result, StringComparer.Ordinal))
		{
			Field(target, name, value);
		}
	}

	private static void Field(StringBuilder target, string name, string value)
	{
		AppendPart(target, name);
		AppendPart(target, value);
	}

	private static void AppendPart(StringBuilder target, string value)
	{
		target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
		target.Append(':');
		target.Append(value);
	}
}
