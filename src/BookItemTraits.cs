using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

using static ModernRecipeBook.BookText;

namespace ModernRecipeBook;

[Flags]
internal enum BookTrait
{
	None = 0,
	Homing = 1,
	StealthHoming = 2,
	Pierces = 4,
	ThroughWalls = 8,
	AutoUse = 16,
	Minion = 32,
	Sentry = 64
}

// What a weapon's shots do, from what the game already says about it; no code of the weapon runs.
// - Homing: the item's description. The lines the game shows (every mod's tooltip edits included) in the current language
//   and, in another language, the item's English text too (a translation may drop the word); plus a short list checked
//   by hand in the decompiled code for vanilla weapons whose text does not say it (BookVerifiedHoming). A line that
//   speaks of stealth strikes gives "homing stealth strike" instead.
// - Pierces / through walls: the defaults of the projectile the item shoots (penetrate, tileCollide), for shots that fly
//   off on their own. Held projectiles, minions, whips and weapons whose shot comes from their ammo get neither; nor
//   does a projectile whose own code changes that field or kills it on a hit: unknown rather than wrong.
// - Auto attack: the item's autoReuse. Minion / sentry: the projectile's flags.
// Fields once per load; descriptions a slice per update once in a world, again after a language change.
internal static class BookItemTraits
{
	internal enum Source
	{
		None,
		// The description as the game shows it now.
		Description,
		// The item's English text, in another language.
		EnglishDescription,
		// Checked in the game's code (BookVerifiedHoming).
		Verified,
		// Item and projectile fields.
		Fields
	}

	internal readonly record struct Why(Source Source, string Detail);

	internal static readonly BookTrait[] AllRows = new BookTrait[7]
	{
		BookTrait.Homing,
		BookTrait.StealthHoming,
		BookTrait.Pierces,
		BookTrait.ThroughWalls,
		BookTrait.AutoUse,
		BookTrait.Minion,
		BookTrait.Sentry
	};

	private static readonly BookTrait[] RowsWithoutRogue = AllRows.Where((BookTrait t) => t != BookTrait.StealthHoming).ToArray();

	// The filter's rows: stealth homing only with Calamity.
	internal static BookTrait[] Rows => BookItemClasses.RogueShown ? AllRows : RowsWithoutRogue;

	// What a saved filter may hold: a trait with no row (stealth homing without Calamity) would hide everything for good.
	internal static int KnownMask => BookItemClasses.RogueShown ? 127 : (127 & ~(int)BookTrait.StealthHoming);

	private static BookTrait[] fields = Array.Empty<BookTrait>();

	private static BookTrait[] described = Array.Empty<BookTrait>();

	// Per type: the line that said homing / stealth homing, and where it came from.
	private static Why[] homingWhy = Array.Empty<Why>();

	private static Why[] stealthWhy = Array.Empty<Why>();

	private static int[] penetrate = Array.Empty<int>();

	private static int[] queue = Array.Empty<int>();

	private static int next;

	private static string culture = "";

	private static Dictionary<string, string> english;

	private static long fieldTicks;

	private static long textTicks;

	private static long englishTicks;

	// The longest single slice: what one update paid at most.
	private static long longestTicks;

	// Spent while a world loaded (OnWorldLoad), and on which thread.
	private static long loadingTicks;

	private static bool loadingOnMainThread;

	private static int slices;

	internal static bool FieldsReady { get; private set; }

	internal static bool Ready
	{
		get
		{
			if (FieldsReady && next >= queue.Length)
			{
				return culture == Language.ActiveCulture.Name;
			}
			return false;
		}
	}

	internal static float Progress => (queue.Length == 0) ? 0f : ((float)next / queue.Length);

	// Bumps when the descriptions add results (the book filters again once they are all read).
	internal static int Version { get; private set; }

	private const double SliceMs = 2.0;

	internal static BookTrait Get(int type)
	{
		if ((uint)type >= (uint)fields.Length)
		{
			return BookTrait.None;
		}
		return fields[type] | described[type];
	}

	internal static Why Reason(int type, BookTrait trait)
	{
		if ((uint)type >= (uint)fields.Length || (Get(type) & trait) == 0)
		{
			return default(Why);
		}
		switch (trait)
		{
		case BookTrait.Homing:
			return homingWhy[type];
		case BookTrait.StealthHoming:
			return stealthWhy[type];
		case BookTrait.Pierces:
			return new Why(Source.Fields, (penetrate[type] < 0) ? "-1" : penetrate[type].ToString());
		default:
			return new Why(Source.Fields, "");
		}
	}

