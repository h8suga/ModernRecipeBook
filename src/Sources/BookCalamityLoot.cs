using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;

namespace ModernRecipeBook.Sources;

// Calamity 2.2.4 items its code hands out where no drop rule, shop entry or recipe names them (the BookCurated area
// "CalamityLoot"): mechanics, critters, shaken trees, world objects. Each row cites the code it is read from. Items
// nothing in Calamity's code gives are hidden from the catalogue instead (Unobtainable).
internal static class BookCalamityLoot
{
	private const string Calamity = "CalamityMod";

	private const string Crag = "CalamityMod/BrimstoneCragsBiome", Astral = "CalamityMod/AstralInfectionBiome", Sulphur = "CalamityMod/SulphurousSeaBiome";

	// Abyss.cs places Abyssal Pots on the gravel, pyre mantle and voidstone below the Sulphurous Sea: layers 2 to 4.
	private static readonly string[] DeepAbyss = { "CalamityMod/AbyssLayer2Biome", "CalamityMod/AbyssLayer3Biome", "CalamityMod/AbyssLayer4Biome" };

	// The last roll of each tree's Shake: AstralTree and AstralPalmTree drop Barberry or Lotus, AstralSnowTree Cometfruit
	// or Mangosteen (all three grow in the Astral Infection, which comes with hardmode), the Sulphurous Sea's palm
	// AcidWoodTree Jackfruit or Salak. Fruit, where, the tree's tile.
	private static readonly (string Item, string Where, int Tile)[] ShakenFruit =
	{
		("Barberry", Astral, TileID.Trees), ("Lotus", Astral, TileID.Trees), ("Cometfruit", Astral, TileID.Trees), ("Mangosteen", Astral, TileID.Trees),
		("Jackfruit", Sulphur, TileID.PalmTree), ("Salak", Sulphur, TileID.PalmTree)
	};

	// Critters that sometimes spawn carrying another catch (NPC.catchItem set on spawn): BabyFlakCrab.AI 1 in 20 a Geyser
	// Shell, Radiator.OnSpawn 1 in 10 a Radiating Crystal (that Radiator is also harmless). Item, critter, one in.
	private static readonly (string Item, string Npc, int OneIn)[] RareCatches = { ("GeyserShell", "BabyFlakCrab", 20), ("RadiatingCrystal", "Radiator", 10) };

	// Nothing in Calamity 2.2.4 gives these: no recipe, drop, shop, chest, catch, shimmer or schematic tile names them (the
	// five Sunken Sea fish are only Cooked Fish ingredients, the Astral Grass Wall's placed wall only ever turns into its
	// unsafe twin, AstralConversion), and the last two are developer tools ("Items.Debug", no research).
	private static readonly HashSet<string> Unobtainable = new HashSet<string>(StringComparer.Ordinal)
	{
		"CoralskinFoolfish", "GleamingCucumber", "MoltenFishron", "SpecularSturgeon", "Squidoom", "WillOWisp", "BottledPanacea", "RunestoneTerracotta",
		"ShellTrimBrick", "AstralGrassWall", "DialogueDisplayDebugItem", "SulphurousSeaWorldSideChanger"
	};

	internal static IEnumerable<BookItemSource> Sources()
	{
		return Rows().Select(r => r.Source);
	}

	internal static IEnumerable<(int Item, int Step, string Why)> Steps()
	{
		return from r in Rows()
			where r.Inputs == null
			let step = r.Step()
			where step >= 0
			select (r.Source.Item, step, "calamity:" + r.Why);
	}

	internal static IEnumerable<(int Item, int[][] Inputs, int Floor, string Why)> Routes()
	{
		return from r in Rows()
			where r.Inputs != null
			select (r.Source.Item, r.Inputs, Math.Max(0, r.Step()), "calamity:" + r.Why);
	}

	internal static bool Hidden(Item item)
	{
		return item.ModItem?.Mod.Name == Calamity && Unobtainable.Contains(item.ModItem.Name);
	}

	internal static string KindName(BookAcquisitionKind kind)
	{
		return kind switch
		{
			BookAcquisitionKind.Enchant => L("CalamityLoot.Enchantment"),
			BookAcquisitionKind.Gift => L("CalamityLoot.Gift"),
			BookAcquisitionKind.SetBonus => L("CalamityLoot.SetBonus"),
			_ => null
		};
	}

