using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStoppedCinemaPacket() : GamePacket(CSOffsets.CSStoppedCinemaPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        // Empty struct
        Logger.Debug("Entering in CSStoppedCinema...");
        //Connection.ActiveChar.Events.OnCinemaEnded(Connection.ActiveChar, new OnCinemaEndedArgs() { CinemaId = Connection.ActiveChar.CurrentlyPlayingCinemaId });
    }
}
