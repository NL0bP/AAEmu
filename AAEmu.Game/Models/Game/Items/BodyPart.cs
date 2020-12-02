using AAEmu.Commons.Network;
using AAEmu.Game.Models.Game.Items.Templates;

namespace AAEmu.Game.Models.Game.Items
{
    public class BodyPart : Item
    {
        public BodyPartTemplate[] Items { get; set; } = new BodyPartTemplate[7];

        public BodyPart()
        {
            for (var i = 0; i < 7; i++)
            {
                Items[i] = new BodyPartTemplate();
            }
        }

        public BodyPart(ulong id, BodyPartTemplate template, int count) : base(id, template, count)
        {
            for (var i = 0; i < 7; i++)
            {
                Items[i] = new BodyPartTemplate();
            }
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(TemplateId);
            return stream;
        }
    }
}
