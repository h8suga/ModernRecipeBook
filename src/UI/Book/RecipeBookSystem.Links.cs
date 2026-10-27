using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;

namespace ModernRecipeBook.UI.Book;

// Links (the owner's rule): wherever the book names something that has its own place in it (an item, a mob, a boss, a
// Bestiary category, a mod, a stage, a trait, a tracked goal), the name is a link there. A link is a piece of its line's
// text drawn as the item card's category path draws its parts (CardCategory): bright among the line's muted words, a
// fill under the mouse, a tip naming where it goes. Its click is a step like any other, so History records it and Back
// returns. The link under the mouse is noted while drawing (HoveredLink) and followed by the next click (FollowLink), so
// what lit up is what opens; inside a row with a click of its own, the link takes the click and the row shows no hover.
public sealed partial class RecipeBookSystem
{
	// Where a link goes: Tip names it, Follow goes there; Target is what it opens ("mob:12", "item:3456") when that is a card.
	private sealed record BookLink(string Tip, Action Follow, string Target = null);

	// What the card being drawn shows, as a link's Target: a link there would go nowhere (LinkPieces).
	private static string CardTarget;

	// A piece of a line: plain words, or a link.
	private readonly record struct LinkSpan(string Text, BookLink Link = null);

	// A piece laid out on its line: where its ink starts, the room it has, where it takes the mouse, whether it was cut.
	private readonly record struct LinkPiece(LinkSpan Span, float X, float Width, BookRect Hit, bool Cut);

	// The link under the mouse in the last frame drawn and where it was: the book's, and the tracker's (in the cards' own
	// space, or the screen's for the compact lines), which draws in a layer of its own.
	private static (BookLink Link, BookRect Hit) HoveredLink;

	private static (BookLink Link, BookRect Hit) TrackerLink;

	// The pieces as one line from line.X, each where the whole line's text puts it (as one FixedLabel would draw the line,
	// at the scale it would: a line lower than the text shrinks it), cut where the line ends. A link to what the card
	// shows (CardTarget) would go nowhere and reads as plain words.
	private static List<LinkPiece> LinkPieces(IReadOnlyList<LinkSpan> spans, BookRect line, float size)
	{
		List<LinkPiece> pieces = new List<LinkPiece>();
		float scale = LineScale(line, size);
		string drawn = "";
		float end = line.X + line.Width;
		foreach (LinkSpan shown in spans)
		{
			LinkSpan span = (shown.Link?.Target != null && shown.Link.Target == CardTarget) ? (shown with { Link = null }) : shown;
			drawn += span.Text;
			float right = line.X + Ink(BookRules.PlainText(drawn)).Width * scale;
			float left = right - Ink(BookRules.PlainText(span.Text)).Width * scale;
			if (left >= end - 8f)
			{
				break;
			}
			bool cut = right > end;
			pieces.Add(new LinkPiece(span, left, end - left, new BookRect(left - 4f, line.Y, Math.Min(right, end) - left + 8f, line.Height), cut));
			if (cut)
			{
				break;
			}
		}
		return pieces;
	}

	private static bool OnLink(List<LinkPiece> pieces)
	{
		return !MenuOpen && pieces.Any((LinkPiece p) => p.Span.Link != null && p.Hit.Contains(Mouse.X, Mouse.Y));
	}

	// The pieces drawn: a line without links in color as before; with links, its words in quiet (the line's muted tone)
	// and the links in link. True while the mouse is on a link: its tip replaces the line's own.
	private static bool DrawLinkPieces(SpriteBatch sb, List<LinkPiece> pieces, BookRect line, float size, Color color, Color? quiet = null, Color? link = null)
	{
		bool linked = pieces.Any((LinkPiece p) => p.Span.Link != null);
		bool over = false;
		foreach (LinkPiece piece in pieces)
		{
			if (piece.Span.Link != null)
			{
				NoteLink(piece.Span.Text, piece.Hit);
			}
			bool hover = piece.Span.Link != null && !MenuOpen && piece.Hit.Contains(Mouse.X, Mouse.Y);
			if (hover)
			{
				Box(sb, piece.Hit, HoverFill, border: false, 6f);
			}
			FixedLabel(sb, piece.Span.Text, piece.X, line.Y, (piece.Span.Link != null) ? (link ?? TextColor) : (linked ? (quiet ?? color) : color), size, piece.Width, line.Height);
			if (hover)
			{
				over = true;
				HoveredLink = (piece.Span.Link, piece.Hit);
				MouseTip((piece.Cut ? (piece.Span.Text + "\n") : "") + piece.Span.Link.Tip);
			}
		}
		return over;
	}

	private static bool DrawLinkLine(SpriteBatch sb, IReadOnlyList<LinkSpan> spans, BookRect line, float size, Color color, Color? quiet = null, Color? link = null)
	{
		return DrawLinkPieces(sb, LinkPieces(spans, line, size), line, size, color, quiet, link);
	}

	// A whole row or icon that is a link (a station in the Active Stations popover, a bag in a list): it takes the click.
	private static void RowLink(BookLink link, BookRect rect)
	{
		if (link != null)
		{
			NoteLink(link.Tip, rect);
		}
		if (link != null && !MenuOpen && rect.Contains(Mouse.X, Mouse.Y))
		{
			HoveredLink = (link, rect);
		}
	}

	// Stand: the links the last frame drew and where (screen pixels), for the screenshot scripts:
	// stand.ps1 c call p=ModernRecipeBook.UI.Book.RecipeBookSystem.LinkSpots
	private static readonly List<(uint Frame, string Text, Vector2 At)> LinksDrawn = new List<(uint, string, Vector2)>();

	private static void NoteLink(string text, BookRect hit, bool screen = false)
	{
		// The book under a modal is drawn with the mouse parked off it (DrawBookContent): none of its links is in reach.
		if (Mouse.X < -10000f)
		{
			return;
		}
		uint frame = Main.GameUpdateCount;
		LinksDrawn.RemoveAll(((uint Frame, string Text, Vector2 At) l) => l.Frame != frame);
		Vector2 center = new Vector2(hit.X + hit.Width / 2f, hit.Y + hit.Height / 2f);
		LinksDrawn.Add((frame, text, (screen ? center : (Origin + center * Scale)) * Main.UIScale));
	}

