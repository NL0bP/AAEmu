using System;
using AAEmu.Commons.Network;

namespace AAEmu.Game.Models.Game.Items.Templates
{
    public class BodyPartTemplate : ItemTemplate
    {
        public override Type ClassType => typeof(BodyPart);

        public uint ModelId { get; set; }
        public bool NpcOnly { get; set; }
        //public bool BeautyShopOnly { get; set; } // отсутствует в 0.5.101.406
    }
}
