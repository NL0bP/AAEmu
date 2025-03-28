using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSHeroRequestRankDataPacket() : GamePacket(CSOffsets.CSHeroRequestRankDataPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSHeroRequestRankData...");
        
        var type = stream.ReadUInt32();
        var division = stream.ReadUInt32();

        Logger.Debug($"CSHeroRequestRankDataPacket: type = {type}, division = {division}");

        Connection.ActiveChar.SendPacket(new SCHeroRankDataPacket(division, type));
    }
}
