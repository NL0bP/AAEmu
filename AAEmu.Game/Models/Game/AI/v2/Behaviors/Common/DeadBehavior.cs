using System;

using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Represents the behavior of an NPC when it dies.
/// Handles death state, cleanup of combat status, and death events.
/// </summary>
public class DeadBehavior : BaseCombatBehavior
{
    private const int MinimumDeadHp = 0;
    private const float DeadStateCheckInterval = 0.5f; // Check dead state every 500ms

    private bool _isInitialized;
    private DateTime _lastStateCheck;
    private bool _deathEventsTriggered;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeDeadState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered dead state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"DeadBehavior.Enter called with null Ai or Owner");
            return false;
        }
        return true;
    }

    private void InitializeDeadState()
    {
        // Stop all active actions
        StopActiveActions();
        // Clear combat state
        ClearCombatState();
        // Trigger death events if not already triggered
        TriggerDeathEvents();
        _lastStateCheck = DateTime.UtcNow;
    }

    private void StopActiveActions()
    {
        Ai.Owner.InterruptSkills();
        Ai.Owner.StopMovement();
    }

    private void ClearCombatState()
    {
        Ai.Owner.ClearAllAggro();
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;
        Ai.Owner.CurrentTarget = null;
        Ai.AlreadyTargeted = false;
    }

    private void TriggerDeathEvents()
    {
        if (_deathEventsTriggered)
            return;
        if (Ai.Owner is { } npc)
        {
            var deathArgs = new OnDeathArgs
            {
                Killer = npc,
                Victim = npc
            };
            npc.Events.OnDeath(this, deathArgs);
            _deathEventsTriggered = true;
        }
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;
        if (!ShouldCheckState())
            return;
        VerifyDeadState();
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"DeadBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }
        if (Ai?.Owner == null)
        {
            Logger.Warn($"DeadBehavior.Tick called with null Ai or Owner");
            return false;
        }
        return true;
    }

    private bool ShouldCheckState()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastStateCheck).TotalSeconds < DeadStateCheckInterval)
            return false;
        _lastStateCheck = now;
        return true;
    }

    private void VerifyDeadState()
    {
        // Ensure the unit stays in dead state
        if (Ai.Owner.Hp > MinimumDeadHp)
        {
            Logger.Warn($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} in dead state but HP > 0, forcing HP to 0");
            Ai.Owner.Hp = MinimumDeadHp;
        }
        // Ensure combat flags are cleared
        if (Ai.Owner.IsInBattle)
        {
            Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} in dead state but still marked as in battle, clearing state");
            Ai.Owner.IsInBattle = false;
        }
        // Ensure no target is selected
        if (Ai.Owner.CurrentTarget != null)
        {
            Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} in dead state but has target, clearing target");
            Ai.Owner.CurrentTarget = null;
        }
    }

    public override void Exit()
    {
        if (!_isInitialized)
            return;
        Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting dead state");
        _isInitialized = false;
        _deathEventsTriggered = false;
    }
}
