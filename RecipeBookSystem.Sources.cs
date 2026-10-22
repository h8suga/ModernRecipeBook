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

namespace ModernRecipeBook;

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
		// Natural rows that read the same (acorns from treetops, dirt from every grass) are one row naming
		// every place; How to Get lists the world's sources first.
		return (from p in entries.Select((BookAcquisitionEntry e, int i) => (Entry: e, Index: i))
			let e = p.Entry
			orderby IsNatural(e.Kind) ? 0 : 1, p.Index
			group e by (e.Kind, e.ConditionLabel, e.Chance, e.Amount, Key: IsNatural(e.Kind) ? -1 : p.Index) into g
			select g.Count() == 1 ? g.First() : g.First() with
			{
				Name = string.Join(", ", g.SelectMany((BookAcquisitionEntry x) => x.Name.Split(", ")).Distinct()),
				Body = string.Join("\n", g.Select((BookAcquisitionEntry x) => x.Body).Where((string b) => b.Length > 0).Distinct())
			}).ToList();
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
			string[] conditions = s.Entry.Conditions.Select((Condition c) => c.Description.Value).ToArray();
			return new BookAcquisitionEntry(Lang.GetNPCNameValue(s.Npc), string.Join("; ", conditions), BookAcquisitionKind.Shop, "", "", 0, s.Npc, conditions.Length != 0, -1, "Terraria", string.Join("; ", conditions), s.Shop.Name);
		}
		case BookAcquisitionKind.Banner:
			return new BookAcquisitionEntry(NpcName(s.Npc), "", s.Kind, "", "×1", 0, s.Npc, false, -1, s.Owner, string.Format(T("каждые {0} убийств", "every {0} kills"), s.NoteValue));
		case BookAcquisitionKind.Catch:
			return new BookAcquisitionEntry(NpcName(s.Npc), "", s.Kind, "", "×1", 0, s.Npc, false, -1, s.Owner);
		case BookAcquisitionKind.Shimmer:
		case BookAcquisitionKind.Use:
			return new BookAcquisitionEntry(ItemName(s.From), "", s.Kind, "", "×1", s.From, 0, false, -1, s.Owner);
		case BookAcquisitionKind.Record:
			return new BookAcquisitionEntry(ItemName(s.From), T("Наденьте пустую шкатулку, пока играет мелодия", "Wear a blank box while its track plays"), s.Kind, "", "×1", s.From, 0, false, -1, s.Owner, T("пока играет мелодия", "while its track plays"));
		case BookAcquisitionKind.Trade:
		{
			string gate = GateText(s.Note);
			string label = T("за ", "for ") + ItemName(s.FromAny.FirstOrDefault()) + (gate.Length > 0 ? " · " + gate : "");
			return new BookAcquisitionEntry(NpcName(s.Npc), label, s.Kind, "", "×1", 0, s.Npc, gate.Length > 0, -1, s.Owner, label);
		}
		case BookAcquisitionKind.Chest:
			return new BookAcquisitionEntry(T("Сундуки и постройки мира", "World chests and structures"), T("Лежит там с создания мира", "Placed there when the world is made"), s.Kind, "", "", ItemID.Chest, 0, false, -1, s.Owner, T("с создания мира", "from world creation"));
		case BookAcquisitionKind.Fishing:
			return FishingEntry(s);
		default:
			return NaturalEntry(s);
		}
	}

	private static BookAcquisitionEntry DropEntry(BookItemSource s)
	{
		DropRateInfo rate = s.Rate.Value;
		List<string> conditions = new List<string>();
		List<string> conditionKeys = new List<string>();
		List<string> shortConditions = new List<string>();
		foreach (IItemDropRuleCondition condition in rate.conditions ?? new List<IItemDropRuleCondition>())
		{
			conditionKeys.Add(condition?.GetType().AssemblyQualifiedName ?? "<null>");
			try
			{
				string description = condition.GetConditionDescription();
				string full = string.IsNullOrWhiteSpace(description) ? T("Особое условие", "Special condition") : description;
				conditions.Add(full);
				shortConditions.Add(DropConditionLabel(condition, full));
			}
			catch (Exception)
			{
				string unavailable = T("Неизвестное условие", "Unknown condition");
				conditions.Add(unavailable);
				shortConditions.Add(unavailable);
				AcquisitionReadErrors++;
			}
		}
		string name = s.Kind == BookAcquisitionKind.Container ? ItemName(s.From) : (s.Npc == 0 ? T("Любые враги", "Any enemy") : ContentSamples.NpcsByNetId[s.Npc].FullName);
		return new BookAcquisitionEntry(name, string.Join("; ", conditions.Distinct()), s.Kind, BookAcquisitionRules.Chance(rate.dropRate), "×" + BookAcquisitionRules.Stack(rate.stackMin, rate.stackMax), s.Kind == BookAcquisitionKind.Container ? s.From : 0, s.Npc, conditions.Count > 0, -1, s.Owner, string.Join("; ", shortConditions.Distinct()), BookAcquisitionGrouping.DropRuleKey(rate.dropRate, rate.stackMin, rate.stackMax, string.Join("|", conditionKeys.OrderBy((string x) => x, StringComparer.Ordinal))));
	}

	private static BookAcquisitionEntry FishingEntry(BookItemSource s)
	{
		string[] note = s.Note.Split('|');
		if (s.Note == "quest")
		{
			string where = QuestCatchLine(s.Item);
			return new BookAcquisitionEntry(where.Length > 0 ? where : T("Задание рыбака", "Angler quest"), T("Ловится, только когда её просит Рыбак", "Caught only while the Angler asks for it"), s.Kind, "", "×1", ItemID.WoodFishingPole, 0, true, -1, s.Owner, T("задание рыбака", "Angler quest"));
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
			where = T("Встречается в мире", "Found in the world");
			break;
		case "starfall":
			parts.Add(T("падает ночью", "falls at night"));
			break;
		case "orb":
			parts.Add(Lang.GetMapObjectName(MapHelper.TileToLookup(TileID.ShadowOrbs, s.FrameX >= 36 ? 1 : 0)));
			break;
		case "mech":
			parts.Add(T("после механического босса", "after a mechanical boss"));
			break;
		case "treetop":
			parts.Add(T("верхушка", "treetop"));
			break;
		case "shake":
			parts.Add(T("потрясите", "shake it"));
			break;
		case "shake:halloween":
			parts.Add(T("потрясите", "shake it"));
			parts.Add(T("Хэллоуин", "Halloween"));
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
		if (s.Kind == BookAcquisitionKind.Plant && !s.Note.StartsWith("bloom:", StringComparison.Ordinal) && s.Grounds.Length > 0 && s.Grounds.All(IsGrass))
		{
			parts.Insert(0, T("на траве", "on grass"));
		}
		if (s.NoteValue > 0)
		{
			parts.Add(T("кирка ", "pickaxe ") + s.NoteValue + "%");
		}
		if (s.Grounds.Length > 0 && s.Kind == BookAcquisitionKind.Plant)
		{
			body = T("Растёт на: ", "Grows on: ") + string.Join(", ", s.Grounds.Select(GroundName).Distinct());
		}
		if (where.Length == 0)
		{
			where = T("В мире", "In the world");
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
				return T("Любая вода", "Any water");
			case "lava":
				return T("Лава", "Lava");
			case "honey":
				return T("Мёд", "Honey");
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
			"day" => T("цветёт днём", "blooms by day"),
			"night" => T("цветёт ночью", "blooms at night"),
			"moon" => T("цветёт в кровавую луну и полнолуние", "blooms at a blood or full moon"),
			"rain" => T("цветёт в дождь", "blooms in rain"),
			"dusk" => T("цветёт на закате", "blooms at sunset"),
			_ => T("цветёт, когда вырастет", "blooms when grown")
		};
	}

	private static string TierText(string tier)
	{
		return tier switch
		{
			"common" => T("обычный улов", "common"),
			"uncommon" => T("необычный улов", "uncommon"),
			"rare" => T("редкий улов", "rare"),
			"veryrare" => T("очень редкий улов", "very rare"),
			"legendary" => T("легендарный улов", "legendary"),
			"crate" => T("ящик", "crate"),
			"junk" => T("мусор в мелкой воде", "junk in small ponds"),
			_ => ""
		};
	}

	// Gates of fishing rows, trades and boss-spawned ores.
	private static string GateText(string gate)
	{
		if (gate.StartsWith("boss:", StringComparison.Ordinal))
		{
			return T("после: ", "after ") + NpcTypeName(gate.Substring(5));
		}
		if (gate.StartsWith("needs:", StringComparison.Ordinal))
		{
			return T("нужно: ", "with ") + ItemName(int.Parse(gate.Substring(6)));
		}
		if (gate.StartsWith("rod:", StringComparison.Ordinal))
		{
			string rod = gate.Substring(4);
			return T("удочкой: ", "with ") + (ModContent.TryFind(rod, out ModItem item) ? item.DisplayName.Value : rod);
		}
		return gate switch
		{
			"Hardmode" => T("Хардмод", "Hardmode"),
			"PostHardmode" => T("после Лунного лорда", "after Moon Lord"),
			"BloodMoon" => T("Кровавая луна", "Blood Moon"),
			"Day" => T("днём", "by day"),
			"Night" => T("ночью", "at night"),
			"lava" => T("в лаве", "in lava"),
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
		return IsGrass(tile) ? T("трава", "grass") : TileName(tile);
	}

	// A tile's name: the item that places it, else its map legend.
	private static string TileName(int tile)
	{
		if (StationItems.TryGetValue(tile, out Item item))
		{
			return item.Name;
		}
		string map = Lang.GetMapObjectName(MapHelper.TileToLookup(tile, 0));
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
