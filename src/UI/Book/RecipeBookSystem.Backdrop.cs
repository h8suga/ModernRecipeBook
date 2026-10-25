using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ModernRecipeBook.UI.Book;

// The book under a modal panel: drawn as it was, with no hover, then blurred and dimmed by BookBackdrop.
public sealed partial class RecipeBookSystem
{
	// Veil over the blurred book, in the book's darkest tone.
	private static readonly Color BackdropVeil = new Color(10, 16, 12);

	private const float BackdropVeilOpacity = 0.4f;

	// The scrim modals had before the blur; still used whenever the blurred copy is not available.
	private static readonly Color BackdropScrim = new Color(10, 16, 12, 235);

	// Text and item icons fade with this, boxes with GraphicsOpacity; only a modal's appearance lowers it.
	private static float ModalOpacity = 1f;

	// Panels that sit over the book (the enemy card lives inside How to Get). A new modal only joins here.
	private static bool ModalOpen => AcquisitionOpen || HelpOpen;

	internal static bool BackdropWanted => Open && !Main.gameMenu && (ModalOpen || BookBackdrop.Fade > 0f);

	internal static Color BackdropBase => Panel;

	// Under a modal the mouse is parked off the book, so nothing under the modal hovers, hints or shows an
	// item tooltip.
	private static void DrawBookContent(SpriteBatch sb, bool inert)
	{
		if (!inert)
		{
			DrawBookContent(sb);
			return;
		}
		Vector2 mouse = Mouse;
		Item hover = Main.HoverItem;
		string hoverName = Main.hoverItemName;
		string tip = PendingTip;
		Mouse = new Vector2(-100000f);
		try
		{
			DrawBookContent(sb);
		}
		finally
		{
			Mouse = mouse;
			Main.HoverItem = hover;
			Main.hoverItemName = hoverName;
			PendingTip = tip;
		}
	}

	// BookBackdrop's render pass before the frame: the layout the interface will use later in this frame.
	internal static void DrawBackdropContent(SpriteBatch sb)
	{
		Vector2 mouse = Mouse;
		bool over = MouseOver;
		Layout();
		DrawBookContent(sb, inert: true);
		Mouse = mouse;
		MouseOver = over;
	}

	// Where the blurred copy shows: the frame's fill inside its border, so the border stays sharp.
	internal static void DrawBackdropMask(SpriteBatch sb)
	{
		Rectangle frame = Rect(0f, 0f, 1120f, 696f);
		int stroke = Math.Max(1, (int)Math.Round(Scale));
		BookSkin.Panel(sb, new Rectangle(frame.X + stroke, frame.Y + stroke, frame.Width - stroke * 2, frame.Height - stroke * 2), Color.White, (float)(Math.Round(18f * Scale) - stroke));
	}

	private static void DrawBackdrop(SpriteBatch sb, float fade)
	{
		if (fade <= 0f)
		{
			return;
		}
		if (BookBackdrop.DrawBlurred(sb, fade))
		{
			Box(sb, 0f, 0f, 1120f, 696f, BackdropVeil * (BackdropVeilOpacity * fade), border: false, 18f);
		}
		else
		{
			Box(sb, 0f, 0f, 1120f, 696f, BackdropScrim * fade, border: false, 18f);
		}
	}

	// The modal fades in and grows from 98 % around the book's center while the backdrop comes in.
	private static void DrawModal(SpriteBatch sb, float appear, Action<SpriteBatch> draw)
	{
		Vector2 origin = Origin;
		float scale = Scale;
		float opacity = GraphicsOpacity;
		float size = 0.98f + 0.02f * appear;
		Origin += new Vector2(560f, 348f) * Scale * (1f - size);
		Scale *= size;
		GraphicsOpacity = ModalOpacity = appear;
		try
		{
			draw(sb);
		}
		finally
		{
			Origin = origin;
			Scale = scale;
			GraphicsOpacity = opacity;
			ModalOpacity = 1f;
		}
	}
}
