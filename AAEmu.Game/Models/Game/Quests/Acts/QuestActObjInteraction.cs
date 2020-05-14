using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActObjInteraction : QuestActTemplate
    {
        public WorldInteractionType WorldInteractionId { get; set; }
        public int Count { get; set; }
        public uint DoodadId { get; set; }
        public bool UseAlias { get; set; }
        public bool TeamShare { get; set; }
        public uint HighlightDoodadId { get; set; }
        public int HighlightDoodadPhase { get; set; }
        public uint QuestActObjAliasId { get; set; }
        public uint Phase { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Debug("QuestActObjInteraction QuestId {0}, WorldInteractionId {1}, Count {2}, DoodadId {3}" +
                       "UseAlias {4}, TeamShare {5}, HighlightDoodadId {6}, HighlightDoodadPhase {7}, " +
                       " QuestActObjAliasId {8}, Phase {9}, objective {10}",
                quest.TemplateId, WorldInteractionId, Count, DoodadId, UseAlias, TeamShare, HighlightDoodadId,
                HighlightDoodadPhase, QuestActObjAliasId, Phase, objective);

            return objective >= Count;
        }
    }
}
