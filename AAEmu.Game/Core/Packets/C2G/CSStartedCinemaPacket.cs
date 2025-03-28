using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStartedCinemaPacket() : GamePacket(CSOffsets.CSStartedCinemaPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        // Empty struct
        Logger.Debug("Entering in StartedCinema...");
        Connection.ActiveChar.Events.OnCinemaStarted(Connection.ActiveChar, new OnCinemaStartedArgs() { CinemaId = Connection.ActiveChar.CurrentlyPlayingCinemaId });
    }
}
