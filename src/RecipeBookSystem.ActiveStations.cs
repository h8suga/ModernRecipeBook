using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using static ModernRecipeBook.BookText;

namespace ModernRecipeBook;

// Active stations (docs/active-stations.md): a footer chip on the tabs that craft, with the strongest station in effect
// and how many things count; hovering it opens a panel of icons in three groups: placed nearby, in a Crafting
// Interface (near the player, or the one open through Portable Crafting Access), and the surroundings recipes ask for.
// It reads the very state BookCrafting.Collect checks recipes against (adjTile and the flags with the interface
// stations merged in by BookMagicStorage.BeginStations), so it cannot disagree with what the book calls craftable.
// A station that counts only through another (an Adamantite Forge is also a Furnace and a Hellforge) is drawn hollow
// and dimmed, with that station's small icon on its corner.
public sealed partial class RecipeBookSystem
{
	// Tile -1: a surrounding, Environment indexes the tables below. Via: the placed tile it counts through, or -1.
	private readonly record struct ActiveStation(int Tile, int Environment, bool Interface, int Via);

	// The recipe conditions vanilla gives the surroundings (water also counts at a Sink), each with the item drawn for it.
	private static readonly Condition[] EnvironmentConditions = new Condition[6] { Condition.NearWater, Condition.NearHoney, Condition.NearLava, Condition.NearShimmer, Condition.InSnow, Condition.InGraveyard };

	private static readonly int[] EnvironmentIcons = new int[6] { ItemID.WaterBucket, ItemID.HoneyBucket, ItemID.LavaBucket, ItemID.BottomlessShimmerBucket, ItemID.SnowBlock, ItemID.Tombstone };

	// Nearby, then the interfaces, then the surroundings; Starts[g]..Starts[g + 1] is group g.
	private static readonly List<ActiveStation> ActiveStations = new List<ActiveStation>();

	private static readonly int[] ActiveStationStarts = new int[4];

	private static int ActiveStationsKey = int.MinValue;

	private static int ActiveStationsVersion;

	private static int ChipTile = -1;

	private static string ActiveStationCount = "0";

	// Tile types inside Player.AdjTiles' box, the tiles the counted interfaces place, and a scratch adjacency.
	private static bool[] PlacedTiles = Array.Empty<bool>();

	private static readonly List<int> PlacedList = new List<int>();

	private static readonly List<int> InterfaceTiles = new List<int>();

	private static bool[] ImpliedScratch = Array.Empty<bool>();

	// Shown in the last frame drawn; the next update swallows clicks and the wheel over it.
	private static bool ActiveStationPanel;

	private static (int Index, int Version, int Culture) StationTipKey = (-1, -1, -1);

	private static string StationTipText = "";

	// As many as the largest group needs, five to eight: a short list gets a narrow panel.
	private static int StationColumns = 5;

	private const float StationCell = 36f;

	// Items, Recipes, Recent and Tracking: the tabs that craft or plan crafts.
	private static bool ActiveStationsTab => CurrentTab is 0 or 1 or 4 or 5;

