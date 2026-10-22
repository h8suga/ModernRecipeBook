using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// Blurred copy of the book behind modal panels, and the clock of their fade.
// The copy is rendered in Main.OnPreDraw, before the frame draws anything to the backbuffer (vanilla renders
// its own tile and background targets right after it), so the interface pass never switches render targets:
// it only draws the finished texture through the batch it is already in.
// Blur without shaders: the book is halved twice with linear filtering (each step averages 2x2 texels),
// blurred at quarter size by a separable 9-tap Gaussian (sigma 2 texels, 5 bilinear taps per direction) and
// scaled back to full size through the book's frame as a mask: about an 8 px Gaussian on screen.
internal sealed class BookBackdrop : ModSystem
{
	private const float OpenSeconds = 0.15f;

	private const float CloseSeconds = 0.1f;

	// Offsets in texels and weights out of 255; the weights sum to 255, so alpha stays exactly opaque.
	private static readonly float[] TapOffsets = { 0f, 1.3846154f, -1.3846154f, 3.2307692f, -3.2307692f };

	private static readonly int[] TapWeights = { 57, 81, 81, 18, 18 };

	private static readonly BlendState Sum = new BlendState
	{
		ColorSourceBlend = Blend.One,
		ColorDestinationBlend = Blend.One,
		AlphaSourceBlend = Blend.One,
		AlphaDestinationBlend = Blend.One
	};

	// Multiplies what is drawn by the mask already in the target's alpha (premultiplied result).
	private static readonly BlendState Masked = new BlendState
	{
		ColorSourceBlend = Blend.DestinationAlpha,
		ColorDestinationBlend = Blend.Zero,
		AlphaSourceBlend = Blend.DestinationAlpha,
		AlphaDestinationBlend = Blend.Zero
	};

	private static readonly Stopwatch Clock = Stopwatch.StartNew();

	private static RenderTarget2D Full;

	private static RenderTarget2D Half;

	private static RenderTarget2D Quarter;

	private static RenderTarget2D Swap;

	private static bool Broken;

	private static bool Ready;

	private static float ReadyScale;

	private static double LastAdvance;

	// 0 without a modal, 1 with one fully shown; moves linearly, Advance returns it eased.
	internal static float Fade { get; private set; }

	public override void Load()
	{
		if (Main.dedServ)
		{
			return;
		}
		Main.OnPreDraw += PreDraw;
		Main.instance.GraphicsDevice.DeviceReset += DeviceReset;
	}

	public override void Unload()
	{
		if (Main.dedServ)
		{
			return;
		}
		Main.OnPreDraw -= PreDraw;
		Main.instance.GraphicsDevice.DeviceReset -= DeviceReset;
		Main.QueueMainThreadAction(Release);
	}

	private static void DeviceReset(object sender, EventArgs e)
	{
		Release();
	}

	// Called once per drawn book frame. A long gap (the book was closed) jumps straight to the end state;
	// a slow frame moves at most 50 ms, so a hitch on opening does not swallow the animation.
	internal static float Advance(bool modal)
	{
		double now = Clock.Elapsed.TotalSeconds;
		float step = (float)Math.Min(now - LastAdvance, 0.05);
		bool resumed = now - LastAdvance > 0.5;
		LastAdvance = now;
		if (resumed)
		{
			Fade = modal ? 1f : 0f;
		}
		else
		{
			Fade = Math.Clamp(Fade + (modal ? (step / OpenSeconds) : (0f - step / CloseSeconds)), 0f, 1f);
		}
		float rest = 1f - Fade;
		return 1f - rest * rest * rest;
	}

	// The blurred copy hides the whole book, so the interface can skip drawing it.
	internal static bool Covers(float fade)
	{
		return fade >= 1f && Usable;
	}

	// Through the interface batch (UI scale matrix, linear sampling): the copy lands 1:1 on screen pixels.
	internal static bool DrawBlurred(SpriteBatch sb, float opacity)
	{
		if (!Usable)
		{
			return false;
		}
		sb.Draw(Full, Vector2.Zero, null, Color.White * opacity, 0f, Vector2.Zero, 1f / Main.UIScale, SpriteEffects.None, 0f);
		return true;
	}

	private static bool Usable => Ready && Full != null && !Full.IsDisposed && ReadyScale == Main.UIScale;

	private static void PreDraw(GameTime gameTime)
	{
		Ready = false;
		if (Broken || !RecipeBookSystem.BackdropWanted)
		{
			return;
		}
		try
		{
			Render();
			Ready = true;
		}
		catch (Exception error)
		{
			Broken = true;
			Release();
			ModContent.GetInstance<ModernRecipeBook>().Logger.Warn("Modal backdrop blur is off until the mod reloads; modals use the plain scrim.", error);
		}
	}

