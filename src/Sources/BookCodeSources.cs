using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook.Sources;

// Items the game's own code hands out, outside drop rules, shops and recipes (a BookCurated area, texts in
// ItemsSources): the Old One's Army's wave rewards, the potions every boss leaves when it dies, Calamity's Greed Pot,
// extractinator, tree and pot finds and its Zenith-world loot, and shimmer decrafting. Also what the item card needs
// about containers: the key a lock box opens with. Every table cites the code it was read from (tModLoader 2025.7,
// Calamity 2.2.4).
internal static class BookCodeSources
{
	private const string Calamity = "CalamityMod";

	private const string Astral = "CalamityMod/AstralInfectionBiome", Sulphur = "CalamityMod/SulphurousSeaBiome";

	// Old One's Army ------------------------------------------------------------------------------------------

	// DD2Event.CheckProgress and WinInvasionInternal: the Eternia Crystal gives every player Defender Medals
	// (DropMedals, an instanced drop) when the wave it names is cleared and when the event is won. The event runs at the
	// highest tier the world allows (FindProperDifficulty: ReadyForTier2 hardmode and any mechanical boss, ReadyForTier3
	// hardmode and Golem); the Tavernkeep who sells the crystal comes after the Eater of Worlds or the Brain (Tier 1).
	// Tier, its gate, medals per cleared wave, medals for the win.
	private static readonly (int Tier, string Gate, (int Wave, int Medals)[] Waves, int Win)[] MedalTiers =
	{
		(1, "", new[] { (3, 1), (4, 1) }, 3),
		(2, "mechs:any", new[] { (4, 1), (5, 3), (6, 6) }, 15),
		(3, "Golem", new[] { (3, 1), (4, 3), (5, 11), (6, 25) }, 60)
	};

	// Boss potions ------------------------------------------------------------------------------------------

	// NPC.DoDeathEvents_DropBossPotionsAndHearts: every NPC with NPC.boss set leaves 5-15 of one potion when it dies,
	// besides its loot (and hearts): Lesser Healing Potions, unless its type is listed here; then NPCLoader.BossLoot lets
	// a ModNPC change it. The Eater of Worlds sets boss on its last segment (NPC.checkDead); Moon Lord's head and hands
	// never die on their own (checkDead returns early) and the Torch God is no fight.
	private static readonly Dictionary<int, int> VanillaBossPotions = new Dictionary<int, int>
	{
		[NPCID.WallofFlesh] = ItemID.HealingPotion, [NPCID.SkeletronHead] = ItemID.HealingPotion, [NPCID.Deerclops] = ItemID.HealingPotion,
		[NPCID.QueenBee] = ItemID.BottledHoney, [NPCID.QueenSlimeBoss] = ItemID.GreaterHealingPotion, [NPCID.HallowBoss] = ItemID.GreaterHealingPotion,
		[NPCID.Retinazer] = ItemID.GreaterHealingPotion, [NPCID.Spazmatism] = ItemID.GreaterHealingPotion, [NPCID.SkeletronPrime] = ItemID.GreaterHealingPotion, [NPCID.TheDestroyer] = ItemID.GreaterHealingPotion,
		[NPCID.Golem] = ItemID.GreaterHealingPotion, [NPCID.Plantera] = ItemID.GreaterHealingPotion, [NPCID.DukeFishron] = ItemID.GreaterHealingPotion,
		[NPCID.MartianSaucerCore] = ItemID.GreaterHealingPotion, [NPCID.CultistBoss] = ItemID.GreaterHealingPotion, [NPCID.MoonLordCore] = ItemID.SuperHealingPotion
	};

	private static readonly HashSet<int> NeverDie = new HashSet<int> { NPCID.MoonLordHead, NPCID.MoonLordHand, NPCID.TorchGod };

