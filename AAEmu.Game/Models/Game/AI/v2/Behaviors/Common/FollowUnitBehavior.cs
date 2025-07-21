using System;

using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// NPC behavior for following another unit.
/// Handles movement, target validation, and state transitions.
/// </summary>
public class FollowUnitBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private DateTime _lastTick;
    private bool _isInitialized;

    public override void Enter()
    {
        if (!Validate())
            return;

        InitializeFollowState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} started following a unit");
    }

    private void InitializeFollowState()
    {
        Ai.Owner.CurrentGameStance = GameStanceType.Relaxed;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;
        _lastTick = DateTime.UtcNow;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!Validate() || !Throttle())
            return;

        if (!UpdateTarget())
            Ai.Owner.SetTarget(null);

        if (CheckAggression() || CheckAlert())
            return;

        if (!ValidateFollowTarget())
            return;

        ProcessFollowMovement(delta);
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
            return false;

        return true;
    }

    private bool ValidateFollowTarget()
    {
        var target = Ai.AiFollowUnitObj;
        // If the target is invalid, stop following and go to idle
        if (target == null || target.Hp <= 0 || target.IsDead)
        {
            Logger.Debug($"Unit {Ai.Owner.ObjId} lost follow target, switching to Idle");
            Ai.AiFollowUnitObj = null;
            Ai.GoToIdle();
            return false;
        }
        return true;
    }

    private void ProcessFollowMovement(TimeSpan delta)
    {
        var target = Ai.AiFollowUnitObj;
        if (target == null)
        {
            Logger.Debug($"Unit {Ai.Owner.ObjId} lost follow target, switching to Idle");
            Ai.AiFollowUnitObj = null;
            Ai.GoToIdle();
            return;
        }
        // Calculate distance to the target
        var targetDistance = Ai.Owner.GetDistanceTo(target, true);
        // Adjust speed based on distance (up to a maximum multiplier)
        var followSpeedMultiplier = (float)Math.Min(5.0, targetDistance / 1.5);
        var moveSpeed = Ai.GetRealMovementSpeed(Ai.Owner.BaseMoveSpeed) * followSpeedMultiplier;
        var moveFlags = Ai.GetRealMovementFlags(moveSpeed);
        moveSpeed *= (delta.Milliseconds / 1000.0);
        // Move towards the target
        Ai.Owner.MoveTowards(target.Transform.World.Position, (float)moveSpeed, moveFlags);
        // Update idle position to current position
        Ai.IdlePosition = Ai.Owner.Transform.World.Position;
    }

    public override void Exit()
    {
        if (!_isInitialized || Ai?.Owner == null)
            return;

        _isInitialized = false;
    }
}
