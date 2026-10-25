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

	internal const string MapTeleportKey = "Mouse3";

	public override void Load()
	{
		if (!Main.dedServ)
		{
			ToggleBook = KeybindLoader.RegisterKeybind(this, "ToggleBook", "Mouse3");
			ChainBack = KeybindLoader.RegisterKeybind(this, "ChainBack", "Mouse4");
			ChainForward = KeybindLoader.RegisterKeybind(this, "ChainForward", "Mouse5");
			MapTeleport = KeybindLoader.RegisterKeybind(this, "MapTeleport", MapTeleportKey);
		}
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
		ToggleBook = (ChainBack = (ChainForward = (MapTeleport = null)));
	}
}
