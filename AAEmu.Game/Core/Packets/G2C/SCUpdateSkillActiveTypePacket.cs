using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCUpdateSkillActiveTypePacket : GamePacket
{
    private readonly uint _heirSkillType;
    private readonly uint _skillId;
    private readonly byte _activeType;

    public SCUpdateSkillActiveTypePacket(uint heirSkillType, uint skillId, byte activeType) : base(SCOffsets.SCUpdateSkillActiveTypePacket, 5)
    {
        _heirSkillType = heirSkillType;
        _skillId = skillId;
        _activeType = activeType;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_heirSkillType);  // heirSkillType
        stream.Write(_skillId);        // skillType
        stream.Write(_activeType);     // activeType (1=all, 2=female, 3=male)
        return stream;
    }
}
