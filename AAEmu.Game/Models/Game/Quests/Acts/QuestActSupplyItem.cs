using System.Collections.Generic;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Quests.Templates;

namespace AAEmu.Game.Models.Game.Quests.Acts
{
    public class QuestActSupplyItem : QuestActTemplate
    {
        public uint ItemId { get; set; }
        public int Count { get; set; }
        public byte GradeId { get; set; }
        public bool ShowActionBar { get; set; }
        public bool Cleanup { get; set; }
        public bool DropWhenDestroy { get; set; }
        public bool DestroyWhenDrop { get; set; }

        public override bool Use(Character character, Quest quest, int objective)
        {
            _log.Warn("QuestActSupplyItem QuestId {0}, ItemId {1}, Count {2}, GradeId {3}" +
                      "ShowActionBar {4}, Cleanup {5}, DropWhenDestroy {6}, DestroyWhenDrop {7}, objective {8}",
                quest.TemplateId, ItemId, Count, GradeId, ShowActionBar, Cleanup, DropWhenDestroy,
                DestroyWhenDrop, objective);

            if (objective >= Count) { return true; }
            var tasks = new List<ItemTask>();
            var item = ItemManager.Instance.Create(ItemId, Count, GradeId);
            if (item == null) { return false; }

            var backpackTemplate = (BackpackTemplate)null;
            if (item.Template is BackpackTemplate)
            {
                backpackTemplate = (BackpackTemplate)item.Template;
            }

            var res = character.Inventory.AddItem(item);
            if (res == null)
            {
                ItemIdManager.Instance.ReleaseId((uint)item.Id);
            }

            if (res.Id != item.Id)
            {
                tasks.Add(new ItemCountUpdate(res, item.Count));
            }
            else
            {
                tasks.Add(new ItemAdd(item));
            }

            character.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.QuestSupplyItems, tasks, new List<ulong>()));
            if (backpackTemplate != null && backpackTemplate.BackpackType == BackpackType.TradePack)
            {
                character.Inventory.Move(item.Id, item.SlotType, (byte)item.Slot, 0, SlotType.Equipment, (byte)EquipmentItemSlot.Backpack);
            }

            return true;
        }
    }
}
