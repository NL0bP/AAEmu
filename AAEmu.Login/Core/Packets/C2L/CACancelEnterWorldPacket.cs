using AAEmu.Commons.Network;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.C2L
{
    public class CACancelEnterWorldPacket : LoginPacket
    {
        public CACancelEnterWorldPacket() : base(0x0a) // в версии 0.5.1.101.406
        {
        }

        public override void Read(PacketStream stream)
        {
            var wId = stream.ReadByte(); // diw -> world id
        }
    }
}
