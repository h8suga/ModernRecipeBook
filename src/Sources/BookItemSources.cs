using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.Sources;

// One way to obtain an item. Language-neutral: How to Get turns it into text when it is shown, the
// progression turns it into a stage.
internal sealed record BookItemSource(BookAcquisitionKind Kind, int Item)
{
	// NPC net id: the enemy, the shopkeeper, the critter, the banner's mob, the Dye Trader.
	internal int Npc { get; init; }

	// What goes in or is used: the container, the shimmered or used item, the blank Music Box, the seed.
	internal int From { get; init; }

	// Several accepted inputs (any Strange Plant for the Dye Trader).
	internal int[] FromAny { get; init; } = Array.Empty<int>();

	// The world tile it comes out of, and the piece to draw as its icon.
	internal int Tile { get; init; } = -1;

	internal int FrameX { get; init; }

	internal int FrameY { get; init; }

	internal string Owner { get; init; } = "Terraria";

	// Drops and containers: the report as the game's drop UI computes it.
	internal DropRateInfo? Rate { get; init; }

	internal AbstractNPCShop Shop { get; init; }

	internal AbstractNPCShop.Entry Entry { get; init; }

	// Natural sources: chance per break/catch (0 when unknown) and stack.
	internal float Chance { get; init; }

	internal int Min { get; init; } = 1;

	internal int Max { get; init; } = 1;

	// Where: biome ids ("Bestiary_Biomes.Jungle" or a ModBiome "CalamityMod/SunkenSeaBiome") or plain localization keys.
	internal string[] Where { get; init; } = Array.Empty<string>();

	// What it grows on or stands on (plant anchors, tree ground).
	internal int[] Grounds { get; init; } = Array.Empty<int>();

	// A fixed condition key How to Get spells out (bloom time, hardmode, quest, pickaxe power...).
	internal string Note { get; init; } = "";

	internal int NoteValue { get; init; }
}

// Every way the book knows to obtain an item, enumerated once per mod load and read by How to Get and the
// progression stages alike. Drop rules, shops and the curated tables never change after loading, so the
// catalogue is built on first use and kept until the mod unloads; the world part (tile drops) needs a
// loaded world and is built separately by BookWorldSources.
internal static class BookItemSources
{
	// A mob with its own drop rules (split reports, see Report), its banner and its bug-net item.
	internal sealed record NpcSource(int NetId, NPC Npc, string Name, List<DropRateInfo> Reports, int Banner, int Critter);

	// An item's own routes: what it contains (bags, crates, presents; split reports) and what it shimmers into.
	internal sealed record ItemRoutes(int Item, List<DropRateInfo> Reports, int Shimmer);

	internal sealed record ShopSource(AbstractNPCShop Shop, List<AbstractNPCShop.Entry> Entries);

	internal sealed record DyeTrade(BookStage Gate, int[] Dyes);

	// Murasama-style curated sources ------------------------------------------------------------------

	// Loot Calamity 2.2.4 puts into chests and structures at world generation (Draedon labs, underground
	// shrines, Brimstone Crag, Vernal Pass; not the Astral shrine, which comes with the hardmode meteor) plus objects
	// that drop themselves when mined: lab machines, the Onyx Excavator of the cavern lab (OnyxExcavatorTile in the
	// Arsenal_Lab_Onyx schematic) and the Crag's broken pylon (BrimstoneCrag: CragsPylonBrokenTile). All of it can be
	// picked up before hardmode.
	internal static readonly HashSet<string> CalamityWorldgenItems = Set("BloodyMary CoastalDemonfish CorruptionEffigy CrimsonEffigy CyanSeekingMechanism DraedonsLogHell DraedonsLogJungle DraedonsLogPlanetoid DraedonsLogSnowBiome DraedonsLogSunkenSea DubiousPlating EncryptedSchematicHell EncryptedSchematicIce EncryptedSchematicJungle EncryptedSchematicPlanetoid EncryptedSchematicSunkenSea FellerofEvergreens FrozenCube FungalSymbiote GladiatorsLocket GreenSeekingMechanism HadalStew LabSeekingMechanism LuxorsGift MysteriousCircuitry OddMushroom PlasmaDriveCore PrismShard RedSeekingMechanism SlagfireDouser SlurperPole SpelunkersAmulet SulphurskinPotion SuspiciousScrap Terminus TrinketofChi UnstableGraniteCore VoidTorch WhiteSeekingMechanism YellowSeekingMechanism PowerCellFactoryItem ChargingStationItem OnyxExcavatorKey CragsPylon");

