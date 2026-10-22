using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// The Bestiary tab: the game's Bestiary entries in the book's grid with the same filters as Items (search, mod,
// category, stage, favorites, sort) plus Encountered, and the mob card on the right. It is the one place for mob info:
// Mouse3 on a mob in the world and How to Get's enemy rows open it.
public sealed partial class RecipeBookSystem
{
	// Filtered and Selected hold indexes into BookBestiary.Mobs on this tab.
	private static bool MetOnly;

	// The mob the card shows without touching the grid (How to Get, Mouse3 on a mob the filters hide); -1 none.
	private static int MobFocus = -1;

	// Net id of the mob pointed at in the world while its card is open: the card shows that mob's own numbers.
	private static int LiveMob;

	private static bool MobStatsMode;

	// What the card's caches (loot, tags, stats, portrait) were read for.
	private static (int NetId, bool Live, string Culture) MobCardKey;

	private static int MobCardNetId => MobCardKey.NetId;

	private static readonly List<string> MobMods = new List<string>();

	private static List<BookCategory> MobCategoryList = new List<BookCategory>();

	private static string MobCategoryCulture;

	private static readonly Dictionary<string, Asset<Texture2D>> MobIcons = new Dictionary<string, Asset<Texture2D>>(StringComparer.Ordinal);

	private static readonly (BookSortMode Mode, bool Descending)[] MobSortOptions = new (BookSortMode, bool)[5]
	{
		(BookSortMode.Name, false),
		(BookSortMode.Name, true),
		(BookSortMode.ItemId, false),
		(BookSortMode.Mod, false),
		(BookSortMode.Stage, false)
	};

	private static int MobSortOptionIndex => Math.Max(0, Array.IndexOf(MobSortOptions, (SortMode, SortDescending)));

	private static string MobSortOptionLabel(int option)
	{
		return option switch
		{
			0 => T("Имя А-Я", "Name A-Z"),
			1 => T("Имя Я-А", "Name Z-A"),
			2 => T("Как в игре", "Game order"),
			3 => T("Мод А-Я", "Mod A-Z"),
			_ => T("Этап", "Stage"),
		};
	}

	private static List<BookCategory> MobCategories
	{
		get
		{
			EnsureBestiary();
			string culture = Language.ActiveCulture.Name;
			if (MobCategoryCulture != culture || MobCategoryList.Count != BookBestiary.Tags.Count)
			{
				MobCategoryCulture = culture;
				MobCategoryList = BookBestiary.Tags.Select((BookBestiary.Tag tag) =>
				{
					string label = BookBestiary.Label(tag, Ru);
					return new BookCategory(tag.Key, label, label, tag.Icon.ItemType, (Item _) => false);
				}).ToList();
			}
			return MobCategoryList;
		}
	}

	private static void EnsureBestiary()
	{
		BookBestiary.EnsureBuilt();
		if (MobMods.Count == 0 && BookBestiary.Mobs.Count > 0)
		{
			MobMods.AddRange(BookBestiary.Mobs.Select((BookMob m) => m.Mod).Distinct().OrderBy(ModName, StringComparer.CurrentCultureIgnoreCase));
		}
	}

	private static void ClearBestiary()
	{
		BookBestiary.Clear();
		MobMods.Clear();
		MobCategoryList = new List<BookCategory>();
		MobCategoryCulture = null;
		MobIcons.Clear();
		MobCardKey = default;
		LiveMobStats.Clear();
		EnemyIcon = null;
		EnemyEntry = null;
		LockedIcon = null;
	}

	// The card's mob: a focused one, else the grid's selection.
	private static BookMob CardMob
	{
		get
		{
			if (!BestiaryView)
			{
				return null;
			}
			int index = (MobFocus >= 0) ? MobFocus : Selected;
			if (index < 0 || index >= BookBestiary.Mobs.Count)
			{
				return null;
			}
			return BookBestiary.Mobs[index];
		}
	}

