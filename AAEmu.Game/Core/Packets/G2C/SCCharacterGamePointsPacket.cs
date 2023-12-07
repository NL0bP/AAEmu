using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCCharacterGamePointsPacket : GamePacket
    {
        private readonly Character _character;

        public SCCharacterGamePointsPacket(Character character) : base(SCOffsets.SCCharacterGamePointsPacket, 1)
        {
            _character = character;
        }

        public override PacketStream Write(PacketStream stream)
        {
            for (var i = 0; i < 5; i++)
                stream.Write(_character.HonorPoint);    // uint honor

            return stream;
        }
    }
}
