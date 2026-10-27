using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ModernRecipeBook.UI.Book;

// How to Get rows from the shared source model (BookItemSources): the text is made here, in the language
// shown, from the model's language-neutral records.
public sealed partial class RecipeBookSystem
{
	private static List<BookAcquisitionEntry> SourceEntries(int type)
	{
		List<BookAcquisitionEntry> entries = new List<BookAcquisitionEntry>();
		foreach (BookItemSource source in BookItemSources.For(type))
		{
			try
			{
				BookAcquisitionEntry entry = SourceEntry(source);
				if (entry != null)
				{
					entries.Add(entry);
				}
			}
			catch (Exception)
			{
				AcquisitionReadErrors++;
			}
		}
		FromBags(entries);
		// Natural rows that read the same (acorns from treetops, dirt from every grass) are one row naming
		// every place; How to Get lists the world's sources first.
		return (from p in entries.Select((BookAcquisitionEntry e, int i) => (Entry: e, Index: i))
			let e = p.Entry
			orderby IsNatural(e.Kind) ? 0 : 1, p.Index
			group e by (e.Kind, e.ConditionLabel, e.Chance, e.Amount, Key: IsNatural(e.Kind) ? -1 : p.Index) into g
			select Places(g.Count() == 1 ? g.First() : g.First() with
			{
				Name = string.Join(", ", g.SelectMany((BookAcquisitionEntry x) => x.Name.Split(", ")).Distinct()),
				Body = string.Join("\n", g.Select((BookAcquisitionEntry x) => x.Body).Where((string b) => b.Length > 0).Distinct())
			})).ToList();
	}

	// As the mob's card (RecipeBookSystem.Bestiary FromBags): in Expert and Master a boss's Classic-only drop comes out of
	// the Treasure Bag it drops instead, so its row takes the bag's chance and amount and names the bag.
	private static void FromBags(List<BookAcquisitionEntry> entries)
	{
		List<BookAcquisitionEntry> bags = entries.Where((BookAcquisitionEntry e) => e.Kind == BookAcquisitionKind.Container && !e.OtherMode && ItemID.Sets.BossBag[e.ItemType]).ToList();
		for (int i = 0; i < entries.Count && bags.Count > 0; i++)
		{
			BookAcquisitionEntry drop = entries[i];
			if (drop.Kind != BookAcquisitionKind.Drop || !drop.OtherMode || drop.NpcNetId == 0)
			{
				continue;
			}
			BookAcquisitionEntry bag = bags.FirstOrDefault((BookAcquisitionEntry b) => BookItemSources.For(b.ItemType).Any((BookItemSource s) => s.Kind == BookAcquisitionKind.Drop && s.Npc == drop.NpcNetId && DropEntry(s) is { OtherMode: false }));
			if (bag != null)
			{
				string label = L("KillConditions.InBag", ItemName(bag.ItemType)) + (bag.ConditionLabel.Length > 0 ? " · " + bag.ConditionLabel : "");
				entries[i] = drop with { Chance = bag.Chance, Amount = bag.Amount, Body = label, ConditionLabel = label, Conditional = true, ReportKey = bag.ReportKey, OtherMode = false };
			}
		}
	}

	// A title naming more than three places keeps the first three and a count; the tooltip lists them all.
	private static BookAcquisitionEntry Places(BookAcquisitionEntry entry)
	{
		string[] places = entry.Name.Split(", ");
		if (!IsNatural(entry.Kind) || places.Length <= 3)
		{
			return entry;
		}
		string all = L("HowToGet.Where", entry.Name);
		return entry with
		{
			Name = L("HowToGet.PlacesAndMore", string.Join(", ", places.Take(3)), places.Length - 3),
			Body = (entry.Body.Length > 0) ? (all + "\n" + entry.Body) : all
		};
	}

