using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Quests.Templates;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActSupplyLp : QuestActTemplate
    {
        public int LaborPower { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActSupplyLp QuestId {0}, LaborPower {1}, objective {2}",
                quest.TemplateId, LaborPower, objective);
            
            character.ChangeLabor((short)LaborPower, 0);

            return true;
        }
    }
}
