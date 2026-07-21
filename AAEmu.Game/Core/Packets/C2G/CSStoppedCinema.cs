using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStoppedCinema() : GamePacket(CSOffsets.CSStoppedCinema, 5)
{
    public override PacketLogLevel LogLevel => PacketLogLevel.Off;

    private byte[] _payload = [];

    public override void Read(PacketStream stream)
    {
        // Raw payload preserved until the structure is fully reverse-engineered.
        _payload = stream.ReadBytes(stream.LeftBytes);
    }
}