	// Calamity's worms put their potion in BossLoot's place (Sand, Sulphurous Sand, Starblight Soot, Cosmilite Bricks:
	// DesertScourgeHead, AquaticScourgeHead, AstrumDeusHead, DevourerofGodsHead.BossLoot) and drop the potion through a
	// rule left out of the loot report: DefineConditionalDropSet(() => true).Add(DropHelper.PerPlayer(potion, 1, 5, 15),
	// hideLootReport: true) in their ModifyNPCLoot. Boss, potion.
	private static readonly (string Npc, string Potion)[] WormPotions = { ("DesertScourgeHead", "LesserHealingPotion"), ("AquaticScourgeHead", "GreaterHealingPotion"), ("AstrumDeusHead", "SuperHealingPotion"), ("DevourerofGodsHead", "CalamityMod/OmegaHealingPotion") };

	// Calamity's Greed Pot ------------------------------------------------------------------------------------

	// GreedTransmutation.GreedChain: an ore used on a placed Greed Pot (GreedTransmutationGlobalItem.UseItem, once the
	// link is open) flies in and comes out as the next ore 1 time in 3 (OnSpawn: Main.rand.NextBool(3)), else as Stone
	// (OnKill). The Shady Salesman sells the pot with no condition. Input, result, term (BookSteps.Resolve), and the term
	// without Calamity's Early Hardmode Progression Rework.
	private static readonly (string From, string To, string Term, string Unreworked)[] GreedChain =
	{
		("CopperOre", "IronOre", "", null), ("IronOre", "SilverOre", "", null), ("SilverOre", "GoldOre", "", null),
		("TinOre", "LeadOre", "", null), ("LeadOre", "TungstenOre", "", null), ("TungstenOre", "PlatinumOre", "", null),
		("GoldOre", "CalamityMod/AerialiteOre", "CalamityMod/HiveMind;CalamityMod/PerforatorHive", null), ("PlatinumOre", "CalamityMod/AerialiteOre", "CalamityMod/HiveMind;CalamityMod/PerforatorHive", null),
		("CalamityMod/AerialiteOre", "PalladiumOre", "Hardmode", null),
		("CobaltOre", "MythrilOre", "mechs:any", "Hardmode"), ("MythrilOre", "AdamantiteOre", "mechs:two", "Hardmode"),
		("PalladiumOre", "OrichalcumOre", "mechs:any", "Hardmode"), ("OrichalcumOre", "TitaniumOre", "mechs:two", "Hardmode"),
		("AdamantiteOre", "CalamityMod/HallowedOre", "mechs:all", "mechs:any"), ("TitaniumOre", "CalamityMod/HallowedOre", "mechs:all", "mechs:any"),
		("CalamityMod/HallowedOre", "ChlorophyteOre", "mechs:all", null), ("ChlorophyteOre", "CalamityMod/PerennialOre", "Plantera", null),
		("CalamityMod/PerennialOre", "CalamityMod/ScoriaOre", "CultistBoss", null), ("CalamityMod/ScoriaOre", "LunarOre", "PostHardmode", null),
		("LunarOre", "CalamityMod/ExodiumCluster", "PostHardmode", null), ("CalamityMod/ExodiumCluster", "CalamityMod/UelibloomOre", "CalamityMod/Providence", null),
		("CalamityMod/UelibloomOre", "CalamityMod/AuricOre", "CalamityMod/SupremeCalamitas;CalamityMod/Draedon", null)
	};

	// Calamity's extractinator blocks ------------------------------------------------------------------------

