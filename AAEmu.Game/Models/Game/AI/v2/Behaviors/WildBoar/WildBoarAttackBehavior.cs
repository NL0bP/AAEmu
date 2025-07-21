using System;
using System.Linq;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI.v2.Params.WildBoar;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.WildBoar;

/// <summary>
/// Represents attack behavior for wild boar NPCs.
/// Handles special combat start skills, spurt skills based on health thresholds, and movement.
/// </summary>
public class WildBoarAttackBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks

    private WildBoarAiParams _aiParams;
    private float _currentHealth;
    private bool _isInitialized;
    private bool _combatStartSkillUsed;
    private DateTime _lastTick;

    public override void Enter()
    {
        if (!Validate())
            return;

        InitializeCombatState();
        _isInitialized = true;
        Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered wild boar attack state");
    }

    private void InitializeCombatState()
    {
        // Initialize AI parameters
        Ai.Param = Ai.Owner.Template.AiParams;

        // Set combat state
        Ai.Owner.CurrentGameStance = GameStanceType.Combat;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Combat;
        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);

        // Trigger combat event
        if (Ai.Owner is { } npc)
        {
            npc.Events.OnCombatStarted(this, new OnCombatStartedArgs { Owner = npc, Target = npc });
        }

        // Set initial state
        Ai.Owner.IsInBattle = true;
        _lastTick = DateTime.UtcNow;
        _combatStartSkillUsed = false;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!Validate() || !Throttle())
            return;

        // Update parameters and state
        if (!UpdateAiParameters())
            return;

        // Update health state
        _currentHealth = Ai.Owner.Hp / (float)Ai.Owner.MaxHp * 100;

        // Check target and return conditions
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
        if (CanUseSkill)
        {
            ProcessSkillUsage();
        }
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"WildBoarAttackBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }

        return true;
    }

    private bool UpdateAiParameters()
    {
        _aiParams = Ai.Param as WildBoarAiParams ?? new WildBoarAiParams("");
        return true;
    }

    private void ProcessSkillUsage()
    {
        // Try to use combat start skill if not used yet
        if (!_combatStartSkillUsed)
        {
            if (TryUseCombatStartSkill())
            {
                _combatStartSkillUsed = true;
                return;
            }
        }

        var targetDist = Ai.Owner.GetDistanceTo(Ai.Owner.CurrentTarget);

        // Check for spurt skills based on health conditions
        if (_aiParams?.OnSpurtSkills == null || _aiParams.OnSpurtSkills.Count == 0)
        {
            ProcessSpurtSkills(targetDist);
        }
        else
        {
            // Use default combat skill if no spurt skills available
            PickSkillAndUseIt(SkillUseConditionKind.InCombat, Ai.Owner.CurrentTarget, targetDist);
        }
    }

    private bool TryUseCombatStartSkill()
    {
        var startCombatSkillId = _aiParams.OnCombatStartSkills.FirstOrDefault();
        if (startCombatSkillId == 0)
            return false;

        Ai.Owner.StopMovement();
        var skillTemplate = SkillManager.Instance.GetSkillTemplate(startCombatSkillId);
        if (skillTemplate == null)
            return false;

        var skill = new Skill(skillTemplate);
        UseSkill(skill, Ai.Owner.CurrentTarget);
        Logger.Debug($"Unit {Ai.Owner.ObjId} used combat start skill {startCombatSkillId}");
        return true;
    }

    private void ProcessSpurtSkills(float targetDist)
    {
        foreach (var skillData in _aiParams.OnSpurtSkills)
        {
            // Check if health threshold is met
            if (_currentHealth >= skillData.HealthCondition)
                continue;

            var skillTemplate = SkillManager.Instance.GetSkillTemplate(skillData.SkillType);
            if (skillTemplate == null)
                continue;

            var skill = new Skill(skillTemplate);

            // Check if target is in range for the skill
            if (targetDist < skill.Template.MinRange || targetDist > skill.Template.MaxRange)
                continue;

            // Try to use spurt skill
            SetWeaponRange(skill, Ai.Owner.CurrentTarget);
            var result = UseSkill(skill, Ai.Owner.CurrentTarget);

            // Fall back to default combat skill if on cooldown
            if (result == SkillResult.CooldownTime)
            {
                PickSkillAndUseIt(SkillUseConditionKind.InCombat, Ai.Owner.CurrentTarget, targetDist);
            }

            return;
        }

        // Use default combat skill if no spurt skills were used
        PickSkillAndUseIt(SkillUseConditionKind.InCombat, Ai.Owner.CurrentTarget, targetDist);
    }

    public override void Exit()
    {
        if (!_isInitialized || Ai?.Owner == null)
            return;

        Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting wild boar attack state");
        _isInitialized = false;
    }
}