	// Dev audit for the test stand: one TSV line per catalogue item (type, internal name, recipes, drops +
	// containers + shops, the other routes, display name). Run after opening the book once.
	internal static IEnumerable<string> SourceAudit()
	{
		foreach (int type in AllItems)
		{
			List<BookItemSource> sources = BookItemSources.For(type);
			int old = sources.Count((BookItemSource s) => s.Kind is BookAcquisitionKind.Drop or BookAcquisitionKind.Container or BookAcquisitionKind.Shop);
			Item item = ContentSamples.ItemsByType[type];
			yield return string.Join('\t', type, item.ModItem?.FullName ?? ItemID.Search.GetName(type), RecipesByItem.GetValueOrDefault(type)?.Count ?? 0, old, sources.Count - old, string.Join(",", sources.Where((BookItemSource s) => s.Kind is not (BookAcquisitionKind.Drop or BookAcquisitionKind.Container or BookAcquisitionKind.Shop)).Select((BookItemSource s) => s.Kind).Distinct()), item.Name);
		}
	}

	private static bool IsNatural(BookAcquisitionKind kind)
	{
		return kind is BookAcquisitionKind.Plant or BookAcquisitionKind.Tree or BookAcquisitionKind.Mining or BookAcquisitionKind.World or BookAcquisitionKind.Fishing or BookAcquisitionKind.Chest;
	}

	private static BookAcquisitionEntry SourceEntry(BookItemSource s)
	{
		switch (s.Kind)
		{
		case BookAcquisitionKind.Drop:
		case BookAcquisitionKind.Container:
			return DropEntry(s);
		case BookAcquisitionKind.Shop:
		{
			string[] conditions = s.Entry.Conditions.Select(ConditionName).ToArray();
			return new BookAcquisitionEntry(Lang.GetNPCNameValue(s.Npc), string.Join("; ", conditions), BookAcquisitionKind.Shop, "", "", 0, s.Npc, conditions.Length != 0, -1, "Terraria", string.Join("; ", conditions), s.Shop.Name);
		}
		case BookAcquisitionKind.Banner:
			return new BookAcquisitionEntry(NpcName(s.Npc), "", s.Kind, "", "×1", 0, s.Npc, false, -1, s.Owner, string.Format(L("HowToGet.Every0Kills"), s.NoteValue));
		case BookAcquisitionKind.Catch:
			return new BookAcquisitionEntry(NpcName(s.Npc), "", s.Kind, "", "×1", 0, s.Npc, false, -1, s.Owner);
		case BookAcquisitionKind.Shimmer:
		case BookAcquisitionKind.Use:
			return new BookAcquisitionEntry(ItemName(s.From), "", s.Kind, "", "×1", s.From, 0, false, -1, s.Owner);
		case BookAcquisitionKind.Machine:
		{
			string seconds = (s.NoteValue / 60f).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
			int keeps = ContentSamples.ItemsByType[s.Item].maxStack;
			return new BookAcquisitionEntry(ItemName(s.From), L("HowToGet.MachineMakesOneEvery", seconds, keeps), s.Kind, "", "×1", s.From, 0, false, -1, s.Owner, L("HowToGet.OneEverySeconds", seconds));
		}
		case BookAcquisitionKind.DevSet:
			// Bags exist in Expert and Master only.
			return new BookAcquisitionEntry(L("HowToGet.HardmodeBossBags"), L("HowToGet.DevSetFromBags"), s.Kind, BookAcquisitionRules.Chance(s.Chance), "×1", s.From, 0, true, -1, s.Owner, L("HowToGet.WithItsWholeSet"), OtherMode: !Main.expertMode);
		case BookAcquisitionKind.Record:
			return new BookAcquisitionEntry(ItemName(s.From), L("HowToGet.WearABlankBoxWhileIts"), s.Kind, "", "×1", s.From, 0, false, -1, s.Owner, L("HowToGet.WhileItsTrackPlays"));
		case BookAcquisitionKind.Trade:
		{
			string gate = GateText(s.Note);
			string label = L("HowToGet.For", ItemName(s.FromAny.FirstOrDefault())) + (gate.Length > 0 ? " · " + gate : "");
			return new BookAcquisitionEntry(NpcName(s.Npc), label, s.Kind, "", "×1", 0, s.Npc, gate.Length > 0, -1, s.Owner, label);
		}
		case BookAcquisitionKind.Chest:
		{
			if (s.Where.Length == 0)
			{
				return new BookAcquisitionEntry(L("HowToGet.WorldChestsAndStructures"), L("HowToGet.PlacedThereWhenTheWorldIs"), s.Kind, "", "", ItemID.Chest, 0, false, -1, s.Owner, L("HowToGet.FromWorldCreation"));
			}
			// A chest that waits for a boss (BookItemSources.CalamityGatedWorldgenItems): its place, and the boss.
			string boss = NpcTypeName(s.Note.Substring(5));
			(string place, string body) = s.Where[0] == "AstralChest"
				? (L("HowToGet.ChestAstralChest"), L("HowToGet.ChestAstralChestBody", boss))
				: (L("HowToGet.ChestHellLab"), L("HowToGet.ChestHellLabBody", boss));
			return new BookAcquisitionEntry(place, body, s.Kind, "", "", s.From, 0, true, -1, s.Owner, GateText(s.Note));
		}
		case BookAcquisitionKind.Fishing:
			return FishingEntry(s);
		default:
			return BookCurated.Entry(s) ?? NaturalEntry(s);
		}
	}

