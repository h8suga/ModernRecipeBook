using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// The enemy card's portrait is the Bestiary's own: the entry's icon (the mob playing its walk or idle animation)
// over the entry's spawn background and overlays, built and drawn like NPCPortraitInfoElement and
// UIBestiaryNPCEntryPortrait do it, in the Bestiary's 189×108 box scaled to the card.
public sealed partial class RecipeBookSystem
{
	private static readonly Rectangle PortraitBox = new Rectangle(0, 0, 189, 108);

	private static readonly RasterizerState PortraitClip = new RasterizerState
	{
		CullMode = CullMode.None,
		ScissorTestEnable = true
	};

	private static readonly List<(Asset<Texture2D> Image, Color Color)> EnemyOverlays = new List<(Asset<Texture2D>, Color)>();

	private static IEntryIcon EnemyIcon;

	private static BestiaryEntry EnemyEntry;

	private static Asset<Texture2D> EnemyBackground;

	private static Color EnemyBackgroundColor;

	private static Asset<Texture2D> LockedIcon;

	// Once per card (open, variant, Back): a copy of the entry's icon, as the Bestiary page makes one, and the
	// background picked the way NPCPortraitInfoElement picks it.
	private static void ReadEnemyPortrait(int netId)
	{
		BestiaryEntry own = Main.BestiaryDB.FindEntryByNPCID(netId);
		EnemyEntry = MobEntry(netId);
		// A variant without an entry of its own still shows itself, not the mob it is credited to.
		EnemyIcon = own.Icon?.CreateClone() ?? new UnlockableNPCEntryIcon(netId);
		LockedIcon ??= Main.Assets.Request<Texture2D>("Images/UI/Bestiary/Icon_Locked");
		EnemyBackground = null;
		EnemyBackgroundColor = Color.White;
		EnemyOverlays.Clear();
		try
		{
			List<IBestiaryInfoElement> info = EnemyEntry.Info;
			List<IPreferenceProviderElement> preferences = info.OfType<IPreferenceProviderElement>().ToList();
			bool preferred = false;
			foreach (IBestiaryBackgroundImagePathAndColorProvider provider in info.OfType<IBestiaryBackgroundImagePathAndColorProvider>().Where((IBestiaryBackgroundImagePathAndColorProvider p) => preferences.Any((IPreferenceProviderElement x) => x.Matches(p))))
			{
				Asset<Texture2D> image = provider.GetBackgroundImage();
				if (image != null)
				{
					EnemyBackground = image;
					EnemyBackgroundColor = provider.GetBackgroundColor() ?? EnemyBackgroundColor;
					preferred = true;
					break;
				}
			}
			List<IBestiaryBackgroundOverlayAndColorProvider> overlays = new List<IBestiaryBackgroundOverlayAndColorProvider>();
			foreach (IBestiaryInfoElement element in info)
			{
				if (element is IBestiaryBackgroundImagePathAndColorProvider background)
				{
					Asset<Texture2D> image = background.GetBackgroundImage();
					if (image == null)
					{
						continue;
					}
					if (!preferred)
					{
						EnemyBackground = image;
					}
					EnemyBackgroundColor = background.GetBackgroundColor() ?? EnemyBackgroundColor;
				}
				if (!preferred && element is IBestiaryBackgroundOverlayAndColorProvider overlay && overlay.GetBackgroundOverlayImage() != null)
				{
					overlays.Add(overlay);
				}
			}
			foreach (IBestiaryBackgroundOverlayAndColorProvider overlay in overlays.OrderBy((IBestiaryBackgroundOverlayAndColorProvider x) => x.DisplayPriority))
			{
				EnemyOverlays.Add((overlay.GetBackgroundOverlayImage(), overlay.GetBackgroundOverlayColor() ?? Color.Lerp(Color.White, EnemyBackgroundColor, 0.5f)));
			}
		}
		catch (Exception)
		{
			EnemyReadErrors++;
		}
		UpdateEnemyPortrait();
	}