	// NovaeSlag and CelestialRemains.ExtractinatorUse, for the finds of their own (the coins, gems, Fallen Stars and
	// hardmode ores they also give come from silt too): one roll of Next(100) down a list. By day the Fallen Star's
	// tenth falls through to the Starblight Soot after it; the Astral Ore takes whatever is left once Astrum Deus is
	// down (it is hardmode, so the hardmode ores before it always take theirs). Block, find, chance by day, at night,
	// stack, term.
	private static readonly (string From, string Item, float Day, float Night, int Min, int Max, string Term)[] ExtractinatorFinds =
	{
		("NovaeSlag", "StarblightSoot", 0.2f, 0.1f, 1, 20, ""), ("NovaeSlag", "AstralOre", 0.1147f, 0.1147f, 1, 1, "CalamityMod/AstrumDeusHead"),
		("CelestialRemains", "AncientBoneDust", 0.2f, 0.2f, 1, 10, ""), ("CelestialRemains", "StarblightSoot", 0.2f, 0.1f, 1, 10, ""),
		("CelestialRemains", "AstralOre", 0.1047f, 0.1047f, 1, 1, "CalamityMod/AstrumDeusHead")
	};

	// Lock boxes ------------------------------------------------------------------------------------------------

	// ItemSlot.TryOpenContainer: the Golden Lock Box opens only by using up a Golden Key, the Obsidian Lock Box only with
	// a Shadow Key in the inventory or an open Void Bag, which it keeps. Box, key, used up.
	private static readonly (int Box, int Key, bool UsedUp)[] LockBoxKeys = { (ItemID.LockBox, ItemID.GoldenKey, true), (ItemID.ObsidianLockbox, ItemID.ShadowKey, false) };

	// The key a container opens with and whether opening uses it up; null when it needs none.
	internal static (int Key, bool UsedUp)? KeyOf(int container)
	{
		foreach (var (box, key, usedUp) in LockBoxKeys)
		{
			if (box == container)
			{
				return (key, usedUp);
			}
		}
		return null;
	}

	// Shimmer decrafting -------------------------------------------------------------------------------------

	// Item.GetShimmered's last case: an item the shimmer has nothing else for (no coin luck, no Lunar Brick or music box,
	// no ItemID.Sets.ShimmerTransformToItem, no Gel Balloon or critter) turns back into its recipe's materials
	// (Recipe.customShimmerResults when set), once per the recipe's output stack. The recipe is
	// ShimmerTransforms.GetDecraftingRecipeIndex's for ItemID.Sets.ShimmerCountsAsItem: the first of
	// ItemID.Sets.CraftingRecipeIndices (no DisableDecraft, not disabled) whose decraft conditions hold (Recipe.AddRecipe
	// gives the evil items theirs: a crimson or corrupt world). With a Bone among the materials it waits for Skeletron,
	// with a Lihzahrd Brick for Golem (IsItemTransformLocked); an alchemy recipe loses each material 1 time in 3
	// (IngredientQuantityRules.Alchemy, isDecrafting). Material, every (item, recipe) giving it. No progression route:
	// decrafting gives back what made the item, so it opens nothing earlier; a route would only carry the stages' guesses
	// for an item (a sink or bookcase placed by its rarity) back into its materials.
	private static Dictionary<int, List<(int From, Recipe Recipe, int Amount)>> decrafts;

	private static Dictionary<int, List<(int From, Recipe Recipe, int Amount)>> Decrafts()
	{
		if (decrafts != null)
		{
			return decrafts;
		}
		decrafts = new Dictionary<int, List<(int, Recipe, int)>>();
		foreach (Item item in ContentSamples.ItemsByType.Values)
		{
			if (item == null || item.IsAir || !Decraftable(item))
			{
				continue;
			}
			int same = ItemID.Sets.ShimmerCountsAsItem[item.type] != -1 ? ItemID.Sets.ShimmerCountsAsItem[item.type] : item.type;
			foreach (int index in ItemID.Sets.CraftingRecipeIndices[same])
			{
				Recipe recipe = Main.recipe[index];
				foreach (Item material in recipe.customShimmerResults ?? recipe.requiredItem)
				{
					if (material.type > 0 && material.type != item.type && ContentSamples.ItemsByType.ContainsKey(material.type))
					{
						(decrafts.TryGetValue(material.type, out var list) ? list : decrafts[material.type] = new List<(int, Recipe, int)>()).Add((item.type, recipe, material.stack));
					}
				}
				// The game takes the first recipe whose conditions hold: one without any ends the list.
				if (recipe.DecraftConditions.Count == 0)
				{
					break;
				}
			}
		}
		return decrafts;
	}

