using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// "Before: <boss>" (the stage filter's second level): everything that can be had with every earlier boss in Boss
// Checklist's order down and that boss still alive (BookSteps, BookProgression). The stage menu is one panel: the
// stages on the left, the bosses in three columns by the stage they close, with their map heads; the next boss of
// this world is offered on its own and marked in the columns, downed bosses carry a tick. "Next boss" follows the
// world as bosses fall.
public sealed partial class RecipeBookSystem
{
	private const string NextBossKey = "next";

	// Which boss, as a BookSteps key or NextBossKey, per filter (Storage has its own, like the stage).
	private static string ListStageBoss = NextBossKey;

	private static string StorageStageBoss = NextBossKey;

	private static string StageBoss
	{
		get
		{
			return StorageView ? StorageStageBoss : ListStageBoss;
		}
		set
		{
			if (StorageView)
			{
				StorageStageBoss = value;
			}
			else
			{
				ListStageBoss = value;
			}
		}
	}

	// The step "Before" stood for in the last filter pass, the next boss then, and what the stage filter hid because
	// the model could not place it (said in the footer, never hidden silently).
	private static int StageBefore = int.MaxValue;

	private static int AppliedNextBoss = -1;

	private static int StageUnknownHidden;

	private static int StageBeforeStep()
	{
		if (StageFilter != BookStageFilter.BeforeBoss)
		{
			return int.MaxValue;
		}
		int step = StageBoss == NextBossKey ? -1 : BookSteps.Find(StageBoss);
		return step > 0 ? step : BookSteps.Next();
	}

	// Once per filter pass: the boss "Before" means now.
	private static void ReadStageBefore()
	{
		StageBefore = StageBeforeStep();
		AppliedNextBoss = BookSteps.Next();
		StageUnknownHidden = 0;
	}

	// "Next boss" follows the world, and a multiplayer client gets the spawn gates once its tiles are in: both are
	// looked at twice a second while the book is open.
	private static void RefreshBossFilter()
	{
		if (Main.GameUpdateCount % 30 != 0 || !BookProgression.Built)
		{
			return;
		}
		if (!BookProgression.UsedGates)
		{
			BookSpawnGates.EnsureBuilt();
			if (BookSpawnGates.Built)
			{
				try
				{
					BookProgression.Rebuild(All, RecipesByItem);
				}
				catch (Exception exception)
				{
					ModContent.GetInstance<ModernRecipeBook>().Logger.Error("MRB progression index failed after the spawn gates arrived.", exception);
				}
				UsedInCache.Clear();
				ApplyFilters(clearMessage: false, keepSelection: true);
				return;
			}
		}
		if (StageFilter == BookStageFilter.BeforeBoss && StageBoss == NextBossKey && BookSteps.Next() != AppliedNextBoss)
		{
			ApplyFilters(clearMessage: false, keepSelection: true);
		}
	}

	// The card's stage line: the stage before the first boss and for the Wall of Flesh and Moon Lord, else the boss.
	private static string StepName(int step)
	{
		if (step < 0)
		{
			return StageName(BookStage.Unknown);
		}
		if (step == 0 || step == BookSteps.HardmodeStep || step == BookSteps.MoonLordStep)
		{
			return StageName(BookSteps.StageOf(step));
		}
		return T("После: ", "After ") + BookSteps.Name(step);
	}

	private static string StageFilterLabel()
	{
		switch (StageFilter)
		{
			case BookStageFilter.BeforeBoss:
			{
				int step = StageBeforeStep();
				return step < BookSteps.All.Count ? T("До: ", "Before: ") + BookSteps.Name(step) : T("Все боссы побеждены", "Every boss is down");
			}
			case BookStageFilter.Unknown:
				return UnknownStageLabel;
			default:
				return StageLabels()[(int)StageFilter];
		}
	}

	private static string UnknownStageLabel => T("Этап неизвестен", "Unknown stage");

