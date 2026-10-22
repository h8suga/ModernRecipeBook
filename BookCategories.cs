using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook;

internal static class BookCategories
{
	private static bool Tool(Item i)
	{
		if (i.pick <= 0 && i.axe <= 0)
		{
			return i.hammer > 0;
		}
		return true;
	}

	internal static bool Weapon(Item i)
	{
		if (i.damage > 0 && i.ammo == 0)
		{
			return !Tool(i);
		}
		return false;
	}

	private static bool Armor(Item i)
	{
		if (i.headSlot < 0 && i.bodySlot < 0)
		{
			return i.legSlot >= 0;
		}
		return true;
	}

	private static bool Food(Item i)
	{
		if (i.buffType != 26 && i.buffType != 206)
		{
			return i.buffType == 207;
		}
		return true;
	}

	private static bool Flag(bool[] values, int index)
	{
		if (index >= 0 && index < values.Length)
		{
			return values[index];
		}
		return false;
	}

	private static bool Pet(Item i)
	{
		if (i.buffType > 0)
		{
			if (!Flag(Main.vanityPet, i.buffType))
			{
				return Flag(Main.lightPet, i.buffType);
			}
			return true;
		}
		return false;
	}

	private static bool Whip(Item i)
	{
		if (i.shoot > 0)
		{
			return Flag(ProjectileID.Sets.IsAWhip, i.shoot);
		}
		return false;
	}

	private static bool Potion(Item i)
	{
		if (i.healLife <= 0 && i.healMana <= 0)
		{
			if (i.consumable && !Food(i) && !Pet(i) && i.mountType < 0 && i.buffType > 0)
			{
				return i.UseSound?.IsTheSameAs(SoundID.Item3) ?? false;
			}
			return false;
		}
		return true;
	}

	private static bool BuffPotion(Item i)
	{
		if (Potion(i) && i.buffType > 0 && !Food(i))
		{
			return !Pet(i);
		}
		return false;
	}

	private static bool TileFlag(Item i, bool[] flags)
	{
		if (i.createTile >= 0)
		{
			return Flag(flags, i.createTile);
		}
		return false;
	}

	private static bool Room(Item i, int[] tiles)
	{
		if (i.createTile >= 0)
		{
			return Array.IndexOf(tiles, i.createTile) >= 0;
		}
		return false;
	}

