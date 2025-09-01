using System;

using AAEmu.Commons.Network;
using AAEmu.Game.Models.Game.Items.Templates;

namespace AAEmu.Game.Models.Game.Items;

public class SlaveEquip : Item
{
    public override ItemDetailType DetailType => ItemDetailType.SlaveEquipment;
    public override uint DetailBytesLength => 12;

    public uint SlaveHp { get; set; }
    public DateTime RepairStartTime { get; set; }

    public SlaveEquip()
    {
    }

    public SlaveEquip(ulong id, ItemTemplate template, int count) : base(id, template, count)
    {
    }

    public SlaveEquip(ulong id, ItemTemplate template, int count, uint slaveHp, DateTime repairStartTime) : base(id, template, count)
    {
        SlaveHp = slaveHp;
        RepairStartTime = repairStartTime;
    }

    public override void ReadDetails(PacketStream stream)
    {
        if (stream.LeftBytes < DetailBytesLength)
            return;

        SlaveHp = stream.ReadUInt32();         // 4 4 HP
        long ticks = stream.ReadInt64();       // 8 12 Считываем 8 байт и преобразуем в long
        RepairStartTime = new DateTime(ticks); // Восстанавливаем DateTime
    }

    public override void WriteDetails(PacketStream stream)
    {
        stream.Write(SlaveHp); // 4 4 HP

        if (SlaveHp > 0)
            RepairStartTime = DateTime.MinValue;
        else
            RepairStartTime = DateTime.UtcNow;

        long ticks = RepairStartTime.Ticks;
        stream.Write(ticks); // 8 12 запишем 8 байт
    }
}
