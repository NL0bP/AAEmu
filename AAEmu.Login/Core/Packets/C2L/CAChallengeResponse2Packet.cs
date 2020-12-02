using AAEmu.Commons.Network;
using AAEmu.Login.Core.Network.Login;
using AAEmu.Login.Core.Packets.L2C;

namespace AAEmu.Login.Core.Packets.C2L
{
    public class CAChallengeResponse2Packet : LoginPacket
    {
        public CAChallengeResponse2Packet() : base(0x04) // в версии 0.5.1.101.406
        {
        }

        public override void Read(PacketStream stream)
        {
            for (var i = 0; i < 8; i++)
                stream.ReadUInt32(); // hc

            Connection.SendPacket(new ACLoginDeniedPacket(2));
        }
    }
}
