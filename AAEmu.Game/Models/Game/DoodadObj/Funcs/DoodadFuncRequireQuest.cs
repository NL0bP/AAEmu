using System;

using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs
{
    public class DoodadFuncRequireQuest : DoodadFuncTemplate
    {
        public uint WorldInteractionId { get; set; }
        public uint QuestId { get; set; }

        public override void Use(Unit caster, Doodad owner, uint skillId)
        {
            _log.Debug("DoodadFuncRequireQuest: skillId {0}, WorldInteractionId {1}, QuestId {2}", skillId, WorldInteractionId, QuestId);
            _log.Debug("InteractionEffect, {0}", (WorldInteractionType)WorldInteractionId);
            var classType = Type.GetType("AAEmu.Game.Models.Game.World.Interactions." + (WorldInteractionType)WorldInteractionId);
            if (classType == null)
            {
                _log.Error("InteractionEffect, Unknown world interaction: {0}", (WorldInteractionType)WorldInteractionId);
                return;
            }
            _log.Debug("InteractionEffect, Action: {0}", classType); // TODO help to debug...

            var action = (IWorldInteraction)Activator.CreateInstance(classType);
            var casterType = SkillCaster.GetByType((SkillCasterType)SkillCastTargetType.Unit);
            var targetType = SkillCastTarget.GetByType((SkillCastTargetType)SkillCastTargetType.Item);
            action.Execute(caster, casterType, owner, targetType, skillId, QuestId);
        }
    }
}
