using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.UI.Book;

// Station filter (Items and Recipes tabs): the player unticks the crafting stations he lacks and what only they make
// goes. An item stays while one of its recipes needs none of them; an item without a recipe never goes. A recipe
// goes when any station it needs is unticked; a merged variant shares its stations, so it counts as one recipe. The
// menu offers the stations of what the other filters list (unticked ones always), with how many of those need each.
// Unticked stations are kept per tab by stable keys (Terraria/<id>, or the mod tile's full name), so they survive
// changes to the mod list.
public sealed partial class RecipeBookSystem
{
	// Per item type: its recipes' distinct station sets (an empty set is a by-hand recipe); null when it has none.
	private static int[][][] ItemStationSets = Array.Empty<int[][]>();

	// Per item type: every station one of its recipes needs (the menu's counts).
	private static int[][] ItemStationUnion = Array.Empty<int[]>();

	// Per recipe index: the stations it needs.
	private static int[][] RecipeStationSet = Array.Empty<int[]>();

	private static readonly Dictionary<string, int> StationByKey = new Dictionary<string, int>(StringComparer.Ordinal);

	// The open tab's unticked stations: keys, sorted; those that resolve to a loaded tile are marked in ExcludedTile.
	private static string[] ExcludedStations = Array.Empty<string>();

	private static bool[] ExcludedTile = Array.Empty<bool>();

	private static int ExcludedTileCount;

	// Listed but for the station filter, and how many of them it hid.
	private static readonly List<int> StationCandidates = new List<int>();

	private static int StationCandidatesVersion;

	private static int StationHidden;

	private static bool StationMenu;

	private static int StationOffset;

	// The keyboard's row in the menu (0 = Show all), -1 before the first arrow.
	private static int StationFocus = -1;

	private const int StationVisibleRows = 10;

	private const int MaxExcludedStations = 64;

	private static readonly List<(int Tile, int Count)> StationRows = new List<(int, int)>();

	private static (int Version, int Culture, int Excluded) StationRowsKey = (-1, -1, -1);

	internal static bool StationMenuOpen => Open && StationMenu;

	// Items (0) and Recipes (1). Recent lists recipes too, but only what was just crafted, so the stations are at hand.
	private static bool StationFilterTab => CurrentTab is 0 or 1;

	private static bool StationFilterActive => StationFilterTab && ExcludedTileCount > 0;

	private static string StationKey(int tile)
	{
		return (tile >= TileID.Count && TileLoader.GetTile(tile) is ModTile modTile) ? modTile.FullName : ("Terraria/" + tile);
	}

	// Built with the recipe index (Rebuild): the sets, then the keys of every station a recipe needs.
	private static void BuildStationIndex()
	{
		RecipeStationSet = new int[Recipe.numRecipes][];
		foreach (int index in All)
		{
			RecipeStationSet[index] = Main.recipe[index].requiredTile.Where((int tile) => tile >= 0).Distinct().OrderBy((int tile) => tile).ToArray();
		}
		int count = ItemLoader.ItemCount;
		ItemStationSets = new int[count][][];
		ItemStationUnion = new int[count][];
		foreach (KeyValuePair<int, List<int>> pair in RecipesByItem)
		{
			if (pair.Key <= 0 || pair.Key >= count)
			{
				continue;
			}
			List<int[]> sets = new List<int[]>();
			foreach (int index in pair.Value)
			{
				int[] set = RecipeStationSet[index];
				if (!sets.Any((int[] known) => known.AsSpan().SequenceEqual(set)))
				{
					sets.Add(set);
				}
			}
			ItemStationSets[pair.Key] = sets.ToArray();
			ItemStationUnion[pair.Key] = sets.SelectMany((int[] set) => set).Distinct().ToArray();
		}
		StationByKey.Clear();
		List<int> used = All.SelectMany((int index) => RecipeStationSet[index]).Distinct().ToList();
		foreach (int tile in used)
		{
			StationByKey[StationKey(tile)] = tile;
		}
		ExcludedTile = new bool[TileLoader.TileCount];
		ResolveExcludedStations();
		RankStations(used);
		SortRankScratch = new int[Math.Max(count, Recipe.numRecipes)];
	}

