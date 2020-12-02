﻿using AAEmu.Commons.Network;

namespace AAEmu.Game.Models.Game.Items.Actions
{
    public class ItemAddNew : ItemTask
    {
        private readonly Item _item;

        public ItemAddNew(Item item)
        {
            _item = item;
            _type = 15;
        }

        public override PacketStream Write(PacketStream stream)
        {
            base.Write(stream);

            stream.Write((byte)_item.SlotType);
            stream.Write((byte)_item.Slot);

            stream.Write(_item.TemplateId);
            stream.Write(_item.Id);
            stream.Write((byte)_item.Grade);
            stream.Write((byte)_item.ItemFlags);
            stream.Write(_item.Count);

            var details = new PacketStream();
            details.Write(_item.DetailType); // DetailType => 15 нет в 0.5.101406
            _item.WriteDetails(details);
            stream.Write((short)128);
            stream.Write(details, false);
            stream.Write(new byte[128 - details.Count]);
            
            stream.Write(_item.CreateTime);
            stream.Write(_item.LifespanMins);
            stream.Write(_item.MadeUnitId);
            stream.Write(_item.WorldId);

            return stream;
        }
    }
}