	// The area's own rows (Kind CalamityLoot, Note the mechanic), shown as the kind of source they are.
	internal static BookAcquisitionEntry Entry(BookItemSource s)
	{
		if (s.Kind != BookAcquisitionKind.CalamityLoot)
		{
			return null;
		}
		switch (s.Note)
		{
		case "enchant":
		{
			string curse = Language.GetTextValue("Mods.CalamityMod.UI.Exhumed.DisplayName"), boss = BossName("SupremeCalamitas");
			return new BookAcquisitionEntry(ItemName(s.From), L("CalamityLoot.EnchantBody", Lang.GetNPCNameValue(s.Npc), ItemName(s.From), curse, boss, ItemName(ItemID.DiscountCard)), BookAcquisitionKind.Enchant, "", "×1", s.From, 0, true, -1, s.Owner, L("CalamityLoot.EnchantLabel", curse, boss));
		}
		case "catch":
			return new BookAcquisitionEntry(Lang.GetNPCNameValue(s.Npc), L("CalamityLoot.RareCatchBody", s.NoteValue), BookAcquisitionKind.Catch, BookAcquisitionRules.Chance(s.Chance), "×1", 0, s.Npc, true, -1, s.Owner, L("CalamityLoot.RareCatchLabel"));
		case "abysschest":
		{
			string boss = Lang.GetNPCNameValue(NPCID.SkeletronHead);
			return new BookAcquisitionEntry(L("CalamityLoot.AbyssChests"), L("CalamityLoot.AbyssChestsBody", boss), BookAcquisitionKind.Chest, "", "×1", s.Item, 0, true, -1, s.Owner, L("Common.After", boss));
		}
		case "blender":
			return new BookAcquisitionEntry(ItemName(s.From), L("CalamityLoot.BlenderBody", Lang.GetBuffName(BuffID.WellFed2), Lang.GetBuffName(BuffID.WellFed3), ItemName(ItemType("QualitySlop")), ItemName(ItemType("DisgustingSlop"))), BookAcquisitionKind.Use, "", "×1", s.From, 0, false, -1, s.Owner, L("CalamityLoot.BlenderLabel"));
		case "altar":
		{
			string altar = Lang.GetMapObjectName(MapHelper.TileToLookup(TileID.DemonAltar, WorldGen.crimson ? 1 : 0));
			return new BookAcquisitionEntry(ItemName(s.From), L("CalamityLoot.AltarBody", altar), BookAcquisitionKind.Use, "", "×1", s.From, 0, true, -1, s.Owner, L("CalamityLoot.AltarLabel", altar));
		}
		case "starter":
			return new BookAcquisitionEntry(L("CalamityLoot.NewCharacter"), L("CalamityLoot.NewCharacterBody"), BookAcquisitionKind.Gift, "", "×1", s.Item, 0, false, -1, s.Owner);
		case "bossrush":
			return new BookAcquisitionEntry(L("CalamityLoot.BossRush"), L("CalamityLoot.BossRushBody", ItemName(s.From)), BookAcquisitionKind.Gift, "", "×1", s.From, 0, true, -1, s.Owner, L("CalamityLoot.BossRushLabel"));
		case "wulfrum":
			return new BookAcquisitionEntry(L("CalamityLoot.WulfrumArmor"), L("CalamityLoot.WulfrumBody", ItemName(s.FromAny[0]), ItemName(s.FromAny[1]), ItemName(s.FromAny[2])), BookAcquisitionKind.SetBonus, "", "×1", s.From, 0, true, -1, s.Owner, L("CalamityLoot.WulfrumLabel"));
		}
		return null;
	}

