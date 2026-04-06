using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCListSkillActiveTypsPacket : GamePacket
{
    private readonly (uint _skillId, byte _activeType)[] _skillActiveTyps;

    public SCListSkillActiveTypsPacket((uint skillId, byte activeType)[] skillActiveTyps) : base(SCOffsets.SCListSkillActiveTypsPacket, 5)
    {
        _skillActiveTyps = skillActiveTyps;
    }

    public override PacketStream Write(PacketStream stream)
    {
        var count = _skillActiveTyps.Length;
        stream.Write(count);
        for (var i = 0; i < count; i++) // max 100
        {
            stream.Write(_skillActiveTyps[i]._skillId);    // skillType (type)
            stream.Write(_skillActiveTyps[i]._activeType); // activeType (1=all, 2=female, 3=male)
        }
        return stream;
    }
}