	private static void Render()
	{
		GraphicsDevice device = Main.instance.GraphicsDevice;
		SpriteBatch sb = Main.spriteBatch;
		int width = device.PresentationParameters.BackBufferWidth;
		int height = device.PresentationParameters.BackBufferHeight;
		Prepare(ref Full, device, width, height);
		Prepare(ref Half, device, (width + 1) / 2, (height + 1) / 2);
		Prepare(ref Quarter, device, (width + 3) / 4, (height + 3) / 4);
		Prepare(ref Swap, device, Quarter.Width, Quarter.Height);
		RenderTargetBinding[] targets = device.GetRenderTargets();
		Viewport viewport = device.Viewport;
		Rectangle scissor = device.ScissorRectangle;
		BlendState blend = device.BlendState;
		DepthStencilState depth = device.DepthStencilState;
		RasterizerState rasterizer = device.RasterizerState;
		SamplerState sampler = device.SamplerStates[0];
		// The book lays itself out in UI units (Main.screenWidth and the mouse divided by the UI scale).
		int screenWidth = Main.screenWidth;
		int screenHeight = Main.screenHeight;
		int mouseX = Main.mouseX;
		int mouseY = Main.mouseY;
		int lastMouseX = Main.lastMouseX;
		int lastMouseY = Main.lastMouseY;
		bool drawing = false;
		try
		{
			PlayerInput.SetZoom_UI();
			device.SetRenderTarget(Full);
			device.Clear(RecipeBookSystem.BackdropBase);
			BeginUi(sb);
			drawing = true;
			RecipeBookSystem.DrawBackdropContent(sb);
			drawing = false;
			sb.End();
			Downscale(device, sb, Full, Half);
			Downscale(device, sb, Half, Quarter);
			Blur(device, sb, Quarter, Swap, Vector2.UnitX);
			Blur(device, sb, Swap, Quarter, Vector2.UnitY);
			device.SetRenderTarget(Full);
			device.Clear(Color.Transparent);
			BeginUi(sb);
			RecipeBookSystem.DrawBackdropMask(sb);
			sb.End();
			sb.Begin(SpriteSortMode.Deferred, Masked, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
			sb.Draw(Quarter, new Rectangle(0, 0, width, height), Color.White);
			sb.End();
			ReadyScale = Main.UIScale;
		}
		finally
		{
			if (drawing)
			{
				try
				{
					sb.End();
				}
				catch (InvalidOperationException)
				{
				}
			}
			device.SetRenderTargets(targets);
			device.Viewport = viewport;
			device.ScissorRectangle = scissor;
			device.BlendState = blend;
			device.DepthStencilState = depth;
			device.RasterizerState = rasterizer;
			device.SamplerStates[0] = sampler;
			Main.screenWidth = screenWidth;
			Main.screenHeight = screenHeight;
			Main.mouseX = mouseX;
			Main.mouseY = mouseY;
			Main.lastMouseX = lastMouseX;
			Main.lastMouseY = lastMouseY;
		}
	}

	// The state every UI-scaled interface layer begins with.
	private static void BeginUi(SpriteBatch sb)
	{
		sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
	}

	// Linear sampling at half size averages each 2x2 block.
	private static void Downscale(GraphicsDevice device, SpriteBatch sb, Texture2D source, RenderTarget2D target)
	{
		device.SetRenderTarget(target);
		sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
		sb.Draw(source, new Rectangle(0, 0, target.Width, target.Height), Color.White);
		sb.End();
	}

	// One direction of the Gaussian: shifted copies summed with their weights. Each copy overhangs the target
	// by 4 texels, where the clamp sampler repeats the edge, so the edges keep their full weight.
	private static void Blur(GraphicsDevice device, SpriteBatch sb, Texture2D source, RenderTarget2D target, Vector2 axis)
	{
		device.SetRenderTarget(target);
		device.Clear(Color.Transparent);
		sb.Begin(SpriteSortMode.Deferred, Sum, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
		Rectangle area = new Rectangle(-4, -4, source.Width + 8, source.Height + 8);
		for (int i = 0; i < TapOffsets.Length; i++)
		{
			int weight = TapWeights[i];
			sb.Draw(source, axis * TapOffsets[i] - new Vector2(4f), area, new Color(weight, weight, weight, weight));
		}
		sb.End();
	}

	private static void Prepare(ref RenderTarget2D target, GraphicsDevice device, int width, int height)
	{
		if (target == null || target.IsDisposed || target.Width != width || target.Height != height)
		{
			target?.Dispose();
			target = new RenderTarget2D(device, width, height, mipMap: false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
		}
	}

	private static void Release()
	{
		Ready = false;
		Full?.Dispose();
		Half?.Dispose();
		Quarter?.Dispose();
		Swap?.Dispose();
		Full = (Half = (Quarter = (Swap = null)));
	}
}
