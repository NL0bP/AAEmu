using System;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Skills.Effects.Enums;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

public class EquipmentAwakening : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.EquipmentAwakening;
    private const int Offset = 13;
    private const int OffsetMax = Offset + 5;
    private int CurrentLevel = 0;
    private readonly double successChance = 10.0; // Base success chance
    private readonly double breakChance = 0.0;
    private double mappingFailBonus = 0.0; // Additional chance from failures

    public override void Execute(BaseUnit caster,
        SkillCaster casterObj,
        BaseUnit target,
        SkillCastTarget targetObj,
        CastAction castObj,
        Skill skill,
        SkillObject skillObject,
        DateTime time,
        int mappingGroupId,
        int value2,
        int value3,
        int value4,
        int value5,
        int value6,
        int value7)
    {
        // Validate caster
        if (caster is not Character character)
        {
            Logger.Debug("Invalid caster type. Expected Character.");
            return;
        }

        Logger.Debug($"Special effects: EquipmentAwakening mappingGroupId {mappingGroupId}, value2 {value2}, value3 {value3}, value4 {value4}, value5 {value5}, value6 {value6}, value7 {value7}");

        // value1 = mappingGroupId 9
        // value2 = breakChance 20 ?
        // value4 = down grade  -2 

        // Validate target type
        if (targetObj is not SkillCastItemTarget itemTarget)
        {
            Logger.Debug("Invalid target type. Expected SkillCastItemTarget.");
            return;
        }

        // Validate caster type
        if (casterObj is not SkillItem skillItem)
        {
            Logger.Debug("Invalid caster type. Expected SkillItem.");
            return;
        }

        // Get the target item
        var sourceItem = character.Inventory.Bag.GetItemByItemId(itemTarget.Id);
        if (sourceItem is null)
        {
            Logger.Debug($"Target item with ID {itemTarget.Id} not found in inventory.");
            return;
        }

        // Validate target item type
        if (sourceItem is not EquipItem equipItem)
        {
            Logger.Debug("Target item is not an EquipItem.");
            return;
        }

        if (!(equipItem is { Template: EquipItemTemplate equipItemTemplate }))
        {
            Logger.Debug($"Attempting to upgrade a non-equipment item. Item={equipItem.Id}");
            return;
        }

        CurrentLevel = sourceItem.TemperMagical;
        mappingFailBonus = sourceItem.MappingFailBonus;
        var result = UseScroll(mappingGroupId);
        sourceItem.MappingFailBonus += (byte)mappingFailBonus;

        if (result > GradeTamperingResult.Success)
        {
            // Send packets to the client
            character.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.ScaleCap, [new ItemBuyback(sourceItem)], []));
            Logger.Debug("Failure.");
            character.SendPacket(new SCItemChangeMappingResultPacket(sourceItem, sourceItem, (uint)mappingGroupId, (byte)result));
        }
        else
        {
            // TODO создать новый предмет и скопируем эффекты
            // // TODO create a new item and copy the effects
            var targetItemId = ItemGameData.Instance.GetMappingItem(mappingGroupId, sourceItem.Grade, (int)sourceItem.TemplateId);
            if (targetItemId == 0)
            {
                Logger.Error($"EquipmentAwakening executed by {character.Name}. An improved version of the item was not found for the item sourceItem={sourceItem.Id}:{sourceItem.TemplateId}:{sourceItem.Grade}!");
                return;
            }
            var targetItem = ItemManager.Instance.Create((uint)targetItemId, 1, sourceItem.Grade);
            ItemGameData.CopyAllAttributes(sourceItem, targetItem);

            targetItem.Grade -= (byte)value4;
            character.Inventory.Bag.AddOrMoveExistingItem(ItemTaskType.Invalid, targetItem);

            // Send packets to the client
            character.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.EquipmentAwakening,
            [
                new ItemRemove(sourceItem),
                new ItemAdd(targetItem)
            ], []));

            character.SendPacket(new SCItemChangeMappingResultPacket(sourceItem, targetItem, (uint)mappingGroupId, (byte)result));
            character.Achievements?.TrackHiramGrade(targetItem.TemplateId, targetItem.Grade);
            Logger.Debug("Success!");
        }

        // Log the action
        Logger.Debug($"EquipmentAwakening executed by {character.Name} on item {sourceItem.Id} with skill item {skillItem.ItemTemplateId}");
    }

    private GradeTamperingResult UseScroll(int mappingGroupId)
    {
        var icmg = ItemGameData.Instance.GetItemChangeMappingGroup(mappingGroupId);

        // Calculate total chance, not exceeding 100%
        var totalChance = icmg.Success / 100.0 + mappingFailBonus;
        if (totalChance > 100.0)
        {
            totalChance = 100.0;
        }

        // Generate a random number between 0 and 100
        var random = new Random();
        var roll = Rand.NextDouble() * 100;
        var breakRoll = Rand.NextDouble() * 100;

        // Determine success or failure
        if (roll <= totalChance)
        {
            Logger.Debug($"Success! (Roll: {roll:F2} <= Total Chance: {totalChance:F2}%)");
            mappingFailBonus = 0.0; // Reset the bonus on success
            // Display current MappingFailBonus
            Logger.Debug($"Current MappingFailBonus: {mappingFailBonus:F2}%\n");

            return GradeTamperingResult.Success;
        }

        if (breakRoll < icmg.Disable / 10000.0)
        {
            Logger.Debug($"Break. (Break Roll: {breakRoll:F2} <= Break Chance: {breakChance:F2}%)");
            return GradeTamperingResult.Break;
        }

        Logger.Debug($"Failure. (Roll: {roll:F2} > Total Chance: {totalChance:F2}%)");
        mappingFailBonus = icmg.FailBonus / 100.0; // Increase bonus on failure

        // Display current MappingFailBonus
        Logger.Debug($"Current MappingFailBonus: {mappingFailBonus:F2}%\n");

        return GradeTamperingResult.Fail;
    }
}
