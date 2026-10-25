using System.ComponentModel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace ModernRecipeBook.Common.Configs;

// World rules the server owns: in multiplayer every client gets the host's values on joining.
public sealed class BookServerConfig : ModConfig
{
	[Header("QualityOfLife")]
	[DefaultValue(true)]
	public bool NearbyStorage = true;

	[DefaultValue(30)]
	[Range(10, 50)]
	[Slider]
	public int NearbyRadius = 30;

	[DefaultValue(true)]
	public bool MapTeleport = true;

	[DefaultValue(true)]
	public bool MapMarkers = true;

	public override ConfigScope Mode => ConfigScope.ServerSide;

	// tModLoader lets any client change a server config by default; only the host may (single player is always the host).
	public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
	{
		if (NetMessage.DoesPlayerSlotCountAsAHost(whoAmI))
		{
			return true;
		}
		message = NetworkText.FromKey("tModLoader.ModConfigRejectChangesNotHost");
		return false;
	}
}
