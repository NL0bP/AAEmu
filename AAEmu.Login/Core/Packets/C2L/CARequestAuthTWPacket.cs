using AAEmu.Commons.Network;
using AAEmu.Login.Core.Controllers;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.C2L
{
    public class CARequestAuthTWPacket : LoginPacket
    {
        public CARequestAuthTWPacket() : base(0x11)
        {
        }

        public override void Read(PacketStream stream)
        {
            var pFrom = stream.ReadUInt32();
            var pTo = stream.ReadUInt32();
            var svc = stream.ReadByte();
            var dev = stream.ReadBoolean();
            var account = stream.ReadString();
            var mac = stream.ReadBytes();

            LoginController.Login(Connection, account);
        }
    }
}
