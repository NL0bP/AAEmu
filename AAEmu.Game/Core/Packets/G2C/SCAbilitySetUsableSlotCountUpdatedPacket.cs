using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCAbilitySetUsableSlotCountUpdatedPacket : GamePacket
{
    private readonly byte _usableSlotCount;

    public SCAbilitySetUsableSlotCountUpdatedPacket(int usableSlotCount) : base(SCOffsets.SCAbilitySetUsableSlotCountUpdatedPacket, 5)
    {
        _usableSlotCount = (byte)Math.Clamp(usableSlotCount, byte.MinValue, byte.MaxValue);
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_usableSlotCount);
        return stream;
    }
}
