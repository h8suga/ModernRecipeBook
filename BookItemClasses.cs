using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.Utilities;

namespace ModernRecipeBook;

[Flags]
internal enum BookClass
{
	None = 0,
	Melee = 1,
	Ranged = 2,
	Magic = 4,
	Summon = 8,
	Rogue = 16
}

// The classes an item serves. Weapons and ammo go by their damage type; armor, accessories and buff potions by
// what they change on a throwaway player compared with a bare one (class damage, crit, speed, mana, minions,
// ammo saving, stealth). An armor piece also serves every class its set serves (a Cobalt Breastplate is part of
// the melee, ranged and magic Cobalt sets). Bonuses to every class (Avenger Emblem, Well Fed) tag nothing: they
// would put the same items under every button. Built once per load; item defaults never change in game.
internal static class BookItemClasses
{
	private readonly record struct Stat(Func<Player, float> Read, BookClass Class, bool Secondary = false, bool LowerIsBetter = false);

	private static readonly Dictionary<int, BookClass> classes = new Dictionary<int, BookClass>();

	private static bool built;

	// Rogue is Calamity's; without it the few throwing weapons other mods add count as ranged (vanilla 1.4 moved its
	// own throwing weapons there, and throwing takes ranged prefixes).
	internal static bool RogueShown => ModLoader.HasMod("CalamityMod");

	// The sidebar row: All first, Rogue only with Calamity.
	internal static BookClass[] Buttons => RogueShown
		? new BookClass[6] { BookClass.None, BookClass.Melee, BookClass.Ranged, BookClass.Magic, BookClass.Summon, BookClass.Rogue }
		: new BookClass[5] { BookClass.None, BookClass.Melee, BookClass.Ranged, BookClass.Magic, BookClass.Summon };

	internal static BookClass Get(int type)
	{
		if (!classes.TryGetValue(type, out var value))
		{
			return BookClass.None;
		}
		return value;
	}

	internal static string Label(BookClass value, bool ru)
	{
		return value switch
		{
			BookClass.Melee => ru ? "Ближний бой" : "Melee",
			BookClass.Ranged => ru ? "Дальний бой" : "Ranged",
			BookClass.Magic => ru ? "Магия" : "Magic",
			BookClass.Summon => ru ? "Призыв" : "Summoner",
			BookClass.Rogue => ru ? "Разбойник" : "Rogue",
			_ => ru ? "Все классы" : "All classes",
		};
	}

	internal static int IconType(BookClass value)
	{
		switch (value)
		{
		case BookClass.Melee:
			return ItemID.TerraBlade;
		case BookClass.Ranged:
			return ItemID.MoltenFury;
		case BookClass.Magic:
			return ItemID.WaterBolt;
		case BookClass.Summon:
			return ItemID.ImpStaff;
		case BookClass.Rogue:
			if (ModContent.TryFind<ModItem>("CalamityMod/GildedDagger", out var dagger))
			{
				return dagger.Type;
			}
			return ItemID.Shuriken;
		default:
			return ItemID.None;
		}
	}

	internal static void Clear()
	{
		classes.Clear();
		built = false;
		bonuses.Clear();
		bonusPlayer = null;
	}

