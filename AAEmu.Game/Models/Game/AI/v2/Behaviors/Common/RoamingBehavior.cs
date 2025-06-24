using System;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI.Utils;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Represents the roaming behavior for NPCs.
/// Handles random movement within a designated area while checking for combat triggers.
/// </summary>
public class RoamingBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private const float MinimumRoamingDistance = 1.0f; // Minimum distance to consider roaming complete
    private const int MinRoamingDelay = 3; // Minimum seconds between roaming attempts
    private const int MaxRoamingDelay = 6; // Maximum seconds between roaming attempts

    private Vector3 _targetRoamPosition;
    private DateTime _nextRoamingTime;
    private DateTime _lastTick;
    private bool _isInitialized;
    private bool _isMoving;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeRoamingState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered roaming state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"RoamingBehavior.Enter: Ai or Owner is null");
            return false;
        }
        return true;
    }

    private void InitializeRoamingState()
    {
        // Stop current actions
        Ai.Owner.InterruptSkills();

        // Set initial state
        Ai.Owner.CurrentGameStance = GameStanceType.Relaxed;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;

        // Initialize movement state
        _targetRoamPosition = Vector3.Zero;
        _lastTick = DateTime.UtcNow;
        _nextRoamingTime = DateTime.UtcNow;
        _isMoving = false;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!ThrottleTick())
            return;

        ProcessTickActions(delta);
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"RoamingBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }

        if (Ai?.Owner == null)
        {
            Logger.Warn($"RoamingBehavior.Tick called with null Ai or Owner");
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

    private void ProcessTickActions(TimeSpan delta)
    {
        // Check for combat triggers
        if (!CheckCombatTriggers())
        {
            // Process roaming movement if no combat
            ProcessRoamingMovement(delta);
        }
    }

    private bool CheckCombatTriggers()
    {
        // Check aggression first
        if (CheckAggression())
        {
            //Logger.Debug($"Unit {Ai.Owner.ObjId} detected aggression, interrupting roaming");
            return true;
        }

        // Then check alert state
        if (CheckAlert())
        {
            //Logger.Debug($"Unit {Ai.Owner.ObjId} entered alert state, interrupting roaming");
            return true;
        }

        return false;
    }

    private void ProcessRoamingMovement(TimeSpan delta)
    {
        // Check if we need to update roaming position
        if (ShouldUpdateRoaming())
        {
            UpdateRoamingPosition();
        }

        // Process movement if we have a target position
        if (!_targetRoamPosition.Equals(Vector3.Zero))
        {
            MoveTowardsTarget(delta);
        }
    }

    private bool ShouldUpdateRoaming()
    {
        return _targetRoamPosition.Equals(Vector3.Zero) && DateTime.UtcNow > _nextRoamingTime;
    }

    private void UpdateRoamingPosition()
    {
        _targetRoamPosition = AIUtils.CalcNextRoamingPosition(Ai);

        if (!_targetRoamPosition.Equals(Vector3.Zero))
        {
            _isMoving = true;
            Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);
            //Logger.Debug($"Unit {Ai.Owner.ObjId} selected new roaming position at {_targetRoamPosition}");
        }
    }

    private void MoveTowardsTarget(TimeSpan delta)
    {
        var moveSpeed = Ai.GetRealMovementSpeed(Ai.Owner.BaseMoveSpeed);
        var moveFlags = Ai.GetRealMovementFlags(moveSpeed);
        moveSpeed *= delta.Milliseconds / 1000.0;

        // Move towards target position
        Ai.Owner.MoveTowards(_targetRoamPosition, (float)moveSpeed, moveFlags);

        // Check if we reached the target
        var distanceToTarget = MathUtil.CalculateDistance(Ai.Owner.Transform.World.Position, _targetRoamPosition);
        if (distanceToTarget < MinimumRoamingDistance)
        {
            CompleteRoaming();
        }
    }

    private void CompleteRoaming()
    {
        // Stop movement and reset state
        Ai.Owner.StopMovement();
        _targetRoamPosition = Vector3.Zero;
        _nextRoamingTime = DateTime.UtcNow.AddSeconds(Rand.Next(MinRoamingDelay, MaxRoamingDelay));

        // Update animation state
        if (_isMoving)
        {
            Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, true), false);
            _isMoving = false;
        }

        //Logger.Debug($"Unit {Ai.Owner.ObjId} completed roaming movement, next roaming in {(_nextRoamingTime - DateTime.UtcNow).TotalSeconds:F1}s");
    }

    public override void Exit()
    {
        if (!_isInitialized)
            return;

        if (_isMoving)
        {
            Ai.Owner.StopMovement();
        }

        //Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting roaming state");
        _isInitialized = false;
        _isMoving = false;
    }
}
