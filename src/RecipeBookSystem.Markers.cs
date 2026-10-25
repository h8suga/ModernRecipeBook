using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;
using static ModernRecipeBook.BookText;

namespace ModernRecipeBook;

// The marker panel on the fullscreen map, in the book's look: right-click an empty spot to name a new marker and pick its
// badge, right-click a marker to see it and (author or host) edit or delete it. Left button stays the map's (drag, ping,
// pylons), the Map Teleport key stays the teleport's. The panel is modal while open: the game takes no controls
// (Main.blockInput) and the name field owns the keyboard like the book's search, so typing never reaches a hotkey.
// Clicks are read while the map draws, where vanilla reads its own map clicks; typing is read in UpdateUI like the search.
public sealed partial class RecipeBookSystem
{
	private enum MarkerPanel : byte
	{
		None,
		Create,
		View,
		Edit
	}

	private const float MarkerWidth = 384f;

	private static readonly object MarkerOwner = new object();

	private static MarkerPanel MarkerMode;

	private static int MarkerId;

	private static int MarkerX;

	private static int MarkerY;

	private static string MarkerName = "";

	private static byte MarkerBadge;

	private static bool MarkerTyping;

	private static bool MarkerOwnBlock;

	// The left button, hidden from the map this update because the press belongs to the panel.
	private static bool MarkerHeldLeft;

	// A left press that starts on the panel is the panel's until the button is let go, also after its click closed the panel:
	// the map takes its drag anchor only when it sees a press (Main.DrawMap, grabMapX), so a press it never saw dragged the
	// map from a stale anchor on the next held frame.
	private static bool MarkerOwnsLeft;

	private static bool MarkerLastLeft;

	private static Vector2 MarkerAnchor;

	// The panel in raw screen pixels, for the update-time hover test.
	private static Rectangle MarkerPixels;

	// The right button in the last map frame. Vanilla stops updating Main.mouseRightRelease while the fullscreen map is open
	// (only the left one is kept), so the press is found here; while the map is closed it follows the button.
	private static bool MarkerLastRight;

	internal static bool MarkerPanelOpen => MarkerMode != MarkerPanel.None;

	private static float MarkerHeight => (MarkerMode == MarkerPanel.View) ? 196f : 284f;

	// The map drags, pings and clicks pylons with the left button while it draws: not with a press that belongs to the panel.
	// Runs last in the update, so the map then draws with these values.
	public override void PostUpdateEverything()
	{
		// What the player saw before the key the game's triggers report next update (RecipeBookSystem.OpenAtUpdate).
		OpenAtUpdate = OpenLastUpdate;
		LayerAtUpdate = LayerLastUpdate;
		TypingAtUpdate = TypingLastUpdate;
		OpenLastUpdate = Open;
		LayerLastUpdate = LayerOpen;
		TypingLastUpdate = Typing;
		if (!Main.mapFullscreen)
		{
			MarkerLastRight = Main.mouseRight;
		}
		bool left = Main.mouseLeft;
		if (left && !MarkerLastLeft)
		{
			MarkerOwnsLeft = MarkerMode != MarkerPanel.None && Main.mapFullscreen && MarkerPixels.Contains(PlayerInput.MouseX, PlayerInput.MouseY);
		}
		MarkerOwnsLeft &= left;
		MarkerLastLeft = left;
		if (MarkerOwnsLeft && Main.mapFullscreen)
		{
			MarkerHeldLeft = true;
			Main.mouseLeft = false;
		}
	}

