using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCAbilitySetAllInfoPacket : GamePacket
{
    private readonly byte _usedFreeActivationCount = 0;
    private readonly int _count = 0;
    private readonly byte[] _savedAbilitySets = [0, 0, 0];
    private readonly byte[] _savedHighAbilitySets = [0, 0, 0];

    public SCAbilitySetAllInfoPacket() : base(SCOffsets.SCAbilitySetAllInfoPacket, 5)
    {
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_usedFreeActivationCount);
        stream.Write(_count);
        //while (true)
        {
            for (var i = 0; i < 3; i++)
            {
                stream.Write(_savedAbilitySets[i]);
                stream.Write(_savedHighAbilitySets[i]);
            }

            for (var i = 0; i < 8; i++)
            {
                stream.Write(0u); // type
            }
        }
        return stream;
    }
}
