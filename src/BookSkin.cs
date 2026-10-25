using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace ModernRecipeBook;

internal static class BookSkin
{
	private static Asset<Texture2D> Mask;

	internal static void Unload()
	{
		Mask = null;
	}

	internal static void Panel(SpriteBatch sb, Rectangle bounds, Color fill, float radius, Color? border = null, float thickness = 1f)
	{
		if (bounds.Width <= 0 || bounds.Height <= 0)
		{
			return;
		}
		int r = BookRoundedGeometry.ClampRadius(bounds.Width, bounds.Height, Math.Max(0, (int)Math.Round(radius)));
		if (!border.HasValue)
		{
			Fill(sb, bounds, fill, r);
			return;
		}
		int stroke = Math.Clamp((int)Math.Round(thickness), 1, Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 2));
		Fill(sb, bounds, border.Value, r);
		Rectangle inside = new Rectangle(bounds.X + stroke, bounds.Y + stroke, bounds.Width - stroke * 2, bounds.Height - stroke * 2);
		if (inside.Width > 0 && inside.Height > 0)
		{
			Fill(sb, inside, fill, Math.Max(0, r - stroke));
		}
	}

	private static void Fill(SpriteBatch sb, Rectangle bounds, Color color, int radius)
	{
		if (radius == 0)
		{
			sb.Draw(TextureAssets.MagicPixel.Value, bounds, color);
			return;
		}
		if (Mask == null)
		{
			Mask = ModContent.Request<Texture2D>("ModernRecipeBook/Assets/RoundedPanel", AssetRequestMode.ImmediateLoad);
		}
		Texture2D texture = Mask.Value;
		for (int i = 0; i < 9; i++)
		{
			BookRoundedGeometry.Patch patch = BookRoundedGeometry.Slice(bounds.Width, bounds.Height, radius, i);
			BookRoundedGeometry.Area src = patch.Source;
			BookRoundedGeometry.Area dst = patch.Destination;
			if (dst.Width > 0 && dst.Height > 0)
			{
				sb.Draw(texture, new Rectangle(bounds.X + dst.X, bounds.Y + dst.Y, dst.Width, dst.Height), new Rectangle(src.X, src.Y, src.Width, src.Height), color);
			}
		}
	}
}
