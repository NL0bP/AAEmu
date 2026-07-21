using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCBuffUpdatedPacket(uint objId, uint index, int stack, int charged) : GamePacket(SCOffsets.SCBuffUpdatedPacket, 5)
{
    public override PacketLogLevel LogLevel => PacketLogLevel.Trace;

    public override PacketStream Write(PacketStream stream)
    {
        // Structure from x2game_dump.dll (SCBuffUpdated_0x12B deserializer @ 0x3993E500):
        // bc objId, optional uint32 index, int32 stack, int32 charged
        stream.WriteBc(objId);
        stream.Write(true); // index block present
        stream.Write(index);
        stream.Write(stack);
        stream.Write(charged);

        return stream;
    }
}