	private static bool Decraftable(Item item)
	{
		int same = ItemID.Sets.ShimmerCountsAsItem[item.type] != -1 ? ItemID.Sets.ShimmerCountsAsItem[item.type] : item.type;
		return ItemID.Sets.CraftingRecipeIndices[same].Length > 0 && ItemID.Sets.CoinLuckValue[same] <= 0 && item.type != ItemID.LunarBrick && item.createTile != TileID.MusicBoxes && ItemID.Sets.ShimmerTransformToItem[same] <= 0 && item.type != ItemID.GelBalloon && item.makeNPC <= 0;
	}

	// The step a decraft opens at: the Bone and Lihzahrd Brick locks and the recipe's decraft conditions; -1 when this
	// world can never take it (the other evil's, outside a drunk world, which turns the evil over every dawn).
	private static int DecraftStep(Recipe recipe)
	{
		int step = 0;
		if (recipe.ContainsIngredient(ItemID.Bone))
		{
			step = Math.Max(step, BookSteps.Resolve("SkeletronHead"));
		}
		if (recipe.ContainsIngredient(ItemID.LihzahrdBrick))
		{
			step = Math.Max(step, BookSteps.Resolve("Golem"));
		}
		foreach (Condition condition in recipe.DecraftConditions)
		{
			if (condition.Description.Key is "Conditions.WorldCrimson" or "Conditions.WorldCorrupt")
			{
				if (!Main.drunkWorld && !condition.IsMet())
				{
					return -1;
				}
				continue;
			}
			step = Math.Max(step, BookProgressionSources.ConditionStep(condition) ?? 0);
		}
		return step;
	}

	// The catalogue -------------------------------------------------------------------------------------------

	internal static IEnumerable<BookItemSource> Sources()
	{
		foreach (var (tier, _, waves, win) in MedalTiers)
		{
			int total = waves.Sum(((int Wave, int Medals) w) => w.Medals) + win;
			yield return new BookItemSource(BookAcquisitionKind.Event, ItemID.DefenderMedal) { From = ItemID.DD2ElderCrystal, Note = "medals", NoteValue = tier, Min = total, Max = total };
		}
		foreach (var (npc, potion) in BossPotions())
		{
			yield return new BookItemSource(BookAcquisitionKind.Event, potion) { Npc = npc, Owner = ContentSamples.NpcsByNetId[npc].ModNPC?.Mod.Name ?? "Terraria", Note = "bosspotion", Min = 5, Max = 15 };
		}
		foreach (BookItemSource source in CalamitySources())
		{
			yield return source;
		}
		// One row per material: every item that decrafts into it, the first one named on the row.
		foreach (var (material, list) in Decrafts())
		{
			int[] from = list.Select(((int From, Recipe Recipe, int Amount) d) => d.From).Distinct().ToArray();
			yield return new BookItemSource(BookAcquisitionKind.Event, material) { From = from[0], FromAny = from, Owner = ContentSamples.ItemsByType[from[0]].ModItem?.Mod.Name ?? "Terraria", Note = "decraft" };
		}
	}

