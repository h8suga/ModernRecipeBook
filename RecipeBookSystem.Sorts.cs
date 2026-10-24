using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// Category sorts (Items, Recipes, Storage): the sort list adds the numbers that mean something for what is listed:
// damage for weapons, defense for armor, healing for potions. The group comes from the sidebar's category, or, on a
// category without one (All items), from what most listed entries are. A chosen sort the list no longer offers reads
// as Name A-Z, and comes back when the list fits again: SortMode keeps the player's choice, saved per tab.
public sealed partial class RecipeBookSystem
{
	// In the order an item in several picks its own: wings before accessories.
	private enum StatGroup
	{
		None,
		Wings,
		Weapon,
		Ammo,
		Armor,
		Accessory,
		Potion,
		Tool,
		Rod,
		Bait
	}

	// The group's sorts, each offered ascending then descending. Speed has no honest meaning for ammo; a rough damage
	// per second misleads for weapons that fire several projectiles, so there is none.
	private static readonly BookSortMode[][] GroupSorts = new BookSortMode[10][]
	{
		Array.Empty<BookSortMode>(),
		new BookSortMode[1] { BookSortMode.FlightTime },
		new BookSortMode[4] { BookSortMode.Damage, BookSortMode.Speed, BookSortMode.Crit, BookSortMode.Knockback },
		new BookSortMode[2] { BookSortMode.Damage, BookSortMode.Knockback },
		new BookSortMode[1] { BookSortMode.Defense },
		new BookSortMode[1] { BookSortMode.Defense },
		new BookSortMode[3] { BookSortMode.HealLife, BookSortMode.HealMana, BookSortMode.BuffTime },
		new BookSortMode[3] { BookSortMode.PickPower, BookSortMode.AxePower, BookSortMode.HammerPower },
		new BookSortMode[1] { BookSortMode.FishingPower },
		new BookSortMode[1] { BookSortMode.BaitPower }
	};

	// Every list: Items and Recipes also have Station; Storage has no recipes to rank.
	private static readonly BookSortMode[] ListSorts = new BookSortMode[6] { BookSortMode.Name, BookSortMode.ItemId, BookSortMode.Mod, BookSortMode.Station, BookSortMode.Rarity, BookSortMode.Value };

	private static readonly BookSortMode[] StorageSorts = new BookSortMode[5] { BookSortMode.Name, BookSortMode.ItemId, BookSortMode.Mod, BookSortMode.Rarity, BookSortMode.Value };

	private static readonly long ListMask = Mask(ListSorts);

	private static readonly long StorageMask = Mask(StorageSorts);

	private static readonly long[] GroupMasks = GroupSorts.Select(Mask).ToArray();

	// Per category index and per item type: the group, built with the category index.
	private static StatGroup[] CategoryStatGroups = Array.Empty<StatGroup>();

	private static StatGroup[] ItemStatGroups = Array.Empty<StatGroup>();

	// Modded rarities have no order of their own (Calamity's load alphabetically): after Purple, by the median value of
	// their items.
	private static int[] ModRarityRanks = Array.Empty<int>();

	private static readonly int[] GroupCounts = new int[10];

	// What the last pass listed.
	private static StatGroup SortGroup;

	private static BookSortMode[] GeneralSorts => StationFilterTab ? ListSorts : StorageSorts;

	private static BookSortMode[] GroupSortList => GroupSorts[(int)SortGroup];

	// The sort in effect: the chosen one while the list offers it, Name A-Z otherwise.
	private static BookSortMode ActiveSort => ((((StationFilterTab ? ListMask : StorageMask) | GroupMasks[(int)SortGroup]) >> (int)SortMode) & 1) != 0 ? SortMode : BookSortMode.Name;

	private static bool ActiveDescending => ActiveSort == SortMode && SortDescending;

	// The pass's sort, read once for its comparisons. Station and the numbers sort by a key per entry
	// (SortRankScratch, PrepareRanks), also once per pass.
	private static (BookSortMode Mode, bool Descending) SortPass;

	private static long Mask(BookSortMode[] modes)
	{
		long mask = 0L;
		foreach (BookSortMode mode in modes)
		{
			mask |= 1L << (int)mode;
		}
		return mask;
	}

	private static StatGroup GroupOf(string categoryKey)
	{
		return categoryKey switch
		{
			"wings" => StatGroup.Wings,
			"weapons" or "swords" or "yoyos" or "bows" or "guns" or "launchers" or "summon" or "whips" or "sentries" => StatGroup.Weapon,
			"ammo" or "arrows" or "bullets" or "rockets" => StatGroup.Ammo,
			"armor" or "head" or "body" or "legs" => StatGroup.Armor,
			"accessories" => StatGroup.Accessory,
			"potions" or "healing" or "mana" or "buffs" or "food" => StatGroup.Potion,
			"tools" or "picks" or "axes" or "hammers" => StatGroup.Tool,
			"rods" => StatGroup.Rod,
			"bait" => StatGroup.Bait,
			_ => StatGroup.None,
		};
	}

