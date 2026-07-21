using AAEmu.Commons.Network;

namespace AAEmu.Game.Models.Game.InstantGame;

public class VisitCountItem : PacketMarshaler
{
    public int ZoneId { get; set; }
    public uint Data { get; set; }
    public int Count { get; set; }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(ZoneId);
        stream.Write(Data);
        stream.Write(Count);
        return stream;
    }
}