	private static int CardVariantNetId(BookMob mob)
	{
		Variant = Math.Clamp(Variant, 0, mob.Variants.Length - 1);
		return mob.Variants[Variant];
	}

	private static bool MobMet(int netId)
	{
		return MobUnlockState(netId) > BestiaryEntryUnlockState.NotKnownAtAll_0;
	}

	private static void ApplyMobFilters()
	{
		EnsureBestiary();
		BookBestiary.Tag tag = (Category > 0 && Category < MobCategories.Count) ? BookBestiary.FindTag(MobCategories[Category].Key) : null;
		foreach (BookMob mob in BookBestiary.Mobs)
		{
			if ((tag != null && !tag.Members.Contains(mob.Index)) || (FavoritesOnly && !Favorites.Contains(mob.Key)) || (ModFilter.Length > 0 && mob.Mod != ModFilter) || (MetOnly && !MobMet(mob.NetId)))
			{
				continue;
			}
			NPC npc = mob.Sample;
			if (!BookRules.Search(Query, npc.FullName, mob.Key, ContentSamples.NpcPersistentIdsByNetIds[mob.NetId], ModName(mob.Mod)) || !BookProgressionRules.Matches(StageFilter, BookProgression.NpcStage(mob.NetId)))
			{
				continue;
			}
			Filtered.Add(mob.Index);
		}
		Filtered.Sort(CompareMobs);
		Page = Math.Clamp(Page, 0, Math.Max(0, (Filtered.Count - 1) / ActivePageSize));
		if (!Filtered.Contains(Selected))
		{
			Selected = (Filtered.Count > 0) ? Filtered[0] : (-1);
			DetailsPage = (Variant = 0);
		}
	}

	private static int CompareMobs(int left, int right)
	{
		BookMob a = BookBestiary.Mobs[left];
		BookMob b = BookBestiary.Mobs[right];
		int comparison = SortMode switch
		{
			BookSortMode.ItemId => a.Order.CompareTo(b.Order),
			BookSortMode.Mod => StringComparer.CurrentCultureIgnoreCase.Compare(ModName(a.Mod), ModName(b.Mod)),
			BookSortMode.Stage => BookProgression.NpcStage(a.NetId).CompareTo(BookProgression.NpcStage(b.NetId)),
			_ => 0,
		};
		if (comparison == 0 && SortMode != BookSortMode.ItemId)
		{
			comparison = StringComparer.CurrentCultureIgnoreCase.Compare(a.Sample.FullName, b.Sample.FullName);
		}
		if (comparison == 0)
		{
			comparison = a.Order.CompareTo(b.Order);
		}
		if (comparison == 0)
		{
			comparison = left.CompareTo(right);
		}
		return SortDescending ? (-comparison) : comparison;
	}

