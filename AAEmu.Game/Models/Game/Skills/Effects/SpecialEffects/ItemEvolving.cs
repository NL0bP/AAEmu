using System;
using System.Collections.Generic;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Achievement.Enums;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

public class ItemEvolving : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.ItemEvolving;
    private const int Offset = 13;
    private const int OffsetMax = Offset + 5;

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
        int value3,
        int value4, int value5, int value6, int value7)
    {
        // Get Player
        if (caster is not Character character)
        {
            Logger.Debug("Invalid caster type. Expected Character.");
            return;
        }

        Logger.Debug($"Special effects: ItemEvolving value1 {value1}, value2 {value2}, value3 {value3}, value4 {value4}, value5 {value5}, value6 {value6}, value7 {value7}");

        var itemId = ((SkillCastItemTarget)targetObj).Id;
        var item = ItemManager.Instance.GetItemByItemId(itemId);
        if (!(item is EquipItem && item.Template is EquipItemTemplate equipItem))
        {
            Logger.Debug($"Attempting to upgrade a non-equipment item. Item={item.Id}");
            return;
        }

        var itemId1 = ((SkillObjectItemEvolvingSupport)skillObject).M1ItemId;
        var item1 = ItemManager.Instance.GetItemByItemId(itemId1);
        if (!(item1 is EquipItem && item1.Template is EquipItemTemplate equipItem1))
        {
            Logger.Debug($"Attempting to use an item that is not equipment. Item={item1.Id}");
            return;
        }

        var itemId2 = ((SkillObjectItemEvolvingSupport)skillObject).M2ItemId;
        var item2 = ItemManager.Instance.GetItemByItemId(itemId2);
        if (!(item2 is EquipItem && item2.Template is EquipItemTemplate equipItem2))
        {
            Logger.Debug($"Attempting to use an item that is not equipment. Item={item2.Id}");
            return;
        }

        // We do not allow the item being upgraded to be destroyed
        if (item.Id == item1.Id || item.Id == item2.Id)
        {
            Logger.Warn($"You cannot use the same item for improvement! Item={item.Id}, Item1={item1.Id}, Item2={item2.Id}");
            character.SendDebugMessage($"You cannot use the same item for improvement! Item @ITEM_NAME({item.TemplateId})'s");
            return;
        }

        var autoUseAAPoint = ((SkillObjectItemEvolvingSupport)skillObject).AutoUseAAPoint;

        var beforeItemGrade = item.Grade;
        Logger.Debug($"ItemEvolving: beforeItemGrade={beforeItemGrade}");

        var addExperience = ItemGameData.CalculateAddExperience(equipItem1, item1, equipItem2, item2);
        Logger.Debug($"ItemEvolving: addExperience={addExperience}");
        var currentExperience = (int)item.GemIds[3]; //RemainingExperience; // взять текущий опыт
        Logger.Debug($"ItemEvolving: currentExperience={currentExperience}");
        currentExperience += addExperience;
        Logger.Debug($"ItemEvolving: currentExperience + addExperience={currentExperience}");

        var bonusExp = ItemGameData.Instance.GetBonus(equipItem.ItemRndAttrCategoryId, item.Grade);
        if (bonusExp > 0)
        {
            currentExperience += bonusExp;
            Logger.Debug($"ItemEvolving: currentExperience + bonusExp={currentExperience}");
            addExperience += bonusExp;
            Logger.Debug($"ItemEvolving: addExperience + bonusExp={addExperience}");
        }

        // ограничим грейд предмета
        var gradeExp = ItemGameData.Instance.GetGradeExp(equipItem.ItemRndAttrCategoryId, item.Grade);
        Logger.Debug($"ItemEvolving: gradeExp={gradeExp}");
        var maxEvolvingGrade = ItemGameData.Instance.GetItemRndAttrCategory(equipItem.ItemRndAttrCategoryId).MaxEvolvingGrade;
        if (beforeItemGrade >= maxEvolvingGrade && currentExperience >= gradeExp)
        {
            currentExperience = gradeExp;
            addExperience = gradeExp;
            item.GemIds[3] = 0;
            Logger.Warn($"ItemEvolving: Reached grade limit! beforeItemGrade:maxEvolvingGrade={beforeItemGrade}:{maxEvolvingGrade}, currentExperience={currentExperience}");
        }

        var (afterItemGrade, remainingExperience) = ItemGameData.CalculateGradeAndExp(currentExperience, equipItem.ItemRndAttrCategoryId, beforeItemGrade);
        item.GemIds[3] = (uint)remainingExperience; // сохраним оставшийся опыт
        Logger.Debug($"ItemEvolving: remainingExperience={remainingExperience}");
        item.Grade = afterItemGrade;
        Logger.Debug($"ItemEvolving: afterItemGrade={afterItemGrade}");

        var changeIndex = ((SkillObjectItemEvolvingSupport)skillObject).ChangeIndex; // -1 - не менять аттрибуты, (0..n) - изменить существующий атрибут
        var needChangeAttribute = changeIndex != -1;

        var beforeAttribute = new ItemEvolvingAttribute();
        var afterAttribute = new ItemEvolvingAttribute();
        var addAttributes = new List<ItemEvolvingAttribute>();
        var currentAttributes = ItemGameData.GetCurrentAttributes(item);

        if (needChangeAttribute)
        {
            if (afterItemGrade > beforeItemGrade)
            {
                var selectAttribute = ItemGameData.Instance.GetUnitAttributeFromModifierGroup((int)item.GemIds[changeIndex + Offset]);
                beforeAttribute.Attribute = (ushort)selectAttribute;
                beforeAttribute.AttributeType = 0;
                beforeAttribute.AttributeValue = 0;

                var newAttribute = ItemGameData.Instance.ReplaceSelectAttribute(equipItem.ItemRndAttrCategoryId, item.Grade, currentAttributes, selectAttribute);
                item.GemIds[changeIndex + Offset] = (uint)newAttribute.id;
                afterAttribute.Attribute = (ushort)newAttribute.attribute;
                afterAttribute.AttributeType = 0;
                afterAttribute.AttributeValue = newAttribute.value;

                Logger.Debug($"ItemEvolving: beforeAttribute id={item.GemIds[changeIndex + Offset]}, attribute={selectAttribute}, value={beforeAttribute.AttributeValue}, afterAttribute id={newAttribute.id}, attribute={newAttribute.attribute}, value={newAttribute.value}");
            }
        }
        else if (ItemGameData.Instance.GetMaxUnitModifierNum(equipItem.ItemRndAttrCategoryId, item.Grade) > currentAttributes.Count && afterItemGrade > beforeItemGrade)
        {
            var randomAttributes = ItemGameData.Instance.GetRandomAttributes(equipItem.ItemRndAttrCategoryId, item.Grade, currentAttributes);

            for (var index = currentAttributes.Count; index < randomAttributes.Count + currentAttributes.Count; index++)
            {
                var newAttribute = new ItemEvolvingAttribute();
                newAttribute.Attribute = (ushort)randomAttributes[index - currentAttributes.Count].attribute;
                newAttribute.AttributeType = 0;
                newAttribute.AttributeValue = randomAttributes[index - currentAttributes.Count].value;

                addAttributes.Add(newAttribute);
                item.GemIds[index + Offset] = (uint)randomAttributes[index - currentAttributes.Count].id;
                Logger.Debug($"ItemEvolving: addAttribute id={randomAttributes[index - currentAttributes.Count].id}, Attribute={randomAttributes[index - currentAttributes.Count].attribute}, value={randomAttributes[index - currentAttributes.Count].value}");
            }
        }

        // we'll use up the material
        character.Inventory.Bag.ConsumeItem(ItemTaskType.Evolving, item1.TemplateId, item1.Count, item1);
        character.Inventory.Bag.ConsumeItem(ItemTaskType.Evolving, item2.TemplateId, item2.Count, item2);

        // израсходуем деньги (не нашел, как правильно найти стоимость)
        // we will spend the money (I haven’t found how to find the cost correctly)
        var grade = item1.Grade == 0 ? 1 : item1.Grade;
        var cost = ItemGameData.Instance.GetGoldMul(equipItem.ItemRndAttrCategoryId, item1.Grade) * grade;
        grade = item2.Grade == 0 ? 1 : item2.Grade;
        cost += ItemGameData.Instance.GetGoldMul(equipItem.ItemRndAttrCategoryId, item2.Grade) * grade;
        var money = (int)Math.Round(cost / 10.0);
        if (money == -1)
        {
            // No gold on template, invalid ?
            return;
        }

        if (character.Money < money)
        {
            character.SendErrorMessage(ErrorMessageType.NotEnoughMoney);
            return;
        }

        character.ChangeMoney(SlotType.Bag, -money);

        character.SendPacket(new SCItemTaskSuccessPacket(
            ItemTaskType.Evolving,
            [
                new ItemGradeChange(item),
                new ItemUpdate(item)
            ],
            [])
        );

        Logger.Debug($"ItemEvolving: isEvolving={true}, itemId={itemId}, changeAttr={needChangeAttribute}, afterItemGrade={afterItemGrade}, addExperience={addExperience}, bonusExp={bonusExp}, beforeItemGrade={beforeItemGrade}");
        character.SendPacket(new SCItemEvolvingPacket(
                true,                      // bool isEvolving,
                itemId,                    // ulong itemId,
                needChangeAttribute,       //bool changeAttr,
                afterItemGrade,            // byte afterItemGrade,
                beforeAttribute,           // ItemEvolvingAttribute beforeAttribute,
                afterAttribute,            // ItemEvolvingAttribute afterAttribute,
                addAttributes,             // List<ItemEvolvingAttribute> addAttributes,
                (byte)addAttributes.Count, // addAttrCount,
                addExperience,
                bonusExp,
                beforeItemGrade
            )
        );

        character.Achievements?.TrackHiramGrade(item.TemplateId, item.Grade);

        // Log the action
        Logger.Debug($"MagicalEnchant executed by {character.Name} on item {item.Id} with skill item {item.TemplateId}");
    }
}
