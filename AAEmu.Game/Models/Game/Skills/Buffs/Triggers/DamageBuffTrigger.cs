using System;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Buffs.Triggers;

public class DamageBuffTrigger : BuffTrigger
{
    public override void Execute(object sender, EventArgs eventArgs)
    {
        var args = eventArgs as OnDamageArgs;

        Logger.Trace("Buff[{0}] {1} executed. Applying {2}[{3}]!", _buff.Template.BuffId, this.GetType().Name, Template.Effect.GetType().Name, Template.Effect.Id);
        TryApply(args?.Attacker, args?.Target, args?.Amount ?? 0);
    }

    public DamageBuffTrigger(Buff owner, BuffTriggerTemplate template) : base(owner, template)
    {

    }
}
