using System.Linq;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Housing;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSRotateHousePacket : GamePacket
{
    public CSRotateHousePacket() : base(CSOffsets.CSRotateHousePacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSRotateHousePacket...");

        var objId = stream.ReadBc();
        var zRot = stream.ReadSingle();
        var height = stream.ReadSingle();

        Logger.Debug($"CSRotateHouse, objId: {objId}, zRot: {zRot}, height: {height}");

        var houses = HousingManager.Instance.GetAllHousesByCharacterId(Connection.ActiveChar.Id);
        foreach (var house in houses.Where(house => house.ObjId == objId))
        {
            house.Transform.World.Rotation = house.Transform.World.Rotation with { Z = zRot };
            house.Transform.World.Position = house.Transform.World.Position with { Z = height };
            house.IsDirty = true;
            // Обновляем мировые координаты всех прикрепленных объектов
            house.Transform.FinalizeTransform();
        }

        Connection.ActiveChar.SendPacket(new SCHouseRotatedPacket(objId, zRot));
    }
}
