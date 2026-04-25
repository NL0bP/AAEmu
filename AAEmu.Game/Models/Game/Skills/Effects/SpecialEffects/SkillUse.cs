using System;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Skills;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

public class SkillUse : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.SkillUse;

    public override void Execute(BaseUnit caster,
        SkillCaster casterObj,
        BaseUnit target,
        SkillCastTarget targetObj,
        CastAction castObj,
        Skill skill,
        SkillObject skillObject,
        DateTime time,
        int skillId,
        int delay,
        int chance,
        int value4, int value5, int value6, int value7)
    {
        // TODO ...
        if (caster is Character) { Logger.Debug("Special effects: SkillUse skillId {0}, delay {1}, value3 {2}, value4 {3}", skillId, delay, chance, value4); }

        if (Rand.Next(0, 100) > chance && chance != 0)
        {
            ((Unit)caster).ConditionChance = false;
            return;
        }
        else
        {
            ((Unit)caster).ConditionChance = true;
        }

        //target = ((Unit)caster).CurrentTarget;
        var useSkill = new Skill(SkillManager.Instance.GetSkillTemplate((uint)skillId));
        targetObj = CreateTargetForTriggeredSkill(caster, target, useSkill.Template);
        Logger.Trace($"SkillUse trigger: parentSkill={skill?.Id ?? 0}, triggeredSkill={skillId}, caster={caster?.ObjId ?? 0}, casterObjType={casterObj?.Type}, casterObj={casterObj?.ObjId ?? 0}, sourceTarget={target?.ObjId ?? 0}, triggeredTarget={DescribeTarget(targetObj)}, sourceMount={useSkill.Template?.SourceMount ?? false}, targetOffsetDistance={useSkill.Template?.TargetOffsetDistance ?? 0}");
        caster.Buffs.TriggerRemoveOn(Buffs.BuffRemoveOn.UseSkill);//Not sure if it belongs here.

        // Secondary skill triggers from buffs/food should behave like a unit skill cast.
        // Reusing SkillItem here makes achievement/item-use hooks fire on every buff tick.
        var effectiveCasterObj = casterObj is SkillItem ? new SkillCasterUnit(caster.ObjId) : casterObj;
        Logger.Trace($"SkillUse schedule: triggeredSkill={skillId}, delay={delay}, effectiveCasterType={effectiveCasterObj?.Type}, effectiveCaster={effectiveCasterObj?.ObjId ?? 0}, scheduledTarget={DescribeTarget(targetObj)}");
        TaskManager.Instance.Schedule(new UseSkillTask(useSkill, caster, effectiveCasterObj, target, targetObj, skillObject), TimeSpan.FromMilliseconds(delay));
        //useSkill.ApplyEffects(caster, casterObj, target, targetObj, skillObject);
    }

    private static SkillCastTarget CreateTargetForTriggeredSkill(BaseUnit caster, BaseUnit target, SkillTemplate template)
    {
        if (template?.TargetOffsetDistance > 0 && caster?.Transform?.World != null)
        {
            var worldTransform = caster.Transform.World;
            var angle = worldTransform.Rotation.Z + (MathF.PI / 2f) + (template.TargetOffsetAngle * MathF.PI / 180f);
            var (x, y) = MathUtil.AddDistanceToFront(
                template.TargetOffsetDistance,
                worldTransform.Position.X,
                worldTransform.Position.Y,
                angle);

            return new SkillCastPositionTarget
            {
                Type = SkillCastTargetType.Position,
                PosX = x,
                PosY = y,
                PosZ = worldTransform.Position.Z,
                PosRot = worldTransform.Rotation.Z
            };
        }

        return new SkillCastUnitTarget(target?.ObjId ?? 0);
    }

    private static string DescribeTarget(SkillCastTarget target)
    {
        return target switch
        {
            SkillCastPositionTarget position => $"Position(obj1={position.ObjId1}, obj2={position.ObjId2}, obj3={position.ObjId3}, x={position.PosX:F3}, y={position.PosY:F3}, z={position.PosZ:F3}, rot={position.PosRot:F3})",
            SkillCastUnitTarget unit => $"Unit(obj={unit.ObjId})",
            SkillCastDoodadTarget doodad => $"Doodad(obj={doodad.ObjId})",
            null => "null",
            _ => $"{target.Type}(obj={target.ObjId})"
        };
    }
}
