using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Quests.Templates;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActSupplyExp : QuestActTemplate
    {
        public int Exp { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActSupplyExp QuestId {0}, Exp {1}, objective {2}",
                quest.TemplateId, Exp, objective);

            character.AddExp(Exp, true);
            return true;
        }
    }
}