	// Station strength for the Station sort: 0 is by hand, then every used station from weakest to strongest, by the
	// stage of the item that places it, then that item's rarity, then its value. A station no item places (the altars)
	// takes the earliest stage of what its recipes make.
	private static int[] StationRank = Array.Empty<int>();

	private static int[] SortRankScratch = Array.Empty<int>();

	// No recipe for Station, no such number for a category sort.
	private const int NoRank = int.MaxValue;

	private static readonly List<int> RankedStations = new List<int>();

	private static readonly Dictionary<int, (int Stage, int Rarity, int Makes, long Value)> StationStrengths = new Dictionary<int, (int, int, int, long)>();

	private static void RankStations(List<int> used)
	{
		StationRank = new int[TileLoader.TileCount];
		StationStrengths.Clear();
		foreach (int tile in used)
		{
			StationStrengths[tile] = StationStrength(tile);
		}
		RankedStations.Clear();
		RankedStations.AddRange(used.OrderBy((int tile) => StationStrengths[tile]).ThenBy((int tile) => tile));
		for (int i = 0; i < RankedStations.Count; i++)
		{
			StationRank[RankedStations[i]] = i + 1;
		}
	}

	// The placing item's stage and rarity; then the median rarity of what the station makes (an anvil and a Hellforge
	// are both common and cheap, but the Hellforge makes Hellstone), then the item's value. A station no item places is
	// found in the world (the altars): it closes its stage.
	private static (int Stage, int Rarity, int Makes, long Value) StationStrength(int tile)
	{
		if (StationItems.TryGetValue(tile, out var item))
		{
			BookStage stage = BookProgression.ItemStage(item.type);
			return ((int)((stage == BookStage.Unknown) ? EarliestResultStage(tile) : stage), item.rare, MedianResultRarity(tile), item.value);
		}
		return ((int)EarliestResultStage(tile), int.MaxValue, 0, 0L);
	}

	private static int MedianResultRarity(int tile)
	{
		List<int> rarities = new List<int>();
		foreach (int index in All)
		{
			if (Array.IndexOf(RecipeStationSet[index], tile) >= 0)
			{
				rarities.Add(Main.recipe[index].createItem.rare);
			}
		}
		rarities.Sort();
		return (rarities.Count == 0) ? 0 : rarities[rarities.Count / 2];
	}

	private static BookStage EarliestResultStage(int tile)
	{
		BookStage best = BookStage.Unknown;
		foreach (int index in All)
		{
			if (Array.IndexOf(RecipeStationSet[index], tile) < 0)
			{
				continue;
			}
			BookStage stage = BookProgression.ItemStage(Main.recipe[index].createItem.type);
			if (stage != BookStage.Unknown && (best == BookStage.Unknown || stage < best))
			{
				best = stage;
			}
		}
		return (best == BookStage.Unknown) ? BookStage.PreHardmode : best;
	}

	private static int SetRank(int[] set)
	{
		int rank = 0;
		foreach (int tile in set)
		{
			rank = Math.Max(rank, StationRank[tile]);
		}
		return rank;
	}

	// The easiest way to make it: the smallest, over its recipes the station filter leaves, of the strongest station
	// each needs. An item with none sorts after every crafted one.
	private static int StationSortRank(int index)
	{
		if (!Catalogue)
		{
			int[] needs = ((uint)index < (uint)RecipeStationSet.Length) ? RecipeStationSet[index] : null;
			return (needs == null) ? NoRank : SetRank(needs);
		}
		int[][] sets = ((uint)index < (uint)ItemStationSets.Length) ? ItemStationSets[index] : null;
		int best = NoRank;
		if (sets != null)
		{
			foreach (int[] set in sets)
			{
				if (ExcludedTileCount == 0 || !NeedsExcluded(set, ExcludedTile))
				{
					best = Math.Min(best, SetRank(set));
				}
			}
		}
		return best;
	}