	internal static void Build()
	{
		if (built)
		{
			return;
		}
		built = true;
		classes.Clear();
		Stopwatch timer = Stopwatch.StartNew();
		BookClass[] byDamage = new BookClass[DamageClassLoader.DamageClassCount];
		for (int t = 0; t < byDamage.Length; t++)
		{
			byDamage[t] = Of(DamageClassLoader.GetDamageClass(t));
		}
		bool[] wasActive = Main.projectile.Select((Projectile p) => p.active).ToArray();
		Probe probe = new Probe(byDamage);
		int weapons = 0;
		int equipment = 0;
		int potions = 0;
		int failures = 0;
		int tooltipOnly = 0;
		foreach (Item sample in ContentSamples.ItemsByType.Values)
		{
			if (sample == null || sample.IsAir || sample.vanity)
			{
				continue;
			}
			bool ammo = sample.ammo > 0 && !sample.notAmmo;
			if (BookCategories.Weapon(sample) || ammo)
			{
				BookClass value = ((sample.DamageType == null) ? BookClass.None : byDamage[sample.DamageType.Type]);
				Add(sample.type, (value == BookClass.None && ammo) ? BookClass.Ranged : value);
				weapons++;
				continue;
			}
			int slot = BookArmorSets.Slot(sample);
			bool potion = sample.consumable && sample.buffType > 0;
			if (slot < 0 && !sample.accessory && !potion)
			{
				continue;
			}
			try
			{
				Item item = Copy(sample);
				if (slot >= 0)
				{
					Add(sample.type, probe.Classes(delegate(Player p)
					{
						Wear(p, item);
					}));
					equipment++;
				}
				else
				{
					BookClass value = probe.Classes(delegate(Player p)
					{
						if (item.accessory)
						{
							p.armor[3] = item;
							p.GrantArmorBenefits(item);
							p.ApplyEquipFunctional(item, hideVisual: true);
						}
						else
						{
							p.buffType[0] = item.buffType;
							p.buffTime[0] = 60;
							p.UpdateBuffs(p.whoAmI);
						}
					});
					if (value == BookClass.None && !probe.Generic && sample.ModItem != null)
					{
						value = FromTooltip(sample);
						tooltipOnly += ((value != BookClass.None) ? 1 : 0);
					}
					// The same bonus spelled out for each class is a bonus for everyone (armor is different: a
					// breastplate shared by five class sets does belong to all five).
					Add(sample.type, (BitOperations.PopCount((uint)value) >= 4) ? BookClass.None : value);
					equipment += (sample.accessory ? 1 : 0);
					potions += ((!sample.accessory) ? 1 : 0);
				}
			}
			catch
			{
				failures++;
			}
		}
		// A piece serves whatever its whole set serves: the three pieces plus the set bonus.
		foreach (BookArmorSets.Set set in ContentSamples.ItemsByType.Keys.Select(BookArmorSets.Get).Where((BookArmorSets.Set s) => s != null).Distinct())
		{
			foreach (int[] combination in Combinations(set))
			{
				try
				{
					Item[] parts = combination.Select((int type) => Copy(ContentSamples.ItemsByType[type])).ToArray();
					BookClass value = probe.Classes(delegate(Player p)
					{
						foreach (Item part in parts)
						{
							Wear(p, part);
						}
						p.UpdateArmorSets(p.whoAmI);
					});
					foreach (int type in combination)
					{
						Add(type, value);
					}
				}
				catch
				{
					failures++;
				}
			}
		}
		RemoveNewProjectiles(wasActive);
		Audit(timer.ElapsedMilliseconds, weapons, equipment, potions, tooltipOnly, failures);
	}

	// Code that ignores the whoAmI guard may still have spawned something for the throwaway player.
	private static void RemoveNewProjectiles(bool[] wasActive)
	{
		for (int i = 0; i < wasActive.Length; i++)
		{
			if (Main.projectile[i].active && !wasActive[i])
			{
				Main.projectile[i].active = false;
			}
		}
	}

	private static Player bonusPlayer;

	// Per language and combination, read once: the card asks every frame.
	private static readonly Dictionary<string, string> bonuses = new Dictionary<string, string>(StringComparer.Ordinal);

