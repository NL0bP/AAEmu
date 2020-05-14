using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActSupplyJuryPoint : QuestActTemplate
    {
        public int Point { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActSupplyJuryPoint QuestId {0}, Point {1}, objective {2}",
                quest.TemplateId, Point, objective);

            return false;
        }
    }
}
