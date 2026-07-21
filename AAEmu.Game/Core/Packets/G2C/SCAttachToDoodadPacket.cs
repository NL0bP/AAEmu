using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.DoodadObj;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCAttachToDoodadPacket(uint unitObjId, BondDoodad bond) : GamePacket(SCOffsets.SCAttachToDoodadPacket, 5)
{
    public override PacketStream Write(PacketStream stream)
    {
        stream.WriteBc(unitObjId);
        stream.Write(bond);
        return stream;
    }
}