	// Station and the category numbers (PrepareRanks): low to high or high to low; what has no recipe or no such number
	// goes last both ways; the name breaks ties in both directions.
	private static int CompareRank(int left, int right)
	{
		int a = ((uint)left < (uint)SortRankScratch.Length) ? SortRankScratch[left] : NoRank;
		int b = ((uint)right < (uint)SortRankScratch.Length) ? SortRankScratch[right] : NoRank;
		if ((a == NoRank) != (b == NoRank))
		{
			return (a == NoRank) ? 1 : (-1);
		}
		int comparison = SortPass.Descending ? b.CompareTo(a) : a.CompareTo(b);
		if (comparison == 0)
		{
			comparison = string.Compare(Result(left).Name, Result(right).Name, StringComparison.CurrentCultureIgnoreCase);
		}
		return (comparison != 0) ? comparison : left.CompareTo(right);
	}

	// Stand: the stations from weakest to strongest with what ranked them.
	internal static string StationRanking()
	{
		return string.Join("; ", RankedStations.Select((int tile) => StationRank[tile] + " " + StationName(tile) + " " + StationStrengths[tile]));
	}

	private static void SetExcludedStations(IEnumerable<string> keys)
	{
		ExcludedStations = (keys ?? Array.Empty<string>()).Where((string key) => !string.IsNullOrEmpty(key)).Distinct(StringComparer.Ordinal).OrderBy((string key) => key, StringComparer.Ordinal).Take(MaxExcludedStations).ToArray();
		ResolveExcludedStations();
	}

	private static void ResolveExcludedStations()
	{
		Array.Clear(ExcludedTile);
		ExcludedTileCount = 0;
		foreach (string key in ExcludedStations)
		{
			if (StationByKey.TryGetValue(key, out var tile) && tile < ExcludedTile.Length && !ExcludedTile[tile])
			{
				ExcludedTile[tile] = true;
				ExcludedTileCount++;
			}
		}
	}

	// `index` is what the tab lists: an item type on Items, a recipe index on Recipes.
	private static bool StationAllowed(int index)
	{
		if (Catalogue)
		{
			return (uint)index >= (uint)ItemStationSets.Length || KeepsARecipe(ItemStationSets[index], ExcludedTile);
		}
		return (uint)index >= (uint)RecipeStationSet.Length || RecipeStationSet[index] == null || !NeedsExcluded(RecipeStationSet[index], ExcludedTile);
	}

	// An item stays while one of its recipes needs no excluded station; one without recipes (null) always stays.
	private static bool KeepsARecipe(int[][] sets, bool[] excluded)
	{
		if (sets == null)
		{
			return true;
		}
		foreach (int[] set in sets)
		{
			if (!NeedsExcluded(set, excluded))
			{
				return true;
			}
		}
		return false;
	}

	private static bool NeedsExcluded(int[] set, bool[] excluded)
	{
		foreach (int tile in set)
		{
			if (excluded[tile])
			{
				return true;
			}
		}
		return false;
	}

	// The menu's rows: the stations of the listed items (or recipes) with how many of them need each, the excluded ones
	// even at 0; by count, then name. Rebuilt only when the list or the language changed.
	private static List<(int Tile, int Count)> StationList()
	{
		(int, int, int) key = (StationCandidatesVersion, Culture, ExcludedTileCount);
		if (StationRowsKey == key)
		{
			return StationRows;
		}
		StationRowsKey = key;
		Dictionary<int, int> counts = new Dictionary<int, int>();
		foreach (int index in StationCandidates)
		{
			int[] used = Catalogue ? (((uint)index < (uint)ItemStationUnion.Length) ? ItemStationUnion[index] : null) : (((uint)index < (uint)RecipeStationSet.Length) ? RecipeStationSet[index] : null);
			if (used == null)
			{
				continue;
			}
			foreach (int tile in used)
			{
				counts[tile] = counts.GetValueOrDefault(tile) + 1;
			}
		}
		for (int tile = 0; tile < ExcludedTile.Length; tile++)
		{
			if (ExcludedTile[tile] && !counts.ContainsKey(tile))
			{
				counts[tile] = 0;
			}
		}
		StationRows.Clear();
		StationRows.AddRange(from pair in counts
			orderby pair.Value descending, StationName(pair.Key)
			select (pair.Key, pair.Value));
		return StationRows;
	}

