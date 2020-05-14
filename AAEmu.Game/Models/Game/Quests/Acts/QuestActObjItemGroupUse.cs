using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjItemGroupUse : QuestActTemplate
    {
        public uint ItemGroupId { get; set; }
        public int Count { get; set; }
        public uint HighlightDoodadId { get; set; }
        public int HighlightDoodadPhase { get; set; }
        public bool UseAlias { get; set; }
        public uint QuestActObjAliasId { get; set; }
        public bool DropWhenDestroy { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Debug("QuestActObjItemGroupUse QuestId {0}, ItemGroupId {1}, Count {2}, HighlightDoodadId {3}, HighlightDoodadPhase {4}," +
                       " UseAlias {5}, QuestActObjAliasId {6}, objective {7}",
                quest.TemplateId, ItemGroupId, Count, HighlightDoodadId, HighlightDoodadPhase, UseAlias, QuestActObjAliasId, objective);

            return objective >= Count;
        }
    }
}
