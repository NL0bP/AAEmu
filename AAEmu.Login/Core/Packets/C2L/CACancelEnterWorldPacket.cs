using AAEmu.Commons.Network;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.C2L
{
    public class CACancelEnterWorldPacket : LoginPacket
    {
        public CACancelEnterWorldPacket() : base(0x09) // в версии 0.5.1.35870
        {
        }

        public override void Read(PacketStream stream)
        {
            var wId = stream.ReadByte(); // diw -> world id
        }
    }
}
