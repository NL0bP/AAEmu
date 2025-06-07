using System;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.slaves;

namespace AAEmu.Game.Models.Game.Skills.Effects;

public class RepairSlaveEffect : EffectTemplate
{
    public int Health { get; set; }
    public int Mana { get; set; }

    public override bool OnActionTime => false;

    public override void Apply(BaseUnit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj,
        CastAction castObj, EffectSource source, SkillObject skillObject, DateTime time,
        CompressedGamePackets packetBuilder = null)
    {
        if (target is Slave childSlave)
        {
            if (targetObj is SkillCastItemTarget scit)
            {
                var item = ItemManager.Instance.GetItemByItemId(scit.Id);
                var skillCasterItem = casterObj as SkillItem;
                // TODO: might need to check if it's a repair point?
                if (caster is not Character player)
                {
                    Logger.Warn($"RepairSlaveEffect target unit {scit.ObjId} is not a player");
                    return;
                }

                var slave = SlaveManager.Instance.GetSlaveByOwnerObjId(player.ObjId);
                if (item is not SummonSlave slaveItem)
                {
                    Logger.Warn($"RepairSlaveEffect target item {scit.Id} is not a salve summon item");
                    return;
                }

                if (slaveItem.Template is not SummonSlaveTemplate summonTemplate)
                {
                    Logger.Warn($"RepairSlaveEffect target item {scit.Id} is not a salve summon item");
                    return;
                }

                slaveItem.IsDestroyed = 0;
                slaveItem.RepairStartTime = DateTime.UtcNow;
                slaveItem.IsDirty = true;

                player.SendPacket(new SCUpdateSlaveSourceItemPacket(slave.ObjId, item.Id, 1000000, EquipSlot: 255));
                var repairCost = SlaveManager.Instance.GetComponent(summonTemplate.SlaveId)?.RepairCost ?? 1;
                player.Inventory.Bag.ConsumeItem(ItemTaskType.RepairSlaves, skillCasterItem.ItemTemplateId, repairCost, null);
                childSlave.Spawn();

                Logger.Debug($"{player.Name} repaired slave on item {item.Id}");
            }
            else
            {
                Logger.Warn($"RepairSlaveEffect target is not a SkillCastItemTarget");
            }
        }
        else
        {
            if (targetObj is SkillCastItemTarget scit)
            {
                var item = ItemManager.Instance.GetItemByItemId(scit.Id);
                var targetPlayer = WorldManager.Instance.GetCharacterByObjId(scit.ObjId);
                // TODO: might need to check if it's a repair point?
                if (targetPlayer == null)
                {
                    Logger.Warn($"RepairSlaveEffect target unit {scit.ObjId} is not a player");
                    return;
                }
                if (item is not SummonSlave slaveItem)
                {
                    Logger.Warn($"RepairSlaveEffect target item {scit.Id} is not a salve summon item");
                    return;
                }

                if (slaveItem.Template is not SummonSlaveTemplate summonTemplate)
                    return;

                //if (!SlaveManager.Instance._repairableSlaves.TryGetValue(summonTemplate.SlaveId, out var expectedEffectId) || (expectedEffectId != Id))
                //{
                //    targetPlayer.SendErrorMessage(ErrorMessageType.ItemFailedRepair); // not sure if this would be the correct one
                //    Logger.Warn($"{targetPlayer.Name} tried to use the wrong repair item {slaveItem.Id} (template: {slaveItem.TemplateId} for slave type {summonTemplate.SlaveId}");
                //    return;
                //}
                slaveItem.IsDestroyed = 0;
                slaveItem.RepairStartTime = DateTime.UtcNow;
                slaveItem.IsDirty = true;
                
                if (casterObj is not SkillItem skillCasterItem)
                    return;

                // Despawn the slave if it's currently active
                var slave = SlaveManager.Instance.GetSlaveByOwnerObjId(targetPlayer.ObjId);
                if (slave != null)
                {
                    slave.Hp = slave.MaxHp;
                    slave.Mp = slave.MaxMp;
                    SlaveManager.Instance.RemoveActiveSlave(slave.Summoner, slave.TlId, true);
                }

                var repairCost = SlaveManager.Instance.GetSlaveTemplate(summonTemplate.SlaveId).Cost;
                targetPlayer.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.RepairSlaves, new ItemUpdate(slaveItem), []));
                targetPlayer.Inventory.Bag.ConsumeItem(ItemTaskType.RepairSlaves, skillCasterItem.ItemTemplateId, repairCost, null);
                Logger.Debug($"{targetPlayer.Name} repaired slave on item {item.Id}, repairCost={repairCost}");
            }
            else
            {
                Logger.Warn($"RepairSlaveEffect target is not a SkillCastItemTarget");
            }
        }
    }
}