	private static string StationLabel
	{
		get
		{
			int n = ExcludedTileCount;
			if (n == 0)
			{
				return L("Stations.Stations");
			}
			return L("Stations.WithoutStations", n);
		}
	}

	private static string StationButtonTip()
	{
		if (ExcludedTileCount == 0)
		{
			return L("Stations.Stations") + "\n" + L("Stations.UntickTheStationsYouLackWhat");
		}
		List<string> names = new List<string>();
		for (int tile = 0; tile < ExcludedTile.Length; tile++)
		{
			if (ExcludedTile[tile])
			{
				names.Add(StationName(tile));
			}
		}
		names.Sort(StringComparer.CurrentCultureIgnoreCase);
		// One station a line: a comma list ran past the tooltip's width and broke mid-name.
		string shown = string.Join("\n", names.Take(6)) + ((names.Count > 6) ? ("\n+" + (names.Count - 6)) : "");
		return StationLabel + ":\n" + shown + "\n" + L("Stations.CtrlBackspaceResetsFilters");
	}

	private static BookRect StationMenuPanel
	{
		get
		{
			BookRect anchor = BookGeometry.StationFilter;
			float width = 340f;
			float height = 8f + 28f + StationVisibleRows * 32f + 28f;
			float x = Math.Clamp(anchor.X + anchor.Width - width, 16f, 1120f - 16f - width);
			return new BookRect(x, anchor.Y + anchor.Height + 4f, width, height);
		}
	}

	private static BookRect StationOption(int visibleRow)
	{
		BookRect panel = StationMenuPanel;
		return new BookRect(panel.X + 4f, panel.Y + 36f + visibleRow * 32f, panel.Width - 16f, 32f);
	}

	// All rows: 0 is Show all, then the stations.
	private static int StationRowCount => StationList().Count + 1;

	private static int StationRowAt()
	{
		for (int k = 0; k < StationVisibleRows; k++)
		{
			int row = StationOffset + k;
			if (row >= StationRowCount)
			{
				break;
			}
			if (StationOption(k).Contains(Mouse.X, Mouse.Y))
			{
				return row;
			}
		}
		return -1;
	}

	private static void ActivateStationRow(int row)
	{
		if (row == 0)
		{
			if (ExcludedTileCount == 0 && ExcludedStations.Length == 0)
			{
				return;
			}
			SetExcludedStations(Array.Empty<string>());
		}
		else
		{
			List<(int Tile, int Count)> rows = StationList();
			if (row - 1 >= rows.Count)
			{
				return;
			}
			string key = StationKey(rows[row - 1].Tile);
			SetExcludedStations(ExcludedStations.Contains(key) ? ExcludedStations.Where((string k) => k != key) : ExcludedStations.Append(key));
		}
		Page = 0;
		ApplyFilters();
	}

	private static void OpenStationMenu()
	{
		SetTyping(value: false);
		ModMenu = (SortMenu = (StageMenu = false));
		StationMenu = true;
		StationOffset = 0;
		StationFocus = -1;
	}

	// The menu's keyboard: Up/Down move the focus (it starts on the first station), Enter or Space ticks it; Esc and the
	// inventory key close the menu as the top layer (the book's Esc chain).
	private static bool HandleStationMenuKeys(KeyboardState keys)
	{
		int step = (Press(Keys.Down) ? 1 : 0) - (Press(Keys.Up) ? 1 : 0);
		if (step != 0)
		{
			StationFocus = (StationFocus < 0) ? Math.Min(1, StationRowCount - 1) : Math.Clamp(StationFocus + step, 0, StationRowCount - 1);
			if (StationFocus < StationOffset)
			{
				StationOffset = StationFocus;
			}
			else if (StationFocus >= StationOffset + StationVisibleRows)
			{
				StationOffset = StationFocus - StationVisibleRows + 1;
			}
			return true;
		}
		if ((Press(Keys.Enter) || Press(Keys.Space)) && StationFocus >= 0)
		{
			ActivateStationRow(StationFocus);
			return true;
		}
		return false;
		bool Press(Keys key)
		{
			return keys.IsKeyDown(key) && !LastKeys.IsKeyDown(key);
		}
	}