	// Weapons and ammo; nothing else has traits.
	internal static bool Covers(Item item)
	{
		if (item == null || item.IsAir)
		{
			return false;
		}
		if (BookCategories.Weapon(item))
		{
			return true;
		}
		if (item.ammo > 0 && !item.notAmmo)
		{
			return item.shoot > 0;
		}
		return false;
	}

	internal static string Label(BookTrait trait)
	{
		return trait switch
		{
			BookTrait.Homing => L("Traits.NameHoming"),
			BookTrait.StealthHoming => L("Traits.NameStealthHoming"),
			BookTrait.Pierces => L("Traits.NamePierces"),
			BookTrait.ThroughWalls => L("Traits.NameThroughWalls"),
			BookTrait.AutoUse => L("Traits.NameAutoUse"),
			BookTrait.Minion => L("Traits.NameMinion"),
			BookTrait.Sentry => L("Traits.NameSentry"),
			_ => ""
		};
	}

	// One line: what the trait means and where it is read from.
	internal static string Tip(BookTrait trait)
	{
		return trait switch
		{
			BookTrait.Homing => L("Traits.TipHoming"),
			BookTrait.StealthHoming => L("Traits.TipStealthHoming"),
			BookTrait.Pierces => L("Traits.TipPierces"),
			BookTrait.ThroughWalls => L("Traits.TipThroughWalls"),
			BookTrait.AutoUse => L("Traits.TipAutoUse"),
			BookTrait.Minion => L("Traits.TipMinion"),
			BookTrait.Sentry => L("Traits.TipSentry"),
			_ => ""
		};
	}

	internal static string SourceLabel(Source source)
	{
		return source switch
		{
			Source.Description => L("Traits.SourceDescription"),
			Source.EnglishDescription => L("Traits.SourceEnglishDescription"),
			Source.Verified => L("Traits.SourceVerified"),
			Source.Fields => L("Traits.SourceFields"),
			_ => ""
		};
	}

	internal static void Clear()
	{
		fields = Array.Empty<BookTrait>();
		described = Array.Empty<BookTrait>();
		homingWhy = Array.Empty<Why>();
		stealthWhy = Array.Empty<Why>();
		penetrate = Array.Empty<int>();
		unjudged = Array.Empty<string>();
		queue = Array.Empty<int>();
		next = 0;
		culture = "";
		english = null;
		FieldsReady = false;
		Started = false;
		fieldNext = 0;
		fieldTicks = textTicks = englishTicks = longestTicks = loadingTicks = 0L;
		slices = 0;
		writes.Clear();
		launcherAmmo = null;
	}

	// While a world loads (single player and multiplayer clients): the fields of every weapon, and the English texts when
	// the game speaks another language. Once per load; the descriptions wait for the game to run (mods' tooltips read
	// the player).
	internal static void Prepare()
	{
		if (Main.dedServ)
		{
			return;
		}
		try
		{
			long start = Stopwatch.GetTimestamp();
			if (!Started)
			{
				Begin();
			}
			while (!FieldsReady)
			{
				ReadFields(1000.0);
			}
			if (english == null && !Language.ActiveCulture.Name.StartsWith("en", StringComparison.Ordinal))
			{
				LoadEnglishNow();
			}
			// Not a slice of a running game.
			loadingTicks += Stopwatch.GetTimestamp() - start;
			loadingOnMainThread = Program.IsMainThread;
			longestTicks = 0L;
			slices = 0;
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Weapon traits: reading the weapons while the world loaded failed; they are read in game instead: " + error.Message);
		}
	}

	private static void LoadEnglishNow()
	{
		long load = Stopwatch.GetTimestamp();
		english = LoadEnglish();
		englishTicks = Stopwatch.GetTimestamp() - load;
	}

	// Once per update (BookItemTraitsSystem), on clients in a world.
	internal static void Pump()
	{
		if (Main.dedServ || Main.gameMenu)
		{
			return;
		}
		if (!Started)
		{
			Begin();
		}
		if (!FieldsReady)
		{
			ReadFields(SliceMs);
			return;
		}
		if (culture != Language.ActiveCulture.Name)
		{
			// A language change: the descriptions are read again.
			culture = Language.ActiveCulture.Name;
			Array.Clear(described);
			Array.Clear(homingWhy);
			Array.Clear(stealthWhy);
			next = 0;
			Version++;
		}
		if (next < queue.Length)
		{
			ReadDescriptions(SliceMs);
		}
	}

