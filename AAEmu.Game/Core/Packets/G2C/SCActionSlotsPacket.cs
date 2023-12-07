using System;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCActionSlotsPacket : GamePacket
    {
        private readonly ActionSlot[] _slots;

        public SCActionSlotsPacket(ActionSlot[] slots) : base(SCOffsets.SCActionSlotsPacket, 1)
        {
            _slots = slots;
        }

        public override PacketStream Write(PacketStream stream)
        {
            foreach (var slot in _slots) // TODO max 85
            {
                stream.Write((byte)slot.Type);
                //if (slot.Type != ActionSlotType.None)
                //    stream.Write(slot.ActionId);

                switch (slot.Type)
                {
                    case ActionSlotType.Item:
                    case ActionSlotType.Skill:
                    case ActionSlotType.Unk5:
                        stream.Write(slot.ActionId); // UInt32 type
                        break;
                    case ActionSlotType.Unk4:
                        stream.Write((long)slot.ActionId); // UInt64 itemId
                        break;
                    case ActionSlotType.None:
                    case ActionSlotType.Unk3:
                    default:
                        break;
                }
            }

            return stream;
        }
    }
}
