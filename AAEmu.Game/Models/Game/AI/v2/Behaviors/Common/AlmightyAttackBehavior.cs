using System;
using System.Collections.Generic;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI.v2.Controls;
using AAEmu.Game.Models.Game.AI.v2.Params.Almighty;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Handles advanced attack behavior for "almighty" NPCs.
/// Manages skill queue, movement, and state transitions during combat.
/// </summary>
public class AlmightyAttackBehavior : BaseCombatBehavior
{
    private const int SkillQueueDelayMs = 150;
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private DateTime _lastTick;
    private AlmightyNpcAiParams _aiParams;
    private bool _isInitialized;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeCombatState();
        _isInitialized = true;
        //Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered almighty attack state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"AlmightyAttackBehavior.Enter: Ai or Owner is null");
            return false;
        }
        return true;
    }

    private void InitializeCombatState()
    {
        Ai.Owner.InterruptSkills();
        _skillQueue = new Queue<AiSkill>();
        Ai.Owner.CurrentGameStance = GameStanceType.Combat;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Combat;
        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);
        _combatStartTime = DateTime.UtcNow;
        if (Ai.Owner is { IsInBattle: false } npc)
        {
            npc.Events.OnCombatStarted(this, new OnCombatStartedArgs { Owner = npc, Target = npc });
        }
        Ai.Param = Ai.Owner.Template.AiParams;
        _lastTick = DateTime.UtcNow;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;
        if (!ThrottleTick())
            return;
        ProcessCombatTick(delta);
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"AlmightyAttackBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }
        if (Ai?.Owner == null)
        {
            Logger.Warn($"AlmightyAttackBehavior.Tick called with null Ai or Owner");
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

    private void ProcessCombatTick(TimeSpan delta)
    {
        var owner = Ai.Owner;
        if (Ai.Param is not AlmightyNpcAiParams aiParams)
            return;
        _aiParams = aiParams;
        var currentTarget = owner.CurrentTarget;

        // Update target and check if we should return
        if (!UpdateTarget() || ShouldReturn)
        {
            Ai.OnNoAggroTarget();
            return;
        }

        // Handle movement if allowed
        if (CanStrafe && !IsUsingSkill)
            MoveInRange(currentTarget, delta);

        // Update phase/pipe if needed
        CheckPipeName();

        // Check if we can use skills
        if (!CanUseSkill)
            return;

        // Throttle skill queue processing
        if (!owner.CheckInterval(SkillQueueDelayMs))
            return;

        // Refresh skill queue if empty
        if (_skillQueue.Count == 0)
        {
            if (!RefreshSkillQueue(_aiParams.AiSkillLists, _aiParams))
                return;
        }

        // Dequeue and use the next skill
        var selectedSkill = _skillQueue.Dequeue();
        if (selectedSkill == null)
            return;
        var skillTemplate = SkillManager.Instance.GetSkillTemplate(selectedSkill.SkillId);
        if (skillTemplate == null)
            return;
        UseSkill(new Skill(skillTemplate), currentTarget, selectedSkill.Delay);
        _strafeDuringDelay = selectedSkill.Strafe;
    }

    public override void Exit()
    {
        // If there are no aggro targets and no path points, return to home position
        if (Ai.Owner.AggroTable.Count == 0 && Ai.PathHandler.AiPathPointsRemaining.Count == 0)
        {
            Ai.PathHandler.TargetPosition = Vector3.Zero;
            Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;
            Ai.Owner.CurrentGameStance = GameStanceType.Combat;
            Ai.PathHandler.AiPathPointsRemaining.Enqueue(new AiPathPoint()
            {
                Action = AiPathPointAction.Speed,
                Param = "3",
                Position = Ai.HomePosition
            });
            Ai.GoToFollowPath();
        }
        _isInitialized = false;
    }
}