	public override void PostDrawFullscreenMap(ref string mouseText)
	{
		if (MarkerHeldLeft)
		{
			Main.mouseLeft = true;
			MarkerHeldLeft = false;
		}
		// The chat draws after the map and calls HandleIME with PlayerInput.WritingText, which the update has reset since
		// (Main.DoDraw); a disabled IME drops every typed character (ReLogic WinImm32Ime). The book's search does the same in
		// its interface layer, which draws after the chat.
		if (MarkerTyping)
		{
			PlayerInput.WritingText = true;
			Main.instance.HandleIME();
		}
		bool rightPressed = Main.mouseRight && !MarkerLastRight;
		MarkerLastRight = Main.mouseRight;
		// The teleport may have closed the map in this same draw (a Map Teleport key rebound to the right button).
		if (!Main.mapFullscreen)
		{
			return;
		}
		if (MarkerMode == MarkerPanel.None)
		{
			if (rightPressed && BookMapMarkers.Enabled && !Main.LocalPlayer.dead)
			{
				OpenMarkerPanel(BookMapMarkerLayer.Hovered);
			}
			return;
		}
		SpriteBatch sb = Main.spriteBatch;
		Vector2 bookOrigin = Origin;
		Vector2 bookMouse = Mouse;
		float bookScale = Scale;
		float graphics = GraphicsOpacity;
		float modal = ModalOpacity;
		Scale = BookGeometry.Create(Main.screenWidth, Main.screenHeight, Main.UIScale, ModContent.GetInstance<BookConfig>().WindowScalePercent, WindowScalePercent, HasCustomWindowPosition, WindowPositionX, WindowPositionY).Scale;
		float width = MarkerWidth * Scale;
		float height = MarkerHeight * Scale;
		Origin = new Vector2(Math.Clamp(MarkerAnchor.X, 8f, Math.Max(8f, Main.screenWidth - width - 8f)), Math.Clamp(MarkerAnchor.Y, 8f, Math.Max(8f, Main.screenHeight - height - 8f)));
		Mouse = (Main.MouseScreen - Origin) / Scale;
		GraphicsOpacity = (ModalOpacity = 1f);
		MarkerPixels = new Rectangle((int)(Origin.X * Main.UIScale), (int)(Origin.Y * Main.UIScale), (int)(width * Main.UIScale), (int)(height * Main.UIScale));
		bool over = Hit(0f, 0f, MarkerWidth, MarkerHeight);
		bool click = over && Main.mouseLeft && Main.mouseLeftRelease;
		if (click)
		{
			Main.mouseLeftRelease = false;
		}
		Box(sb, 0f, 0f, MarkerWidth, MarkerHeight, Surface, border: true, 12f);
		if (MarkerMode == MarkerPanel.View)
		{
			DrawMarkerView(sb, click);
		}
		else
		{
			DrawMarkerForm(sb, click);
		}
		FlushTip();
		Origin = bookOrigin;
		Mouse = bookMouse;
		Scale = bookScale;
		GraphicsOpacity = graphics;
		ModalOpacity = modal;
		if (over)
		{
			mouseText = "";
		}
	}

	private static void OpenMarkerPanel(BookMapMarker marker)
	{
		if (marker == null)
		{
			Vector2 tile = BookMapTeleport.CursorTile();
			int x = (int)Math.Floor(tile.X);
			int y = (int)Math.Floor(tile.Y);
			if (!WorldGen.InWorld(x, y))
			{
				BookMapTeleport.Say(L("Markers.NotHere"));
				return;
			}
			(MarkerX, MarkerY) = (x, y);
			MarkerName = "";
			MarkerBadge = BookMarkerBadges.Default;
		}
		SetTyping(value: false);
		MarkerMode = (marker == null) ? MarkerPanel.Create : MarkerPanel.View;
		MarkerId = marker?.Id ?? 0;
		MarkerAnchor = Main.MouseScreen + new Vector2(18f);
		BlockMarkerInput();
		if (marker == null)
		{
			StartMarkerTyping();
		}
	}

	// Every update while open, as the book does while typing: Magic Storage resets Main.blockInput to its own search bars' focus
	// each update.
	private static void BlockMarkerInput()
	{
		MarkerOwnBlock |= !Main.blockInput;
		if (MarkerOwnBlock)
		{
			Main.blockInput = true;
		}
	}

	private static void StartMarkerTyping()
	{
		if (!MarkerTyping)
		{
			Main.clrInput();
		}
		MarkerTyping = true;
		Main.CurrentInputTextTakerOverride = MarkerOwner;
	}

