using System;
using System.Numerics;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSRequestGameEventInfoPacket() : GamePacket(CSOffsets.CSRequestGameEventInfoPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        // empty
        Logger.Debug("Entering in CSRequestGameEventInfo...");

        var count = 0;
        var loadedTime = DateTime.UtcNow;
        Connection.SendPacket(new SCGameEventPacket(count, loadedTime));

        var unitObjId = Connection.ActiveChar.ObjId;
        uint returnDistrict = 342;
        uint resurrectionDistrict = 70;
        var returnDistrictChanged = false;
        uint id = 0;
        var name = "";
        uint zoneKey = 0;
        var pos = new Vector3(0, 0, 0); //Connection.ActiveChar.Transform.World.Position;
        var zRot = 0f; //Connection.ActiveChar.Transform.World.Rotation.Z;
        var isFavorite = false;
        Connection.SendPacket(new SCCharacterBoundPacket(unitObjId, returnDistrict, resurrectionDistrict, returnDistrictChanged, id, name, zoneKey, pos, zRot, isFavorite));

        Connection.ActiveChar?.TodayAssignments?.Send();

    }
}