	// BookCrafting.Collect, with the interface stations applied (`scoped`): player.adjTile and the flags are what the
	// recipes are checked against now. Cheap when nothing changed: a fold over the used stations' bits, the
	// surroundings, the interface items and the placed tiles around the player.
	internal static void CaptureActiveStations(Player player, bool scoped)
	{
		bool[] merged = player.adjTile;
		bool[] own = scoped ? BookMagicStorage.OwnTiles : merged;
		var flags = scoped ? BookMagicStorage.OwnFlags : (Water: player.adjWater, Lava: player.adjLava, Honey: player.adjHoney, Shimmer: player.adjShimmer, Alchemy: player.alchemyTable, Snow: player.ZoneSnow, Graveyard: player.ZoneGraveyard);
		ScanPlaced(player, merged.Length);
		InterfaceTiles.Clear();
		int key = RankedStations.Count * 2 + (scoped ? 1 : 0);
		List<List<Item>> lists = scoped ? BookSources.CraftStations : null;
		if (lists != null)
		{
			foreach (List<Item> list in lists)
			{
				foreach (Item item in list)
				{
					if (item == null || item.IsAir)
					{
						continue;
					}
					key = key * 31 + item.type;
					if (item.createTile >= 0 && item.createTile < merged.Length && !InterfaceTiles.Contains(item.createTile))
					{
						InterfaceTiles.Add(item.createTile);
					}
				}
			}
		}
		foreach (int tile in RankedStations)
		{
			key = key * 31 + (merged[tile] ? 1 : 0) + (own[tile] ? 2 : 0);
		}
		foreach (int tile in PlacedList)
		{
			key = key * 31 + tile;
		}
		for (int e = 0; e < EnvironmentConditions.Length; e++)
		{
			key = key * 31 + (EnvironmentConditions[e].IsMet() ? 1 : 0) + (OwnEnvironment(e, own, flags) ? 2 : 0);
		}
		if (key == ActiveStationsKey)
		{
			return;
		}
		ActiveStationsKey = key;
		ActiveStations.Clear();
		AddStations(player, merged, own, fromInterface: false);
		ActiveStationStarts[1] = ActiveStations.Count;
		AddStations(player, merged, own, fromInterface: true);
		ActiveStationStarts[2] = ActiveStations.Count;
		for (int e = 0; e < EnvironmentConditions.Length; e++)
		{
			if (EnvironmentConditions[e].IsMet())
			{
				ActiveStations.Add(new ActiveStation(-1, e, !OwnEnvironment(e, own, flags), -1));
			}
		}
		ActiveStationStarts[3] = ActiveStations.Count;
		int largest = 0;
		for (int group = 0; group < 3; group++)
		{
			largest = Math.Max(largest, ActiveStationStarts[group + 1] - ActiveStationStarts[group]);
		}
		StationColumns = Math.Clamp(largest, 5, 8);
		// The chip shows the strongest station that stands somewhere (StationRank: weakest to strongest).
		ChipTile = -1;
		foreach (ActiveStation station in ActiveStations)
		{
			if (station.Tile >= 0 && station.Via < 0 && (ChipTile < 0 || StationRank[station.Tile] > StationRank[ChipTile]))
			{
				ChipTile = station.Tile;
			}
		}
		ActiveStationCount = ActiveStations.Count.ToString();
		ActiveStationsVersion++;
	}

	private static bool OwnEnvironment(int environment, bool[] own, (bool Water, bool Lava, bool Honey, bool Shimmer, bool Alchemy, bool Snow, bool Graveyard) flags)
	{
		return environment switch
		{
			0 => flags.Water || own[TileID.Sinks],
			1 => flags.Honey,
			2 => flags.Lava,
			3 => flags.Shimmer,
			4 => flags.Snow,
			_ => flags.Graveyard,
		};
	}

	// The tile types in the box Player.AdjTiles reads (Artisan Loaf widens it). Only to tell a placed station from one
	// that counts through another: whether a station counts at all is always adjTile's answer.
	private static void ScanPlaced(Player player, int length)
	{
		if (PlacedTiles.Length != length)
		{
			PlacedTiles = new bool[length];
			ImpliedScratch = new bool[length];
			PlacedList.Clear();
		}
		foreach (int tile in PlacedList)
		{
			PlacedTiles[tile] = false;
		}
		PlacedList.Clear();
		int rx = player.ateArtisanBread ? 8 : 4;
		int ry = player.ateArtisanBread ? 7 : 3;
		int cx = (int)((player.position.X + player.width / 2) / 16f);
		int cy = (int)((player.position.Y + player.height) / 16f);
		for (int x = cx - rx; x <= cx + rx; x++)
		{
			for (int y = cy - ry; y < cy + ry; y++)
			{
				if (!WorldGen.InWorld(x, y))
				{
					continue;
				}
				Tile tile = Main.tile[x, y];
				if (tile.HasTile && tile.TileType < length && !PlacedTiles[tile.TileType])
				{
					PlacedTiles[tile.TileType] = true;
					PlacedList.Add(tile.TileType);
				}
			}
		}
	}

