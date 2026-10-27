using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook.Mobs;

// How Boss Checklist describes a boss, mini-boss or event: its NPCs, what summons it and its spawn text. From Boss
// Checklist itself when it is loaded (its "GetBossInfoDictionary" call: vanilla's entries from its BossTracker and every
// mod's), else from Calamity's own registration with it (BookSteps.CalamityLogs, LogBoss/LogMiniBoss/LogEvent and their
// extra info). Read once, the first time a mob card asks.
internal static class BookBossInfo
{
	internal sealed record Entry(bool Event, Func<string> Name, int[] Npcs, int[] Items, Func<string> Info, bool FromChecklist);

	private static List<Entry> entries;

	internal static IEnumerable<Entry> Of(int type)
	{
		entries ??= Read();
		return entries.Where((Entry e) => e.Npcs.Contains(type));
	}

	private static List<Entry> Read()
	{
		List<Entry> list = new List<Entry>();
		try
		{
			if (ModLoader.TryGetMod("BossChecklist", out Mod checklist) && checklist.Call("GetBossInfoDictionary", ModContent.GetInstance<ModernRecipeBook>(), "2.0") is IDictionary dictionary)
			{
				foreach (object value in dictionary.Values)
				{
					if (value is Dictionary<string, object> info)
					{
						list.Add(new Entry(info.GetValueOrDefault("isEvent") is true, Text(info.GetValueOrDefault("displayName")), Ints(info.GetValueOrDefault("npcIDs")), Ints(info.GetValueOrDefault("spawnItems")), Text(info.GetValueOrDefault("spawnInfo")), true));
					}
				}
				return list;
			}
			BookSteps.EnsureBuilt();
			foreach (object[] args in BookSteps.CalamityLogs)
			{
				// Boss Checklist's Call: message, host mod, internal name, progression, downed, NPC id(s), extra info.
				Dictionary<string, object> extra = args.Length > 6 ? args[6] as Dictionary<string, object> : null;
				int[] npcs = Ints(args.Length > 5 ? args[5] : null);
				list.Add(new Entry((string)args[0] == "LogEvent", Text(extra?.GetValueOrDefault("displayName")), npcs, Ints(extra?.GetValueOrDefault("spawnItems")), Text(extra?.GetValueOrDefault("spawnInfo")), false));
			}
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Boss Checklist's entries could not be read; mob cards leave summons out.", error);
		}
		return list;
	}

	private static int[] Ints(object value)
	{
		return value switch
		{
			int one => new[] { one },
			IEnumerable<int> many => many.ToArray(),
			_ => Array.Empty<int>()
		};
	}

	// A LocalizedText or a function giving one; empty when its key has no text (Language gives the key back). Item chat
	// tags ("Use [i:43] at night") read as the item's name, which the book's plain text can show.
	private static Func<string> Text(object value)
	{
		return () =>
		{
			LocalizedText text = value switch
			{
				LocalizedText t => t,
				Func<LocalizedText> f => f(),
				_ => null
			};
			return text == null || text.Value == text.Key ? "" : Regex.Replace(text.Value, @"\[i(?:/[^:\]]*)?:([^\]]+)\]", (Match m) => int.TryParse(m.Groups[1].Value, out int id) ? Lang.GetItemNameValue(id) : (ModContent.TryFind(m.Groups[1].Value, out ModItem item) ? item.DisplayName.Value : m.Value));
		};
	}
}