	private static bool Started;

	private static int fieldNext;

	private static void Begin()
	{
		Started = true;
		int count = ItemLoader.ItemCount;
		fields = new BookTrait[count];
		described = new BookTrait[count];
		homingWhy = new Why[count];
		stealthWhy = new Why[count];
		penetrate = new int[count];
		unjudged = new string[count];
		queue = (from pair in ContentSamples.ItemsByType
			where pair.Key > 0 && pair.Key < count && Covers(pair.Value)
			orderby pair.Key
			select pair.Key).ToArray();
		fieldNext = 0;
	}

	// The fields, a slice at a time too: reading the mods' code for writes takes a few hundred milliseconds in all.
	private static void ReadFields(double budgetMs)
	{
		long start = Stopwatch.GetTimestamp();
		long end = start + (long)(Stopwatch.Frequency * budgetMs / 1000.0);
		int before = fieldNext;
		while (fieldNext < queue.Length && (fieldNext == before || Stopwatch.GetTimestamp() < end))
		{
			int type = queue[fieldNext++];
			try
			{
				fields[type] = FromFields(ContentSamples.ItemsByType[type], out penetrate[type]);
			}
			catch (Exception error)
			{
				ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Weapon traits: " + Lang.GetItemNameValue(type) + ": " + error.Message);
			}
		}
		fieldTicks += Stopwatch.GetTimestamp() - start;
		longestTicks = Math.Max(longestTicks, Stopwatch.GetTimestamp() - start);
		slices++;
		if (fieldNext >= queue.Length)
		{
			FieldsReady = true;
			Version++;
		}
	}

	// Held weapons: aiStyles of flails, spears, drills, held guns (Last Prism), yoyos, shortswords and sword swings.
	private static readonly HashSet<int> HeldAiStyles = new HashSet<int> { 15, 19, 20, 75, 99, 161, 190, 191 };

	// Mods' base classes for held projectiles (Calamity: BaseIdleHoldoutProjectile, BaseSpearProjectile, ...).
	private static readonly string[] HeldBaseWords = new string[9] { "Holdout", "Spear", "Shortsword", "Flail", "Laserbeam", "Whip", "Swing", "CustomUseStyle", "Yoyo" };

	// Stand: why a weapon got no pierce / walls verdict, per type ("" when it was judged).
	private static string[] unjudged = Array.Empty<string>();

	private static BookTrait FromFields(Item item, out int pen)
	{
		BookTrait value = FromFields(item, out pen, out string why);
		unjudged[item.type] = why;
		return value;
	}

