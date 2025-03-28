using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStartedCinema2Packet() : GamePacket(CSOffsets.CSStartedCinema2Packet, 5)
{
    public override void Read(PacketStream stream)
    {
        // Empty struct
        Logger.Warn("StartedCinema2");
        //Connection.ActiveChar.Events.OnCinemaStarted(Connection.ActiveChar, new OnCinemaStartedArgs() { CinemaId = Connection.ActiveChar.CurrentlyPlayingCinemaId });
    }
}
