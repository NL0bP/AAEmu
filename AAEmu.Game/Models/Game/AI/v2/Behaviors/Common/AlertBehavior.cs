using System;
using System.Numerics;

using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Represents the alert state behavior for NPCs.
/// Handles target awareness, rotation tracking, and transitions between alert and combat states.
/// </summary>
public class AlertBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private const string DefaultPipeName = "phase_dragon_ground";

    private DateTime _lastTick;
    private bool _isInitialized;
    private Vector3 _originalRotation;
    private bool _isTracking;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeAlertState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered alert state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"AlertBehavior.Enter: Ai or Owner is null");
            return false;
        }
        return true;
    }

    private void InitializeAlertState()
    {
        // Stop current actions
        Ai.Owner.InterruptSkills();
        Ai.Owner.StopMovement();

        // Set combat state
        Ai.Owner.CurrentGameStance = GameStanceType.Combat;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Alert;

        // Set visual state
        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);

        // Initialize tracking
        InitializeTargetTracking();

        // Trigger alert event
        TriggerAlertEvent();

        // Set phase name for special behaviors
        _pipeName = DefaultPipeName;
        CheckPipeName();
    }

    private void InitializeTargetTracking()
    {
        if (Ai.Owner.CurrentTarget != null)
        {
            _originalRotation = Ai.Owner.Transform.Local.Rotation;
            Ai.Owner.LookTowards(Ai.Owner.CurrentTarget.Transform.World.Position);
            _isTracking = true;
        }
        else
        {
            _isTracking = false;
        }
    }

    private void TriggerAlertEvent()
    {
        if (Ai.Owner is { } npc)
        {
            npc.Events.InAlert(this, new InAlertArgs { Npc = npc });
        }
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!ThrottleTick())
            return;

        ProcessAlertState();
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"AlertBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }

        if (Ai?.Owner == null)
        {
            Logger.Warn($"AlertBehavior.Tick called with null Ai or Owner");
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

    private void ProcessAlertState()
    {
        // Update target tracking if we have a target
        UpdateTargetTracking();

        // Check if alert state has expired
        if (ShouldExitAlertState())
        {
            CompleteAlertState();
            return;
        }

        // Check for aggression triggers
        CheckAggression();
    }

    private void UpdateTargetTracking()
    {
        if (Ai.Owner.CurrentTarget != null && _isTracking)
        {
            Ai.Owner.LookTowards(Ai.Owner.CurrentTarget.Transform.World.Position);
        }
    }

    private bool ShouldExitAlertState()
    {
        return DateTime.UtcNow > Ai._alertEndTime && Ai.Owner.SkillTask == null;
    }

    private void CompleteAlertState()
    {
        // Reset rotation and clear target
        if (_isTracking)
        {
            Ai.Owner.Transform.Local.SetRotation(_originalRotation.X, _originalRotation.Y, _originalRotation.Z);
        }

        Ai.Owner.SetTarget(null);
        //Logger.Debug($"Unit {Ai.Owner.ObjId} alert state expired, transitioning to idle");
        Ai.GoToIdle();
    }

    public override void Exit()
    {
        if (!_isInitialized)
            return;

        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, true), false);

        //Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting alert state");
        _isInitialized = false;
        _isTracking = false;
    }
}
