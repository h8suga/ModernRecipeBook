using Terraria;
using Terraria.ModLoader;

namespace ModernRecipeBook;

public sealed class ModernRecipeBook : Mod
{
	internal static ModKeybind ToggleBook;

	internal static ModKeybind ChainBack;

	internal static ModKeybind ChainForward;

	public override void Load()
	{
		if (!Main.dedServ)
		{
			ToggleBook = KeybindLoader.RegisterKeybind(this, "ToggleBook", "Mouse3");
			ChainBack = KeybindLoader.RegisterKeybind(this, "ChainBack", "Mouse4");
			ChainForward = KeybindLoader.RegisterKeybind(this, "ChainForward", "Mouse5");
		}
	}

	public override void Unload()
	{
		ToggleBook = (ChainBack = (ChainForward = null));
	}

	public override void HandlePacket(System.IO.BinaryReader reader, int whoAmI)
	{
		BookNearbyServer.HandlePacket(reader, whoAmI);
	}
}
