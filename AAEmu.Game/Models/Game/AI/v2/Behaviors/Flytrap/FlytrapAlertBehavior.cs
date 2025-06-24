using System;

using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Flytrap;

/// <summary>
/// Represents alert behavior for flytrap-type NPCs.
/// Handles alert state transitions, posture changes, and combat initiation.
/// </summary>
public class FlytrapAlertBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private DateTime _lastTick;
    private bool _isInitialized;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeAlertState();
        _isInitialized = true;
        Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered flytrap alert state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"FlytrapAlertBehavior.Enter: Ai or Owner is null");
            return false;
        }
        return true;
    }

    private void InitializeAlertState()
    {
        // Stop current actions
        Ai.Owner.InterruptSkills();

        // Set alert state and posture
        Ai.Owner.CurrentGameStance = GameStanceType.Combat;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Alert;
        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);

        // Trigger alert event
        if (Ai.Owner is { } npc)
        {
            npc.Events.InAlert(this, new InAlertArgs { Npc = npc });
        }

        _lastTick = DateTime.UtcNow;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;
        if (!ThrottleTick())
            return;

        // Check for combat triggers
        CheckAggression();
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"FlytrapAlertBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }
        if (Ai?.Owner == null)
        {
            Logger.Warn($"FlytrapAlertBehavior.Tick called with null Ai or Owner");
            return false;
        }
        return true;
    }

    private bool ThrottleTick()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTick).TotalSeconds < MinimumTickInterval)
            return false;
        _lastTick = now;
        return true;
    }

    public override void Exit()
    {
        if (!_isInitialized)
            return;

        Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting flytrap alert state");
        _isInitialized = false;
    }
}
