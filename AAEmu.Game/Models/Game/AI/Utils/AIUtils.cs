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

        // Calculate random offset from idle position within roaming bounds
        var randomOffset = new Vector2(
            (Rand.NextSingle() - 0.5f) * DefaultMaxRoamingDistance * 2,
            (Rand.NextSingle() - 0.5f) * DefaultMaxRoamingDistance * 2
        );

        // Calculate new position
        var newPosition = new Vector3(
            ai.IdlePosition.X + randomOffset.X,
            ai.IdlePosition.Y + randomOffset.Y,
            ai.IdlePosition.Z
        );

        // Get terrain height at new position
        if (!ai.Owner.CanFly)
        {
            var terrainHeight = ai.Owner.GetReferenceHeight(newPosition.X, newPosition.Y, newPosition.Z, ai.Owner.Transform.ZoneId);
            if (terrainHeight != 0)
            {
                newPosition.Z = terrainHeight;
            }
        }
        //var terrainHeight = WorldManager.Instance.GetHeight(ai.Owner.Transform.ZoneId, newPosition.X, newPosition.Y);

        //// Handle terrain height adjustments
        //if (terrainHeight <= 0.0f || ai.Owner.CanFly)
        //{
        //    // For flying units or invalid terrain, use current Z position
        //    terrainHeight = newPosition.Z;
        //}
        //else if (newPosition.Z < terrainHeight && terrainHeight - DefaultMaxHeightAdjustment < newPosition.Z)
        //{
        //    // Adjust position to terrain height if within reasonable range
        //    newPosition.Z = terrainHeight;
        //}

        return newPosition;
    }

    /// <summary>
    /// Creates and returns an appropriate AI controller based on the AI parameter type.
    /// </summary>
    /// <param name="type">The type of AI behavior to create</param>
    /// <param name="owner">The NPC that will use this AI</param>
    /// <returns>A configured NpcAi instance, or null if type is not supported</returns>
    public static NpcAi GetAiByType(AiParamType type, Npc owner)
    {
        if (owner == null)
            return null;

        return type switch
        {
            // Combat AIs
            AiParamType.AlmightyNpc => new AlmightyNpcAiCharacter { Owner = owner },

            // Archer variants
            AiParamType.ArcherHoldPosition => new ArcherHoldPositionAiCharacter { Owner = owner },
            AiParamType.ArcherRoaming => new ArcherRoamingAiCharacter { Owner = owner },

            // Big monster variants
            AiParamType.BigMonsterRoaming => new BigMonsterRoamingAiCharacter { Owner = owner },
            AiParamType.BigMonsterHoldPosition => new BigMonsterHoldPositionAiCharacter { Owner = owner },

            // Basic behaviors
            AiParamType.Default => new DefaultAiCharacter { Owner = owner },
            AiParamType.Dummy => new DummyAiCharacter { Owner = owner },
            AiParamType.Flytrap => new FlytrapAiCharacter { Owner = owner },
            AiParamType.HoldPosition => new HoldPositionAiCharacter { Owner = owner },
            AiParamType.Roaming => new RoamingAiCharacter { Owner = owner },

            // Special behaviors
            AiParamType.TowerDefenseAttacker => new TowerDefenseAttackerAiCharacter { Owner = owner },

            // Wild boar variants
            AiParamType.WildBoarHoldPosition => new WildBoarHoldPositionAiCharacter { Owner = owner },
            AiParamType.WildBoarRoaming => new WildBoarRoamingAiCharacter { Owner = owner },

            // Unsupported types
            _ => null
        };
    }
}
