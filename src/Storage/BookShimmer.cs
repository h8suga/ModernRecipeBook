using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook.Storage;

// Shimmer from the book (docs/shimmer.md). What an item turns into in Shimmer, by vanilla's own rules and in their order
// (Item.CanShimmer / GetShimmered, TML:Terraria/Item.cs:49561-49850), and doing it through a Magic Storage network that
// has a Decrafting Interface, as MS's own window does: the server takes the items out of that network, shimmers them as
// the liquid would and puts the results back in (BookNearbyServer.Shimmer). Without Magic Storage the plan still shows.
internal static class BookShimmer
{
	internal enum Kind : byte
	{
		// Into another item, one for one (ShimmerTransformToItem, the Moon Lord pairs, Lunar Brick by the moon phase, a
		// music box into the blank one).
		Transform,
		// Back into its recipe's ingredients (or the recipe's own shimmer results), one craft's worth at a time.
		Decraft,
		// Coins: gone, coin luck for the player.
		CoinLuck,
		// A critter item lets its critter (or the critter's shimmer form) go.
		Critter,
		// A Gel Balloon in a world without the rainbow Town Slime yet: that slime arrives.
		TownSlime
	}

	// What shimmering `Unit` of the item gives: Results per unit (Transform, Decraft), CoinLuck per item, or the NPC that
	// comes out (Critter, TownSlime). Ready: it shimmers now; otherwise GateNpc is the boss to beat first (Skeletron, Golem,
	// Moon Lord's head, which bears his name) or 0 for a mod's own rule. Recipe: the decraft's recipe index.
	internal sealed record Plan(int Item, Kind What, int Unit, IReadOnlyList<(int Type, int Stack)> Results, int CoinLuck, int Npc, bool Ready, int GateNpc, int Recipe = -1);

	// Most items one request asks for (the server checks it again).
	internal const int MaxItems = 99999;

	private static readonly Dictionary<int, Plan> Plans = new Dictionary<int, Plan>();

	// What the plans were built under: the moon phase, the bosses and the Town Slime change them; a mod's own gates (Calamity's) are read again every ten seconds.
	private static (int Moon, bool Skeletron, bool Golem, bool MoonLord, bool Slime, int Recipes, int Tick) PlansKey;

	// After the Moon Lord (Item.GetShimmered): Rod of Discord, Clentaminator and the bottomless buckets.
	private static readonly int[] MoonLordPairs = new int[4] { ItemID.RodofDiscord, ItemID.Clentaminator, ItemID.BottomlessBucket, ItemID.BottomlessShimmerBucket };

	// Vanilla's plan for `type`, or null when it does not shimmer at all (Item.CanShimmer says no for a reason no boss
	// lifts). Pure: callers on the server and the client get the same answer for the same world.
	internal static Plan For(int type)
	{
		if (type <= 0 || type >= ItemLoader.ItemCount || !ContentSamples.ItemsByType.TryGetValue(type, out var sample) || sample.IsAir)
		{
			return null;
		}
		var key = ((int)Main.GetMoonPhase(), NPC.downedBoss3, NPC.downedGolemBoss, NPC.downedMoonlord, NPC.unlockedSlimeRainbowSpawn, Recipe.numRecipes, (int)(Main.GameUpdateCount / 600));
		if (key != PlansKey)
		{
			Plans.Clear();
			PlansKey = key;
		}
		if (!Plans.TryGetValue(type, out var plan))
		{
			plan = Calamity(Build(sample));
			Plans[type] = plan;
		}
		return plan;
	}