	private static IEnumerable<BookItemSource> CalamitySources()
	{
		if (!ModLoader.HasMod(Calamity))
		{
			yield break;
		}
		int pot = ItemType("GreedPot");
		foreach (var (from, to, _, _) in GreedChain)
		{
			if (pot > 0 && AnyItemType(from) is int input and > 0 && AnyItemType(to) is int output and > 0)
			{
				yield return new BookItemSource(BookAcquisitionKind.Event, output) { From = input, FromAny = new[] { pot }, Chance = 1f / 3f, Owner = Calamity, Note = "greed" };
			}
		}
		foreach (var (from, item, day, _, min, max, _) in ExtractinatorFinds)
		{
			if (ItemType(from) is int block and > 0 && ItemType(item) is int type and > 0)
			{
				yield return new BookItemSource(BookAcquisitionKind.Event, type) { From = block, FromAny = new int[] { ItemID.Extractinator, ItemID.ChlorophyteExtractinator }, Chance = day, Min = min, Max = max, Owner = Calamity, Note = "extract" };
			}
		}
		// AstralTree, AstralPalmTree and AstralSnowTree.Shake: a roll of 1 in 15 late in the chain gives 1-2 Starblight
		// Soot, at night half the time a Fallen Star instead. The trees grow in the Astral Infection (hardmode).
		if (ItemType("StarblightSoot") is int soot and > 0)
		{
			foreach (int tree in new[] { TileID.Trees, TileID.PalmTree })
			{
				yield return new BookItemSource(BookAcquisitionKind.Tree, soot) { Tile = tree, FrameX = 22, FrameY = 198, Min = 1, Max = 2, Owner = Calamity, Where = new[] { Astral }, Note = "shake" };
			}
		}
		// SulphurousPots.GetItemDrops: Abyss.cs sets them on Sulphurous Shale above the world surface, the Sulphurous Sea's
		// caves; each breaks into Sulphuric Treasure (a bomb at the worst luck, a Fallen Star 1 in 5 in a remix world, a
		// bomb 1 in 4 in a For the Worthy one).
		if (ItemType("SulphuricTreasure") is int treasure and > 0 && ModContent.TryFind(Calamity, "SulphurousPots", out ModTile pots))
		{
			yield return new BookItemSource(BookAcquisitionKind.World, treasure) { Tile = pots.Type, Owner = Calamity, Where = new[] { Sulphur } };
		}
		// Zenith ("get fixed boi") worlds: Supreme Calamitas drops the Coldheart Icicle when she comes as Permafrost, summoned
		// with Delicious Meat held at her altar (SCalAltar.AttemptToSummonSCal sets SCalRitualDrama's ai[1], which sets
		// SupremeCalamitas.permafrost; SupremeCalamitas.ModifyNPCLoot hides that rule, ui: false).
		if (ItemType("ColdheartIcicle") is int icicle and > 0 && NpcType("SupremeCalamitas") is int scal and > 0 && ItemType("DeliciousMeat") is int meat and > 0)
		{
			IItemDropRuleCondition gfb = Condition.ZenithWorld.ToDropCondition(ShowItemDropInUI.WhenConditionSatisfied);
			IItemDropRuleCondition summoned = new SimpleItemDropRuleCondition(Text("ItemsSources.SummonedWith").WithFormatArgs(Lang.GetItemName(meat)), () => Main.zenithWorld, ShowItemDropInUI.WhenConditionSatisfied);
			yield return new BookItemSource(BookAcquisitionKind.Drop, icicle) { Npc = scal, Owner = Calamity, Rate = new DropRateInfo(icicle, 1, 1, 1f, new List<IItemDropRuleCondition> { gfb, summoned }) };
		}
		// UndergroundShrines.FillCrimsonShrineChest: only a Zenith world's Crimson shrine (placed in a crimson or drunk world,
		// WorldgenManagementSystem "Special Shrines") holds Bloody Mary, two.
		if (ItemType("BloodyMary") is int mary and > 0)
		{
			yield return new BookItemSource(BookAcquisitionKind.World, mary) { From = ItemID.Chest, Min = 2, Max = 2, Owner = Calamity, Note = "crimsonshrine" };
		}
	}

