using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCRankerInformationPacket : GamePacket
    {
        private readonly uint _characterId;
        //private readonly byte _race;
        //private readonly byte _ability1;
        //private readonly byte _ability2;
        //private readonly byte _ability3;
        //private readonly int _health;
        //private readonly int _mana;
        //private readonly byte _lvl;
        //private readonly uint _gearScore;
        //private readonly uint _leadership;
        //private readonly uint _pvpHonorPoint;
        //private readonly uint _pvpKillCount;
        //private readonly string _expeditionName;
        private readonly Character _character;

        public SCRankerInformationPacket(byte worldId, uint type)
            : base(SCOffsets.SCRankerInformationPacket, 5)
        {
            _characterId = type;
            //_character = Connection.ActiveChar;
        }

        public override PacketStream Write(PacketStream stream)
        {
            return stream;
            stream.Write(_characterId); // type d
            stream.Write(_character.RaceGender); // race b
            stream.Write((byte)_character.Ability1); // ability b
            stream.Write((byte)_character.Ability2); // ability b
            stream.Write((byte)_character.Ability3); // ability b
            stream.Write(_character.Hp); // health i
            stream.Write(_character.Mp); // mana i
            stream.Write(_character.Level); // lvl b
            stream.Write(0); // gearScore d
            stream.Write(0); // leadership d
            stream.Write(0); // pvpHonorPoint d
            stream.Write(0); // pvpKillCount d
            stream.Write(_character.Expedition.Name); // expeditionName s

            return stream;
        }
    }
}
