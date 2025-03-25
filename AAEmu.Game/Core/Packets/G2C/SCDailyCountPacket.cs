using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCDailyCountPacket : GamePacket
{
    private readonly byte _totalCount;
    private readonly byte _dailyCount;
    private readonly byte _dailyMaxCount;

    public SCDailyCountPacket(byte totalCount, byte dailyCount, byte dailyMaxCount) : base(SCOffsets.SCDailyCountPacket, 5)
    {
        _totalCount = totalCount;
        _dailyCount = dailyCount;
        _dailyMaxCount = dailyMaxCount;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_totalCount);
        stream.Write(_dailyCount);
        stream.Write(_dailyMaxCount);

        return stream;
    }
}
