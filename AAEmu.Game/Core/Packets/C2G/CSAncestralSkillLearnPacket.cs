using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSAncestralSkillLearnPacket : GamePacket
{
    public CSAncestralSkillLearnPacket() : base(CSOffsets.CSAncestralSkillLearnPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSAncestralSkillLearnPacket...");
        var heirSkillId = stream.ReadUInt32();
        var skillId = stream.ReadUInt32();
        var isChange = stream.ReadBoolean();

        Connection.ActiveChar.Skills.AddHeroSkill(heirSkillId, skillId, isChange, true);
    }
}
