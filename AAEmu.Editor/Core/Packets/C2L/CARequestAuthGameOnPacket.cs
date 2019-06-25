﻿using AAEmu.Commons.Network;
using AAEmu.Editor.Core.Network.Editor;

namespace AAEmu.Editor.Core.Packets.C2L
{
    public class CARequestAuthGameOnPacket : EditorPacket
    {
        public CARequestAuthGameOnPacket() : base(0x03)
        {
        }

        public override void Read(PacketStream stream)
        {
            var pFrom = stream.ReadUInt32();
            var pTo = stream.ReadUInt32();
            var dev = stream.ReadBoolean();
            var mac = stream.ReadBytes(8);
            var param = stream.ReadString(); // or length 1023
            var si = stream.ReadString(); // or length 15
        }
    }
}
