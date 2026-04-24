using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCPlaySequencePacket : GamePacket
{
    private readonly string _sequenceName;
    private readonly uint _houseUnitId;

    public SCPlaySequencePacket(string sequenceName, uint houseUnitId)
        : base(SCOffsets.SCPlaySequencePacket, 5)
    {
        _sequenceName = sequenceName;
        _houseUnitId = houseUnitId;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_sequenceName); // sequenceName
        stream.WriteBc(_houseUnitId); // houseUnitId - 24-bit (3 bytes)

        return stream;
    }
}
