using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSEditorAddGimmickPacket : GamePacket
    {
        public CSEditorAddGimmickPacket() : base(CSOffsets.CSEditorAddGimmickPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var gimmick = new Gimmick();

            gimmick.GimmickId = stream.ReadUInt32();  // id
            gimmick.TemplateId = stream.ReadUInt32(); // type
            gimmick.EntityGuid = stream.ReadUInt64();
            gimmick.Faction = FactionManager.Instance.GetFaction(stream.ReadUInt32()); // type
            gimmick.SpawnerUnitId = stream.ReadUInt32();
            gimmick.GrasperUnitId = stream.ReadUInt32();
            gimmick.Position.ZoneId = (uint)stream.ReadInt32();
            gimmick.ModelPath = stream.ReadString();

            var (x, y, z) = stream.ReadWorldPosition();
            gimmick.WorldPos = new WorldPos(x, y, z);
            //var x = Helpers.ConvertLongX(stream.ReadInt64());  // World Pos
            //var y = Helpers.ConvertLongY(stream.ReadInt64());
            //var z = stream.ReadSingle();

            gimmick.Rot = stream.ReadQuaternionWithScalarSingle();
            //var x2 = stream.ReadSingle(); // Quaternion
            //var y2 = stream.ReadSingle();
            //var z2 = stream.ReadSingle();
            //var w2 = stream.ReadSingle();

            gimmick.Scale = stream.ReadSingle();

            gimmick.Vel = stream.ReadVector3Single();
            //var velX = stream.ReadSingle(); // vel
            //var velY = stream.ReadSingle();
            //var velZ = stream.ReadSingle();

            gimmick.AngVel = stream.ReadVector3Single();
            //var angVelX = stream.ReadSingle(); // angVel
            //var angVelY = stream.ReadSingle();
            //var angVelZ = stream.ReadSingle();

            gimmick.ScaleVel = stream.ReadSingle();

            _log.Debug("CSEditorAddGimmickPacket");

            Connection.ActiveChar.SendPacket(new SCGimmicksCreatedPacket(new[] { gimmick }));
        }
    }
}
