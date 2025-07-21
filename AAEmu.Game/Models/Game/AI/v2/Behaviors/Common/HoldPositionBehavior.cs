using System;

using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Represents the behavior of an NPC when holding its position.
/// Handles skill usage, aggression checks, and following the nearest NPC if needed.
/// </summary>
public class HoldPositionBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private const float SkillCheckInterval = 1.0f; // 1 second between skill checks

    private DateTime _lastTick;
    private DateTime _lastSkillCheck;
    private bool _isInitialized;
    private bool _isFollowingNpc;

    public override void Enter()
    {
        if (!Validate())
            return;

        InitializeHoldPosition();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered hold position state");
    }

    private void InitializeHoldPosition()
    {
        // Set stance and alertness
        Ai.Owner.CurrentGameStance = GameStanceType.Relaxed;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;
        // Stop all current actions
        Ai.Owner.InterruptSkills();
        Ai.Owner.StopMovement();
        Ai.Owner.CurrentTarget = Ai.Owner;
        // Initialize timers and state
        _lastTick = DateTime.UtcNow;
        _lastSkillCheck = DateTime.UtcNow;
        _isFollowingNpc = false;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!Validate() || !Throttle())
            return;

        ProcessTickActions(delta);
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"HoldPositionBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }

        return true;
    }

    private void ProcessTickActions(TimeSpan delta)
    {
        // Handle skill usage if possible
        ProcessSkillUsage();

        // Check for aggression or alert state
        if (CheckCombatStates())
            return;

        // If not following an NPC, try to follow the nearest one
        if (!_isFollowingNpc)
        {
            ProcessNpcFollowing();
        }
    }

    private void ProcessSkillUsage()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastSkillCheck).TotalSeconds < SkillCheckInterval)
            return;

        _lastSkillCheck = now;
        if (Ai.Owner.CurrentTarget != null)
        {
            var targetDist = Ai.Owner.GetDistanceTo(Ai.Owner.CurrentTarget);
            PickSkillAndUseIt(SkillUseConditionKind.InIdle, Ai.Owner, targetDist);

            Ai.GoToTalk();
        }
    }

    private bool CheckCombatStates()
    {
        // Check for aggression
        if (CheckAggression())
        {
            _isFollowingNpc = false;
            //Logger.Debug($"Unit {Ai.Owner.ObjId} switched to aggression state");
            return true;
        }

        // Check for alert state
        if (CheckAlert())
        {
            _isFollowingNpc = false;
            //Logger.Debug($"Unit {Ai.Owner.ObjId} switched to alert state");
            return true;
        }

        return false;
    }

    private void ProcessNpcFollowing()
    {
        // Try to follow the nearest NPC if possible
        if (Ai.DoFollowDefaultNearestNpc())
        {
            _isFollowingNpc = true;
            //Logger.Debug($"Unit {Ai.Owner.ObjId} started following the nearest NPC");
        }
    }

    public override void Exit()
    {
        if (!_isInitialized || Ai?.Owner == null)
            return;

        //Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exited hold position state");
        _isInitialized = false;
        _isFollowingNpc = false;
    }
}