	internal static string LinkSpots()
	{
		LinksDrawn.RemoveAll(((uint Frame, string Text, Vector2 At) l) => l.Frame != Main.GameUpdateCount && l.Frame + 1 != Main.GameUpdateCount);
		return string.Join("\n", LinksDrawn.Select(((uint Frame, string Text, Vector2 At) l) => (int)l.At.X + "\t" + (int)l.At.Y + "\t" + l.Text.Replace("\n", " | ")));
	}

	// A click on the link that was under the mouse goes there; true when one did. The menus fold, the search lets go.
	private static bool FollowLink(ref (BookLink Link, BookRect Hit) hovered, float x, float y)
	{
		(BookLink link, BookRect hit) = hovered;
		hovered = default;
		if (link == null || !hit.Contains(x, y))
		{
			return false;
		}
		SetTyping(value: false);
		ModMenu = (SortMenu = (StageMenu = (StationMenu = (TraitMenu = false))));
		link.Follow();
		return true;
	}

	private static List<LinkSpan> Plain(string text)
	{
		return new List<LinkSpan> { new LinkSpan(text ?? "") };
	}

	private static List<LinkSpan> Whole(string text, BookLink link)
	{
		return new List<LinkSpan> { new LinkSpan(text ?? "", link) };
	}

	// Targets -----------------------------------------------------------------------------------------------------------

	// That item's card, a bag or crate on what it holds (OpenItemCard).
	private static BookLink ItemLink(int type)
	{
		if (type <= 0 || !ContentSamples.ItemsByType.ContainsKey(type))
		{
			return null;
		}
		return new BookLink(L(HasContents(type) ? "Links.ItemContents" : "Links.ItemCard", ItemName(type)), () => OpenItemCard(type), "item:" + type);
	}

	// How to Get of the item, the others a row accepts (a recipe group) on its pager.
	private static BookLink HowToGetLink(int type, IEnumerable<int> alternatives = null)
	{
		return new BookLink(L("Links.HowToGet", ItemName(type)), () => OpenAcquisition(type, alternatives));
	}

	private static BookLink MobLink(int netId)
	{
		int index = BookBestiary.IndexOf(netId);
		if (index < 0)
		{
			return null;
		}
		return new BookLink(L("Links.MobCard", NpcName(netId)), () =>
		{
			CloseAcquisition();
			ShowMob(index, netId);
		}, "mob:" + index);
	}

	// The card a step's boss opens on: its first part with a Bestiary entry, the world's evil picking between the Eater
	// of Worlds and the Brain of Cthulhu (as BookSteps.HeadNpc does); 0 for a step no card has.
	private static int StepMob(int step)
	{
		if (step <= 0 || step >= BookSteps.All.Count)
		{
			return 0;
		}
		IEnumerable<int> npcs = BookSteps.All[step].Npcs;
		if (WorldGen.crimson)
		{
			npcs = npcs.OrderBy((int n) => (n == NPCID.BrainofCthulhu) ? 0 : 1);
		}
		return npcs.FirstOrDefault((int n) => BookBestiary.IndexOf(n) >= 0);
	}

	private static BookLink MobCategoryLink(BookBestiary.Tag tag)
	{
		if (tag == null)
		{
			return null;
		}
		string key = tag.Key;
		return new BookLink(FilterTip(L("Links.MobCategory", BookBestiary.Label(tag)), 2), () => ShowMobs((BookUiState.TabFilters f) => f.CategoryKey = key));
	}

	// A mob's kind as its Bestiary category (BookBestiary's "kind:" tags).
	private static string KindTag(BookMobKind kind)
	{
		return kind switch
		{
			BookMobKind.Boss => "kind:boss",
			BookMobKind.MiniBoss => "kind:miniboss",
			BookMobKind.Town => "kind:town",
			BookMobKind.Critter => "kind:critter",
			_ => "kind:enemy"
		};
	}

	// The mod filter of the card's tab: Items for an item, the Bestiary for a mob.
	private static BookLink ModLink(string mod, bool mobs, int keep = -1)
	{
		return new BookLink(FilterTip(L("Links.ModFilter", ModName(mod), TabLabel(mobs ? 2 : 0)), mobs ? 2 : 0), () =>
		{
			if (mobs)
			{
				ShowMobs((BookUiState.TabFilters f) => f.ModFilter = mod);
			}
			else
			{
				ShowItems((BookUiState.TabFilters f) => f.ModFilter = mod, keep);
			}
		});
	}

	private static BookLink StageLink(BookStage stage, bool mobs, int keep = -1)
	{
		BookStageFilter filter = (BookStageFilter)((int)stage + 1);
		return new BookLink(FilterTip(L("Links.StageFilter", StageName(stage)), mobs ? 2 : 0), () =>
		{
			if (mobs)
			{
				ShowMobs((BookUiState.TabFilters _) => StageFilter = filter);
			}
			else
			{
				ShowItems((BookUiState.TabFilters _) => StageFilter = filter, keep);
			}
		});
	}

	private static BookLink TraitLink(BookTrait trait)
	{
		return new BookLink(FilterTip(L("Links.TraitFilter", BookItemTraits.Label(trait)), 0), () => ShowItems((BookUiState.TabFilters _) => TraitFilter = trait));
	}

	private static BookLink GoalLink(TrackedGoal goal)
	{
		return new BookLink(L("Links.TrackingGoal", TabLabel(5)), () => OpenTrackingGoal(goal));
	}

	// Used in's way out of "Hidden by the stage filter": the open tab's stage filter back to every stage.
	private static BookLink ShowAllStagesLink()
	{
		return new BookLink(L("Links.ShowAllStagesTip") + "\n" + L("Common.PickTheStageAtTheTop"), () =>
		{
			StageFilter = BookStageFilter.All;
			DetailsPage = 0;
			ApplyFilters(clearMessage: false, keepSelection: true);
		});
	}

	// A goal's own card: the item, a set on its body piece, which shows the whole set.
	private static BookLink GoalCardLink(TrackedGoal goal)
	{
		return ItemLink(GoalCardType(goal)) is { } link ? link with { Tip = L("Links.ItemCard", GoalTitle(goal)) } : null;
	}

	private static int GoalCardType(TrackedGoal goal)
	{
		return goal.IsSet ? goal.Parts[1].type : goal.Recipe.createItem.type;
	}

