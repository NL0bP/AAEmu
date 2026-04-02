using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSGameEventPacket() : GamePacket(CSOffsets.CSGameEventPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        // empty
        Logger.Debug("Entering in CSGameEvent...");

        Connection.SendPacket(new SCGameEvent2Packet());
        Connection.ActiveChar?.TodayAssignments?.Send();
    }
}
