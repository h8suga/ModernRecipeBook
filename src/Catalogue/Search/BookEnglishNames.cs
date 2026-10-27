using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hjson;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ModernRecipeBook.Catalogue.Search;

// The English names of items, NPCs and Bestiary tags while the game runs in another language, so the quick search
// finds "Terraprisma" for a Russian player. The game keeps only the active language's texts (LanguageManager loads
// English, then overwrites it), so English is read again from where the game reads it: vanilla's embedded
// Terraria.Localization.Content.en_US.*.json (LanguageManager.GetLanguageFilesForCulture) and every mod's en-US .hjson
// (LocalizationLoader.LoadTranslations, keys flattened the same way). The files are read a slice per update on the main
// thread (a mod's file is not safe to read from two threads); parsing runs on a worker. Kept until the mods unload.
internal static class BookEnglishNames
{
	private const int SliceMs = 2;

	// Vanilla categories the search names things from.
	private static readonly string[] VanillaCategories = { "ItemName", "NPCName", "Bestiary_Biomes", "Bestiary_Events", "Bestiary_Invasions" };

	private static Dictionary<string, string> texts;

	private static Queue<(Mod Mod, string File, string Prefix)> files;

	private static List<(string Prefix, string Text, bool Vanilla)> read;

	private static Task<Dictionary<string, string>> parsing;

	internal static bool Ready => texts != null;

	// Nothing to read in English.
	internal static bool Needed => Language.ActiveCulture != GameCulture.DefaultCulture;

	internal static long ReadMs { get; private set; }

	internal static void Clear()
	{
		texts = null;
		files = null;
		read = null;
		parsing = null;
		ReadMs = 0;
	}

	// The English text of a localization key, null when unknown or while not read yet.
	internal static string Of(string key)
	{
		return texts != null && key != null && texts.TryGetValue(key, out string value) ? value : null;
	}

	// Called every update: starts once, reads a slice, then waits for the worker. True on the update the texts arrive.
	internal static bool Pump()
	{
		if (texts != null || !Needed)
		{
			return false;
		}
		long start = Stopwatch.GetTimestamp();
		if (files == null)
		{
			files = new Queue<(Mod, string, string)>();
			read = new List<(string, string, bool)>();
			string vanilla = "Terraria.Localization.Content." + GameCulture.DefaultCulture.CultureInfo.Name.Replace('-', '_');
			foreach (string resource in typeof(Main).Assembly.GetManifestResourceNames().Where((string name) => name.StartsWith(vanilla, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)))
			{
				read.Add(("", Utils.ReadEmbeddedResource(resource), true));
			}
			foreach (Mod mod in ModLoader.Mods)
			{
				foreach (string file in mod.GetFileNames() ?? new List<string>())
				{
					if (file.EndsWith(".hjson", StringComparison.Ordinal) && LocalizationLoader.TryGetCultureAndPrefixFromPath(file, out GameCulture culture, out string prefix) && culture == GameCulture.DefaultCulture)
					{
						files.Enqueue((mod, file, prefix));
					}
				}
			}
		}
		if (parsing == null)
		{
			long end = start + Stopwatch.Frequency * SliceMs / 1000;
			while (files.Count > 0 && Stopwatch.GetTimestamp() < end)
			{
				(Mod mod, string file, string prefix) = files.Dequeue();
				try
				{
					using StreamReader reader = new StreamReader(new MemoryStream(mod.GetFileBytes(file)), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
					read.Add((prefix, reader.ReadToEnd(), false));
				}
				catch (Exception error)
				{
					ModContent.GetInstance<ModernRecipeBook>().Logger.Warn($"English names: {mod.Name}/{file} could not be read ({error.Message}).");
				}
			}
			if (files.Count == 0)
			{
				List<(string, string, bool)> all = read;
				read = null;
				parsing = Task.Run(() => Parse(all));
			}
		}
		ReadMs += (Stopwatch.GetTimestamp() - start) * 1000 / Stopwatch.Frequency;
		if (parsing == null || !parsing.IsCompleted)
		{
			return false;
		}
		texts = parsing.IsCompletedSuccessfully ? parsing.Result : new Dictionary<string, string>();
		if (!parsing.IsCompletedSuccessfully)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("English names could not be parsed; the quick search uses this language's names.", parsing.Exception);
		}
		parsing = null;
		ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"English names: {texts.Count} read ({ReadMs} ms on the main thread).");
		return true;
	}

	private static Dictionary<string, string> Parse(List<(string Prefix, string Text, bool Vanilla)> sources)
	{
		Dictionary<string, string> found = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach ((string prefix, string text, bool vanilla) in sources)
		{
			try
			{
				if (vanilla)
				{
					foreach (KeyValuePair<string, Dictionary<string, string>> category in JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(text))
					{
						if (VanillaCategories.Contains(category.Key))
						{
							foreach (KeyValuePair<string, string> entry in category.Value)
							{
								found[category.Key + "." + entry.Key] = entry.Value;
							}
						}
					}
				}
				else
				{
					Walk(JObject.Parse(HjsonValue.Parse(text).ToString()), prefix);
				}
			}
			catch (Exception)
			{
			}
		}
		// A name that is only a reference to another key ("{$Mods.X.Items.Y.DisplayName}") reads that key's text.
		foreach (string key in found.Keys.ToList())
		{
			Match reference = Regex.Match(found[key], "^\\{\\$([^}]+)\\}$");
			if (reference.Success)
			{
				found[key] = found.TryGetValue(reference.Groups[1].Value, out string target) && !target.Contains("{$") ? target : "";
			}
		}
		return found;
		void Walk(JToken token, string path)
		{
			if (token is JObject obj)
			{
				foreach (JProperty property in obj.Properties())
				{
					Walk(property.Value, path.Length == 0 ? property.Name : path + "." + property.Name);
				}
			}
			else if (token is JValue { Type: JTokenType.String } value && path.EndsWith(".DisplayName", StringComparison.Ordinal))
			{
				found[path.Replace(".$parentVal", "")] = (string)value;
			}
		}
	}
}