	// World generation loot that waits for a boss, with no other source: the Astral Chest AstralChestGeneration puts
	// in the Dungeon opens after Astrum Aureus (AstralChestLocked.UnlockChest), and only then can the emptied chest itself
	// be broken (Chest.CanDestroyChest); the Hell lab's Murasama lies there from the start but only swings after the
	// Devourer of Gods (Murasama.CanUseItem). Item, place, chest icon, gate.
	internal static readonly (string Item, string Place, string Icon, string Gate)[] CalamityGatedWorldgenItems =
	{
		("HeavenfallenStardisk", "AstralChest", "AstralChest", "CalamityMod/AstrumAureus"),
		("AstralChest", "AstralChest", "AstralChest", "CalamityMod/AstrumAureus"),
		("Murasama", "HellLab", "AgedSecurityChest", "CalamityMod/DevourerofGodsHead")
	};

	// Vanilla items found in the world from the start: blocks, ores, gems, plants and the commonest monster
	// materials. How to Get explains most of them through their tiles; the list keeps a generic "found in
	// the world" row for the rest.
	internal static readonly HashSet<string> VanillaWorldItems = Set("Wood BorealWood RichMahogany Ebonwood Shadewood PalmWood AshWood Bamboo StoneBlock DirtBlock ClayBlock SandBlock EbonsandBlock CrimsandBlock MudBlock SnowBlock IceBlock SiltBlock SlushBlock DesertFossil Cobweb Gel FallenStar Acorn CopperOre TinOre IronOre LeadOre SilverOre TungstenOre GoldOre PlatinumOre DemoniteOre CrimtaneOre Meteorite Hellstone Obsidian SturdyFossil Amethyst Topaz Sapphire Emerald Ruby Diamond Amber Mushroom GlowingMushroom Daybloom Moonglow Blinkroot Deathweed Waterleaf Fireblossom Shiverthorn GrassSeeds JungleGrassSeeds MushroomGrassSeeds CorruptSeeds CrimsonSeeds AshGrassSeeds VileMushroom ViciousMushroom Bone RottenChunk Vertebrae JungleSpores Stinger Vine Lens BlackLens AntlionMandible SharkFin Feather");

	// Player.GetDyeTraderReward: what the Dye Trader gives for a Strange Plant, by the gate in that method.
	internal static readonly DyeTrade[] DyeTraderRewards =
	{
		new DyeTrade(BookStage.PreHardmode, new[] { 3560, 3028, 3041, 3040, 3025, 3190, 3027, 3026, 3554, 3553, 3555, 2872, 3534, 2871 }),
		new DyeTrade(BookStage.Hardmode, new[] { 3039, 3038, 3598, 3597, 3600, 3042, 3533, 3561, 2883, 2869, 2873, 2870, 2878, 2879, 2884, 2885, 2864, 3556 }),
		new DyeTrade(BookStage.PostHardmode, new[] { 3024 })
	};

	internal static readonly int[] StrangePlants = { ItemID.StrangePlant1, ItemID.StrangePlant2, ItemID.StrangePlant3, ItemID.StrangePlant4 };

	// Items another item turns into when used (from → to), outside recipes and shimmer.
	internal static readonly (string From, string To)[] CalamityConversions = { ("XyksBlessingBlue", "XyksBlessingOrange") };

