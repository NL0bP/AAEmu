using AAEmu.Commons.Network;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.L2C
{
    public class ACChallenge2Packet : LoginPacket
    {
        public ACChallenge2Packet() : base(0x04) // в версии 0.5.1.35870
        {

        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(5000); // round
            stream.Write("xnDekI2enmWuAvwL"); // salt; length 16?
            for (var i = 0; i < 8; i++)
                stream.Write((uint)0); // ch

            return stream;
        }
    }
}
