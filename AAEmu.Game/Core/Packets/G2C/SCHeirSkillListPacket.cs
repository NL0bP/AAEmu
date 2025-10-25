using System.Collections.Generic;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Skills;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCHeirSkillListPacket : GamePacket
{
    private readonly List<HeirSkill> _heirSkills;

    public SCHeirSkillListPacket(List<HeirSkill> heirSkills) : base(SCOffsets.SCHeirSkillListPacket, 5)
    {
        _heirSkills = heirSkills;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_heirSkills.Count);
        foreach (var heirSkill in _heirSkills)
            heirSkill.WriteHeirSkill(stream);

        return stream;
    }
}
