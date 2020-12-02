using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjMonsterGroupHunt : QuestActTemplate
    {
        public uint QuestMonsterGroupId { get; set; }
        public int Count { get; set; }
        public bool UseAlias { get; set; }
        public uint QuestActObjAliasId { get; set; }
        //public uint HighlightDoodadId { get; set; } // отсутствует в 0.5.101.406
        //public int HighlightDoodadPhase { get; set; } // отсутствует в 0.5.101.406

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Trace(
                "QuestActObjMonsterGroupHunt: Score {6} QuestMonsterGroupId {0}," +
                " Count {1}, UseAlias {2}, QuestActObjAliasId {3}," +
                //" HighlightDoodadId {4}," +
                //" HighlightDoodadPhase {5}," +
                " quest {4}, objective {5}",
                QuestMonsterGroupId,
                Count,
                UseAlias,
                QuestActObjAliasId,
                //HighlightDoodadId,
                //HighlightDoodadPhase,
                quest.TemplateId,
                objective,
                quest.Template.Score);
            var tempScore = objective * Count;
            if(tempScore >= quest.Template.Score)
            {
                return true;
            }
            return objective >= Count;
        }
    }
}
