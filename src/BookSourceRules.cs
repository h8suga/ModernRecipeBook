using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ModernRecipeBook;

// Pure rules of the nearby-storage feature (docs/nearby-storage.md): what is in range, which bank a tile opens, how
// many inventory slots incoming stacks need, the Russian plural. The stand runs the self-check:
// stand.ps1 c call p=ModernRecipeBook.BookSourceRules.SelfCheck
internal static class BookSourceRules
{
	// Pull and deposit validation on the server allow this much more than the radius: the player keeps moving while
	// the request travels (about 6 ticks at 20 px per tick).
	internal const int SlackTiles = 8;

	// Chests are 2x2, dressers 3x2; the centre is what vanilla quick stack measures from (Chest.PutItemInNearbyChest).
	internal static Vector2 ChestCenter(int x, int y, bool dresser)
	{
		return new Vector2(x * 16 + (dresser ? 24 : 16), y * 16 + 16);
	}

	// The 2x2 Magic Storage tiles and every tile of a bank are measured from their centre.
	internal static Vector2 TileCenter(int x, int y, int width = 1, int height = 1)
	{
		return new Vector2(x * 16 + width * 8, y * 16 + height * 8);
	}

	internal static bool InRange(Vector2 player, Vector2 point, int tiles)
	{
		return Vector2.DistanceSquared(player, point) <= (float)tiles * tiles * 256f;
	}

	// Bank tiles and the player.chest value of the bank they open (Player.QuickStackAllChests).
	internal static int BankOf(int tileType)
	{
		return tileType switch
		{
			29 => -2,
			97 => -3,
			463 => -4,
			491 => -5,
			_ => 0,
		};
	}

	// 1 сундук, 2 сундука, 5 сундуков, 11 сундуков, 21 сундук.
	internal static string Plural(int n, string one, string few, string many)
	{
		int last = n % 10;
		bool teens = n % 100 >= 11 && n % 100 <= 14;
		return (last == 1 && !teens) ? one : ((last >= 2 && last <= 4 && !teens) ? few : many);
	}

	internal static string Count(int n, bool ru, string enOne, string enMany, string ruOne, string ruFew, string ruMany)
	{
		return n + " " + (ru ? Plural(n, ruOne, ruFew, ruMany) : ((n == 1) ? enOne : enMany));
	}

	internal readonly record struct Slot(int Type, int Prefix, int Stack, int MaxStack);

	internal readonly record struct Incoming(int Type, int Prefix, long Amount, int MaxStack);

	// How many more empty slots the incoming stacks need than the inventory has. Each kind first fills what its own
	// partial stacks can still hold; the rest needs whole slots. Conservative: only slots 0..49 count, not the ammo
	// and coin slots or the Void Vault.
	internal static int Shortage(IReadOnlyList<Slot> inventory, IReadOnlyList<Incoming> incoming)
	{
		Dictionary<(int, int), long> room = new Dictionary<(int, int), long>();
		int empty = 0;
		foreach (Slot slot in inventory)
		{
			if (slot.Type <= 0 || slot.Stack <= 0)
			{
				empty++;
			}
			else if (slot.Stack < slot.MaxStack)
			{
				room[(slot.Type, slot.Prefix)] = room.GetValueOrDefault((slot.Type, slot.Prefix)) + slot.MaxStack - slot.Stack;
			}
		}
		long needed = 0;
		foreach (Incoming item in incoming)
		{
			if (item.Type <= 0 || item.Amount <= 0)
			{
				continue;
			}
			long rest = item.Amount;
			long free = room.GetValueOrDefault((item.Type, item.Prefix));
			long used = Math.Min(free, rest);
			room[(item.Type, item.Prefix)] = free - used;
			rest -= used;
			if (rest > 0)
			{
				int max = Math.Max(1, item.MaxStack);
				long slots = (rest + max - 1) / max;
				needed += slots;
				// The new stacks' unused part takes later items of the same kind.
				room[(item.Type, item.Prefix)] = room.GetValueOrDefault((item.Type, item.Prefix)) + slots * max - rest;
			}
		}
		return (int)Math.Clamp(needed - empty, 0L, int.MaxValue);
	}

	// A left click on a chest slot holding `slot` of the cursor's kind (Terraria.UI/ItemSlot.cs:696-739): with the item
	// unstackable or either side full the two swap, otherwise the cursor tops the slot up. Returns what the cursor holds
	// afterwards; an empty cursor (0) picks the slot up.
	internal static int CursorAfterLeftClick(int slot, int cursor, int maxStack)
	{
		if (cursor == 0 || slot == 0 || maxStack <= 1 || slot >= maxStack || cursor >= maxStack)
		{
			return slot;
		}
		return cursor - Math.Min(cursor, maxStack - slot);
	}

