using System.Collections.Generic;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSAncestralSkillLearnPacket : GamePacket
{
    public CSAncestralSkillLearnPacket() : base(CSOffsets.CSAncestralSkillLearnPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSAncestralSkillLearnPacket...");
        var type = stream.ReadUInt32();
        var skillId = stream.ReadUInt32();
        var isChange = stream.ReadBoolean();

        if (isChange)
        {
            var tasks = new List<ItemTask>();
            if (Connection.ActiveChar.Money <= 6500)
                return;

            Connection.ActiveChar.SendPacket(new SCActivatedHeirSkillPacket(type, skillId, true));
            Connection.ActiveChar.ChangeMoney(SlotType.Bag, -6500);
            tasks.Add(new MoneyChange(-6500));
            Connection.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.HeirSkillReset, tasks, []));
        }
        else
        {
            Connection.ActiveChar.SendPacket(new SCActivatedHeirSkillPacket(type, skillId, false));
        }
    }
}
