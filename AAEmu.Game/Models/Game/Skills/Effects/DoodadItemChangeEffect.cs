using System;

using AAEmu.Game.Core.Packets;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects;

public class DoodadItemChangeEffect : EffectTemplate
{
    public int Idx { get; set; }

    public override bool OnActionTime => false;

    public override void Apply(BaseUnit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj,
        CastAction castObj, EffectSource source, SkillObject skillObject, DateTime time,
        CompressedGamePackets packetBuilder = null)
    {
        Logger.Debug($"DoodadItemChangeEffect Idx={Idx}");

        /*
         * по idx выбирается id
         * пример 0 -> 1
         * по id выбираем item_count, item_id, next_phase и skill_id
         * по skill_id запускаем пакет skillstarted
         */
    }
}