	// Every boss NPC and the potion it leaves: vanilla's by type (VanillaBossPotions), Calamity's through its BossLoot,
	// which only sets the type (asked on the sample). Other mods' BossLoot can set world flags, so their bosses are left
	// out. ponytail: other mods' bosses get no row; add them when their BossLoot is known to be pure.
	private static IEnumerable<(int Npc, int Potion)> BossPotions()
	{
		foreach (var (netId, npc) in ContentSamples.NpcsByNetId)
		{
			if (netId <= 0 || (!npc.boss && netId != NPCID.EaterofWorldsHead) || NeverDie.Contains(netId))
			{
				continue;
			}
			int potion = VanillaBossPotions.GetValueOrDefault(netId, ItemID.LesserHealingPotion);
			if (npc.ModNPC != null)
			{
				if (npc.ModNPC.Mod.Name != Calamity || npc.ModNPC.GetType().GetMethod(nameof(ModNPC.BossLoot), new[] { typeof(int).MakeByRefType() })?.DeclaringType == typeof(ModNPC))
				{
					continue;
				}
				try
				{
					npc.ModNPC.BossLoot(ref potion);
				}
				catch (Exception)
				{
					continue;
				}
			}
			if (potion > 0 && ContentSamples.ItemsByType.ContainsKey(potion))
			{
				yield return (netId, potion);
			}
		}
		foreach (var (name, potion) in ModLoader.HasMod(Calamity) ? WormPotions : Array.Empty<(string, string)>())
		{
			if (NpcType(name) is int worm and > 0 && AnyItemType(potion) is int type and > 0)
			{
				yield return (worm, type);
			}
		}
	}

	// The progression ------------------------------------------------------------------------------------------

	internal static IEnumerable<(int Item, int Step, string Why)> Steps()
	{
		foreach (var (tier, gate, _, _) in MedalTiers)
		{
			yield return (ItemID.DefenderMedal, Math.Max(BookProgressionSources.TownNpcStep(NPCID.DD2Bartender), BookSteps.Resolve(gate)), "medals:tier" + tier);
		}
		// The Primordial Wyrm is no step: its loot waits for Supreme Calamitas, where players meet it (CalamityConditions'
		// DownedAEW), and so do its potions.
		int wyrm = NpcType("PrimordialWyrmHead");
		foreach (var (npc, potion) in BossPotions())
		{
			int step = npc == wyrm ? Math.Max(BookProgression.NpcStep(npc), BookSteps.Resolve(Calamity + "/SupremeCalamitas")) : BookProgression.NpcStep(npc);
			yield return (potion, step, "bosspotion:" + (ContentSamples.NpcsByNetId[npc].ModNPC?.Name ?? NPCID.Search.GetName(npc)));
		}
		if (!ModLoader.HasMod(Calamity))
		{
			yield break;
		}
		if (ItemType("StarblightSoot") is int soot and > 0)
		{
			yield return (soot, BookSteps.HardmodeStep, "shake:AstralTree");
		}
		if (ItemType("SulphuricTreasure") is int treasure and > 0)
		{
			yield return (treasure, 0, "pot:SulphurousPots");
		}
		// The Zenith world's loot exists only there; elsewhere How to Get lists it as another world's.
		if (Main.zenithWorld && ItemType("ColdheartIcicle") is int icicle and > 0)
		{
			yield return (icicle, BookSteps.Resolve(Calamity + "/SupremeCalamitas"), "zenith:SupremeCalamitas");
		}
		if (Main.zenithWorld && ItemType("BloodyMary") is int mary and > 0)
		{
			yield return (mary, 0, "zenith:CrimsonShrine");
		}
	}

	internal static IEnumerable<(int Item, int[][] Inputs, int Floor, string Why)> Routes()
	{
		if (ModLoader.HasMod(Calamity))
		{
			bool rework = BookProgressionSources.CalamityRework();
			int pot = ItemType("GreedPot");
			foreach (var (from, to, term, unreworked) in GreedChain)
			{
				int floor = BookSteps.Resolve(rework || unreworked == null ? term : unreworked);
				if (pot > 0 && floor >= 0 && AnyItemType(from) is int input and > 0 && AnyItemType(to) is int output and > 0)
				{
					yield return (output, new[] { new[] { input }, new[] { pot } }, floor, "greed:" + from);
				}
			}
			foreach (var (from, item, _, _, _, _, term) in ExtractinatorFinds)
			{
				int floor = BookSteps.Resolve(term);
				if (floor >= 0 && ItemType(from) is int block and > 0 && ItemType(item) is int type and > 0)
				{
					yield return (type, new[] { new[] { block }, new int[] { ItemID.Extractinator, ItemID.ChlorophyteExtractinator } }, floor, "extractinator:" + from);
				}
			}
		}
	}


