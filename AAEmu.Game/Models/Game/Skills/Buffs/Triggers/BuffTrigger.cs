using System;

using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Units;

using NLog;

namespace AAEmu.Game.Models.Game.Skills.Buffs.Triggers;

public class BuffTrigger
{
    protected static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    protected Buff _buff;
    protected readonly BaseUnit _owner;
    public BuffTriggerTemplate Template { get; set; }

    protected virtual BaseUnit ResolveAgent(uint agentId, Unit eventSource, BaseUnit eventTarget)
    {
        return agentId switch
        {
            0 => _buff?.Owner,
            1 => eventSource ?? _buff?.Owner,
            2 => eventTarget ?? _buff?.Owner,
            3 => _buff?.Caster ?? _buff?.Owner,
            _ => _buff?.Owner
        };
    }

    protected bool TryApply(Unit eventSource, BaseUnit eventTarget, int amount = 0)
    {
        var source = ResolveAgent(Template.SourceAgentId, eventSource, eventTarget) ?? _buff?.Owner;
        var target = ResolveAgent(Template.TargetAgentId, eventSource, eventTarget) ?? _buff?.Owner;

        if (Template.UseOriginalSource && _buff?.Caster != null)
            source = _buff.Caster;

        if (Template.EffectOnSource)
            target = source;

        if (source == null || target == null)
            return false;

        if (Template.OwnerBuffTagId != 0 && !_buff.Owner.Buffs.CheckBuffTag(Template.OwnerBuffTagId))
            return false;
        if (Template.OwnerNoBuffTagId != 0 && _buff.Owner.Buffs.CheckBuffTag(Template.OwnerNoBuffTagId))
            return false;

        if (Template.SourceBuffTagId != 0 && !source.Buffs.CheckBuffTag(Template.SourceBuffTagId))
            return false;
        if (Template.SourceNoBuffTagId != 0 && source.Buffs.CheckBuffTag(Template.SourceNoBuffTagId))
            return false;

        if (Template.TargetBuffTagId != 0 && !target.Buffs.CheckBuffTag(Template.TargetBuffTagId))
            return false;
        if (Template.TargetNoBuffTagId != 0 && target.Buffs.CheckBuffTag(Template.TargetNoBuffTagId))
            return false;

        SkillCaster casterObj;
        if (source is Doodad sourceDoodad)
            casterObj = new SkillCasterDoodad(sourceDoodad.ObjId);
        else
            casterObj = new SkillCasterUnit(source.ObjId);

        SkillCastTarget targetObj;
        if (target is Doodad targetDoodad)
            targetObj = new SkillCastDoodadTarget { ObjId = targetDoodad.ObjId };
        else
            targetObj = new SkillCastUnitTarget(target.ObjId);

        Template.Effect.Apply(source, casterObj, target, targetObj, new CastBuff(_buff),
            new EffectSource(_buff?.Template) { Amount = amount, IsTrigger = true },
            null, DateTime.UtcNow);
        return true;
    }

    public virtual void Execute(object sender, EventArgs eventArgs)
    {
        Logger.Trace("Buff[{0}] {1} executed. Applying {2}[{3}]!", _buff?.Template?.BuffId, GetType().Name, Template.Effect.GetType().Name, Template.Effect.Id);
        TryApply(null, null);
    }

    public BuffTrigger(Buff buff, BuffTriggerTemplate template)
    {
        _buff = buff;
        _owner = _buff.Owner;
        Template = template;
    }
}
