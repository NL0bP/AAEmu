using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Quests.Acts;

public class QuestActObjLaborPower(QuestComponentTemplate parentComponent) : QuestActTemplate(parentComponent)
{
    public override bool CountsAsAnObjective => true;
    public int ActabilityGroupId { get; set; }
    public bool UseAlias { get; set; }
    public uint QuestActObjAliasId { get; set; }

    public override bool RunAct(Quest quest, QuestAct questAct, int currentObjectiveCount)
    {
        Logger.Debug(
            $"{QuestActTemplateName}({DetailId}).RunAct: Quest: {quest.TemplateId}, Owner {quest.Owner.Name} ({quest.Owner.Id}), " +
            $"ActabilityGroupId {ActabilityGroupId}, Count {currentObjectiveCount}/{Count}");
        return currentObjectiveCount >= Count;
    }

    public override void InitializeAction(Quest quest, QuestAct questAct)
    {
        base.InitializeAction(quest, questAct);
        quest.Owner.Events.OnLaborPowerChanged += questAct.OnLaborPowerChanged;
    }

    public override void FinalizeAction(Quest quest, QuestAct questAct)
    {
        quest.Owner.Events.OnLaborPowerChanged -= questAct.OnLaborPowerChanged;
        base.FinalizeAction(quest, questAct);
    }

    public override void OnLaborPowerChanged(QuestAct questAct, object sender, OnLaborPowerChangedArgs args)
    {
        if (args.AmountSpent <= 0)
        {
            return;
        }

        if (ActabilityGroupId > 0 && args.ActabilityGroupId != ActabilityGroupId)
        {
            return;
        }

        Logger.Debug(
            $"{QuestActTemplateName}({DetailId}).OnLaborPowerChanged: Quest: {questAct.QuestComponent.Parent.Parent.TemplateId}, " +
            $"Owner {questAct.QuestComponent.Parent.Parent.Owner.Name} ({questAct.QuestComponent.Parent.Parent.Owner.Id}), " +
            $"ActabilityGroupId {ActabilityGroupId}, AmountSpent {args.AmountSpent}");
        AddObjective(questAct, args.AmountSpent);
    }
}
