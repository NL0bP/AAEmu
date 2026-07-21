using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCBlockedUsersPacket(Blocked[] blockeds) : GamePacket(SCOffsets.SCBlockedUsersPacket, 5)
{
    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(blockeds.Length);
        stream.Write((ushort)blockeds.Length); // TODO max length 500
        foreach (var blocked in blockeds)
            stream.Write(blocked);
        return stream;
    }
}
