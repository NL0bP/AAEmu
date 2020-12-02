using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSWorldRayCastingPacket : GamePacket
    {
        public CSWorldRayCastingPacket() : base(CSOffsets.CSWorldRayCastingPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var (x, y, z) = stream.ReadWorldPosition();
            //var x = stream.ReadUInt64();
            //var y = stream.ReadUInt64();
            //var z = stream.ReadSingle();

            var (x2, y2, z2) = stream.ReadPosition();
            //var x2 = stream.ReadSingle();
            //var y2 = stream.ReadSingle();
            //var z2 = stream.ReadSingle();
            
            var id = stream.ReadInt32();
            var isWaterLevelCasting = stream.ReadBoolean();
            var isZoneServerCasting = stream.ReadBoolean();
            var isTextInfo = stream.ReadBoolean();

            _log.Debug("CSWorldRayCastingPacket, x: {0}, y: {1}, z: {2}, x2: {3}, y2: {4}, z2: {5}, id: {6}", x, y, z, x2, y2, z2, id);
        }
    }
}
