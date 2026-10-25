using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static ModernRecipeBook.BookText;

namespace ModernRecipeBook;

internal sealed class BookMapMarker
{
	internal int Id;

	internal short X;

	internal short Y;

	internal string Name;

	internal byte Badge;

	internal string Author;

	// UTC ticks of the server's clock.
	internal long Created;

	internal long Edited;

	internal void Write(BinaryWriter writer)
	{
		writer.Write(Id);
		writer.Write(X);
		writer.Write(Y);
		writer.Write(Name);
		writer.Write(Badge);
		writer.Write(Author);
		writer.Write(Created);
		writer.Write(Edited);
	}

	internal static BookMapMarker Read(BinaryReader reader)
	{
		return new BookMapMarker { Id = reader.ReadInt32(), X = reader.ReadInt16(), Y = reader.ReadInt16(), Name = reader.ReadString(), Badge = reader.ReadByte(), Author = reader.ReadString(), Created = reader.ReadInt64(), Edited = reader.ReadInt64() };
	}
}

// Shared map markers. The server (the world itself in single player) owns the list, checks every change and saves the
// list with the world; a client mirrors it: the whole list when it enters the world, then every change as it happens.
// The author owns a marker; the host may change any. Authors are matched by player name, which any client can claim:
// fine for friendly co-op, not a security boundary.
internal sealed class BookMapMarkers : ModSystem
{
	internal const int NameLength = 32;

	internal const int PerPlayer = 100;

	// At most one change (or list request) per player every half second.
	private const int ChangeTicks = 30;

	private const int ListPart = 300;

	internal enum Refusal : byte
	{
		None,
		Off,
		Badge,
		Place,
		Limit,
		TooFast,
		NotYours,
		Missing
	}

	internal static readonly List<BookMapMarker> Markers = new List<BookMapMarker>();

	private static readonly uint[] LastChange = new uint[256];

	// List requests have their own clock, so a change just made never costs a joining player the list.
	private static readonly uint[] LastList = new uint[256];

	private static int NextId = 1;

	// A client's estimate of the server clock minus its own, from the last packet that carried the server time.
	private static long ClockOffset;

	internal static bool Enabled => ModContent.GetInstance<BookServerConfig>().MapMarkers;

	// The server clock, in the UTC ticks markers are stamped with.
	internal static long Now => DateTime.UtcNow.Ticks + ClockOffset;

	public override void ClearWorld()
	{
		Markers.Clear();
		NextId = 1;
		ClockOffset = 0;
		Array.Clear(LastChange);
		Array.Clear(LastList);
	}

	public override void SaveWorldData(TagCompound tag)
	{
		tag["Markers"] = Markers.Select((BookMapMarker m) => new TagCompound
		{
			["Id"] = m.Id,
			["X"] = m.X,
			["Y"] = m.Y,
			["Name"] = m.Name,
			["Badge"] = m.Badge,
			["Author"] = m.Author,
			["Created"] = m.Created,
			["Edited"] = m.Edited
		}).ToList();
	}

	public override void LoadWorldData(TagCompound tag)
	{
		Markers.Clear();
		foreach (TagCompound m in tag.GetList<TagCompound>("Markers"))
		{
			Markers.Add(new BookMapMarker { Id = m.GetInt("Id"), X = m.GetShort("X"), Y = m.GetShort("Y"), Name = m.GetString("Name"), Badge = m.GetByte("Badge"), Author = m.GetString("Author"), Created = m.GetLong("Created"), Edited = m.GetLong("Edited") });
		}
		NextId = Markers.Count == 0 ? 1 : Markers.Max((BookMapMarker m) => m.Id) + 1;
	}

	// Client side: what the panel offers. The server decides again.
	internal static bool CanChange(BookMapMarker marker)
	{
		return Main.netMode == NetmodeID.SinglePlayer || marker.Author == Main.LocalPlayer.name || Main.countsAsHostForGameplay[Main.myPlayer];
	}

	// The name, or for an unnamed marker its badge in the current language (nothing is stored for it).
	internal static string Title(BookMapMarker marker)
	{
		if (marker.Name.Length > 0)
		{
			return marker.Name;
		}
		BookMarkerBadges.Badge badge = BookMarkerBadges.Get(marker.Badge);
		return L(badge.Name);
	}

