using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActSupplySkill : QuestActTemplate
    {
        public uint SkillId { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActSupplySkill QuestId {0}, SkillId {1}, objective {2}",
                quest.TemplateId, SkillId, objective);

            return false;
        }
    }
}
