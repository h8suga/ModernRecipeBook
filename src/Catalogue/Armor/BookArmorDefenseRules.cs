using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernRecipeBook.Catalogue.Armor;

internal static class BookArmorDefenseRules
{
	internal static long SumParts(IEnumerable<int> defense)
	{
		return defense.Sum((Func<int, long>)((int value) => value));
	}

	internal static int VanillaSetBonus(int head, int body, int legs)
	{
		switch (head)
		{
		case 52:
			if (body != 32 || legs != 31)
			{
				break;
			}
			goto IL_0155;
		case 53:
			if (body != 33 || legs != 32)
			{
				break;
			}
			goto IL_0155;
		case 54:
			if (body != 34 || legs != 33)
			{
				break;
			}
			goto IL_0155;
		case 55:
			if (body != 35 || legs != 34)
			{
				break;
			}
			goto IL_0155;
		case 71:
			if (body != 47 || legs != 43)
			{
				break;
			}
			goto IL_0155;
		case 166:
			if (body != 173 || legs != 108)
			{
				break;
			}
			goto IL_0155;
		case 167:
			if (body != 174 || legs != 109)
			{
				break;
			}
			goto IL_0155;
		case 1:
			if (body != 1 || legs != 1)
			{
				break;
			}
			goto IL_0159;
		case 2:
		case 72:
			if (body != 2 || legs != 2)
			{
				break;
			}
			goto IL_0159;
		case 47:
			if (body != 28 || legs != 27)
			{
				break;
			}
			goto IL_0159;
		case 3:
			if (body != 3 || legs != 3)
			{
				break;
			}
			goto IL_015d;
		case 4:
		case 73:
			if (body != 4 || legs != 4)
			{
				break;
			}
			goto IL_015d;
		case 48:
			if (body != 29 || legs != 28)
			{
				break;
			}
			goto IL_015d;
		case 49:
			if (body != 30 || legs != 29)
			{
				break;
			}
			goto IL_015d;
		case 50:
			{
				if (body != 31 || legs != 30)
				{
					break;
				}
				return 4;
			}
			IL_0159:
			return 2;
			IL_015d:
			return 3;
			IL_0155:
			return 1;
		}
		return 0;
	}
}
