using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCGmCommandPacket : GamePacket
    {
        private readonly uint _unitId;
        private readonly byte _cmd;
        private readonly byte _level;
        private readonly string _params1;
        private readonly string _params2;

        public SCGmCommandPacket(uint unitId, byte cmd, string params1, string params2) : base(SCOffsets.SCGmCommandPacket, 1)
        {
            _unitId = unitId;
            _cmd = cmd;
            _level = 1;
            _params1 = params1;
            _params2 = params2;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_unitId);
            stream.Write(_cmd);
            stream.Write(_level);
            stream.Write(_params2);

            return stream;
        }
    }
}
