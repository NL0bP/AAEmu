using System;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using ZstdSharp.Unsafe;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCRestrictPacket : GamePacket
{
    private readonly string _chname;
    private readonly int _index;
    private readonly byte _restrictCode;
    private readonly uint _duration;
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;

    public SCRestrictPacket() : base(SCOffsets.SCRestrictPacket, 5)
    {
        _chname = "";
        _index = 0;
        _restrictCode = 0;
        _duration = 0;
        _startDate = DateTime.MinValue;
        _endDate = DateTime.MinValue;

    }
    public override PacketStream Write(PacketStream stream)
    {
            stream.Write(_chname);
            stream.Write(_index);
            stream.Write(_restrictCode);
            stream.Write(_duration);
            stream.Write(_startDate);
            stream.Write(_endDate);
            return stream;
        }
}
