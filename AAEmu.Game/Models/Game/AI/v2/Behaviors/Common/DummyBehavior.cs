using System;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Behavior for dummy (inactive or placeholder) AI states.
/// Sets the unit to a relaxed stance and idle alertness, but performs no actions.
/// </summary>
public class DummyBehavior : BaseCombatBehavior
{
    /// <summary>
    /// Called when entering the dummy state.
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
    /// Tick update for dummy state. No behavior is performed.
    /// </summary>
    /// <param name="delta">Time elapsed since last tick</param>
    public override void Tick(TimeSpan delta)
    {
        // No behavior needed in dummy state
    }

    /// <summary>
    /// Called when exiting the dummy state. No cleanup needed.
    /// </summary>
    public override void Exit()
    {
        // No cleanup needed for dummy state
    }
}
