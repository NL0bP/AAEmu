using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Skills;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCBuffStatePacket(Buff buff) : GamePacket(SCOffsets.SCBuffStatePacket, 5)
{
    public override PacketLogLevel LogLevel => PacketLogLevel.Trace;

    public override PacketStream Write(PacketStream stream)
    {
        // Structure from x2game_dump.dll (SCBuffState_0x04E deserializer @ 0x399438C0):
        // SkillCaster (type + bc), uint32 casterId, bc targetId, optional uint32 buffId
        stream.Write(buff.SkillCaster);        // skillCaster
        stream.Write(buff.Caster?.Id ?? 0);    // casterId
        stream.WriteBc(buff.Owner.ObjId);      // targetId
        stream.Write(true);                    // buffId block present
        stream.Write(buff.Index);              // buffId

        return stream;
    }

    public override string Verbose()
    {
        return $" - {buff.Owner.DebugName()} <- {buff?.Template?.BuffId}";
    }
}
