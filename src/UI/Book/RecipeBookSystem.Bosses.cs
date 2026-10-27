using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ModernRecipeBook.UI.Book;

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
		return L("Stages.After", BookSteps.Name(step));
	}

	private static string StageFilterLabel()
	{
		switch (StageFilter)
		{
			case BookStageFilter.BeforeBoss:
			{
				int step = StageBeforeStep();
				return step < BookSteps.All.Count ? L("Stages.Before", BookSteps.Name(step)) : L("Stages.EveryBossIsDown");
			}
			case BookStageFilter.Unknown:
				return UnknownStageLabel;
			default:
				return StageLabels()[(int)StageFilter];
		}
	}

	private static string UnknownStageLabel => L("Stages.UnknownStage");

	// The dropdown in the header: the boss's head next to "Before: X", shrunk to fit so the tabs never move. The boss's
	// name is a link to its card (RecipeBookSystem.Links); the rest of the dropdown opens the menu. True while the mouse
	// is on the link.
	private static bool DrawStageDropdown(SpriteBatch sb, BookRect rect)
	{
		bool before = StageFilter == BookStageFilter.BeforeBoss;
		if (!before)
		{
			Dropdown(sb, rect, StageFilterLabel(), StageMenu, enabled: true, highlight: StageFilter != BookStageFilter.All);
			return false;
		}
		int step = StageBeforeStep();
		string label = StageFilterLabel();
		BookRect line = new BookRect(rect.X + 32f, rect.Y + 4f, rect.Width - 60f, rect.Height - 8f);
		float size = Math.Min(0.85f, line.Width / Math.Max(1f, Ink(BookRules.PlainText(label)).Width * TextScale) * 0.999f);
		List<LinkSpan> spans = Linked(label, LinkNames.Bosses);
		if (step < BookSteps.All.Count && !spans.Any((LinkSpan s) => s.Link != null))
		{
			spans = Whole(label, MobLink(StepMob(step)));
		}
		List<LinkPiece> pieces = LinkPieces(spans, line, size);
		bool onLink = OnLink(pieces);
		Dropdown(sb, rect, "", StageMenu, enabled: true, highlight: true, hoverable: !onLink);
		DrawBossHead(sb, step, new BookRect(rect.X + 8f, rect.Y + (rect.Height - 20f) / 2f, 20f, 20f), 1f);
		DrawLinkPieces(sb, pieces, line, size, Accent);
		return onLink;
	}

	private static string StageDropdownTip()
	{
		string tip = L("Stages.StageForEveryTabAlt1") + "\n" + L("Stages.EarliestStageYouCanGetAn");
		if (StageFilter == BookStageFilter.BeforeBoss)
		{
			tip += "\n" + L("Stages.ShowsWhatYouCanGetWith");
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
		SectionLabel(sb, L("Stages.Stage"), panel.X + 16f, panel.Y + 10f, left - 24f);
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
						MouseTip(L("Stages.WhatTheBookCouldNotPlace"));
					}
					break;
				}
				case StageRowNext:
				{
					SectionLabel(sb, L("Stages.NextBoss"), rect.X + 8f, rect.Y - 22f, rect.Width - 16f);
					bool active = StageFilter == BookStageFilter.BeforeBoss && StageBoss == NextBossKey;
					RowFill(sb, rect, active, hover);
					DrawBossHead(sb, row.Value, new BookRect(rect.X + 8f, rect.Y + 5f, 20f, 20f), 1f);
					CenterLabel(sb, L("Stages.Before", BookSteps.Name(row.Value)), rect.X + 34f, rect.Y + 4f, rect.Width - 42f, rect.Height - 8f, active ? Accent : TextColor, 0.85f, rightAligned: false, leftAligned: true);
					if (hover)
					{
						MouseTip(L("Stages.TheFirstBossNotYetDown") + "\n" + L("Links.ShiftClickBossCard"));
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
						string state = row.Value == next ? L("Stages.NextBossInThisWorld") : (down ? L("Stages.AlreadyDown") : (!available ? L("Stages.NotInThisWorldTheOther") : L("Stages.NotDownYet")));
						MouseTip(L("Stages.Before", BookSteps.Name(row.Value)) + " · " + state + "\n" + L("Stages.EverythingYouCanGetWithEvery") + "\n" + L("Links.ShiftClickBossCard"));
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

	// A click while the menu is open: the row under the mouse, if any. The menu closes either way. Shift+Click on a
	// boss opens its card instead (RecipeBookSystem.Links), the filter as it was.
	private static void ClickStageMenu()
	{
		KeyboardState keys = Keyboard.GetState();
		bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
		foreach (StageMenuRow row in StageMenuRows)
		{
			if (!row.Rect.Contains(Mouse.X, Mouse.Y))
			{
				continue;
			}
			if (shift && row.Kind != StageRowOption && MobLink(StepMob(row.Value)) is BookLink boss)
			{
				StageMenu = false;
				boss.Follow();
				return;
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
		bool modeExcluded = recipe != null ? BookProgression.RecipeModeExcluded(recipe.RecipeIndex) : BookProgression.ItemModeExcluded(item.type);
		string head = StageName(item, recipe);
		if (step > 0 && step != BookSteps.HardmodeStep && step != BookSteps.MoonLordStep)
		{
			head += " · " + StageName(BookSteps.StageOf(step));
		}
		string chain = modeExcluded ? ItemName(item.type) + " ← " + L("Stages.ItsSourceIsOnlyThereAt") : ChainText(BookProgression.Chain(item.type, recipe?.RecipeIndex ?? -1));
		string lead = recipe != null ? L("Stages.RecipeStageFromItsMaterialsStation") : L("Stages.TheEarliestStageYouCanGet");
		return head + (chain.Length > 0 ? "\n" + chain : "") + "\n" + lead + "\n" + L("Stages.TheWorldSDifficultyCountsBosses");
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
				"drop" or "enemy" or "spawn" => L("Stages.Drop", NpcName(link.Id)) + LaterThan(BookProgression.NpcStep(link.Id), link.Step),
				"banner" => L("Stages.Banner", NpcName(link.Id)),
				"catch" => L("Stages.BugNet", NpcName(link.Id)),
				"shop" => L("Stages.SoldBy", NpcName(link.Id)) + LaterThan(BookProgression.NpcStep(link.Id), link.Step),
				"list" or "worldgen" => L("Stages.FoundInTheWorld"),
				"fishing" => L("Stages.Fishing"),
				"grows" => L("Stages.GrowsInTheWorld"),
				"pickaxe" => L("Stages.NeedsPickaxePower", link.Id) + " · " + StepPhrase(link.Step),
				"rarity" => L("Stages.ByRarityAGuess"),
				"gate" => link.Id <= 0 ? L("Stages.FromTheStart") : StepPhrase(link.Id),
				"recipe" or "bag" or "use" or "record" or "carried" or "mine" or "shimmer" or "trade" or "rod" => null,
				_ => L("Stages.NoSourceFound")
			};
			if (phrase != null)
			{
				parts.Add(phrase);
				break;
			}
			if (i + 1 < links.Count && links[i + 1].Kind != "gate")
			{
				string how = link.Kind switch
				{
					"mine" => "Stages.Pickaxe",
					"shimmer" => "Stages.Shimmer",
					"trade" => "Stages.Trade",
					"rod" => "Stages.Rod",
					_ => null
				};
				string next = ItemName(links[i + 1].Item);
				parts.Add((how != null) ? L(how, next) : next);
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
		return L("Stages.AfterInline", BookSteps.Name(step));
	}
}
