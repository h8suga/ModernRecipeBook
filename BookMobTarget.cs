using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModernRecipeBook;

// Mouse3 on a mob opens its card. The mob is the one vanilla names under the cursor: the same box, the same
// exclusions (a hidden mimic stays hidden), a worm segment counts as its head. Read only, nothing is sent.
internal static class BookMobTarget
{
	internal static NPC UnderMouse()
	{
		Player player = Main.LocalPlayer;
		if (Main.gameMenu || Main.mapFullscreen || Main.ingameOptionsWindow || Main.inFancyUI || Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.blockInput || PlayerInput.WritingText || RecipeBookSystem.Typing || (RecipeBookSystem.Open && RecipeBookSystem.MouseOver) || player == null || !player.active || player.mouseInterface)
		{
			return null;
		}
		// Runs from UpdateUI, where the mouse is in UI coordinates; the world position needs the game zoom.
		PlayerInput.SetZoom_Unscaled();
		PlayerInput.SetZoom_MouseInWorld();
		Point mouse = Main.MouseWorld.ToPoint();
		PlayerInput.SetZoom_UI();
		for (int i = 0; i < Main.maxNPCs; i++)
		{
			NPC npc = Main.npc[i];
			if (!npc.active || npc.life <= 0 || !npc.ShowNameOnHover || (npc.shimmerTransparency != 0f && !npc.CanApplyHunterPotionEffects()) || npc.type == NPCID.TargetDummy || ((npc.type == NPCID.Mimic || npc.type == NPCID.PresentMimic || npc.type == NPCID.IceMimic || npc.aiStyle == NPCAIStyleID.BiomeMimic) && npc.ai[0] == 0f))
			{
				continue;
			}
			Vector2 bottom = npc.Bottom + npc.netOffset;
			Rectangle box = new Rectangle((int)bottom.X - npc.frame.Width / 2, (int)bottom.Y - npc.frame.Height, npc.frame.Width, npc.frame.Height);
			if (npc.type >= NPCID.WyvernHead && npc.type <= NPCID.WyvernTail)
			{
				box = new Rectangle((int)npc.Center.X - 32, (int)npc.Center.Y - 32, 64, 64);
			}
			NPCLoader.ModifyHoverBoundingBox(npc, ref box);
			if (!box.Contains(mouse) && !npc.Hitbox.Contains(mouse))
			{
				continue;
			}
			NPC head = (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].active) ? Main.npc[npc.realLife] : npc;
			return ContentSamples.NpcsByNetId.ContainsKey(head.netID) ? head : null;
		}
		return null;
	}
}
