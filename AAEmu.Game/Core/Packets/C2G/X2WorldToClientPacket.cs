using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class X2ClientToWorldPacket : GamePacket
    {
        public X2ClientToWorldPacket() : base(CSOffsets.X2ClientToWorldPacket, 1)
        {

        }

        public override void Read(PacketStream stream)
        {
            var pFrom = stream.ReadUInt32();
            var pTo = stream.ReadUInt32();
            var accountId = stream.ReadUInt32();
            var cookie = stream.ReadInt32();
            var zoneId = stream.ReadInt32();
            var tb = stream.ReadByte();

            EnterWorldManager.Instance.Login(Connection, accountId, cookie);
        }
    }
}