	// Looking at a mob in the world shows it whatever the Bestiary knows; a card from How to Get keeps the
	// Bestiary's own "not met yet" look for a mob the player has never met.
	private static BestiaryUICollectionInfo EnemyPortraitInfo(int netId)
	{
		BestiaryEntryUnlockState state = MobUnlockState(netId);
		if (EnemyStandalone && state == BestiaryEntryUnlockState.NotKnownAtAll_0)
		{
			state = BestiaryEntryUnlockState.CanShowPortraitOnly_1;
		}
		return new BestiaryUICollectionInfo
		{
			OwnerEntry = EnemyEntry,
			UnlockState = state
		};
	}

	private static EntryIconDrawSettings PortraitSettings => new EntryIconDrawSettings
	{
		iconbox = PortraitBox,
		IsPortrait = true,
		IsHovered = false
	};

	// Once per UI update, like UIBestiaryEntryIcon.Update: the animation runs at the game's pace, not the frame rate.
	private static void UpdateEnemyPortrait()
	{
		if (EnemyIcon == null || !EnemyOpen)
		{
			return;
		}
		try
		{
			EnemyIcon.Update(EnemyPortraitInfo(EnemyVariants[EnemyVariant]), PortraitBox, PortraitSettings);
		}
		catch (Exception error)
		{
			EnemyIcon = null;
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Enemy portrait could not be updated.", error);
		}
	}

	// Drawn inside the box in the Bestiary's own coordinates (189×108), scaled to the card and clipped to it like
	// the Bestiary's OverflowHidden portrait; the mob is drawn in immediate mode with the game's sampler, as there.
	private static void DrawEnemyPortrait(SpriteBatch sb, BookRect rect, int netId)
	{
		// The frame first; the picture is clipped one pixel inside it. FieldFill shows when the entry has no background.
		Box(sb, rect, FieldFill, border: true, 4f);
		rect = new BookRect(rect.X + 1f, rect.Y + 1f, rect.Width - 2f, rect.Height - 2f);
		BestiaryUICollectionInfo info = EnemyPortraitInfo(netId);
		bool known = EnemyIcon != null && EnemyIcon.GetUnlockState(info);
		Matrix matrix = Matrix.CreateScale(rect.Width / PortraitBox.Width * Scale) * Matrix.CreateTranslation(Origin.X + rect.X * Scale, Origin.Y + rect.Y * Scale, 0f) * Main.UIScaleMatrix;
		Vector2 topLeft = Vector2.Transform(Vector2.Zero, matrix);
		Vector2 bottomRight = Vector2.Transform(new Vector2(PortraitBox.Width, PortraitBox.Height), matrix);
		GraphicsDevice device = sb.GraphicsDevice;
		Rectangle scissor = device.ScissorRectangle;
		Rectangle clip = Rectangle.Intersect(new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)(bottomRight.X - topLeft.X), (int)(bottomRight.Y - topLeft.Y)), device.Viewport.Bounds);
		sb.End();
		try
		{
			device.ScissorRectangle = clip;
			if (known)
			{
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, PortraitClip, null, matrix);
				if (EnemyBackground != null)
				{
					sb.Draw(EnemyBackground.Value, PortraitBox, EnemyBackgroundColor);
				}
				foreach ((Asset<Texture2D> image, Color color) in EnemyOverlays)
				{
					sb.Draw(image.Value, PortraitBox, color);
				}
				sb.End();
			}
			sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, PortraitClip, null, matrix);
			try
			{
				if (known)
				{
					EnemyIcon.Draw(info, sb, PortraitSettings);
				}
				else if (LockedIcon != null)
				{
					Texture2D locked = LockedIcon.Value;
					sb.Draw(locked, PortraitBox.Center.ToVector2(), null, Color.White * 0.15f, 0f, locked.Size() / 2f, 1f, SpriteEffects.None, 0f);
				}
			}
			catch (Exception error)
			{
				EnemyIcon = null;
				ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Enemy portrait could not be drawn.", error);
			}
			sb.End();
		}
		finally
		{
			device.ScissorRectangle = scissor;
			sb.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
		}
		if (rect.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(known ? T("Как в бестиарии", "As in the Bestiary") : T("Ещё не встречен", "Not met yet"));
		}
	}
}
