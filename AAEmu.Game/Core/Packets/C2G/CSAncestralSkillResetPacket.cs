using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

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

        Connection.ActiveChar.Skills.HeroReset(resetKind, ability, skillId);
    }
}