	private static BookTrait FromFields(Item item, out int pen, out string why)
	{
		pen = 1;
		why = "";
		BookTrait value = BookTrait.None;
		bool weapon = BookCategories.Weapon(item);
		if (weapon && item.autoReuse)
		{
			value |= BookTrait.AutoUse;
		}
		if (item.shoot <= 0 || !ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out var p))
		{
			why = "no projectile";
			return value;
		}
		bool minion = p.minion || (Main.projPet[p.type] && p.minionSlots > 0f);
		if (weapon && minion)
		{
			value |= BookTrait.Minion;
		}
		if (weapon && p.sentry)
		{
			value |= BookTrait.Sentry;
		}
		// The shot of a weapon that uses ammo is the ammo's; an ammo whose shot depends on the launcher (rockets) or that
		// deals no damage (solutions) says nothing; no shot of its own for a minion, a sentry, a whip or a held weapon.
		why = (minion || p.sentry || ProjectileID.Sets.IsAWhip[p.type]) ? "summon or whip" : ((weapon && item.useAmmo > 0) ? "shot comes from the ammo" : ((!weapon && (item.damage <= 0 || LauncherAmmo(item.type))) ? "launcher decides / no damage" : (Held(item, p, weapon) ? "held" : (BookVanillaShots.NotAShot(p) ? "no flying shot" : ""))));
		if (why.Length > 0)
		{
			return value;
		}
		pen = p.penetrate;
		if (p.penetrate < 0 || p.penetrate > 1)
		{
			if (BookVanillaShots.StopsOnHit(p))
			{
				why = "pierce: stops on a hit";
			}
			else if (Writes(p, item, PenetrateField, killsOnHit: true))
			{
				why = "pierce: its code changes penetrate";
			}
			else
			{
				value |= BookTrait.Pierces;
			}
		}
		if (!p.tileCollide)
		{
			if (BookVanillaShots.TileCollideChanges(p))
			{
				why += ((why.Length > 0) ? "; " : "") + "walls: vanilla AI switches tileCollide";
			}
			else if (Writes(p, item, TileCollideField, killsOnHit: false))
			{
				why += ((why.Length > 0) ? "; " : "") + "walls: its code changes tileCollide";
			}
			else
			{
				value |= BookTrait.ThroughWalls;
			}
		}
		return value;
	}

	private static HashSet<int> launcherAmmo;

	// Ammo some launcher turns into its own shot (AmmoID.Sets.SpecificLauncherAmmoProjectileMatches: every rocket).
	private static bool LauncherAmmo(int type)
	{
		if (launcherAmmo == null)
		{
			launcherAmmo = new HashSet<int>();
			foreach (Dictionary<int, int> byAmmo in AmmoID.Sets.SpecificLauncherAmmoProjectileMatches.Values)
			{
				launcherAmmo.UnionWith(byAmmo.Keys);
			}
		}
		return launcherAmmo.Contains(type);
	}

	private static bool Held(Item item, Projectile p, bool weapon)
	{
		if (p.ownerHitCheck || HeldAiStyles.Contains(p.aiStyle))
		{
			return true;
		}
		if (weapon && ((item.channel && item.noUseGraphic) || item.shootSpeed <= 0f))
		{
			return true;
		}
		for (Type type = p.ModProjectile?.GetType().BaseType; type != null && type != typeof(ModProjectile) && type != typeof(object); type = type.BaseType)
		{
			string name = type.Name;
			if (HeldBaseWords.Any((string word) => name.Contains(word, StringComparison.Ordinal)))
			{
				return true;
			}
		}
		return false;
	}

	private static readonly FieldInfo PenetrateField = typeof(Projectile).GetField(nameof(Projectile.penetrate));

	private static readonly FieldInfo TileCollideField = typeof(Projectile).GetField(nameof(Projectile.tileCollide));

	private static readonly MethodInfo KillMethod = typeof(Projectile).GetMethod(nameof(Projectile.Kill), Type.EmptyTypes);

	// Per (mod type, field): whether its code stores that field outside SetDefaults.
	private static readonly Dictionary<(Type, FieldInfo, bool), bool> writes = new Dictionary<(Type, FieldInfo, bool), bool>();

	// Whether the projectile's own code (outside SetDefaults) or the item's sets the field, or, for pierce, kills the
	// projectile in a hit hook: then the default is not the whole story. Read from the IL; nothing runs. Vanilla code
	// is one big AI for every type, so only mods' classes are read.
	private static bool Writes(Projectile p, Item item, FieldInfo field, bool killsOnHit)
	{
		if (p.ModProjectile != null && WritesIn(p.ModProjectile.GetType(), typeof(ModProjectile), field, killsOnHit))
		{
			return true;
		}
		if (item.ModItem != null)
		{
			return WritesIn(item.ModItem.GetType(), typeof(ModItem), field, killsOnHit: false);
		}
		return false;
	}

	private static readonly HashSet<string> ItemShootHooks = new HashSet<string>(StringComparer.Ordinal) { "Shoot", "ModifyShootStats", "UseItem", "HoldItem", "UseStyle", "CanUseItem" };

	private static bool WritesIn(Type owner, Type stop, FieldInfo field, bool killsOnHit)
	{
		if (field == null)
		{
			return true;
		}
		(Type, FieldInfo, bool) key = (owner, field, killsOnHit);
		if (writes.TryGetValue(key, out var known))
		{
			return known;
		}
		bool found = false;
		for (Type type = owner; type != null && type != stop && type != typeof(object) && !found; type = type.BaseType)
		{
			foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			{
				// Defaults are what the trait reads; drawing, tooltips and recipes never move a shot. An item only matters
				// where it fires.
				string name = method.Name;
				if (name is "SetDefaults" or "SetStaticDefaults" or "ModifyTooltips" or "AddRecipes" || name.Contains("Draw", StringComparison.Ordinal) || (stop == typeof(ModItem) && !ItemShootHooks.Contains(name)))
				{
					continue;
				}
				bool hit = killsOnHit && method.Name is "OnHitNPC" or "ModifyHitNPC" or "OnHitPlayer";
				if (Scan(method, field, hit ? KillMethod : null))
				{
					found = true;
					break;
				}
			}
		}
		writes[key] = found;
		return found;
	}

	// The IL walked instruction by instruction (operand sizes from System.Reflection.Emit.OpCodes), so an operand is never
	// read as an opcode: stfld with a token that resolves to the field, or call/callvirt to the method.
	private static readonly OpCode[] OneByteOps = new OpCode[256];

	private static readonly OpCode[] TwoByteOps = new OpCode[256];

	private static bool opsReady;

	private static void ReadOpCodes()
	{
		foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			if (f.GetValue(null) is OpCode op)
			{
				ushort value = (ushort)op.Value;
				if (value < 0x100)
				{
					OneByteOps[value] = op;
				}
				else if ((value & 0xFF00) == 0xFE00)
				{
					TwoByteOps[value & 0xFF] = op;
				}
			}
		}
		opsReady = true;
	}

	private static bool Scan(MethodInfo method, FieldInfo field, MethodInfo call)
	{
		byte[] il;
		try
		{
			il = method.GetMethodBody()?.GetILAsByteArray();
		}
		catch
		{
			return false;
		}
		if (il == null)
		{
			return false;
		}
		if (!opsReady)
		{
			ReadOpCodes();
		}
		Module module = method.Module;
		Type[] typeArguments = method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
		Type[] methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
		int i = 0;
		while (i < il.Length)
		{
			OpCode op = (il[i] == 0xFE && i + 1 < il.Length) ? TwoByteOps[il[++i]] : OneByteOps[il[i]];
			i++;
			int size = op.OperandType switch
			{
				OperandType.InlineNone => 0,
				OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
				OperandType.InlineVar => 2,
				OperandType.InlineI8 or OperandType.InlineR => 8,
				OperandType.InlineSwitch => (i + 4 <= il.Length) ? (4 + 4 * BitConverter.ToInt32(il, i)) : 4,
				_ => 4
			};
			if (i + 4 <= il.Length && (op == OpCodes.Stfld || (call != null && (op == OpCodes.Call || op == OpCodes.Callvirt))))
			{
				int token = BitConverter.ToInt32(il, i);
				try
				{
					if (op == OpCodes.Stfld ? (module.ResolveField(token, typeArguments, methodArguments) == field) : (module.ResolveMethod(token, typeArguments, methodArguments) == call))
					{
						return true;
					}
				}
				catch
				{
					// A token the generic context cannot resolve: not ours.
				}
			}
			i += size;
		}
		return false;
	}

	private static void ReadDescriptions(double budgetMs)
	{
		long start = Stopwatch.GetTimestamp();
		long end = start + (long)(Stopwatch.Frequency * budgetMs / 1000.0);
		bool other = !Language.ActiveCulture.Name.StartsWith("en", StringComparison.Ordinal);
		if (other && english == null)
		{
			// The game was switched to another language while in a world: a one-off, after the switch's own long pause.
			LoadEnglishNow();
			return;
		}
		int before = next;
		while (next < queue.Length && (next == before || Stopwatch.GetTimestamp() < end))
		{
			int type = queue[next++];
			Item sample = ContentSamples.ItemsByType[type];
			if ((fields[type] & (BookTrait.Minion | BookTrait.Sentry)) != 0)
			{
				continue;
			}
			try
			{
				Read(type, sample, other);
			}
			catch (Exception error)
			{
				ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Weapon traits: the description of " + Lang.GetItemNameValue(type) + " failed: " + error.Message);
			}
		}
		textTicks += Stopwatch.GetTimestamp() - start;
		longestTicks = Math.Max(longestTicks, Stopwatch.GetTimestamp() - start);
		slices++;
		if (next >= queue.Length)
		{
			Version++;
			ModContent.GetInstance<ModernRecipeBook>().Logger.Info("Weapon traits (" + culture + "): " + Totals() + "; " + Timing());
		}
	}

	private static void Read(int type, Item sample, bool other)
	{
		BookVerifiedHoming.Entry verified = BookVerifiedHoming.Get(sample);
		if (verified.Traits != BookTrait.None)
		{
			described[type] |= verified.Traits;
			if ((verified.Traits & BookTrait.Homing) != 0)
			{
				homingWhy[type] = new Why(Source.Verified, verified.Note);
			}
			if ((verified.Traits & BookTrait.StealthHoming) != 0)
			{
				stealthWhy[type] = new Why(Source.Verified, verified.Note);
			}
		}
		// In another language the item's own lines are read in English: translations drop the word (Russian vanilla
		// says "attacks" for Bee Gun; the Calamity translation mod drops "homing" from Blighted Cleaver) or lag behind
		// the mod (it still gives Nanoblack Reaper homing silhouettes it no longer has). An English text that is only
		// placeholders ("[MAIN]", "[GFB]": Calamity builds the lines from other keys when shown) leaves the shown lines.
		string own = null;
		if (other && english != null)
		{
			string key = (sample.ModItem != null) ? sample.ModItem.Tooltip.Key : ("ItemTooltip." + ItemID.Search.GetName(type));
			english.TryGetValue(key, out own);
		}
		bool ownInEnglish = own != null && !Placeholder.IsMatch(own);
		// What the game shows (a tooltip line may hold several lines: Calamity joins its own), and what mods add to it.
		List<string> lines = (from line in BookItemTooltip.Lines(sample)
			where BookItemTooltip.Describes(line) && !(ownInEnglish && Own(line))
			from part in line.Text.Split('\n')
			select part).ToList();
		Apply(type, lines, Source.Description);
		if (own != null)
		{
			Apply(type, own.Split('\n').ToList(), Source.EnglishDescription);
		}
	}

	private static readonly Regex Placeholder = new Regex("\\[[A-Z][A-Z0-9_]*\\]", RegexOptions.CultureInvariant);

	// The item's own description lines, as opposed to those a mod adds in ModifyTooltips.
	private static bool Own(TooltipLine line)
	{
		if (line.Mod == "Terraria")
		{
			return line.Name.StartsWith("Tooltip", StringComparison.Ordinal);
		}
		return false;
	}

	private static void Apply(int type, List<string> lines, Source source)
	{
		foreach (string raw in lines)
		{
			BookTrait found = BookHomingText.Classify(raw);
			if (found == BookTrait.None)
			{
				continue;
			}
			string shown = BookRules.PlainText(BookRules.TagFree(raw)).Trim();
			if ((found & BookTrait.Homing) != 0 && (described[type] & BookTrait.Homing) == 0)
			{
				described[type] |= BookTrait.Homing;
				homingWhy[type] = new Why(source, shown);
			}
			if ((found & BookTrait.StealthHoming) != 0 && (described[type] & BookTrait.StealthHoming) == 0)
			{
				described[type] |= BookTrait.StealthHoming;
				stealthWhy[type] = new Why(source, shown);
			}
		}
	}

	// Every loaded mod's English texts that are item tooltips, and vanilla's (Terraria's own English file), by key.
	private static Dictionary<string, string> LoadEnglish()
	{
		Dictionary<string, string> texts = new Dictionary<string, string>(StringComparer.Ordinal);
		try
		{
			Assembly game = typeof(Main).Assembly;
			foreach (string name in game.GetManifestResourceNames().Where((string n) => n.StartsWith("Terraria.Localization.Content.en_US.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)))
			{
				using Stream stream = game.GetManifestResourceStream(name);
				using StreamReader reader = new StreamReader(stream);
				if (JObject.Parse(reader.ReadToEnd())["ItemTooltip"] is JObject tooltips)
				{
					foreach (JProperty property in tooltips.Properties())
					{
						texts["ItemTooltip." + property.Name] = property.Value.ToString();
					}
				}
			}
			MethodInfo load = typeof(LocalizationLoader).GetMethod("LoadTranslations", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[2]
			{
				typeof(Mod),
				typeof(GameCulture)
			}, null);
			if (load != null)
			{
				GameCulture en = GameCulture.FromCultureName(GameCulture.CultureName.English);
				HashSet<Mod> owners = queue.Select((int t) => ContentSamples.ItemsByType[t].ModItem?.Mod).Where((Mod m) => m != null).ToHashSet();
				foreach (Mod mod in owners)
				{
					if (!(load.Invoke(null, new object[2] { mod, en }) is List<(string, string)> pairs))
					{
						continue;
					}
					foreach (var (key, value) in pairs)
					{
						if (key.EndsWith(".Tooltip", StringComparison.Ordinal))
						{
							texts[key] = value;
						}
					}
				}
			}
		}
		catch (Exception error)
		{
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Weapon traits: the English descriptions could not be read: " + error.Message);
		}
		return texts;
	}

	internal static string Totals()
	{
		return string.Join(", ", AllRows.Select(delegate(BookTrait t)
		{
			int all = queue.Count((int type) => (Get(type) & t) != 0);
			if (t != BookTrait.Homing && t != BookTrait.StealthHoming)
			{
				return t.ToString().ToLowerInvariant() + " " + all;
			}
			Why[] why = (t == BookTrait.Homing) ? homingWhy : stealthWhy;
			int verified = queue.Count((int type) => (Get(type) & t) != 0 && why[type].Source == Source.Verified);
			int englishOnly = queue.Count((int type) => (Get(type) & t) != 0 && why[type].Source == Source.EnglishDescription);
			return t.ToString().ToLowerInvariant() + " " + all + " (" + verified + " verified list, " + englishOnly + " English text only)";
		}));
	}

	private static string Timing()
	{
		double f = Stopwatch.Frequency / 1000.0;
		return $"fields {fieldTicks / f:F1} ms, descriptions {textTicks / f:F0} ms in {slices} slices, English texts {englishTicks / f:F0} ms (one go), longest slice in game {longestTicks / f:F1} ms; while loading {loadingTicks / f:F0} ms ({(loadingOnMainThread ? "main thread" : "loading thread")})";
	}

	// Stand: how many items got no pierce / walls verdict, by reason.
	internal static string Unjudged()
	{
		return string.Join(", ", from type in queue
			let why = unjudged[type] ?? ""
			where why.Length > 0
			from part in why.Split("; ")
			group part by part into g
			orderby g.Count() descending
			select g.Key + " " + g.Count());
	}

	internal static string Status()
	{
		return $"{(Ready ? "ready" : "reading")} {next}/{queue.Length} ({culture}); {Totals()}; not judged for pierce/walls: {Unjudged()}; {Timing()}";
	}

	// Stand: every covered item, "type mod name class traits | homing why | stealth why | penetrate" a line.
	internal static string Dump()
	{
		StringBuilder text = new StringBuilder();
		text.Append(Status()).Append('\n');
		foreach (int type in queue)
		{
			Item sample = ContentSamples.ItemsByType[type];
			BookTrait value = Get(type);
			text.Append(type).Append('\t').Append(sample.ModItem?.Mod.Name ?? "Terraria").Append('\t').Append(sample.Name).Append('\t').Append(BookItemClasses.Get(type)).Append('\t').Append(value).Append('\t');
			if ((value & BookTrait.Homing) != 0)
			{
				text.Append(homingWhy[type].Source).Append(": ").Append(homingWhy[type].Detail);
			}
			text.Append('\t');
			if ((value & BookTrait.StealthHoming) != 0)
			{
				text.Append(stealthWhy[type].Source).Append(": ").Append(stealthWhy[type].Detail);
			}
			text.Append('\t').Append(penetrate[type]).Append('\t').Append(unjudged[type] ?? "");
			if (sample.shoot > 0 && ContentSamples.ProjectilesByType.TryGetValue(sample.shoot, out var p))
			{
				text.Append('\t').Append(p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type)).Append(" ai").Append(p.aiStyle).Append(" pen").Append(p.penetrate).Append(p.tileCollide ? "" : " noTile").Append(p.ownerHitCheck ? " ownerHit" : "").Append(sample.useAmmo > 0 ? " ammo-weapon" : "").Append((sample.channel && sample.noUseGraphic) ? " channel-held" : "");
			}
			text.Append('\n');
		}
		return text.ToString();
	}

	// Stand: rebuild everything now (fields and every description) and report.
	internal static string Rebuild()
	{
		Clear();
		Begin();
		while (!FieldsReady)
		{
			ReadFields(1000.0);
		}
		culture = Language.ActiveCulture.Name;
		bool other = !culture.StartsWith("en", StringComparison.Ordinal);
		if (other)
		{
			ReadDescriptions(0.0);
		}
		while (next < queue.Length)
		{
			ReadDescriptions(1000.0);
		}
		return Status();
	}
}

internal sealed class BookItemTraitsSystem : ModSystem
{
	// The loading screen pays for the fields and the English texts, where a few hundred milliseconds go unnoticed.
	public override void OnWorldLoad()
	{
		BookItemTraits.Prepare();
	}

	public override void PostUpdateEverything()
	{
		BookItemTraits.Pump();
	}

	public override void Unload()
	{
		BookItemTraits.Clear();
	}
}