	// One group, the placed stations first, then those that only count through another, each in station order.
	private static void AddStations(Player player, bool[] merged, bool[] own, bool fromInterface)
	{
		int implied = -1;
		for (int pass = 0; pass < 2; pass++)
		{
			if (pass == 1)
			{
				implied = ActiveStations.Count;
			}
			foreach (int tile in RankedStations)
			{
				if (!merged[tile] || own[tile] == fromInterface)
				{
					continue;
				}
				if (Placed(tile, fromInterface) == (pass == 0))
				{
					ActiveStations.Add(new ActiveStation(tile, -1, fromInterface, -1));
				}
			}
		}
		if (implied == ActiveStations.Count)
		{
			return;
		}
		// What each placed tile counts as, by the crafting rule itself; the first that covers a station names it. The
		// interfaces' merge also runs over what stands nearby (MS lets a nearby Work Bench be a chair), so their group
		// asks the interface items first and then the placed tiles.
		bool[] saved = player.adjTile;
		player.adjTile = ImpliedScratch;
		try
		{
			implied = ResolveVia(player, fromInterface ? InterfaceTiles : PlacedList, implied, fromInterface, insert: true);
			if (fromInterface)
			{
				ResolveVia(player, PlacedList, implied, fromInterface, insert: false);
			}
		}
		finally
		{
			player.adjTile = saved;
		}
	}

	// player.adjTile is ImpliedScratch here. A used source no recipe names itself (the Alchemy Table only stands for a
	// Bottle and a Table) joins its group's placed stations when `insert`.
	private static int ResolveVia(Player player, List<int> sources, int implied, bool fromInterface, bool insert)
	{
		foreach (int source in sources)
		{
			Array.Clear(ImpliedScratch);
			BookMagicStorage.AddStation(player, ImpliedScratch, source);
			bool used = false;
			for (int i = implied; i < ActiveStations.Count; i++)
			{
				ActiveStation station = ActiveStations[i];
				if (station.Via < 0 && station.Tile != source && ImpliedScratch[station.Tile])
				{
					ActiveStations[i] = station with { Via = source };
					used = true;
				}
			}
			if (used && insert && !Listed(source))
			{
				ActiveStations.Insert(implied++, new ActiveStation(source, -1, fromInterface, -1));
			}
		}
		return implied;
	}

	private static bool Listed(int tile)
	{
		foreach (ActiveStation station in ActiveStations)
		{
			if (station.Tile == tile)
			{
				return true;
			}
		}
		return false;
	}

	private static bool Placed(int tile, bool fromInterface)
	{
		return fromInterface ? InterfaceTiles.Contains(tile) : PlacedTiles[tile];
	}

	private static Item ActiveStationIcon(ActiveStation station)
	{
		if (station.Tile < 0)
		{
			return ContentSamples.ItemsByType.TryGetValue(EnvironmentIcons[station.Environment], out Item icon) ? icon : null;
		}
		return StationItems.TryGetValue(station.Tile, out Item item) ? item : null;
	}

	private static string ActiveStationName(ActiveStation station)
	{
		return (station.Tile < 0) ? ConditionName(EnvironmentConditions[station.Environment]) : StationName(station.Tile);
	}

	// The interfaces' group is named after Magic Storage's own item.
	private static string InterfaceName => (StationItems.TryGetValue(BookMagicStorage.CraftingAccessTile, out Item item) && !string.IsNullOrWhiteSpace(item.Name)) ? item.Name : L("ActiveStations.CraftingInterface");

