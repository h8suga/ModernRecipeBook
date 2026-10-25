# Weapon traits filter (branch `feature/weapon-traits`)

Owner: «рассортировать оружие по типам например самонаводящиеся и т.д... фильтром». Code: `BookItemTraits.cs` (what an
item has and why), `BookHomingText.cs` (the description rule), `BookVerifiedHoming.cs` (the checked list),
`BookVanillaShots.cs` (vanilla code that overrides projectile defaults), `RecipeBookSystem.Traits.cs` (button, menu,
filter pass, card line).

No code of a weapon runs to find its traits. An earlier draft fired every weapon on a throwaway player and stepped its
projectiles; the owner vetoed any simulation, so every trait comes from what the game already states: the item's
description, the item's fields and the defaults of the projectile it shoots.

## Traits and where each comes from

| Trait | EN / RU | Source |
|---|---|---|
| Homing | Homing / Самонаведение | The description (see below), or the checked list |
| Homing stealth strike | Homing stealth strike / Наводится скрытый удар | A description line about stealth strikes that says homing; with Calamity only |
| Pierces | Pierces / Пробивает | `penetrate` of the shot is -1 or above 1 |
| Through walls | Through walls / Сквозь стены | `tileCollide` of the shot is false |
| Auto attack | Auto attack / Автоатака | The item's `autoReuse` |
| Minion | Minion / Миньон | The shot's `minion` (or a pet with minion slots) |
| Sentry | Sentry / Турель | The shot's `sentry` |

Dropped on the owner's word: "several projectiles per use" and "inflicts a debuff". Neither can be known without
running the weapon's code (Shoot hooks, OnHitNPC).

The card names the item's traits on a third header line in accent colour, as many as fit and then "+N". Its hover
lists each trait with its source: the description line quoted (and whether it was the English text), the checked
note, "up to N hits (penetrate)", "the shot ignores blocks (tileCollide)", "the item's autoReuse".

## Homing from the description

- **Text.** The lines the game shows on hover, with every mod's ModifyTooltips edits (`BookItemTooltip.Lines`, the
  descriptive lines only), split at newlines.
- **Other languages.** The item's own lines are read in English instead: vanilla's from Terraria's own `en-US` JSON,
  a mod's from its English translation (tModLoader's `LocalizationLoader.LoadTranslations`). Translations drop the
  word (Russian vanilla says «атакует», "attacks", for Bee Gun and Chlorophyte Bullet; the Calamity translation mod
  drops "homing" from Blighted Cleaver, Empyrean Knives, The Prince and more) or lag behind the mod (it still promises
  Nanoblack Reaper homing silhouettes). When the English text is only placeholders (`[MAIN]`, `[GFB]`: Calamity builds
  SHPC, Superradiant Slaughterer or Galaxia from other keys when shown), the shown lines are read. Lines other mods add
  are read as shown. With CalamityModRuLocalization, Russian finds exactly what English finds (checked trait by trait
  on the stand), and the card says "from the English description".
- **Words** (start of a word): homing, home/homes/homed (whole word), seek, chase, track, pursue, "lock onto";
  самонавод, наводящ, наводят, наводит, преследу, преследова.
