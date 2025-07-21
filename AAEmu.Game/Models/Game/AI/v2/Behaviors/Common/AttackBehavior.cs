using System;

using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Represents standard attack behavior for NPCs.
/// Handles combat stance, movement, and skill usage while in combat.
/// </summary>
public class AttackBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms minimum between ticks
    private DateTime _lastTick;
    private bool _isInitialized;

    public override void Enter()
    {
        if (!Validate())
            return;

        InitializeCombatState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered attack state");
    }

    private void InitializeCombatState()
    {
        // Interrupt any current skills and set combat state
        Ai.Owner.InterruptSkills();
        Ai.Owner.CurrentGameStance = GameStanceType.Combat;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Combat;
        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);
        Ai.Owner.IsInBattle = true;

        // Trigger combat started event
        if (Ai.Owner is { } npc)
        {
            npc.Events.OnCombatStarted(this, new OnCombatStartedArgs { Owner = npc, Target = npc });
        }
        _lastTick = DateTime.UtcNow;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!Validate() || !Throttle())
            return;

        if (!UpdateCombatTarget())
            return;

        ProcessCombatActions(delta);
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
            return false;

        return true;
    }

    private bool UpdateCombatTarget()
    {
        // Check target validity and return conditions
        if (!UpdateTarget() || ShouldReturn)
        {
            //Logger.Debug($"AI {Ai.Owner.ObjId} lost target or should return. Transitioning out of combat.");
            Ai.OnNoAggroTarget();
            return false;
        }
        return true;
    }

    private void ProcessCombatActions(TimeSpan delta)
    {
        // Handle movement if we can strafe and aren't using a skill
        if (CanStrafe && !IsUsingSkill)
        {
            ProcessCombatMovement(delta);
        }
        // Handle skill usage if we can use skills
        if (CanUseSkill)
        {
            ProcessSkillUsage();
        }
    }

    private void ProcessCombatMovement(TimeSpan delta)
    {
        // Move within range of target while considering combat conditions
        if (Ai.Owner.CurrentTarget != null)
        {
            MoveInRange(Ai.Owner.CurrentTarget, delta);
        }
    }

    private void ProcessSkillUsage()
    {
        // Use a skill if a target is available
        if (Ai.Owner.CurrentTarget != null)
        {
            var targetDist = Ai.Owner.GetDistanceTo(Ai.Owner.CurrentTarget);
            PickSkillAndUseIt(SkillUseConditionKind.InCombat, Ai.Owner.CurrentTarget, targetDist);
        }
    }

    public override void Exit()
    {
        if (!_isInitialized || Ai?.Owner == null)
            return;

        _isInitialized = false;
    }
}
