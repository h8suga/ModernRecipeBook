# Category sorts (branch `feature/search-and-craftable`)

Owner: «для отдельных категорий разные сортировки: для брони по защите, для оружия по урону, для зелий по тому, сколько восстанавливает, и т.п. Сам придумай как лучше». Code: `RecipeBookSystem.Sorts.cs`; the menu is in `DrawSortMenu` and `BookGeometry.SortMenuPanel`.

## What the list offers

- **Every list** (Items, Recipes, Storage):
  - Name, ID and Mod;
  - Station on Items and Recipes;
  - Rarity and Value.
- **The listed category adds its own sorts:**

  | Group | Categories | Sorts |
  |---|---|---|
  | Weapons | All weapons, Swords, Yoyos, Bows, Guns, Rocket launchers, Minions, Whips, Sentries | Damage, Speed, Crit chance, Knockback |
  | Ammo | All ammo, Arrows, Bullets, Rockets | Damage, Knockback |
  | Armor | All armor, Helmets, Chest armor, Leg armor | Defense |
  | Accessories | Accessories | Defense |
  | Wings | Wings | Flight time |
  | Potions and food | All potions, Healing potions, Mana potions, Buff potions, Food and drinks | Heals life, Heals mana, Duration |
  | Tools | All tools, Pickaxes, Axes, Hammers | Pickaxe power, Axe power, Hammer power |
  | Fishing rods | Fishing rods | Fishing power |
  | Bait | Bait | Bait power |

- **A category without a group** (All items, Hooks, Materials…) takes the group that more than half the listed entries have. Each item's group comes from its categories, with wings before accessories.
  - Any class button on All items lists mostly weapons, so it offers the weapon sorts.
  - A search for "sword" does the same.
  - The plain All items list has no majority and offers only the general sorts.
- **No damage per second.** Multi-projectile weapons make a rough DPS misleading. Speed means nothing for ammo, so ammo does not offer it.

## Order and values

- Each sort comes in both directions, as Station does: "Damage: lowest first" / "Damage: highest first".
- **The closed dropdown** shows the field and an arrow: "Damage ↓" / «Урон ↓». ↓ means the highest (fastest, longest) first.
  - A label a little too long ("Knockback ↓", «Редкость ↓», «Скорость ↓») shrinks to fit instead of being cut.
  - The RU knockback label is «Отбрас. ↓»: the whole word would shrink to 60%.
- **Where the numbers come from:**
  - Items: the sample (`ContentSamples.ItemsByType`).
  - Recipes: the recipe's `createItem`. Neither has a prefix.
  - Storage: the real stack, so a prefixed weapon sorts by its prefixed damage.
- **What each sort reads:**
  - Speed: `useAnimation`; fewer ticks is faster.
  - Crit chance: `crit` + 4, only for damage classes that crit (minions and whips do not).
  - Flight time: `ArmorIDs.Wing.Sets.Stats[wingSlot].FlyTime`, which Calamity's wings fill too.
  - Axe power: the raw `axe`. The tooltip's ×5 does not change the order.
- **Rarity:**
  - Gray to Purple come in the game's order.
  - Modded rarities come after Purple. They have no order of their own: Calamity's load alphabetically, so BurnishedAuric would come before Turquoise. They are ordered by the median value of their items. With Calamity: DarkOrange (Draedon, any tier), Turquoise, PureGreen, CosmicPurple, BurnishedAuric, CalamityRed, ExoticRainbow, HotPink.
  - Expert, Master and Quest (negative ids) have no tier.
- **An entry without the stat goes last in both directions:** a weapon with no knockback, an accessory with no defense, an item that sells for nothing. Ties fall back to the name.

## Switching category

- The player's choice stays in `SortMode` / `SortDescending`, per tab in `TabFilters`, saved as before.
- When the listed category does not offer it, the list is sorted Name A-Z, and the dropdown and the menu say so.
- When a category that offers it is listed again, the choice comes back. For example: Swords by Damage ↓, then All armor shows Name A-Z, then Swords shows Damage ↓ again.
- New enum values are saved by name. An older build reading a newer save gets Name; `BookUiState` needs no bump.

## The menu

- **Columns:**
  - The general list stays under Sort where it always was, 196 wide.
  - A category's sorts get a second column on its left, under the group's name ("WEAPONS" / «ОРУЖИЕ»).
  - That column is 240 wide: «Длительность: от короткой» measures 204 units.
- **Height:** at most 12 rows (Items and Recipes: 6 general sorts × 2). The panel ends at 508 of the book's 696 units, so it never runs off the book, at 70% book scale too. Labels shrink at a large text size instead of being cut.
- The Bestiary's list is unchanged.

## Showing the value

- Not added. The hover tooltip already shows damage, speed, crit, knockback, defense, healing, duration, tool power, fishing and bait power, and rarity by the name's colour.
- Value and flight time are the two the game does not show. A card line for them can come later if the order alone is not enough.

## Cost

- Each pass computes one key per listed entry (`PrepareRanks`, into the Station sort's `SortRankScratch`). The group comes from a per-type table built with the category index. The comparison reads the pass's sort once (`SortPass`).
- `c bench p=ModernRecipeBook.RecipeBookSystem.ApplyFilters a=false n=10`, Items with Calamity:
  - All items by Name: 7.2 ms, 44 B. It was 7.0 ms, 44 B before.
  - All items by Rarity: 6.5 ms, 44 B.
  - Swords by Damage: 0.31 ms, 44 B.
  - Recipes, all by Name: 3.6 ms, 76 B. It was the same before.
