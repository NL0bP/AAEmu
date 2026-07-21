using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSRequestGameEventInfoPacket() : GamePacket(CSOffsets.CSRequestGameEventInfoPacket, 5)
{
    public override PacketLogLevel LogLevel => PacketLogLevel.Off;

    private byte[] _payload = [];

    public override void Read(PacketStream stream)
    {
        // Payload is currently unknown; preserve it so the session stays in sync.
        _payload = stream.ReadBytes(stream.LeftBytes);
    }
}
