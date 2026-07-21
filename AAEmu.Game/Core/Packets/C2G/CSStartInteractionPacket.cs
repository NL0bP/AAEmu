using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Funcs;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.StaticValues;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStartInteractionPacket() : GamePacket(CSOffsets.CSStartInteractionPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        var npcObjId = stream.ReadBc();
        var objId = stream.ReadBc();
        var extraInfo = stream.ReadInt32();
        var pickId = stream.ReadInt32();
        var mouseButton = stream.ReadByte();
        var modifierKeys = stream.ReadInt32();

        Logger.Warn("StartInteraction, NpcObjId: {0}, objId: {1}, extraInfo: {2}, pickId: {3}, mouse: {4}, mods: {5}", npcObjId, objId, extraInfo, pickId, mouseButton, modifierKeys);
        var activeChar = Connection.ActiveChar;
        var npc = activeChar?.ParentWorld?.GetNpc(npcObjId);
        // TODO: Distance-check
        if (npc != null)
        {
            // The returned skillsList is supposed to be a list of what actions you can take, and the client will
            // use the first one regardless of what you put in there.
            // Also noted is that even when you send a zero (0) skill list back (one skill of 0),
            // it will still use the first action that is prompted to the user. This effectively makes quest NPCS
            // right-clickable as intended
            // This could later be used to implement some of the anti-cheating
            // 0 is the intended default or else quests go wonky

            uint option = 0;
            if (npc.Template.Banker)
                option = SkillsEnum.UseWarehouse; // Open warehouse
            // TODO: fill in the skills and maybe change the order to what it would show in-game
            else if (npc.Template.AbilityChanger)
                option = SkillsEnum.ChangeSkillsets; // Open Skill-Trainer
            else if (npc.Template.Auctioneer)
                option = SkillsEnum.UseAuctioneer; // Open Auctioneer
            else if (npc.Template.Priest)
                option = SkillsEnum.Blessing; // Open Recover-Exp dialog ?
            else if (npc.Template.Repairman)
                option = SkillsEnum.Repair; // Open Repair dialog ?
            else if (npc.Template.Merchant)
                option = SkillsEnum.UseStore; // Open Shop dialog ?
            else if (npc.Template.Stabler)
                option = SkillsEnum.HealPetSWounds; // Open Pet Recovery dialog ?
            else if (npc.Template.Expedition)
                option = SkillsEnum.FormGuild; // Open Repair dialog ?
            else if (npc.Template.RecrutingBattlefieldId > 0)
                option = SkillsEnum.WarSupport; // Open Arena dialog ?
            else if (npc.Template.Blacksmith)
                option = SkillsEnum.ItemFusion; // Open Item Fuse dialog ?

            activeChar.SendPacket(new SCNpcInteractionSkillListPacket(npcObjId, objId, extraInfo, pickId, mouseButton, modifierKeys, [option]));
        }

        var slave = activeChar?.ParentWorld?.GetUnit(npcObjId);
        if (slave is Mate)
        {
            activeChar.SendPacket(new SCNpcInteractionSkillListPacket(npcObjId, objId, extraInfo, pickId, mouseButton, modifierKeys, [SkillsEnum.SlaveMounting]));
            return;
        }

        // Handle doodad interaction (port from reference 3.5 server: client shows the F prompt
        // only after the server answers with an interaction skill list)
        var doodad = activeChar?.ParentWorld?.GetDoodad(npcObjId);
        if (doodad == null) { return; }

        var skillId = GetDoodadInteractionSkillId(doodad);
        if (skillId > 0)
        {
            activeChar.SendPacket(new SCNpcInteractionSkillListPacket(npcObjId, objId, extraInfo, pickId, mouseButton, modifierKeys, [skillId]));
        }
        else
        {
            // Send 0 to allow client to use first prompted action for non-skill doodads (loot, cutdown, etc.)
            activeChar.SendPacket(new SCNpcInteractionSkillListPacket(npcObjId, objId, extraInfo, pickId, mouseButton, modifierKeys, [0]));
        }
    }

    /// <summary>
    /// Gets the interaction skill ID for a doodad based on its current funcs.
    /// </summary>
    private static uint GetDoodadInteractionSkillId(Doodad doodad)
    {
        if (doodad.CurrentFuncs == null || doodad.CurrentFuncs.Count == 0)
            return 0;

        foreach (var func in doodad.CurrentFuncs)
        {
            // Direct skill ID from doodad_funcs table
            if (func.SkillId > 0)
                return func.SkillId;

            // Check func template for skill IDs
            var template = DoodadManager.Instance.GetFuncTemplate(func.FuncId, func.FuncType);
            switch (template)
            {
                case DoodadFuncFakeUse fakeUse when fakeUse.FakeSkillId > 0:
                    return fakeUse.FakeSkillId;
                case DoodadFuncUse use when use.SkillId > 0:
                    return use.SkillId;
                case DoodadFuncConditionalUse conditional when conditional.FakeSkillId > 0:
                    return conditional.FakeSkillId;
                case DoodadFuncConditionalUse conditional when conditional.SkillId > 0:
                    return conditional.SkillId;
            }
        }

        return 0;
    }
}
