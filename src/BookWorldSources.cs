using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace ModernRecipeBook;

// Sources that come from the world itself: tiles broken (plants, herbs, trees, ores, gems, blocks), fishing,
// shaken trees and a few world events.
//
// Tile drops are read from the game's own code, not from hand lists: every tile type and frame is set up on
// a scratch tile at the world's edge and passed to WorldGen.KillTile_GetItemDrops (vanilla) or
// ModTile.GetItemDrops (mods), with the random generators replaced by one that walks every outcome of the
// rolls (so "1 in 40" and "×2-3" come out as chance and stack), and a stand-in player with empty hands
// standing next to it, carrying the two things drop code asks about (cordage for Vine Rope, a Blowpipe
// for Seeds; those rows say so). The scratch tiles, the random generators and the player slot are restored
// exactly afterwards; nothing is spawned or sent. Limits: multi-tile objects drop through WorldGen.Check*
// code, so they are read by the item that places them (statues) or curated (life crystals, orbs, Life
// Fruit); pots, chests and the extractinator spawn items directly and are not read; acorns from treetops
// are computed by the server only, so a multiplayer client misses them.
internal static class BookWorldSources
{
	private delegate void VanillaDrops(int x, int y, Tile tile, out int item, out int stack, out int secondary, out int secondaryStack, bool includeLargeObjectDrops);

	private static Dictionary<int, List<BookItemSource>> tiles;

	internal static int TileReadErrors { get; private set; }

	internal static void Clear()
	{
		tiles = null;
		TileReadErrors = 0;
	}

	// Tile sources of one item; the index is built the first time an item is looked up in a world.
	internal static IEnumerable<BookItemSource> For(int type)
	{
		if (tiles == null && !Main.gameMenu && Main.tile.Width > 64 && Main.maxTilesX > 64 && Main.maxTilesY > 64)
		{
			Build();
		}
		return tiles?.GetValueOrDefault(type) ?? Enumerable.Empty<BookItemSource>();
	}

	// Dev audit: one TSV line per tile source (item, tile, frame, kind, chance, stack, where, grounds).
	internal static IEnumerable<string> Audit()
	{
		For(ItemID.DirtBlock);
		return from pair in tiles ?? new Dictionary<int, List<BookItemSource>>()
			from s in pair.Value
			orderby s.Item, s.Tile
			select string.Join('\t', s.Item, ItemID.Search.TryGetName(s.Item, out string n) ? n : ContentSamples.ItemsByType[s.Item].ModItem?.FullName, s.Tile, s.FrameX + "," + s.FrameY, s.Kind, s.Chance.ToString("0.####"), s.Min + "-" + s.Max, string.Join(",", s.Where), string.Join(",", s.Grounds), s.Note);
	}

	// Tile sources ------------------------------------------------------------------------------------

	// Tiles whose drop code has side effects (pressure plates and logic sensors unregister themselves, the
	// Christmas tree spawns its ornaments, hives spill honey and spawn bees; minecart tracks index their track tables by frame).
	private static readonly HashSet<int> Unsafe = new HashSet<int> { TileID.WeightedPressurePlate, TileID.LogicSensor, TileID.ChristmasTree, TileID.Hive, TileID.MinecartTrack };

	// Ore-like tiles world generation never places: Sturdy Fossil comes from the extractinator, Amber Stone
	// Blocks are player-made, Luminite falls from Moon Lord.
	private static readonly HashSet<int> NeverGenerated = new HashSet<int> { TileID.FossilOre, TileID.AmberStoneBlock, TileID.LunarOre };

	// Natural objects whose drop is hardcoded in WorldGen.Check* and listed in CuratedTileSources: their
	// placing item (the repaired crystal) is not what breaking the world's one gives.
	private static readonly HashSet<int> CuratedTiles = new HashSet<int> { TileID.Heart, TileID.ManaCrystal, TileID.LifeCrystalBoulder, TileID.ShadowOrbs, TileID.LifeFruit };

	// WorldGen.PlantCheck: the ground each grass plant needs (78, 380 and 579 are pots, planter boxes and
	// the rock golem head, left out).
	private static readonly Dictionary<int, int[]> PlantGrounds = new Dictionary<int, int[]>
	{
		[TileID.Plants] = new int[] { TileID.Grass, TileID.GolfGrass },
		[TileID.Plants2] = new int[] { TileID.Grass, TileID.GolfGrass },
		[TileID.CorruptPlants] = new int[] { TileID.CorruptGrass, TileID.CorruptJungleGrass },
		[TileID.JunglePlants] = new int[] { TileID.JungleGrass },
		[TileID.JunglePlants2] = new int[] { TileID.JungleGrass },
		[TileID.MushroomPlants] = new int[] { TileID.MushroomGrass },
		[TileID.HallowedPlants] = new int[] { TileID.HallowedGrass, TileID.GolfGrassHallowed },
		[TileID.HallowedPlants2] = new int[] { TileID.HallowedGrass, TileID.GolfGrassHallowed },
		[TileID.CrimsonPlants] = new int[] { TileID.CrimsonGrass, TileID.CrimsonJungleGrass },
		[TileID.AshPlants] = new int[] { TileID.AshGrass }
	};

	private const int ScratchX = 24;

	private const int ScratchY = 24;

