using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class X2WorldToClientPacket : GamePacket
    {
        private readonly short _reason;
        private readonly uint _token;
        private readonly ushort _port;
        private readonly bool _gm;

        public X2WorldToClientPacket(short reason, bool gm, uint token, ushort port)
            : base(SCOffsets.X2WorldToClientPacket, 1)
        {
            _reason = reason;
            _token = token;
            _port = port;
            _gm = gm;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_reason);
            stream.Write(_gm);
            stream.Write(_token); // Stream Token // cs
            stream.Write(_port); // Stream Port // cp
            stream.Write(Helpers.UnixTimeNow()); // wf

            return stream;
        }
    }
}