	// How to Get ----------------------------------------------------------------------------------------------

	internal static BookAcquisitionEntry Entry(BookItemSource s)
	{
		switch (s.Note)
		{
		case "medals" when s.Kind == BookAcquisitionKind.Event:
		{
			var (tier, gate, waves, win) = MedalTiers.First(((int Tier, string Gate, (int Wave, int Medals)[] Waves, int Win) t) => t.Tier == s.NoteValue);
			string list = string.Join(", ", waves.Select(((int Wave, int Medals) w) => L("ItemsSources.WaveMedals", w.Wave, w.Medals)).Append(L("ItemsSources.VictoryMedals", win)));
			string label = tier switch
			{
				1 => L("ItemsSources.BeforeAnyMechanicalBoss"),
				2 => L("Common.AfterAnyMechanicalBoss"),
				_ => L("Common.After", Lang.GetNPCNameValue(NPCID.Golem))
			};
			string name = L("ItemsSources.EventTier", Language.GetTextValue("Bestiary_Invasions.OldOnesArmy"), tier);
			return Row(name, L("ItemsSources.DefenderMedalsBody", Lang.GetNPCNameValue(NPCID.DD2EterniaCrystal), list, s.Min), BookAcquisitionKind.Event, label, item: s.From);
		}
		case "bosspotion":
		{
			string boss = ContentSamples.NpcsByNetId.TryGetValue(s.Npc, out NPC npc) ? npc.FullName : Lang.GetNPCNameValue(s.Npc);
			return Row(boss, L("ItemsSources.BossPotionBody"), BookAcquisitionKind.Drop, L("ItemsSources.WhenItDies"), BookAcquisitionRules.Chance(1f), npc: s.Npc);
		}
		case "greed":
		{
			int pot = s.FromAny[0];
			string term = GreedChain.Where(((string From, string To, string Term, string Unreworked) g) => AnyItemType(g.From) == s.From && AnyItemType(g.To) == s.Item).Select(g => BookProgressionSources.CalamityRework() || g.Unreworked == null ? g.Term : g.Unreworked).FirstOrDefault() ?? "";
			string label = L("ItemsSources.InAGreedPot", Name(pot)) + (term.Length > 0 ? " · " + RecipeBookSystem.GateTermText(term) : "");
			return Row(Name(s.From), L("ItemsSources.GreedPotBody", Name(pot), Lang.GetNPCNameValue(NpcType("ShadySalesman")), Name(ItemID.StoneBlock)), BookAcquisitionKind.Use, label, BookAcquisitionRules.Chance(s.Chance), item: s.From);
		}
		case "extract":
		{
			var (_, _, _, night, _, _, term) = ExtractinatorFinds.First(((string From, string Item, float Day, float Night, int Min, int Max, string Term) f) => ItemType(f.From) == s.From && ItemType(f.Item) == s.Item);
			List<string> label = new List<string> { L("Rewards.InAnExtractinator") };
			if (term.Length > 0)
			{
				label.Add(RecipeBookSystem.GateTermText(term));
			}
			if (night != s.Chance)
			{
				label.Add(L("ItemsSources.AtNight", BookAcquisitionRules.Chance(night)));
			}
			string body = L("Rewards.ExtractinatorBody") + (night != s.Chance ? "\n" + L("ItemsSources.FallenStarAtNight", Name(ItemID.FallenStar)) : "");
			return Row(Name(s.From), body, BookAcquisitionKind.Use, string.Join(" · ", label), BookAcquisitionRules.Chance(s.Chance), item: s.From);
		}
		case "crimsonshrine":
			return Row(L("ItemsSources.CrimsonShrine"), L("ItemsSources.CrimsonShrineBody"), BookAcquisitionKind.Chest, L("KillConditions.WithSeed", "get fixed boi"), item: s.From, otherMode: !Main.zenithWorld);
		case "decraft":
			return DecraftEntry(s);
		}
		return null;
		BookAcquisitionEntry Row(string name, string body, BookAcquisitionKind kind, string label, string chance = "", int item = 0, int npc = 0, bool otherMode = false)
		{
			return new BookAcquisitionEntry(name, body, kind, chance, "×" + BookAcquisitionRules.Stack(s.Min, s.Max), item, npc, label.Length > 0, -1, s.Owner, label, "", -1, 0, 0, otherMode, SourceItem: item);
		}
	}

