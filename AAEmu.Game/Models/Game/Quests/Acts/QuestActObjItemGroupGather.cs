using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjItemGroupGather : QuestActTemplate
    {
        public uint ItemGroupId { get; set; }
        public int Count { get; set; }
        public bool Cleanup { get; set; }
        public uint HighlightDoodadId { get; set; }
        public int HighlightDoodadPhase { get; set;  }
        public bool UseAlias { get; set; }
        public uint QuestActObjAliasId { get; set; }
        public bool DropWhenDestroy { get; set; }
        public bool DestroyWhenDrop { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Debug("QuestActObjItemGroupGather QuestId {0}, ItemGroupId {1}, Count {2}, Cleanup {3}, HighlightDoodadId {4}," +
                       " HighlightDoodadPhase {5}, UseAlias {6}, QuestActObjAliasId {7}, DropWhenDestroy {8}, DestroyWhenDrop {9}," +
                       " objective {10}",
                quest.TemplateId, ItemGroupId, Count, Cleanup, HighlightDoodadId, HighlightDoodadPhase, UseAlias, QuestActObjAliasId,
                DropWhenDestroy, DestroyWhenDrop, objective);

            return objective >= Count;
        }
    }
}