	// Calamity's own shimmer rules (CalamityMod.ILEditing.ILChanges). The Rod of Discord stays locked until Supreme
	// Calamitas and then the Exo Mechs are beaten (AdjustShimmerRequirements, a hook on IsItemTransformLocked, so the lock
	// itself is already in the plan; this names the boss). A Plagued Containment Brick before Golem does not shimmer at
	// all: the whole stack is gone and three Plague Nanodroids come out (ShimmerEffectEdits), which the book will not do to
	// a stack in storage, so it waits for Golem.
	private static Plan Calamity(Plan plan)
	{
		if (plan == null || !ModLoader.TryGetMod("CalamityMod", out Mod calamity))
		{
			return plan;
		}
		if (plan.Item == ItemID.RodofDiscord && !plan.Ready && NPC.downedMoonlord)
		{
			Type downed = calamity.Code.GetType("CalamityMod.DownedBossSystem");
			bool scal = downed?.GetProperty("downedCalamitas")?.GetValue(null) is true;
			string boss = scal ? "Draedon" : "SupremeCalamitas";
			return plan with { GateNpc = calamity.TryFind<ModNPC>(boss, out ModNPC npc) ? npc.Type : 0 };
		}
		if (!NPC.downedGolemBoss && calamity.TryFind<ModItem>("PlaguedContainmentBrick", out ModItem brick) && plan.Item == brick.Type)
		{
			return plan with { Ready = false, GateNpc = NPCID.Golem };
		}
		return plan;
	}

	private static Plan Build(Item sample)
	{
		int type = sample.type;
		int same = (ItemID.Sets.ShimmerCountsAsItem[type] != -1) ? ItemID.Sets.ShimmerCountsAsItem[type] : type;
		// A decraft lock holds the whole item back, whatever else it would do (Item.CanShimmer first line).
		int gate = 0;
		bool locked = false;
		int recipeIndex = ShimmerTransforms.GetDecraftingRecipeIndex(same);
		// The running game also locks items with no decraft recipe (the Rod of Discord before the Moon Lord), so the boss is
		// read off the recipe only when there is one.
		if (ShimmerTransforms.IsItemTransformLocked(same))
		{
			locked = true;
			if (recipeIndex >= 0)
			{
				gate = (!NPC.downedBoss3 && ShimmerTransforms.RecipeSets.PostSkeletron[recipeIndex]) ? NPCID.SkeletronHead : ((!NPC.downedGolemBoss && ShimmerTransforms.RecipeSets.PostGolem[recipeIndex]) ? NPCID.Golem : 0);
			}
		}
		// GetShimmered's order.
		if (ItemID.Sets.CoinLuckValue[same] > 0)
		{
			return new Plan(type, Kind.CoinLuck, 1, Array.Empty<(int, int)>(), ItemID.Sets.CoinLuckValue[same], 0, !locked, gate);
		}
		if (MoonLordPairs.Contains(same))
		{
			int into = same switch
			{
				ItemID.RodofDiscord => ItemID.RodOfHarmony,
				ItemID.Clentaminator => ItemID.Clentaminator2,
				ItemID.BottomlessBucket => ItemID.BottomlessShimmerBucket,
				_ => ItemID.BottomlessBucket
			};
			// Before the Moon Lord these fall through to the rules below, and none of them has another one.
			return new Plan(type, Kind.Transform, 1, new (int, int)[1] { (into, 1) }, 0, 0, NPC.downedMoonlord && !locked, NPC.downedMoonlord ? gate : NPCID.MoonLordHead);
		}
		if (same == ItemID.LunarBrick)
		{
			int into = Main.GetMoonPhase() switch
			{
				MoonPhase.QuarterAtRight => ItemID.StarRoyaleBrick,
				MoonPhase.HalfAtRight => ItemID.CryocoreBrick,
				MoonPhase.ThreeQuartersAtRight => ItemID.CosmicEmberBrick,
				MoonPhase.Full => ItemID.HeavenforgeBrick,
				MoonPhase.ThreeQuartersAtLeft => ItemID.LunarRustBrick,
				MoonPhase.HalfAtLeft => ItemID.AstraBrick,
				MoonPhase.QuarterAtLeft => ItemID.DarkCelestialBrick,
				_ => ItemID.MercuryBrick
			};
			return new Plan(type, Kind.Transform, 1, new (int, int)[1] { (into, 1) }, 0, 0, !locked, gate);
		}
		if (sample.createTile == TileID.MusicBoxes)
		{
			return new Plan(type, Kind.Transform, 1, new (int, int)[1] { (ItemID.MusicBox, 1) }, 0, 0, !locked, gate);
		}
		if (ItemID.Sets.ShimmerTransformToItem[same] > 0)
		{
			return new Plan(type, Kind.Transform, 1, new (int, int)[1] { (ItemID.Sets.ShimmerTransformToItem[same], 1) }, 0, 0, !locked, gate);
		}
		if (type == ItemID.GelBalloon)
		{
			// Once the slime is there, the balloon does nothing in Shimmer (GetShimmered returns before any other rule).
			return NPC.unlockedSlimeRainbowSpawn ? null : new Plan(type, Kind.TownSlime, 1, Array.Empty<(int, int)>(), 0, NPCID.TownSlimeRainbow, !locked, gate);
		}
		if (sample.makeNPC > 0)
		{
			int npc = (NPCID.Sets.ShimmerTransformToNPC[sample.makeNPC] >= 0) ? NPCID.Sets.ShimmerTransformToNPC[sample.makeNPC] : sample.makeNPC;
			return new Plan(type, Kind.Critter, 1, Array.Empty<(int, int)>(), 0, npc, !locked, gate);
		}
		if (recipeIndex >= 0)
		{
			Recipe recipe = Main.recipe[recipeIndex];
			List<(int, int)> results = new List<(int, int)>();
			foreach (Item item in (IEnumerable<Item>)(recipe.customShimmerResults ?? recipe.requiredItem))
			{
				if (item.type <= 0)
				{
					break;
				}
				int amount = item.stack;
				RecipeLoader.ConsumeIngredient(recipe, item.type, ref amount, isDecrafting: true);
				if (amount > 0)
				{
					results.Add((item.type, amount));
				}
			}
			return new Plan(type, Kind.Decraft, Math.Max(1, recipe.createItem.stack), results, 0, 0, !locked, gate, recipeIndex);
		}
		return null;
	}

