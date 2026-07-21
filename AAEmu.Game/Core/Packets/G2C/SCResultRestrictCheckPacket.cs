using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCResultRestrictCheckPacket(uint characterId, byte code, byte result)
    : GamePacket(SCOffsets.SCResultRestrictCheckPacket, 5)
{
    public override PacketStream Write(PacketStream stream)
    {
        // 3.5.0.3 client reads only msg:string (deserializer @ 0x3991D430, class opcode 641=0x281)
        // TODO: map (characterId, code, result) to a proper message
        stream.Write("");
        return stream;
    }
}
