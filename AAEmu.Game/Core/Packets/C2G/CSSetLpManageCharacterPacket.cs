using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSSetLpManageCharacterPacket() : GamePacket(CSOffsets.CSSetLpManageCharacterPacket, 1)
{
    public override void Read(PacketStream stream)
    {
        var characterId = stream.ReadUInt32();
        // Retail 3.5.0.3 server does not respond to this packet (verified against packet capture)
        // Connection.SendPacket(new SCCharacterLpManagedPacket(characterId));
    }
}
