using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSGetRankerInformationPacket() : GamePacket(CSOffsets.CSGetRankerInformationPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSGetRankerInformation...");

        var worldId = stream.ReadByte();
        var type = stream.ReadUInt32();
        
        Connection.ActiveChar.SendPacket(new SCRankerInformationPacket(worldId, type));
    }
}
