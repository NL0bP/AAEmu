using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjCompleteQuest : QuestActTemplate
    {
        public uint QuestId { get; set; }
        public bool AcceptWith { get; set; }
        public bool UseAlias { get; set; }
        public uint QuestActObjAliasId { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Debug("QuestActObjCompleteQuest QuestId {0}, AcceptWith {1}, UseAlias {2}, QuestActObjAliasId {3}",
                QuestId, AcceptWith, UseAlias, QuestActObjAliasId);

            return character.Quests.IsQuestComplete(QuestId) == AcceptWith;
        }
    }
}