	private static void DrawStationButton(SpriteBatch sb)
	{
		BookRect rect = BookGeometry.StationFilter;
		bool active = ExcludedTileCount > 0;
		IconButtonFrame(sb, rect, active || StationMenu);
		if (ContentSamples.ItemsByType.TryGetValue(ItemID.IronAnvil, out var anvil))
		{
			Icon(sb, anvil, rect.X + 6f, rect.Y + 6f, 24f, tooltip: false);
		}
		if (active)
		{
			// How many are unticked, on the corner like a notification badge.
			string badge = ExcludedTileCount.ToString();
			// Light text on a dark fill: the book's text has a dark outline, so dark digits would blur into a blob.
			float width = Math.Max(20f, TextWidth(badge, 0.8f) + 10f);
			float x = rect.X + rect.Width - width + 7f;
			float y = rect.Y - 7f;
			Box(sb, x, y, width, 20f, SelectedFill, border: true, 10f, Accent, 1.5f);
			CenterLabel(sb, badge, x, y + 1f, width, 18f, TextColor, 0.8f);
		}
		if (!MenuOpen && rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(StationButtonTip());
		}
	}

	private static void DrawStationMenu(SpriteBatch sb)
	{
		BookRect panel = StationMenuPanel;
		if (panel.Contains(Mouse.X, Mouse.Y))
		{
			PendingTip = null;
			Main.HoverItem = new Item();
			Main.hoverItemName = "";
		}
		List<(int Tile, int Count)> rows = StationList();
		int total = rows.Count + 1;
		StationOffset = Math.Clamp(StationOffset, 0, Math.Max(0, total - StationVisibleRows));
		Box(sb, panel.X, panel.Y + 4f, panel.Width, panel.Height, new Color(0, 0, 0, 110), border: false, 12f);
		Box(sb, panel, Panel, border: true, 12f);
		SectionLabel(sb, StationLabel, panel.X + 16f, panel.Y + 12f, panel.Width - 32f);
		int hovered = StationRowAt();
		if (hovered >= 0)
		{
			StationFocus = hovered;
		}
		for (int k = 0; k < StationVisibleRows; k++)
		{
			int row = StationOffset + k;
			if (row >= total)
			{
				break;
			}
			BookRect rect = StationOption(k);
			if (row == StationFocus)
			{
				Box(sb, rect, HoverFill, border: false, 6f);
			}
			if (row == 0)
			{
				bool any = ExcludedTileCount > 0;
				CenterLabel(sb, L("Stations.ShowAll"), rect.X + 40f, rect.Y + 4f, rect.Width - 52f, rect.Height - 8f, any ? Accent : Muted, 0.9f, rightAligned: false, leftAligned: true);
				if (row == hovered)
				{
					MouseTip(any ? L("Stations.TickEveryStationAgain") : L("Stations.EveryStationIsTicked"));
				}
				continue;
			}
			(int tile, int count) = rows[row - 1];
			bool excluded = ExcludedTile[tile];
			// Ticked or crossed out: the mark, the colour and a line through the name, never colour alone.
			float boxX = rect.X + 12f;
			float boxY = rect.Y + 8f;
			if (excluded)
			{
				Box(sb, boxX, boxY, 16f, 16f, FieldFill, border: true, 4f, Bad);
				Cross(sb, boxX + 8f, boxY + 8f, 3.5f, Bad);
			}
			else
			{
				Box(sb, boxX, boxY, 16f, 16f, SelectedFill, border: true, 4f, Accent);
				Line(sb, boxX + 4f, boxY + 8.5f, boxX + 7f, boxY + 11.5f, Accent);
				Line(sb, boxX + 7f, boxY + 11.5f, boxX + 12.5f, boxY + 5f, Accent);
			}
			if (StationItems.TryGetValue(tile, out var station))
			{
				Icon(sb, station, rect.X + 36f, rect.Y + 4f, 24f, tooltip: false);
			}
			string name = StationName(tile);
			float nameX = rect.X + 68f;
			float nameWidth = rect.Width - 68f - 48f;
			CenterLabel(sb, name, nameX, rect.Y + 4f, nameWidth, rect.Height - 8f, excluded ? Muted : TextColor, 0.9f, rightAligned: false, leftAligned: true);
			if (excluded)
			{
				Line(sb, nameX, rect.Y + rect.Height / 2f, nameX + Math.Min(nameWidth, TextWidth(name, 0.9f)), rect.Y + rect.Height / 2f, Muted, 1.5f);
			}
			CenterLabel(sb, Count(count), rect.X + rect.Width - 44f, rect.Y + 4f, 36f, rect.Height - 8f, Muted, 0.8f, rightAligned: true);
			if (row == hovered)
			{
				string listed = L(Catalogue ? "Stations.ListedItemsMadeHere" : "Stations.ListedRecipesMadeHere", Count(count));
				MouseTip(name + "\n" + listed + "\n" + (excluded ? L("Stations.ClickShowAgain") : L("Stations.ClickHideWhatOnlyItMakes")));
			}
		}
		if (total > StationVisibleRows)
		{
			float track = StationVisibleRows * 32f;
			float thumb = Math.Max(24f, track * StationVisibleRows / total);
			float x = panel.X + panel.Width - 10f;
			Box(sb, x, panel.Y + 36f, 3f, track, Raised, border: false, 1f);
			Box(sb, x, panel.Y + 36f + (track - thumb) * StationOffset / (total - StationVisibleRows), 3f, thumb, Accent, border: false, 1f);
		}
		CenterLabel(sb, L("Stations.ArrowsAndEnterTickEscClose"), panel.X + 16f, panel.Y + panel.Height - 26f, panel.Width - 32f, 20f, Muted, 0.75f, rightAligned: false, leftAligned: true);
	}