	private static BookAcquisitionEntry DropEntry(BookItemSource s)
	{
		DropRateInfo rate = s.Rate.Value;
		// As on the mob's card (ReadLoot): another mob's drop that shares these rules is no source of this mob, and what the
		// game's Bestiary hides is no source shown.
		if ((s.Kind == BookAcquisitionKind.Drop && BookKillConditions.OtherNpcs(rate, s.Npc)) || BookProgressionSources.HiddenFromUI(rate))
		{
			return null;
		}
		List<string> conditions = new List<string>();
		List<string> conditionKeys = new List<string>();
		List<string> shortConditions = new List<string>();
		bool otherMode = false;
		foreach (IItemDropRuleCondition condition in rate.conditions ?? new List<IItemDropRuleCondition>())
		{
			conditionKeys.Add(condition?.GetType().AssemblyQualifiedName ?? "<null>");
			try
			{
				// As the Bestiary reads it (ReadLoot): a met difficulty or seed gate is no condition in this world.
				bool? gate = BookProgressionSources.WorldGate(condition);
				otherMode |= gate == false;
				if (gate == true)
				{
					continue;
				}
				// Vanilla's difficulty conditions without a text of their own get their word.
				string description = condition.GetConditionDescription();
				if (string.IsNullOrWhiteSpace(description))
				{
					description = DropConditionLabel(condition, "");
				}
				string full = string.IsNullOrWhiteSpace(description) ? HiddenConditionLabel(condition, s.Npc) : description;
				// A test of the dying mob that its normal kill meets says nothing, as on its card.
				if (full.Length == 0)
				{
					continue;
				}
				conditions.Add(full);
				shortConditions.Add(DropConditionLabel(condition, full));
			}
			catch (Exception)
			{
				string unavailable = L("HowToGet.UnknownCondition");
				conditions.Add(unavailable);
				shortConditions.Add(unavailable);
				AcquisitionReadErrors++;
			}
		}
		string name = s.Kind == BookAcquisitionKind.Container ? ItemName(s.From) : (s.Npc == 0 ? L("HowToGet.AnyEnemy") : ContentSamples.NpcsByNetId[s.Npc].FullName);
		return new BookAcquisitionEntry(name, string.Join("; ", conditions.Distinct()), s.Kind, BookAcquisitionRules.Chance(rate.dropRate), "×" + BookAcquisitionRules.Stack(rate.stackMin, rate.stackMax), s.Kind == BookAcquisitionKind.Container ? s.From : 0, s.Npc, conditions.Count > 0, -1, s.Owner, string.Join("; ", shortConditions.Distinct()), BookAcquisitionGrouping.DropRuleKey(rate.dropRate, rate.stackMin, rate.stackMax, string.Join("|", conditionKeys.OrderBy((string x) => x, StringComparer.Ordinal))), OtherMode: otherMode);
	}

