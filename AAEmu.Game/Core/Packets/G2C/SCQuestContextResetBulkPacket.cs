using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCQuestContextResetBulkPacket : GamePacket
{
    private readonly uint[] _questIds;

    public SCQuestContextResetBulkPacket(uint[] questIds) : base(SCOffsets.SCQuestContextResetBulkPacket, 5)
    {
        _questIds = questIds;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write((byte)_questIds.Length);
        foreach (var questId in _questIds)
        {
            stream.Write(questId);
        }
        return stream;
    }
}
