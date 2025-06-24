using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI.v2.Params.BigMonster;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.BigMonster;

/// <summary>
/// Represents the attack behavior for large monster NPCs.
/// Handles combat mechanics, skill selection based on health thresholds, and movement.
/// </summary>
public class BigMonsterAttackBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private DateTime _lastTick;
    private bool _isInitialized;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeCombatState();
        _isInitialized = true;
        Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered big monster attack state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"BigMonsterAttackBehavior.Enter: Ai or Owner is null");
            return false;
        }
        return true;
    }

    private void InitializeCombatState()
    {
        // Stop current actions
        Ai.Owner.InterruptSkills();

        // Set combat state
        Ai.Owner.CurrentGameStance = GameStanceType.Combat;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Combat;
        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);
        Ai.Owner.IsInBattle = true;

        // Trigger combat event
        if (Ai.Owner is { } npc)
        {
            npc.Events.OnCombatStarted(this, new OnCombatStartedArgs { Owner = npc, Target = npc });
        }

        // Initialize AI parameters
        Ai.Param = Ai.Owner.Template.AiParams;
        _lastTick = DateTime.UtcNow;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;
        if (!ThrottleTick())
            return;

        // Ensure we have big monster AI parameters
        Ai.Param ??= new BigMonsterAiParams("");
        if (Ai.Param is not BigMonsterAiParams aiParams)
            return;

        // Update target and check return conditions
        if (!UpdateTarget() || ShouldReturn)
        {
            Ai.OnNoAggroTarget();
            return;
        }

        // Handle movement if possible
        if (CanStrafe && !IsUsingSkill)
        {
            MoveInRange(Ai.Owner.CurrentTarget, delta);
        }

        // Handle skill usage if possible
        if (!CanUseSkill)
            return;

        ProcessSkillSelection(aiParams);
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"BigMonsterAttackBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }
        if (Ai?.Owner == null)
        {
            Logger.Warn($"BigMonsterAttackBehavior.Tick called with null Ai or Owner");
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

    private void ProcessSkillSelection(BigMonsterAiParams aiParams)
    {
        _strafeDuringDelay = false;
        var targetDist = Ai.Owner.GetDistanceTo(Ai.Owner.CurrentTarget);

        // Get available skills and pick one
        var availableSkills = RequestAvailableSkills(aiParams, targetDist);
        var selectedSkill = PickSkill(availableSkills);

        if (selectedSkill == null)
        {
            // Fall back to base combat skill
            PickSkillAndUseIt(SkillUseConditionKind.InCombat, Ai.Owner.CurrentTarget, targetDist);
            return;
        }

        UseSelectedSkill(selectedSkill);
    }

    private void UseSelectedSkill(AAEmu.Game.Models.Game.AI.V2.Params.BigMonster.BigMonsterCombatSkill skill)
    {
        var skillTemplate = SkillManager.Instance.GetSkillTemplate(skill.SkillType);
        if (skillTemplate == null)
            return;

        UseSkill(new Skill(skillTemplate), Ai.Owner.CurrentTarget, skill.SkillDelay);
        _strafeDuringDelay = skill.StrafeDuringDelay;

        Logger.Debug($"Unit {Ai.Owner.ObjId} using skill {skill.SkillType} with delay {skill.SkillDelay}");
    }

    private List<AAEmu.Game.Models.Game.AI.V2.Params.BigMonster.BigMonsterCombatSkill> RequestAvailableSkills(BigMonsterAiParams aiParams, float targetDist)
    {
        var healthRatio = (int)((float)Ai.Owner.Hp / Ai.Owner.MaxHp * 100);

        // Filter skills based on conditions
        return aiParams.CombatSkills
            .Where(s => s.HealthRangeMin <= healthRatio && healthRatio <= s.HealthRangeMax)
            .Where(s => !Ai.Owner.Cooldowns.CheckCooldown(s.SkillType))
            .Where(s =>
            {
                var template = SkillManager.Instance.GetSkillTemplate(s.SkillType);
                return template != null &&
                       (targetDist >= template.MinRange && targetDist <= template.MaxRange ||
                        template.TargetType == SkillTargetType.Self);
            })
            .ToList();
    }

    private AAEmu.Game.Models.Game.AI.V2.Params.BigMonster.BigMonsterCombatSkill PickSkill(
        List<AAEmu.Game.Models.Game.AI.V2.Params.BigMonster.BigMonsterCombatSkill> skills)
    {
        if (skills.Count > 0)
            return skills[Rand.Next(0, skills.Count)];

        // Fall back to base skill if available
        if (!Ai.Owner.Cooldowns.CheckCooldown((uint)Ai.Owner.Template.BaseSkillId))
        {
            return new AAEmu.Game.Models.Game.AI.V2.Params.BigMonster.BigMonsterCombatSkill
            {
                SkillType = (uint)Ai.Owner.Template.BaseSkillId,
                SkillDelay = Ai.Owner.Template.BaseSkillDelay,
                StrafeDuringDelay = Ai.Owner.Template.BaseSkillStrafe
            };
        }

        return null;
    }

    public override void Exit()
    {
        if (!_isInitialized)
            return;

        Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting big monster attack state");
        _isInitialized = false;
    }
}
