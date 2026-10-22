using System;

namespace ModernRecipeBook;

internal readonly record struct BookGeometry(float Left, float Top, float Scale)
{
	internal static BookRect DetailsPanel => new BookRect(796f, 92f, 316f, 548f);

	internal static BookRect CategoryPanel => new BookRect(4f, 92f, 160f, 548f);

	internal static BookRect StageFilter => new BookRect(312f, 24f, 148f, 40f);

	internal static BookRect ArmorDefense => new BookRect(812f, 224f, 284f, 20f);

	internal static BookRect ArmorName => new BookRect(812f, 196f, 284f, 24f);

	internal static BookRect ArmorSource => new BookRect(812f, 248f, 284f, 16f);

	internal static BookRect ArmorPrevious => new BookRect(812f, 156f, 36f, 28f);

	internal static BookRect ArmorNext => new BookRect(1060f, 156f, 36f, 28f);

	internal static BookRect TabPanel => new BookRect(472f, 16f, 572f, 56f);

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

	internal static BookRect VariantPrevious => new BookRect(812f, 148f, 36f, 40f);

	internal static BookRect VariantNext => new BookRect(1060f, 148f, 36f, 40f);

	internal static BookRect SelectedItemObtain => new BookRect(812f, 456f, 284f, 40f);

	internal static BookRect TrackSingleAction => new BookRect(812f, 604f, 164f, 28f);

	internal static BookRect TrackSetAction => new BookRect(984f, 604f, 112f, 28f);

	internal static BookRect TrackAction => new BookRect(812f, 604f, 284f, 28f);

	internal static BookRect FavoriteAction => new BookRect(812f, 568f, 128f, 28f);

	internal static BookRect CraftAction => new BookRect(948f, 568f, 148f, 28f);

	internal static BookRect CategoryPrevious => new BookRect(12f, 612f, 44f, 28f);

	internal static BookRect CategoryNext => new BookRect(108f, 612f, 44f, 28f);

	internal static BookRect CategoryPager => new BookRect(64f, 612f, 36f, 28f);

	internal static BookRect PagePrevious => new BookRect(184f, 612f, 44f, 28f);

	internal static BookRect PageNext => new BookRect(716f, 612f, 44f, 28f);

	internal static BookRect PageLabel => new BookRect(280f, 612f, 428f, 28f);

	internal static BookRect ModMenuPanel => new BookRect(184f, 200f, 576f, 384f);

	internal static BookRect TrackingPanel => new BookRect(12f, 92f, 768f, 548f);

	internal static BookRect TrackingClear => new BookRect(TrackingPanel.X + TrackingPanel.Width - 16f - 160f, TrackingPanel.Y + 12f, 160f, 32f);

	internal static BookRect TrackingPrevious => new BookRect(TrackingPanel.X + 16f, TrackingPanel.Y + TrackingPanel.Height - 12f - 28f, 40f, 28f);

	internal static BookRect TrackingNext => new BookRect(TrackingPanel.X + TrackingPanel.Width - 16f - 40f, TrackingPrevious.Y, 40f, 28f);

	internal static BookRect TrackingPager => new BookRect(TrackingPrevious.X + 48f, TrackingPrevious.Y, TrackingNext.X - TrackingPrevious.X - 96f, 28f);

	// The selected goal lives in the same right-hand column as the item details on the other tabs.
	internal static BookRect TrackingRemove => new BookRect(DetailsPanel.X + 16f, DetailsPanel.Y + DetailsPanel.Height - 16f - 32f, DetailsPanel.Width - 32f, 32f);

	internal static BookRect TrackingDetailPrevious => new BookRect(DetailsPanel.X + 16f, TrackingRemove.Y - 8f - 28f, 40f, 28f);

	internal static BookRect TrackingDetailNext => new BookRect(DetailsPanel.X + DetailsPanel.Width - 16f - 40f, TrackingDetailPrevious.Y, 40f, 28f);

	internal static BookRect TrackingDetailPager => new BookRect(TrackingDetailPrevious.X + 48f, TrackingDetailPrevious.Y, TrackingDetailNext.X - TrackingDetailPrevious.X - 96f, 28f);

	internal static BookRect CraftSource => new BookRect(558f, 660f, 360f, 24f);

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

	internal static BookRect HeaderClose => new BookRect(1060f, 24f, 40f, 40f);

	internal static BookRect DragHandle => new BookRect(24f, 660f, 52f, 24f);

	internal static BookRect WindowSizeDecrease => new BookRect(84f, 660f, 24f, 24f);

	internal static BookRect WindowSizeIncrease => new BookRect(164f, 660f, 24f, 24f);

	internal static BookRect WindowSizeLabel => new BookRect(116f, 660f, 40f, 24f);

	internal static BookRect Search => new BookRect(184f, 108f, 212f, 48f);

	internal static BookRect SearchText => new BookRect(196f, 116f, 152f, 32f);

	internal static BookRect SearchClear => new BookRect(356f, 116f, 32f, 32f);

	internal static BookRect ModFilter => new BookRect(404f, 108f, 140f, 48f);

	internal static BookRect FavoritesFilter => new BookRect(552f, 108f, 44f, 48f);

	internal static BookRect Refresh => new BookRect(604f, 108f, 36f, 48f);

	internal static BookRect Sort => new BookRect(648f, 108f, 56f, 48f);

	internal static BookRect SortDirection => new BookRect(712f, 108f, 48f, 48f);

	internal static BookRect SamplePanel => new BookRect(184f, 168f, 576f, 64f);

	internal static BookRect SampleSlot => new BookRect(196f, 176f, 48f, 48f);

	internal static BookRect SampleOpen => new BookRect(580f, 180f, 128f, 40f);

	internal static BookRect SampleClear => new BookRect(716f, 184f, 32f, 32f);

	internal static BookRect ChainBack => new BookRect(184f, 612f, 40f, 28f);

	internal static BookRect ChainPrev => new BookRect(232f, 612f, 40f, 28f);

	internal const int Width = 1120;

	internal const int Height = 696;

	internal const int Unit = 4;

	internal const int Gap = 8;

	internal const int Inset = 12;

	internal const int PanelPadding = 16;

	internal const int SectionGap = 24;

	internal const int CategoryColumns = 3;

	internal const int VisibleCategoryRows = 10;

	internal const int TabCount = 6;

	internal const int TrackingPageSize = 7;

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
		return new BookRect(12 + visibleIndex % 3 * 48, 124 + visibleIndex / 3 * 48, 44f, 44f);
	}

	internal static BookRect Tab(int index)
	{
		return new BookRect(480 + index * 94, 24f, 86f, 40f);
	}

	internal static BookRect TrackingRow(int index)
	{
		return new BookRect(TrackingPanel.X + 16f, TrackingPanel.Y + 56f + (float)(index * 64), TrackingPanel.Width - 32f, 56f);
	}

	internal static BookRect BatchChoice(int index)
	{
		return new BookRect(896 + index * 52, 268f, 44f, 28f);
	}

	internal static BookRect ChainFilter(int index)
	{
		return new BookRect(184 + index * 292, 244f, 284f, 32f);
	}

	internal static BookRect ChainCell(int index)
	{
		return new BookRect(188 + index % 4 * 144, 348 + index / 4 * 80, 136f, 72f);
	}

	internal static BookRect ChainDrill(int index)
	{
		BookRect cell = ChainCell(index);
		return new BookRect(cell.X + cell.Width - 8f - 32f, cell.Y + 8f, 32f, 32f);
	}

	internal static BookRect ChainCrumb(int visible)
	{
		return new BookRect(188 + visible * 144, 284f, 136f, 32f);
	}

	internal static BookRect RecipeCell(int index)
	{
		return new BookRect(188 + index % 8 * 72, 244 + index / 8 * 72, 64f, 64f);
	}
}
