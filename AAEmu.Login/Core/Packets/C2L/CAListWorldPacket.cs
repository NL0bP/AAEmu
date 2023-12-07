﻿using AAEmu.Commons.Network;
using AAEmu.Login.Core.Controllers;
using AAEmu.Login.Core.Network.Login;

namespace AAEmu.Login.Core.Packets.C2L
{
    public class CAListWorldPacket : LoginPacket
    {
        public CAListWorldPacket() : base(0x07) // в версии 0.5.1.35870
        {
        }

        public override void Read(PacketStream stream)
        {
            var flag = stream.ReadUInt64();

            GameController.Instance.RequestWorldList(Connection);
        }
    }
}