	// Doing it. The network: among the counted ones with a Decrafting Interface (BookSources.ShimmerNetworks), the one
	// that holds most of the item. At most what it holds, in whole units.
	internal static (BookMagicStorage.Network Network, long Count) Best(int type)
	{
		BookMagicStorage.Network best = null;
		long most = -1;
		foreach ((BookMagicStorage.Network network, long count) in BookSources.ShimmerNetworks(type))
		{
			if (count > most)
			{
				best = network;
				most = count;
			}
		}
		return (best, Math.Max(0, most));
	}

	// A counted network with a Decrafting Interface is in reach: the UI offers shimmer buttons only then, so a world
	// without one shows no buttons that could never work.
	internal static bool InReach => BookSources.ShimmerNetworks(0).Any();

	// How many items one shimmer can take now (whole units, at most MaxItems); 0 when Reason says why not.
	internal static int Max(int type)
	{
		Plan plan = For(type);
		if (plan == null || Reason(type).Length > 0)
		{
			return 0;
		}
		long count = Math.Min(Best(type).Count, MaxItems);
		return (int)(count - count % plan.Unit);
	}

	// A request in flight (a multiplayer client waits for the server's answer; five seconds at most).
	internal static bool Pending => PendingSince != 0 && Main.GameUpdateCount - PendingSince < 300;

	private static ulong PendingSince;

	// The plan the last request was made under: a shimmer can change the plan itself (the Town Slime arrives, so the
	// balloon no longer shimmers), and the result line names what it did.
	private static Plan LastPlan;

	// The progression step of the boss a locked plan waits for (-1 when none): the book's boss links and names go by step.
	internal static int GateStep(Plan plan)
	{
		return (plan?.GateNpc > 0) ? BookSteps.Npc(plan.GateNpc) : -1;
	}

	// The boss as the book names its step ("Exo Mechs", "Moon Lord"), else the NPC's own name.
	internal static string GateName(Plan plan)
	{
		string name = BookSteps.Name(GateStep(plan));
		return (name.Length > 0) ? name : Lang.GetNPCNameValue(plan.GateNpc);
	}

	// Why the item cannot be shimmered from the book now (None: it can), for the UI to word or link it; Reason words it.
	internal enum Why : byte
	{
		None,
		DoesNotShimmer,
		Locked,
		NoMagicStorage,
		StorageOff,
		Pending,
		NoInterface,
		NoneInNetwork,
		NeedsUnit,
		CantRightNow
	}