	private static void Build()
	{
		Stopwatch timer = Stopwatch.StartNew();
		tiles = new Dictionary<int, List<BookItemSource>>();
		TileReadErrors = 0;
		int evaluated = 0;
		HashSet<int> failed = new HashSet<int>();
		VanillaDrops vanilla = (VanillaDrops)Delegate.CreateDelegate(typeof(VanillaDrops), typeof(WorldGen).GetMethod("KillTile_GetItemDrops", BindingFlags.NonPublic | BindingFlags.Static));
		Dictionary<int, List<Item>> placedBy = (from i in ContentSamples.ItemsByType.Values
			where i != null && !i.IsAir && i.createTile > -1
			group i by i.createTile).ToDictionary((IGrouping<int, Item> g) => g.Key, (IGrouping<int, Item> g) => g.ToList());
		int[] treeGrounds = Enumerable.Range(0, TileLoader.TileCount).Where((int g) => WorldGen.GetTreeType(g) != TreeTypes.None).ToArray();
		using (Sandbox sandbox = new Sandbox())
		{
			for (int t = 0; t < TileLoader.TileCount; t++)
			{
				// Saplings give back what planted them; chests are loot, not a source of the chest.
				if (Unsafe.Contains(t) || NeverGenerated.Contains(t) || CuratedTiles.Contains(t) || TileID.Sets.BasicChest[t] || TileID.Sets.BasicChestFake[t] || TileID.Sets.BasicDresser[t] || IsSapling(t))
				{
					continue;
				}
				try
				{
					if (IsTrunk(t))
					{
						foreach (int ground in TrunkGrounds(t, treeGrounds))
						{
							foreach ((int fx, int fy) in new[] { (0, 0), (22, 198) })
							{
								evaluated++;
								Record(t, fx, fy, ground, sandbox.Drops(t, fx, fy, ground, vanilla));
							}
						}
						continue;
					}
					if (IsMultiTile(t))
					{
						// Multi-tile objects drop the item that places their style (WorldGen.Check*, ModTile default);
						// grown plants (pumpkins) drop their crop instead, which this cannot read.
						foreach (Item item in placedBy.GetValueOrDefault(t) ?? new List<Item>())
						{
							if (Natural(t) && (Category(t) != BookAcquisitionKind.Plant || t == TileID.Sunflower))
							{
								Add(new BookItemSource(Category(t), item.type) { Tile = t, FrameX = StyleFrame(t, item.placeStyle).X, FrameY = StyleFrame(t, item.placeStyle).Y, Owner = Owner(t), Chance = 1f, Where = Where(t, Grounds(t, item.placeStyle), item.placeStyle), Note = Note(t, 0, 0, item.type) });
							}
						}
						continue;
					}
					foreach ((int fx, int fy) in Frames(t, placedBy.GetValueOrDefault(t)))
					{
						evaluated++;
						Record(t, fx, fy, TileID.Stone, sandbox.Drops(t, fx, fy, TileID.Stone, vanilla));
					}
				}
				catch (Exception)
				{
					sandbox.Failed.Add(t);
				}
			}
			TileReadErrors = sandbox.Failed.Count;
			failed = sandbox.Failed;
		}
		foreach (BookItemSource source in CuratedTileSources())
		{
			Add(source);
		}
		// One row per tile and where: the frames of one plant that drop the item merge, at the best frame's
		// chance and the widest stack.
		foreach (int key in tiles.Keys.ToList())
		{
			tiles[key] = (from s in tiles[key]
				group s by (s.Kind, s.Tile, Where: string.Join(",", s.Where), s.Note) into g
				let best = g.OrderByDescending((BookItemSource x) => x.Chance).First()
				select best with { Min = g.Min((BookItemSource x) => x.Min), Max = g.Max((BookItemSource x) => x.Max) }).ToList();
		}
		ModContent.GetInstance<ModernRecipeBook>().Logger.Info($"World source audit ({timer.ElapsedMilliseconds} ms): {evaluated} tile frames read, {tiles.Sum((KeyValuePair<int, List<BookItemSource>> p) => p.Value.Count)} sources for {tiles.Count} items; {TileReadErrors} tiles unreadable" + (failed.Count > 0 ? ": " + string.Join(", ", failed.Take(12).Select((int f) => TileLoader.GetTile(f)?.FullName ?? (TileID.Search.TryGetName(f, out string n) ? n : f.ToString()))) : "") + ".");
		void Record(int t, int fx, int fy, int ground, Dictionary<int, (double Chance, int Min, int Max)> drops)
		{
			foreach (KeyValuePair<int, (double Chance, int Min, int Max)> drop in drops)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(drop.Key, out Item item) || item.IsAir)
				{
					continue;
				}
				// Only what the world makes or grows is a source: a placed chair giving back the chair is not, and
				// neither is decor that breaks into something else (piles, bottles, bricks).
				if (!Natural(t) && !IsTrunk(t) && (item.createTile == t || Category(t) != BookAcquisitionKind.Plant))
				{
					continue;
				}
				bool trunk = IsTrunk(t);
				int[] grounds = trunk ? new[] { ground } : Grounds(t, StyleOf(t, fx, fy));
				Add(new BookItemSource(Category(t), drop.Key)
				{
					Tile = t,
					FrameX = fx,
					FrameY = fy,
					Owner = Owner(t),
					Chance = (float)Math.Min(1.0, drop.Value.Chance),
					Min = drop.Value.Min,
					Max = drop.Value.Max,
					Grounds = grounds,
					Where = trunk ? TreeWhere(t, ground) : Where(t, grounds, StyleOf(t, fx, fy)),
					Note = drop.Key == ItemID.VineRope ? "needs:" + ItemID.CordageGuide : (drop.Key == ItemID.Seed ? "needs:" + ItemID.Blowpipe : Note(t, fx, fy, drop.Key)),
					NoteValue = PickPower(t)
				});
			}
		}
		static void Add(BookItemSource source)
		{
			if (!tiles.TryGetValue(source.Item, out var list))
			{
				list = tiles[source.Item] = new List<BookItemSource>();
			}
			list.Add(source);
		}
	}

	private static bool IsTrunk(int t)
	{
		return TileID.Sets.IsATreeTrunk[t] || t == TileID.PalmTree;
	}

	// Trees drop by the ground they stand on (WorldGen.GetTreeType; a mod's ground counts when it registered a
	// ModTree or ModPalmTree for it): palms on sand, the rest on grass; gem and mushroom trees do not care.
	private static IEnumerable<int> TrunkGrounds(int t, int[] grounds)
	{
		if (t == TileID.PalmTree)
		{
			return grounds.Where((int g) => g < TileID.Count ? WorldGen.GetTreeType(g) is TreeTypes.Palm or TreeTypes.PalmCorrupt or TreeTypes.PalmCrimson or TreeTypes.PalmHallowed : PlantLoader.Exists(TileID.PalmTree, g));
		}
		if (t == TileID.Trees || t == TileID.VanityTreeSakura || t == TileID.VanityTreeYellowWillow || t == TileID.TreeAsh)
		{
			return grounds.Where((int g) => g < TileID.Count ? WorldGen.GetTreeType(g) is not (TreeTypes.None or TreeTypes.Palm or TreeTypes.PalmCorrupt or TreeTypes.PalmCrimson or TreeTypes.PalmHallowed or TreeTypes.Mushroom) : PlantLoader.Exists(TileID.Trees, g));
		}
		return new int[] { TileID.Stone };
	}

	private static bool IsSapling(int t)
	{
		return TileID.Sets.TreeSapling[t] || t == TileID.GemSaplings || t == TileID.VanityTreeSakuraSaplings || t == TileID.VanityTreeWillowSaplings;
	}

	private static bool IsMultiTile(int t)
	{
		if (!Main.tileFrameImportant[t])
		{
			return false;
		}
		TileObjectData data = TileObjectData.GetTileData(t, 0);
		return TileID.Sets.IsMultitile[t] || (data != null && (data.Width > 1 || data.Height > 1));
	}

	// Every frame a 1×1 tile can show: one per style cell of its texture (TileObjectData's cell size, else
	// the 18-pixel grid of plants), so frame tests like "frameX == 144" in the drop code are all met.
	// A styled tile that items place has as many styles as its items (Tiles_227 holds more frames than dye plants).
	private static IEnumerable<(int X, int Y)> Frames(int t, List<Item> placers)
	{
		if (!Main.tileFrameImportant[t])
		{
			yield return (0, 0);
			yield break;
		}
		TileObjectData data = TileObjectData.GetTileData(t, 0);
		int stepX = Math.Max(2, data?.CoordinateFullWidth ?? 18);
		int stepY = Math.Max(2, data?.CoordinateFullHeight ?? 18);
		(int width, int height) = TextureSize(t);
		int columns = Math.Clamp(width / stepX, 1, 64);
		int rows = Math.Clamp(height / stepY, 1, 32);
		if (data != null && placers?.Count > 0)
		{
			int styles = placers.Max((Item i) => i.placeStyle) + 1;
			if (data.StyleHorizontal)
			{
				columns = Math.Min(columns, styles);
			}
			else
			{
				rows = Math.Min(rows, styles);
			}
		}
		for (int row = 0; row < rows; row++)
		{
			for (int column = 0; column < columns; column++)
			{
				yield return (column * stepX, row * stepY);
			}
		}
	}

	private static (int Width, int Height) TextureSize(int t)
	{
		Texture2D texture = TileTexture(t);
		return (texture.Width, texture.Height);
	}

	// The tile's texture, loaded now if the game has not needed it yet (vanilla and mod tiles alike).
	internal static Texture2D TileTexture(int t)
	{
		Asset<Texture2D> asset = TextureAssets.Tile[t];
		if (!asset.IsLoaded)
		{
			asset = t < TileID.Count ? Main.Assets.Request<Texture2D>(asset.Name, AssetRequestMode.ImmediateLoad) : ModContent.Request<Texture2D>(asset.Name, AssetRequestMode.ImmediateLoad);
		}
		return asset.Value;
	}

	private static Point StyleFrame(int t, int style)
	{
		TileObjectData data = TileObjectData.GetTileData(t, style);
		if (data == null)
		{
			return new Point(style * 18, 0);
		}
		int index = style * Math.Max(1, data.StyleMultiplier);
		int wrap = data.StyleWrapLimit;
		int column = wrap > 0 ? index % wrap : index;
		int row = wrap > 0 ? index / wrap * Math.Max(1, data.StyleLineSkip) : 0;
		return data.StyleHorizontal ? new Point(column * data.CoordinateFullWidth, row * data.CoordinateFullHeight) : new Point(row * data.CoordinateFullWidth, column * data.CoordinateFullHeight);
	}

	private static int StyleOf(int t, int fx, int fy)
	{
		TileObjectData data = TileObjectData.GetTileData(t, 0);
		if (data == null)
		{
			return fx / 18;
		}
		int column = fx / Math.Max(1, data.CoordinateFullWidth);
		int row = fy / Math.Max(1, data.CoordinateFullHeight);
		return data.StyleHorizontal ? column : row;
	}

	private static BookAcquisitionKind Category(int t)
	{
		if (IsTrunk(t))
		{
			return BookAcquisitionKind.Tree;
		}
		if (Main.tileAlch[t] || TileID.Sets.IsVine[t] || TileID.Sets.TileCutIgnore.Regrowth[t] || PlantGrounds.ContainsKey(t) || t is TileID.Cactus or TileID.LifeFruit or TileID.DyePlants or TileID.Sunflower or TileID.Bamboo or TileID.Pumpkins || (!Main.tileSolid[t] && TileObjectData.GetTileData(t, 0)?.AnchorValidTiles?.Length > 0))
		{
			return BookAcquisitionKind.Plant;
		}
		return Main.tileSolid[t] ? BookAcquisitionKind.Mining : BookAcquisitionKind.World;
	}

	// What the world generates or grows: ores and metal-detector finds, biome and conversion blocks, the
	// curated layer tables, and plants that need a particular ground.
	internal static bool Natural(int t)
	{
		if (TileID.Sets.Ore[t] || Main.tileOreFinderPriority[t] > 0 || BiomeKey(t) != null || VanillaLayers.ContainsKey(t) || CalamityBiome(t) != null)
		{
			return true;
		}
		if (TileID.Sets.Conversion.Stone[t] || TileID.Sets.Conversion.Sand[t] || TileID.Sets.Conversion.Ice[t] || TileID.Sets.Conversion.Snow[t] || TileID.Sets.Conversion.Dirt[t] || TileID.Sets.Conversion.Sandstone[t] || TileID.Sets.Conversion.HardenedSand[t] || TileID.Sets.Conversion.Moss[t] || TileID.Sets.Conversion.Thorn[t] || TileID.Sets.Conversion.Grass[t] || TileID.Sets.Conversion.JungleGrass[t] || TileID.Sets.Conversion.MushroomGrass[t])
		{
			return true;
		}
		return Category(t) == BookAcquisitionKind.Plant && Grounds(t, 0).Length > 0;
	}

	private static string Owner(int t)
	{
		return TileLoader.GetTile(t)?.Mod.Name ?? "Terraria";
	}

	// The pickaxe power a tile needs: ModTile.MinPick, or Player.GetPickaxeDamage's vanilla checks (Demonite and
	// Crimtane only below the surface, which is where most of it is; Dungeon bricks only off the entrance).
	internal static int PickPower(int t)
	{
		if (TileLoader.GetTile(t) is ModTile tile)
		{
			return tile.MinPick;
		}
		return t switch
		{
			TileID.Chlorophyte => 200,
			TileID.LihzahrdBrick or TileID.LihzahrdAltar => 210,
			TileID.Adamantite or TileID.Titanium => 150,
			TileID.Mythril or TileID.Orichalcum => 110,
			TileID.Cobalt or TileID.Palladium => 100,
			TileID.Ebonstone or TileID.Crimstone or TileID.Pearlstone or TileID.Hellstone => 65,
			TileID.Demonite or TileID.Crimtane or TileID.Obsidian => 55,
			TileID.Meteorite => 50,
			_ => 0
		};
	}

	// What a natural tile waits for, as a BookSteps term: "" none, "Hardmode", a boss, or the mechanical bosses'
	// words. With Calamity's Early Hardmode Progression Rework (on by default) the second and third ore tiers come
	// with the first and second mechanical boss and Hallowed Ore with the third (CalamityGlobalNPC.
	// SpawnMechBossHardmodeOres), and Astral Ore lands with the hardmode meteor but cannot be mined before Astrum
	// Deus (AstralOre.CanKillTile).
	internal static string TileGate(int t)
	{
		if (HardmodeTiles.Contains(t))
		{
			if (BookProgressionSources.CalamityRework())
			{
				if (t is TileID.Mythril or TileID.Orichalcum)
				{
					return "mechs:any";
				}
				if (t is TileID.Adamantite or TileID.Titanium)
				{
					return "mechs:two";
				}
			}
			return "Hardmode";
		}
		if (TileLoader.GetTile(t) is not ModTile tile || tile.Mod.Name != "CalamityMod")
		{
			return "";
		}
		return CalamityOreBosses.GetValueOrDefault(tile.Name, "");
	}

	// The ground a plant needs: WorldGen.PlantCheck's table, else the style's TileObjectData anchors (herbs).
	private static int[] Grounds(int t, int style)
	{
		if (PlantGrounds.TryGetValue(t, out int[] grounds))
		{
			return grounds;
		}
		// Grown herbs keep the sprout's per-style anchors (TileObjectData sub-tiles of ImmatureHerbs).
		int[] anchors = TileObjectData.GetTileData(t is TileID.MatureHerbs or TileID.BloomingHerbs ? TileID.ImmatureHerbs : t, Math.Max(0, style))?.AnchorValidTiles;
		return anchors?.Where((int a) => a >= 0 && a < TileLoader.TileCount).ToArray() ?? Array.Empty<int>();
	}

	// Herb seeds and bloom time: WorldGen.IsHarvestableHerbWithSeed (style 0 by day, 1 at night, 3 at night
	// in a blood moon or full moon, 4 in rain, 5 in the evening without rain; 2 and 6 only when fully grown).
	private static string Note(int t, int fx, int fy, int drop)
	{
		if (t == TileID.MatureHerbs || t == TileID.BloomingHerbs)
		{
			int style = fx / 18;
			return style switch
			{
				0 => "bloom:day",
				1 => "bloom:night",
				3 => "bloom:moon",
				4 => "bloom:rain",
				5 => "bloom:dusk",
				_ => "bloom:grown"
			};
		}
		if (IsTrunk(t) && fy >= 198 && drop == ItemID.Acorn)
		{
			return "treetop";
		}
		string gate = TileGate(t);
		return (gate.Length == 0 || gate == "Hardmode" || gate.StartsWith("mechs:", StringComparison.Ordinal)) ? gate : "boss:" + gate;
	}

	// Where -----------------------------------------------------------------------------------------

	internal const string Biome = "Bestiary_Biomes.";

	// TileID.Sets.*Biome: what the game counts the tile toward when it decides the player's biome.
	private static string BiomeKey(int t)
	{
		bool corrupt = TileID.Sets.CorruptBiome[t] > 0, crimson = TileID.Sets.CrimsonBiome[t] > 0, hallow = TileID.Sets.HallowBiome[t] > 0;
		bool snow = TileID.Sets.SnowBiome[t] > 0, sand = TileID.Sets.SandBiome[t] > 0;
		if (sand)
		{
			return corrupt ? "CorruptDesert" : (crimson ? "CrimsonDesert" : (hallow ? "HallowDesert" : "Desert"));
		}
		if (snow)
		{
			return corrupt ? "CorruptIce" : (crimson ? "CrimsonIce" : (hallow ? "HallowIce" : "Snow"));
		}
		if (corrupt)
		{
			return "TheCorruption";
		}
		if (crimson)
		{
			return "Crimson";
		}
		if (hallow)
		{
			return "TheHallow";
		}
		if (TileID.Sets.JungleBiome[t] > 0)
		{
			return "Jungle";
		}
		if (TileID.Sets.MushroomBiome[t] > 0)
		{
			return "UndergroundMushroom";
		}
		return TileID.Sets.DungeonBiome[t] > 0 ? "TheDungeon" : null;
	}

	// Grounds no biome set covers.
	private static readonly Dictionary<int, string> GroundBiomes = new Dictionary<int, string>
	{
		[TileID.Grass] = "Surface",
		[TileID.GolfGrass] = "Surface",
		[TileID.Dirt] = "Underground",
		[TileID.Stone] = "Underground",
		[TileID.Mud] = "UndergroundJungle",
		[TileID.Ash] = "TheUnderworld",
		[TileID.AshGrass] = "TheUnderworld"
	};

	// The tile's own biome or layer first, else the biomes of what it grows on.
	private static string[] Where(int t, int[] grounds, int style = 0)
	{
		if (t == TileID.ExposedGems)
		{
			return new[] { Biome + (style == 6 ? "UndergroundDesert" : "Caverns") };
		}
		string own = CalamityBiome(t) ?? (BiomeKey(t) is string key ? Biome + key : null);
		if (own != null)
		{
			return new[] { own };
		}
		if (VanillaLayers.TryGetValue(t, out string[] layers))
		{
			return layers.Select((string l) => Biome + l).ToArray();
		}
		string[] where = grounds.Select(GroundWhere).Where((string w) => w != null).Distinct().ToArray();
		if (where.Length == 0 && GroundBiomes.TryGetValue(t, out string ground))
		{
			return new[] { Biome + ground };
		}
		return where;
	}

	private static string GroundWhere(int g)
	{
		return CalamityBiome(g) ?? (BiomeKey(g) is string key ? Biome + key : (GroundBiomes.TryGetValue(g, out string ground) ? Biome + ground : null));
	}

	private static string[] TreeWhere(int t, int ground)
	{
		if (CalamityBiome(t) is string biome)
		{
			return new[] { biome };
		}
		if (t == TileID.PalmTree && ground == TileID.Sand)
		{
			return new[] { Biome + "Ocean", Biome + "Oasis" };
		}
		if (t >= TileID.TreeTopaz && t <= TileID.TreeAmber)
		{
			return new[] { Biome + "Caverns" };
		}
		if (t == TileID.MushroomTrees)
		{
			return new[] { Biome + "UndergroundMushroom" };
		}
		string where = GroundWhere(ground);
		return where != null ? new[] { where } : Array.Empty<string>();
	}

	// Vanilla tiles without a biome set: where world generation puts them (WorldGen passes "Shinies", "Gems",
	// "Gem Caves", "Random Gems", "Life Crystals", "Webs", "Spider Caves", "Underworld", "Silt", "Slush",
	// "Clay", Marble/Granite, the desert hive, dropMeteor, Liquid merges for obsidian, SmashAltar for the
	// hardmode ores, hardUpdateWorld for chlorophyte). Heights as the Bestiary names them.
	private static readonly Dictionary<int, string[]> VanillaLayers = new Dictionary<int, string[]>
	{
		[TileID.Copper] = new[] { "Surface", "Underground", "Caverns" },
		[TileID.Tin] = new[] { "Surface", "Underground", "Caverns" },
		[TileID.Iron] = new[] { "Surface", "Underground", "Caverns" },
		[TileID.Lead] = new[] { "Surface", "Underground", "Caverns" },
		[TileID.Silver] = new[] { "Underground", "Caverns", "Sky" },
		[TileID.Tungsten] = new[] { "Underground", "Caverns", "Sky" },
		[TileID.Gold] = new[] { "Caverns", "Sky" },
		[TileID.Platinum] = new[] { "Caverns", "Sky" },
		[TileID.Demonite] = new[] { "Caverns", "TheCorruption" },
		[TileID.Crimtane] = new[] { "Caverns" },
		[TileID.Meteorite] = new[] { "Meteor" },
		[TileID.Hellstone] = new[] { "TheUnderworld" },
		[TileID.Obsidian] = new[] { "Caverns", "TheUnderworld" },
		[TileID.Cobalt] = new[] { "Underground", "Caverns" },
		[TileID.Palladium] = new[] { "Underground", "Caverns" },
		[TileID.Mythril] = new[] { "Caverns" },
		[TileID.Orichalcum] = new[] { "Caverns" },
		[TileID.Adamantite] = new[] { "Caverns" },
		[TileID.Titanium] = new[] { "Caverns" },
		[TileID.Chlorophyte] = new[] { "UndergroundJungle" },
		[TileID.Sapphire] = new[] { "Underground", "Caverns" },
		[TileID.Ruby] = new[] { "Underground", "Caverns" },
		[TileID.Emerald] = new[] { "Underground", "Caverns" },
		[TileID.Topaz] = new[] { "Underground", "Caverns" },
		[TileID.Amethyst] = new[] { "Underground", "Caverns" },
		[TileID.Diamond] = new[] { "Underground", "Caverns" },
		[TileID.Heart] = new[] { "Underground", "Caverns" },
		[TileID.Cobweb] = new[] { "Underground", "Caverns", "SpiderNest" },
		[TileID.DesertFossil] = new[] { "UndergroundDesert" },
		[TileID.Dirt] = new[] { "Surface", "Underground" },
		[TileID.Stone] = new[] { "Underground", "Caverns" },
		[TileID.ClayBlock] = new[] { "Surface", "Underground" },
		[TileID.Mud] = new[] { "Jungle", "UndergroundJungle" },
		[TileID.Silt] = new[] { "Caverns" },
		[TileID.Slush] = new[] { "UndergroundSnow" },
		[TileID.Ash] = new[] { "TheUnderworld" },
		[TileID.Marble] = new[] { "Marble" },
		[TileID.Granite] = new[] { "Granite" },
		// Moss grows on cavern stone; crystal shards in the underground Hallow (hardmode); cacti in deserts;
		// bamboo by jungle water.
		[TileID.GreenMoss] = new[] { "Caverns" },
		[TileID.BrownMoss] = new[] { "Caverns" },
		[TileID.RedMoss] = new[] { "Caverns" },
		[TileID.BlueMoss] = new[] { "Caverns" },
		[TileID.PurpleMoss] = new[] { "Caverns" },
		[TileID.LavaMoss] = new[] { "Caverns" },
		[TileID.KryptonMoss] = new[] { "Caverns" },
		[TileID.XenonMoss] = new[] { "Caverns" },
		[TileID.ArgonMoss] = new[] { "Caverns" },
		[TileID.VioletMoss] = new[] { "Caverns" },
		[TileID.RainbowMoss] = new[] { "Caverns" },
		[TileID.Crystals] = new[] { "UndergroundHallow" },
		[TileID.Cactus] = new[] { "Desert" },
		[TileID.BeachPiles] = new[] { "Ocean" },
		// Vines hang from the grass of their biome.
		[TileID.Vines] = new[] { "Surface" },
		[TileID.VineFlowers] = new[] { "Surface" },
		[TileID.JungleVines] = new[] { "Jungle" },
		[TileID.HallowedVines] = new[] { "TheHallow" },
		[TileID.CrimsonVines] = new[] { "Crimson" },
		[TileID.CorruptVines] = new[] { "TheCorruption" },
		[TileID.AshVines] = new[] { "TheUnderworld" },
		// Only the mushroom-biome cattail drops anything (a Glowing Mushroom).
		[TileID.Cattail] = new[] { "UndergroundMushroom" },
		[TileID.Bamboo] = new[] { "Jungle" }
	};

	// Hardmode-only tiles of that table: altar smashing (or, with Calamity's default early-hardmode rework,
	// boss kills) spreads the ores, chlorophyte grows after the Wall of Flesh.
	private static readonly HashSet<int> HardmodeTiles = new HashSet<int> { TileID.Cobalt, TileID.Palladium, TileID.Mythril, TileID.Orichalcum, TileID.Adamantite, TileID.Titanium, TileID.Chlorophyte, TileID.Crystals };

	// Calamity 2.2.4 tiles by where they generate: the biome BiomeTileCounterSystem counts them toward, and
	// for the boss-kill ores the band CalamityUtils.SpawnOre fills (Cryogen, Plantera, Providence, Yharon,
	// the mechanical bosses, Moon Lord's planetoids; Aerialite on the floating islands).
	private static readonly Dictionary<string, string> CalamityTiles = Tiles(
		("CalamityMod/BrimstoneCragsBiome", "InfernalSuevite BrimstoneSlag ScorchedRemains CinderBlossomTallPlants LavaPistil SpineTree"),
		("CalamityMod/SulphurousSeaBiome", "SulphurousSand SulphurousSandstone HardenedSulphurousSandstone"),
		("CalamityMod/AbyssLayer1Biome", "SulphurousShale"),
		("CalamityMod/AbyssLayer2Biome", "AbyssGravel PlantyMush ScoriaOre"),
		("CalamityMod/AbyssLayer3Biome", "PyreMantle"),
		("CalamityMod/AbyssLayer4Biome", "Voidstone LumenylCrystals"),
		("CalamityMod/AstralInfectionBiome", "AstralSand AstralSandstone HardenedAstralSand CelestialRemains AstralIce AstralSnow AstralDirt AstralStone AstralGrass NovaeSlag AstralClay AstralOre"),
		("CalamityMod/SunkenSeaBiome", "EutrophicSand HardenedEutrophicSand Navystone SeaPrism SeaPrismCrystals MediumSeaPrismCrystal Runestone Dunesand AridSoil Limestone PolypSand Shellstone MossyStone Basalt VolcanicSand"),
		(Biome + "Sky", "AerialiteOre AerialiteOreDisenchanted ExodiumOre"),
		(Biome + "Caverns", "AuricOre PerennialOre"),
		(Biome + "UndergroundSnow", "CryonicOre"),
		(Biome + "UndergroundHallow", "HallowedOre"),
		(Biome + "UndergroundJungle", "UelibloomOre"));

	// The boss whose first kill spawns a Calamity ore (or, for Lumenyl, lets the crystals grow; Aerialite is
	// enchanted by the Hive Mind or the Perforators, whichever falls first; Hallowed Ore comes with the third
	// mechanical boss whichever it is; Astral Ore is there from hardmode but only breaks after Astrum Deus).
	private static readonly Dictionary<string, string> CalamityOreBosses = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["LumenylCrystals"] = "CalamityMod/Leviathan",
		["AerialiteOre"] = "CalamityMod/HiveMind;CalamityMod/PerforatorHive",
		["AstralOre"] = "CalamityMod/AstrumDeusHead",
		["AuricOre"] = "CalamityMod/Yharon",
		["CryonicOre"] = "CalamityMod/Cryogen",
		["ExodiumOre"] = "MoonLordCore",
		["HallowedOre"] = "mechs:all",
		["PerennialOre"] = "Plantera",
		["UelibloomOre"] = "CalamityMod/Providence"
	};

	private static Dictionary<string, string> Tiles(params (string Biome, string Names)[] groups)
	{
		Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach ((string biome, string names) in groups)
		{
			foreach (string name in names.Split(' '))
			{
				result[name] = biome;
			}
		}
		return result;
	}

	private static string CalamityBiome(int t)
	{
		return TileLoader.GetTile(t) is ModTile tile && tile.Mod.Name == "CalamityMod" && CalamityTiles.TryGetValue(tile.Name, out string biome) ? biome : null;
	}

	// Curated tile sources: multi-tile objects whose drop code is not callable.
	private static IEnumerable<BookItemSource> CuratedTileSources()
	{
		// WorldGen.CheckOrb: the first orb always gives the gun with 100 Musket Balls, later ones one of five.
		int[] shadowOrb = { ItemID.Musket, ItemID.Vilethorn, ItemID.BallOHurt, ItemID.ShadowOrb, ItemID.BandofStarpower };
		int[] crimsonHeart = { ItemID.TheUndertaker, ItemID.CrimsonRod, ItemID.TheRottedFork, ItemID.CrimsonHeart, ItemID.PanicNecklace };
		foreach (int item in shadowOrb)
		{
			yield return new BookItemSource(BookAcquisitionKind.World, item) { Tile = TileID.ShadowOrbs, Chance = 0.2f, Where = new[] { Biome + "TheCorruption" }, Note = "orb" };
		}
		foreach (int item in crimsonHeart)
		{
			yield return new BookItemSource(BookAcquisitionKind.World, item) { Tile = TileID.ShadowOrbs, FrameX = 36, Chance = 0.2f, Where = new[] { Biome + "Crimson" }, Note = "orb" };
		}
		yield return new BookItemSource(BookAcquisitionKind.World, ItemID.MusketBall) { Tile = TileID.ShadowOrbs, Chance = 0.2f, Min = 100, Max = 100, Where = new[] { Biome + "TheCorruption", Biome + "Crimson" }, Note = "orb" };
		// The statues world generation scatters underground (WorldGen.SetupStatueList, the "Statues" pass).
		if (GenVars.statueList == null)
		{
			WorldGen.SetupStatueList();
		}
		foreach (Terraria.DataStructures.Point16 statue in GenVars.statueList)
		{
			int item = TileLoader.GetItemDropFromTypeAndStyle(statue.X, statue.Y);
			if (item > 0)
			{
				Point frame = StyleFrame(statue.X, statue.Y);
				yield return new BookItemSource(BookAcquisitionKind.World, item) { Tile = statue.X, FrameX = frame.X, FrameY = frame.Y, Chance = 1f, Where = new[] { Biome + "Underground", Biome + "Caverns" } };
			}
		}
		// WorldGen.Check2x2: a natural Life Crystal (Heart tile) drops the item; the placeable one is decor.
		yield return new BookItemSource(BookAcquisitionKind.World, ItemID.LifeCrystal) { Tile = TileID.Heart, Chance = 1f, Where = new[] { Biome + "Underground", Biome + "Caverns" } };
		// WorldGen.CheckJunglePlant drops it; it grows once a mechanical boss is down (hardUpdateWorld).
		yield return new BookItemSource(BookAcquisitionKind.Plant, ItemID.LifeFruit) { Tile = TileID.LifeFruit, Chance = 1f, Where = new[] { Biome + "UndergroundJungle" }, Note = "mech" };
	}

	// Catalogue sources that need no world: fishing, shaken trees, falling stars ---------------------------

	internal static IEnumerable<BookItemSource> CatalogueSources()
	{
		foreach (BookItemSource source in Fishing())
		{
			yield return source;
		}
		foreach (BookItemSource source in Shaking())
		{
			yield return source;
		}
		// Falling stars (Projectile FallingStar) land as a Fallen Star on the surface at night.
		yield return new BookItemSource(BookAcquisitionKind.World, ItemID.FallenStar) { From = ItemID.FallenStar, Where = new[] { Biome + "Surface" }, Note = "starfall" };
	}

	// WorldGen.ShakeTree: the fruit and rare finds a tree of each kind drops when shaken.
	private static IEnumerable<BookItemSource> Shaking()
	{
		(string Where, int[] Items)[] table =
		{
			("Surface", new int[] { ItemID.Apple, ItemID.Peach, ItemID.Apricot, ItemID.Grapefruit, ItemID.Lemon, ItemID.LivingWoodWand, ItemID.LeafWand, ItemID.EucaluptusSap }),
			("Snow", new int[] { ItemID.Plum, ItemID.Cherry }),
			("Jungle", new int[] { ItemID.Mango, ItemID.Pineapple, ItemID.LivingMahoganyWand, ItemID.LivingMahoganyLeafWand }),
			("Ocean", new int[] { ItemID.Coconut, ItemID.Banana }),
			("TheCorruption", new int[] { ItemID.Elderberry, ItemID.BlackCurrant }),
			("TheHallow", new int[] { ItemID.Dragonfruit, ItemID.Starfruit }),
			("Crimson", new int[] { ItemID.BloodOrange, ItemID.Rambutan }),
			("TheUnderworld", new int[] { ItemID.Pomegranate, ItemID.SpicyPepper }),
			("UndergroundMushroom", new int[] { ItemID.MushroomGrassSeeds })
		};
		foreach ((string where, int[] items) in table)
		{
			foreach (int item in items)
			{
				yield return new BookItemSource(BookAcquisitionKind.Tree, item) { Tile = TileID.Trees, FrameX = 22, FrameY = 198, Where = new[] { Biome + where }, Note = "shake" };
			}
		}
		yield return new BookItemSource(BookAcquisitionKind.Tree, ItemID.Acorn) { Tile = TileID.Trees, FrameX = 22, FrameY = 198, Where = new[] { Biome + "Surface", Biome + "Snow", Biome + "TheHallow", Biome + "TheUnderworld" }, Note = "shake" };
		yield return new BookItemSource(BookAcquisitionKind.Tree, ItemID.RottenEgg) { Tile = TileID.Trees, FrameX = 22, FrameY = 198, Where = new[] { Biome + "Surface" }, Note = "shake:halloween" };
	}

	// Fishing: vanilla Projectile.FishingCheck_RollItemDrop and Calamity's CalamityPlayer.CatchFish are code,
	// not data; these tables are read from them (tier = the catch's rarity roll; heights: Sky, Surface,
	// Underground, Caverns). Quest fish come from the game's own quest list and the Angler's catch line.
	// "Name|where,where|tier|gate"; where "any", "lava", "honey" or Bestiary_Biomes keys / Calamity ModBiomes.
	private static readonly string[] VanillaFishing =
	{
		"Bass|any||", "Trout|Ocean||", "Salmon|Surface,Sky|common|", "Tuna|Ocean|common|", "RedSnapper|Ocean|common|", "Shrimp|Ocean|uncommon|",
		"Swordfish|Ocean|rare|", "PinkJellyfish|Ocean|rare|", "ReaverShark|Ocean|veryrare|", "SawtoothShark|Ocean|veryrare|",
		"NeonTetra|Jungle|common|", "DoubleCod|Jungle|uncommon|", "VariegatedLardfish|UndergroundJungle|uncommon|",
		"AtlanticCod|Snow|common|", "FrostMinnow|Snow|uncommon|", "FrostDaggerfish|Snow|common|", "SpecularFish|Underground,Caverns,UndergroundSnow||",
		"CrimsonTigerfish|Crimson|common|", "Hemopiranha|Crimson|uncommon|", "Ebonkoi|TheCorruption|uncommon|", "PurpleClubberfish|TheCorruption|rare|",
		"PrincessFish|TheHallow|uncommon|", "Prismite|TheHallow|rare|", "ChaosFish|UndergroundHallow|veryrare|",
		"Flounder|Desert,HallowDesert||", "RockLobster|Desert,HallowDesert||", "Oyster|Desert,HallowDesert|uncommon|",
		"Damselfish|Sky|uncommon|", "ArmoredCavefish|Underground,Caverns|uncommon|", "Stinkfish|Underground,Caverns|rare|", "Rockfish|Underground,Caverns|veryrare|",
		"GoldenCarp|Underground,Caverns|legendary|", "BlueJellyfish|Underground,Caverns|rare|", "GreenJellyfish|Underground,Caverns|rare|Hardmode",
		"Obsidifish|lava|rare|", "FlarefinKoi|lava|veryrare|", "DemonConch|lava|legendary|", "BottomlessLavaBucket|lava|legendary|", "LavaAbsorbantSponge|lava|legendary|", "ObsidianSwordfish|lava|legendary|Hardmode",
		"Honeyfin|honey|rare|", "BombFish|any|uncommon|", "FrogLeg|any|legendary|", "BalloonPufferfish|any|legendary|", "ZephyrFish|any|legendary|",
		"CombatBook|any|legendary|BloodMoon", "DreadoftheRedSea|any|legendary|BloodMoon", "AlchemyTable|TheDungeon|veryrare|boss:SkeletronHead",
		"Toxikarp|TheCorruption|legendary|Hardmode", "Bladetongue|Crimson|legendary|Hardmode", "CrystalSerpent|TheHallow|legendary|Hardmode", "LadyOfTheLake|TheHallow|legendary|Hardmode",
		"ScalyTruffle|CorruptIce,CrimsonIce,HallowIce|legendary|Hardmode",
		"OldShoe|any|junk|", "FishingSeaweed|any|junk|", "TinCan|any|junk|", "JojaCola|any|junk|",
		// The crate branch of the same method: biome crates are the rare roll in their biome, Wooden/Iron/Golden anywhere.
		"WoodenCrate|any|crate|", "IronCrate|any|crate|", "GoldenCrate|any|crate|", "WoodenCrateHard|any|crate|Hardmode", "IronCrateHard|any|crate|Hardmode", "GoldenCrateHard|any|crate|Hardmode",
		"DungeonFishingCrate|TheDungeon|crate|", "DungeonFishingCrateHard|TheDungeon|crate|Hardmode", "OceanCrate|Ocean|crate|", "OceanCrateHard|Ocean|crate|Hardmode",
		"CorruptFishingCrate|TheCorruption|crate|", "CorruptFishingCrateHard|TheCorruption|crate|Hardmode", "CrimsonFishingCrate|Crimson|crate|", "CrimsonFishingCrateHard|Crimson|crate|Hardmode",
		"HallowedFishingCrate|TheHallow|crate|", "HallowedFishingCrateHard|TheHallow|crate|Hardmode", "JungleFishingCrate|Jungle|crate|", "JungleFishingCrateHard|Jungle|crate|Hardmode",
		"FrozenCrate|Snow|crate|", "FrozenCrateHard|Snow|crate|Hardmode", "OasisCrate|Desert|crate|", "OasisCrateHard|Desert|crate|Hardmode",
		"FloatingIslandFishingCrate|Sky|crate|", "FloatingIslandFishingCrateHard|Sky|crate|Hardmode", "LavaCrate|lava|crate|", "LavaCrateHard|lava|crate|Hardmode"
	};

	private const string Crag = "CalamityMod/BrimstoneCragsBiome", Astral = "CalamityMod/AstralInfectionBiome", Sunken = "CalamityMod/SunkenSeaBiome", Sulphur = "CalamityMod/SulphurousSeaBiome";

	private const string Abyss = "CalamityMod/AbyssLayer2Biome,CalamityMod/AbyssLayer3Biome,CalamityMod/AbyssLayer4Biome";

	private static readonly string[] CalamityFishing =
	{
		"SlagCrate|" + Crag + "|crate|lava", "BrimstoneCrate|" + Crag + "|crate|lava,Hardmode", "DragoonDrizzlefish|" + Crag + "|legendary|lava", "CharredLasher|" + Crag + "|veryrare|lava",
		"Bloodfin|" + Crag + "|rare|lava,boss:CalamityMod/Providence", "CoastalDemonfish|" + Crag + "|rare|lava", "Shadowfish|" + Crag + "|rare|lava", "CragBullhead|" + Crag + "||lava",
		"MonolithCrate|" + Astral + "|crate|", "AstralCrate|" + Astral + "|crate|Hardmode", "PolarisParrotfish|" + Astral + "|legendary|", "GacruxianMollusk|" + Astral + "|legendary|", "UrsaSergeant|" + Astral + "|legendary|",
		"ArcturusAstroidean|" + Astral + "|veryrare|", "ProcyonidPrawn|" + Astral + "|uncommon|", "AldebaranAlewife|" + Astral + "|uncommon|", "TwinklingPollox|" + Astral + "||",
		"EutrophicCrate|" + Sunken + "|crate|", "PrismCrate|" + Sunken + "|crate|Hardmode", "RustedJingleBell|" + Sunken + "|legendary|", "SerpentsBite|" + Sunken + "|legendary|Hardmode",
		"GreenwaveLoach|" + Sunken + "|veryrare|", "SparklingEmpress|" + Sunken + "|veryrare|boss:CalamityMod/DesertScourgeHead", "SeaSpiritAmulet|" + Sunken + "|veryrare|boss:CalamityMod/DesertScourgeHead",
		"SunkenSailfish|" + Sunken + "|uncommon|", "Driftwood|" + Sunken + "||", "PrismaticGuppy|" + Sunken + "||",
		"SulphurousCrate|" + Sulphur + "|crate|", "HydrothermalCrate|" + Sulphur + "|crate|Hardmode", "AlluringBait|" + Sulphur + "|legendary|", "PlantyMush|" + Sulphur + "|common|",
		"Lionfish|" + Abyss + "|rare|boss:SkeletronHead", "HerringStaff|" + Abyss + "|rare|boss:SkeletronHead", "BallOFugu|" + Abyss + "|rare|boss:SkeletronHead", "BlackAnurian|" + Abyss + "|rare|boss:SkeletronHead",
		"Archerfish|" + Abyss + "|rare|boss:SkeletronHead", "AnechoicPlating|" + Abyss + "|rare|boss:SkeletronHead", "IronBoots|" + Abyss + "|rare|boss:SkeletronHead", "DepthCharm|" + Abyss + "|rare|boss:SkeletronHead",
		"StrangeOrb|" + Abyss + "|rare|boss:SkeletronHead", "TorrentialTear|" + Abyss + "|rare|boss:SkeletronHead",
		"Floodtide|any|legendary|boss:CalamityMod/Leviathan", "Gorecodile|Surface|uncommon|BloodMoon", "StuffedFish|Surface||Day", "GlimmeringGemfish|Caverns||", "FishofFlight|Sky||Hardmode",
		"EnchantedStarfish|Surface|uncommon|Night", "Shadowfish|Surface|uncommon|Night", "Spadefish|Underground|rare|",
		"WulfrumMetalScrap|any||rod:CalamityMod/WulfrumRod", "EnergyCore|any||rod:CalamityMod/WulfrumRod", "RoverDrive|any||rod:CalamityMod/WulfrumRod", "WulfrumBattery|any||rod:CalamityMod/WulfrumRod", "AbandonedWulfrumHelmet|any||rod:CalamityMod/WulfrumRod"
	};

	private static IEnumerable<BookItemSource> Fishing()
	{
		foreach (BookItemSource source in Table(VanillaFishing, null).Concat(ModLoader.HasMod("CalamityMod") ? Table(CalamityFishing, "CalamityMod") : Enumerable.Empty<BookItemSource>()))
		{
			yield return source;
		}
		// Quest fish: caught only while the Angler asks for it; the catch line ends his quest text (vanilla) or
		// comes from ModItem.AnglerQuestChat.
		foreach (int fish in Main.anglerQuestItemNetIDs.Distinct())
		{
			if (ContentSamples.ItemsByType.TryGetValue(fish, out Item item) && !item.IsAir)
			{
				yield return new BookItemSource(BookAcquisitionKind.Fishing, fish) { From = fish, Owner = item.ModItem?.Mod.Name ?? "Terraria", Note = "quest" };
			}
		}
		static IEnumerable<BookItemSource> Table(string[] rows, string mod)
		{
			foreach (string row in rows)
			{
				string[] f = row.Split('|');
				int type = mod == null ? (ItemID.Search.TryGetId(f[0], out int id) ? id : 0) : (ModContent.TryFind(mod, f[0], out ModItem modItem) ? modItem.Type : 0);
				if (type <= 0)
				{
					continue;
				}
				string[] where = f[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select((string w) => w is "any" or "lava" or "honey" ? w : (w.Contains('/') ? w : Biome + w)).ToArray();
				yield return new BookItemSource(BookAcquisitionKind.Fishing, type) { From = type, Owner = mod ?? "Terraria", Where = where, Note = (f[2] + "|" + f[3]).Trim('|') };
			}
		}
	}

	// The sandbox ---------------------------------------------------------------------------------------

	// Scratch tiles at the world's edge (unreachable, never drawn), the random generators and an inactive
	// player slot, borrowed for the reads and given back exactly as found.
	private sealed class Sandbox : IDisposable
	{
		private readonly (TileTypeData Type, WallTypeData Wall, LiquidData Liquid, TileWallWireStateData State, TileWallBrightnessInvisibilityData Paint)[] saved = new (TileTypeData, WallTypeData, LiquidData, TileWallWireStateData, TileWallBrightnessInvisibilityData)[3];

		private readonly UnifiedRandom rand;

		private readonly UnifiedRandom genRand;

		private readonly BranchRandom branches = new BranchRandom();

		private readonly int slot = -1;

		private readonly Player player;

		internal readonly HashSet<int> Failed = new HashSet<int>();

		internal Sandbox()
		{
			for (int i = 0; i < 3; i++)
			{
				Tile tile = Main.tile[ScratchX, ScratchY - 1 + i];
				saved[i] = (tile.Get<TileTypeData>(), tile.Get<WallTypeData>(), tile.Get<LiquidData>(), tile.Get<TileWallWireStateData>(), tile.Get<TileWallBrightnessInvisibilityData>());
			}
			rand = Main.rand;
			genRand = WorldGen.genRand;
			Main.rand = branches;
			WorldGen._genRand = branches;
			// The drop code asks the nearest player for its held item, cordage and axe: a stand-in with empty
			// hands next to the scratch tile answers, so the result does not depend on what the player carries.
			for (int i = Main.maxPlayers - 1; i > 0; i--)
			{
				if (i != Main.myPlayer && Main.player[i] != null && !Main.player[i].active)
				{
					slot = i;
					break;
				}
			}
			if (slot > 0)
			{
				player = Main.player[slot];
				Player standIn = new Player();
				standIn.active = true;
				standIn.dead = false;
				standIn.position = new Vector2(ScratchX * 16, ScratchY * 16);
				standIn.cordage = true;
				standIn.inventory[10] = new Item(ItemID.Blowpipe);
				Main.player[slot] = standIn;
			}
		}

		public void Dispose()
		{
			for (int i = 0; i < 3; i++)
			{
				Tile tile = Main.tile[ScratchX, ScratchY - 1 + i];
				tile.Get<TileTypeData>() = saved[i].Type;
				tile.Get<WallTypeData>() = saved[i].Wall;
				tile.Get<LiquidData>() = saved[i].Liquid;
				tile.Get<TileWallWireStateData>() = saved[i].State;
				tile.Get<TileWallBrightnessInvisibilityData>() = saved[i].Paint;
			}
			Main.rand = rand;
			WorldGen._genRand = genRand;
			if (slot > 0)
			{
				Main.player[slot] = player;
			}
		}

		// Item -> (chance per break, min stack, max stack) over every outcome of the random rolls.
		internal Dictionary<int, (double Chance, int Min, int Max)> Drops(int t, int fx, int fy, int ground, VanillaDrops vanilla)
		{
			Dictionary<int, (double Chance, int Min, int Max)> result = new Dictionary<int, (double, int, int)>();
			Set(0, 0, 0, 0);
			Set(1, t, fx, fy);
			Set(2, ground, 0, 0);
			Tile tile = Main.tile[ScratchX, ScratchY];
			ModTile modTile = TileLoader.GetTile(t);
			branches.Explore(delegate
			{
				Dictionary<int, int> run = new Dictionary<int, int>();
				if (modTile == null)
				{
					vanilla(ScratchX, ScratchY, tile, out int item, out int stack, out int secondary, out int secondaryStack, false);
					Count(item, stack);
					Count(secondary, secondaryStack);
				}
				else if (modTile.CanDrop(ScratchX, ScratchY))
				{
					foreach (Item drop in modTile.GetItemDrops(ScratchX, ScratchY) ?? Enumerable.Empty<Item>())
					{
						Count(drop?.type ?? 0, drop?.stack ?? 0);
					}
				}
				foreach (KeyValuePair<int, int> drop in run)
				{
					(double chance, int min, int max) = result.TryGetValue(drop.Key, out var old) ? old : (0.0, int.MaxValue, 0);
					result[drop.Key] = (chance + branches.Probability, Math.Min(min, drop.Value), Math.Max(max, drop.Value));
				}
				void Count(int type, int amount)
				{
					if (type > 0 && amount > 0)
					{
						run[type] = run.GetValueOrDefault(type) + amount;
					}
				}
			}, () => Failed.Add(t));
			return result;
		}

		private static void Set(int row, int type, int fx, int fy)
		{
			Tile tile = Main.tile[ScratchX, ScratchY - 1 + row];
			tile.Get<TileWallWireStateData>() = default;
			tile.Get<LiquidData>() = default;
			tile.Get<TileTypeData>().Type = (ushort)type;
			tile.HasTile = row != 0;
			tile.TileFrameX = (short)fx;
			tile.TileFrameY = (short)fy;
		}
	}

	// A random generator that replays a computation over the outcomes of its rolls, depth first: Next(n) up
	// to 16 tries every value, larger n tries 0 (the game's "1 in n") and n-1; ranges likewise. Each run's
	// probability is the product of the chosen outcomes'. After MaxDepth rolls (or MaxRuns runs) it stops
	// branching and takes the common outcome, so long chains of stack rolls stay cheap.
	private sealed class BranchRandom : UnifiedRandom
	{
		private const int MaxDepth = 10;

		private const int MaxRuns = 512;

		private readonly List<int> script = new List<int>();

		private readonly List<int> taken = new List<int>();

		private readonly List<int> options = new List<int>();

		internal double Probability { get; private set; } = 1.0;

		internal void Explore(Action run, Action failed)
		{
			Stack<List<int>> pending = new Stack<List<int>>();
			pending.Push(new List<int>());
			int runs = 0;
			while (pending.Count > 0 && runs++ < MaxRuns)
			{
				List<int> next = pending.Pop();
				script.Clear();
				script.AddRange(next);
				taken.Clear();
				options.Clear();
				Probability = 1.0;
				try
				{
					run();
				}
				catch (Exception)
				{
					failed();
					return;
				}
				for (int i = taken.Count - 1; i >= script.Count; i--)
				{
					for (int choice = options[i] - 1; choice >= 1; choice--)
					{
						List<int> branch = taken.Take(i).ToList();
						branch.Add(choice);
						pending.Push(branch);
					}
				}
			}
		}

		// Picks outcome index for this roll among count outcomes and multiplies in its probability.
		private int Pick(int count, Func<int, double> probability)
		{
			int depth = taken.Count;
			if (depth >= MaxDepth)
			{
				return -1;
			}
			int choice = depth < script.Count ? script[depth] : 0;
			taken.Add(choice);
			options.Add(count);
			Probability *= probability(choice);
			return choice;
		}

		public override int Next(int maxValue)
		{
			return Range(0, maxValue);
		}

		public override int Next(int minValue, int maxValue)
		{
			return Range(minValue, maxValue);
		}

		private int Range(int min, int max)
		{
			int span = max - min;
			if (span <= 1)
			{
				return min;
			}
			if (span <= 16)
			{
				int choice = Pick(span, (int _) => 1.0 / span);
				return choice < 0 ? max - 1 : min + choice;
			}
			int edge = Pick(2, (int c) => c == 0 ? 1.0 / span : (span - 1.0) / span);
			return edge == 0 ? min : max - 1;
		}

		public override int Next()
		{
			return Range(0, int.MaxValue);
		}

		public override double NextDouble()
		{
			int choice = Pick(2, (int _) => 0.5);
			return choice == 0 ? 0.0 : 0.999;
		}

		protected override double Sample()
		{
			return NextDouble();
		}
	}
}
