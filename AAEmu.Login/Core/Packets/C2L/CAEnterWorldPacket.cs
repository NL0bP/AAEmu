﻿using AAEmu.Commons.Network;
using AAEmu.Login.Core.Controllers;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.C2L
{
    public class CAEnterWorldPacket : LoginPacket
    {
        public CAEnterWorldPacket() : base(0x09) // в версии 0.5.1.101.406
        {
        }

        public override void Read(PacketStream stream)
        {
            var flag = stream.ReadUInt64();
            var gsId = stream.ReadByte();

            GameController.Instance.RequestEnterWorld(Connection, gsId);
        }
    }
}
