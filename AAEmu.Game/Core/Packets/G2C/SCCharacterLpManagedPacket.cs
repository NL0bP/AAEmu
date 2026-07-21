using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCCharacterLpManagedPacket(uint characterId) : GamePacket(SCOffsets.SCCharacterLpManagedPacket, 5)
{
    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(characterId);
        return stream;
    }
}
