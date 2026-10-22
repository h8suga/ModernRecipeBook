using System;

namespace ModernRecipeBook;

internal readonly record struct BookGeometry(float Left, float Top, float Scale)
{
	internal static BookRect DetailsPanel => new BookRect(796f, 76f, 316f, 564f);

	internal static BookRect CategoryPanel => new BookRect(8f, 76f, 188f, 564f);


	internal static BookRect AcquisitionPanel => new BookRect(160f, 80f, 800f, 536f);

	// Card header: [back] [icon] title / subtitle ........ [‹ n/m ›] [X]; one divider; list; footer pager.
	internal static BookRect AcquisitionBack => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 24f, 32f, 32f);

	internal static BookRect AcquisitionClose => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 56f, AcquisitionPanel.Y + 24f, 32f, 32f);

	internal static BookRect AcquisitionVariantPrevious => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 188f, AcquisitionPanel.Y + 24f, 32f, 32f);

	internal static BookRect AcquisitionVariantLabel => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 152f, AcquisitionPanel.Y + 24f, 48f, 32f);

	internal static BookRect AcquisitionVariantNext => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 96f, AcquisitionPanel.Y + 24f, 32f, 32f);

	internal static BookRect AcquisitionDivider => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 80f, AcquisitionPanel.Width - 48f, 1f);

	internal static BookRect AcquisitionPrevious => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 152f, AcquisitionPanel.Y + 480f, 32f, 32f);

	internal static BookRect AcquisitionNext => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 56f, AcquisitionPanel.Y + 480f, 32f, 32f);

	internal static BookRect AcquisitionPager => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 116f, AcquisitionPanel.Y + 480f, 56f, 32f);

	internal static BookRect AcquisitionStatus => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 480f, AcquisitionPanel.Width - 200f, 32f);

	internal static BookRect AcquisitionList => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 92f, AcquisitionPanel.Width - 48f, 376f);

	internal static BookRect TrackSingleAction => new BookRect(812f, 604f, 164f, 28f);

	internal static BookRect TrackSetAction => new BookRect(984f, 604f, 112f, 28f);

	internal static BookRect TrackAction => new BookRect(812f, 604f, 284f, 28f);

	internal static BookRect FavoriteAction => new BookRect(812f, 564f, 32f, 32f);

	internal static BookRect CraftAction => new BookRect(852f, 564f, 244f, 32f);

	internal static BookRect PagePrevious => new BookRect(212f, 604f, 32f, 32f);

	internal static BookRect PageNext => new BookRect(748f, 604f, 32f, 32f);

	internal static BookRect PageLabel => new BookRect(252f, 604f, 488f, 32f);

	// The mod list drops down from the mod filter button, wide enough for long mod names.
	internal static BookRect ModMenuPanel
	{
		get
		{
			BookRect anchor = ModFilter;
			float width = Math.Max(anchor.Width, 288f);
			float height = 16f + 9f * 32f;
			float x = Math.Clamp(anchor.X, 16f, Width - 16f - width);
			float y = Math.Clamp(anchor.Y + anchor.Height + 4f, 16f, Height - 16f - height);
			return new BookRect(x, y, width, height);
		}
	}

	internal static BookRect TrackingPanel => new BookRect(8f, 76f, 772f, 564f);

	internal static BookRect TrackingClear => new BookRect(TrackingPanel.X + TrackingPanel.Width - 16f - 160f, TrackingPanel.Y + 12f, 160f, 32f);

	internal static BookRect TrackingPrevious => new BookRect(TrackingPanel.X + 16f, TrackingPanel.Y + TrackingPanel.Height - 12f - 28f, 40f, 28f);

	internal static BookRect TrackingNext => new BookRect(TrackingPanel.X + TrackingPanel.Width - 16f - 40f, TrackingPrevious.Y, 40f, 28f);

	internal static BookRect TrackingPager => new BookRect(TrackingPrevious.X + 48f, TrackingPrevious.Y, TrackingNext.X - TrackingPrevious.X - 96f, 28f);

	// The selected goal lives in the same right-hand column as the item details on the other tabs.
	internal static BookRect TrackingRemove => new BookRect(DetailsPanel.X + 16f, DetailsPanel.Y + DetailsPanel.Height - 16f - 32f, DetailsPanel.Width - 32f, 32f);

	internal static BookRect TrackingDetailPrevious => new BookRect(DetailsPanel.X + 16f, TrackingRemove.Y - 8f - 28f, 40f, 28f);

	internal static BookRect TrackingDetailNext => new BookRect(DetailsPanel.X + DetailsPanel.Width - 16f - 40f, TrackingDetailPrevious.Y, 40f, 28f);

	internal static BookRect TrackingDetailPager => new BookRect(TrackingDetailPrevious.X + 48f, TrackingDetailPrevious.Y, TrackingDetailNext.X - TrackingDetailPrevious.X - 96f, 28f);

	// With Magic Storage the craft row is [star][source chip][Craft]; without it, [star][Craft].
	internal static BookRect CraftSourceChip => new BookRect(852f, 564f, 120f, 32f);

	internal static BookRect CraftActionBesideSource => new BookRect(980f, 564f, 116f, 32f);

	// Storage tab: bulk inventory actions sit in the strip above the grid (four buttons). The details panel, top to
	// bottom: label + status pill; what the book is (or could be) connected to; one hint line; the Connect /
	// Disconnect toggle; the selected stack; the last action; "Open Magic Storage" pinned to the bottom.
	internal static BookRect StorageQuickStack => StorageToolbarButton(0);

	internal static BookRect StorageDepositAll => StorageToolbarButton(1);

	internal static BookRect StorageRestock => StorageToolbarButton(2);

	internal static BookRect StorageDeposit => StorageToolbarButton(3);

	internal static BookRect StorageContent => new BookRect(DetailsPanel.X + 16f, DetailsPanel.Y + 16f, DetailsPanel.Width - 32f, DetailsPanel.Height - 32f);

	internal static BookRect StorageStatus => new BookRect(StorageContent.X + StorageContent.Width - 160f, StorageContent.Y, 160f, 20f);

	internal static BookRect StorageSource => new BookRect(StorageContent.X, StorageContent.Y + 32f, StorageContent.Width, 24f);

	internal static BookRect StorageHint => new BookRect(StorageContent.X, StorageContent.Y + 60f, StorageContent.Width, 20f);

	internal static BookRect StorageConnect => new BookRect(StorageContent.X, StorageContent.Y + 88f, StorageContent.Width, 32f);

	internal static BookRect StorageItemIcon => new BookRect(StorageContent.X, StorageContent.Y + 152f, 48f, 48f);

	internal static BookRect StorageMessage => new BookRect(StorageContent.X, StorageContent.Y + 216f, StorageContent.Width, 20f);

	internal static BookRect StorageNative => new BookRect(StorageContent.X, StorageContent.Y + StorageContent.Height - 32f, StorageContent.Width, 32f);

	internal static BookRect Help => new BookRect(934f, 660f, 40f, 24f);

	internal static BookRect HelpPanel => new BookRect(40f, 64f, 1040f, 568f);

	internal static BookRect HelpClose => new BookRect(HelpPanel.X + HelpPanel.Width - 56f, HelpPanel.Y + 24f, 32f, 32f);

	internal static BookRect HeaderClose => new BookRect(1056f, 12f, 40f, 40f);

	internal static BookRect Header => new BookRect(0f, 0f, 1120f, 64f);

	internal static BookRect Frame => new BookRect(0f, 0f, 1120f, 696f);

	// The six-dot grip left of the title, with some room around it.
	internal static BookRect WindowGrip => new BookRect(12f, 14f, 22f, 36f);

	internal static BookRect WindowSizeDecrease => new BookRect(24f, 660f, 24f, 24f);

	internal static BookRect WindowSizeIncrease => new BookRect(100f, 660f, 24f, 24f);

	internal static BookRect WindowSizeLabel => new BookRect(52f, 660f, 44f, 24f);

	// The search narrows and the dropdowns shift left by this much where the bar has one more toggle (the
	// Bestiary tab's Met filter); set by the book's layout.
	internal static float FilterInset;

	internal static BookRect Search => new BookRect(212f, 76f, 240f - FilterInset, 36f);

	internal static BookRect SearchText => new BookRect(246f, 80f, 168f - FilterInset, 28f);

	internal static BookRect SearchClear => new BookRect(420f - FilterInset, 80f, 28f, 28f);

	internal static BookRect ModFilter => new BookRect(460f - FilterInset, 76f, 152f, 36f);

	internal static BookRect FavoritesFilter => new BookRect(744f, 76f, 36f, 36f);

	internal static BookRect MetFilter => new BookRect(700f, 76f, 36f, 36f);

	internal static BookRect Sort => new BookRect(620f - FilterInset, 76f, 116f, 36f);

	internal static BookRect SortMenuPanel => new BookRect(604f - FilterInset, 116f, 176f, 200f);

	// Storage keeps a toolbar between the filter bar and its grid.
	internal static BookRect StorageToolbar => new BookRect(212f, GridTop, 568f, 68f);

	internal const int Width = 1120;

	internal const int Height = 696;

	internal const int Unit = 4;

	internal const int Gap = 8;

	internal const int Inset = 12;

	internal const int PanelPadding = 16;

	internal const int SectionGap = 24;

	internal const int VisibleCategoryRows = 14;

	internal const int VisibleCategoryRowsWithoutClass = 18;

	internal const int TabCount = 6;

	internal const int TrackingPageSize = 7;

	internal static BookRect AcquisitionIcon(bool back)
	{
		return new BookRect(AcquisitionPanel.X + (back ? 72f : 24f), AcquisitionPanel.Y + 16f, 48f, 48f);
	}

	internal static BookRect AcquisitionTitle(bool back, bool pager)
	{
		BookRect icon = AcquisitionIcon(back);
		float right = (pager ? AcquisitionVariantPrevious.X : AcquisitionClose.X) - 16f;
		return new BookRect(icon.X + icon.Width + 12f, AcquisitionPanel.Y + 16f, right - icon.X - icon.Width - 12f, 26f);
	}

	internal static BookRect AcquisitionSubtitle(bool back, bool pager)
	{
		BookRect title = AcquisitionTitle(back, pager);
		return new BookRect(title.X, title.Y + 30f, title.Width, 20f);
	}

	// Six 56-pixel rows with 8-pixel gaps fill AcquisitionList exactly.
	internal static BookRect AcquisitionRow(int index)
	{
		return new BookRect(AcquisitionList.X, AcquisitionList.Y + (float)(index * 64), AcquisitionList.Width, 56f);
	}

	internal static BookRect AcquisitionSourceIcon(int index)
	{
		return AcquisitionRow(index).InsetIcon(8f, 40f);
	}

	// The whole row is the button; the chevron only marks rows that open something.
	internal static BookRect AcquisitionSourceAction(int index)
	{
		BookRect row = AcquisitionRow(index);
		return new BookRect(row.X + row.Width - 36f, row.Y + 12f, 24f, 32f);
	}

	internal static BookRect AcquisitionAmount(int index)
	{
		BookRect action = AcquisitionSourceAction(index);
		return new BookRect(action.X - 76f, action.Y, 68f, 32f);
	}

	internal static BookRect AcquisitionChance(int index)
	{
		BookRect amount = AcquisitionAmount(index);
		return new BookRect(amount.X - 84f, amount.Y, 76f, 32f);
	}

	internal static BookRect AcquisitionName(int index)
	{
		BookRect row = AcquisitionRow(index);
		return new BookRect(row.X + 60f, row.Y + 8f, AcquisitionChance(index).X - row.X - 72f, 22f);
	}

	internal static BookRect AcquisitionDetail(int index)
	{
		BookRect name = AcquisitionName(index);
		return new BookRect(name.X, name.Y + 24f, name.Width, 20f);
	}

	// The Bestiary tab's mob card in the right panel (812..1096): the nav row (back, Loot | Stats), the Bestiary's
	// 189×108 portrait at 4/3 size, name with the variants pager, kind · mod, Stage/Biome/Spawns, then four loot
	// rows or eight stat rows, and the bottom row: favorite star and the loot pager.
	internal static BookRect MobPortrait => new BookRect(828f, 124f, 252f, 144f);

	internal static BookRect MobName(bool pager)
	{
		return new BookRect(812f, 272f, pager ? 176f : 284f, 24f);
	}

	internal static BookRect MobSubtitle => new BookRect(812f, 296f, 284f, 20f);

	internal static BookRect MobVariantPrevious => new BookRect(996f, 270f, 28f, 28f);

	internal static BookRect MobVariantLabel => new BookRect(1024f, 270f, 44f, 28f);

	internal static BookRect MobVariantNext => new BookRect(1068f, 270f, 28f, 28f);

	// Stage, Biome, Spawns and Met.
	internal static BookRect MobTags => new BookRect(812f, 320f, 284f, 80f);

	internal const float MobTagStep = 18.5f;

	internal static BookRect MobBody => new BookRect(812f, 404f, 284f, 184f);

	internal const int MobLootRows = 4;

	internal static BookRect MobLootRow(int index)
	{
		return new BookRect(812f, 404f + (float)(index * 46), 284f, 42f);
	}

	internal static BookRect MobStatRow(int index)
	{
		return new BookRect(820f, 404f + (float)(index * 23), 268f, 23f);
	}

	internal static BookRect MobFavorite => new BookRect(812f, 598f, 32f, 32f);

	internal static BookRect MobPagePrevious => new BookRect(996f, 600f, 28f, 28f);

	internal static BookRect MobPageLabel => new BookRect(1024f, 600f, 44f, 28f);

	internal static BookRect MobPageNext => new BookRect(1068f, 600f, 28f, 28f);
	// Quick Stack gets the widest slot (its Russian label is the longest), Restock the narrowest; gaps stay 8.
	private static readonly float[] StorageToolbarExtra = new float[4] { 16f, 0f, -16f, 0f };

	internal static BookRect StorageToolbarButton(int index)
	{
		BookRect strip = StorageToolbar;
		float width = (strip.Width - 24f - 24f) / 4f;
		float x = strip.X + 12f;
		for (int i = 0; i < index; i++)
		{
			x += width + StorageToolbarExtra[i] + 8f;
		}
		return new BookRect(x, strip.Y + 12f, width + StorageToolbarExtra[index], strip.Height - 24f);
	}

	internal static BookRect ModOption(int index)
	{
		BookRect panel = ModMenuPanel;
		return new BookRect(panel.X + 8f, panel.Y + 8f + (float)(index * 32), panel.Width - 24f, 32f);
	}

	internal static BookGeometry Create(float uiWidth, float uiHeight, float uiScale, int percent)
	{
		return Create(uiWidth, uiHeight, uiScale, percent, 100, hasCustomPosition: false, 0.5f, 0.5f);
	}

	internal static BookGeometry Create(float uiWidth, float uiHeight, float uiScale, int percent, int userScalePercent, bool hasCustomPosition, float positionX, float positionY)
	{
		float desired = (float)Math.Clamp(percent, 60, 150) / 100f * (float)Math.Clamp(userScalePercent, 70, 130) / 100f / Math.Max(0.01f, uiScale);
		float fit = Math.Min((uiWidth - 32f) / 1120f, (uiHeight - 32f) / 696f);
		float scale = Math.Max(0.01f, Math.Min(desired, fit));
		float left;
		float top;
		if (hasCustomPosition)
		{
			float travelX = Math.Max(0f, uiWidth - 32f - 1120f * scale);
			float travelY = Math.Max(0f, uiHeight - 32f - 696f * scale);
			left = 16f + travelX * NormalizePosition(positionX);
			top = 16f + travelY * NormalizePosition(positionY);
		}
		else
		{
			left = (uiWidth - 1120f * scale) / 2f;
			top = (uiHeight - 696f * scale) / 2f;
		}
		return new BookGeometry(left, top, scale);
	}

	private static float NormalizePosition(float value)
	{
		if (!float.IsFinite(value))
		{
			return 0.5f;
		}
		return Math.Clamp(value, 0f, 1f);
	}

	internal (float X, float Y) ToLocal(float uiX, float uiY)
	{
		return (X: (uiX - Left) / Scale, Y: (uiY - Top) / Scale);
	}

	// Sidebar: CLASS label, the class buttons, CATEGORIES label, then the rows; same 22-pixel label-to-content step.
	internal const float ClassLabelTop = 86f;

	internal const float CategoryLabelTop = 192f;

	// Without the class row (the Bestiary tab) the list starts where the class row would.
	internal static BookRect CategoryCell(int visibleIndex, bool classRow)
	{
		return new BookRect(12f, (classRow ? 214 : 108) + visibleIndex * 28, 172f, 28f);
	}

	// Two rows of three 52×32 buttons with 8-pixel gaps, as wide as a category row (five buttons: 3 + 2).
	internal static BookRect ClassButton(int index)
	{
		return new BookRect(12 + index % 3 * 60, 108 + index / 3 * 40, 52f, 32f);
	}

	internal static BookRect TrackingRow(int index)
	{
		return new BookRect(TrackingPanel.X + 16f, TrackingPanel.Y + 56f + (float)(index * 64), TrackingPanel.Width - 32f, 56f);
	}


	internal const float GridTop = 124f;

	internal const float StorageGridTop = 204f;

	internal static BookRect GridCell(int index, float top)
	{
		return new BookRect(212 + index % 8 * 72, top + index / 8 * 72, 64f, 64f);
	}

	internal const float TabTop = 12f;

	internal const float TabHeight = 40f;

	internal const float TabRight = 1044f;

	internal static BookRect SortOption(int index)
	{
		return new BookRect(SortMenuPanel.X + 4f, SortMenuPanel.Y + 4f + index * 32, SortMenuPanel.Width - 8f, 32f);
	}

	// Detail panel: header (icon, name, subtitle) and the recipe block below it, whose top depends on
	// whether the header shows one item or an armor set.
	internal static BookRect CardBack => new BookRect(812f, 88f, 28f, 28f);

	internal static BookRect CardMode => new BookRect(848f, 88f, 248f, 28f);

	internal static BookRect DetailHeaderIcon => new BookRect(812f, 128f, 56f, 56f);

	internal static BookRect DetailHeaderName => new BookRect(880f, 132f, 216f, 26f);

	internal static BookRect DetailHeaderSub => new BookRect(880f, 160f, 216f, 20f);

	internal const float RecipeTop = 204f;

	internal const float ArmorRecipeTop = 280f;

	internal static BookRect ArmorTile(int slot)
	{
		return new BookRect(812 + slot * 98, 128f, 88f, 56f);
	}

	internal static BookRect ArmorTitle => new BookRect(812f, 194f, 284f, 24f);

	internal static BookRect ArmorSubtitle => new BookRect(812f, 220f, 284f, 20f);

	internal static BookRect ArmorStats => new BookRect(812f, 242f, 284f, 20f);

	internal static BookRect RecipeLine(float top)
	{
		return new BookRect(812f, top, 216f, 28f);
	}

	internal static BookRect VariantPrevious(float top)
	{
		return new BookRect(1036f, top, 28f, 28f);
	}

	internal static BookRect VariantNext(float top)
	{
		return new BookRect(1068f, top, 28f, 28f);
	}

	internal static BookRect BatchBar(float top)
	{
		return new BookRect(812f, top + 36f, 284f, 28f);
	}

	internal static BookRect SelectedItemObtain(float top)
	{
		return new BookRect(812f, top + 72f, 284f, 36f);
	}

	internal const float DetailListBottom = 552f;

	internal const float DetailListPagedBottom = 516f;

	internal static BookRect DetailListPrevious => new BookRect(812f, 524f, 28f, 28f);

	internal static BookRect DetailListPager => new BookRect(848f, 524f, 212f, 28f);

	internal static BookRect DetailListNext => new BookRect(1068f, 524f, 28f, 28f);

	internal static BookRect DetailListObtain(BookRect row)
	{
		return new BookRect(row.X + row.Width - 28f, row.Y + 4f, 24f, 24f);
	}
}
