using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Items;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCGradeEnchantResultPacket : GamePacket
    {
        // result :
        //  0 = break, 1 = downgrade, 2 = fail, 3 = success, 4 = great success 
        private readonly byte _result;
        private readonly Item _item;
        private readonly ItemGrade _type1;
        private readonly ItemGrade _type2;

        public SCGradeEnchantResultPacket(byte result, Item item, ItemGrade type1, ItemGrade type2)
            : base(SCOffsets.SCGradeEnchantResultPacket, 1)
        {
            _result = result;
            _item = item;
            _type1 = type1;
            _type2 = type2;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_result);
            stream.Write(_item);
            stream.Write((byte)_type1);
            stream.Write((byte)_type2);

            return stream;
        }
    }
}
