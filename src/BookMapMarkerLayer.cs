using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace ModernRecipeBook;

// Markers on the fullscreen map, the minimap and the overlay map, sized by the map icon scale like vanilla's icons: the
// badge's item sprite on a dark rounded plate with the book's accent rim, so it stays readable over any terrain.
// Hovering grows it and shows the vanilla map text: badge and name, then who placed it and how long ago.
// Nothing here allocates per frame: the hover text is rebuilt only when it would read differently.
internal sealed class BookMapMarkerLayer : ModMapLayer
{
	// Plate and sprite box in pixels at map icon scale 1.
	private const float Plate = 26f;

	private const float IconBox = 18f;

	private static readonly Color Rim = new Color(178, 218, 191);

	private static readonly Color Fill = new Color(25, 32, 28);

	private static Asset<Texture2D> Mask;

	// The marker under the cursor in the frame just drawn (on the fullscreen map the panel opens it).
	internal static BookMapMarker Hovered;

	private static string Tip = "";

	private static (int Id, long Edited, long Minute, int Culture) TipKey = (-1, 0L, 0L, -1);

	public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

	public override void Draw(ref MapOverlayDrawContext context, ref string text)
	{
		Hovered = null;
		if (!BookMapMarkers.Enabled || BookMapMarkers.Markers.Count == 0)
		{
			return;
		}
		Mask ??= ModContent.Request<Texture2D>("ModernRecipeBook/Assets/RoundedPanel", AssetRequestMode.ImmediateLoad);
		Texture2D mask = Mask.Value;
		float rim = Plate / mask.Width;
		foreach (BookMapMarker marker in BookMapMarkers.Markers)
		{
			Vector2 tile = new Vector2(marker.X + 0.5f, marker.Y + 0.5f);
			Vector2 screen = (tile - context.MapPosition) * context.MapScale + context.MapOffset;
			if (context.ClippingRectangle is Rectangle clip && !clip.Contains(screen.ToPoint()))
			{
				continue;
			}
			bool over = context.Draw(mask, tile, Rim, new SpriteFrame(1, 1), rim, rim * 1.25f, Alignment.Center).IsMouseOver;
			float scale = context.DrawScale * (over ? 1.25f : 1f);
			Main.spriteBatch.Draw(mask, screen, null, Fill, 0f, mask.Size() / 2f, scale * (Plate - 4f) / mask.Width, SpriteEffects.None, 0f);
			int item = BookMarkerBadges.Get(marker.Badge).Item;
			Main.instance.LoadItem(item);
			Texture2D texture = TextureAssets.Item[item].Value;
			Rectangle frame = Main.itemAnimations[item]?.GetFrame(texture) ?? texture.Bounds;
			Main.spriteBatch.Draw(texture, screen, frame, Color.White, 0f, frame.Size() / 2f, scale * IconBox / Math.Max(frame.Width, frame.Height), SpriteEffects.None, 0f);
			if (over)
			{
				Hovered = marker;
			}
		}
		if (Hovered != null)
		{
			text = TipFor(Hovered);
		}
	}

	private static string TipFor(BookMapMarker marker)
	{
		(int, long, long, int) key = (marker.Id, marker.Edited, BookMapMarkers.Now / TimeSpan.TicksPerMinute, BookText.Culture);
		if (key != TipKey)
		{
			TipKey = key;
			Tip = $"[i:{BookMarkerBadges.Get(marker.Badge).Item}] {BookMapMarkers.Title(marker)}\n{BookMapMarkers.Placed(marker)}";
		}
		return Tip;
	}
}
