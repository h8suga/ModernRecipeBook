using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace ModernRecipeBook;

// Magic Storage options. The page exists only when Magic Storage is installed, so nothing mentions it otherwise.
public sealed class BookStorageConfig : ModConfig
{
	[DefaultValue(true)]
	public bool AutoConnect = true;

	public override ConfigScope Mode => ConfigScope.ClientSide;

	public override bool Autoload(ref string name)
	{
		return ModLoader.HasMod("MagicStorage");
	}
}
