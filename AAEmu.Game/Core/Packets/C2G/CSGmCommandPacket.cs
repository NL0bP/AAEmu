using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSGmCommandPacket : GamePacket
    {
        public CSGmCommandPacket() : base(CSOffsets.CSGmCommandPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var unitId = stream.ReadBc(); // unitId
            var cmd = stream.ReadByte();
            var params1 = stream.ReadString();
            var params2 = stream.ReadString();
            _log.Debug("CSGmCommandPacket, unitId: {0}, cmd {1}, params1 {2}, params2 {3}", unitId, cmd, params1, params2);
            
            Connection.ActiveChar.BroadcastPacket(new SCGmCommandPacket(unitId, cmd, params1, params2), true);
        }
    }
}