	private static void CloseMarkerPanel()
	{
		if (MarkerMode == MarkerPanel.None)
		{
			return;
		}
		MarkerMode = MarkerPanel.None;
		MarkerTyping = false;
		if (Main.CurrentInputTextTakerOverride == MarkerOwner)
		{
			Main.CurrentInputTextTakerOverride = null;
		}
		if (MarkerOwnBlock)
		{
			Main.blockInput = false;
			MarkerOwnBlock = false;
		}
		// An Esc or Enter still held must not then close the map or open the chat: both wait for a release.
		Main.chatRelease = false;
		if (!Main.gameMenu)
		{
			Main.LocalPlayer.releaseInventory = false;
		}
	}

	// UpdateUI: the panel closes with the map, the setting or its marker; the name field reads the keyboard.
	private static void UpdateMarkerInput()
	{
		if (MarkerMode == MarkerPanel.None)
		{
			return;
		}
		if (Main.gameMenu || !Main.mapFullscreen || !BookMapMarkers.Enabled || (MarkerMode != MarkerPanel.Create && BookMapMarkers.Find(MarkerId) == null))
		{
			CloseMarkerPanel();
			return;
		}
		BlockMarkerInput();
		if (!MarkerTyping)
		{
			if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape))
			{
				CloseMarkerPanel();
			}
			return;
		}
		PlayerInput.WritingText = true;
		Main.CurrentInputTextTakerOverride = MarkerOwner;
		Main.instance.HandleIME();
		string next = Main.GetInputText(MarkerName).Replace("\r", "").Replace("\n", "");
		MarkerName = (next.Length > BookMapMarkers.NameLength) ? next.Substring(0, BookMapMarkers.NameLength) : next;
		if (Main.inputTextEnter)
		{
			SaveMarker();
		}
		else if (Main.inputTextEscape)
		{
			CloseMarkerPanel();
		}
	}

	private static void SaveMarker()
	{
		// The name is optional: an unnamed marker is titled by its badge.
		string name = BookMapMarkers.Clean(MarkerName);
		if (MarkerMode == MarkerPanel.Create)
		{
			BookMapMarkers.Create(MarkerX, MarkerY, name, MarkerBadge);
		}
		else if (MarkerMode == MarkerPanel.Edit)
		{
			BookMapMarkers.Edit(MarkerId, name, MarkerBadge);
		}
		CloseMarkerPanel();
	}

	private static void DrawMarkerForm(SpriteBatch sb, bool click)
	{
		Text(sb, (MarkerMode == MarkerPanel.Create) ? L("Markers.NewMarker") : L("Markers.EditMarker"), 18f, 14f, TextColor, 1f, MarkerWidth - 36f, 26f);
		BookRect field = new BookRect(18f, 50f, MarkerWidth - 36f, 36f);
		Box(sb, field, MarkerTyping ? new Color(35, 51, 40) : FieldFill, border: true, 8f, MarkerTyping ? new Color?(Accent) : null, (!MarkerTyping) ? 1 : 2);
		string shown = (MarkerName.Length > 0) ? (MarkerName + ((MarkerTyping && (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0) ? "|" : "")) : L("Markers.NameOptional");
		CenterLabel(sb, shown, field.X + 12f, field.Y + 4f, field.Width - 76f, field.Height - 8f, (MarkerName.Length > 0) ? TextColor : Muted, 0.9f, rightAligned: false, leftAligned: true);
		CenterLabel(sb, MarkerName.Length + "/" + BookMapMarkers.NameLength, field.X + field.Width - 62f, field.Y + 4f, 50f, field.Height - 8f, Muted, 0.75f, rightAligned: true);
		if (click && field.Contains(Mouse.X, Mouse.Y))
		{
			StartMarkerTyping();
		}
		for (int i = 0; i < BookMarkerBadges.Count; i++)
		{
			BookRect cell = new BookRect(16f + (float)(i % BookMarkerBadges.Columns) * 60f, 100f + (float)(i / BookMarkerBadges.Columns) * 60f, 52f, 52f);
			bool selected = i == MarkerBadge;
			bool hover = cell.Contains(Mouse.X, Mouse.Y);
			Box(sb, cell, selected ? new Color(50, 76, 59) : (hover ? HoverFill : Raised), border: true, 8f, selected ? new Color?(Accent) : null, (!selected) ? 1 : 2);
			DrawBadge(sb, (byte)i, cell, 32f);
			if (hover)
			{
				BookMarkerBadges.Badge badge = BookMarkerBadges.All[i];
				MouseTip(T(badge.Ru, badge.En));
				if (click)
				{
					MarkerBadge = (byte)i;
				}
			}
		}
		BookRect cancel = new BookRect(18f, 228f, 166f, 38f);
		BookRect save = new BookRect(200f, 228f, 166f, 38f);
		CardButton(sb, cancel, L("Markers.Cancel"));
		CardButton(sb, save, L("Markers.Save"), primary: true);
		if (cancel.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(L("Markers.CancelEsc"));
			if (click)
			{
				CloseMarkerPanel();
			}
		}
		else if (save.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(L("Markers.SaveEnter"));
			if (click)
			{
				SaveMarker();
			}
		}
	}

	private static void DrawMarkerView(SpriteBatch sb, bool click)
	{
		BookMapMarker marker = BookMapMarkers.Find(MarkerId);
		if (marker == null)
		{
			CloseMarkerPanel();
			return;
		}
		BookMarkerBadges.Badge badge = BookMarkerBadges.Get(marker.Badge);
		BookRect icon = new BookRect(18f, 16f, 44f, 44f);
		Box(sb, icon, Raised, border: true, 8f);
		DrawBadge(sb, marker.Badge, icon, 30f);
		Text(sb, BookMapMarkers.Title(marker), 74f, 14f, TextColor, 1.05f, MarkerWidth - 92f, 26f);
		if (marker.Name.Length > 0)
		{
			Text(sb, T(badge.Ru, badge.En), 74f, 40f, Muted, 0.8f, MarkerWidth - 92f, 18f);
		}
		Text(sb, BookMapMarkers.Placed(marker), 18f, 74f, TextColor, 0.85f, MarkerWidth - 36f, 22f);
		if (marker.Edited != marker.Created)
		{
			Text(sb, L("Markers.Edited") + BookMapMarkers.Ago(marker.Edited), 18f, 98f, Muted, 0.8f, MarkerWidth - 36f, 20f);
		}
		BookRect close = new BookRect(256f, 140f, 110f, 38f);
		if (BookMapMarkers.CanChange(marker))
		{
			BookRect edit = new BookRect(18f, 140f, 110f, 38f);
			BookRect delete = new BookRect(137f, 140f, 110f, 38f);
			CardButton(sb, edit, L("Markers.Edit"));
			CardButton(sb, delete, L("Markers.Delete"));
			if (click && edit.Contains(Mouse.X, Mouse.Y))
			{
				MarkerMode = MarkerPanel.Edit;
				MarkerName = marker.Name;
				MarkerBadge = marker.Badge;
				StartMarkerTyping();
				return;
			}
			if (click && delete.Contains(Mouse.X, Mouse.Y))
			{
				BookMapMarkers.Delete(marker.Id);
				CloseMarkerPanel();
				return;
			}
		}
		else
		{
			Text(sb, L("Markers.AuthorOrHostOnly"), 18f, 148f, Muted, 0.8f, 226f, 22f);
		}
		CardButton(sb, close, L("Markers.Close"));
		if (close.Contains(Mouse.X, Mouse.Y))
		{
			MouseTip(L("Markers.CloseEsc"));
			if (click)
			{
				CloseMarkerPanel();
			}
		}
	}

	// The badge's item sprite, centred in the cell and fitted to size, without the item icon's lookup and tooltip.
	private static void DrawBadge(SpriteBatch sb, byte index, BookRect cell, float size)
	{
		int item = BookMarkerBadges.Get(index).Item;
		Main.instance.LoadItem(item);
		Texture2D texture = TextureAssets.Item[item].Value;
		Rectangle frame = Main.itemAnimations[item]?.GetFrame(texture) ?? texture.Bounds;
		float fit = Math.Min(size / (float)frame.Width, size / (float)frame.Height);
		sb.Draw(texture, Origin + new Vector2(cell.X + cell.Width / 2f, cell.Y + cell.Height / 2f) * Scale, frame, Color.White, 0f, frame.Size() / 2f, fit * Scale, SpriteEffects.None, 0f);
	}
}