	internal static Why WhyNot(int type)
	{
		Plan plan = For(type);
		if (plan == null)
		{
			return Why.DoesNotShimmer;
		}
		if (!plan.Ready)
		{
			return Why.Locked;
		}
		if (!BookMagicStorage.Installed || !BookMagicStorage.Shimmers || !BookMagicStorage.Writable)
		{
			return Why.NoMagicStorage;
		}
		if (!BookSources.Enabled)
		{
			return Why.StorageOff;
		}
		if (Pending)
		{
			return Why.Pending;
		}
		(BookMagicStorage.Network network, long count) = Best(type);
		if (network == null)
		{
			return Why.NoInterface;
		}
		if (count <= 0)
		{
			return Why.NoneInNetwork;
		}
		if (count < plan.Unit)
		{
			return Why.NeedsUnit;
		}
		Player player = Main.LocalPlayer;
		return (!player.active || player.dead || player.ghost) ? Why.CantRightNow : Why.None;
	}

	// The Decrafting Interface by the name Magic Storage gives it in the language shown ("Storage Aether Interface" in
	// 0.7), which the reasons name.
	internal static string InterfaceName => (BookMagicStorage.DecraftingItem > 0) ? Lang.GetItemNameValue(BookMagicStorage.DecraftingItem) : "Decrafting Interface";

	// Why the item cannot be shimmered from the book now, or "" when it can.
	internal static string Reason(int type)
	{
		Plan plan = For(type);
		return WhyNot(type) switch
		{
			Why.None => "",
			Why.DoesNotShimmer => L("Shimmer.DoesNotShimmer"),
			Why.Locked => (plan.GateNpc > 0) ? L("Shimmer.AfterBoss", GateName(plan)) : L("Shimmer.NotNow"),
			Why.NoMagicStorage => L("Shimmer.NeedsMagicStorage", InterfaceName),
			Why.StorageOff => L("Shimmer.StorageOff"),
			Why.Pending => L("Shimmer.Shimmering"),
			Why.NoInterface => L("Shimmer.NoInterface", InterfaceName),
			Why.NoneInNetwork => L("Shimmer.NoneInNetwork", InterfaceName),
			Why.NeedsUnit => L("Shimmer.NeedsUnit", plan.Unit),
			_ => L("Shimmer.CantRightNow")
		};
	}