	// The material's decraft row: the item that gives it (or how many do), what each gives and since when.
	private static BookAcquisitionEntry DecraftEntry(BookItemSource s)
	{
		List<(int From, Recipe Recipe, int Amount)> all = Decrafts()[s.Item];
		List<(int From, Recipe Recipe, int Amount, int Step)> here = all.Select(((int From, Recipe Recipe, int Amount) d) => (d.From, d.Recipe, d.Amount, Step: DecraftStep(d.Recipe))).Where(d => d.Step >= 0).ToList();
		bool otherWorld = here.Count == 0;
		var shown = otherWorld ? all.Select(d => (d.From, d.Recipe, d.Amount, Step: 0)).ToList() : here;
		string Piece((int From, Recipe Recipe, int Amount, int Step) d)
		{
			string piece = d.Recipe.createItem.stack > 1 ? L("ItemsSources.DecraftPer", Name(d.From), d.Amount, d.Recipe.createItem.stack) : L("ItemsSources.DecraftOne", Name(d.From), d.Amount);
			return d.Step > 0 ? piece + " (" + L("Common.After", BookSteps.Name(d.Step)) + ")" : piece;
		}
		const int Listed = 12;
		string[] names = shown.DistinctBy(d => d.From).Select(Piece).ToArray();
		string list = string.Join("\n", names.Take(Listed)) + (names.Length > Listed ? "\n" + L("ItemsSources.AndMore", names.Length - Listed) : "");
		bool alchemy = shown.Any(d => d.Recipe.requiredTile.Contains(TileID.Bottles));
		string body = L("ItemsSources.DecraftBody") + "\n" + list + (alchemy ? "\n" + L("ItemsSources.DecraftAlchemy") : "");
		int count = shown.Select(d => d.From).Distinct().Count();
		int min = shown.Min(d => d.Amount), max = shown.Max(d => d.Amount);
		string name = count == 1 ? Name(shown[0].From) : L("ItemsSources.AnyOfItems", count);
		string label = count == 1 ? L("ItemsSources.ShimmerIt") : L("ItemsSources.ShimmerAny");
		return new BookAcquisitionEntry(name, body, BookAcquisitionKind.Shimmer, "", "×" + BookAcquisitionRules.Stack(min, max), shown[0].From, 0, true, -1, s.Owner, label, "", -1, 0, 0, otherWorld, SourceItem: shown[0].From);
	}

	private static int ItemType(string name)
	{
		return ModContent.TryFind(Calamity, name, out ModItem item) ? item.Type : 0;
	}

	private static int NpcType(string name)
	{
		return ModContent.TryFind(Calamity, name, out ModNPC npc) ? npc.Type : 0;
	}

	// "CalamityMod/HallowedOre" or a vanilla internal name.
	private static int AnyItemType(string name)
	{
		if (name.Contains('/'))
		{
			return ModContent.TryFind(name, out ModItem item) ? item.Type : 0;
		}
		return ItemID.Search.TryGetId(name, out int id) ? id : 0;
	}

	private static string Name(int type)
	{
		return type > 0 && ContentSamples.ItemsByType.TryGetValue(type, out Item item) ? item.Name : "";
	}
}
