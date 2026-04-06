using System;

using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

public class ChangeSkillActiveType : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.ChangeSkillActiveType;

    public override void Execute(BaseUnit caster,
        SkillCaster casterObj,
        BaseUnit target,
        SkillCastTarget targetObj,
        CastAction castObj,
        Skill skill,
        SkillObject skillObject,
        DateTime time,
        int value1,
        int value2,
        int value3,
        int value4, int value5, int value6, int value7)
    {
        if (caster is not Character character)
            return;

        // value1 = skillId to unlock (e.g. 33599 = /Sextant emote)
        // value2 = activeType variant (1=all, 2=female, 3=male)
        var skillId = (uint)value1;
        var activeType = (byte)(value2 > 0 ? value2 : 1);

        Logger.Debug("ChangeSkillActiveType: caster={0} teachingSkill={1} unlocking skillId={2} activeType={3}",
            character.Name, skill.Id, skillId, activeType);

        character.SkillActiveTypes.Unlock(skillId, activeType);
    }
}