	private static string ActiveStationGroup(int group)
	{
		return group switch
		{
			0 => L("ActiveStations.Nearby"),
			1 => InterfaceName,
			_ => L("ActiveStations.Surroundings"),
		};
	}

	// Rebuilt only when the hovered icon, the list or the language changed.
	private static string ActiveStationTip(int index)
	{
		(int, int, int) key = (index, ActiveStationsVersion, Culture);
		if (StationTipKey == key)
		{
			return StationTipText;
		}
		StationTipKey = key;
		ActiveStation station = ActiveStations[index];
		string detail;
		if (station.Via >= 0)
		{
			detail = L("ActiveStations.CountsVia", StationName(station.Via));
		}
		else if (station.Tile >= 0)
		{
			detail = station.Interface ? L("ActiveStations.InTheCraftingInterface") : L("ActiveStations.PlacedNearby");
		}
		else if (station.Interface)
		{
			detail = L("ActiveStations.FromTheCraftingInterface");
		}
		else
		{
			detail = (station.Environment >= 4) ? L("ActiveStations.YouAreInThisBiome") : L("ActiveStations.NextToYou");
		}
		StationTipText = ActiveStationName(station) + "\n" + detail;
		return StationTipText;
	}

	// Left of the Help button, as wide as its count needs.
	private static BookRect ActiveStationChip
	{
		get
		{
			float width = 40f + TextWidth(ActiveStationCount, 0.8f);
			return new BookRect(BookGeometry.Help.X - 8f - width, 660f, width, 24f);
		}
	}

	private static int StationRowsOf(int count) => (count + StationColumns - 1) / StationColumns;

	private static (int Version, int Culture, float Scale) PanelWidthKey = (-1, -1, 0f);

	private static float PanelWidth;

	// The icon columns, or the title or a group's name if wider (up to eight columns' width), measured once per list.
	private static float ActiveStationPanelWidth()
	{
		(int, int, float) key = (ActiveStationsVersion, Culture, TextScale);
		if (PanelWidthKey == key)
		{
			return PanelWidth;
		}
		PanelWidthKey = key;
		float width = Math.Max(24f + StationColumns * StationCell - 4f, TextWidth(L("ActiveStations.ActiveStations"), 0.9f) + 88f);
		for (int group = 0; group < 3; group++)
		{
			if (ActiveStationStarts[group + 1] > ActiveStationStarts[group])
			{
				width = Math.Max(width, TextWidth(ActiveStationGroup(group).ToUpperInvariant(), 0.8f) + 40f);
			}
		}
		PanelWidth = Math.Min(width, 24f + 8 * StationCell - 4f);
		return PanelWidth;
	}

	// Above the chip, right edges aligned: a title row, then each group's label and icon rows.
	private static BookRect ActiveStationPanelRect
	{
		get
		{
			float height = 12f + 28f + 8f;
			if (ActiveStations.Count == 0)
			{
				height += 48f;
			}
			for (int group = 0; group < 3; group++)
			{
				int count = ActiveStationStarts[group + 1] - ActiveStationStarts[group];
				if (count > 0)
				{
					height += 26f + StationRowsOf(count) * StationCell;
				}
			}
			float width = ActiveStationPanelWidth();
			BookRect chip = ActiveStationChip;
			// ponytail: a panel taller than the book (over ~70 stations) runs past its top; page it if a pack ever gets there.
			return new BookRect(Math.Max(16f, chip.X + chip.Width - width), Math.Max(16f, chip.Y - 8f - height), width, height);
		}
	}

