﻿using AAEmu.Commons.Network;
using AAEmu.Login.Core.Controllers;
using AAEmu.Login.Core.Network.Login;
using AAEmu.Login.Core.Packets.L2C;

namespace AAEmu.Login.Core.Packets.C2L
{
    public class CARequestAuthPacket : LoginPacket
    {
        public CARequestAuthPacket() : base(0x01) // в версии 0.5.1.35870
        {
        }

        public override void Read(PacketStream stream)
        {
            var pFrom = stream.ReadUInt32();
            var pTo = stream.ReadUInt32();
            var dev = stream.ReadBoolean();
            var account = stream.ReadString();
            var mac = stream.ReadBytes();
            var mac2 = stream.ReadBytes();
            var cpu = stream.ReadUInt64();

            LoginController.Login(Connection, account);
        }
    }
}