	// The set bonus the game gives these three pieces, read from Player.setBonus after vanilla's UpdateArmorSets
	// (which runs the mods' IsArmorSet / UpdateArmorSet too) on a throwaway player, in the current language.
	// "" when the set has none or its code failed.
	internal static string SetBonus(int[] combination)
	{
		string key = Language.ActiveCulture.Name + ":" + string.Join(",", combination);
		if (bonuses.TryGetValue(key, out var text))
		{
			return text;
		}
		text = "";
		bool[] wasActive = Main.projectile.Select((Projectile p) => p.active).ToArray();
		try
		{
			if (bonusPlayer == null)
			{
				bonusPlayer = new Player();
				Probe.Prepare(bonusPlayer);
			}
			Probe.Undress(bonusPlayer);
			foreach (int type in combination)
			{
				Wear(bonusPlayer, Copy(ContentSamples.ItemsByType[type]));
			}
			bonusPlayer.UpdateArmorSets(bonusPlayer.whoAmI);
			text = bonusPlayer.setBonus?.Trim() ?? "";
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("MRB set bonus of " + string.Join(" + ", combination.Select(Lang.GetItemNameValue)) + " failed: " + error.Message);
		}
		finally
		{
			RemoveNewProjectiles(wasActive);
		}
		bonuses[key] = text;
		return text;
	}

	// Mod accessories and potions whose whole effect is a flag their own player code reads later (Calamity's
	// Filthy Glove: "Stealth strikes ...") change nothing the probe sees; a class word in the tooltip decides.
	// Matched at the start of a word ("мана" is not in "кармана"), so stems cover the inflected forms.
	private static readonly (BookClass Class, string[] Words)[] TooltipWords = new (BookClass, string[])[5]
	{
		(BookClass.Melee, new string[4] { "melee", "ближнего боя", "ближнем бою", "ближний бой" }),
		(BookClass.Ranged, new string[4] { "ranged", "дальнего боя", "дальнем бою", "дальний бой" }),
		(BookClass.Magic, new string[8] { "magic", "mana", "магическ", "магии", "мана", "маны", "ману", "маной" }),
		(BookClass.Summon, new string[8] { "minion", "sentr", "whip", "summon damage", "миньон", "прислужник", "турел", "кнут" }),
		(BookClass.Rogue, new string[4] { "rogue", "stealth", "разбойн", "скрытн" })
	};

	private static bool StartsWord(string text, string key)
	{
		for (int i = text.IndexOf(key, StringComparison.Ordinal); i >= 0; i = text.IndexOf(key, i + 1, StringComparison.Ordinal))
		{
			if (i == 0 || !char.IsLetter(text[i - 1]))
			{
				return true;
			}
		}
		return false;
	}

	private static BookClass FromTooltip(Item item)
	{
		ItemTooltip tooltip = item.ToolTip;
		if (tooltip == null)
		{
			return BookClass.None;
		}
		string text = string.Join("\n", Enumerable.Range(0, tooltip.Lines).Select(tooltip.GetLine)).ToLowerInvariant();
		BookClass value = BookClass.None;
		foreach (var (cls, words) in TooltipWords)
		{
			if (words.Any((string word) => StartsWord(text, word)))
			{
				value |= ((cls == BookClass.Rogue && !RogueShown) ? BookClass.Ranged : cls);
			}
		}
		// A tooltip that names (nearly) every class describes a bonus for all of them.
		if (BitOperations.PopCount((uint)value) < 4)
		{
			return value;
		}
		return BookClass.None;
	}

	private static void Add(int type, BookClass value)
	{
		classes[type] = Get(type) | value;
	}

	// Damage classes count for every class they inherit effects from: Calamity's melee/ranged hybrid for both,
	// whips (summon with melee speed) for summon, true melee for melee, rogue for throwing.
	private static BookClass Of(DamageClass damage)
	{
		if (damage == null || damage == DamageClass.Default)
		{
			return BookClass.None;
		}
		BookClass value = BookClass.None;
		if (damage.CountsAsClass(DamageClass.Melee))
		{
			value |= BookClass.Melee;
		}
		if (damage.CountsAsClass(DamageClass.Ranged))
		{
			value |= BookClass.Ranged;
		}
		if (damage.CountsAsClass(DamageClass.Magic))
		{
			value |= BookClass.Magic;
		}
		if (damage.CountsAsClass(DamageClass.Summon))
		{
			value |= BookClass.Summon;
		}
		if (damage.CountsAsClass(DamageClass.Throwing))
		{
			value |= (RogueShown ? BookClass.Rogue : BookClass.Ranged);
		}
		// Counting as (nearly) every class, like Calamity's all-class type, is no class at all.
		if (BitOperations.PopCount((uint)value) < 4)
		{
			return value;
		}
		return BookClass.None;
	}

