using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSRequestHouseTaxPacket : GamePacket
{
    public CSRequestHouseTaxPacket() : base(CSOffsets.CSRequestHouseTaxPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSRequestHouseTaxPacket...");

        var tl = stream.ReadUInt16(); // houseId
        var objId = stream.ReadBc();

        Logger.Debug($"RequestHouseTax, Tl: {tl}, objId: {objId}");

        HousingManager.Instance.SendHouseTaxInfo(Connection, tl, objId);
    }
}