	private static BookAcquisitionEntry FishingEntry(BookItemSource s)
	{
		string[] note = s.Note.Split('|');
		if (s.Note == "quest")
		{
			string where = QuestCatchLine(s.Item);
			return new BookAcquisitionEntry(where.Length > 0 ? where : L("HowToGet.AnglerQuest"), L("HowToGet.CaughtOnlyWhileTheAnglerAsks"), s.Kind, "", "×1", ItemID.WoodFishingPole, 0, true, -1, s.Owner, L("HowToGet.AnglerQuest2"));
		}
		List<string> parts = new List<string>();
		string tier = TierText(note[0]);
		if (tier.Length > 0)
		{
			parts.Add(tier);
		}
		foreach (string gate in (note.Length > 1 ? note[1] : "").Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			parts.Add(GateText(gate));
		}
		string label = string.Join(" · ", parts.Where((string p) => p.Length > 0));
		return new BookAcquisitionEntry(WhereText(s.Where), "", s.Kind, "", "×1", ItemID.WoodFishingPole, 0, label.Length > 0, -1, s.Owner, label);
	}

	// Plants, trees, mining and world finds: the title says where, the detail what to do and when.
	private static BookAcquisitionEntry NaturalEntry(BookItemSource s)
	{
		string where = WhereText(s.Where);
		List<string> parts = new List<string>();
		string body = "";
		switch (s.Note)
		{
		case "world":
			where = L("HowToGet.FoundInTheWorld");
			break;
		case "starfall":
			parts.Add(L("HowToGet.FallsAtNight"));
			break;
		case "orb":
			parts.Add(Lang.GetMapObjectName(MapHelper.TileToLookup(TileID.ShadowOrbs, s.FrameX >= 36 ? 1 : 0)));
			break;
		case "mech":
			parts.Add(L("HowToGet.AfterAMechanicalBoss"));
			break;
		case "treetop":
			parts.Add(L("HowToGet.Treetop"));
			break;
		case "shake":
			parts.Add(L("HowToGet.ShakeIt"));
			break;
		case "shake:halloween":
			parts.Add(L("HowToGet.ShakeIt"));
			parts.Add(L("HowToGet.Halloween"));
			break;
		default:
			if (s.Note.StartsWith("bloom:", StringComparison.Ordinal))
			{
				parts.Add(BloomText(s.Note.Substring(6)));
			}
			else if (s.Note.Length > 0)
			{
				parts.Add(GateText(s.Note));
			}
			break;
		}
		bool grass = s.Grounds.Length > 0 && s.Grounds.All(IsGrass);
		bool herb = s.Note.StartsWith("bloom:", StringComparison.Ordinal);
		if (s.Kind == BookAcquisitionKind.Plant && grass && !herb)
		{
			parts.Insert(0, L("HowToGet.OnGrass"));
		}
		if (s.NoteValue > 0)
		{
			parts.Add(L("HowToGet.Pickaxe", s.NoteValue));
		}
		// The ground goes in the tooltip, unless the detail already says "on grass".
		if (s.Grounds.Length > 0 && s.Kind == BookAcquisitionKind.Plant && (herb || !grass))
		{
			body = grass ? L("HowToGet.GrowsOnGrass") : L("HowToGet.GrowsOn", string.Join(", ", s.Grounds.Select(GroundName).Distinct()));
		}
		if (where.Length == 0)
		{
			where = L("HowToGet.InTheWorld");
		}
		string chance = s.Chance > 0f && s.Chance < 1f ? BookAcquisitionRules.Chance(s.Chance) : "";
		string amount = s.Kind == BookAcquisitionKind.World && s.Note == "world" ? "" : "×" + BookAcquisitionRules.Stack(s.Min, s.Max);
		// Icons: the tile itself (a blooming herb, the gem in its stone), an acorn for ordinary trees.
		bool acorn = s.Tile is TileID.Trees or TileID.PalmTree or TileID.VanityTreeSakura or TileID.VanityTreeYellowWillow or TileID.TreeAsh || (s.Kind == BookAcquisitionKind.Tree && s.Tile >= TileID.Count);
		int icon = acorn ? ItemID.Acorn : (s.Tile < 0 ? s.From : 0);
		int tile = acorn ? -1 : (s.Tile == TileID.MatureHerbs ? TileID.BloomingHerbs : s.Tile);
		string label = string.Join(" · ", parts.Where((string p) => !string.IsNullOrEmpty(p)));
		return new BookAcquisitionEntry(where, body, s.Kind, chance, amount, icon, 0, false, -1, s.Owner, label, "", tile, s.FrameX, s.FrameY);
	}

