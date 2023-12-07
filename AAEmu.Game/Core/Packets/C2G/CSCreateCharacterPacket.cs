using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSCreateCharacterPacket : GamePacket
    {
        public CSCreateCharacterPacket() : base(CSOffsets.CSCreateCharacterPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var name = stream.ReadString();
            var race = stream.ReadByte();
            var gender = stream.ReadByte();
            var body = new uint[7];
            for (var i = 0; i < 28; i++)
            {
                if (i is < 19 or > 25)
                {
                    stream.ReadUInt32();
                }
                else
                {
                    body[i - 19] = stream.ReadUInt32();
                }
            }

            var customModel = new UnitCustomModelParams();
            customModel.Read(stream);

            var ability1 = stream.ReadByte();
            var ability2 = stream.ReadByte();
            var ability3 = stream.ReadByte();
            var level = stream.ReadByte();

            CharacterManager.Instance.Create(Connection, name, race, gender, body, customModel, ability1);
        }
    }
}
