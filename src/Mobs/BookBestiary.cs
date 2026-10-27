using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;


namespace ModernRecipeBook.Mobs;

internal enum BookMobKind
{
	Boss,
	MiniBoss,
	Enemy,
	Town,
	Critter
}

// One Bestiary entry. The NPCs the Bestiary counts as it (zombie skins, a worm's segments) are its variants.
internal sealed class BookMob
{
	internal int Index;

	internal int NetId;

	internal int[] Variants;

	internal BookMobKind Kind;

	internal string Mod;

	internal int Order;

	internal string[] Tags;

	internal NPC Sample => ContentSamples.NpcsByNetId[NetId];

	internal string Key => "npc:" + ContentSamples.NpcPersistentIdsByNetIds[NetId];
}

// The book's Bestiary tab reads the game's own Bestiary: its entries, their spawn tags (biomes, events,
// mods' biomes) and its order. Built once per load, the first time something needs it.
internal static class BookBestiary
{
	// A sidebar icon: an item, or a frame of a texture (the Bestiary's tag icons, a mod biome's icon).
	internal readonly record struct Icon(int ItemType, string Texture, Rectangle? Frame);

	// LabelKey: the game's localization key of its name, or the book's own for its kinds.
	internal sealed record Tag(string Key, string LabelKey, Icon Icon, HashSet<int> Members);

	// A mob of Calamity's Acid Rain: the first and last tier it spawns in (0 after the Eye of Cthulhu, 1 after
	// Aquatic Scourge, 2 after Polterghast), from a mini-boss table or not, and where (AcidRainSpawnRequirement:
	// Water, Land, Anywhere; Sky for Irradiated Slime).
	internal readonly record struct AcidRainSpawn(int Tier, int LastTier, bool MiniBoss, string Requirement);

	internal static readonly List<BookMob> Mobs = new List<BookMob>();

	internal static readonly List<Tag> Tags = new List<Tag>();

	private static readonly Dictionary<string, int> ByCredit = new Dictionary<string, int>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Tag> TagsByKey = new Dictionary<string, Tag>(StringComparer.Ordinal);

	private static readonly Dictionary<int, AcidRainSpawn> AcidRain = new Dictionary<int, AcidRainSpawn>();

	// The wiki's vanilla mini-bosses ("Mini-bosses" category, October 2026) that carry no boss flag.
	private static readonly HashSet<int> VanillaMiniBosses = new HashSet<int> { 325, 327, 344, 345, 346, 392, 395, 477, 491, 564, 565, 576, 577, 618 };

	private const string TagTexture = "Images/UI/Bestiary/Icon_Tags_Shadow";

	internal static bool Built { get; private set; }

	internal static void Clear()
	{
		Mobs.Clear();
		Tags.Clear();
		ByCredit.Clear();
		TagsByKey.Clear();
		AcidRain.Clear();
		BookSpawnGates.Clear();
		Built = false;
	}

	internal static AcidRainSpawn? AcidRainOf(int type)
	{
		EnsureBuilt();
		return AcidRain.TryGetValue(type, out AcidRainSpawn spawn) ? spawn : null;
	}

