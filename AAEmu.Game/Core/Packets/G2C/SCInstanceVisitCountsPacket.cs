using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.InstantGame;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCInstanceVisitCountsPacket : GamePacket
{
    private readonly VisitCountItem[] _items;

    public SCInstanceVisitCountsPacket(params VisitCountItem[] items)
        : base(SCOffsets.SCInstanceVisitCountsPacket, 5)
    {
        _items = items;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_items.Length);
        foreach (var item in _items)
            item.Write(stream);

        return stream;
    }
}