	// "Bestiary_Biomes.Jungle" through the game's localization, "CalamityMod/SunkenSeaBiome" through the ModBiome.
	private static string WhereText(IEnumerable<string> where)
	{
		return string.Join(", ", where.Select(delegate(string w)
		{
			switch (w)
			{
			case "any":
				return L("HowToGet.AnyWater");
			case "lava":
				return L("HowToGet.Lava");
			case "honey":
				return L("HowToGet.Honey");
			}
			if (w.Contains('/'))
			{
				return ModContent.TryFind(w, out ModBiome biome) ? biome.DisplayName.Value : "";
			}
			return Language.GetTextValue(w);
		}).Where((string w) => w.Length > 0).Distinct());
	}

	private static string BloomText(string when)
	{
		return when switch
		{
			"day" => L("HowToGet.BloomsByDay"),
			"night" => L("HowToGet.BloomsAtNight"),
			"moon" => L("HowToGet.BloomsAtABloodOrFull"),
			"rain" => L("HowToGet.BloomsInRain"),
			"dusk" => L("HowToGet.BloomsAtSunset"),
			_ => L("HowToGet.BloomsWhenGrown")
		};
	}

	private static string TierText(string tier)
	{
		return tier switch
		{
			"common" => L("HowToGet.Common"),
			"uncommon" => L("HowToGet.Uncommon"),
			"rare" => L("HowToGet.Rare"),
			"veryrare" => L("HowToGet.VeryRare"),
			"legendary" => L("HowToGet.Legendary"),
			"crate" => L("HowToGet.Crate"),
			"junk" => L("HowToGet.JunkInSmallPonds"),
			_ => ""
		};
	}

	// Gates of fishing rows, trades and boss-spawned ores.
	private static string GateText(string gate)
	{
		if (gate.StartsWith("boss:", StringComparison.Ordinal))
		{
			return GateTermText(gate.Substring(5));
		}
		if (gate.StartsWith("mechs:", StringComparison.Ordinal))
		{
			return GateTermText(gate);
		}
		if (gate.StartsWith("needs:", StringComparison.Ordinal))
		{
			return L("HowToGet.With", ItemName(int.Parse(gate.Substring(6))));
		}
		if (gate.StartsWith("rod:", StringComparison.Ordinal))
		{
			string rod = gate.Substring(4);
			return L("HowToGet.WithRod", ModContent.TryFind(rod, out ModItem item) ? item.DisplayName.Value : rod);
		}
		return gate switch
		{
			"Hardmode" => L("HowToGet.Hardmode"),
			"PostHardmode" => L("HowToGet.AfterMoonLord"),
			"BloodMoon" => L("HowToGet.BloodMoon"),
			"Day" => L("HowToGet.ByDay"),
			"Night" => L("HowToGet.AtNight"),
			"lava" => L("HowToGet.InLava"),
			_ => ""
		};
	}

	private static string NpcTypeName(string name)
	{
		if (name.Contains('/'))
		{
			return ModContent.TryFind(name, out ModNPC npc) ? Lang.GetNPCNameValue(npc.Type) : name;
		}
		return NPCID.Search.TryGetId(name, out int id) ? Lang.GetNPCNameValue(id) : name;
	}

	// The Angler's catch line: the last "(...)" of his quest text, or ModItem.AnglerQuestChat's location.
	private static string QuestCatchLine(int fish)
	{
		Item item = ContentSamples.ItemsByType[fish];
		if (item.ModItem != null)
		{
			string description = "", location = "";
			item.ModItem.AnglerQuestChat(ref description, ref location);
			return location.Trim();
		}
		string text = Language.GetTextValue("AnglerQuestText.Quest_" + ItemID.Search.GetName(fish));
		int open = text.LastIndexOf('(');
		int close = text.LastIndexOf(')');
		return open >= 0 && close > open ? text.Substring(open + 1, close - open - 1).Trim() : "";
	}

