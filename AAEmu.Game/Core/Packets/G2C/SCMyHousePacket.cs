using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Housing;
using AAEmu.Game.Models.Game.Items.Actions;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCMyHousePacket : GamePacket
{
    private const int MaxTaskCount = 30;
    private readonly uint _type;
    private readonly ItemTaskType _action;
    private readonly List<ItemTask> _tasks;
    private readonly List<ulong> _forceRemove;
    private readonly uint _targetType;
    private readonly int _lockItemSlotKey;

    public SCMyHousePacket(House house)
        : this(house.TlId, ItemTaskType.Invalid, [], [], 0, 0)
    {
    }

    public SCMyHousePacket(uint type, ItemTaskType action, List<ItemTask> tasks, List<ulong> forceRemove, uint targetType = 0, int lockItemSlotKey = 0)
        : base(SCOffsets.SCMyHousePacket, 5)
    {
        _type = type;
        _action = action;
        _tasks = tasks ?? [];
        _forceRemove = forceRemove ?? [];
        _targetType = targetType;
        _lockItemSlotKey = lockItemSlotKey;
    }

    public override PacketStream Write(PacketStream stream)
    {
        var taskCount = _tasks.Count;
        if (taskCount > MaxTaskCount)
            taskCount = MaxTaskCount;

        var forceRemoveCount = _forceRemove.Count;
        if (forceRemoveCount > MaxTaskCount)
            forceRemoveCount = MaxTaskCount;

        stream.Write(_type);
        stream.Write((byte)_action);
        stream.Write((byte)taskCount);
        for (var i = 0; i < taskCount; i++)
            stream.Write(_tasks[i]);

        stream.Write((byte)forceRemoveCount);
        for (var i = 0; i < forceRemoveCount; i++)
            stream.Write(_forceRemove[i]);

        stream.Write(_targetType);
        stream.Write(_lockItemSlotKey);
        return stream;
    }
}
