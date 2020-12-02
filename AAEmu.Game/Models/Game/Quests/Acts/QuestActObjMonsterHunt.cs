using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjMonsterHunt : QuestActTemplate
    {
        public uint NpcId { get; set; }
        public int Count { get; set; }
        public bool UseAlias { get; set; }
        public uint QuestActObjAliasId { get; set; }
        //public uint HighlightDoodadId { get; set; } // отсутствует в 0.5.101.406
        //public int HighlightDoodadPhase { get; set; } // отсутствует в 0.5.101.406

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Debug("QuestActObjMonsterHunt: NpcId {0}, Count {1}, UseAlias {2}, QuestActObjAliasId {3}," +
                       //" HighlightDoodadId {4}, HighlightDoodadPhase {5}," +
                       " quest {4}, objective {5}",
                NpcId, Count, UseAlias, QuestActObjAliasId,
                //HighlightDoodadId, HighlightDoodadPhase,
                quest.TemplateId, objective);

            return objective >= Count;
        }
    }
}
