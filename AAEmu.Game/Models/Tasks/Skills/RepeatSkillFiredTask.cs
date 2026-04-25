using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Tasks.Skills;

public class RepeatSkillFiredTask : Task
{
    private const byte RepeatFiredFlag = 2;

    private readonly Skill _skill;
    private readonly BaseUnit _caster;
    private readonly SkillCaster _casterCaster;
    private readonly SkillCastTarget _targetCaster;
    private readonly SkillObject _skillObject;
    private readonly short _computedDelay;
    private readonly int _repeatFiredCount;
    private int _executedRepeats;

    public RepeatSkillFiredTask(Skill skill, BaseUnit caster, SkillCaster casterCaster, SkillCastTarget targetCaster, SkillObject skillObject, short computedDelay)
    {
        _skill = skill;
        _caster = caster;
        _casterCaster = casterCaster;
        _targetCaster = targetCaster;
        _skillObject = skillObject;
        _computedDelay = computedDelay;
        _repeatFiredCount = skill.Template.RepeatCount - 1;
    }

    public override void Execute()
    {
        _executedRepeats++;
        _caster.BroadcastPacket(new SCSkillFiredPacket(_skill.Id, _skill.TlId, _casterCaster, _targetCaster, _skill, _skillObject, _caster)
        {
            ComputedDelay = _computedDelay,
            FiredFlag = RepeatFiredFlag
        }, true);

        if (_executedRepeats >= _repeatFiredCount)
        {
            _skill.EndSkill(_caster);
        }
    }
}
