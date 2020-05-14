using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjZoneQuestComplete : QuestActTemplate
    {
        public uint ZoneId { get; set; }
        public int Count { get; set; }
        public bool UseAlias { get; set; }
        public uint QuestActObjAliasId { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActObjZoneQuestComplete QuestId {0}, ZoneId {1}, Count {2}, UseAlias {3}, QuestActObjAliasId {4}, objective {5}",
                quest.TemplateId, ZoneId, Count, UseAlias, QuestActObjAliasId, objective);

            return character.Quests.IsQuestComplete(quest.TemplateId) == objective >= Count;
        }
    }
}
