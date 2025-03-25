using System;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCGameEventPacket : GamePacket
{
    private readonly int _count;
    private readonly DateTime _loadedTime;
        
    public SCGameEventPacket(int count, DateTime loadedTime) : base(SCOffsets.SCGameEventPacket, 5)
    {
        _count = count;
        _loadedTime = loadedTime;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_count);
        stream.Write(_loadedTime);
        return stream;
    }
}
