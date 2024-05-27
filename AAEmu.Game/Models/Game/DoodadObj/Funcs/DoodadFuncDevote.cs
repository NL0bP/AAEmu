﻿using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncDevote : DoodadFuncTemplate
{
    // doodad_funcs
    public int Count { get; set; }
    public int ItemCount { get; set; }
    public uint ItemId { get; set; }

    private int CurrentCount { get; set; }

    public override void Use(BaseUnit caster, Doodad owner, uint skillId, int nextPhase = 0)
    {
        Logger.Debug($"DoodadFuncDevote: Count={Count}, ItemCount={ItemCount}, ItemId={ItemId}");

        var character = (Character)caster;
        if (character == null)
            return;

        if (character.Inventory.Bag.ConsumeItem(ItemTaskType.DoodadInteraction, ItemId, ItemCount, null) <= 0)
        {
            character.SendErrorMessage(ErrorMessageType.NotEnoughItem);
            owner.ToNextPhase = false;
            return;
        }

        CurrentCount += ItemCount;
        if (CurrentCount >= Count)
        {
            CurrentCount = 0;
            character.SendPacket(new SCDoodadChangedPacket(owner.ObjId, CurrentCount));
            ResidentManager.Instance.UpdateDevelopmentStage(character);
            owner.ToNextPhase = true;
            return;
        }

        character.SendPacket(new SCDoodadChangedPacket(owner.ObjId, CurrentCount));
        owner.ToNextPhase = false;
    }
}
