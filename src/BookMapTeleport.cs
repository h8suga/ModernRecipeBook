using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// The Map Teleport key (Mouse3, the wheel, by default; rebindable in Controls) on an explored spot of the fullscreen map
// moves the player there. Vanilla's fullscreen map has no use for it: it drags and pings with the left button, clicks
// pylons, players and the close button with it, and clears every player control while it is open. The book's Open key
// shares Mouse3 by default; on the map the teleport wins (BookPlayer.ProcessTriggers), with the map closed nothing changes.
// The player lands with the feet at the cursor when the hitbox fits there (mid-air too: it falls, and the fall counts from
// the new height as after any Player.Teleport); otherwise on the nearest floor within SearchRadius tiles. Every tile the
// hitbox covers must be explored, open and free of lava. The teleport is the Rod of Discord's: Player.Teleport on the
// client plus MessageID.TeleportEntity, which the server applies and relays. Player position is the client's in Terraria,
// so the host's BookServerConfig.MapTeleport, synced to every client, is the gate.
internal sealed class BookMapTeleport : ModSystem
{
	private const int SearchRadius = 10;

	private const int MessageTicks = 60;

	// A refusal shown at the cursor for a second, like vanilla's map hover text.
	private static string Message = "";

	private static uint MessageTick;

	// The key is read where vanilla reads its map clicks, while the map draws; a press lasts one update, which may draw twice.
	private static uint PressTick = uint.MaxValue;

	// Multiplayer: the map point (in tiles) waiting for the server to send that part of the world. A client only has the
	// tiles it has been near this session, and must not land in unknown ground.
	private static Vector2? Pending;

	private static bool DefaultKeyChecked;

	internal static bool Enabled => ModContent.GetInstance<BookServerConfig>().MapTeleport;

	// This update's press of the key on the open map belongs to the teleport, not to a key it shares.
	internal static bool TakesPress => Main.mapFullscreen && Enabled && (ModernRecipeBook.MapTeleport?.JustPressed ?? false);

	// The gesture and the landing are the local player's; the server only answers SendSections.
	public override bool IsLoadingEnabled(Mod mod) => !Main.dedServ;

	// tModLoader gives a new keybind its default only when the player resets the controls; for everyone who already has
	// an input profile it would stay unassigned. An empty binding gets the default once per start, in the first world update
	// (the profiles have loaded by then), so unassigning it lasts until the game restarts; binding another key sticks.
	public override void PostUpdateInput()
	{
		if (DefaultKeyChecked || Main.gameMenu)
		{
			return;
		}
		DefaultKeyChecked = true;
		List<string> keys = ModernRecipeBook.MapTeleport.GetAssignedKeys();
		if (keys.Count == 0)
		{
			keys.Add(ModernRecipeBook.MapTeleportKey);
		}
	}

	public override void PostDrawFullscreenMap(ref string mouseText)
	{
		if (ModernRecipeBook.MapTeleport.JustPressed && PressTick != Main.GameUpdateCount && Enabled && !Main.LocalPlayer.dead && !RecipeBookSystem.MarkerPanelOpen)
		{
			PressTick = Main.GameUpdateCount;
			// The book's Mouse3 item search watches the raw button: this press is taken.
			BookInventoryShortcut.TakeMousePress();
			Request(CursorTile());
		}
		if (Pending is Vector2 pending && AreaLoaded(pending))
		{
			Pending = null;
			Land(pending);
		}
		if (mouseText.Length == 0 && Main.GameUpdateCount - MessageTick < MessageTicks)
		{
			mouseText = Message;
		}
	}

	public override void PostUpdateEverything()
	{
		if (!Main.mapFullscreen)
		{
			Pending = null;
		}
	}

	public override void OnWorldUnload()
	{
		Pending = null;
		MessageTick = 0;
	}

	public override void Unload()
	{
		DefaultKeyChecked = false;
	}

	// Server: the client is about to teleport there, so it gets that part of the world first (as MessageID.TeleportEntity's
	// documentation asks for long teleports). Sections already sent are skipped.
	internal static void SendSections(BinaryReader reader, int whoAmI)
	{
		int x = reader.ReadInt16();
		int y = reader.ReadInt16();
		if (Main.netMode == NetmodeID.Server)
		{
			RemoteClient.CheckSection(whoAmI, new Vector2(x, y) * 16f);
		}
	}