	// The panel stays while the mouse is on the chip, on the panel or in the gap between them.
	private static void DrawActiveStationChip(SpriteBatch sb)
	{
		BookRect chip = ActiveStationChip;
		bool overChip = !MenuOpen && !DraggingWindow && chip.Contains(Mouse.X, Mouse.Y);
		BookRect panel = ActiveStationPanelRect;
		float gapTop = panel.Y + panel.Height;
		bool overPanel = ActiveStationPanel && (panel.Contains(Mouse.X, Mouse.Y) || Hit(chip.X, gapTop, chip.Width, chip.Y - gapTop));
		ActiveStationPanel = overChip || (overPanel && !MenuOpen);
		bool none = ActiveStations.Count == 0;
		Box(sb, chip, ActiveStationPanel ? SelectedFill : Raised, border: true, 8f, ActiveStationPanel ? new Color?(Accent) : null);
		Item icon = (ChipTile >= 0 && StationItems.TryGetValue(ChipTile, out Item station)) ? station : ContentSamples.ItemsByType[ItemID.WorkBench];
		Icon(sb, icon, chip.X + 6f, chip.Y + 2f, 20f, tooltip: false, none ? 0.4f : 1f);
		CenterLabel(sb, ActiveStationCount, chip.X + 30f, chip.Y + 2f, chip.Width - 34f, chip.Height - 4f, none ? Muted : TextColor, 0.8f, rightAligned: false, leftAligned: true);
	}

	private static bool OverActiveStationPanel => ActiveStationPanel && ActiveStationsTab && ActiveStationPanelRect.Contains(Mouse.X, Mouse.Y);

	// A popover like the dropdowns: a shadow and the panel, no blur. Nothing under it hovers or shows a tooltip.
	private static void DrawActiveStationPanel(SpriteBatch sb)
	{
		BookRect panel = ActiveStationPanelRect;
		if (panel.Contains(Mouse.X, Mouse.Y))
		{
			PendingTip = null;
			if (Main.HoverItem != null && !Main.HoverItem.IsAir)
			{
				Main.HoverItem = new Item();
			}
			Main.hoverItemName = "";
		}
		Box(sb, panel.X, panel.Y + 4f, panel.Width, panel.Height, new Color(0, 0, 0, 110), border: false, 12f);
		Box(sb, panel, Panel, border: true, 12f);
		float x = panel.X + 12f;
		float y = panel.Y + 12f;
		CenterLabel(sb, L("ActiveStations.ActiveStations"), x + 4f, y, panel.Width - 88f, 24f, TextColor, 0.9f, rightAligned: false, leftAligned: true);
		CenterLabel(sb, ActiveStationCount, panel.X + panel.Width - 72f, y, 56f, 24f, Muted, 0.85f, rightAligned: true);
		y += 28f;
		if (ActiveStations.Count == 0)
		{
			CenterLabel(sb, L("ActiveStations.NoStationsInReach"), x + 4f, y + 2f, panel.Width - 32f, 20f, TextColor, 0.85f, rightAligned: false, leftAligned: true);
			string hint = BookMagicStorage.Installed ? L("ActiveStations.StandByAStationOrA") : L("ActiveStations.StandByACraftingStation");
			CenterLabel(sb, hint, x + 4f, y + 24f, panel.Width - 32f, 20f, Muted, 0.8f, rightAligned: false, leftAligned: true, fitWidth: true);
			return;
		}
		for (int group = 0; group < 3; group++)
		{
			int start = ActiveStationStarts[group];
			int end = ActiveStationStarts[group + 1];
			if (start == end)
			{
				continue;
			}
			y += 4f;
			SectionLabel(sb, ActiveStationGroup(group), x + 4f, y, panel.Width - 32f);
			y += 22f;
			for (int i = start; i < end; i++)
			{
				int slot = i - start;
				BookRect cell = new BookRect(x + slot % StationColumns * StationCell, y + slot / StationColumns * StationCell, StationCell - 4f, StationCell - 4f);
				DrawActiveStationCell(sb, i, cell);
			}
			y += StationRowsOf(end - start) * StationCell;
		}
	}