	// Stand: items whose every recipe needs the tile, and items that have one recipe there and one elsewhere.
	internal static string StationProbe(int tile)
	{
		List<string> only = new List<string>();
		List<string> also = new List<string>();
		for (int type = 1; type < ItemStationSets.Length; type++)
		{
			int[][] sets = ItemStationSets[type];
			if (sets == null || !sets.Any((int[] set) => set.Contains(tile)))
			{
				continue;
			}
			(sets.All((int[] set) => set.Contains(tile)) ? only : also).Add(type + ":" + Lang.GetItemNameValue(type));
		}
		return "only=" + only.Count + " [" + string.Join(", ", only.Take(4)) + "] also=" + also.Count + " [" + string.Join(", ", also.Take(4)) + "] key=" + StationKey(tile) + " name=" + StationName(tile);
	}

	// Stand: the rule on made-up sets (index 1 and 2 are stations, 2 excluded).
	internal static string StationSelfCheck()
	{
		bool[] excluded = new bool[4] { false, false, true, false };
		List<string> failures = new List<string>();
		if (!KeepsARecipe(null, excluded))
		{
			failures.Add("no recipe stays");
		}
		if (!KeepsARecipe(new int[2][] { new int[1] { 2 }, Array.Empty<int>() }, excluded))
		{
			failures.Add("a by-hand recipe keeps it");
		}
		if (KeepsARecipe(new int[1][] { new int[2] { 1, 2 } }, excluded))
		{
			failures.Add("one excluded station in a set drops it");
		}
		if (!KeepsARecipe(new int[2][] { new int[1] { 2 }, new int[1] { 1 } }, excluded))
		{
			failures.Add("another station's recipe keeps it");
		}
		if (KeepsARecipe(Array.Empty<int[]>(), excluded) || NeedsExcluded(new int[1] { 3 }, excluded))
		{
			failures.Add("edges");
		}
		return (failures.Count == 0) ? "ok" : string.Join("; ", failures);
	}
}