	// The map point under the cursor in tiles, as vanilla computes a ping: the map is drawn centred on mapFullscreenPos in
	// unscaled screen pixels, and this hook runs with the UI zoom applied, so the unscaled mouse is read.
	internal static Vector2 CursorTile()
	{
		Vector2 half = new Vector2((int)PlayerInput.OriginalScreenSize.X / 2, (int)PlayerInput.OriginalScreenSize.Y / 2);
		return (new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) - half) / Main.mapFullscreenScale + Main.mapFullscreenPos;
	}

	private void Request(Vector2 tile)
	{
		Pending = null;
		int x = (int)Math.Floor(tile.X);
		int y = (int)Math.Floor(tile.Y);
		if (!WorldGen.InWorld(x, y) || !Main.Map.IsRevealed(x, y))
		{
			Say(RecipeBookSystem.T("Не исследовано", "Unexplored"));
			return;
		}
		if (!AreaLoaded(tile))
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)BookMessage.MapSections);
			packet.Write((short)x);
			packet.Write((short)y);
			packet.Send();
			Pending = tile;
			return;
		}
		Land(tile);
	}

	// Single player has the whole world; a multiplayer client needs the search area's sections.
	private static bool AreaLoaded(Vector2 tile)
	{
		int x = (int)tile.X;
		int y = (int)tile.Y;
		int reach = SearchRadius + 4;
		return Main.netMode != NetmodeID.MultiplayerClient || Main.sectionManager.TilesLoaded(Math.Max(0, x - reach), Math.Max(0, y - reach), Math.Min(Main.maxTilesX - 1, x + reach), Math.Min(Main.maxTilesY - 1, y + reach));
	}

	private static void Land(Vector2 tile)
	{
		Player player = Main.LocalPlayer;
		if (!FindLanding(player, tile * 16f, out Vector2 position))
		{
			Say(RecipeBookSystem.T("Некуда приземлиться", "No room to land"));
			return;
		}
		player.Teleport(position, TeleportationStyleID.TeleportationPotion);
		player.velocity = Vector2.Zero;
		if (Main.netMode == NetmodeID.MultiplayerClient)
		{
			NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI, position.X, position.Y, TeleportationStyleID.TeleportationPotion);
		}
		// The map closes like after a pylon click. If the key is the right button, holding it must not also open a chest, talk
		// or use the held item's right-click at the landing spot: tile use and its item fallback both wait for its release.
		Main.mapFullscreen = false;
		player.releaseUseTile = false;
		player.tileInteractionHappened = true;
		SoundEngine.PlaySound(SoundID.MenuClose);
	}

	// point: world pixels under the cursor, the feet (the head with reversed gravity). The exact spot first; else the
	// nearest one within SearchRadius that has floor under the feet, so a click into rock or lava never ends in a fall.
	internal static bool FindLanding(Player player, Vector2 point, out Vector2 position)
	{
		bool flipped = player.gravDir == -1f;
		int width = player.width;
		int height = player.height;
		position = new Vector2(point.X - width / 2f, flipped ? point.Y : point.Y - height);
		if (Fits(player, position))
		{
			return true;
		}
		int column = (int)(point.X / 16f);
		int row = (int)(point.Y / 16f);
		float best = SearchRadius * SearchRadius * 256f;
		bool found = false;
		for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
		{
			// Standing on tile row floor: the feet on its top edge (its bottom edge with reversed gravity).
			int floor = row + dy;
			float feetY = flipped ? (floor + 1) * 16f : floor * 16f;
			// Centred on tile centres and tile edges: a 20 px player fits a 2-tile gap only centred on its middle edge.
			for (int halves = -2 * SearchRadius; halves <= 2 * SearchRadius; halves++)
			{
				Vector2 feet = new Vector2(column * 16f + 8f + halves * 8f, feetY);
				float distance = Vector2.DistanceSquared(feet, point);
				if (distance > best)
				{
					continue;
				}
				Vector2 candidate = new Vector2(feet.X - width / 2f, flipped ? feetY : feetY - height);
				if (Collision.SolidTiles((int)(candidate.X / 16f), (int)((candidate.X + width - 1f) / 16f), floor, floor, allowTopSurfaces: !flipped) && Fits(player, candidate))
				{
					best = distance;
					position = candidate;
					found = true;
				}
			}
		}
		return found;
	}

	private static bool Fits(Player player, Vector2 position)
	{
		int width = player.width;
		int height = player.height;
		// Player.BordersMovement's limits: inside them nothing pushes the player back.
		if (position.X < Main.leftWorld + 656f || position.X + width > Main.rightWorld - 672f || position.Y < Main.topWorld + 656f || position.Y + height > Main.bottomWorld - 672f)
		{
			return false;
		}
		int left = (int)(position.X / 16f);
		int top = (int)(position.Y / 16f);
		for (int x = left; x <= (int)((position.X + width - 1f) / 16f); x++)
		{
			for (int y = top; y <= (int)((position.Y + height - 1f) / 16f); y++)
			{
				Tile tile = Main.tile[x, y];
				if (!Main.Map.IsRevealed(x, y) || (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava))
				{
					return false;
				}
				// Like every vanilla teleport: not into the Lihzahrd Temple before Plantera.
				if (tile.WallType == WallID.LihzahrdBrickUnsafe && !NPC.downedPlantBoss && (Main.remixWorld || y > Main.worldSurface))
				{
					return false;
				}
			}
		}
		return !Collision.SolidCollision(position, width, height) && CombinedHooks.CanBeTeleportedTo(player, position, left, top, "MapTeleport");
	}

	internal static void Say(string message)
	{
		Message = message;
		MessageTick = Main.GameUpdateCount;
	}
}