	// After the category index (Rebuild).
	private static void BuildStatGroups()
	{
		CategoryStatGroups = ItemCategoryList.Select((BookCategory category) => GroupOf(category.Key)).ToArray();
		ItemStatGroups = new StatGroup[ItemLoader.ItemCount];
		foreach (KeyValuePair<int, HashSet<int>> pair in ItemCategories)
		{
			StatGroup group = StatGroup.None;
			foreach (int c in pair.Value)
			{
				StatGroup next = CategoryStatGroups[c];
				if (next != StatGroup.None && (group == StatGroup.None || next < group))
				{
					group = next;
				}
			}
			if ((uint)pair.Key < (uint)ItemStatGroups.Length)
			{
				ItemStatGroups[pair.Key] = group;
			}
		}
		Dictionary<int, List<int>> values = new Dictionary<int, List<int>>();
		foreach (Item item in ContentSamples.ItemsByType.Values)
		{
			if (item != null && item.rare >= ItemRarityID.Count)
			{
				if (!values.TryGetValue(item.rare, out var list))
				{
					list = (values[item.rare] = new List<int>());
				}
				list.Add(item.value);
			}
		}
		ModRarityRanks = new int[Math.Max(0, RarityLoader.RarityCount - ItemRarityID.Count)];
		int rank = ItemRarityID.Purple + 1;
		foreach (int rare in from pair in values
			orderby Median(pair.Value), pair.Key
			select pair.Key)
		{
			if (rare - ItemRarityID.Count < ModRarityRanks.Length)
			{
				ModRarityRanks[rare - ItemRarityID.Count] = rank++;
			}
		}
		static int Median(List<int> list)
		{
			list.Sort();
			return list[list.Count / 2];
		}
	}

	// The sidebar's category when it has a group, else what more than half the listed entries are.
	private static StatGroup ListGroup(List<int> first, List<int> second)
	{
		StatGroup chosen = ((uint)Category < (uint)CategoryStatGroups.Length) ? CategoryStatGroups[Category] : StatGroup.None;
		if (chosen != StatGroup.None)
		{
			return chosen;
		}
		Array.Clear(GroupCounts);
		Count(first);
		Count(second);
		int total = first.Count + second.Count;
		for (int g = 1; g < GroupCounts.Length; g++)
		{
			if (GroupCounts[g] * 2 > total)
			{
				return (StatGroup)g;
			}
		}
		return StatGroup.None;
		static void Count(List<int> entries)
		{
			foreach (int index in entries)
			{
				int type = Result(index).type;
				GroupCounts[(int)(((uint)type < (uint)ItemStatGroups.Length) ? ItemStatGroups[type] : StatGroup.None)]++;
			}
		}
	}

	private static void PrepareRanks(List<int> entries)
	{
		BookSortMode mode = SortPass.Mode;
		if (StorageView && SortRankScratch.Length < BookSources.Items.Count)
		{
			Array.Resize(ref SortRankScratch, BookSources.Items.Count);
		}
		foreach (int index in entries)
		{
			if ((uint)index < (uint)SortRankScratch.Length)
			{
				SortRankScratch[index] = (mode == BookSortMode.Station) ? StationSortRank(index) : StatKey(Result(index), mode);
			}
		}
	}

	// The number as the item has it (Storage: the real stack, prefix included); NoRank when it has none, and that
	// goes last both ways. Speed is negated: higher is faster.
	private static int StatKey(Item item, BookSortMode mode)
	{
		bool weapon = item.damage > 0;
		int value = mode switch
		{
			BookSortMode.Rarity => RarityRank(item.rare),
			BookSortMode.Value => item.value,
			BookSortMode.Damage => item.damage,
			BookSortMode.Speed => (weapon && item.useAnimation > 0) ? (-item.useAnimation) : 0,
			BookSortMode.Crit => (weapon && item.DamageType.UseStandardCritCalcs) ? (item.crit + 4) : 0,
			BookSortMode.Knockback => weapon ? ((int)Math.Round(item.knockBack * 100f)) : 0,
			BookSortMode.Defense => item.defense,
			BookSortMode.FlightTime => ((uint)item.wingSlot < (uint)(ArmorIDs.Wing.Sets.Stats?.Length ?? 0)) ? ArmorIDs.Wing.Sets.Stats[item.wingSlot].FlyTime : 0,
			BookSortMode.HealLife => item.healLife,
			BookSortMode.HealMana => item.healMana,
			BookSortMode.BuffTime => (item.buffType > 0) ? item.buffTime : 0,
			BookSortMode.PickPower => item.pick,
			BookSortMode.AxePower => item.axe,
			BookSortMode.HammerPower => item.hammer,
			BookSortMode.FishingPower => item.fishingPole,
			BookSortMode.BaitPower => item.bait,
			_ => 0,
		};
		if (mode == BookSortMode.Speed)
		{
			return (value == 0) ? NoRank : value;
		}
		return (value > 0) ? value : NoRank;
	}

