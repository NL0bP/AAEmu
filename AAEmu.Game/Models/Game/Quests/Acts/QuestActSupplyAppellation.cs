using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActSupplyAppellation : QuestActTemplate
    {
        public uint AppellationId { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActSupplyAppellation QuestId {0}, AppellationId {1}, objective {2}",
                quest.TemplateId, AppellationId, objective);
            
            character.Appellations.Add(AppellationId);
            return true;
        }
    }
}