	// What a row's mention stands for (BookMention), its lead line on top of the tip.
	private static BookLink MentionLink(BookMention mention)
	{
		BookLink link = (mention.Item > 0) ? ItemLink(mention.Item) : ((mention.Npc != 0) ? MobLink(mention.Npc) : ((mention.Place != null) ? MobCategoryLink(BookBestiary.FindTag(mention.Place)) : null));
		return (link != null && !string.IsNullOrEmpty(mention.Lead)) ? (link with { Tip = mention.Lead + "\n" + link.Tip }) : link;
	}

	// What a How to Get row's rules name: the detail line shows one rule's label or what they share.
	private static IEnumerable<BookMention> GroupMentions(BookAcquisitionGroup group)
	{
		return group.Rules.SelectMany((BookAcquisitionRule r) => r.Reports).SelectMany((BookAcquisitionEntry e) => e.Mentions ?? Array.Empty<BookMention>());
	}

	private static IEnumerable<(string Name, BookLink Link)> Mentioned(IEnumerable<BookMention> mentions)
	{
		return (mentions ?? Enumerable.Empty<BookMention>()).Select((BookMention m) => (m.Text, MentionLink(m)));
	}

	// A filter link's tip says the search goes too, when the tab it lands on has one (ShowItems, ShowMobs clear it).
	private static string FilterTip(string tip, int tab)
	{
		string query = (CurrentTab == tab) ? Query : (TabFilterSlots.GetValueOrDefault(tab)?.Query ?? "");
		return tip + ((query.Length > 0) ? ("\n" + L("CardCategory.SearchCleared")) : "");
	}

	// Items with the filter changed and no search, which would hide the rest; every other filter stays as the player set
	// it. The card keeps its item (or keep's, a tracked goal's), selected and paged to in the grid when the filters show it.
	private static void ShowItems(Action<BookUiState.TabFilters> change, int keep = -1)
	{
		int type = (keep >= 0) ? keep : (SelectedItem?.type ?? 0);
		bool same = CardItemType == type;
		int variant = Variant;
		int details = DetailsPage;
		BookCardMode mode = CardMode;
		CloseAcquisition();
		if (!Catalogue)
		{
			SwitchTab(0);
		}
		BookUiState.TabFilters filters = LiveTabFilters();
		filters.Query = "";
		change(filters);
		LoadTabFilters(filters);
		SetTyping(value: false);
		Page = 0;
		ApplyFilters();
		ArmorFocus = 0;
		ResetCard();
		if (type <= 0)
		{
			return;
		}
		int position = Filtered.IndexOf(type);
		if (position >= 0)
		{
			Selected = type;
			Page = position / ActivePageSize;
		}
		else
		{
			FocusType = type;
		}
		Variant = same ? variant : 0;
		DetailsPage = same ? details : 0;
		CardMode = mode;
	}

	// The Bestiary with the filter changed and no search; the card keeps its mob.
	private static void ShowMobs(Action<BookUiState.TabFilters> change)
	{
		BookMob mob = CardMob;
		int netId = (mob != null) ? CardVariantNetId(mob) : 0;
		CloseAcquisition();
		if (!BestiaryView)
		{
			SwitchTab(2);
		}
		BookUiState.TabFilters filters = LiveTabFilters();
		filters.Query = "";
		change(filters);
		LoadTabFilters(filters);
		SetTyping(value: false);
		Page = 0;
		ApplyFilters();
		if (mob != null)
		{
			int details = DetailsPage;
			ShowMob(mob.Index, netId);
			DetailsPage = details;
		}
	}

	// Names ---------------------------------------------------------------------------------------------------------

	// What a text can name, found by the name it shows: the bosses (each step's name and its parts' names), the Bestiary's
	// categories (events and biomes, under their label and the game's own name for it), the boss Treasure Bags.
	[Flags]
	private enum LinkNames
	{
		None = 0,
		Bosses = 1,
		Places = 2,
		Bags = 4
	}

	private static readonly LinkNames[] LinkNameKinds = { LinkNames.Bosses, LinkNames.Places, LinkNames.Bags };

	// The spawn probe's own words for vanilla places (PlaceName) and the Bestiary biome each one is.
	private static readonly (string Place, string Tag)[] PlaceTags =
	{
		("Forest", "Surface"), ("Underground", "Underground"), ("Caverns", "Caverns"), ("Jungle", "Jungle"), ("UndergroundJungle", "UndergroundJungle"),
		("Temple", "TheTemple"), ("Snow", "Snow"), ("UndergroundSnow", "UndergroundSnow"), ("Desert", "Desert"), ("UndergroundDesert", "UndergroundDesert"),
		("Ocean", "Ocean"), ("Dungeon", "TheDungeon"), ("Underworld", "TheUnderworld"), ("Corruption", "TheCorruption"), ("Crimson", "Crimson"),
		("Hallow", "TheHallow"), ("Sky", "Sky")
	};

