using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// Packets between a client and the server; the first byte says which.
internal enum BookMessage : byte
{
	MapSections
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
		switch ((BookMessage)reader.ReadByte())
		{
		case BookMessage.MapSections:
			BookMapTeleport.SendSections(reader, whoAmI);
			break;
		}
	}

	public override void Unload()
	{
		ToggleBook = (ChainBack = (ChainForward = (MapTeleport = null)));
	}
}