	// Gray -1 to Purple 11 as the game has them, then modded rarities, all above 0; Expert, Master and Quest (negative
	// ids) are no tier and go last.
	private static int RarityRank(int rare)
	{
		if (rare >= ItemRarityID.Gray && rare <= ItemRarityID.Purple)
		{
			return rare + 2;
		}
		int mod = rare - ItemRarityID.Count;
		return ((uint)mod < (uint)ModRarityRanks.Length && ModRarityRanks[mod] > 0) ? (ModRarityRanks[mod] + 2) : 0;
	}

	// The option list: Bestiary's own, or the general sorts then the group's, each ascending then descending.
	private static (BookSortMode Mode, bool Descending) SortOptionAt(int option)
	{
		if (BestiaryView)
		{
			return MobSortOptions[Math.Clamp(option, 0, MobSortOptions.Length - 1)];
		}
		int general = GeneralSorts.Length * 2;
		if (option < general)
		{
			return (GeneralSorts[option / 2], option % 2 == 1);
		}
		BookSortMode[] group = GroupSortList;
		int k = Math.Clamp(option - general, 0, Math.Max(0, group.Length * 2 - 1));
		return (group.Length == 0) ? (BookSortMode.Name, false) : (group[k / 2], k % 2 == 1);
	}

	// The field, its short form for the closed dropdown (room for about ten letters, a little more shrinks to fit), and
	// the two directions.
	private static (string Field, string Short, string Up, string Down) SortWords(BookSortMode mode)
	{
		return mode switch
		{
			BookSortMode.Station => (T("Станция", "Station"), T("Станция", "Station"), T("от слабой", "weakest first"), T("от сильной", "strongest first")),
			BookSortMode.Rarity => (T("Редкость", "Rarity"), T("Редкость", "Rarity"), T("от низкой", "lowest first"), T("от высокой", "highest first")),
			BookSortMode.Value => (T("Цена", "Value"), T("Цена", "Value"), T("от низкой", "lowest first"), T("от высокой", "highest first")),
			BookSortMode.Damage => (T("Урон", "Damage"), T("Урон", "Damage"), T("от меньшего", "lowest first"), T("от большего", "highest first")),
			BookSortMode.Speed => (T("Скорость", "Speed"), T("Скорость", "Speed"), T("от медленных", "slowest first"), T("от быстрых", "fastest first")),
			BookSortMode.Crit => (T("Шанс крита", "Crit chance"), T("Крит", "Crit"), T("от меньшего", "lowest first"), T("от большего", "highest first")),
			BookSortMode.Knockback => (T("Отбрасывание", "Knockback"), T("Отбрас.", "Knockback"), T("от слабого", "weakest first"), T("от сильного", "strongest first")),
			BookSortMode.Defense => (T("Защита", "Defense"), T("Защита", "Defense"), T("от меньшей", "lowest first"), T("от большей", "highest first")),
			BookSortMode.FlightTime => (T("Время полёта", "Flight time"), T("Полёт", "Flight"), T("от короткого", "shortest first"), T("от долгого", "longest first")),
			BookSortMode.HealLife => (T("Лечение", "Heals life"), T("Лечение", "Healing"), T("от меньшего", "least first"), T("от большего", "most first")),
			BookSortMode.HealMana => (T("Мана", "Heals mana"), T("Мана", "Mana"), T("от меньшего", "least first"), T("от большего", "most first")),
			BookSortMode.BuffTime => (T("Длительность", "Duration"), T("Время", "Duration"), T("от короткой", "shortest first"), T("от долгой", "longest first")),
			BookSortMode.PickPower => (T("Сила кирки", "Pickaxe power"), T("Кирка", "Pickaxe"), T("от меньшей", "lowest first"), T("от большей", "highest first")),
			BookSortMode.AxePower => (T("Сила топора", "Axe power"), T("Топор", "Axe"), T("от меньшей", "lowest first"), T("от большей", "highest first")),
			BookSortMode.HammerPower => (T("Сила молота", "Hammer power"), T("Молот", "Hammer"), T("от меньшей", "lowest first"), T("от большей", "highest first")),
			BookSortMode.FishingPower => (T("Сила удочки", "Fishing power"), T("Удочка", "Fishing"), T("от меньшей", "lowest first"), T("от большей", "highest first")),
			BookSortMode.BaitPower => (T("Сила наживки", "Bait power"), T("Наживка", "Bait"), T("от меньшей", "lowest first"), T("от большей", "highest first")),
			_ => ("", "", "", ""),
		};
	}

	private static string GroupLabel(StatGroup group)
	{
		return group switch
		{
			StatGroup.Wings => T("Крылья", "Wings"),
			StatGroup.Weapon => T("Оружие", "Weapons"),
			StatGroup.Ammo => T("Боеприпасы", "Ammo"),
			StatGroup.Armor => T("Броня", "Armor"),
			StatGroup.Accessory => T("Аксессуары", "Accessories"),
			StatGroup.Potion => T("Зелья и еда", "Potions and food"),
			StatGroup.Tool => T("Инструменты", "Tools"),
			StatGroup.Rod => T("Удочки", "Fishing rods"),
			_ => T("Наживка", "Bait"),
		};
	}
}
