using AAEmu.Commons.Network;

namespace AAEmu.Game.Models.Game.Items.Actions
{
    public class ItemAdd : ItemTask
    {
        private readonly Item _item;

        public ItemAdd(Item item)
        {
            _type = 5;
            _item = item;
        }

        public override PacketStream Write(PacketStream stream)
        {
            // ------------------24----------------------
            base.Write(stream);

            stream.Write((byte)_item.SlotType);
            stream.Write((byte)_item.Slot);

            stream.Write(_item.TemplateId);
            stream.Write(_item.Id);
            stream.Write((byte)_item.Grade);
            stream.Write((byte)_item.ItemFlags); // bounded
            stream.Write(_item.Count); // stack
            
            var details = new PacketStream();
            details.Write(_item.DetailType); // DetailType => 5
            _item.WriteDetails(details);
            stream.Write((short)128);        // length details?
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
