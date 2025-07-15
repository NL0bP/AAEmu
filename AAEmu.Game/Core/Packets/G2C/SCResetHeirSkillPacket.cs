using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCResetHeirSkillPacket : GamePacket
{
    private readonly uint _resetKind;
    private readonly uint _skillId;
    private readonly byte _ability;

    public SCResetHeirSkillPacket(uint resetKind, uint skillId, byte ability) : base(SCOffsets.SCResetHeirSkillPacket, 5)
    {
        _resetKind = resetKind;
        _skillId = skillId;
        _ability = ability;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_resetKind);
        stream.Write(_skillId);
        stream.Write(_ability);

        return stream;
    }
}
