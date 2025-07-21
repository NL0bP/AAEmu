using System;

using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Handles the behavior of an NPC returning to its idle position.
/// Manages movement, health restoration, and state transitions.
/// </summary>
public class ReturnStateBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private const float ReturnTimeout = 20.0f; // Timeout in seconds for return movement
    private const float CompletionDistance = 1.0f; // Distance threshold for return completion
    private const float TeleportThreshold = 2.0f; // Distance threshold for teleport decision
    private const float EmergencyTeleportSpeed = 1000000.0f; // Speed for emergency teleport movement

    private DateTime _timeoutTime;
    private DateTime _lastTick;
    private bool _isInitialized;
    private bool _hasRestoredHealth;

    public override void Enter()
    {
        if (!Validate())
            return;

        InitializeReturnState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered return state");
    }

    private void InitializeReturnState()
    {
        // Clear combat state
        ClearCombatState();

        // Set movement state
        InitializeMovementState();

        // Handle health restoration if needed
        HandleHealthRestoration();

        // Initialize timers
        _timeoutTime = DateTime.UtcNow.AddSeconds(ReturnTimeout);
        _lastTick = DateTime.UtcNow;

        // Handle immediate teleport if configured
        if (ShouldTeleportImmediately())
        {
            OnCompletedReturn();
            return;
        }

        // Handle return state override
        if (Ai.Param is { GoReturnState: false })
        {
            OnCompletedReturnNoTeleport();
        }
    }

    private void ClearCombatState()
    {
        if (!Ai.Owner.AggroTable.IsEmpty)
            Ai.Owner.ClearAllAggro();

        Ai.Owner.SetTarget(null);
        Ai.Owner.IsInBattle = false;
    }

    private void InitializeMovementState()
    {
        Ai.Owner.CurrentGameStance = GameStanceType.Relaxed;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;
        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);
    }

    private void HandleHealthRestoration()
    {
        if (!ShouldRestoreHealth())
            return;

        Ai.Owner.Buffs.AddBuff((uint)BuffConstants.NpcReturn, Ai.Owner);
        RestoreHealth();
        _hasRestoredHealth = true;
    }

    private bool ShouldRestoreHealth()
    {
        return Ai.Param == null || Ai.Param.RestorationOnReturn;
    }

    private void RestoreHealth()
    {
        Ai.Owner.PostUpdateCurrentHp(Ai.Owner, Ai.Owner.Hp, Ai.Owner.MaxHp, KillReason.Unknown);
        Ai.Owner.Hp = Ai.Owner.MaxHp;
        Ai.Owner.Mp = Ai.Owner.MaxMp;
        Ai.Owner.BroadcastPacket(new SCUnitPointsPacket(Ai.Owner.ObjId, Ai.Owner.Hp, Ai.Owner.Mp, Ai.Owner.HighAbilityRsc), true);
    }

    private bool ShouldTeleportImmediately()
    {
        return Ai.Param is { AlwaysTeleportOnReturn: true };
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!Validate() || !Throttle())
            return;

        ProcessReturnMovement(delta);
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"ReturnStateBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }

        return true;
    }

    private void ProcessReturnMovement(TimeSpan delta)
    {
        var moveSpeed = Ai.GetRealMovementSpeed(Ai.Owner.BaseMoveSpeed);
        var moveFlags = Ai.GetRealMovementFlags(moveSpeed);
        moveSpeed *= delta.Milliseconds / 1000.0;

        // Move towards idle position
        Ai.Owner.MoveTowards(Ai.IdlePosition, (float)moveSpeed, moveFlags);

        // Check completion conditions
        var distanceToIdle = MathUtil.CalculateDistance(Ai.IdlePosition, Ai.Owner.Transform.World.Position);

        if (distanceToIdle < CompletionDistance)
        {
            OnCompletedReturnNoTeleport();
            return;
        }

        // Check timeout
        if (DateTime.UtcNow > _timeoutTime)
        {
            OnCompletedReturn();
        }
    }

    private void OnCompletedReturn()
    {
        var distanceToIdle = MathUtil.CalculateDistance(Ai.IdlePosition, Ai.Owner.Transform.World.Position);
        if (distanceToIdle > TeleportThreshold)
        {
            //Ai.Owner.MoveTowards(Ai.IdlePosition, EmergencyTeleportSpeed);
            Ai.Owner.Transform.Local.SetPosition(Ai.IdlePosition); // Teleport to idle position
            Ai.Owner.StopMovement();
        }

        OnCompletedReturnNoTeleport();
    }

    public void OnCompletedReturnNoTeleport()
    {
        //Logger.Debug($"Unit {Ai.Owner.ObjId} completed return movement");
        Ai.GoToIdle();
    }

    public override void Exit()
    {
        try
        {
            if (!_isInitialized || Ai?.Owner == null)
                return;

            if (_hasRestoredHealth)
                Ai.Owner.Buffs.RemoveBuff((uint)BuffConstants.NpcReturn);

            Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, true), false);
        }
        finally
        {
            _isInitialized = false;
        }
    }
}