	private static bool IsGrass(int tile)
	{
		return TileID.Sets.Grass[tile] || TileID.Sets.GrassSpecial[tile];
	}

	private static string GroundName(int tile)
	{
		return IsGrass(tile) ? L("HowToGet.Grass") : TileName(tile);
	}

	// A tile's name: the item that places it, else its map legend.
	private static string TileName(int tile)
	{
		if (StationItems.TryGetValue(tile, out Item item))
		{
			return item.Name;
		}
		return MapTileName(tile, 0);
	}

	// Stand check: every recipe station that no item places, and every recipe condition, with the text the card shows.
	internal static string StationAudit()
	{
		IEnumerable<string> stations = from t in Main.recipe.Take(Recipe.numRecipes).SelectMany((Recipe r) => r.requiredTile).Where((int t) => t >= 0).Distinct()
			where !StationItems.ContainsKey(t)
			select t + "=" + StationName(t);
		IEnumerable<string> conditions = Main.recipe.Take(Recipe.numRecipes).SelectMany((Recipe r) => r.Conditions).Select(ConditionName).Distinct();
		return "stations without an item: " + string.Join(" | ", stations) + "\nconditions: " + string.Join(" | ", conditions);
	}

	// The map legend of a tile style, else a modded tile's name.
	private static string MapTileName(int tile, int style)
	{
		string map = Lang.GetMapObjectName(MapHelper.TileToLookup(tile, style));
		return string.IsNullOrWhiteSpace(map) ? (TileLoader.GetTile(tile)?.Name ?? ("#" + tile)) : map;
	}

	private static string ItemName(int type)
	{
		return type > 0 && ContentSamples.ItemsByType.TryGetValue(type, out Item item) ? item.Name : "";
	}

	private static string NpcName(int netId)
	{
		return ContentSamples.NpcsByNetId.TryGetValue(netId, out NPC npc) ? npc.FullName : Lang.GetNPCNameValue(netId);
	}

	// The tile piece itself: a plant's frame, an object's whole style, a block's middle piece; the card's item
	// when the tile has no usable piece (Sea Prism draws through a shader).
	private static void DrawTileIcon(SpriteBatch sb, int tile, int frameX, int frameY, BookRect rect, int fallbackItem)
	{
		Texture2D texture = tile >= 0 && tile < TextureAssets.Tile.Length ? BookWorldSources.TileTexture(tile) : null;
		if (texture == null || texture.Width < 16 || texture.Height < 16)
		{
			Fallback();
			return;
		}
		Rectangle frame;
		if (!Main.tileFrameImportant[tile])
		{
			frame = new Rectangle(18, 18, 16, 16);
		}
		else
		{
			TileObjectData data = TileObjectData.GetTileData(tile, 0);
			int width = data != null ? data.CoordinateFullWidth - data.CoordinatePadding : 16;
			int height = data != null ? data.CoordinateFullHeight - data.CoordinatePadding : Math.Min(texture.Height, 24) - (texture.Height > 24 ? 8 : 0);
			if (tile == TileID.ShadowOrbs)
			{
				(width, height) = (34, 34);
			}
			frame = new Rectangle(frameX, frameY, Math.Max(1, width), Math.Max(1, height));
		}
		frame = Rectangle.Intersect(frame, texture.Bounds);
		if (frame.Width < 8 || frame.Height < 8)
		{
			Fallback();
			return;
		}
		float scale = Math.Min(2f, Math.Min(rect.Width / (float)frame.Width, rect.Height / (float)frame.Height));
		sb.Draw(texture, Origin + new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f) * Scale, frame, Color.White * ModalOpacity, 0f, frame.Size() / 2f, scale * Scale, SpriteEffects.None, 0f);
		void Fallback()
		{
			if (ContentSamples.ItemsByType.TryGetValue(fallbackItem, out Item item))
			{
				Icon(sb, item, rect.X, rect.Y, rect.Width, tooltip: false);
			}
			else
			{
				Label(sb, rect, "?", Muted);
			}
		}
	}
}
