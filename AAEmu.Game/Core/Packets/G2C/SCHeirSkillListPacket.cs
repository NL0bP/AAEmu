using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCHeirSkillListPacket : GamePacket
{
    public readonly record struct HeirSkillEntry(
        ushort Type1,
        ushort Type2,
        ushort Type3,
        uint SkillLevel,
        byte Ability,
        byte HighAbility,
        bool IsActive);

    private readonly HeirSkillEntry[] _entries;

    public SCHeirSkillListPacket() : base(SCOffsets.SCHeirSkillListPacket, 5)
    {
        _entries = [];
    }

    public SCHeirSkillListPacket(HeirSkillEntry[] entries) : base(SCOffsets.SCHeirSkillListPacket, 5)
    {
        _entries = entries;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write((uint)_entries.Length);
        foreach (var entry in _entries)
        {
            stream.Write(entry.Type1);
            stream.Write(entry.Type2);
            stream.Write(entry.Type3);
            stream.Write(entry.SkillLevel);
            stream.Write(entry.Ability);
            stream.Write(entry.HighAbility);
            stream.Write(entry.IsActive);
        }

        return stream;
    }
}
