using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCListSkillActiveTypsPacket : GamePacket
{
    public readonly record struct SkillActiveType(ushort SkillId, byte ActiveType);

    private readonly SkillActiveType[] _entries;

    public SCListSkillActiveTypsPacket() : base(SCOffsets.SCListSkillActiveTypsPacket, 5)
    {
        _entries = [];
    }

    public SCListSkillActiveTypsPacket(SkillActiveType[] entries) : base(SCOffsets.SCListSkillActiveTypsPacket, 5)
    {
        _entries = entries;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write((uint)_entries.Length);
        foreach (var entry in _entries)
        {
            stream.Write((byte)1); // skillType block present
            stream.Write(entry.SkillId);
            stream.Write(entry.ActiveType);
        }

        return stream;
    }
}