	// Machines that make an item by themselves once placed: TEPowerCellFactory.Update adds a cell every 900 ticks of
	// world time and keeps them, up to the cell's max stack, until they are taken out. Machine, product, ticks each.
	internal static readonly (string Machine, string Product, int Ticks)[] CalamityMachines = { ("PowerCellFactoryItem", "DraedonPowerCell", 900) };

	// What a hardmode boss's treasure bag adds when opened, in code rather than drop rules (Player.OpenBossBag →
	// TryGettingDevArmor): one of tModLoader's patron sets 1 in 14, else one of its developer sets 1 in 50
	// (ModLoaderMod.TryGettingPatreonOrDevArmor), and on top one of vanilla's 18 developer sets 1 in 16 (1 in 8 in a
	// tenth-anniversary world, left out). The whole picked set drops.
	internal static readonly int[][] VanillaDevSets =
	{
		new[] { 666, 667, 668, 665, 3287 }, new[] { 1554, 1555, 1556, 1586 }, new[] { 1554, 1587, 1588, 1586 }, new[] { 1557, 1558, 1559, 1585 },
		new[] { 1560, 1561, 1562, 1584 }, new[] { 1563, 1564, 1565, 3582 }, new[] { 1566, 1567, 1568 }, new[] { 1580, 1581, 1582, 1583 },
		new[] { 3226, 3227, 3228, 3288 }, new[] { 3583, 3581, 3578, 3579, 3580 }, new[] { 3585, 3586, 3587, 3588, 3024 },
		new[] { 3589, 3590, 3591, 3592, 3599 }, new[] { 3368, 3921, 3922, 3923, 3924 }, new[] { 3925, 3926, 3927, 3928, 3929 },
		new[] { 4732, 4733, 4734, 4730 }, new[] { 4747, 4748, 4749, 4746 }, new[] { 4751, 4752, 4753, 4750 }, new[] { 4755, 4756, 4757, 4754 }
	};

	// The catalogue --------------------------------------------------------------------------------------

	private static bool built;

	internal static List<NpcSource> Npcs { get; private set; } = new List<NpcSource>();

	// Global loot (biome essences and the like) applies to every NPC.
	internal static List<DropRateInfo> GlobalReports { get; private set; } = new List<DropRateInfo>();

	internal static List<ItemRoutes> Items { get; private set; } = new List<ItemRoutes>();

	internal static List<(int From, int To)> Conversions { get; private set; } = new List<(int, int)>();

	internal static List<(int Machine, int Product, int Ticks)> Machines { get; private set; } = new List<(int, int, int)>();

	// Treasure bags that roll the developer and patron sets (boss bags that are not pre-hardmode-like), and what they give.
	internal static int[] HardmodeBags { get; private set; } = Array.Empty<int>();

	internal static Dictionary<int, float> DevSetChances { get; private set; } = new Dictionary<int, float>();

	// Recorded music boxes: made by wearing a blank Music Box while the track plays.
	internal static List<int> RecordedBoxes { get; private set; } = new List<int>();

	internal static List<ShopSource> Shops { get; private set; } = new List<ShopSource>();

	// Reports that threw while read (another mod's rule): How to Get says some sources could not be read.
	internal static int ReadErrors { get; private set; }

	private static Dictionary<int, List<BookItemSource>> byItem = new Dictionary<int, List<BookItemSource>>();

	internal static void Clear()
	{
		built = false;
		Npcs = new List<NpcSource>();
		GlobalReports = new List<DropRateInfo>();
		Items = new List<ItemRoutes>();
		Conversions = new List<(int, int)>();
		Machines = new List<(int, int, int)>();
		HardmodeBags = Array.Empty<int>();
		DevSetChances = new Dictionary<int, float>();
		RecordedBoxes = new List<int>();
		Shops = new List<ShopSource>();
		byItem = new Dictionary<int, List<BookItemSource>>();
		ReadErrors = 0;
		BookWorldSources.Clear();
		BookKillConditions.Clear();
		NotCondition.Clear();
	}