	// Calamity spawns the Acid Rain from its own tables (AcidRainEvent.PossibleEnemiesPreHM and the rest, NPC type to
	// AcidRainSpawnData), tier by tier as CalamityGlobalNPC.EditSpawnPool picks them; its mobs have no SpawnChance and
	// no Bestiary spawn tags. Read without a reference; anything missing leaves the cards as they were.
	private static void ReadAcidRain()
	{
		if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
		{
			return;
		}
		try
		{
			Type events = calamity.Code.GetType("CalamityMod.Events.AcidRainEvent", throwOnError: true);
			foreach (var (field, tier, miniBoss) in new[] { ("PossibleEnemiesPreHM", 0, false), ("PossibleEnemiesAS", 1, false), ("PossibleMinibossesAS", 1, true), ("PossibleEnemiesPolter", 2, false), ("PossibleMinibossesPolter", 2, true) })
			{
				IDictionary table = (IDictionary)(events.GetField(field, BindingFlags.Static | BindingFlags.Public) ?? throw new MissingFieldException(events.FullName, field)).GetValue(null);
				foreach (DictionaryEntry pair in table)
				{
					int type = (int)pair.Key;
					object requirement = pair.Value.GetType().GetProperty("SpawnRequirement")?.GetValue(pair.Value) ?? throw new MissingMemberException("AcidRainSpawnData", "SpawnRequirement");
					AcidRain[type] = AcidRain.TryGetValue(type, out AcidRainSpawn first) ? first with { LastTier = tier } : new AcidRainSpawn(tier, tier, miniBoss, requirement.ToString());
				}
			}
			// Irradiated Slime falls from the sky after Aquatic Scourge until Polterghast (CalamityPlayer); Calamity adds
			// it to the AS table only once that tier first spawns.
			if (calamity.TryFind("IrradiatedSlime", out ModNPC slime))
			{
				AcidRain[slime.Type] = new AcidRainSpawn(1, 1, false, "Sky");
			}
		}
		catch (Exception error)
		{
			AcidRain.Clear();
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Calamity's Acid Rain tables could not be read; its mobs keep the Bestiary's spawn tags.", error);
		}
	}

	internal static void EnsureBuilt()
	{
		if (!Built)
		{
			Build();
		}
		// Apart from the index: it waits for a world and, on a multiplayer client, for the tiles it stands on.
		BookSpawnGates.EnsureBuilt();
	}

	// The entry a mob belongs to, through the Bestiary's credit (a zombie skin opens the Zombie entry).
	internal static int IndexOf(int netId)
	{
		EnsureBuilt();
		if (!ContentSamples.NpcBestiaryCreditIdsByNpcNetIds.TryGetValue(netId, out string credit) || !ByCredit.TryGetValue(credit, out int index))
		{
			return -1;
		}
		return index;
	}

	internal const string RecentKey = "recent";

	internal static int IndexOfCredit(string credit)
	{
		EnsureBuilt();
		return ByCredit.TryGetValue(credit, out int index) ? index : -1;
	}

	internal static Tag FindTag(string key)
	{
		return TagsByKey.GetValueOrDefault(key);
	}

