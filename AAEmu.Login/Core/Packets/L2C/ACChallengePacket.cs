﻿using AAEmu.Commons.Network;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.L2C
{
    public class ACChallengePacket : LoginPacket
    {
        public ACChallengePacket() : base(0x02) // в версии 0.5.1.101.406
        {
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write((uint) 0); // salt
            for (var i = 0; i < 4; i++)
                stream.Write((uint) 0); // ch

            return stream;
        }
    }
}