	// The vanilla recipe and shop conditions that ask the player to stand in a Bestiary biome or event (Terraria.Condition:
	// InJungle reads Player.ZoneJungle, and so on).
	private static readonly Dictionary<string, string> ConditionTags = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["Conditions.InDungeon"] = "tag:Bestiary_Biomes.TheDungeon", ["Conditions.InCorrupt"] = "tag:Bestiary_Biomes.TheCorruption",
		["Conditions.InHallow"] = "tag:Bestiary_Biomes.TheHallow", ["Conditions.InMeteor"] = "tag:Bestiary_Biomes.Meteor",
		["Conditions.InJungle"] = "tag:Bestiary_Biomes.Jungle", ["Conditions.InSnow"] = "tag:Bestiary_Biomes.Snow",
		["Conditions.InCrimson"] = "tag:Bestiary_Biomes.Crimson", ["Conditions.InTowerSolar"] = "tag:Bestiary_Biomes.SolarPillar",
		["Conditions.InTowerVortex"] = "tag:Bestiary_Biomes.VortexPillar", ["Conditions.InTowerNebula"] = "tag:Bestiary_Biomes.NebulaPillar",
		["Conditions.InTowerStardust"] = "tag:Bestiary_Biomes.StardustPillar", ["Conditions.InDesert"] = "tag:Bestiary_Biomes.Desert",
		["Conditions.InGlowshroom"] = "tag:Bestiary_Biomes.SurfaceMushroom", ["Conditions.InUndergroundDesert"] = "tag:Bestiary_Biomes.UndergroundDesert",
		["Conditions.InSkyHeight"] = "tag:Bestiary_Biomes.Sky", ["Conditions.InUnderworldHeight"] = "tag:Bestiary_Biomes.TheUnderworld",
		["Conditions.InBeach"] = "tag:Bestiary_Biomes.Ocean", ["Conditions.InRain"] = "tag:Bestiary_Events.Rain",
		["Conditions.InSandstorm"] = "tag:Bestiary_Events.Sandstorm", ["Conditions.InOldOneArmy"] = "tag:Bestiary_Invasions.OldOnesArmy",
		["Conditions.InGranite"] = "tag:Bestiary_Biomes.Granite", ["Conditions.InMarble"] = "tag:Bestiary_Biomes.Marble",
		["Conditions.InLihzardTemple"] = "tag:Bestiary_Biomes.TheTemple", ["Conditions.InGraveyard"] = "tag:Bestiary_Biomes.Graveyard",
		["Conditions.InShoppingForest"] = "tag:Bestiary_Biomes.Surface", ["Conditions.InBelowSurface"] = "tag:Bestiary_Biomes.Underground",
		// The events a condition waits for (Condition.Christmas, BloodMoon, Eclipse, BirthdayParty, HappyWindyDay) and the
		// invasions it waits to see beaten (DownedGoblinArmy and the rest).
		["Conditions.Christmas"] = "tag:Bestiary_Events.Christmas", ["Conditions.Halloween"] = "tag:Bestiary_Events.Halloween",
		["Conditions.BloodMoon"] = "tag:Bestiary_Events.BloodMoon", ["Conditions.SolarEclipse"] = "tag:Bestiary_Events.Eclipse",
		["Conditions.BirthdayParty"] = "tag:Bestiary_Events.Party", ["Conditions.HappyWindyDay"] = "tag:Bestiary_Events.WindyDay",
		["Conditions.DownedGoblinArmy"] = "tag:Bestiary_Invasions.Goblins", ["Conditions.DownedPirates"] = "tag:Bestiary_Invasions.Pirates",
		["Conditions.DownedMartians"] = "tag:Bestiary_Invasions.Martian", ["Conditions.DownedFrostLegion"] = "tag:Bestiary_Invasions.FrostLegion",
		["Conditions.DownedOldOnesArmyAny"] = "tag:Bestiary_Invasions.OldOnesArmy", ["Conditions.DownedOldOnesArmyT1"] = "tag:Bestiary_Invasions.OldOnesArmy",
		["Conditions.DownedOldOnesArmyT2"] = "tag:Bestiary_Invasions.OldOnesArmy", ["Conditions.DownedOldOnesArmyT3"] = "tag:Bestiary_Invasions.OldOnesArmy"
	};

	// The moons' mini-bosses a condition waits to see beaten (Condition.DownedMourningWood and the rest): their cards.
	private static readonly Dictionary<string, int> ConditionNpcs = new Dictionary<string, int>(StringComparer.Ordinal)
	{
		["Conditions.DownedMourningWood"] = NPCID.MourningWood, ["Conditions.DownedPumpking"] = NPCID.Pumpking, ["Conditions.DownedEverscream"] = NPCID.Everscream,
		["Conditions.DownedSantaNK1"] = NPCID.SantaNK1, ["Conditions.DownedIceQueen"] = NPCID.IceQueen
	};

	// Built once per language and world (the world's evil names its boss); the texts split by them likewise.
	private static (string Culture, int World, bool Crimson, bool Drunk) LinkNamesKey;

	private static readonly Dictionary<LinkNames, List<(string Name, BookLink Link)>> LinkNameLists = new Dictionary<LinkNames, List<(string, BookLink)>>();

	private static readonly Dictionary<string, List<LinkSpan>> LinkedTexts = new Dictionary<string, List<LinkSpan>>(StringComparer.Ordinal);

	// Russian declines the names it writes ("Сундуки Преисподней", "Святилище Багрянца", "после победы над Плантерой"):
	// per name a pattern of its words' stems (StemPattern), found where the name as written is not.
	private static readonly Dictionary<LinkNames, List<(Regex Stem, BookLink Link)>> LinkStemLists = new Dictionary<LinkNames, List<(Regex, BookLink)>>();

	private static List<(string Name, BookLink Link)> LinkNameList(LinkNames kind)
	{
		(string, int, bool, bool) key = (Language.ActiveCulture.Name, Main.worldID, WorldGen.crimson, Main.drunkWorld);
		if (LinkNamesKey != key)
		{
			LinkNamesKey = key;
			LinkNameLists.Clear();
			LinkStemLists.Clear();
			LinkedTexts.Clear();
		}
		if (!LinkNameLists.TryGetValue(kind, out List<(string, BookLink)> list))
		{
			list = LinkNameLists[kind] = ReadLinkNames(kind);
		}
		return list;
	}

	// Bosses' and places' names as Russian stems, in a Russian game only; none for the bags, whose names a text writes whole.
	private static List<(Regex Stem, BookLink Link)> LinkStemList(LinkNames kind)
	{
		List<(string Name, BookLink Link)> names = LinkNameList(kind);
		if (kind == LinkNames.Bags || !GameCulture.FromCultureName(GameCulture.CultureName.Russian).IsActive)
		{
			return new List<(Regex, BookLink)>();
		}
		if (!LinkStemLists.TryGetValue(kind, out List<(Regex, BookLink)> list))
		{
			list = LinkStemLists[kind] = names.Select(((string Name, BookLink Link) n) => (Pattern: string.Join(@"\s+", n.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(StemPattern)), n.Link)).Where(((string Pattern, BookLink Link) n) => n.Pattern.Contains(@"\p{L}")).Select(((string Pattern, BookLink Link) n) => (new Regex(@"(?<!\p{L})" + n.Pattern + @"(?!\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), n.Link)).ToList();
		}
		return list;
	}

	// A Russian word's stem and up to three letters of ending: its last vowels (and й, ь) go, a fleeting "е" of "-ец" too
	// (Багрянец, Багрянца); a word whose stem would be under four letters stays as written.
	private static string StemPattern(string word)
	{
		if (!word.Any((char c) => c >= 'а' && c <= 'я' || c >= 'А' && c <= 'Я'))
		{
			return Regex.Escape(word);
		}
		string stem = word.EndsWith("ец", StringComparison.OrdinalIgnoreCase) ? (word[..^2] + "ц") : word.TrimEnd("аеёиоуыэюяйьАЕЁИОУЫЭЮЯЙЬ".ToCharArray());
		return (stem.Length >= 4) ? (Regex.Escape(stem) + @"\p{L}{0,3}") : Regex.Escape(word);
	}

	private static List<(string, BookLink)> ReadLinkNames(LinkNames kind)
	{
		EnsureBestiary();
		List<(string Name, BookLink Link)> list = new List<(string, BookLink)>();
		switch (kind)
		{
		case LinkNames.Bosses:
			for (int step = 1; step < BookSteps.All.Count; step++)
			{
				int mob = StepMob(step);
				if (mob == 0)
				{
					continue;
				}
				// A drunk world's evil step reads "Eater of Worlds or Brain of Cthulhu": each boss is its own name below.
				if (BookSteps.All[step].Key != "Terraria EaterofWorldsHead")
				{
					list.Add((BookSteps.Name(step), MobLink(mob)));
				}
				foreach (int npc in BookSteps.All[step].Npcs)
				{
					list.Add((Lang.GetNPCNameValue(npc), MobLink(npc)));
				}
			}
			break;
		case LinkNames.Places:
			foreach (BookBestiary.Tag tag in BookBestiary.Tags.Where((BookBestiary.Tag t) => t.Key.StartsWith("tag:", StringComparison.Ordinal) || t.Key.StartsWith("biome:", StringComparison.Ordinal)))
			{
				BookLink link = MobCategoryLink(tag);
				list.Add((BookBestiary.Label(tag), link));
				list.Add((Language.GetTextValue(tag.LabelKey), link));
			}
			foreach ((string place, string biome) in PlaceTags)
			{
				list.Add((PlaceName(place), MobCategoryLink(BookBestiary.FindTag("tag:Bestiary_Biomes." + biome))));
			}
			break;
		case LinkNames.Bags:
			foreach (Item bag in ContentSamples.ItemsByType.Values.Where((Item i) => i != null && i.type > 0 && i.type < ItemID.Sets.BossBag.Length && ItemID.Sets.BossBag[i.type] && !BookCurated.Hidden(i)))
			{
				list.Add((bag.Name, ItemLink(bag.type)));
			}
			break;
		}
		return list.Where(((string Name, BookLink Link) x) => x.Link != null && x.Name != null && x.Name.Length > 2 && x.Name.Any(char.IsLetter)).DistinctBy(((string Name, BookLink Link) x) => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	// The text in pieces: each name of those kinds it shows, and of more (a row's own items and NPCs), is a link; the
	// longest name first, whole words. A boss or a bag in any case ("the Leviathan" in Calamity's words); a place only
	// as its name is written, so the prose around it stays prose ("falls from the sky" is no Sky category). Kept per
	// text, so a row reads it once. Then each fallback (a condition's whole label, BookMention.Whole) links where nothing it
	// covers is a link yet.
	private static List<LinkSpan> Linked(string text, LinkNames kinds, IEnumerable<(string Name, BookLink Link)> more = null, IEnumerable<(string Name, BookLink Link)> fallback = null)
	{
		text ??= "";
		List<(string Name, BookLink Link, StringComparison Case)> names = Usable(more);
		List<(string Name, BookLink Link, StringComparison Case)> last = Usable(fallback);
		string key = (int)kinds + "\u0001" + text + "\u0001" + string.Join("\u0002", names.Select(((string Name, BookLink Link, StringComparison Case) m) => m.Name)) + "\u0001" + string.Join("\u0002", last.Select(((string Name, BookLink Link, StringComparison Case) m) => m.Name));
		foreach (LinkNames kind in LinkNameKinds.Where((LinkNames k) => kinds.HasFlag(k)))
		{
			StringComparison comparison = (kind == LinkNames.Places) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			names.AddRange(LinkNameList(kind).Select(((string Name, BookLink Link) n) => (n.Name, n.Link, comparison)));
		}
		if (LinkedTexts.TryGetValue(key, out List<LinkSpan> cached))
		{
			return cached;
		}
		List<(int Start, int End, BookLink Link)> found = new List<(int, int, BookLink)>();
		Find(names);
		// The declined forms (Russian), longest name first.
		foreach ((Regex stem, BookLink link) in LinkNameKinds.Where((LinkNames k) => kinds.HasFlag(k)).SelectMany(LinkStemList).OrderByDescending(((Regex Stem, BookLink Link) s) => s.Stem.ToString().Length))
		{
			foreach (Match match in stem.Matches(text))
			{
				Take(match.Index, match.Index + match.Length, link);
			}
		}
		Find(last);
		List<LinkSpan> spans = new List<LinkSpan>();
		int from = 0;
		foreach ((int start, int end, BookLink link) in found.OrderBy(((int Start, int End, BookLink Link) f) => f.Start))
		{
			if (start > from)
			{
				spans.Add(new LinkSpan(text[from..start]));
			}
			spans.Add(new LinkSpan(text[start..end], link));
			from = end;
		}
		if (from < text.Length || spans.Count == 0)
		{
			spans.Add(new LinkSpan(text[from..]));
		}
		if (LinkedTexts.Count > 4096)
		{
			LinkedTexts.Clear();
		}
		LinkedTexts[key] = spans;
		return spans;
		void Find(List<(string Name, BookLink Link, StringComparison Case)> list)
		{
			foreach ((string name, BookLink link, StringComparison comparison) in list.OrderByDescending(((string Name, BookLink Link, StringComparison Case) n) => n.Name.Length))
			{
				for (int at = text.IndexOf(name, comparison); at >= 0; at = text.IndexOf(name, at + 1, comparison))
				{
					int end = at + name.Length;
					// A plural takes its "s" along ("Drops on Windy Days").
					if (end < text.Length && text[end] == 's' && (end + 1 == text.Length || !char.IsLetterOrDigit(text[end + 1])))
					{
						end++;
					}
					if ((at == 0 || !char.IsLetterOrDigit(text[at - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end])))
					{
						Take(at, end, link);
					}
				}
			}
		}
		void Take(int at, int end, BookLink link)
		{
			if (!found.Any(((int Start, int End, BookLink Link) f) => at < f.End && f.Start < end))
			{
				found.Add((at, end, link));
			}
		}
		static List<(string, BookLink, StringComparison)> Usable(IEnumerable<(string Name, BookLink Link)> given)
		{
			// A name without a letter ("???", a mod's boss before it shows itself) names nothing.
			return (given ?? Enumerable.Empty<(string, BookLink)>()).Where(((string Name, BookLink Link) m) => m.Link != null && !string.IsNullOrEmpty(m.Name) && m.Name.Any(char.IsLetter)).Select(((string Name, BookLink Link) m) => (m.Name, m.Link, StringComparison.OrdinalIgnoreCase)).ToList();
		}
	}

	// Linked with a row's mentions: its own phrases first, a condition's whole label only as a fallback.
	private static List<LinkSpan> LinkedMentions(string text, LinkNames kinds, IEnumerable<BookMention> mentions)
	{
		List<BookMention> all = (mentions ?? Enumerable.Empty<BookMention>()).ToList();
		return Linked(text, kinds, Mentioned(all.Where((BookMention m) => !m.Whole)), Mentioned(all.Where((BookMention m) => m.Whole)));
	}

	// What a recipe or shop condition names, as its own text: the town NPC who must live there, the Bestiary biome or event
	// the player stands in, a boss's defeat (not Hardmode's: that names the stage); null when nothing.
	private static BookMention? ConditionMention(Condition condition)
	{
		LocalizedText text = condition?.Description;
		if (text == null)
		{
			return null;
		}
		if (text.Key == "Conditions.NpcIsPresent" && text.BoundArgs?.FirstOrDefault() is LocalizedText npcName && BookProgressionSources.NpcFromNameKey(npcName.Key) is int npc)
		{
			return new BookMention(ConditionName(condition), Npc: npc);
		}
		if (ConditionTags.TryGetValue(text.Key, out string place))
		{
			return new BookMention(ConditionName(condition), Place: place);
		}
		if (ConditionNpcs.TryGetValue(text.Key, out int miniBoss))
		{
			return new BookMention(ConditionName(condition), Npc: miniBoss);
		}
		// The mechanical bosses as a count (DownedMechBossAny, DownedMechBossAll) name no one boss.
		int step = text.Key.Contains("MechBoss", StringComparison.Ordinal) ? -1 : (BookProgressionSources.ConditionStep(condition) ?? -1);
		return (step > 0 && step != BookSteps.HardmodeStep && StepMob(step) is int boss and not 0) ? new BookMention(ConditionName(condition), Npc: boss) : null;
	}

	// The places and events vanilla's global drop conditions ask for (ItemDropDatabase.RegisterGlobalRules; their texts,
	// Bestiary_ItemDropConditions.*, name them: "Drops in the Hardmode Jungle"), by condition type. "evil": the world's
	// underground evil (SoulOfNight: Underground Crimson or Corruption).
	private static readonly Dictionary<string, string> DropConditionPlaces = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		[typeof(Conditions.PirateMap).FullName] = "tag:Bestiary_Biomes.Ocean", [typeof(Conditions.HalloweenWeapons).FullName] = "tag:Bestiary_Events.Halloween",
		[typeof(Conditions.HalloweenGoodieBagDrop).FullName] = "tag:Bestiary_Events.Halloween", [typeof(Conditions.XmasPresentDrop).FullName] = "tag:Bestiary_Events.Christmas",
		[typeof(Conditions.SoulOfLight).FullName] = "tag:Bestiary_Biomes.UndergroundHallow", [typeof(Conditions.SoulOfNight).FullName] = "evil",
		[typeof(Conditions.LivingFlames).FullName] = "tag:Bestiary_Biomes.TheUnderworld", [typeof(Conditions.JungleKeyCondition).FullName] = "tag:Bestiary_Biomes.Jungle",
		[typeof(Conditions.CorruptKeyCondition).FullName] = "tag:Bestiary_Biomes.TheCorruption", [typeof(Conditions.CrimsonKeyCondition).FullName] = "tag:Bestiary_Biomes.Crimson",
		[typeof(Conditions.HallowKeyCondition).FullName] = "tag:Bestiary_Biomes.TheHallow", [typeof(Conditions.FrozenKeyCondition).FullName] = "tag:Bestiary_Biomes.Snow",
		[typeof(Conditions.DesertKeyCondition).FullName] = "tag:Bestiary_Biomes.Desert", [typeof(Conditions.WindyEnoughForKiteDrops).FullName] = "tag:Bestiary_Events.WindyDay",
		// And the world's own: the evil it has (IsCorruption, IsCrimson and their Classic forms), the moon or season it is in.
		[typeof(Conditions.IsCorruption).FullName] = "tag:Bestiary_Biomes.TheCorruption", [typeof(Conditions.IsCrimson).FullName] = "tag:Bestiary_Biomes.Crimson",
		[typeof(Conditions.IsCorruptionAndNotExpert).FullName] = "tag:Bestiary_Biomes.TheCorruption", [typeof(Conditions.IsCrimsonAndNotExpert).FullName] = "tag:Bestiary_Biomes.Crimson",
		[typeof(Conditions.IsPumpkinMoon).FullName] = "tag:Bestiary_Invasions.PumpkinMoon", [typeof(Conditions.IsBloodMoonAndNotFromStatue).FullName] = "tag:Bestiary_Events.BloodMoon",
		[typeof(Conditions.IsChristmas).FullName] = "tag:Bestiary_Events.Christmas"
	};

	// The bosses vanilla's yoyo conditions wait for (YoyoCascade: after Skeletron, before Hardmode; YoyosKraken: after Plantera).
	private static readonly Dictionary<string, int> DropConditionNpcs = new Dictionary<string, int>(StringComparer.Ordinal)
	{
		[typeof(Conditions.YoyoCascade).FullName] = NPCID.SkeletronHead, [typeof(Conditions.YoyosKraken).FullName] = NPCID.Plantera
	};

	// What a drop condition names, under its whole label, whatever language the label is in: the place or event a vanilla
	// condition asks for (DropConditionPlaces), else the boss of the step that decides it: the probe's (a mod's "after
	// defeating X", BookProgressionSources.DropConditionStep, a wrapped Condition's step) or the step it comes before, the
	// mechanical boss a summon waits for (MechBossSpawnersDropRule). Null for what names no boss or place: a seed, Hardmode,
	// the mechanical bosses as a count (MechGate), the kill itself, the dying boss's own step.
	private static BookMention? ConditionTarget(IItemDropRuleCondition condition, int npcNetId, int item, string label)
	{
		if (condition == null)
		{
			return null;
		}
		if (DropConditionPlaces.TryGetValue(condition.GetType().FullName ?? "", out string place))
		{
			return new BookMention(label, Place: (place == "evil") ? (WorldGen.crimson ? "tag:Bestiary_Biomes.UndergroundCrimson" : "tag:Bestiary_Biomes.UndergroundCorruption") : place, Whole: true);
		}
		if (DropConditionNpcs.TryGetValue(condition.GetType().FullName ?? "", out int named))
		{
			return new BookMention(label, Npc: named, Whole: true);
		}
		// The mechanical bosses as a count, and the moons' wave odds the book words itself, name no one boss.
		if (BookProgressionSources.MechGate(condition, out _) != null || condition is Conditions.PumpkinMoonDropGatingChance or Conditions.FrostMoonDropGatingChance)
		{
			return null;
		}
		int step = item switch
		{
			_ when condition is not Conditions.MechanicalBossesDummyCondition => -1,
			ItemID.MechanicalWorm => BookSteps.Find("Terraria TheDestroyer"),
			ItemID.MechanicalEye => BookSteps.Find("Terraria TheTwins"),
			ItemID.MechanicalSkull => BookSteps.Find("Terraria SkeletronPrime"),
			_ => -1
		};
		if (step <= 0)
		{
			step = (condition is SimpleItemDropRuleCondition simple) ? (BookProgressionSources.ConditionStep(BookProgressionSources.WrappedKey(simple), simple.Predicate) ?? -1) : (BookProgressionSources.DropConditionStep(condition) ?? -1);
		}
		if (step <= 0)
		{
			step = BookProgressionSources.BeforeStep(condition);
		}
		int own = (npcNetId != 0 && ContentSamples.NpcsByNetId.TryGetValue(npcNetId, out NPC npc)) ? BookSteps.Npc(npc.type) : -1;
		return (step > 0 && step != BookSteps.HardmodeStep && step != own && StepMob(step) is int boss and not 0) ? new BookMention(label, Npc: boss, Whole: true) : null;
	}

	// A condition's text with the bosses and places it names as links, else the whole text a link to what it stands for.
	private static List<LinkSpan> ConditionSpans(Condition condition)
	{
		string text = ConditionName(condition);
		List<LinkSpan> spans = Linked(text, LinkNames.Bosses | LinkNames.Places);
		if (spans.Any((LinkSpan s) => s.Link != null) || ConditionMention(condition) is not BookMention mention)
		{
			return spans;
		}
		return Whole(text, MentionLink(mention));
	}

	// A stage line: a boss's step names the boss, a link to its card; a stage (the start, Hardmode, after Moon Lord) is a
	// link to the stage filter of the card's tab.
	private static List<LinkSpan> StageSpans(int step, string text, bool mobs, int keep = -1)
	{
		if (step < 0)
		{
			return Plain(text);
		}
		if (step == 0 || step == BookSteps.HardmodeStep || step == BookSteps.MoonLordStep)
		{
			return Whole(text, StageLink(BookSteps.StageOf(step), mobs, keep));
		}
		List<LinkSpan> spans = Linked(text, LinkNames.Bosses);
		return spans.Any((LinkSpan s) => s.Link != null) ? spans : Whole(text, MobLink(StepMob(step)));
	}

	// The item's stage, as the card's header names it (StageName).
	private static List<LinkSpan> ItemStageSpans(Item item, Recipe recipe)
	{
		int step = (recipe != null) ? BookProgression.RecipeStep(recipe.RecipeIndex) : BookProgression.ItemStep(item.type);
		bool excluded = (recipe != null) ? BookProgression.RecipeModeExcluded(recipe.RecipeIndex) : BookProgression.ItemModeExcluded(item.type);
		return StageSpans(excluded && step < 0 ? -1 : step, StageName(item, recipe), mobs: false);
	}

	// Items named one after another on a line, each a link to its card: "Iron Bar, Lead Bar"; when that does not fit and
	// they end in the same word, that word once ("Iron, Lead Bar", as AlternativesLabel shortens); else as many as fit
	// and "+N" for the rest, which the row's tooltip lists.
	private static List<LinkSpan> ItemListSpans(IReadOnlyList<int> types, BookRect line, float size)
	{
		string[] names = types.Select(ItemName).ToArray();
		List<LinkSpan> full = List(names, "");
		if (LineWidth(full, line, size) <= line.Width || names.Length < 2)
		{
			return full;
		}
		string last = names[0].Split(' ')[^1];
		if (names.All((string n) => n.Contains(' ') && n.Split(' ')[^1] == last))
		{
			List<LinkSpan> shared = List(names.Select((string n) => n[..n.LastIndexOf(' ')]).ToArray(), " " + last);
			if (LineWidth(shared, line, size) <= line.Width)
			{
				return shared;
			}
		}
		for (int shown = names.Length - 1; shown >= 1; shown--)
		{
			List<LinkSpan> some = List(names.Take(shown).ToArray(), " +" + (names.Length - shown));
			if (LineWidth(some, line, size) <= line.Width || shown == 1)
			{
				return some;
			}
		}
		return full;
		List<LinkSpan> List(string[] shown, string tail)
		{
			List<LinkSpan> spans = shown.SelectMany((string name, int i) => new[] { new LinkSpan((i > 0) ? ", " : ""), new LinkSpan(name, ItemLink(types[i])) }).Where((LinkSpan s) => s.Text.Length > 0).ToList();
			if (tail.Length > 0)
			{
				spans.Add(new LinkSpan(tail));
			}
			return spans;
		}
	}

	// The width the spans take on that line, at the scale LinkPieces lays them out.
	private static float LineWidth(List<LinkSpan> spans, BookRect line, float size)
	{
		return Ink(BookRules.PlainText(string.Concat(spans.Select((LinkSpan s) => s.Text)))).Width * LineScale(line, size);
	}

	private static float LineScale(BookRect line, float size)
	{
		return Math.Min(size * Math.Clamp(TextScale, 0.9f, 1.15f), line.Height / Math.Max(1f, Ink(ReferenceGlyphs).Height));
	}

	// The earliest pickaxe strong enough for that power (a drill or the Picksaw too): by stage, then the weakest that is.
	private static int FirstPickaxe(int power)
	{
		return ContentSamples.ItemsByType.Values.Where((Item i) => i != null && i.pick >= power && BookProgression.ItemStep(i.type) >= 0 && !BookCurated.Hidden(i)).OrderBy((Item i) => BookProgression.ItemStep(i.type)).ThenBy((Item i) => i.pick).ThenBy((Item i) => i.type).Select((Item i) => i.type).FirstOrDefault();
	}

	// The tracker's layer draws its links into its own slot, so the book's stay as they were.
	private static void CollectTrackerLinks(Action draw)
	{
		(BookLink, BookRect) book = HoveredLink;
		string card = CardTarget;
		HoveredLink = default;
		CardTarget = null;
		try
		{
			draw();
		}
		finally
		{
			TrackerLink = HoveredLink;
			HoveredLink = book;
			CardTarget = card;
		}
	}

	// A link the tracker shows while the book is closed opens the book first.
	private static BookLink Opening(BookLink link)
	{
		return (link == null) ? null : (link with
		{
			Follow = () =>
			{
				if (!Open)
				{
					Toggle();
				}
				link.Follow();
			}
		});
	}

	// Dev audit for the test stand: the places of the link inventory (LINKS-SPEC) over every mob card (each variant) and
	// every item's How to Get and recipes, as they would be drawn: per place the lines, the lines with a link, the links,
	// then the phrases left plain and ("+") those linked, most frequent first (the plain ones are what the report gives a
	// reason for; the linked ones show what a name matched).
	// stand.ps1 c call p=ModernRecipeBook.UI.Book.RecipeBookSystem.LinkAudit out=links-audit.txt
	internal static IEnumerable<string> LinkAudit()
	{
		Dictionary<string, (int Lines, int Linked, int Links, Dictionary<string, int> Plain)> places = new Dictionary<string, (int, int, int, Dictionary<string, int>)>(StringComparer.Ordinal);
		EnsureBestiary();
		foreach (int net in BookBestiary.Mobs.SelectMany((BookMob m) => m.Variants).Distinct())
		{
			ReadMobCard(net);
			BookMob mob = BookBestiary.Mobs[BookBestiary.IndexOf(net)];
			int step = BookProgression.NpcStep(net);
			Count("mob subtitle", new List<LinkSpan> { new LinkSpan(MobKindName(mob.Kind), MobCategoryLink(BookBestiary.FindTag(KindTag(mob.Kind)))), new LinkSpan(" · "), new LinkSpan(ModName(mob.Mod), ModLink(mob.Mod, mobs: true)) });
			Count("mob stage", StageSpans(step, StepName(step), mobs: true));
			Count("mob biome", (EnemyBiomes.Length > 0) ? Linked(EnemyBiomes, LinkNames.Places | LinkNames.Bosses) : Plain(L("Bestiary.NotListed")));
			Count("mob spawns", (EnemySpawn.Length > 0) ? Linked(EnemySpawn, LinkNames.Bosses | LinkNames.Places, EnemySpawnItems.Select((int item) => (ItemName(item), ItemLink(item)))) : Plain(L("Bestiary.NotListed2")));
			foreach (BookEnemyLoot loot in EnemyLoot.Concat(EnemyGlobalLoot))
			{
				Count("mob loot row detail", LinkedMentions(LootRowText(loot).Detail, LinkNames.Bosses | LinkNames.Places | LinkNames.Bags, loot.Mentions));
			}
			int banner = Item.NPCtoBanner(ContentSamples.NpcsByNetId[net].BannerID());
			Count("mob stats banner", (banner > 0 && Item.BannerToItem(banner) > 0) ? Whole(L("MobCard.Banner"), ItemLink(Item.BannerToItem(banner))) : new List<LinkSpan>());
		}
		MobCardKey = default;
		int[] keep = AcquisitionVariants;
		foreach (int type in AllItems)
		{
			AcquisitionVariants = new[] { type };
			AcquisitionVariant = 0;
			try
			{
				ReadAcquisitionSources();
			}
			catch (Exception)
			{
				continue;
			}
			foreach (BookAcquisitionGroup group in AcquisitionSources)
			{
				string kind = "how " + group.Source.Kind;
				Count(kind + " title", AcquisitionTitleSpans(group, group.Source, group.Source.Name, expanded: false));
				Count(kind + " detail", LinkedMentions(AcquisitionDetailText(group), LinkNames.Bosses | LinkNames.Places | LinkNames.Bags, GroupMentions(group)));
			}
			foreach (int index in RecipesByItem.GetValueOrDefault(type) ?? new List<int>())
			{
				foreach (Condition condition in Main.recipe[index].Conditions)
				{
					Count("recipe condition", ConditionSpans(condition));
				}
			}
		}
		CloseAcquisition();
		AcquisitionVariants = keep;
		return places.OrderBy((KeyValuePair<string, (int, int, int, Dictionary<string, int>)> p) => p.Key, StringComparer.Ordinal).SelectMany((KeyValuePair<string, (int Lines, int Linked, int Links, Dictionary<string, int> Plain)> p) => p.Value.Plain.OrderByDescending((KeyValuePair<string, int> w) => w.Value).Take(60).Select((KeyValuePair<string, int> w) => "\t" + w.Value + "\t" + w.Key).Prepend($"{p.Key}: {p.Value.Lines} lines, {p.Value.Linked} with a link, {p.Value.Links} links"));
		void Count(string place, List<LinkSpan> spans)
		{
			if (spans.Count == 0)
			{
				return;
			}
			(int Lines, int Linked, int Links, Dictionary<string, int> Plain) at = places.TryGetValue(place, out var found) ? found : (0, 0, 0, new Dictionary<string, int>(StringComparer.Ordinal));
			int links = spans.Count((LinkSpan s) => s.Link != null);
			foreach (string phrase in spans.Where((LinkSpan s) => s.Link == null).SelectMany((LinkSpan s) => s.Text.Split(new[] { " · ", "; ", ", " }, StringSplitOptions.None)).Select((string w) => w.Trim(' ', '·', ',', ';')).Where((string w) => w.Length > 1 && !w.All((char c) => char.IsDigit(c) || c is '%' or '.' or ',' or '×' or '–' or '-' or '+' or ' ')))
			{
				at.Plain[phrase] = at.Plain.GetValueOrDefault(phrase) + 1;
			}
			foreach (LinkSpan link in spans.Where((LinkSpan s) => s.Link != null))
			{
				at.Plain["+ " + link.Text] = at.Plain.GetValueOrDefault("+ " + link.Text) + 1;
			}
			places[place] = (at.Lines + 1, at.Linked + ((links > 0) ? 1 : 0), at.Links + links, at.Plain);
		}
	}
}
