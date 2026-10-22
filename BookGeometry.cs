using System;

namespace ModernRecipeBook;

internal readonly record struct BookGeometry(float Left, float Top, float Scale)
{
	internal static BookRect DetailsPanel => new BookRect(796f, 76f, 316f, 564f);

	internal static BookRect CategoryPanel => new BookRect(8f, 76f, 188f, 564f);

	internal static BookRect StageFilter => new BookRect(212f, 120f, 568f, 32f);

	internal static BookRect ArmorDefense => new BookRect(812f, 224f, 284f, 20f);

	internal static BookRect ArmorName => new BookRect(812f, 196f, 284f, 24f);

	internal static BookRect ArmorSource => new BookRect(812f, 248f, 284f, 16f);

	internal static BookRect ArmorPrevious => new BookRect(812f, 156f, 36f, 28f);

	internal static BookRect ArmorNext => new BookRect(1060f, 156f, 36f, 28f);

	internal static BookRect AcquisitionPanel => new BookRect(160f, 80f, 800f, 536f);

	internal static BookRect AcquisitionBack => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 16f, 32f, 24f);

	internal static BookRect AcquisitionClose => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 56f, AcquisitionPanel.Y + 16f, 32f, 32f);

	internal static BookRect AcquisitionVariantPrevious => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 160f, AcquisitionPanel.Y + 52f, 32f, 32f);

	internal static BookRect AcquisitionVariantLabel => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 120f, AcquisitionPanel.Y + 52f, 56f, 32f);

	internal static BookRect AcquisitionVariantNext => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 56f, AcquisitionPanel.Y + 52f, 32f, 32f);

	internal static BookRect AcquisitionInfo => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 488f, 32f, 32f);

	internal static BookRect AcquisitionPrevious => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 184f, AcquisitionPanel.Y + 488f, 40f, 32f);

	internal static BookRect AcquisitionNext => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 64f, AcquisitionPanel.Y + 488f, 40f, 32f);

	internal static BookRect AcquisitionPager => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 136f, AcquisitionPanel.Y + 488f, 64f, 32f);

	internal static BookRect AcquisitionInfoPanel => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 352f, AcquisitionPanel.Width - 48f, 120f);

	internal static BookRect EnemyOpenAction => new BookRect(AcquisitionPanel.X + AcquisitionPanel.Width - 144f, AcquisitionPanel.Y + 52f, 120f, 32f);

	internal static BookRect EnemyPortrait => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 52f, 64f, 64f);

	internal static BookRect EnemyTags => new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 124f, AcquisitionPanel.Width - 48f, 64f);

	internal static BookRect DetailPrevious => new BookRect(812f, 528f, 40f, 32f);

	internal static BookRect DetailNext => new BookRect(1056f, 528f, 40f, 32f);

	internal static BookRect DetailPager => new BookRect(860f, 528f, 188f, 32f);

	internal static BookRect TrackSingleAction => new BookRect(812f, 604f, 172f, 28f);

	internal static BookRect TrackSetAction => new BookRect(992f, 604f, 104f, 28f);

	internal static BookRect TrackAction => new BookRect(812f, 604f, 284f, 28f);

	internal static BookRect FavoriteAction => new BookRect(812f, 564f, 32f, 32f);

	internal static BookRect CraftAction => new BookRect(852f, 564f, 244f, 32f);

	internal static BookRect PagePrevious => new BookRect(212f, 604f, 32f, 32f);

	internal static BookRect PageNext => new BookRect(748f, 604f, 32f, 32f);

	internal static BookRect PageLabel => new BookRect(252f, 604f, 488f, 32f);

	internal static BookRect ModMenuPanel => new BookRect(184f, 200f, 576f, 384f);

	internal static BookRect TrackingPanel => new BookRect(12f, 92f, 768f, 548f);

	internal static BookRect TrackingClear => new BookRect(604f, 108f, 160f, 32f);

	internal static BookRect TrackingPrevious => new BookRect(28f, 608f, 44f, 28f);

	internal static BookRect TrackingNext => new BookRect(720f, 608f, 44f, 28f);

	internal static BookRect TrackingPager => new BookRect(80f, 608f, 632f, 28f);

	internal static BookRect TrackingRemove => new BookRect(812f, 568f, 284f, 28f);

	internal static BookRect TrackingStatus => new BookRect(812f, 604f, 284f, 28f);

	internal static BookRect TrackingQuantity => new BookRect(812f, 268f, 284f, 28f);

	internal static BookRect CraftSource => new BookRect(712f, 660f, 208f, 24f);

	internal static BookRect StorageConnect => new BookRect(812f, 480f, 138f, 40f);

	internal static BookRect StorageDeposit => new BookRect(388f, 180f, 184f, 40f);

	internal static BookRect StorageNative => new BookRect(958f, 480f, 138f, 40f);

	internal static BookRect StorageQuickStack => new BookRect(196f, 180f, 184f, 40f);

	internal static BookRect StorageRestock => new BookRect(580f, 180f, 168f, 40f);

	internal static BookRect StorageDepositAll => new BookRect(812f, 432f, 284f, 40f);

	internal static BookRect StorageDisconnect => new BookRect(812f, 528f, 284f, 40f);

	internal static BookRect Help => new BookRect(934f, 660f, 40f, 24f);

	internal static BookRect HelpPanel => new BookRect(60f, 36f, 1000f, 624f);

	internal static BookRect HelpClose => new BookRect(HelpPanel.X + HelpPanel.Width - 62f, HelpPanel.Y + 16f, 38f, 32f);

	internal static BookRect HeaderClose => new BookRect(1056f, 12f, 40f, 40f);

	internal static BookRect Header => new BookRect(0f, 0f, 1120f, 64f);

	internal static BookRect WindowSizeDecrease => new BookRect(24f, 660f, 24f, 24f);

	internal static BookRect WindowSizeIncrease => new BookRect(100f, 660f, 24f, 24f);

	internal static BookRect WindowSizeLabel => new BookRect(52f, 660f, 44f, 24f);

	internal static BookRect Search => new BookRect(212f, 76f, 240f, 36f);

	internal static BookRect SearchText => new BookRect(246f, 80f, 168f, 28f);

	internal static BookRect SearchClear => new BookRect(420f, 80f, 28f, 28f);

	internal static BookRect ModFilter => new BookRect(460f, 76f, 152f, 36f);

	internal static BookRect FavoritesFilter => new BookRect(744f, 76f, 36f, 36f);

	internal static BookRect Sort => new BookRect(620f, 76f, 116f, 36f);

	internal static BookRect SortMenuPanel => new BookRect(604f, 116f, 176f, 200f);

	internal static BookRect SamplePanel => new BookRect(212f, 164f, 568f, 68f);

	internal static BookRect SampleSlot => new BookRect(212f, 164f, 48f, 48f);

	internal static BookRect SampleClear => new BookRect(248f, 158f, 18f, 18f);

	internal const int Width = 1120;

	internal const int Height = 696;

	internal const int Unit = 4;

	internal const int Gap = 8;

	internal const int Inset = 12;

	internal const int PanelPadding = 16;

	internal const int SectionGap = 24;

	internal const int VisibleCategoryRows = 18;

	internal const int TabCount = 6;

	internal const int TrackingColumns = 10;

	internal const int TrackingPageSize = 60;

	internal static BookRect ArmorPart(int slot)
	{
		return new BookRect(856 + slot * 68, 140f, 60f, 52f);
	}

	internal static BookRect ArmorPartDefense(int slot)
	{
		BookRect part = ArmorPart(slot);
		return new BookRect(part.X + 4f, part.Y + 34f, part.Width - 8f, 16f);
	}

	internal static BookRect DetailRow(int index)
	{
		return new BookRect(812f, 304 + index * 72, 284f, 64f);
	}

	internal static BookRect DetailObtain(int index)
	{
		BookRect row = DetailRow(index);
		return new BookRect(row.X + row.Width - 8f - 32f, row.Y + (row.Height - 32f) / 2f, 32f, 32f);
	}

	internal static BookRect AcquisitionRow(int index)
	{
		return new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 96f + (float)(index * 64), AcquisitionPanel.Width - 48f, 56f);
	}

	internal static BookRect AcquisitionSourceIcon(int index)
	{
		return AcquisitionRow(index).InsetIcon(8f, 40f);
	}

	internal static BookRect AcquisitionSourceAction(int index)
	{
		BookRect row = AcquisitionRow(index);
		return new BookRect(row.X + row.Width - 44f, row.Y + 12f, 32f, 32f);
	}

	internal static BookRect AcquisitionAmount(int index)
	{
		BookRect action = AcquisitionSourceAction(index);
		return new BookRect(action.X - 72f, action.Y, 64f, 32f);
	}

	internal static BookRect AcquisitionChance(int index)
	{
		BookRect amount = AcquisitionAmount(index);
		return new BookRect(amount.X - 88f, amount.Y, 80f, 32f);
	}

	internal static BookRect AcquisitionName(int index)
	{
		BookRect row = AcquisitionRow(index);
		return new BookRect(row.X + 60f, row.Y + 8f, AcquisitionChance(index).X - row.X - 72f, 20f);
	}

	internal static BookRect EnemyLootRow(int index)
	{
		return new BookRect(AcquisitionPanel.X + 24f, AcquisitionPanel.Y + 224f + (float)(index * 48), AcquisitionPanel.Width - 48f, 40f);
	}

	internal static BookRect EnemyLootInfo(int index)
	{
		BookRect r = EnemyLootRow(index);
		return new BookRect(r.X + r.Width - 44f, r.Y + 4f, 32f, 32f);
	}

	internal static BookRect EnemyLootAmount(int index)
	{
		BookRect r = EnemyLootInfo(index);
		return new BookRect(r.X - 72f, r.Y, 64f, 32f);
	}

	internal static BookRect EnemyLootChance(int index)
	{
		BookRect r = EnemyLootAmount(index);
		return new BookRect(r.X - 88f, r.Y, 80f, 32f);
	}

	internal static BookRect DetailIcon(int index)
	{
		return DetailRow(index).InsetIcon(8f, 36f);
	}

	internal static BookRect ModOption(int index)
	{
		return new BookRect(196f, 212 + index * 36, 552f, 32f);
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

	internal static BookRect CategoryCell(int visibleIndex)
	{
		return new BookRect(12f, 108 + visibleIndex * 28, 172f, 28f);
	}

	internal static BookRect TrackingCell(int index)
	{
		return new BookRect(40 + index % 10 * 72, 176 + index / 10 * 72, 64f, 64f);
	}

	internal static BookRect ChainCell(int index)
	{
		return new BookRect(212 + index % 2 * 292, 288 + index / 2 * 52, 276f, 44f);
	}

	internal static BookRect ChainDrill(int index)
	{
		BookRect cell = ChainCell(index);
		return new BookRect(cell.X + cell.Width - 36f, cell.Y + 8f, 28f, 28f);
	}

	internal static BookRect ChainCrumb(int visible)
	{
		return new BookRect(212 + visible * 144, 220f, 136f, 32f);
	}

	internal static BookRect RecipeCell(int index)
	{
		return GridCell(index, GridTop);
	}

	// Storage keeps its toolbar band (SamplePanel) above the grid, so its grid starts lower.
	internal const float GridTop = 164f;

	internal const float StorageGridTop = 244f;

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
	internal static BookRect DetailHeaderIcon => new BookRect(812f, 92f, 56f, 56f);

	internal static BookRect DetailHeaderName => new BookRect(880f, 96f, 216f, 26f);

	internal static BookRect DetailHeaderSub => new BookRect(880f, 124f, 216f, 20f);

	internal const float RecipeTop = 168f;

	internal const float ArmorRecipeTop = 244f;

	internal static BookRect ArmorTile(int slot)
	{
		return new BookRect(812 + slot * 98, 92f, 88f, 56f);
	}

	internal static BookRect ArmorTitle => new BookRect(812f, 158f, 284f, 24f);

	internal static BookRect ArmorSubtitle => new BookRect(812f, 184f, 284f, 20f);

	internal static BookRect ArmorStats => new BookRect(812f, 206f, 284f, 20f);

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