	// Shimmers up to `items` of the type (rounded down to whole units) in the best network. The result is the card's
	// status line (BookCrafting.LastMessage); on a multiplayer client it comes with the server's answer.
	internal static void TryShimmer(int type, int items)
	{
		string reason = Reason(type);
		if (reason.Length > 0)
		{
			BookCrafting.LastMessage = reason;
			return;
		}
		Plan plan = For(type);
		(BookMagicStorage.Network network, long count) = Best(type);
		long amount = Math.Min(Math.Min(Math.Max(1, items), MaxItems), count);
		amount -= amount % plan.Unit;
		if (amount <= 0)
		{
			BookCrafting.Say(() => L("Shimmer.NeedsUnit", plan.Unit));
			return;
		}
		LastPlan = plan;
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			List<Item> results = new List<Item>();
			BookNearbyServer.Status status = BookNearbyServer.Shimmer(Main.myPlayer, network.Position, type, (int)amount, results, out int done, out int spilled);
			Finish(status, type, done, Summarize(results), spilled);
			return;
		}
		ModPacket packet = BookNearbyServer.NewPacket(BookMessage.NearbyShimmer);
		packet.Write(network.Position.X);
		packet.Write(network.Position.Y);
		packet.Write7BitEncodedInt(type);
		packet.Write7BitEncodedInt((int)amount);
		packet.Send();
		PendingSince = Math.Max(1UL, Main.GameUpdateCount);
		BookCrafting.Say("Shimmer.Shimmering");
	}

	// The results by type, as the server deposited them, at most 32 kinds for the reply.
	internal static List<(int Type, int Stack)> Summarize(List<Item> results)
	{
		List<(int Type, int Stack)> summary = new List<(int, int)>();
		foreach (Item item in results)
		{
			int at = summary.FindIndex(((int Type, int Stack) s) => s.Type == item.type);
			if (at >= 0)
			{
				summary[at] = (item.type, summary[at].Stack + item.stack);
			}
			else if (summary.Count < 32)
			{
				summary.Add((item.type, item.stack));
			}
		}
		return summary;
	}

	// Server: the answer to a client's NearbyShimmer.
	internal static void SendResult(int p, BookNearbyServer.Status status, int type, int done, List<Item> results, int spilled)
	{
		List<(int Type, int Stack)> summary = Summarize(results);
		ModPacket packet = BookNearbyServer.NewPacket(BookMessage.NearbyShimmered);
		packet.Write((byte)status);
		packet.Write7BitEncodedInt(type);
		packet.Write7BitEncodedInt(done);
		packet.Write7BitEncodedInt(spilled);
		packet.Write((byte)summary.Count);
		foreach ((int t, int stack) in summary)
		{
			packet.Write7BitEncodedInt(t);
			packet.Write7BitEncodedInt(stack);
		}
		packet.Send(p);
	}

	// Client: the server's answer.
	internal static void Receive(BinaryReader reader)
	{
		BookNearbyServer.Status status = (BookNearbyServer.Status)reader.ReadByte();
		int type = reader.Read7BitEncodedInt();
		int done = reader.Read7BitEncodedInt();
		int spilled = reader.Read7BitEncodedInt();
		int n = reader.ReadByte();
		List<(int Type, int Stack)> results = new List<(int, int)>(n);
		for (int i = 0; i < n; i++)
		{
			results.Add((reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt()));
		}
		Finish(status, type, done, results, spilled);
	}

	// "Shimmered 10 Wooden Arrow: 10 Wood, 1 Stone Block" or why nothing was shimmered.
	private static void Finish(BookNearbyServer.Status status, int type, int done, List<(int Type, int Stack)> results, int spilled)
	{
		PendingSince = 0;
		BookSources.Refresh(force: true);
		BookCrafting.Refresh(force: true);
		if (done <= 0)
		{
			BookCrafting.Say(status switch
			{
				BookNearbyServer.Status.Off => "Shimmer.StorageOff",
				BookNearbyServer.Status.Busy => "Shimmer.TooFast",
				BookNearbyServer.Status.Invalid => "Shimmer.SomethingChanged",
				_ => "Shimmer.NothingShimmered"
			});
			return;
		}
		Plan plan = (LastPlan?.Item == type) ? LastPlan : For(type);
		BookCrafting.Say(() =>
		{
			string what = plan?.What switch
			{
				Kind.CoinLuck => L("Shimmer.CoinLuck"),
				Kind.Critter => L("Shimmer.Released", Lang.GetNPCNameValue(plan.Npc)),
				Kind.TownSlime => L("Shimmer.Arrived", Lang.GetNPCNameValue(plan.Npc)),
				_ => Items(results)
			};
			string line = L("Shimmer.Done", done, Lang.GetItemNameValue(type), what);
			if (spilled > 0)
			{
				line += " · " + L("Shimmer.Spilled", spilled);
			}
			if (status == BookNearbyServer.Status.Partial)
			{
				line += " · " + L("Shimmer.Partly");
			}
			return line;
		});
	}

	private static string Items(List<(int Type, int Stack)> results)
	{
		string shown = string.Join(", ", results.Take(3).Select(((int Type, int Stack) r) => r.Stack + " " + Lang.GetItemNameValue(r.Type)));
		return (results.Count > 3) ? (shown + " " + L("Shimmer.More", results.Count - 3)) : shown;
	}

	// Stand: stand.ps1 c call p=ModernRecipeBook.Storage.BookShimmer.Describe a=<item type>
	internal static string Describe(int type)
	{
		Plan plan = For(type);
		if (plan == null)
		{
			return "no plan: " + Reason(type);
		}
		string results = string.Join(",", plan.Results.Select(((int Type, int Stack) r) => r.Type + "x" + r.Stack));
		(BookMagicStorage.Network network, long count) = Best(type);
		return $"{plan.What} unit={plan.Unit} results=[{results}] coin={plan.CoinLuck} npc={plan.Npc} ready={plan.Ready} gate={plan.GateNpc} network={network?.Position.ToString() ?? "none"} count={count} max={Max(type)} reason=\"{Reason(type)}\"";
	}

	// Stand: stand.ps1 c call p=ModernRecipeBook.Storage.BookShimmer.Test a=<item type> a=<items>
	internal static string Test(int type, int items)
	{
		TryShimmer(type, items);
		return BookCrafting.LastMessage;
	}
}
