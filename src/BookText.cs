using System.Collections.Concurrent;
using Terraria.Localization;

namespace ModernRecipeBook;

// The book's own texts, laid out like Calamity's: L("Area.Key") reads Mods.ModernRecipeBook.Area.Key from
// Localization/<culture>/Mods.ModernRecipeBook.<Area>.hjson. A culture without its own files, or a key they lack,
// reads the English text (tModLoader's own fallback), so a translation can be partial. Plurals use tModLoader's
// {^0:one;few;many} with each language's own rules.
internal static class BookText
{
	private const string Prefix = "Mods.ModernRecipeBook.";

	// GetOrRegister returns the live text, which follows a language change; a key looked up before the mod's
	// localization loads is filled in when it does.
	private static readonly ConcurrentDictionary<string, LocalizedText> Texts = new ConcurrentDictionary<string, LocalizedText>();

	internal static string L(string key)
	{
		return Text(key).Value;
	}

	internal static string L(string key, params object[] args)
	{
		return Text(key).Format(args);
	}

	internal static LocalizedText Text(string key)
	{
		return Texts.GetOrAdd(key, (string k) => Language.GetOrRegister(Prefix + k));
	}
}
