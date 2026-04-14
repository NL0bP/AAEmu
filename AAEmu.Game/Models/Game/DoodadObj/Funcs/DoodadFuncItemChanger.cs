using System;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Skills;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncItemChanger : DoodadPhaseFuncTemplate
{
    // doodad_phase_funcs
    public int ItemCount { get; set; }
    public int ItemId { get; set; }
    public int NextPhase { get; set; }
    public int SkillId { get; set; }

    public override bool Use(BaseUnit caster, Doodad owner)
    {
        if (caster is not Character character)
        {
            Logger.Trace($"DoodadFuncItemChanger: Id={Id}, ItemCount={ItemCount}, ItemId={ItemId}, NextPhase={NextPhase}, SkillId={SkillId}");
            return false;
        }

        Logger.Debug($"DoodadFuncItemChanger: Id={Id}, ItemCount={ItemCount}, ItemId={ItemId}, NextPhase={NextPhase}, SkillId={SkillId}");

        if (SkillId > 0)
        {
            var skillTemplate = SkillManager.Instance.GetSkillTemplate((uint)SkillId);
            if (skillTemplate == null)
            {
                return false;
            }
            var useSkill = new Skill(skillTemplate);
            TaskManager.Instance.Schedule(new UseSkillTask(useSkill, caster, new SkillCasterUnit(caster.ObjId), owner, new SkillCastDoodadTarget { ObjId = owner.ObjId }, null), TimeSpan.FromMilliseconds(0));
        }

        // Consuming the item
        character.Inventory.Bag.ConsumeItem(ItemTaskType.DoodadItemChanger, (uint)ItemId, ItemCount, null);

        owner.ToNextPhase = SkillId > 0;
        owner.OverridePhase = NextPhase;
        return true; // we will continue to execute
    }
}
