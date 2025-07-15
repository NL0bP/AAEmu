using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSAncestrallSkillLearnPacket : GamePacket
{
    public CSAncestrallSkillLearnPacket() : base(CSOffsets.CSAncestrallSkillLearnPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSAncestrallSkillLearnPacket...");
        var type = stream.ReadUInt32();
        var skillId = stream.ReadUInt32();
        var isChange = stream.ReadBoolean();

        Connection.ActiveChar.SendPacket(new SCActivatedHeirSkillPacket(type, skillId, isChange));
    }
}
