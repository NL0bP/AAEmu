using System;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Buffs.Triggers;

internal class StartedBuffTrigger : BuffTrigger
{
    public override void Execute(object sender, EventArgs eventArgs)
    {
        Logger.Trace("Buff[{0}] {1} executed. Applying {2}[{3}]!", _buff?.Template?.BuffId, this.GetType()?.Name, Template?.Effect?.GetType().Name, Template?.Effect?.Id);
        TryApply(null, null);
    }

    public StartedBuffTrigger(Buff owner, BuffTriggerTemplate template) : base(owner, template)
    {

    }
}