	// The NPC's first frame scaled into the box (at most 1.5×, so critters stay pixel-crisp), tinted like the
	// game draws slimes; never-met mobs dimmed.
	private static void DrawNpcIcon(SpriteBatch sb, int netId, BookRect rect, float opacity = 1f)
	{
		if (!ContentSamples.NpcsByNetId.TryGetValue(netId, out var npc) || npc.type <= 0 || npc.type >= TextureAssets.Npc.Length)
		{
			Label(sb, rect, "?", Muted);
			return;
		}
		Main.instance.LoadNPC(npc.type);
		Texture2D texture = TextureAssets.Npc[npc.type].Value;
		int frames = Math.Max(1, Main.npcFrameCount[npc.type]);
		Rectangle frame = new Rectangle(0, 0, texture.Width, Math.Max(1, texture.Height / frames));
		float scale = Math.Min(1.5f, Math.Min(rect.Width / (float)frame.Width, rect.Height / (float)frame.Height));
		Color tint = (npc.color == default(Color)) ? Color.White : npc.GetColor(Color.White);
		sb.Draw(texture, Origin + new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f) * Scale, frame, tint * opacity * GraphicsOpacity, 0f, frame.Size() / 2f, scale * Scale, SpriteEffects.None, 0f);
	}

	private static void DrawMobGrid(SpriteBatch sb)
	{
		float top = GridTop;
		int size = ActivePageSize;
		BookMob shown = CardMob;
		for (int n = 0; n < size; n++)
		{
			int f = Page * size + n;
			if (f >= Filtered.Count)
			{
				break;
			}
			BookMob mob = BookBestiary.Mobs[Filtered[f]];
			BookRect cell = BookGeometry.GridCell(n, top);
			bool hovered = !MenuOpen && cell.Contains(Mouse.X, Mouse.Y);
			bool selected = mob == shown;
			Box(sb, cell, selected ? SelectedFill : (hovered ? HoverFill : Raised), border: true, 10f, selected ? new Color?(Accent) : null, (!selected) ? 1 : 2);
			bool met = MobMet(mob.NetId);
			DrawNpcIcon(sb, mob.NetId, new BookRect(cell.X + 8f, cell.Y + 8f, cell.Width - 16f, cell.Height - 16f), met ? 1f : 0.35f);
			if (Favorites.Contains(mob.Key))
			{
				Star(sb, cell.X + 46f, cell.Y + 4f, 14f, on: true);
			}
			if (hovered)
			{
				MouseTip(mob.Sample.FullName + "\n" + (met ? (MobKindName(mob.Kind) + " · " + ModName(mob.Mod)) : T("Ещё не встречен", "Not encountered yet")));
			}
		}
		if (Filtered.Count == 0)
		{
			EmptyState(sb, new BookRect(212f, top, 568f, 596f - top), T("Ничего не найдено", "Nothing found"), T("Ctrl+Backspace сбрасывает фильтры", "Ctrl+Backspace resets filters"));
		}
	}

	private static string MobKindName(BookMobKind kind)
	{
		return kind switch
		{
			BookMobKind.Boss => T("Босс", "Boss"),
			BookMobKind.MiniBoss => T("Мини-босс", "Mini-boss"),
			BookMobKind.Town => T("Житель", "Town NPC"),
			BookMobKind.Critter => T("Существо", "Critter"),
			_ => T("Враг", "Enemy"),
		};
	}

	// Bestiary tag names are the game's (and mods') own and some run long ("Hallowed Underground Desert"): they
	// shrink a little to fit the sidebar instead of being cut. Item categories keep the standard size.
	private static float CategoryLabelSize(string label, float width)
	{
		if (!BestiaryView)
		{
			return 0.8f;
		}
		float natural = TextWidth(label, 0.8f);
		return (natural <= width) ? 0.8f : Math.Max(0.68f, 0.8f * width / natural);
	}

	// Sidebar icons: an item, or on the Bestiary tab the Bestiary's own tag icon (a mod biome's icon).
	private static void DrawCategoryIcon(SpriteBatch sb, BookCategory category, BookRect rect)
	{
		if (category.IconType > 0)
		{
			if (ContentSamples.ItemsByType.TryGetValue(category.IconType, out var icon))
			{
				Icon(sb, icon, rect.X + 8f, rect.Y + 4f, 20f, tooltip: false);
			}
			return;
		}
		BookBestiary.Tag tag = BestiaryView ? BookBestiary.FindTag(category.Key) : null;
		if (tag?.Icon.Texture == null)
		{
			return;
		}
		if (!MobIcons.TryGetValue(tag.Icon.Texture, out var asset))
		{
			asset = (MobIcons[tag.Icon.Texture] = tag.Icon.Texture.StartsWith("Images/", StringComparison.Ordinal) ? Main.Assets.Request<Texture2D>(tag.Icon.Texture) : ModContent.Request<Texture2D>(tag.Icon.Texture));
		}
		if (!asset.IsLoaded)
		{
			return;
		}
		Texture2D texture = asset.Value;
		Rectangle source = (tag.Icon.Frame is Rectangle grid) ? texture.Frame(grid.Width, grid.Height, grid.X, grid.Y) : texture.Bounds;
		float scale = 20f / Math.Max(source.Width, source.Height);
		sb.Draw(texture, Origin + new Vector2(rect.X + 18f, rect.Y + rect.Height / 2f) * Scale, source, Color.White * GraphicsOpacity, 0f, source.Size() / 2f, scale * Scale, SpriteEffects.None, 0f);
	}

	// Encountered filter: only the mobs the Bestiary knows (killed, talked to or seen). An eye drawn with the book's strokes.
	private static void DrawMetFilter(SpriteBatch sb)
	{
		BookRect rect = BookGeometry.MetFilter;
		IconButtonFrame(sb, rect, MetOnly);
		float cx = rect.X + rect.Width / 2f;
		float cy = rect.Y + rect.Height / 2f;
		Color color = MetOnly ? Accent : TextColor;
		for (int i = 0; i < 10; i++)
		{
			double a = Math.PI * i / 10.0;
			double b = Math.PI * (i + 1) / 10.0;
			Line(sb, cx - (float)Math.Cos(a) * 9f, cy - (float)Math.Sin(a) * 5f, cx - (float)Math.Cos(b) * 9f, cy - (float)Math.Sin(b) * 5f, color);
			Line(sb, cx - (float)Math.Cos(a) * 9f, cy + (float)Math.Sin(a) * 5f, cx - (float)Math.Cos(b) * 9f, cy + (float)Math.Sin(b) * 5f, color);
		}
		Box(sb, cx - 2.5f, cy - 2.5f, 5f, 5f, color, border: false, 2.5f);
		if (!MenuOpen && rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Только встреченные", "Encountered only") + "\n" + T("Те, что уже есть в бестиарии", "The ones the Bestiary knows"));
		}
	}

	private static bool HandleMobGridClick()
	{
		for (int n = 0; n < ActivePageSize; n++)
		{
			int f = Page * ActivePageSize + n;
			if (f >= Filtered.Count)
			{
				break;
			}
			if (!BookGeometry.GridCell(n, GridTop).Contains(Mouse.X, Mouse.Y))
			{
				continue;
			}
			KeyboardState keyboard = Keyboard.GetState();
			if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
			{
				ToggleMobFavorite(BookBestiary.Mobs[Filtered[f]]);
				return true;
			}
			ResetCard();
			Selected = Filtered[f];
			DetailsPage = (Variant = 0);
			return true;
		}
		return false;
	}

	private static void ToggleMobFavorite(BookMob mob)
	{
		if (!Favorites.Add(mob.Key))
		{
			Favorites.Remove(mob.Key);
		}
		ApplyFilters(clearMessage: false);
	}

	// Reads the card's data when the shown mob (or the language, or whether it is the live one) changes.
	private static void EnsureMobCard()
	{
		BookMob mob = CardMob;
		if (mob == null)
		{
			MobCardKey = default;
			return;
		}
		int netId = CardVariantNetId(mob);
		(int, bool, string) key = (netId, netId == LiveMob, Language.ActiveCulture.Name);
		if (MobCardKey != key)
		{
			MobCardKey = key;
			ReadMobCard(netId);
		}
	}

	private static void UpdateMobCard()
	{
		EnsureMobCard();
		if (MobCardNetId != 0)
		{
			UpdateEnemyPortrait();
		}
	}

	private static int MobMaxDetailsPage()
	{
		EnsureMobCard();
		if (MobStatsMode || CardMob == null)
		{
			return 0;
		}
		return Math.Max(0, (EnemyLoot.Count - 1) / BookGeometry.MobLootRows);
	}

	private static string[] MobModeLabels()
	{
		return new string[2]
		{
			T("Добыча", "Loot"),
			T("Характеристики", "Stats")
		};
	}

	private static BookRect[] MobModeRects()
	{
		return SegmentRects(BookGeometry.CardMode, MobModeLabels(), 0.8f, natural: false);
	}

	private static void DrawMobCard(SpriteBatch sb)
	{
		Box(sb, BookGeometry.DetailsPanel, Surface, border: false, 12f);
		BookMob mob = CardMob;
		if (mob == null)
		{
			EmptyState(sb, BookGeometry.DetailsPanel, T("Ничего не выбрано", "Nothing selected"), T("Выберите моба слева", "Pick a mob on the left"));
			return;
		}
		EnsureMobCard();
		int netId = MobCardNetId;
		NPC npc = ContentSamples.NpcsByNetId[netId];
		BookRect back = BookGeometry.CardBack;
		bool canBack = History.CanBack;
		IconButtonFrame(sb, back, active: false, canBack);
		Chevron(sb, back.X + back.Width / 2f - 1f, back.Y + back.Height / 2f, 4f, 2, canBack ? TextColor : Border);
		if (!MenuOpen && back.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("Назад", "Back") + (canBack ? (": " + HistoryLabel(History.Previous)) : "") + "\nMouse4 · Alt+←");
		}
		BookRect[] modes = MobModeRects();
		Segmented(sb, modes, MobModeLabels(), MobStatsMode ? 1 : 0, 0.8f);
		int hoverMode = MenuOpen ? (-1) : SegmentAt(modes);
		if (hoverMode >= 0)
		{
			MouseTip(((hoverMode == 0) ? T("Что с него падает", "What it drops") : T("Здоровье, защита, урон и другое", "Life, defense, damage and more")) + " · Alt+U");
		}
		DrawEnemyPortrait(sb, BookGeometry.MobPortrait, netId);
		bool pager = mob.Variants.Length > 1;
		BookRect name = BookGeometry.MobName(pager);
		FixedLabel(sb, npc.FullName, name.X, name.Y, TextColor, 1.05f, name.Width, name.Height);
		if (pager)
		{
			ChevronPager(sb, BookGeometry.MobVariantPrevious, BookGeometry.MobVariantNext, BookGeometry.MobVariantLabel, Variant, mob.Variants.Length);
			if (!MenuOpen && (BookGeometry.MobVariantPrevious.Contains(Mouse.X, Mouse.Y) || BookGeometry.MobVariantNext.Contains(Mouse.X, Mouse.Y) || BookGeometry.MobVariantLabel.Contains(Mouse.X, Mouse.Y)))
			{
				MouseTip(T("Варианты, которые бестиарий считает этим мобом", "Variants the Bestiary counts as this mob"));
			}
		}
		BookRect subtitle = BookGeometry.MobSubtitle;
		FixedLabel(sb, MobKindName(mob.Kind) + " · " + ModName(mob.Mod), subtitle.X, subtitle.Y, Muted, 0.8f, subtitle.Width, subtitle.Height);
		BookRect tags = BookGeometry.MobTags;
		Box(sb, tags, Raised, border: false, 8f);
		TagLine(0, T("Этап", "Stage"), StageName(BookProgression.NpcStage(netId)));
		TagLine(1, T("Биом", "Biome"), (EnemyBiomes.Length > 0) ? EnemyBiomes : T("Не указан", "Not listed"));
		TagLine(2, T("Появление", "Spawns"), (EnemySpawn.Length > 0) ? EnemySpawn : T("Не указано", "Not listed"));
		if (!MenuOpen && tags.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(T("По данным бестиария; может быть неполно", "From the Bestiary; may be incomplete"));
		}
		if (MobStatsMode)
		{
			DrawMobStats(sb, netId);
		}
		else
		{
			DrawMobLoot(sb);
		}
		bool favorite = Favorites.Contains(mob.Key);
		BookRect star = BookGeometry.MobFavorite;
		IconButtonFrame(sb, star, favorite);
		Star(sb, star.X + 6f, star.Y + 6f, 20f, favorite);
		if (!MenuOpen && star.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(favorite ? T("Убрать из избранного · Alt+F", "Remove from favorites · Alt+F") : T("В избранное · Alt+F", "Add to favorites · Alt+F"));
		}
		void TagLine(int line, string label, string value)
		{
			float y = tags.Y + 4f + (float)(line * 20);
			FixedLabel(sb, label, tags.X + 10f, y, Muted, 0.75f, 84f, 20f);
			FixedLabel(sb, value, tags.X + 96f, y, TextColor, 0.8f, tags.Width - 104f, 20f);
		}
	}

	// Loot rows: name and chance on the first line, amount and conditions below; a click opens the item.
	private static void DrawMobLoot(SpriteBatch sb)
	{
		BookRect body = BookGeometry.MobBody;
		if (EnemyLoot.Count == 0)
		{
			DrawCardEmpty(sb, body, T("Добыча не указана", "No loot listed"), T("С него ничего не падает", "It drops nothing"));
			return;
		}
		int pages = Math.Max(1, (EnemyLoot.Count + BookGeometry.MobLootRows - 1) / BookGeometry.MobLootRows);
		DetailsPage = Math.Clamp(DetailsPage, 0, pages - 1);
		for (int row = 0; row < BookGeometry.MobLootRows; row++)
		{
			int index = DetailsPage * BookGeometry.MobLootRows + row;
			if (index >= EnemyLoot.Count)
			{
				break;
			}
			BookEnemyLoot loot = EnemyLoot[index];
			Item item = ContentSamples.ItemsByType[loot.ItemType];
			BookRect rect = BookGeometry.MobLootRow(row);
			bool hover = !MenuOpen && rect.Contains(Mouse.X, Mouse.Y);
			Box(sb, rect, hover ? HoverFill : Raised, border: true, 8f);
			Icon(sb, item, rect.X + 6f, rect.Y + 5f, 32f, tooltip: false);
			bool varies = loot.Rules.Length > 1;
			string chance = varies ? "" : BookAcquisitionRules.Chance(loot.Rules[0].Chance);
			float chanceWidth = (chance.Length > 0) ? (TextWidth(chance, 0.85f) + 4f) : 0f;
			FixedLabel(sb, item.Name, rect.X + 46f, rect.Y + 2f, TextColor, 0.9f, rect.Width - 54f - chanceWidth - 8f, 20f);
			if (chance.Length > 0)
			{
				CenterLabel(sb, chance, rect.X + rect.Width - 8f - chanceWidth, rect.Y + 2f, chanceWidth, 20f, TextColor, 0.85f, rightAligned: true);
			}
			string detail = varies ? T("Зависит от условий", "Varies by condition") : (loot.Amount + ((loot.Rules[0].Conditions.Length > 0) ? (" · " + loot.Rules[0].Conditions) : ""));
			FixedLabel(sb, detail, rect.X + 46f, rect.Y + 21f, Muted, 0.75f, rect.Width - 54f, 18f);
			if (hover)
			{
				MouseTip(item.Name + "\n" + (varies ? (EnemyLootTooltip(loot) + "\n") : "") + T("Клик: карточка предмета", "Click: the item's card"));
			}
		}
		if (pages > 1)
		{
			ChevronPager(sb, BookGeometry.MobPagePrevious, BookGeometry.MobPageNext, BookGeometry.MobPageLabel, DetailsPage, pages);
			if (!MenuOpen && BookGeometry.MobPagePrevious.Contains(Mouse.X, Mouse.Y) && DetailsPage > 0)
			{
				MouseTip(T("Назад · колесо", "Previous · wheel"));
			}
			if (!MenuOpen && BookGeometry.MobPageNext.Contains(Mouse.X, Mouse.Y) && DetailsPage < pages - 1)
			{
				MouseTip(T("Дальше · колесо", "Next · wheel"));
			}
		}
	}

	private static void HandleMobCardClick()
	{
		BookMob mob = CardMob;
		if (mob == null)
		{
			return;
		}
		EnsureMobCard();
		if (BookGeometry.CardBack.Contains(Mouse.X, Mouse.Y))
		{
			HistoryBack();
			return;
		}
		int mode = SegmentAt(MobModeRects());
		if (mode >= 0)
		{
			MobStatsMode = mode == 1;
			return;
		}
		if (mob.Variants.Length > 1)
		{
			if (BookGeometry.MobVariantPrevious.Contains(Mouse.X, Mouse.Y))
			{
				Variant = Math.Max(0, Variant - 1);
				DetailsPage = 0;
				return;
			}
			if (BookGeometry.MobVariantNext.Contains(Mouse.X, Mouse.Y))
			{
				Variant = Math.Min(mob.Variants.Length - 1, Variant + 1);
				DetailsPage = 0;
				return;
			}
		}
		if (BookGeometry.MobFavorite.Contains(Mouse.X, Mouse.Y))
		{
			ToggleMobFavorite(mob);
			return;
		}
		if (MobStatsMode)
		{
			return;
		}
		int pages = MobMaxDetailsPage() + 1;
		if (pages > 1 && BookGeometry.MobPagePrevious.Contains(Mouse.X, Mouse.Y))
		{
			DetailsPage = Math.Max(0, DetailsPage - 1);
			return;
		}
		if (pages > 1 && BookGeometry.MobPageNext.Contains(Mouse.X, Mouse.Y))
		{
			DetailsPage = Math.Min(pages - 1, DetailsPage + 1);
			return;
		}
		for (int row = 0; row < BookGeometry.MobLootRows; row++)
		{
			int index = DetailsPage * BookGeometry.MobLootRows + row;
			if (index < EnemyLoot.Count && BookGeometry.MobLootRow(row).Contains(Mouse.X, Mouse.Y))
			{
				// The item opens on Items; Back returns to this mob.
				SwitchTab(0);
				StepCard(EnemyLoot[index].ItemType, usedIn: false);
				return;
			}
		}
	}

	// Opens a mob on the Bestiary tab: selected in the grid when the filters show it, else focused in the card.
	private static void ShowMob(int index, int netId)
	{
		if (index < 0 || index >= BookBestiary.Mobs.Count)
		{
			return;
		}
		if (!BestiaryView)
		{
			SwitchTab(2);
		}
		ResetCard();
		int position = Filtered.IndexOf(index);
		if (position >= 0)
		{
			Selected = index;
			Page = position / ActivePageSize;
		}
		else
		{
			MobFocus = index;
		}
		Variant = Math.Max(0, Array.IndexOf(BookBestiary.Mobs[index].Variants, netId));
		DetailsPage = 0;
	}

	// How to Get's enemy rows: Back returns to How to Get.
	private static void ShowMobFromSource(BookAcquisitionGroup source)
	{
		int netId = source.Source.NpcNetId;
		int index = BookBestiary.IndexOf(netId);
		if (index < 0)
		{
			return;
		}
		CloseAcquisition();
		ShowMob(index, netId);
	}

	// Mouse3 on a mob in the world: the book opens on the Bestiary tab with that mob, showing its own numbers.
	internal static bool OpenForMob(NPC npc)
	{
		if (npc == null || Main.dedServ || Main.gameMenu || !ContentSamples.NpcsByNetId.ContainsKey(npc.netID))
		{
			return false;
		}
		int index = BookBestiary.IndexOf(npc.netID);
		if (index < 0)
		{
			return false;
		}
		if (!Open)
		{
			Toggle();
		}
		CloseAcquisition();
		SetTyping(value: false);
		ModMenu = (SortMenu = (StageMenu = (HelpOpen = false)));
		ShowMob(index, npc.netID);
		LiveMobStats[npc.netID] = ReadMobStats(npc);
		LiveMob = npc.netID;
		return true;
	}
}
