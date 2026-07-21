using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSHeroRequestRankDataPacket() : GamePacket(CSOffsets.CSHeroRequestRankDataPacket, 5)
{
    public override PacketLogLevel LogLevel => PacketLogLevel.Off;

    private byte[] _payload = [];

    public override void Read(PacketStream stream)
    {
        // The client sends a fixed-size blob here; keep it opaque for now.
        _payload = stream.ReadBytes(stream.LeftBytes);
    }
}
