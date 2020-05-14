using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjItemGather : QuestActTemplate // Сбор предметов
    {
        public uint ItemId { get; set; }
        public int Count { get; set; }
        public uint HighlightDoodadId { get; set; }
        public int HighlightDoodadPhase { get; set; }
        public bool UseAlias { get; set; }
        public uint QuestActObjAliasId { get; set; }
        public bool Cleanup { get; set; }
        public bool DropWhenDestroy { get; set; }
        public bool DestroyWhenDrop { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Debug("QuestActObjItemGather QuestId {0}, ItemId {1}, Count {2}, HighlightDoodadId {3}," +
                       " HighlightDoodadPhase {4}, UseAlias {5}, QuestActObjAliasId {6}, Cleanup {7}, " +
                       "DropWhenDestroy {8}, DestroyWhenDrop {9}, objective {10}",
                quest.TemplateId, ItemId, Count, HighlightDoodadId, HighlightDoodadPhase, UseAlias, QuestActObjAliasId,
                Cleanup, DropWhenDestroy, DestroyWhenDrop, objective);

            return objective >= Count;
        }
    }
}
