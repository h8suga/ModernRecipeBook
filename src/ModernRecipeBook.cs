using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// Packets between a client and the server; the first byte says which.
internal enum BookMessage : byte
{
	MapSections,
	// Map markers (BookMapMarkers): a client asks for the list or a change, the server answers to all or to one.
	MarkerList,
	MarkerCreate,
	MarkerEdit,
	MarkerDelete,
	MarkerSet,
	MarkerRemoved,
	MarkerRefused,
	// Nearby storage (BookNearbyServer, BookSources): the chest mirror, pulls and deposits (docs/nearby-storage.md §4.2).
	NearbySubscribe,
	NearbyChestSync,
	NearbyPull,
	NearbyGive,
	NearbyAck,
	NearbyDeposit,
	// The Storage tab's Quick Stack into the counted networks: the client's locked slots, then the server's result.
	NearbyQuickStack,
	NearbyStacked
}

public sealed class ModernRecipeBook : Mod
{
	internal static ModKeybind ToggleBook;

	internal static ModKeybind ChainBack;

	internal static ModKeybind ChainForward;

	internal static ModKeybind MapTeleport;

	// The quick search on one key, for those who would rather not press Ctrl+F; unbound, so it takes no one's key.
	internal static ModKeybind QuickSearch;

	internal const string MapTeleportKey = "Mouse3";

	public override void Load()
	{
		if (!Main.dedServ)
		{
			ToggleBook = KeybindLoader.RegisterKeybind(this, "ToggleBook", "Mouse3");
			ChainBack = KeybindLoader.RegisterKeybind(this, "ChainBack", "Mouse4");
			ChainForward = KeybindLoader.RegisterKeybind(this, "ChainForward", "Mouse5");
			MapTeleport = KeybindLoader.RegisterKeybind(this, "MapTeleport", MapTeleportKey);
			QuickSearch = KeybindLoader.RegisterKeybind(this, "QuickSearch", Microsoft.Xna.Framework.Input.Keys.None);
		}
	}

	// Boss Checklist's registration calls ("LogBoss", "LogMiniBoss", "LogEvent"), kept while BookSteps lets a mod register
	// its bosses with the book (Calamity's WeakReferenceSupport.AddCalamityBosses). Anything else, and any call outside
	// that, is ignored: the book is no Boss Checklist for other mods.
	private static List<object[]> bossLogs;

	internal static List<object[]> CaptureBossLogs(Action register)
	{
		bossLogs = new List<object[]>();
		try
		{
			register();
			return bossLogs;
		}
		finally
		{
			bossLogs = null;
		}
	}

	public override object Call(params object[] args)
	{
		if (bossLogs != null && args.Length > 0 && args[0] is "LogBoss" or "LogMiniBoss" or "LogEvent")
		{
			bossLogs.Add(args);
			return "Success";
		}
		return null;
	}

	public override void HandlePacket(BinaryReader reader, int whoAmI)
	{
		BookMessage kind = (BookMessage)reader.ReadByte();
		switch (kind)
		{
		case BookMessage.MapSections:
			BookMapTeleport.SendSections(reader, whoAmI);
			break;
		case >= BookMessage.MarkerList and <= BookMessage.MarkerRefused:
			BookMapMarkers.Receive(kind, reader, whoAmI);
			break;
		case >= BookMessage.NearbySubscribe and <= BookMessage.NearbyStacked:
			BookNearbyServer.Receive(kind, reader, whoAmI);
			break;
		}
	}

	public override void Unload()
	{
		ToggleBook = (ChainBack = (ChainForward = (MapTeleport = (QuickSearch = null))));
	}
}