	// "Placed by X · 2 h ago".
	internal static string Placed(BookMapMarker marker)
	{
		return L("Markers.PlacedBy", marker.Author, Ago(marker.Created));
	}

	internal static string Ago(long stamp)
	{
		return BookText.Ago(TimeSpan.FromTicks(Math.Max(0L, Now - stamp)));
	}

	internal static BookMapMarker Find(int id)
	{
		return Markers.Find((BookMapMarker m) => m.Id == id);
	}

	// A multiplayer client entering the world asks for the list.
	internal static void RequestList()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
		{
			Packet(BookMessage.MarkerList).Send();
		}
	}

	internal static void Create(int x, int y, string name, byte badge)
	{
		Send(BookMessage.MarkerCreate, 0, x, y, name, badge);
	}

	internal static void Edit(int id, string name, byte badge)
	{
		Send(BookMessage.MarkerEdit, id, 0, 0, name, badge);
	}

	internal static void Delete(int id)
	{
		Send(BookMessage.MarkerDelete, id, 0, 0, "", 0);
	}

	// Single player applies the change itself; a client sends it to the server, whose answer updates the list.
	private static void Send(BookMessage kind, int id, int x, int y, string name, byte badge)
	{
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			Refused(Apply(kind, Main.myPlayer, id, x, y, name, badge));
			return;
		}
		ModPacket packet = Packet(kind);
		packet.Write(id);
		packet.Write((short)x);
		packet.Write((short)y);
		packet.Write(name ?? "");
		packet.Write(badge);
		packet.Send();
	}

	internal static void Receive(BookMessage kind, BinaryReader reader, int whoAmI)
	{
		if (Main.netMode == NetmodeID.Server)
		{
			if (kind == BookMessage.MarkerList)
			{
				if (!TooFast(LastList, whoAmI))
				{
					SendList(whoAmI);
				}
				return;
			}
			int id = reader.ReadInt32();
			int x = reader.ReadInt16();
			int y = reader.ReadInt16();
			string name = reader.ReadString();
			byte badge = reader.ReadByte();
			Refusal refusal = Apply(kind, whoAmI, id, x, y, name, badge);
			if (refusal != Refusal.None)
			{
				ModPacket packet = Packet(BookMessage.MarkerRefused);
				packet.Write((byte)refusal);
				packet.Send(whoAmI);
			}
			return;
		}
		switch (kind)
		{
		case BookMessage.MarkerList:
			ClockOffset = reader.ReadInt64() - DateTime.UtcNow.Ticks;
			if (reader.ReadBoolean())
			{
				Markers.Clear();
			}
			for (int count = reader.ReadInt32(); count > 0; count--)
			{
				Markers.Add(BookMapMarker.Read(reader));
			}
			break;
		case BookMessage.MarkerSet:
		{
			ClockOffset = reader.ReadInt64() - DateTime.UtcNow.Ticks;
			BookMapMarker marker = BookMapMarker.Read(reader);
			int index = Markers.FindIndex((BookMapMarker m) => m.Id == marker.Id);
			if (index < 0)
			{
				Markers.Add(marker);
			}
			else
			{
				Markers[index] = marker;
			}
			break;
		}
		case BookMessage.MarkerRemoved:
		{
			int id = reader.ReadInt32();
			Markers.RemoveAll((BookMapMarker m) => m.Id == id);
			break;
		}
		case BookMessage.MarkerRefused:
			Refused((Refusal)reader.ReadByte());
			break;
		}
	}

	// The authority: the server, or the single player world. Every change is checked here, whatever the client showed.
	private static Refusal Apply(BookMessage kind, int player, int id, int x, int y, string name, byte badge)
	{
		if (!Enabled)
		{
			return Refusal.Off;
		}
		if (TooFast(LastChange, player))
		{
			return Refusal.TooFast;
		}
		string author = Main.player[player].name;
		name = Clean(name);
		BookMapMarker marker = null;
		if (kind != BookMessage.MarkerCreate)
		{
			marker = Find(id);
			if (marker == null)
			{
				return Refusal.Missing;
			}
			if (marker.Author != author && Main.netMode == NetmodeID.Server && !Main.countsAsHostForGameplay[player])
			{
				return Refusal.NotYours;
			}
		}
		// The name may be empty (the badge then titles the marker); the badge must exist.
		if (kind != BookMessage.MarkerDelete && badge >= BookMarkerBadges.Count)
		{
			return Refusal.Badge;
		}
		long now = DateTime.UtcNow.Ticks;
		switch (kind)
		{
		case BookMessage.MarkerCreate:
			if (!WorldGen.InWorld(x, y))
			{
				return Refusal.Place;
			}
			if (Markers.Count((BookMapMarker m) => m.Author == author) >= PerPlayer)
			{
				return Refusal.Limit;
			}
			marker = new BookMapMarker { Id = NextId++, X = (short)x, Y = (short)y, Name = name, Badge = badge, Author = author, Created = now, Edited = now };
			Markers.Add(marker);
			break;
		case BookMessage.MarkerEdit:
			marker.Name = name;
			marker.Badge = badge;
			marker.Edited = now;
			break;
		case BookMessage.MarkerDelete:
			Markers.Remove(marker);
			break;
		default:
			return Refusal.Missing;
		}
		LastChange[player] = Math.Max(1u, Main.GameUpdateCount);
		if (Main.netMode == NetmodeID.Server)
		{
			ModPacket packet;
			if (kind == BookMessage.MarkerDelete)
			{
				packet = Packet(BookMessage.MarkerRemoved);
				packet.Write(marker.Id);
			}
			else
			{
				packet = Packet(BookMessage.MarkerSet);
				packet.Write(now);
				marker.Write(packet);
			}
			packet.Send();
		}
		return Refusal.None;
	}

	// A packet holds 64 KB and a marker takes up to about 200 bytes, so the list goes in parts; the first part tells the client
	// to start over.
	private static void SendList(int toClient)
	{
		for (int start = 0; start == 0 || start < Markers.Count; start += ListPart)
		{
			int count = Math.Min(ListPart, Markers.Count - start);
			ModPacket packet = Packet(BookMessage.MarkerList);
			packet.Write(DateTime.UtcNow.Ticks);
			packet.Write(start == 0);
			packet.Write(count);
			for (int i = start; i < start + count; i++)
			{
				Markers[i].Write(packet);
			}
			packet.Send(toClient);
		}
		LastList[toClient] = Math.Max(1u, Main.GameUpdateCount);
	}

	private static bool TooFast(uint[] last, int player)
	{
		// 0: nothing from this player yet (a fresh server counts its updates from 0 too).
		return Main.netMode == NetmodeID.Server && last[player] != 0 && Main.GameUpdateCount - last[player] < ChangeTicks;
	}

	// Trimmed, no control characters (no line breaks in the map text), at most NameLength without splitting a pair.
	internal static string Clean(string name)
	{
		string clean = new string((name ?? "").Where((char c) => !char.IsControl(c)).ToArray()).Trim();
		if (clean.Length > NameLength)
		{
			clean = clean.Substring(0, char.IsHighSurrogate(clean[NameLength - 1]) ? NameLength - 1 : NameLength).TrimEnd();
		}
		return clean;
	}

	private static void Refused(Refusal refusal)
	{
		string message = refusal switch
		{
			Refusal.Off => L("Markers.MarkersAreOffOnThisServer"),
			Refusal.Place => L("Markers.NotHere"),
			Refusal.Limit => L("Markers.MarkersAtMost", PerPlayer),
			Refusal.TooFast => L("Markers.TooFastTryAgain"),
			Refusal.NotYours => L("Markers.NotYourMarker"),
			Refusal.Missing => L("Markers.ThatMarkerIsGone"),
			_ => null
		};
		if (message != null && !Main.dedServ)
		{
			BookMapTeleport.Say(message);
		}
	}

	private static ModPacket Packet(BookMessage kind)
	{
		ModPacket packet = ModContent.GetInstance<ModernRecipeBook>().GetPacket();
		packet.Write((byte)kind);
		return packet;
	}
}