	private static void DrawActiveStationCell(SpriteBatch sb, int index, BookRect cell)
	{
		ActiveStation station = ActiveStations[index];
		bool via = station.Via >= 0;
		bool hover = cell.Contains(Mouse.X, Mouse.Y);
		// Placed: a filled tile. Through another: hollow, dimmed, with that station on its corner.
		Box(sb, cell, hover ? HoverFill : (via ? Panel : Raised), border: true, 6f);
		Item icon = ActiveStationIcon(station);
		if (icon != null)
		{
			Icon(sb, icon, cell.X + 4f, cell.Y + 4f, 24f, tooltip: false, via ? 0.4f : 1f);
		}
		else
		{
			// A station no item places (the altars): its initial.
			string name = ActiveStationName(station);
			CenterLabel(sb, (name.Length > 0) ? name.Substring(0, 1) : "?", cell.X, cell.Y + 4f, cell.Width, cell.Height - 8f, via ? Muted : TextColor, 0.9f);
		}
		if (via && StationItems.TryGetValue(station.Via, out Item source))
		{
			Icon(sb, source, cell.X + cell.Width - 15f, cell.Y + cell.Height - 15f, 13f, tooltip: false);
		}
		if (hover)
		{
			MouseTip(ActiveStationTip(index));
		}
	}

	// Stand: one capture as Collect makes it, with the inputs as they are (a=false) or with a forced rebuild (a=true).
	// stand.ps1 c bench p=ModernRecipeBook.RecipeBookSystem.ActiveStationsBench a=false n=1000
	internal static void ActiveStationsBench(bool rebuild)
	{
		Player player = Main.LocalPlayer;
		bool scoped = BookMagicStorage.BeginStations(player, BookSources.CraftStations);
		try
		{
			if (rebuild)
			{
				ActiveStationsKey = int.MinValue;
			}
			CaptureActiveStations(player, scoped);
		}
		finally
		{
			if (scoped)
			{
				BookMagicStorage.EndStations(player);
			}
		}
	}

	// Stand: the list as drawn, and whether every recipe the book calls craftable has its stations and surroundings in
	// it. stand.ps1 c call p=ModernRecipeBook.RecipeBookSystem.ActiveStationsAudit
	internal static string ActiveStationsAudit()
	{
		StringBuilder text = new StringBuilder();
		for (int group = 0; group < 3; group++)
		{
			text.Append(ActiveStationGroup(group)).Append(": ");
			for (int i = ActiveStationStarts[group]; i < ActiveStationStarts[group + 1]; i++)
			{
				ActiveStation station = ActiveStations[i];
				text.Append(ActiveStationName(station));
				if (station.Via >= 0)
				{
					text.Append(" (via ").Append(StationName(station.Via)).Append(')');
				}
				if (station.Tile < 0 && station.Interface)
				{
					text.Append(" (interface)");
				}
				text.Append(", ");
			}
			text.Append("| ");
		}
		int craftable = 0;
		int missing = 0;
		string first = "";
		for (int r = 0; r < Recipe.numRecipes; r++)
		{
			Recipe recipe = Main.recipe[r];
			if (!BookCrafting.IsAvailable(recipe))
			{
				continue;
			}
			craftable++;
			foreach (int tile in recipe.requiredTile)
			{
				if (tile >= 0 && !ActiveStations.Exists((ActiveStation s) => s.Tile == tile))
				{
					missing++;
					first = (first.Length > 0) ? first : (recipe.createItem.Name + " needs " + StationName(tile));
				}
			}
			foreach (Condition condition in recipe.Conditions)
			{
				int e = Array.IndexOf(EnvironmentConditions, condition);
				if (e >= 0 && !ActiveStations.Exists((ActiveStation s) => s.Tile < 0 && s.Environment == e))
				{
					missing++;
					first = (first.Length > 0) ? first : (recipe.createItem.Name + " needs " + ConditionName(condition));
				}
			}
		}
		return text.Append("craftable=").Append(craftable).Append(" missing=").Append(missing).Append(' ').Append(first).ToString();
	}
}
