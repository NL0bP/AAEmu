using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Models.Game.AI.Enums;
using AAEmu.Game.Models.Game.AI.v2.AiCharacters;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.NPChar;

namespace AAEmu.Game.Models.Game.AI.Utils;

/// <summary>
/// Provides utility methods for AI behavior and positioning calculations.
/// </summary>
public static class AIUtils
{
    // Default roaming settings
    private const float DefaultMaxRoamingDistance = 6.0f;
    //private const float DefaultMaxHeightAdjustment = 0.5f;

    /// <summary>
    /// Calculates the next roaming position for an NPC within its idle area.
    /// This method ensures the NPC stays within bounds and at a valid height.
    /// </summary>
    /// <param name="ai">The AI controller for the NPC</param>
    /// <returns>A new Vector3 position for the NPC to move to</returns>
    public static Vector3 CalcNextRoamingPosition(NpcAi ai)
    {
        if (ai?.Owner == null || ai.IdlePosition == default)
            return Vector3.Zero;

        var randomOffset = new Vector2(
            (Rand.NextSingle() - 0.5f) * DefaultMaxRoamingDistance * 2,
            (Rand.NextSingle() - 0.5f) * DefaultMaxRoamingDistance * 2);

        var newPosition = new Vector3(
            ai.IdlePosition.X + randomOffset.X,
            ai.IdlePosition.Y + randomOffset.Y,
            ai.IdlePosition.Z);

        // Get terrain height at new position
        newPosition.Z = ai.Owner.GetReferenceHeight(newPosition.X, newPosition.Y, newPosition.Z, ai.Owner.Transform.ZoneId);

        return newPosition;
    }

    private static readonly System.Collections.Generic.Dictionary<AiParamType, System.Func<Npc, NpcAi>> _aiFactory =
        new()
        {
            { AiParamType.AlmightyNpc, owner => new AlmightyNpcAiCharacter { Owner = owner } },
            { AiParamType.ArcherHoldPosition, owner => new ArcherHoldPositionAiCharacter { Owner = owner } },
            { AiParamType.ArcherRoaming, owner => new ArcherRoamingAiCharacter { Owner = owner } },
            { AiParamType.BigMonsterHoldPosition, owner => new BigMonsterHoldPositionAiCharacter { Owner = owner } },
            { AiParamType.BigMonsterRoaming, owner => new BigMonsterRoamingAiCharacter { Owner = owner } },
            { AiParamType.Default, owner => new DefaultAiCharacter { Owner = owner } },
            { AiParamType.Dummy, owner => new DummyAiCharacter { Owner = owner } },
            { AiParamType.Flytrap, owner => new FlytrapAiCharacter { Owner = owner } },
            { AiParamType.HoldPosition, owner => new HoldPositionAiCharacter { Owner = owner } },
            { AiParamType.Roaming, owner => new RoamingAiCharacter { Owner = owner } },
            { AiParamType.TowerDefenseAttacker, owner => new TowerDefenseAttackerAiCharacter { Owner = owner } },
            { AiParamType.WildBoarHoldPosition, owner => new WildBoarHoldPositionAiCharacter { Owner = owner } },
            { AiParamType.WildBoarRoaming, owner => new WildBoarRoamingAiCharacter { Owner = owner } }
        };

    /// <summary>
    /// Creates and returns an appropriate AI controller based on the AI parameter type.
    /// </summary>
    /// <param name="type">The type of AI behavior to create</param>
    /// <param name="owner">The NPC that will use this AI</param>
    /// <returns>A configured NpcAi instance, or null if type is not supported</returns>
    public static NpcAi GetAiByType(AiParamType type, Npc owner)
    {
        if (owner == null) return null;
        return _aiFactory.TryGetValue(type, out var factory) ? factory(owner) : null;
    }
}
