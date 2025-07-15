using System.Collections.Generic;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSAncestralSkillResetPacket : GamePacket
{
    public CSAncestralSkillResetPacket() : base(CSOffsets.CSAncestralSkillResetPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSAncestralSkillResetPacket...");
        var resetKind = stream.ReadUInt32();
        var ability = stream.ReadByte();
        var skillId = stream.ReadUInt32();

        var tasks = new List<ItemTask>();
        if (Connection.ActiveChar.Money <= 6500)
            return;

        Connection.ActiveChar.SendPacket(new SCResetHeirSkillPacket(resetKind, skillId, ability));
        Connection.ActiveChar.ChangeMoney(SlotType.Bag, -6500);
        tasks.Add(new MoneyChange(-6500));
        Connection.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.HeirSkillReset, tasks, []));
    }
}
