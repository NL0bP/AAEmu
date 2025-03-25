using System;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCGameEvent2Packet : GamePacket
{
    public SCGameEvent2Packet() : base(SCOffsets.SCGameEvent2Packet, 5)
    {
    }

    public override PacketStream Write(PacketStream stream)
    {
        return stream;
    }
}
