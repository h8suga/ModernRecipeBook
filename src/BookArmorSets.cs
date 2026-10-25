using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook;

internal static class BookArmorSets
{
	internal sealed record Set(int[][] Parts, bool Modded, int[][] Combinations);

	private static readonly Dictionary<int, Set> sets = new Dictionary<int, Set>();

	internal static Set Get(int type)
	{
		if (!sets.TryGetValue(type, out var set))
		{
			return null;
		}
		return set;
	}

	internal static void Clear()
	{
		sets.Clear();
	}

	internal static int Slot(Item item)
	{
		if (item.headSlot < 0)
		{
			if (item.bodySlot < 0)
			{
				if (item.legSlot < 0)
				{
					return -1;
				}
				return 2;
			}
			return 1;
		}
		return 0;
	}

	internal static void Rebuild()
	{
		sets.Clear();
		Dictionary<string, List<Item>[]> groups = new Dictionary<string, List<Item>[]>();
		foreach (Item item in ContentSamples.ItemsByType.Values.OrderBy((Item i) => i.type))
		{
			if (item == null || item.IsAir || item.vanity)
			{
				continue;
			}
			int slot = Slot(item);
			if (slot < 0)
			{
				continue;
			}
			string family = BookArmorSetRules.Family(item.ModItem?.Name ?? ItemID.Search.GetName(item.type) ?? "", slot, item.ModItem == null);
			if (family.Length != 0)
			{
				string key = (item.ModItem?.Mod.Name ?? "Terraria") + ":" + family;
				if (!groups.TryGetValue(key, out var parts))
				{
					List<Item>[] array = new List<Item>[3]
					{
						new List<Item>(),
						new List<Item>(),
						new List<Item>()
					};
					parts = array;
					groups[key] = array;
				}
				parts[slot].Add(item);
			}
		}
		foreach (List<Item>[] parts2 in groups.Values)
		{
			if (parts2.Any((List<Item> p) => p.Count == 0))
			{
				continue;
			}
			bool modded = parts2[0][0].ModItem != null;
			List<int[]> combinations = new List<int[]>();
			if (modded)
			{
				HashSet<int>[] accepted = new HashSet<int>[3]
				{
					new HashSet<int>(),
					new HashSet<int>(),
					new HashSet<int>()
				};
				int attempts = 0;
				foreach (Item head in parts2[0])
				{
					foreach (Item body in parts2[1])
					{
						foreach (Item legs in parts2[2])
						{
							if (++attempts > 512)
							{
								continue;
							}
							try
							{
								Item h = head.Clone();
								Item b = body.Clone();
								Item l = legs.Clone();
								ModItem modItem = h.ModItem;
								if (modItem != null && modItem.IsArmorSet(h, b, l))
								{
									goto IL_02aa;
								}
								ModItem modItem2 = b.ModItem;
								if (modItem2 != null && modItem2.IsArmorSet(h, b, l))
								{
									goto IL_02aa;
								}
								ModItem modItem3 = l.ModItem;
								if (modItem3 != null && modItem3.IsArmorSet(h, b, l))
								{
									goto IL_02aa;
								}
								goto end_IL_023b;
								IL_02aa:
								combinations.Add(new int[3] { head.type, body.type, legs.type });
								accepted[0].Add(head.type);
								accepted[1].Add(body.type);
								accepted[2].Add(legs.type);
								end_IL_023b:;
							}
							catch
							{
							}
						}
					}
				}
				if (accepted.Any((HashSet<int> p) => p.Count == 0))
				{
					continue;
				}
				int n;
				for (n = 0; n < 3; n++)
				{
					parts2[n].RemoveAll((Item i) => !accepted[n].Contains(i.type));
				}
			}
			Set set = new Set(parts2.Select((List<Item> p) => p.Select((Item i) => i.type).ToArray()).ToArray(), modded, combinations.ToArray());
			foreach (int type in set.Parts.SelectMany((int[] p) => p))
			{
				sets[type] = set;
			}
		}
	}
}
