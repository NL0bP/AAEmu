using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSRequestSysInstanceIndexPacket : GamePacket
{
    public CSRequestSysInstanceIndexPacket() : base(CSOffsets.CSRequestSysInstanceIndexPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        var type = stream.ReadUInt16();
        var zoneId = stream.ReadInt32();
        var instId = stream.ReadUInt32();
    }
}
