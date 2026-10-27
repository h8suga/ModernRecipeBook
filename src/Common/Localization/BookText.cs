using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Localization;

namespace ModernRecipeBook.Common.Localization;

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

	// The full localization key of a book text ("Categories.Wings" -> "Mods.ModernRecipeBook.Categories.Wings").
	internal static string KeyOf(string key)
	{
		return Prefix + key;
	}

	internal static string L(string key)
	{
		return Text(key).Value;
	}

	internal static string L(string key, params object[] args)
	{
		return Text(key).Format(args);
	}

	// The language shown, for caches of built texts.
	internal static int Culture => Language.ActiveCulture.LegacyId;

	// "just now", "5 min ago", "3 h ago", "2 d ago".
	internal static string Ago(TimeSpan span)
	{
		if (span.TotalMinutes < 1.0)
		{
			return L("Time.JustNow");
		}
		if (span.TotalHours < 1.0)
		{
			return L("Time.MinutesAgo", (int)span.TotalMinutes);
		}
		if (span.TotalDays < 1.0)
		{
			return L("Time.HoursAgo", (int)span.TotalHours);
		}
		return L("Time.DaysAgo", (int)span.TotalDays);
	}

	internal static LocalizedText Text(string key)
	{
		return Texts.GetOrAdd(key, (string k) => Language.GetOrRegister(Prefix + k));
	}

	// Dev audit for the test stand: every book text as the game loaded it in the language shown ("Area.Key<TAB>value"
	// with \n escaped), the settings and keybinds left out.
	internal static IEnumerable<string> Audit()
	{
		return from t in Language.FindAll(Lang.CreateDialogFilter(Prefix))
			let key = t.Key.Substring(Prefix.Length)
			where !key.StartsWith("Configs.") && !key.StartsWith("Keybinds.")
			orderby key
			select key + "\t" + t.Value.Replace("\\", "\\\\").Replace("\n", "\\n");
	}

	// Dev audit for the test stand: the counted texts at the numbers each language's plural rules tell apart.
	internal static IEnumerable<string> PluralAudit()
	{
		string[] keys = new string[8] { "Crafting.InventoryFullFreeSlots", "Storage.CountChests", "Storage.CountLocked", "Storage.WithinTiles", "HowToGet.CountSources", "Stations.WithoutStations", "MobCard.CountPlayers", "Markers.MarkersAtMost" };
		return from key in keys
			from n in new int[7] { 1, 2, 5, 11, 21, 22, 111 }
			select key + " " + n + ": " + L(key, n);
	}
}