- **Not counted:**
  - the word goes on to the cursor or the mouse within four words ("seeks the cursor", «следует за курсором»): the
    player steers it;
  - "home" as a noun ("return home", "a home");
  - "track record";
  - flavor text: a line that starts or ends with a quote;
  - a line about what appears on a hit or a death ("small stars that home on enemy hits", "explode into homing blood on
    death", «при попадании», «при столкновении»): the shot itself does not home. This also drops a few real ones that
    mention a hit in passing, e.g. Exoblade's "beams that home in on enemies and slice them on hit";
  - minions and sentries: they always chase.
- **Stealth.** A line that mentions stealth ("Stealth strikes ...", «Скрытые удары ...») gives "homing stealth strike"
  instead of "homing".
- `BookHomingText.SelfCheck` runs the rule on real lines.

## The checked list (`BookVerifiedHoming`)

Weapons whose shots steer toward enemies in flight, verified in the decompiled code (tModLoader 2026.8, Terraria
1.4.4, Calamity 2.2.4 loaded). P = `TerrariaStand\ref\tml\Terraria\Projectile.cs`, CGP = Calamity's
`CalamityGlobalProjectile.cs`.

| Item | Shot | Code | Note |
|---|---|---|---|
| Magic Missile, Flamelash, Rainbow Rod | 16, 34, 79 (aiStyle 9) | P:36102-36197 | Cursor-steered while held; once let go, seek within 800 px (LOS) |
| Bee Gun, Wasp Gun, Bat Scepter | 181/566, 189, 316 (aiStyle 36) | P:23954-24057 | After 30 AI ticks, 800 (Manhattan), LOS; Calamity adds an extra update (CGP:4166) |
| Possessed Hatchet | 182 | P:20117-20193 | 800 px, LOS |
| Sergeant United Shield | 866 | P:20194-20280 | 800 px, LOS |
| Chlorophyte Bullet | 207 | vanilla P:47657-47758; Calamity's rewrite CGP:1951-2045 | 300 (Manhattan), LOS |
| Book of Skulls | 837 | P:47657-47758 | 300, weak turn |
| Spectre Staff | 297 | P:25174-25245 | 400 px, LOS |
| Snowman Cannon | 338-341, 803-810 | P:39916-39955 | After 30 ticks, 600, LOS |
| Flairon | bubbles 405 | P:27161-27213 | Released in flight; 650 px, LOS |
| Razorblade Typhoon | 409 (aiStyle 71) | P:27225-27331 | 500 px, LOS |
| Vortex Beater | rocket 616 | P:48207-48300 | Only the rocket every 7th shot |
| Nebula Arcanum | 617 | P:31792-31941 | Once slowed down; 800 px |
| Nebula Blaze | 634/635 | P:45423-45506 | 250 / 500 px |
| Spirit Flame | 659 | vanilla P:33573-33672; Calamity's rewrite CGP:1843-1949 | 500 px, LOS |
| Celebration Mk2 | 715-718 | P:43955-44043 | Only one of its 7 rocket modes |
| Nightglow | 931 | P:38264-38320 | 800 px while 20 < timeLeft ≤ 180 |
| Weather Pain | 969 | P:37944-37984 | 400 px, LOS |
| Frosty Flare (Calamity) | stealth: FrostyFlareStealth drops FrostyFlareProj with ai[2] = 1 | FrostyFlareStealth.cs:45-49, FrostyFlareProj.cs:53-56 | Homing stealth strike: the phantom flares home within 400 px |

Left out on purpose (checked, not homing in flight): Chlorophyte Arrow and Nano Bullet (re-aim only when they bounce
off a tile, P:16632-16680), Crystal Dart (jumps on a hit or a bounce), Vampire Knives (aiStyle 2 has no target search),
Hellwing Bow and Bee's Knees (their arrows fly straight; Bee's Knees' bees appear when the arrow dies), Scourge of the
Corruptor (tiny eaters spawn on impact), Piranha Gun (re-targets only after its enemy dies), Flower Pow, Terrarian,
Magnet Sphere, Medusa Head (aimed once), Flying Knife (cursor only), Influx Waver (dashes after a hit), Lunar Flare.

## Pierces and through walls from the projectile

Read off `ContentSamples.ProjectilesByType[item.shoot]`, only for a shot that flies off on its own. No trait (unknown)
for:

- a weapon that uses ammo: its shot is the ammo's (the item's `shoot` is a placeholder; Megashark's is Purification
  Powder). The ammo item has its own traits; ammo a launcher turns into its own shot (rockets,
  `AmmoID.Sets.SpecificLauncherAmmoProjectileMatches`) and ammo without damage (solutions) have none;
- minions, sentries, whips;
- held weapons: `ownerHitCheck`, aiStyles 15 flail, 19 spear, 20 drill, 75 held gun, 99 yoyo, 161 shortsword, 190/191
  swing, a channelled weapon without its own graphic (holdouts), a weapon with no shoot speed, a mod projectile whose
  base class says Holdout / Spear / Shortsword / Flail / Laserbeam / Whip / Swing / CustomUseStyle / Yoyo;
- shots that do not fly: aiStyle 4 (Vilethorn vines), 22 (Ice Rod blocks).

