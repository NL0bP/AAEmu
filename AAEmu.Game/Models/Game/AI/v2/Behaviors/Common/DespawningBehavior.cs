using System;

using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Behavior for handling NPC despawning.
/// Triggers despawn events and cleanup when the NPC is being removed from the world.
/// </summary>
public class DespawningBehavior : BaseCombatBehavior
{
    /// <summary>
    /// Called when entering the despawn state.
    /// Triggers the OnDespawn event for cleanup and notification.
    /// </summary>
    public override void Enter()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"DespawningBehavior.Enter: Ai or Owner is null");
            return;
        }

        // Trigger despawn event
        if (Ai.Owner is { } npc)
        {
            Logger.Debug($"NPC {npc.ObjId}:{npc.TemplateId} despawning");
            npc.Events.OnDespawn(this, new OnDespawnArgs { Npc = npc });
        }
    }

    /// <summary>
    /// Tick update for despawn state.
    /// No behavior needed during despawn.
    /// </summary>
    /// <param name="delta">Time elapsed since last tick</param>
    public override void Tick(TimeSpan delta)
    {
        // No behavior needed during despawn
    }

    /// <summary>
    /// Called when exiting the despawn state.
    /// No cleanup needed as the NPC is being removed.
    /// </summary>
    public override void Exit()
    {
        // No cleanup needed for despawn state
    }
}