	// The dropdown in the header: the boss's head next to "Before: X", shrunk to fit so the tabs never move.
	private static void DrawStageDropdown(SpriteBatch sb, BookRect rect)
	{
		bool before = StageFilter == BookStageFilter.BeforeBoss;
		Dropdown(sb, rect, before ? "" : StageFilterLabel(), StageMenu, enabled: true, highlight: StageFilter != BookStageFilter.All);
		if (before)
		{
			int step = StageBeforeStep();
			DrawBossHead(sb, step, new BookRect(rect.X + 8f, rect.Y + (rect.Height - 20f) / 2f, 20f, 20f), 1f);
			CenterLabel(sb, StageFilterLabel(), rect.X + 32f, rect.Y + 4f, rect.Width - 60f, rect.Height - 8f, Accent, 0.85f, rightAligned: false, leftAligned: true, fitWidth: true);
		}
	}

	private static string StageDropdownTip()
	{
		string tip = T("Этап для всех вкладок · Alt+1…5", "Stage for every tab · Alt+1…5") + "\n" + T("Самый ранний этап получения; учитывается сложность мира", "Earliest stage you can get an item; world difficulty counts");
		if (StageFilter == BookStageFilter.BeforeBoss)
		{
			tip += "\n" + T("Показано то, что можно получить, пока этот босс жив, а все боссы до него побеждены", "Shows what you can get with every earlier boss down and this one still alive");
		}
		return tip;
	}

