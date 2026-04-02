using System;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Achievement.Enums;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Skills.Effects.Enums;
using AAEmu.Game.Models.Game.Units;

using ItemGradeEnchantingSupport = AAEmu.Game.Models.Game.Items.ItemEnchants.ItemGradeEnchantingSupport;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

public class GradeEnchant : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.GradeEnchant;

    public override void Execute(BaseUnit caster,
        SkillCaster casterObj,
        BaseUnit target,
        SkillCastTarget targetObj,
        CastAction castObj,
        Skill skill,
        SkillObject skillObject,
        DateTime time,
        int value1,
        int value2,
        int itemType,
        int value4, int value5, int value6, int value7)
    {
        // Get Player
        if (caster is not Character character)
        {
            Logger.Debug("Invalid caster type. Expected Character.");
            return;
        }

        Logger.Debug($"Special effects: GradeEnchant value1 {value1}, value2 {value2}, itemType {itemType}, value4 {value4}, value5 {value5}, value6 {value6}, value7 {value7}");

        // Get Regrade Scroll Item
        if (casterObj is not SkillItem scroll)
        {
            Logger.Debug("Invalid caster type. Expected SkillItem.");
            return;
        }

        // Get Item to regrade
        if (targetObj is not SkillCastItemTarget itemTarget)
        {
            Logger.Debug("Invalid target type. Expected SkillCastItemTarget.");
            return;
        }

        // Check Charm
        var useCharm = false;
        SkillObjectItemGradeEnchantingSupport charm = null;
        if (skillObject is SkillObjectItemGradeEnchantingSupport support)
        {
            charm = support;
            if (charm is { SupportItemId: not 0 })
            {
                useCharm = true;
            }
        }

        var isLucky = value1 != 0;
        var item = character.Inventory.GetItemById(itemTarget.Id);
        if (item == null)
        {
            // Invalid item
            return;
        }
        var initialGrade = item.Grade;
        //var itemEnchantRatio = ItemGameData.Instance.GetItemEnchantRatio((int)item.TemplateId, item.Grade);
        var ratioGroupId = ItemGameData.Instance.GetItemEnchantRatioGroupByItemId((int)item.TemplateId);
        var itemEnchantRatio = ItemGameData.Instance.GetItemEnchantRatio(ratioGroupId, item.Grade);

        //var tasks = new List<ItemTask>();

        var cost = ItemGameData.GoldCost(item, (ItemImpl)itemType);
        if (cost == -1)
        {
            // No gold on template, invalid ?
            return;
        }

        if (character.Money < cost)
        {
            character.SendErrorMessage(ErrorMessageType.NotEnoughMoney);
            return;
        }

        if (!character.Inventory.CheckItems(SlotType.Bag, scroll.ItemTemplateId, 1))
        {
            // No scroll
            character.SendErrorMessage(ErrorMessageType.NotEnoughRequiredItem);
            return;
        }

        ItemGradeEnchantingSupport charmInfo = null;
        Item charmItem = null;
        if (useCharm)
        {
            charmItem = character.Inventory.GetItemById(charm.SupportItemId);
            if (charmItem == null)
            {
                return;
            }

            charmInfo = ItemGameData.Instance.GetItemGradEnchantingSupportByItemId((int)charmItem.TemplateId);
            if (charmInfo.RequireGradeMin != -1 && item.Grade < charmInfo.RequireGradeMin)
            {
                character.SendErrorMessage(ErrorMessageType.NotEnoughRequiredItem);
                return;
            }

            if (charmInfo.RequireGradeMax != -1 && item.Grade > charmInfo.RequireGradeMax)
            {
                character.SendErrorMessage(ErrorMessageType.GradeEnchantMax);
                return;
            }

            // tasksRemove.Add(InventoryHelper.GetTaskAndRemoveItem(character, charmItem, 1));
        }

        // All seems to be in order, roll item, consume items and send the results
        var result = ItemGameData.RollRegrade(itemEnchantRatio, item, isLucky, useCharm, charmInfo);
        if (result == GradeEnchantResult.Break)
        {
            // Poof
            item.HoldingContainer.RemoveItem(ItemTaskType.GradeEnchant, item, true);
        }
        else
        {
            // No Poof
            character.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.GradeEnchant, [new ItemGradeChange(item)], []));
        }

        // Consume
        character.SubtractMoney(SlotType.Bag, cost);
        // TODO: Handled by skill already, do more tests
        // character.Inventory.PlayerInventory.ConsumeItem(ItemTaskType.GradeEnchant, scroll.ItemTemplateId, 1, character.Inventory.GetItemById(scroll.ItemId));
        if (useCharm)
        {
            character.Inventory.Bag.ConsumeItem(ItemTaskType.GradeEnchant, charmItem.TemplateId, 1, charmItem);
        }

        character.SendPacket(new SCItemGradeEnchantResultPacket((byte)result, item, initialGrade, item.Grade, 0u, 0, false));

        if (result is GradeEnchantResult.Success or GradeEnchantResult.GreatSuccess)
        {
            character.Achievements?.TrackRecordProgress(CharRecordKind.EnchantItem, (uint)item.Grade);
        }
        else
        {
            character.Achievements?.TrackRecordProgress(CharRecordKind.EnchantFailure);
        }

        // Let the world know if we got lucky enough
        if (item.Grade >= 8 && result is GradeEnchantResult.Success or GradeEnchantResult.GreatSuccess)
        {
            WorldManager.Instance.BroadcastPacketToServer(new SCLinkTextForEnchantBroadcastPacket(character.Name, (byte)result, item, initialGrade, item.Grade));
        }
    }
}