	private static Item Copy(Item sample)
	{
		Item item = sample.Clone();
		// The probe does not depend on the world's difficulty; expert-only accessories still say what they do.
		item.expertOnly = false;
		item.masterOnly = false;
		// Calamity reads the reserving player's armor in IsArmorSet; point it at the empty slot, not at the local player.
		item.playerIndexTheItemIsReservedFor = Main.maxPlayers;
		return item;
	}

	private static void Wear(Player p, Item item)
	{
		int slot = BookArmorSets.Slot(item);
		p.armor[slot] = item;
		switch (slot)
		{
		case 0:
			p.head = item.headSlot;
			break;
		case 1:
			p.body = item.bodySlot;
			break;
		default:
			p.legs = item.legSlot;
			break;
		}
		p.GrantArmorBenefits(item);
	}

	// Modded sets list the combinations their IsArmorSet accepts; vanilla families combine freely.
	private static IEnumerable<int[]> Combinations(BookArmorSets.Set set)
	{
		if (set.Modded)
		{
			return set.Combinations.Take(64);
		}
		return (from head in set.Parts[0]
			from body in set.Parts[1]
			from legs in set.Parts[2]
			select new int[3] { head, body, legs }).Take(64);
	}

	private static void Audit(long milliseconds, int weapons, int equipment, int potions, int tooltipOnly, int failures)
	{
		ModernRecipeBook mod = ModContent.GetInstance<ModernRecipeBook>();
		string totals = string.Join(", ", from BookClass value in Buttons.Skip(1)
			select Label(value, ru: false).ToLowerInvariant() + " " + classes.Values.Count((BookClass c) => (c & value) != 0));
		mod.Logger.Info($"Class audit ({milliseconds} ms) {totals}; checked {weapons} weapons/ammo, {equipment} armor/accessories, {potions} potions ({tooltipOnly} of them by tooltip); {classes.Values.Count((BookClass c) => c == BookClass.None)} without class, {failures} failed.");
		// Well-known items; a mismatch here means the probe broke, not that the item changed.
		List<string> wrong = new List<string>();
		Check(ItemID.MeteorHelmet, BookClass.Magic);
		Check(ItemID.NecroHelmet, BookClass.Ranged);
		Check(ItemID.SpiderMask, BookClass.Summon);
		Check(ItemID.CobaltBreastplate, BookClass.Melee | BookClass.Ranged | BookClass.Magic);
		Check(ItemID.WarriorEmblem, BookClass.Melee);
		Check(ItemID.RangerEmblem, BookClass.Ranged);
		Check(ItemID.SorcererEmblem, BookClass.Magic);
		Check(ItemID.SummonerEmblem, BookClass.Summon);
		Check(ItemID.PapyrusScarab, BookClass.Summon);
		Check(ItemID.ManaFlower, BookClass.Magic);
		Check(ItemID.MagicQuiver, BookClass.Ranged);
		Check(ItemID.FeralClaws, BookClass.Melee);
		Check(ItemID.AvengerEmblem, BookClass.None);
		Check(ItemID.MagicPowerPotion, BookClass.Magic);
		Check(ItemID.WoodenArrow, BookClass.Ranged);
		Check(ItemID.FireWhip, BookClass.Summon);
		if (RogueShown)
		{
			Check(Find("CalamityMod/UmbraphileHood"), BookClass.Rogue);
			Check(Find("CalamityMod/SulphurousBreastplate"), BookClass.Rogue);
			Check(Find("CalamityMod/SilencingSheath"), BookClass.Rogue);
			Check(Find("CalamityMod/RogueEmblem"), BookClass.Rogue);
			Check(Find("CalamityMod/GildedDagger"), BookClass.Rogue);
		}
		mod.Logger.Info("Class audit spot checks: " + ((wrong.Count == 0) ? "all as expected" : ("unexpected " + string.Join(", ", wrong))));
		static int Find(string name)
		{
			if (!ModContent.TryFind<ModItem>(name, out var modItem))
			{
				return 0;
			}
			return modItem.Type;
		}
		void Check(int type, BookClass want)
		{
			if (type <= 0)
			{
				wrong.Add("a missing Calamity item");
				return;
			}
			BookClass got = Get(type);
			if (got != want)
			{
				wrong.Add($"{Lang.GetItemNameValue(type)}={got} (want {want})");
			}
		}
	}