	// The boss's map head (NPC.GetBossHeadTextureIndex), else its first frame as the Bestiary draws it.
	private static void DrawBossHead(SpriteBatch sb, int step, BookRect rect, float opacity)
	{
		int npc = BookSteps.HeadNpc(step, out int head);
		if (head >= 0)
		{
			Texture2D texture = TextureAssets.NpcHeadBoss[head].Value;
			float scale = Math.Min(rect.Width / Math.Max(1, texture.Width), rect.Height / Math.Max(1, texture.Height));
			sb.Draw(texture, Origin + new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f) * Scale, null, Color.White * opacity * ModalOpacity, 0f, texture.Size() / 2f, scale * Scale, SpriteEffects.None, 0f);
		}
		else if (npc != 0)
		{
			DrawNpcIcon(sb, npc, rect, opacity);
		}
	}

	// The menu's rows, laid out once per frame while it is open: stage options (Value: the filter), the next boss
	// (Value: its step) and the bosses (Value: their step), each with its rectangle.
	private readonly record struct StageMenuRow(BookRect Rect, int Kind, int Value);

	private const int StageRowOption = 0, StageRowNext = 1, StageRowBoss = 2;

	private static readonly List<StageMenuRow> StageMenuRows = new List<StageMenuRow>();

	private static readonly int[] StageColumnRows = new int[3];

	private const float BossRowHeight = 26f;

	private static BookRect StageMenuPanel;

	private static float StageColumnWidth;

	private static void LayoutStageMenu()
	{
		StageMenuRows.Clear();
		BookRect anchor = StageDropdownRect();
		IReadOnlyList<BookSteps.Step> steps = BookSteps.All;
		float left = Math.Max(anchor.Width, 200f);
		StageColumnWidth = 160f;
		Array.Clear(StageColumnRows);
		for (int k = 1; k < steps.Count; k++)
		{
			int column = (int)BookSteps.StageOf(k - 1);
			StageColumnRows[column]++;
			StageColumnWidth = Math.Max(StageColumnWidth, Math.Min(230f, TextWidth(BookSteps.Name(k), 0.8f) + 56f));
		}
		int columns = 0;
		foreach (int rows in StageColumnRows)
		{
			columns += rows > 0 ? 1 : 0;
		}
		float x = anchor.X;
		float y = anchor.Y + anchor.Height + 4f;
		int tallest = Math.Max(StageColumnRows[0], Math.Max(StageColumnRows[1], StageColumnRows[2]));
		float height = Math.Max(16f + 6 * 32f + 72f, 40f + tallest * BossRowHeight + 12f);
		float width = left + 16f + columns * (StageColumnWidth + 8f);
		x = Math.Clamp(x, 8f, 1112f - width);
		StageMenuPanel = new BookRect(x, y, width, height);
		for (int option = 0; option < 6; option++)
		{
			if (option == (int)BookStageFilter.BeforeBoss)
			{
				continue;
			}
			int slot = option < (int)BookStageFilter.BeforeBoss ? option : option - 1;
			StageMenuRows.Add(new StageMenuRow(new BookRect(x + 8f, y + 32f + slot * 32f, left - 12f, 30f), StageRowOption, option));
		}
		int next = BookSteps.Next();
		if (next < steps.Count)
		{
			StageMenuRows.Add(new StageMenuRow(new BookRect(x + 8f, y + 32f + 5 * 32f + 32f, left - 12f, 30f), StageRowNext, next));
		}
		float columnX = x + left + 8f;
		int[] filled = new int[3];
		float[] columnLeft = new float[3];
		for (int c = 0; c < 3; c++)
		{
			columnLeft[c] = columnX;
			if (StageColumnRows[c] > 0)
			{
				columnX += StageColumnWidth + 8f;
			}
		}
		for (int k = 1; k < steps.Count; k++)
		{
			int column = (int)BookSteps.StageOf(k - 1);
			StageMenuRows.Add(new StageMenuRow(new BookRect(columnLeft[column], y + 32f + filled[column]++ * BossRowHeight, StageColumnWidth, BossRowHeight - 2f), StageRowBoss, k));
		}
	}

	private static void DrawStageMenu(SpriteBatch sb)
	{
		LayoutStageMenu();
		BookRect panel = StageMenuPanel;
		Box(sb, panel.X, panel.Y + 4f, panel.Width, panel.Height, new Color(0, 0, 0, 110), border: false, 12f);
		Box(sb, panel, Panel, border: true, 12f, Accent);
		float left = Math.Max(StageDropdownRect().Width, 200f);
		SectionLabel(sb, T("Этап", "Stage"), panel.X + 16f, panel.Y + 10f, left - 24f);
		Box(sb, panel.X + left, panel.Y + 12f, 1f, panel.Height - 24f, Border, border: false, 0f);
		string[] stages = new string[3] { StageName(BookStage.PreHardmode), StageName(BookStage.Hardmode), StageName(BookStage.PostHardmode) };
		bool[] headed = new bool[3];
		int next = BookSteps.Next();
		int chosen = StageFilter == BookStageFilter.BeforeBoss ? StageBeforeStep() : -1;
		string[] labels = StageLabels();
		foreach (StageMenuRow row in StageMenuRows)
		{
			BookRect rect = row.Rect;
			bool hover = rect.Contains(Mouse.X, Mouse.Y);
			switch (row.Kind)
			{
				case StageRowOption:
				{
					bool active = row.Value == (int)StageFilter;
					RowFill(sb, rect, active, hover);
					string label = row.Value == (int)BookStageFilter.Unknown ? UnknownStageLabel : labels[row.Value];
					CenterLabel(sb, label, rect.X + 12f, rect.Y + 4f, rect.Width - 24f, rect.Height - 8f, active ? Accent : TextColor, 0.85f, rightAligned: false, leftAligned: true);
					if (hover && row.Value == (int)BookStageFilter.Unknown)
					{
						MouseTip(T("То, что книга не смогла отнести к этапу: нет обычного источника или он не читается", "What the book could not place: no regular source, or one it cannot read"));
					}
					break;
				}
				case StageRowNext:
				{
					SectionLabel(sb, T("Следующий босс", "Next boss"), rect.X + 8f, rect.Y - 22f, rect.Width - 16f);
					bool active = StageFilter == BookStageFilter.BeforeBoss && StageBoss == NextBossKey;
					RowFill(sb, rect, active, hover);
					DrawBossHead(sb, row.Value, new BookRect(rect.X + 8f, rect.Y + 5f, 20f, 20f), 1f);
					CenterLabel(sb, T("До: ", "Before: ") + BookSteps.Name(row.Value), rect.X + 34f, rect.Y + 4f, rect.Width - 42f, rect.Height - 8f, active ? Accent : TextColor, 0.85f, rightAligned: false, leftAligned: true);
					if (hover)
					{
						MouseTip(T("Первый ещё не побеждённый босс этого мира; фильтр сам перейдёт к следующему · Alt+5", "The first boss not yet down in this world; the filter moves on by itself · Alt+5"));
					}
					break;
				}
				default:
				{
					int column = (int)BookSteps.StageOf(row.Value - 1);
					if (!headed[column])
					{
						headed[column] = true;
						SectionLabel(sb, stages[column], rect.X + 8f, panel.Y + 10f, rect.Width - 16f);
					}
					BookSteps.Step step = BookSteps.All[row.Value];
					bool down = BookSteps.SafeDowned(step);
					bool available = step.Available();
					bool active = row.Value == chosen && StageBoss != NextBossKey;
					RowFill(sb, rect, active, hover);
					if (row.Value == next)
					{
						// The next boss: a bar, not only a color.
						Box(sb, rect.X + 1f, rect.Y + 4f, 3f, rect.Height - 8f, Accent, border: false, 1f);
					}
					DrawBossHead(sb, row.Value, new BookRect(rect.X + 8f, rect.Y + 2f, 20f, 20f), (down || !available) ? 0.55f : 1f);
					Color color = active || row.Value == next ? Accent : ((down || !available) ? Muted : TextColor);
					CenterLabel(sb, BookSteps.Name(row.Value), rect.X + 34f, rect.Y + 3f, rect.Width - 34f - (down ? 22f : 6f), rect.Height - 6f, color, 0.8f, rightAligned: false, leftAligned: true);
					if (down)
					{
						float cx = rect.X + rect.Width - 16f, cy = rect.Y + rect.Height / 2f;
						Line(sb, cx - 4f, cy, cx - 1f, cy + 3f, Muted);
						Line(sb, cx - 1f, cy + 3f, cx + 5f, cy - 4f, Muted);
					}
					if (hover)
					{
						string state = row.Value == next ? T("Следующий босс этого мира", "Next boss in this world") : (down ? T("Уже побеждён", "Already down") : (!available ? T("Не в этом мире: босс другого зла", "Not in this world: the other evil's boss") : T("Ещё не побеждён", "Not down yet")));
						MouseTip(T("До: ", "Before: ") + BookSteps.Name(row.Value) + " · " + state + "\n" + T("Всё, что можно получить, пока он жив, а все боссы до него побеждены", "Everything you can get with every earlier boss down and this one still alive"));
					}
					break;
				}
			}
		}
	}

	private static void RowFill(SpriteBatch sb, BookRect rect, bool active, bool hover)
	{
		if (active)
		{
			Box(sb, rect, SelectedFill, border: false, 6f);
		}
		else if (hover)
		{
			Box(sb, rect, HoverFill, border: false, 6f);
		}
	}

	// A click while the menu is open: the row under the mouse, if any. The menu closes either way.
	private static void ClickStageMenu()
	{
		foreach (StageMenuRow row in StageMenuRows)
		{
			if (!row.Rect.Contains(Mouse.X, Mouse.Y))
			{
				continue;
			}
			switch (row.Kind)
			{
				case StageRowOption:
					StageFilter = (BookStageFilter)row.Value;
					break;
				case StageRowNext:
					StageFilter = BookStageFilter.BeforeBoss;
					StageBoss = NextBossKey;
					break;
				default:
					StageFilter = BookStageFilter.BeforeBoss;
					StageBoss = BookSteps.All[row.Value].Key;
					break;
			}
			Page = (DetailsPage = (Variant = 0));
			ApplyFilters();
			break;
		}
		StageMenu = false;
	}

	// The card's hint for the stage line: the step, then why, from the item down to what fixes it.
	private static string StageExplanation(Item item, Recipe recipe)
	{
		int step = recipe != null ? BookProgression.RecipeStep(recipe.RecipeIndex) : BookProgression.ItemStep(item.type);
		string head = StepName(step);
		if (step > 0 && step != BookSteps.HardmodeStep && step != BookSteps.MoonLordStep)
		{
			head += " · " + StageName(BookSteps.StageOf(step));
		}
		string chain = ChainText(BookProgression.Chain(item.type, recipe?.RecipeIndex ?? -1));
		string lead = recipe != null ? T("Этап рецепта: по материалам, станции и условиям", "Recipe stage: from its materials, station and conditions") : T("Самый ранний этап, когда предмет можно получить", "The earliest stage you can get this item");
		return head + (chain.Length > 0 ? "\n" + chain : "") + "\n" + lead + "\n" + T("Учитывается сложность мира; боссы — в порядке Boss Checklist", "The world's difficulty counts; bosses go in Boss Checklist's order");
	}

	// "Spectre Bar ← Ectoplasm ← drop: Dungeon Spirit": each item through which the step came, then what fixed it.
	private static string ChainText(List<BookProgression.Link> links)
	{
		List<string> parts = new List<string>();
		for (int i = 0; i < links.Count; i++)
		{
			BookProgression.Link link = links[i];
			if (i == 0)
			{
				parts.Add(ItemName(link.Item));
			}
			string phrase = link.Kind switch
			{
				"drop" or "enemy" or "spawn" => T("выпадает: ", "drop: ") + NpcName(link.Id) + LaterThan(BookProgression.NpcStep(link.Id), link.Step),
				"banner" => T("знамя: ", "banner: ") + NpcName(link.Id),
				"catch" => T("сачок: ", "bug net: ") + NpcName(link.Id),
				"shop" => T("продаёт: ", "sold by: ") + NpcName(link.Id) + LaterThan(BookProgression.NpcStep(link.Id), link.Step),
				"list" or "worldgen" => T("есть в мире", "found in the world"),
				"fishing" => T("рыбалка", "fishing"),
				"grows" => T("растёт в мире", "grows in the world"),
				"rarity" => T("по редкости (оценка)", "by rarity (a guess)"),
				"gate" => link.Id <= 0 ? T("с самого начала", "from the start") : StepPhrase(link.Id),
				"recipe" or "bag" or "use" or "record" or "carried" or "mine" or "shimmer" or "trade" or "rod" => null,
				_ => T("источник не найден", "no source found")
			};
			if (phrase != null)
			{
				parts.Add(phrase);
				break;
			}
			if (i + 1 < links.Count && links[i + 1].Kind != "gate")
			{
				string prefix = link.Kind switch
				{
					"mine" => T("кирка: ", "pickaxe: "),
					"shimmer" => T("мерцание: ", "shimmer: "),
					"trade" => T("обмен: ", "trade: "),
					"rod" => T("удочка: ", "rod: "),
					_ => ""
				};
				parts.Add(prefix + ItemName(links[i + 1].Item));
			}
		}
		return string.Join(" ← ", parts);
	}

	// A drop or stock whose own condition comes later than its NPC (Calamity's Hallowed bars from the Twins wait for
	// all three mechanical bosses): " · after Skeletron Prime".
	private static string LaterThan(int source, int step)
	{
		return step > source && step > 0 ? " · " + StepPhrase(step) : "";
	}

	// "after Plantera", "Hardmode", "after Moon Lord".
	private static string StepPhrase(int step)
	{
		if (step == BookSteps.HardmodeStep)
		{
			return StageName(BookStage.Hardmode);
		}
		return T("после: ", "after ") + BookSteps.Name(step);
	}
}
