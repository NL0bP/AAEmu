﻿using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCChatSpamDelayPacket : GamePacket
    {
        public SCChatSpamDelayPacket() : base(SCOffsets.SCChatSpamDelayPacket, 1)
        {
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write((float)0); // yellDelay
            stream.Write(0);        // maxSpamYell
            stream.Write((float)0); // spamYellDelay
            stream.Write(0);        // maxChatLen
            return stream;
        }
    }
}