	private sealed class Probe
	{
		private static readonly UnifiedRandom Steady = new SteadyRandom();

		// The outcome a roll usually has: "1 in n" fails, ranges give their top.
		private sealed class SteadyRandom : UnifiedRandom
		{
			public override int Next(int maxValue)
			{
				return Math.Max(0, maxValue - 1);
			}

			public override int Next(int minValue, int maxValue)
			{
				return Math.Max(minValue, maxValue - 1);
			}

			public override double NextDouble()
			{
				return 0.999;
			}
		}

		private static readonly Item Air = new Item();

		private readonly Player player = new Player();

		private readonly List<Stat> stats;

		private readonly float[] baseline;

		internal Probe(BookClass[] byDamage)
		{
			Prepare(player);
			stats = Stats(byDamage);
			baseline = Read(null);
		}

		internal static void Prepare(Player player)
		{
			// Nobody's index: code guarded by whoAmI == Main.myPlayer (projectiles, sync, sounds) leaves it alone.
			player.whoAmI = Main.maxPlayers;
			// Buffs granted by set bonuses would be synced in multiplayer; the probe only needs the stats.
			Array.Fill(player.buffImmune, true);
			// "Summon the companion if it is missing" stays quiet.
			Array.Fill(player.ownedProjectileCounts, 1);
			// Scopes only zoom while a gun is held.
			player.inventory[player.selectedItem] = new Item(ItemID.Minishark);
		}

		// Bare: no armor, no buffs, effects reset.
		internal static void Undress(Player p)
		{
			for (int i = 0; i < p.armor.Length; i++)
			{
				p.armor[i] = Air;
			}
			Array.Clear(p.buffType);
			Array.Clear(p.buffTime);
			p.head = (p.body = (p.legs = -1));
			// Twice: the first pass clears flags the previous item left that ResetEffects itself reads.
			p.ResetEffects();
			p.ResetEffects();
		}

		// Whether the last item raised a bonus for every class (then it is generic, not unknown).
		internal bool Generic { get; private set; }

		internal BookClass Classes(Action<Player> equip)
		{
			float[] now = Read(equip);
			bool generic = false;
			BookClass primary = BookClass.None;
			BookClass secondary = BookClass.None;
			for (int i = 0; i < stats.Count; i++)
			{
				Stat stat = stats[i];
				float gain = (stat.LowerIsBetter ? (baseline[i] - now[i]) : (now[i] - baseline[i]));
				if (!(gain <= 0.0001f) && !float.IsNaN(gain))
				{
					if (stat.Class == BookClass.None)
					{
						generic = true;
					}
					else if (stat.Secondary)
					{
						secondary |= stat.Class;
					}
					else
					{
						primary |= stat.Class;
					}
				}
			}
			Generic = generic;
			// Well Fed and the like raise every class plus melee speed and minion knockback: still all classes.
			if (!generic)
			{
				return primary | secondary;
			}
			return primary;
		}

