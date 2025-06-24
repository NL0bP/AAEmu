using System;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Behavior for AI that does nothing. Used for passive or idle states.
/// Sets the unit to a relaxed stance and idle alertness.
/// </summary>
public class DoNothingBehavior : BaseCombatBehavior
{
    /// <summary>
    /// Called when entering the do-nothing state.
    /// Sets the owner's stance and alertness to idle/relaxed.
    /// </summary>
    public override void Enter()
    {
        if (Ai?.Owner == null)
            return;
        Ai.Owner.CurrentGameStance = GameStanceType.Relaxed;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;
    }

    /// <summary>
    /// Tick update for do-nothing state. No behavior is performed.
    /// </summary>
    /// <param name="delta">Time elapsed since last tick</param>
    public override void Tick(TimeSpan delta)
    {
        // No behavior needed in do-nothing state
    }

    /// <summary>
    /// Called when exiting the do-nothing state. No cleanup needed.
    /// </summary>
    public override void Exit()
    {
        // No cleanup needed for do-nothing state
    }
}