	internal static string SelfCheck()
	{
		List<string> failures = new List<string>();
		void Expect(bool condition, string name)
		{
			if (!condition)
			{
				failures.Add(name);
			}
		}
		Vector2 player = new Vector2(1000f, 1000f);
		Expect(InRange(player, player + new Vector2(30 * 16, 0f), 30), "range: exactly the radius is in");
		Expect(!InRange(player, player + new Vector2(30 * 16 + 1, 0f), 30), "range: one pixel past is out");
		Expect(InRange(player, player + new Vector2(18 * 16, 24 * 16), 30), "range: a diagonal of 30 tiles is in");
		Expect(ChestCenter(10, 20, dresser: false) == new Vector2(176f, 336f), "centre: chest");
		Expect(ChestCenter(10, 20, dresser: true) == new Vector2(184f, 336f), "centre: dresser");
		Expect(BankOf(29) == -2 && BankOf(97) == -3 && BankOf(463) == -4 && BankOf(491) == -5 && BankOf(21) == 0, "banks");
		Expect(Plural(1, "a", "b", "c") == "a" && Plural(2, "a", "b", "c") == "b" && Plural(5, "a", "b", "c") == "c", "plural 1/2/5");
		Expect(Plural(11, "a", "b", "c") == "c" && Plural(12, "a", "b", "c") == "c" && Plural(21, "a", "b", "c") == "a" && Plural(22, "a", "b", "c") == "b" && Plural(111, "a", "b", "c") == "c", "plural 11/12/21/22/111");
		Expect(Count(1, ru: false, "chest", "chests", "", "", "") == "1 chest" && Count(7, ru: true, "", "", "сундук", "сундука", "сундуков") == "7 сундуков", "count text");
		Expect(CursorAfterLeftClick(750, 0, 9999) == 750, "click: an empty cursor picks the slot up");
		Expect(CursorAfterLeftClick(9999, 10, 9999) == 9999, "click: a full slot swaps, the cursor ends full");
		Expect(CursorAfterLeftClick(50, 10, 9999) == 0, "click: a partial slot takes the whole cursor");
		Expect(CursorAfterLeftClick(9990, 20, 9999) == 11, "click: the slot fills, the rest stays on the cursor");
		Expect(CursorAfterLeftClick(50, 9999, 9999) == 50, "click: a full cursor swaps");
		Expect(CursorAfterLeftClick(1, 1, 1) == 1 && CursorAfterLeftClick(0, 5, 99) == 0, "click: unstackable, empty slot");

		// Layers: inventory first, then banks, then remote.
		Dictionary<int, long>[] plan = BookBatchPlanner.PlanLayers(new long[1] { 10 }, new IReadOnlyCollection<int>[1] { new int[1] { 9 } }, new IReadOnlyDictionary<int, long>[3]
		{
			new Dictionary<int, long> { [9] = 3 },
			new Dictionary<int, long> { [9] = 2 },
			new Dictionary<int, long> { [9] = 100 }
		});
		Expect(plan != null && plan[0][9] == 3 && plan[1][9] == 2 && plan[2][9] == 5, "plan: layers in order");
		// A needs wood or ebonwood, B needs wood only. The inventory's 5 wood must go to B, so A comes from remote
		// ebonwood; a greedy split would give the wood to A and fail B.
		plan = BookBatchPlanner.PlanLayers(new long[2] { 5, 5 }, new IReadOnlyCollection<int>[2]
		{
			new int[2] { 9, 619 },
			new int[1] { 9 }
		}, new IReadOnlyDictionary<int, long>[2]
		{
			new Dictionary<int, long> { [9] = 5 },
			new Dictionary<int, long> { [619] = 10 }
		});
		Expect(plan != null && plan[0].GetValueOrDefault(9) == 5 && plan[1].GetValueOrDefault(619) == 5 && plan[1].GetValueOrDefault(9) == 0, "plan: overlap routed through the later layer");
		Expect(BookBatchPlanner.PlanLayers(new long[1] { 10 }, new IReadOnlyCollection<int>[1] { new int[1] { 9 } }, new IReadOnlyDictionary<int, long>[1] { new Dictionary<int, long> { [9] = 9 } }) == null, "plan: short is null");
		plan = BookBatchPlanner.PlanLayers(new long[1] { 4 }, new IReadOnlyCollection<int>[1] { new int[1] { 9 } }, new IReadOnlyDictionary<int, long>[2]
		{
			new Dictionary<int, long> { [9] = 10 },
			new Dictionary<int, long> { [9] = long.MaxValue }
		});
		Expect(plan != null && plan[0][9] == 4 && plan[1].Count == 0, "plan: nothing pulled when local is enough");

		// Room: partial stacks first, then whole slots.
		Slot[] inventory = new Slot[3]
		{
			new Slot(9, 0, 9990, 9999),
			new Slot(0, 0, 0, 0),
			new Slot(2, 0, 9999, 9999)
		};
		Expect(Shortage(inventory, new Incoming[1] { new Incoming(9, 0, 9L, 9999) }) == 0, "room: fits the partial stack");
		Expect(Shortage(inventory, new Incoming[1] { new Incoming(9, 0, 10L, 9999) }) == 0, "room: one new slot");
		Expect(Shortage(inventory, new Incoming[2]
		{
			new Incoming(9, 0, 10L, 9999),
			new Incoming(3, 0, 1L, 9999)
		}) == 1, "room: two kinds, one empty slot");
		Expect(Shortage(inventory, new Incoming[1] { new Incoming(4, 0, 5L, 1) }) == 4, "room: unstackable results need a slot each");
		Expect(Shortage(inventory, new Incoming[2]
		{
			new Incoming(5, 0, 3L, 9999),
			new Incoming(5, 0, 4L, 9999)
		}) == 0, "room: a new stack takes later items of its kind");
		return (failures.Count == 0) ? "ok" : string.Join("; ", failures);
	}
}
