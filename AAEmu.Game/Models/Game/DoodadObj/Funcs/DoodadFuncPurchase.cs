using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncPurchase : DoodadFuncTemplate
{
    // doodad_funcs
    public uint ItemId { get; set; }
    public int Count { get; set; }
    public uint CoinItemId { get; set; }
    public int CoinCount { get; set; }
    public uint CurrencyId { get; set; }

    public override void Use(BaseUnit caster, Doodad owner, uint skillId, int nextPhase = 0)
    {
        if (!(caster is Character character))
            return;
        if (character.Inventory.Bag.SpaceLeftForItem(ItemId) < Count)
        {
            character.SendErrorMessage(ErrorMessageType.BagFull);
            return;
        }

        // обрабатывать валютные транзакции (когда CoinItemId и CoinCount == 0)
        // process currency transactions (when CoinItemId and CoinCount == 0)
        if (CoinItemId == 0 && CoinCount == 0)
        {
            // Получить шаблон товара, чтобы определить цену
            // Get the product template to determine the price
            var itemTemplate = ItemManager.Instance.GetTemplate(ItemId);
            if (itemTemplate == null)
            {
                Logger.Warn($"DoodadFuncPurchase: id={ItemId}");
                return;
            }
            // Получить информацию о цене из шаблона товара
            // Примечание. Здесь предполагается, что шаблон предмета имеет атрибут цены или цена может быть получена с помощью других методов
            // Get price information from the product template
            // Note. Here it is assumed that the item template has a price attribute or the price can be obtained using other methods
            var itemPrice = itemTemplate.Price;
            if (character.Money < itemPrice)
            {
                character.SendErrorMessage(ErrorMessageType.NotEnoughMoney);
                return;
            }
            // Считайте золотые монеты
            character.ChangeMoney(SlotType.Bag, -itemPrice);
        }
        else
        {
            // Обработка транзакций на основе CoinItemId, CoinCount
            // Item-based transaction processing
            if (character.Inventory.Bag.ConsumeItem(ItemTaskType.DoodadInteraction, CoinItemId, CoinCount, null) <= 0)
            {
                character.SendErrorMessage(ErrorMessageType.NotEnoughItem);
                return;
            }
        }

        if (ItemManager.Instance.IsAutoEquipTradePack(ItemId))
        {
            if (!character.Inventory.TryEquipNewBackPack(ItemTaskType.QuestSupplyItems, ItemId, Count))
            {
                Logger.Warn($"DoodadFuncPurchase: Failed to auto-equip backpack item {ItemId} for player {character.Name}");
                character.SendErrorMessage(ErrorMessageType.BackpackOccupied);
            }
        }
        else
        {
            if (!character.Inventory.Bag.AcquireDefaultItem(ItemTaskType.DoodadInteraction, ItemId, Count))
            {
                Logger.Warn($"DoodadFuncPurchase: Failed to create item {ItemId} for player {character.Name}");
            }
        }
    }
}
