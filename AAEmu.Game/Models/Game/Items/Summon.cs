﻿using AAEmu.Commons.Network;
using AAEmu.Game.Models.Game.Items.Templates;

namespace AAEmu.Game.Models.Game.Items
{
    public class Summon : Item
    {
        public override byte DetailType => 3;

        public Summon()
        {
        }

        public Summon(ulong id, ItemTemplate template, int count)
            : base(id, template, count)
        {
        }

        public override void ReadDetails(PacketStream stream)
        {
            // ------------------6----------------------
            stream.ReadInt32(); // exp
            stream.ReadByte();
            stream.ReadByte();  // level
        }

        public override void WriteDetails(PacketStream stream)
        {
            // ------------------6----------------------
            stream.Write(0);        // exp
            stream.Write((byte) 0);
            stream.Write((byte) 1); // level
        }
    }
}
