using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCActivatedHeirSkillPacket : GamePacket
{
    private readonly uint _heirSkillId;
    private readonly uint _skillId;
    private readonly bool _isChange;

    public SCActivatedHeirSkillPacket(uint heirSkillId, uint skillId, bool isChange) : base(SCOffsets.SCActivatedHeirSkillPacket, 5)
    {
        _heirSkillId = heirSkillId;
        _skillId = skillId;
        _isChange = isChange;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_heirSkillId);
        stream.Write(_skillId);
        stream.Write(_isChange);

        return stream;
    }
}
