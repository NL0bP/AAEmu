using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCActivatedHeirSkillPacket : GamePacket
{
    private readonly uint _type;
    private readonly uint _skillId;
    private readonly bool _isChange;

    public SCActivatedHeirSkillPacket(uint type, uint skillId, bool isChange) : base(SCOffsets.SCActivatedHeirSkillPacket, 5)
    {
        _type = type;
        _skillId = skillId;
        _isChange = isChange;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_type);
        stream.Write(_skillId);
        stream.Write(_isChange);

        return stream;
    }
}
