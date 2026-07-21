using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Housing;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCAddHousePacket(House[] houses) : GamePacket(SCOffsets.SCAddHousePacket, 5)
{
    public const int MaxCountPerPacket = 20;

    public override PacketStream Write(PacketStream stream)
    {
        var count = houses.Length;
        if (count > MaxCountPerPacket)
            count = MaxCountPerPacket;

        stream.Write((byte)count);
        for (var i = 0; i < count; i++)
            houses[i].WriteAddHouse(stream);

        return stream;
    }
}