Vanilla runs one AI for every type, so where its code overrides these defaults was read by hand from
`Projectile.cs` (`BookVanillaShots`; P = `TerrariaStand\ref\tml\Terraria\Projectile.cs`):

- **Pierces is left out** when the shot does not keep flying after a hit: boomerangs turn back (aiStyle 3,
  P:12239-12252), hooks, chains and Flairon are pulled back (7, 13, 69; P:13051-13066), explosives and Molotovs go off
  (16, 68, `ProjectileID.Sets.Explosive`; P:12291-12311), flares go harmless once stuck (33; P:23697), the Piranha
  Gun latches (39; P:12393), nails stick (93; P:12444), yoyos bounce off (99); and types that stick, end or change on
  a hit: javelins 598/614/636/971/975 (P:12368-12374; sticking is in `Projectile.Damage`, which tests the real type, so
  Calamity's Crystal Piercer, which borrows the Bone Javelin AI, does pierce), Hellfire Arrow 41 (P:12409), Spirit
  Flame 659 (P:12380), Electrosphere 442/443, Influx Waver 451, Charged Blaster orb 459, Ichor Dart 479, Shadowflame
  Knife 497, Meowmere 502, toxic clouds 511-513, Ichor splash 524, Terrarian beam 604, Solar Eruption ray 607, Rainbow
  Crystal 644, Lunar Flare 645, Sandnado 656, Celebration Mk2 rockets 715/717, Blood Thorn 756, Smolstar 864, 973 and
  985 (harmless partway), and minion shots that toggle friendly (307, 317, 356). A penetrate lost on tile bounces
  (bees, Meteor Shot, Crystal Dart) still pierces enemies and keeps the trait.
- **Through walls is left out** for types whose AI switches `tileCollide` in flight: falling stars that collide once
  below the cursor (9, 92, 503, 723-726; P:20636-20653), Blizzard and Meteor Staff (337, 424-426; P:47810, 47900),
  Lunar Flare (645), Phantasm arrows (631), Zapinator laser (876), Pygmy spear (195), nails (514), toxic clouds,
  Molotov and Ale, Influx Waver, Piranha, Flairon, Flying Knife, javelins, explosives at detonation, and the held or
  returning ones (boomerangs, chains, flails, yoyos).

For mod projectiles the IL of their own class is read (nothing runs): a projectile whose code stores `penetrate` or
`tileCollide` outside SetDefaults, or calls `Kill()` in a hit hook, gets no claim for that trait; nor does an item whose
Shoot, ModifyShootStats, UseItem, HoldItem, UseStyle or CanUseItem stores them. Terror Talons' small claw starts with
`tileCollide = false` but its AI turns it on after two ticks: no "through walls". Blazing Star's glaive (penetrate 6)
loses penetrate on bounces and becomes -1 on stealth: no claim.

## Filter and UI

- **Where.** Items and Recipes. The button sits under the class buttons, full width; the category list loses one row
  there (13 instead of 14). On the other tabs the sidebar is as before.
- **Semantics.** Multi-select, every ticked trait must be there (AND). Combines with search, mod, category, class,
  stage, stations and Craftable.
- **Button.** "Traits" / «Свойства», or the ticked names ("Homing + Pierces"); when they do not fit, the first and "+N".
  Accent text while active. Its tooltip lists the ticked traits and how many items they hid.
- **Menu.** Like the station filter's: "Show all" clears; one row per trait with a check box (a tick, not colour alone)
  and how many listed items have it (counted among what the other filters list, so the counts never shrink while
  ticking). A row's tooltip says what the trait means and where it is read from. Arrows and Enter/Space tick, Esc
  closes it as the top layer; it captures the keyboard while open. Rows with 0 are dimmed, still clickable. While the
  descriptions are still being read the footer says "Reading item descriptions… N%".
- **Empty grid.** "Hidden by the traits filter" / «Скрыто фильтром свойств».
- **State.** Global like the class filter, `BookUiState.TraitFilter` (version 9; older saves have none). Saved per
  player, restored by Back/Forward. Only the player changes it; Ctrl+Backspace leaves it, as it leaves the class.

## Results with Calamity 2.2.4 (stand, English; Russian identical)

1250 weapons and ammo. Homing 116 (21 from the checked list), homing stealth strike 18 (1 from the list), pierces 223,
through walls 137, auto attack 871, minion 103, sentry 34. With the class filter on Rogue and Homing ticked, 26
weapons stay.

Not judged for pierce / walls (unknown, no claim): held 229, shot comes from the ammo 223, summon or whip 147, no
projectile 90, pierce: the projectile's code changes penetrate 56, pierce: stops on a hit 54, walls: its code changes
tileCollide 35, launcher decides or no damage 10, walls: vanilla AI switches tileCollide 6, no flying shot 4.

## Spot checks (book's verdict, code evidence)

CAL = `ModSources\.ref\CalamityMod-2.2.4`.

| Weapon | Book | Evidence |
|---|---|---|
| Chlorophyte Bullet | Homing (list) | Calamity's rewrite CGP:1951-2045: 300 (Manhattan), LOS |
| Chlorophyte Arrow | no homing | re-aims only off a tile, P:16632-16654 |
| Magic Missile | Homing (list) | cursor while held, seeks 800 px once let go, P:36102-36197 |
| Vampire Knives | no homing | aiStyle 2 has no target search, P:19398-19949 |
| Bee Gun | Homing (list), Pierces 2 | aiStyle 36 P:23954-24057; Calamity sets penetrate 2, CGP:5592 |
| Razorblade Typhoon | Homing (list), Pierces -1 | aiStyle 71 P:27225-27331 |
| Wooden Bow, Megashark | no homing; no pierce/walls verdict | the shot is the ammo's; Wooden Arrow and Musket Ball have no homing |
| Blazing Star | no homing; pierce not judged | ricochets toward enemies only on a hit or a bounce, CAL BlazingStarProj.cs:65-124; its code changes penetrate |
| Frosty Flare | Homing stealth strike (list) | FrostyFlareStealth.cs:45-49 drops FrostyFlareProj with ai[2] = 1, which homes, FrostyFlareProj.cs:53-56; the normal flare does not |
| Icebreaker | no homing | boomerang (AIType 52); boomerang homing is only 182/866, P:20099 |
| Terror Talons | no homing; walls not judged | sine-wave claw; the stealth claw re-strikes after a hit, TalonLargeProj.cs:47-91; AI sets tileCollide, TalonSmallProj.cs:33 |
| Crystal Piercer | Pierces -1 | does not stick (sticking needs type 598, P:12368); no homing |
| Exorcism | Pierces -1, Through walls | falls at the cursor, ExorcismProj.cs:123-139; penetrate -1, tileCollide false, :60-64 |
| Spear of Destiny | Homing (description) | "Cycles between throwing 3 small homing spears..."; SpearofDestinyProjectile.cs:56-81 homes |

## Cost

- Fields, with the IL reads, run while the world loads (`OnWorldLoad`, on the loading thread): about 20 ms with
  Calamity. The IL reader walks instructions with their operand sizes, so it never throws (an earlier byte scan threw
  on bad tokens, which tModLoader logs, and took 260 ms).
- The English texts, in another language, also while loading: about 0.1 s. A language switch in game reads them once,
  after the switch's own long pause.
- Descriptions: a 2 ms slice per update from the first update in the world, about 35 ms in all in English, 60 ms in
  Russian (13–30 slices). The longest slice seen in game was 5.8 ms (one item's tooltip runs over the budget).
- Filter pass (`ApplyFilters` on Items, 8310 items): 0.3–0.5 ms per pass with Homing ticked, 12 ms with no filter
  (the name sort); the bytes per call are the same with and without the traits filter (the traits code allocates
  nothing per pass; the candidate list is reused). The button label, the card line and the menu counts are cached.

## Stand checks

- `c call p=ModernRecipeBook.BookItemTraits.Status` (totals, why items were not judged, timing)
- `c call p=ModernRecipeBook.BookItemTraits.Dump out=traits.tsv` (type, mod, name, class, traits, homing source and
  line, stealth source and line, penetrate, why not judged, projectile and its fields). Open the book first, or the
  class column is empty.
- `c call p=ModernRecipeBook.BookItemTraits.Rebuild`
- `c call p=ModernRecipeBook.BookHomingText.SelfCheck`
