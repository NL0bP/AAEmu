﻿using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCRecoverableExpPacket : GamePacket
    {
        private readonly uint _objId;
        private readonly int _recoverableExp;
        private readonly int _reason;
        
        public SCRecoverableExpPacket(uint objId, int recoverableExp, int reason) : base(SCOffsets.SCRecoverableExpPacket, 1)
        {
            _objId = objId;
            _recoverableExp = recoverableExp;
            _reason = reason;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.WriteBc(_objId);
            stream.Write(_recoverableExp);
            stream.Write(_reason);
            return stream;
        }
    }
}
