using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using System.Numerics;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCGlideBuffRemovedPacket(uint objId, uint index, Vector3 position) : GamePacket(SCOffsets.SCGlideBuffRemovedPacket, 5)
{
    public override PacketLogLevel LogLevel => PacketLogLevel.Trace;

    public override PacketStream Write(PacketStream stream)
    {
        // Structure from x2game_dump.dll (SCGlideBuffRemoved_0x17F deserializer @ 0x3993E460):
        // bc objId, optional uint32 index, position (9 bytes)
        stream.WriteBc(objId);
        stream.Write(true); // index block present
        stream.Write(index);
        stream.WritePosition(position);

        return stream;
    }
}
