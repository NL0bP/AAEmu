using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCAbilitySetAllInfoPacket : GamePacket
{
    private readonly Character _character;

    public SCAbilitySetAllInfoPacket(Character character) : base(SCOffsets.SCAbilitySetAllInfoPacket, 5)
    {
        _character = character;
    }

    public override PacketStream Write(PacketStream stream)
    {
        if (_character == null)
        {
            stream.Write((byte)0);
            stream.Write(0);
            return stream;
        }

        var abilitySets = _character.AbilitySets;
        var abilitySetCount = abilitySets?.Count ?? 0;

        stream.Write(_character.AbilitySetFreeActivationCount);
        stream.Write(abilitySetCount);

        for (var i = 0; i < abilitySetCount; i++)
        {
            var abilitySet = abilitySets[i];

            if (abilitySet == null)
            {
                for (var j = 0; j < 3; j++)
                {
                    stream.Write((byte)0);
                    stream.Write((byte)0);
                }
                stream.Write(0);
                stream.Write(0);
                continue;
            }

            for (var j = 0; j < 3; j++)
            {
                stream.Write(abilitySet.SavedAbilitySets[j]);
                stream.Write(abilitySet.SavedHighAbilitySets[j]);
            }

            var typeCount = abilitySet.Types.Count;
            if (typeCount > 36)
                typeCount = 36;

            stream.Write(typeCount);
            for (var j = 0; j < typeCount; j++)
                stream.Write(abilitySet.Types[j]);

            var extraTypeCount = abilitySet.ExtraTypes.Count;
            stream.Write(extraTypeCount);
            for (var j = 0; j < extraTypeCount; j++)
                stream.Write(abilitySet.ExtraTypes[j]);
        }

        return stream;
    }
}