		private float[] Read(Action<Player> equip)
		{
			Player p = player;
			Undress(p);
			// Equip effects that roll (the Spore Sac spawns spores one time in 15, around the probe at 0,0 off the
			// map) take their common outcome, so the probe only ever reads stats and every run classifies alike.
			UnifiedRandom rand = Main.rand;
			Main.rand = Steady;
			try
			{
				equip?.Invoke(p);
			}
			finally
			{
				Main.rand = rand;
			}
			float[] values = new float[stats.Count];
			for (int j = 0; j < values.Length; j++)
			{
				values[j] = stats[j].Read(p);
			}
			return values;
		}

		private static List<Stat> Stats(BookClass[] byDamage)
		{
			List<Stat> list = new List<Stat>();
			for (int t = 0; t < byDamage.Length; t++)
			{
				DamageClass damage = DamageClassLoader.GetDamageClass(t);
				if (damage == DamageClass.Default)
				{
					continue;
				}
				// A class that counts as no single class (Generic, all-class) marks a bonus for everyone.
				BookClass value = byDamage[t];
				list.Add(new Stat((Player p) => p.GetDamage(damage).ApplyTo(100f), value));
				list.Add(new Stat((Player p) => p.GetCritChance(damage), value));
				list.Add(new Stat((Player p) => p.GetArmorPenetration(damage), value));
				list.Add(new Stat((Player p) => p.GetAttackSpeed(damage), value, Secondary: true));
				list.Add(new Stat((Player p) => p.GetKnockback(damage).ApplyTo(100f), value, Secondary: true));
			}
			list.Add(new Stat((Player p) => p.statManaMax2, BookClass.Magic));
			list.Add(new Stat((Player p) => p.manaCost, BookClass.Magic, Secondary: false, LowerIsBetter: true));
			list.Add(new Stat((Player p) => p.manaRegenBonus, BookClass.Magic));
			list.Add(new Stat((Player p) => p.manaRegenDelayBonus, BookClass.Magic));
			list.Add(new Stat((Player p) => (p.manaFlower || p.manaMagnet || p.magicCuffs || p.manaRegenBuff) ? 1 : 0, BookClass.Magic));
			list.Add(new Stat((Player p) => p.maxMinions, BookClass.Summon));
			list.Add(new Stat((Player p) => p.maxTurrets, BookClass.Summon));
			list.Add(new Stat((Player p) => p.whipRangeMultiplier, BookClass.Summon));
			// Applied after the equip loop (Monk's Belt and the other Old One's Army accessories): +sentry, +minion damage.
			list.Add(new Stat((Player p) => p.dd2Accessory ? 1 : 0, BookClass.Summon));
			list.Add(new Stat((Player p) => (p.ammoCost80 || p.ammoCost75 || p.huntressAmmoCost90 || p.chloroAmmoCost80 || p.ammoPotion || p.magicQuiver || p.hasMoltenQuiver || p.archery || p.scope) ? 1 : 0, BookClass.Ranged));
			list.Add(new Stat((Player p) => p.arrowDamage.ApplyTo(100f) + p.bulletDamage.ApplyTo(100f) + p.specialistDamage.ApplyTo(100f), BookClass.Ranged));
			// Glove autoswing and knockback (applied after the equip loop), yoyo gear, magma stone, weapon imbues.
			list.Add(new Stat((Player p) => (p.autoReuseGlove || p.kbGlove || p.meleeScaleGlove || p.yoyoGlove || p.yoyoString || p.counterWeight > 0 || p.magmaStone || p.meleeEnchant > 0) ? 1 : 0, BookClass.Melee));
			if (RogueShown && ModContent.TryFind<ModPlayer>("CalamityMod/CalamityPlayer", out var calamity))
			{
				foreach (string name in new string[4] { "rogueStealthMax", "rogueVelocity", "stealthGenStandstill", "stealthGenMoving" })
				{
					FieldInfo field = calamity.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (field?.FieldType == typeof(float))
					{
						list.Add(new Stat((Player p) => (float)field.GetValue(p.GetModPlayer(calamity)), BookClass.Rogue));
					}
				}
			}
			return list;
		}
	}
}
