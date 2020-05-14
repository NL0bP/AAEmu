using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Quests.Templates;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActConAutoComplete : QuestActTemplate
    {
        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Debug("QuestActConAutoComplete QuestId {0}, objective {1}", quest.TemplateId, objective);

            return character.Quests.IsQuestComplete(quest.TemplateId);
        }
    }
}