	private static bool BossSummon(Item i)
	{
		bool flag = ItemID.Sets.SortingPriorityBossSpawns[i.type] >= 0;
		if (flag)
		{
			bool flag2;
			switch (i.type)
			{
			case 29:
			case 50:
			case 109:
			case 1291:
			case 3124:
			case 3199:
			case 4263:
			case 4819:
			case 5358:
			case 5359:
			case 5360:
			case 5361:
			case 5437:
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = !flag2;
		}
		return flag;
	}

	// Item types only: the class (melee, ranged, magic, summoner, rogue) is picked with the class buttons above
	// the list, so there are no per-class or per-damage-type categories. A saved key that is gone falls back to All.
	internal static List<BookCategory> Create(HashSet<int> stations)
	{
		List<BookCategory> categories = new List<BookCategory>();
		Add("all", "Все предметы", "All items", 149, (Item i) => true);
		Add("weapons", "Всё оружие", "All weapons", 273, Weapon);
		Add("swords", "Мечи", "Swords", 4, (Item i) => Weapon(i) && i.CountsAsClass(DamageClass.Melee) && !i.noMelee && i.useStyle == 1);
		Add("yoyos", "Йо-йо", "Yoyos", 3278, (Item i) => Flag(ItemID.Sets.Yoyo, i.type));
		Add("bows", "Луки", "Bows", 39, (Item i) => Weapon(i) && i.useAmmo == AmmoID.Arrow);
		Add("guns", "Огнестрельное", "Guns", 96, (Item i) => Weapon(i) && i.useAmmo == AmmoID.Bullet);
		Add("launchers", "Ракетницы", "Rocket launchers", 759, (Item i) => Weapon(i) && i.useAmmo == AmmoID.Rocket);
		Add("summon", "Миньоны", "Minions", 2365, (Item i) => Weapon(i) && i.CountsAsClass(DamageClass.Summon) && !i.sentry && !Whip(i));
		Add("whips", "Кнуты", "Whips", 4672, Whip);
		Add("sentries", "Турели", "Sentries", 2366, (Item i) => i.sentry);
		Add("tools", "Все инструменты", "All tools", 1, Tool);
		Add("picks", "Кирки и буры", "Pickaxes", 3521, (Item i) => i.pick > 0);
		Add("axes", "Топоры", "Axes", 10, (Item i) => i.axe > 0);
		Add("hammers", "Молоты", "Hammers", 196, (Item i) => i.hammer > 0);
		Add("rods", "Удочки", "Fishing rods", 2289, (Item i) => i.fishingPole > 0);
		Add("bait", "Наживка", "Bait", 2002, (Item i) => i.bait > 0);
		Add("hooks", "Крюки", "Hooks", 84, (Item i) => i.shoot > 0 && Flag(Main.projHook, i.shoot));
		Add("armor", "Вся броня", "All armor", 81, (Item i) => Armor(i) && !i.vanity);
		Add("head", "Шлемы", "Helmets", 90, (Item i) => i.headSlot >= 0 && !i.vanity);
		Add("body", "Нагрудники", "Chest armor", 83, (Item i) => i.bodySlot >= 0 && !i.vanity);
		Add("legs", "Поножи", "Leg armor", 77, (Item i) => i.legSlot >= 0 && !i.vanity);
		Add("vanity", "Внешний вид", "Vanity", 239, (Item i) => i.vanity && Armor(i));
		Add("accessories", "Аксессуары", "Accessories", 54, (Item i) => i.accessory);
		Add("wings", "Крылья", "Wings", 493, (Item i) => i.wingSlot > 0);
		Add("ammo", "Все боеприпасы", "All ammo", 40, (Item i) => i.ammo > 0 && !i.notAmmo);
		Add("arrows", "Стрелы", "Arrows", 41, (Item i) => i.ammo == AmmoID.Arrow && !i.notAmmo);
		Add("bullets", "Пули", "Bullets", 97, (Item i) => i.ammo == AmmoID.Bullet && !i.notAmmo);
		Add("rockets", "Ракеты", "Rockets", 771, (Item i) => i.ammo == AmmoID.Rocket && !i.notAmmo);
		Add("tiles", "Всё размещаемое", "All placeable", 9, (Item i) => i.createTile >= 0);
		Add("blocks", "Блоки", "Blocks", 2, (Item i) => i.createTile >= 0 && !TileFlag(i, Main.tileFrameImportant));
		Add("platforms", "Платформы", "Platforms", 94, (Item i) => TileFlag(i, TileID.Sets.Platforms));
		Add("walls", "Стены", "Walls", 26, (Item i) => i.createWall > 0);
		Add("furniture", "Мебель", "Furniture", 354, (Item i) => TileFlag(i, Main.tileFrameImportant));
		Add("stations", "Станции", "Stations", 36, (Item i) => i.createTile >= 0 && stations.Contains(i.createTile));
		Add("chests", "Сундуки", "Chests", 48, (Item i) => TileFlag(i, Main.tileContainer));
		Add("doors", "Двери", "Doors", 25, (Item i) => Room(i, TileID.Sets.RoomNeeds.CountsAsDoor));
		Add("chairs", "Стулья", "Chairs", 34, (Item i) => Room(i, TileID.Sets.RoomNeeds.CountsAsChair));
		Add("tables", "Столы", "Tables", 32, (Item i) => Room(i, TileID.Sets.RoomNeeds.CountsAsTable));
		Add("lights", "Освещение", "Light sources", 344, (Item i) => Room(i, TileID.Sets.RoomNeeds.CountsAsTorch));
		Add("torches", "Факелы", "Torches", 8, (Item i) => TileFlag(i, TileID.Sets.Torch));
		Add("wiring", "Механизмы", "Wiring", 509, (Item i) => ItemID.Sets.SortingPriorityWiring[i.type] >= 0);
		Add("potions", "Все зелья", "All potions", 188, Potion);
		Add("healing", "Лечебные зелья", "Healing potions", 499, (Item i) => i.healLife > 0 && !Food(i));
		Add("mana", "Зелья маны", "Mana potions", 189, (Item i) => i.healMana > 0 && !Food(i));
		Add("buffs", "Зелья усилений", "Buff potions", 292, BuffPotion);
		Add("food", "Еда и напитки", "Food and drinks", 2425, Food);
		Add("pets", "Питомцы", "Pets", 2420, (Item i) => i.buffType > 0 && Flag(Main.vanityPet, i.buffType));
		Add("lightpets", "Световые питомцы", "Light pets", 115, (Item i) => i.buffType > 0 && Flag(Main.lightPet, i.buffType));
		Add("mounts", "Ездовые питомцы", "Mounts", 2430, (Item i) => i.mountType >= 0);
		Add("carts", "Вагонетки", "Minecarts", 2343, (Item i) => i.mountType >= 0 && Flag(MountID.Sets.Cart, i.mountType));
		Add("dyes", "Красители", "Dyes", 1007, (Item i) => i.dye > 0);
		Add("hairdyes", "Краски для волос", "Hair dyes", 1977, (Item i) => i.hairDye >= 0);
		Add("summons", "Призыв боссов", "Boss summons", 43, BossSummon);
		Add("bags", "Мешки боссов", "Treasure bags", 3319, (Item i) => Flag(ItemID.Sets.BossBag, i.type));
		Add("crates", "Рыбацкие ящики", "Fishing crates", 2334, (Item i) => Flag(ItemID.Sets.IsFishingCrate, i.type));
		Add("critters", "Существа", "Critters", 2019, (Item i) => i.makeNPC > 0);
		Add("materials", "Материалы", "Materials", 75, (Item i) => i.material);
		Add("ores", "Руда", "Ores", 11, (Item i) => TileFlag(i, TileID.Sets.Ore));
		Add("bars", "Слитки", "Bars", 22, (Item i) => i.createTile == 239);
		Add("gems", "Самоцветы", "Gems", 182, (Item i) => i.createTile == 178);
		Add("extractinator", "Для экстрактора", "Extractinator", 424, (Item i) => ItemID.Sets.ExtractinatorMode[i.type] >= 0);
		Add("expert", "Эксперт", "Expert", 3335, (Item i) => i.expert);
		Add("master", "Мастер", "Master", 4929, (Item i) => i.master);
		Add("consumables", "Расходуемое", "Consumables", 166, (Item i) => i.consumable && i.createTile < 0 && i.createWall <= 0 && i.ammo == 0);
		return categories;
		void Add(string key, string ru, string en, int icon, Func<Item, bool> match)
		{
			categories.Add(new BookCategory(key, ru, en, icon, match));
		}
	}
}
