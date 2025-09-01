using System;
using System.Numerics;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Items.Templates;

namespace AAEmu.Game.Models.Game.Items;

public class SummonSlave : Item
{
    private DateTime _repairStartTime;
    public override ItemDetailType DetailType => ItemDetailType.Slave;
    public override uint DetailBytesLength => 33;

    public byte SlaveType { get; set; } // Not sure about this, captures show 2 here
    public uint SlaveDbId { get; set; }
    public byte IsDestroyed { get; set; }

    public DateTime RepairStartTime
    {
        get => _repairStartTime;
        set
        {
            _repairStartTime = value;
        }
    }

    // TODO: Actually use this location for saving the data in ItemDetails
    public Vector3 SummonLocation { get; set; }

    public SummonSlave()
    {
    }

    public SummonSlave(ulong id, ItemTemplate template, int count) : base(id, template, count)
    {
    }

    public override void ReadDetails(PacketStream stream)
    {
        if (stream.LeftBytes < DetailBytesLength)
            return;
        SlaveType = stream.ReadByte();   // 1 Type? (2 = slave?)
        SlaveDbId = stream.ReadBc();     // 3 4 DbId
        IsDestroyed = stream.ReadByte(); // 1 5
        if (IsDestroyed == 0)
            RepairStartTime = DateTime.MinValue;
        else
        {
            long ticks = stream.ReadInt64();       // 8 13 Считываем 8 байт и преобразуем в long
            RepairStartTime = new DateTime(ticks); // Восстанавливаем DateTime
        }
    }

    public override void WriteDetails(PacketStream stream)
    {
        stream.Write(SlaveType);   // 1
        stream.WriteBc(SlaveDbId); // 3 4
        stream.Write(IsDestroyed); // 1 5

        if (IsDestroyed == 0)
            RepairStartTime = DateTime.MinValue;
        long ticks = RepairStartTime.Ticks;
        stream.Write(ticks); // 8 13 запишем 8 байт

        // The following 16 bytes somehow determine where a Vehicle is allowed to be summoned
        // TODO: Get real live data capture of this value being set
        // TODO: Get this from having a vehicle out when maintenance starts
        stream.Write(0); // 4 17
        stream.Write(0); // 4 21
        stream.Write(0); // 4 25
        stream.Write(0); // 4 29
        stream.Write(0); // 4 33 in 5+
    }

    public override void WriteAdditionalDetails(PacketStream stream)
    {
        stream.Write(SlaveDbId);   // 4 4
        stream.Write(IsDestroyed); // 1 5

        if (RepairStartTime == DateTime.MinValue)
        {
            stream.Write(0); // 4 9
        }
        else
        {
            var unixTime = (int)((RepairStartTime.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds);
            stream.Write(unixTime); // 4 9
        }

        stream.Write(0); // 4 13
    }

    public override void OnManuallyDestroyingItem()
    {
        if (!SlaveManager.Instance.OnDeleteSlaveItem(this))
            Logger.Warn($"Failed to delete Slave attached to Item Id: {Id}, Type: {TemplateId}");
    }

    public override bool CanDestroy()
    {
        // TODO: Always allow expired items to be removed regardless if summoned or not 
        var owner = WorldManager.Instance.GetCharacterById((uint)OwnerId);
        if (owner != null)
        {
            var checkSlave = SlaveManager.Instance.GetSlaveByOwnerObjId(owner.ObjId);
            if (checkSlave?.Id == SlaveDbId)
            {
                owner.SendErrorMessage(ErrorMessageType.SlaveSpawnItemLocked);
                return false;
            }
        }

        return true;
    }
}