	internal static void EnsureBuilt()
	{
		if (built)
		{
			return;
		}
		built = true;
		Stopwatch timer = Stopwatch.StartNew();
		try
		{
			Build();
		}
		catch (Exception exception)
		{
			ReadErrors++;
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("MRB source catalogue failed; How to Get and stages use what was read.", exception);
		}
		ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"Source catalogue audit ({timer.ElapsedMilliseconds} ms): {Npcs.Count} NPCs, {Items.Count((ItemRoutes i) => i.Reports.Count > 0)} containers, {Shops.Sum((ShopSource s) => s.Entries.Count)} shop entries, {Items.Count((ItemRoutes i) => i.Shimmer > 0)} shimmer, {RecordedBoxes.Count} music boxes; {byItem.Count} items with a source; {ReadErrors} unreadable reports.");
	}

	// Every source of one item, catalogue and world, in How to Get order.
	internal static List<BookItemSource> For(int type)
	{
		EnsureBuilt();
		List<BookItemSource> sources = new List<BookItemSource>(byItem.GetValueOrDefault(type) ?? new List<BookItemSource>());
		sources.AddRange(BookWorldSources.For(type));
		// The curated "found in the world" list only speaks when nothing else explains the item (it also holds
		// early monster materials, whose drops say more).
		if (sources.Count == 0 && ContentSamples.ItemsByType.TryGetValue(type, out Item item) && item.ModItem == null && VanillaWorldItems.Contains(ItemID.Search.GetName(type) ?? ""))
		{
			sources.Add(new BookItemSource(BookAcquisitionKind.World, type) { From = type, Note = "world" });
		}
		return sources;
	}

	internal static bool IsCalamityWorldgen(Item item)
	{
		return item.ModItem?.Mod.Name == "CalamityMod" && CalamityWorldgenItems.Contains(item.ModItem.Name);
	}

	// CalamityGatedWorldgenItems as sources: Where names the place, Note the gate ("boss:CalamityMod/AstrumAureus").
	internal static IEnumerable<BookItemSource> CalamityGatedWorldgen()
	{
		foreach (var (name, place, icon, gate) in CalamityGatedWorldgenItems)
		{
			if (ModContent.TryFind("CalamityMod", name, out ModItem item))
			{
				int chest = ModContent.TryFind("CalamityMod", icon, out ModItem iconItem) ? iconItem.Type : ItemID.Chest;
				yield return new BookItemSource(BookAcquisitionKind.Chest, item.Type) { From = chest, Owner = "CalamityMod", Where = new[] { place }, Note = "boss:" + gate };
			}
		}
	}

	private static void Build()
	{
		foreach (KeyValuePair<int, NPC> pair in ContentSamples.NpcsByNetId)
		{
			NPC npc = pair.Value;
			string name = npc.ModNPC?.Name ?? (NPCID.Search.TryGetName(npc.type, out var vanillaName) ? vanillaName : npc.type.ToString());
			List<IItemDropRule> rules = Main.ItemDropsDB.GetRulesForNPCID(pair.Key, includeGlobalDrops: false);
			// A banner follows its own NPC, not helpers sharing it (Vile Spit for the Corruptor's banner). The slimes that
			// are net ids of one type (Green, Pinky, Black...) count kills by that net id (NPC.BannerID), and so do their banners.
			int banner = Item.NPCtoBanner(npc.BannerID());
			int bannerItem = banner > 0 && Item.BannerToNPC(banner) == npc.BannerID() ? Item.BannerToItem(banner) : 0;
			int critter = npc.catchItem > 0 && ContentSamples.ItemsByType.ContainsKey(npc.catchItem) ? npc.catchItem : 0;
			Npcs.Add(new NpcSource(pair.Key, npc, name, Reports(rules), bannerItem, critter));
			if (pair.Key == 0)
			{
				continue;
			}
			string owner = npc.ModNPC?.Mod.Name ?? "Terraria";
			Display(rules, (DropRateInfo rate) => new BookItemSource(BookAcquisitionKind.Drop, rate.itemId) { Npc = pair.Key, Owner = owner, Rate = rate });
		}
		List<IItemDropRule> specific = Main.ItemDropsDB.GetRulesForNPCID(NPCID.BlueSlime, includeGlobalDrops: false);
		List<IItemDropRule> global = Main.ItemDropsDB.GetRulesForNPCID(NPCID.BlueSlime, includeGlobalDrops: true).Except(specific).ToList();
		GlobalReports = Reports(global);
		Display(global, (DropRateInfo rate) => new BookItemSource(BookAcquisitionKind.Drop, rate.itemId) { Owner = "<global>", Rate = rate });
		foreach (KeyValuePair<int, Item> pair in ContentSamples.ItemsByType)
		{
			if (pair.Value == null || pair.Value.IsAir)
			{
				continue;
			}
			List<IItemDropRule> rules = Main.ItemDropsDB.GetRulesForItemID(pair.Key);
			// Shimmer (the Aether exists from world generation; late transmutations are curated stage floors).
			int shimmered = ItemID.Sets.ShimmerTransformToItem[pair.Key];
			if (rules.Count > 0 || shimmered > 0)
			{
				Items.Add(new ItemRoutes(pair.Key, Reports(rules), shimmered));
			}
			if (rules.Count > 0)
			{
				string owner = pair.Value.ModItem?.Mod.Name ?? "Terraria";
				Display(rules, (DropRateInfo rate) => new BookItemSource(BookAcquisitionKind.Container, rate.itemId) { From = pair.Key, Owner = owner, Rate = rate });
			}
		}
		foreach (var (from, to) in CalamityConversions)
		{
			if (ModContent.TryFind("CalamityMod", from, out ModItem fromItem) && ModContent.TryFind("CalamityMod", to, out ModItem toItem))
			{
				Conversions.Add((fromItem.Type, toItem.Type));
			}
		}
		foreach (var (machine, product, ticks) in CalamityMachines)
		{
			if (ModContent.TryFind("CalamityMod", machine, out ModItem machineItem) && ModContent.TryFind("CalamityMod", product, out ModItem productItem))
			{
				Machines.Add((machineItem.Type, productItem.Type, ticks));
			}
		}
		HardmodeBags = (from i in ContentSamples.ItemsByType.Values
			where i != null && ItemID.Sets.BossBag[i.type] && !ItemID.Sets.PreHardmodeLikeBossBag[i.type]
			select i.type).ToArray();
		DevSetChances = DevSets();
		// Vanilla boxes place the MusicBoxes tile; modded ones are registered with MusicLoader.
		var modBoxes = typeof(MusicLoader).GetField("itemToMusic", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Dictionary<int, int>;
		RecordedBoxes = (from i in ContentSamples.ItemsByType.Values
			where i != null && i.ModItem == null && i.createTile == TileID.MusicBoxes && i.type != ItemID.MusicBox
			select i.type).Concat(modBoxes?.Keys ?? Enumerable.Empty<int>()).Distinct().Where(ContentSamples.ItemsByType.ContainsKey).ToList();
		foreach (AbstractNPCShop shop in NPCShopDatabase.AllShops)
		{
			List<AbstractNPCShop.Entry> entries = new List<AbstractNPCShop.Entry>();
			foreach (AbstractNPCShop.Entry entry in shop.ActiveEntries)
			{
				if (entry.Item != null && !entry.Item.IsAir)
				{
					entries.Add(entry);
				}
			}
			Shops.Add(new ShopSource(shop, entries));
		}
		Index();
		void Display(List<IItemDropRule> rules, Func<DropRateInfo, BookItemSource> make)
		{
			foreach (IItemDropRule rule in rules)
			{
				List<DropRateInfo> rates = new List<DropRateInfo>();
				try
				{
					ReportShown(rule, rates, new DropRateInfoChainFeed(1f));
				}
				catch (Exception)
				{
					ReadErrors++;
					continue;
				}
				foreach (DropRateInfo rate in rates)
				{
					if (rate.itemId > 0)
					{
						Add(make(rate));
					}
				}
			}
		}
	}

	// Every catalogue source by item, in How to Get order: drops (global first, then mobs by id), containers
	// by id, shops, then the routes outside the drop database.
	private static void Index()
	{
		Dictionary<int, List<BookItemSource>> drops = byItem;
		byItem = new Dictionary<int, List<BookItemSource>>();
		foreach (List<BookItemSource> list in drops.Values)
		{
			foreach (BookItemSource source in list.OrderBy((BookItemSource s) => s.Kind == BookAcquisitionKind.Container ? 2 : (s.Npc == 0 ? 0 : 1)).ThenBy((BookItemSource s) => s.Npc).ThenBy((BookItemSource s) => s.From))
			{
				Add(source);
			}
		}
		foreach (ShopSource shop in Shops)
		{
			foreach (AbstractNPCShop.Entry entry in shop.Entries)
			{
				Add(new BookItemSource(BookAcquisitionKind.Shop, entry.Item.type) { Npc = shop.Shop.NpcType, Shop = shop.Shop, Entry = entry });
			}
		}
		foreach (NpcSource npc in Npcs)
		{
			string owner = npc.Npc.ModNPC?.Mod.Name ?? "Terraria";
			if (npc.Banner > 0)
			{
				Add(new BookItemSource(BookAcquisitionKind.Banner, npc.Banner) { Npc = npc.NetId, Owner = owner, NoteValue = ItemID.Sets.KillsToBanner[npc.Banner] });
			}
			if (npc.Critter > 0)
			{
				Add(new BookItemSource(BookAcquisitionKind.Catch, npc.Critter) { Npc = npc.NetId, Owner = owner });
			}
		}
		foreach (ItemRoutes routes in Items.Where((ItemRoutes i) => i.Shimmer > 0 && ContentSamples.ItemsByType.ContainsKey(i.Shimmer)))
		{
			Add(new BookItemSource(BookAcquisitionKind.Shimmer, routes.Shimmer) { From = routes.Item });
		}
		foreach (var (from, to) in Conversions)
		{
			Add(new BookItemSource(BookAcquisitionKind.Use, to) { From = from, Owner = "CalamityMod" });
		}
		foreach (var (machine, product, ticks) in Machines)
		{
			Add(new BookItemSource(BookAcquisitionKind.Machine, product) { From = machine, Owner = "CalamityMod", NoteValue = ticks });
		}
		if (HardmodeBags.Length > 0)
		{
			int icon = HardmodeBags.Contains(ItemID.WallOfFleshBossBag) ? ItemID.WallOfFleshBossBag : HardmodeBags[0];
			foreach (var (item, chance) in DevSetChances)
			{
				Add(new BookItemSource(BookAcquisitionKind.DevSet, item) { From = icon, Chance = chance, Owner = ContentSamples.ItemsByType[item].ModItem?.Mod.Name ?? "Terraria" });
			}
		}
		foreach (int box in RecordedBoxes)
		{
			Add(new BookItemSource(BookAcquisitionKind.Record, box) { From = ItemID.MusicBox, Owner = ContentSamples.ItemsByType[box].ModItem?.Mod.Name ?? "Terraria" });
		}
		foreach (DyeTrade trade in DyeTraderRewards)
		{
			foreach (int dye in trade.Dyes)
			{
				Add(new BookItemSource(BookAcquisitionKind.Trade, dye) { Npc = NPCID.DyeTrader, FromAny = StrangePlants, Note = trade.Gate == BookStage.PreHardmode ? "" : trade.Gate.ToString() });
			}
		}
		foreach (Item item in ContentSamples.ItemsByType.Values.Where((Item i) => i != null && IsCalamityWorldgen(i)))
		{
			Add(new BookItemSource(BookAcquisitionKind.Chest, item.type) { From = item.type, Owner = "CalamityMod" });
		}
		foreach (BookItemSource source in CalamityGatedWorldgen())
		{
			Add(source);
		}
		foreach (BookItemSource source in BookWorldSources.CatalogueSources())
		{
			Add(source);
		}
		foreach (BookItemSource source in BookCurated.Sources())
		{
			Add(source);
		}
	}

	// Chance per opened bag of every developer and patron set item (VanillaDevSets and tModLoader's own sets, whose items
	// are named <Set>_<Piece> in the Terraria.ModLoader.Default.Patreon and .Developer namespaces).
	private static Dictionary<int, float> DevSets()
	{
		var chances = new Dictionary<int, float>();
		void Give(IEnumerable<int> items, float chance)
		{
			foreach (int item in items.Distinct().Where(ContentSamples.ItemsByType.ContainsKey))
			{
				chances[item] = chances.GetValueOrDefault(item) + chance;
			}
		}
		foreach (int[] set in VanillaDevSets)
		{
			Give(set, 1f / 16f / VanillaDevSets.Length);
		}
		var tml = (from i in ContentSamples.ItemsByType.Values
			let m = i?.ModItem
			where m != null && m.Mod.Name == "ModLoader" && (m.GetType().Namespace ?? "").StartsWith("Terraria.ModLoader.Default.", StringComparison.Ordinal)
			let patron = m.GetType().Namespace == "Terraria.ModLoader.Default.Patreon"
			where patron || m.GetType().Namespace.StartsWith("Terraria.ModLoader.Default.Developer", StringComparison.Ordinal)
			group i.type by (patron, Set: m.Name.Contains('_') ? m.Name.Substring(0, m.Name.LastIndexOf('_')) : m.Name)).ToList();
		int patrons = tml.Count((IGrouping<(bool patron, string Set), int> g) => g.Key.patron), developers = tml.Count - patrons;
		foreach (IGrouping<(bool patron, string Set), int> set in tml)
		{
			Give(set, set.Key.patron ? 1f / 14f / patrons : 13f / 14f / 50f / developers);
		}
		return chances;
	}

	private static void Add(BookItemSource source)
	{
		if (!byItem.TryGetValue(source.Item, out var list))
		{
			list = byItem[source.Item] = new List<BookItemSource>();
		}
		list.Add(source);
	}

	// A drop rule condition that holds when another does not: the failed-condition branch of a LeadingConditionRule. One
	// per condition (Of), so the stages, How to Get and the Bestiary card read the same object and the probe's answer.
	internal sealed class NotCondition : IItemDropRuleCondition
	{
		internal readonly IItemDropRuleCondition Inner;

		private static Dictionary<IItemDropRuleCondition, NotCondition> made = new Dictionary<IItemDropRuleCondition, NotCondition>(ReferenceEqualityComparer.Instance);

		private NotCondition(IItemDropRuleCondition inner)
		{
			Inner = inner;
		}

		internal static NotCondition Of(IItemDropRuleCondition inner)
		{
			if (!made.TryGetValue(inner, out NotCondition not))
			{
				not = made[inner] = new NotCondition(inner);
			}
			return not;
		}

		internal static void Clear()
		{
			made = new Dictionary<IItemDropRuleCondition, NotCondition>(ReferenceEqualityComparer.Instance);
		}

		public bool CanDrop(DropAttemptInfo info)
		{
			return !Inner.CanDrop(info);
		}

		public bool CanShowItemDropInUI()
		{
			return true;
		}

		public string GetConditionDescription()
		{
			return null;
		}
	}

	// Split reports for the stages. Sequential rules report each entry as a failed roll of the previous one,
	// so a condition on an early entry (the Present's hardmode-only Snow Globe) leaks onto everything after
	// it: entries are reported alone. A LeadingConditionRule reports its failed-condition branch under the
	// condition itself (Chains.TryIfDoesntFillConditions), though it drops when the condition fails (Calamity's
	// AddFail: the Caustic Croaker Staff before Aquatic Scourge); that branch is reported under its negation.
	private static List<DropRateInfo> Reports(IEnumerable<IItemDropRule> rules)
	{
		List<DropRateInfo> rates = new List<DropRateInfo>();
		foreach (IItemDropRule rule in rules)
		{
			try
			{
				Report(rule, rates);
			}
			catch (Exception)
			{
			}
		}
		return rates.Where((DropRateInfo r) => r.itemId > 0 && r.dropRate > 0f).ToList();
		static void Report(IItemDropRule rule, List<DropRateInfo> rates)
		{
			if (rule is LeadingConditionRule leading)
			{
				foreach (IItemDropRuleChainAttempt chain in leading.ChainedRules)
				{
					bool failed = chain is Chains.TryIfDoesntFillConditions;
					if (chain is Chains.TryIfDoesntFillConditions { hideLootReport: true } or Chains.TryIfSucceeded { hideLootReport: true } or Chains.TryIfFailedRandomRoll { hideLootReport: true })
					{
						continue;
					}
					List<DropRateInfo> branch = new List<DropRateInfo>();
					Report(chain.RuleToChain, branch);
					foreach (DropRateInfo entry in branch)
					{
						DropRateInfo gated = entry;
						gated.AddCondition(failed ? NotCondition.Of(leading.condition) : leading.condition);
						rates.Add(gated);
					}
				}
				return;
			}
			IItemDropRule[] sequence = (rule as SequentialRulesNotScalingWithLuckRule)?.rules ?? (rule as SequentialRulesRule)?.rules;
			if (sequence == null)
			{
				rule.ReportDroprates(rates, new DropRateInfoChainFeed(1f));
				return;
			}
			foreach (IItemDropRule entry in sequence)
			{
				Report(entry, rates);
			}
			Chains.ReportDroprates(rule.ChainedRules, 1f, rates, new DropRateInfoChainFeed(1f));
		}
	}

	// What How to Get and the Bestiary card list: the game's own report (chances as the drop UI computes them), except that
	// a LeadingConditionRule's failed-condition branch carries the condition's negation, as in Reports (Nova's Starblight
	// Soot drops when it does not blow itself up).
	internal static void ReportShown(IItemDropRule rule, List<DropRateInfo> rates, DropRateInfoChainFeed feed)
	{
		if (rule is not LeadingConditionRule leading)
		{
			rule.ReportDroprates(rates, feed);
			return;
		}
		// As LeadingConditionRule.ReportDroprates: every branch with the rule's chance of 1.
		foreach (IItemDropRuleChainAttempt chain in leading.ChainedRules)
		{
			DropRateInfoChainFeed branch = feed.With(1f);
			if (chain is Chains.TryIfDoesntFillConditions failed)
			{
				if (!failed.hideLootReport)
				{
					branch.AddCondition(NotCondition.Of(leading.condition));
					ReportShown(failed.RuleToChain, rates, branch);
				}
				continue;
			}
			branch.AddCondition(leading.condition);
			if (chain is Chains.TryIfSucceeded { hideLootReport: false } succeeded)
			{
				ReportShown(succeeded.RuleToChain, rates, branch);
			}
			else
			{
				chain.ReportDroprates(1f, rates, branch);
			}
		}
	}

	private static HashSet<string> Set(string names)
	{
		return new HashSet<string>(names.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
	}
}