	// Every row with its step (-1: none in this world) and, for an item that comes from other items, their groups.
	private static IEnumerable<(BookItemSource Source, string Why, Func<int> Step, int[][] Inputs)> Rows()
	{
		if (!ModLoader.HasMod(Calamity))
		{
			yield break;
		}
		Func<int> scal = () => BossStep("SupremeCalamitas");
		// The Exhumed curse of Calamitas the Brimstone Witch (CalamitasEnchantUI.InteractWithEnchantIcon) turns each item of
		// EnchantmentManager.ItemUpgradeRelationship into its upgrade; she moves in after Supreme Calamitas (CanTownNPCSpawn:
		// downedCalamitas). Read from the dictionary, which Calamity's ModCall can extend.
		int witch = NpcType("BrimstoneWitch");
		var upgrades = ModLoader.GetMod(Calamity).Code.GetType("CalamityMod.UI.CalamitasEnchants.EnchantmentManager")?.GetProperty("ItemUpgradeRelationship")?.GetValue(null) as Dictionary<int, int>;
		foreach (var (from, to) in upgrades ?? new Dictionary<int, int>())
		{
			if (witch > 0 && ContentSamples.ItemsByType.ContainsKey(from) && ContentSamples.ItemsByType.ContainsKey(to))
			{
				yield return (Loot(to, "enchant") with { From = from, Npc = witch }, "enchant", scal, new[] { new[] { from } });
			}
		}
		// Getfixedboi drops whose rules are left out of the loot report (hideLootReport): the Old Duke's Shattered Community
		// (OldDuke.ModifyNPCLoot, DropHelper.GFB) and Ares's Lava Chicken Broth when the Exo Mechs come as the Exo Mechdusa
		// (AresBody.ModifyNPCLoot; Draedon.AI sets it when Draedon is contacted with a Bloody Vein in the Codebreaker,
		// CodebreakerUI).
		IItemDropRuleCondition gfb = Condition.ZenithWorld.ToDropCondition(ShowItemDropInUI.WhenConditionSatisfied);
		IItemDropRuleCondition mechdusa = new SimpleItemDropRuleCondition(Text("CalamityLoot.ExoMechdusa").WithFormatArgs(Lang.GetItemName(ItemType("BloodyVein")), Lang.GetItemName(ItemType("CodebreakerBase"))), () => Main.zenithWorld, ShowItemDropInUI.WhenConditionSatisfied);
		foreach (var (item, npc, boss, conditions) in new[] { ("ShatteredCommunity", "OldDuke", "CalamityMod/OldDuke", new[] { gfb }), ("LavaChickenBroth", "AresBody", "CalamityMod/Draedon", new[] { gfb, mechdusa }) })
		{
			int type = ItemType(item), npcType = NpcType(npc);
			if (type > 0 && npcType > 0)
			{
				yield return (new BookItemSource(BookAcquisitionKind.Drop, type) { Npc = npcType, Owner = Calamity, Rate = new DropRateInfo(type, 1, 1, 1f, conditions.ToList()) }, "gfb", () => Main.zenithWorld ? BookSteps.Resolve(boss) : -1, null);
			}
		}
		// The Divine Swine, one at a time in the sky (SpawnChance 0.001), takes a Platinum Coin from the inventory or Piggy
		// Bank when right-clicked (TryOfferingPlatinumToSwine) and gives Delicious Meat (AI, Timer 210).
		int swine = NpcType("DivineSwine"), meat = ItemType("DeliciousMeat");
		if (swine > 0 && meat > 0)
		{
			yield return (new BookItemSource(BookAcquisitionKind.Trade, meat) { Npc = swine, FromAny = new int[] { ItemID.PlatinumCoin }, Owner = Calamity }, "swine", () => BookProgression.NpcStep(swine), null);
		}
		foreach (var (item, npc, oneIn) in RareCatches)
		{
			int type = ItemType(item), npcType = NpcType(npc);
			if (type > 0 && npcType > 0)
			{
				yield return (Loot(type, "catch") with { Npc = npcType, Chance = 1f / oneIn, NoteValue = oneIn }, "catch", () => BookProgression.NpcStep(npcType), null);
			}
		}
		// Abyss.AbyssChest locks an Ancient Treasure Chest on each Abyss island; Skeletron's first defeat unlocks them all
		// (CalamityGlobalNPC: UnlockAllAbyssChests), and AbyssTreasureChest.UnlockChest waits for it too. A chest breaks only
		// once empty (Chest.CanDestroyChest), so the chest itself comes after Skeletron.
		int chest = ItemType("AbyssTreasureChest");
		if (chest > 0)
		{
			yield return (Loot(chest, "abysschest") with { Where = new[] { "CalamityMod/AbyssLayer2Biome" } }, "abysschest", () => BookSteps.Resolve("SkeletronHead"), null);
		}
		// Using a food on a placed Gluttony Blender (GluttonyBlenderGlobalItem.CanUseItem) blends it into Quality Slop with a
		// chance of buff minutes ÷ 10 percent (×2 Plenty Satisfied, ×10 Exquisitely Stuffed), else Disgusting Slop
		// (GluttonyBlenderAnimation.AI).
		int blender = ItemType("GluttonyBlender");
		foreach (string slop in new[] { "QualitySlop", "DisgustingSlop" })
		{
			if (blender > 0 && ItemType(slop) is int type and > 0)
			{
				yield return (Loot(type, "blender") with { From = blender }, "blender", () => 0, new[] { new[] { blender } });
			}
		}
		// A Pwnhammer lying on a Demon or Crimson Altar turns into the Evil Smasher with its modifier (EvilSmasherSacrifice.Update).
		if (ItemType("EvilSmasher") is int smasher and > 0)
		{
			yield return (Loot(smasher, "altar") with { From = ItemID.Pwnhammer }, "altar", () => 0, new[] { new int[] { ItemID.Pwnhammer } });
		}
		// CalamityPlayer.AddStartingItems: every new character gets one, not again after a mediumcore death.
		if (ItemType("StarterBag") is int bag and > 0)
		{
			yield return (Loot(bag, "starter"), "starter", () => 0, null);
		}
		// BossRushEndEffectThing.OnKill: winning the Boss Rush (started with the Terminus) gives every player a Rock. Boss
		// Checklist puts the event after every boss (WeakReferenceSupport: 25.99).
		if (ItemType("Rock") is int rock and > 0)
		{
			yield return (Loot(rock, "bossrush") with { From = ItemType("Terminus") }, "bossrush", scal, null);
		}
		// WulfrumHat: with the whole Wulfrum armor on, the Wulfrum Bastion puts the cannon in the player's hand until it ends
		// (a HeldOnlyItem: it cannot be picked up or kept).
		int[] wulfrum = { ItemType("WulfrumHat"), ItemType("WulfrumJacket"), ItemType("WulfrumOveralls") };
		if (ItemType("WulfrumFusionCannon") is int cannon and > 0 && wulfrum.All((int piece) => piece > 0))
		{
			yield return (Loot(cannon, "wulfrum") with { From = wulfrum[0], FromAny = wulfrum }, "wulfrum", () => 0, wulfrum.Select((int piece) => new[] { piece }).ToArray());
		}
		// Natural rows (How to Get's own Tree, Plant and World rows).
		foreach (var (item, where, tile) in ShakenFruit)
		{
			if (ItemType(item) is int type and > 0)
			{
				yield return (new BookItemSource(BookAcquisitionKind.Tree, type) { Tile = tile, FrameX = 22, FrameY = 198, Owner = Calamity, Where = new[] { where }, Note = "shake" }, "shake", () => where == Astral ? BookSteps.HardmodeStep : 0, null);
			}
		}
		// SpineTree.GetItemDrops: the top piece (frame 36) of a Brimstone Crag spine tree drops one or two saplings.
		if (ItemType("SpineSapling") is int sapling and > 0 && TileType("SpineTree") is int spine and > 0)
		{
			yield return (new BookItemSource(BookAcquisitionKind.Tree, sapling) { Tile = spine, FrameX = 36, Owner = Calamity, Min = 1, Max = 2, Where = new[] { Crag }, Note = "treetop" }, "spine", () => 0, null);
		}
		// WorldMiscUpdateSystem: after Yharon, Lilies of Finality grow on the Crag's blocks before its walls (at most one in 66
		// tiles); the tile drops the item (RegisterItemDrop).
		if (ItemType("LiliesOfFinality") is int lilies and > 0 && TileType("LiliesOfFinalityTile") is int lily and > 0)
		{
			yield return (new BookItemSource(BookAcquisitionKind.Plant, lilies) { Tile = lily, Owner = Calamity, Where = new[] { Crag }, Note = "boss:CalamityMod/Yharon" }, "lilies", () => BossStep("Yharon"), null);
		}
		// AbyssalPots.GetItemDrops: an Abyssal Pot breaks into Abyssal Treasure (a bomb instead at the worst luck, a Fallen
		// Star 1 in 5 in a remix world).
		if (ItemType("AbyssalTreasure") is int treasure and > 0 && TileType("AbyssalPots") is int pot and > 0)
		{
			yield return (new BookItemSource(BookAcquisitionKind.World, treasure) { Tile = pot, Owner = Calamity, Where = DeepAbyss }, "pot", () => 0, null);
		}
	}

	private static BookItemSource Loot(int item, string note)
	{
		return new BookItemSource(BookAcquisitionKind.CalamityLoot, item) { Owner = Calamity, Note = note };
	}

	private static int ItemType(string name)
	{
		return ModContent.TryFind(Calamity, name, out ModItem item) ? item.Type : 0;
	}

	private static int NpcType(string name)
	{
		return ModContent.TryFind(Calamity, name, out ModNPC npc) ? npc.Type : 0;
	}

	private static int TileType(string name)
	{
		return ModContent.TryFind(Calamity, name, out ModTile tile) ? tile.Type : 0;
	}

	private static int BossStep(string npc)
	{
		return BookSteps.Resolve(Calamity + "/" + npc);
	}

	private static string BossName(string npc)
	{
		return Lang.GetNPCNameValue(NpcType(npc));
	}

	private static string ItemName(int type)
	{
		return type > 0 ? Lang.GetItemNameValue(type) : "";
	}
}
