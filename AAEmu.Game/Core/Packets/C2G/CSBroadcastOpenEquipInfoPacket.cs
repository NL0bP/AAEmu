using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSBroadcastOpenEquipInfoPacket() : GamePacket(CSOffsets.CSBroadcastOpenEquipInfoPacket, 5)
{
    public override PacketLogLevel LogLevel => PacketLogLevel.Off;

    private byte[] _payload = [];

    public override void Read(PacketStream stream)
    {
        // The client currently sends a fixed-size zeroed payload here.
        _payload = stream.ReadBytes(stream.LeftBytes);
    }
}