	private static void Build()
	{
		Clear();
		Built = true;
		ReadAcidRain();
		Dictionary<string, List<int>> credited = new Dictionary<string, List<int>>(StringComparer.Ordinal);
		foreach (KeyValuePair<int, string> pair in ContentSamples.NpcBestiaryCreditIdsByNpcNetIds.OrderBy((KeyValuePair<int, string> x) => x.Key))
		{
			if (ContentSamples.NpcsByNetId.ContainsKey(pair.Key))
			{
				if (!credited.TryGetValue(pair.Value, out var list))
				{
					list = (credited[pair.Value] = new List<int>());
				}
				list.Add(pair.Key);
			}
		}
		(Dictionary<object, string> vanilla, Dictionary<object, int> groups) = VanillaTags();
		FieldInfo frameField = typeof(FilterProviderInfoElement).GetField("_filterIconFrame", BindingFlags.Instance | BindingFlags.NonPublic);
		FieldInfo modField = typeof(ModBestiaryInfoElement).GetField("_mod", BindingFlags.Instance | BindingFlags.NonPublic);
		FieldInfo iconField = typeof(ModBestiaryInfoElement).GetField("_iconPath", BindingFlags.Instance | BindingFlags.NonPublic);
		Dictionary<string, (int Group, int Order, string ModName, string LabelKey, Icon Icon)> found = new Dictionary<string, (int, int, string, string, Icon)>(StringComparer.Ordinal);
		Dictionary<string, HashSet<int>> members = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
		foreach (BestiaryEntry entry in Main.BestiaryDB.Entries)
		{
			int netId = entry.Info.OfType<NPCNetIdBestiaryInfoElement>().FirstOrDefault()?.NetId ?? 0;
			if (netId == 0 || !ContentSamples.NpcsByNetId.TryGetValue(netId, out var npc) || !ContentSamples.NpcBestiaryCreditIdsByNpcNetIds.TryGetValue(netId, out string credit) || ByCredit.ContainsKey(credit))
			{
				continue;
			}
			BookMob mob = new BookMob
			{
				Index = Mobs.Count,
				NetId = netId,
				Variants = BookEnemyRules.Variants(netId, credited.GetValueOrDefault(credit)),
				Kind = KindOf(npc, entry),
				Mod = npc.ModNPC?.Mod.Name ?? "Terraria",
				Order = ContentSamples.NpcBestiarySortingId.TryGetValue(netId, out int order) ? order : int.MaxValue
			};
			List<string> tags = new List<string>();
			foreach (IBestiaryInfoElement element in entry.Info)
			{
				try
				{
					if (vanilla.TryGetValue(element, out string vanillaKey))
					{
						if (!found.ContainsKey(vanillaKey))
						{
							Point frame = (frameField?.GetValue(element) is Point p) ? p : Point.Zero;
							found[vanillaKey] = (groups[element], frame.Y * 16 + frame.X, "", ((IFilterInfoProvider)element).GetDisplayNameKey(), new Icon(0, TagTexture, new Rectangle(frame.X, frame.Y, 16, 5)));
						}
						tags.Add(vanillaKey);
					}
					else if (element is ModBiomeBestiaryInfoElement biome)
					{
						Mod owner = modField?.GetValue(biome) as Mod;
						string key = "biome:" + (owner?.Name ?? "?") + "/" + biome.GetDisplayNameKey();
						if (!found.ContainsKey(key))
						{
							string icon = iconField?.GetValue(biome) as string;
							bool hasIcon = icon != null && ModContent.HasAsset(icon);
							found[key] = (3, 0, owner?.DisplayName ?? "", biome.GetDisplayNameKey(), hasIcon ? new Icon(0, icon, null) : new Icon(0, TagTexture, new Rectangle(0, 4, 16, 5)));
						}
						tags.Add(key);
					}
				}
				catch (Exception)
				{
				}
			}
			mob.Tags = tags.Distinct().ToArray();
			foreach (string tag in mob.Tags)
			{
				if (!members.TryGetValue(tag, out var set))
				{
					set = (members[tag] = new HashSet<int>());
				}
				set.Add(mob.Index);
			}
			ByCredit[credit] = mob.Index;
			Mobs.Add(mob);
		}
		Add("all", "Mods.ModernRecipeBook.BestiaryCategories.AllMobs", new Icon(ItemID.Book, null, null), Mobs.Select((BookMob m) => m.Index));
		// Members come from the player's list at filter time.
		Add(RecentKey, "Mods.ModernRecipeBook.BestiaryCategories.RecentlySeen", new Icon(ItemID.LifeformAnalyzer, null, null), Array.Empty<int>());
		Add("kind:boss", "Mods.ModernRecipeBook.BestiaryCategories.Bosses", new Icon(ItemID.SuspiciousLookingEye, null, null), Kind(BookMobKind.Boss));
		Add("kind:miniboss", "Mods.ModernRecipeBook.BestiaryCategories.MiniBosses", new Icon(ItemID.PumpkinMoonMedallion, null, null), Kind(BookMobKind.MiniBoss));
		Add("kind:enemy", "Mods.ModernRecipeBook.BestiaryCategories.Enemies", new Icon(ItemID.ZombieBanner, null, null), Kind(BookMobKind.Enemy));
		Add("kind:town", "Mods.ModernRecipeBook.BestiaryCategories.TownNPCs", new Icon(ItemID.Bed, null, null), Kind(BookMobKind.Town));
		Add("kind:critter", "Mods.ModernRecipeBook.BestiaryCategories.Critters", new Icon(ItemID.Bunny, null, null), Kind(BookMobKind.Critter));
		// Events, then the vanilla biomes in the Bestiary's own icon order (surface, underground, evil, ...),
		// then mods' biomes grouped by mod.
		foreach (var (key, info) in from x in found
			orderby x.Value.Group, x.Value.ModName, x.Value.Order, x.Key
			select (x.Key, x.Value))
		{
			Add(key, info.LabelKey, info.Icon, members[key]);
		}
		ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"Bestiary index: {Mobs.Count} entries, {Tags.Count - 7} spawn tags; bosses {Kind(BookMobKind.Boss).Count()}, mini-bosses {Kind(BookMobKind.MiniBoss).Count()}, town {Kind(BookMobKind.Town).Count()}, critters {Kind(BookMobKind.Critter).Count()}.");
		IEnumerable<int> Kind(BookMobKind kind)
		{
			return from m in Mobs
				where m.Kind == kind
				select m.Index;
		}
		void Add(string key, string labelKey, Icon icon, IEnumerable<int> indexes)
		{
			HashSet<int> set = new HashSet<int>(indexes);
			if (set.Count > 0 || key == "all" || key == RecentKey)
			{
				Tag tag = new Tag(key, labelKey, icon, set);
				Tags.Add(tag);
				TagsByKey[key] = tag;
			}
		}
	}

	// The Bestiary's own tags, by reference: events and invasions (group 1) and biomes (group 2). Times of day
	// are not categories.
	private static (Dictionary<object, string>, Dictionary<object, int>) VanillaTags()
	{
		Dictionary<object, string> keys = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
		Dictionary<object, int> groups = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
		Collect(typeof(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Invasions), 1);
		Collect(typeof(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Events), 1);
		Collect(typeof(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes), 2);
		return (keys, groups);
		void Collect(Type type, int group)
		{
			foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public))
			{
				if (field.GetValue(null) is FilterProviderInfoElement element)
				{
					keys[element] = "tag:" + element.GetDisplayNameKey();
					groups[element] = group;
				}
			}
		}
	}

	private static BookMobKind KindOf(NPC npc, BestiaryEntry entry)
	{
		if (npc.CountsAsACritter)
		{
			return BookMobKind.Critter;
		}
		if (npc.isLikeATownNPC)
		{
			return BookMobKind.Town;
		}
		// Calamity makes the Aquatic Scourge a boss only once it turns on the player (AquaticScourgeHead.AI sets NPC.boss), so
		// its sample is none: a mob of a boss's progression step (BookSteps: Calamity's own Boss Checklist entries) that drops
		// a Treasure Bag is one (AquaticScourgeHead.ModifyNPCLoot: ItemDropRule.BossBag).
		if (npc.boss || npc.type == NPCID.EaterofWorldsHead || (BookSteps.Npc(npc.type) >= 0 && DropsBossBag(npc.netID)))
		{
			return BookMobKind.Boss;
		}
		if (VanillaMiniBosses.Contains(npc.type) || entry.Info.Any((IBestiaryInfoElement x) => x is BossBestiaryInfoElement) || (AcidRain.TryGetValue(npc.type, out AcidRainSpawn rain) && rain.MiniBoss))
		{
			return BookMobKind.MiniBoss;
		}
		return BookMobKind.Enemy;
	}

	private static bool DropsBossBag(int netId)
	{
		try
		{
			List<DropRateInfo> rates = new List<DropRateInfo>();
			foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForNPCID(netId, includeGlobalDrops: false))
			{
				BookItemSources.ReportShown(rule, rates, new DropRateInfoChainFeed(1f));
			}
			return rates.Any((DropRateInfo r) => r.itemId > 0 && r.itemId < ItemID.Sets.BossBag.Length && ItemID.Sets.BossBag[r.itemId]);
		}
		catch (Exception)
		{
			return false;
		}
	}

	// The game's invasion names are long for a sidebar row ("Нашествие гоблинов"); these read the same, shorter.
	private static readonly Dictionary<string, string> ShortLabels = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["tag:Bestiary_Invasions.Goblins"] = "BestiaryCategories.GoblinArmy",
		["tag:Bestiary_Invasions.Pirates"] = "BestiaryCategories.Pirates",
		["tag:Bestiary_Invasions.Martian"] = "BestiaryCategories.Martians",
		["tag:Bestiary_Biomes.UndergroundSnow"] = "BestiaryCategories.UndergroundSnow"
	};

	// Tag labels come from the game's localization, so the sidebar follows the language.
	internal static string Label(Tag tag)
	{
		if (ShortLabels.TryGetValue(tag.Key, out var shortLabel))
		{
			return L(shortLabel);
		}
		return Language.GetTextValue(tag.LabelKey);
	}
}
