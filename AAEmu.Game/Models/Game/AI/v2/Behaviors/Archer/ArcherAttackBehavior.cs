using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI.v2.Params.Archer;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Archer;

/// <summary>
/// Represents the attack behavior for archer-type NPCs.
/// Handles combat phases, ranged combat positioning, and skill selection.
/// </summary>
public class ArcherAttackBehavior : BaseCombatBehavior
{
    private const float MinGapDistance = 1.0f;
    private const float MinimumTickInterval = 0.1f;

    private enum ArcherPhase
    {
        Base,
        TryingMeleeSkill,
        UsedMeleeSkill,
        TryingRangedDefSkill,
        UsedRangedDefSkill,
        TryingMakeAGapSkill,
        NeedMakeAGap
    }

    private ArcherPhase _currentPhase;
    private int _makeAGapCount;
    private bool _isInitialized;
    private DateTime _lastTick;

    public override void Enter()
    {
        if (!Validate())
            return;

        InitializeCombatState();
        _isInitialized = true;
        Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered archer attack state");
    }

    private void InitializeCombatState()
    {
        // Initialize combat state
        _currentPhase = ArcherPhase.Base;
        _makeAGapCount = 0;
        _lastTick = DateTime.UtcNow;

        // Stop current actions and set combat state
        Ai.Owner.InterruptSkills();
        Ai.Owner.CurrentGameStance = GameStanceType.Combat;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Combat;
        Ai.Owner.BroadcastPacket(new SCUnitModelPostureChangedPacket(Ai.Owner, Ai.Owner.AnimActionId, false), false);

        // Set battle state and trigger events
        Ai.Owner.IsInBattle = true;
        if (Ai.Owner is { } npc)
        {
            npc.Events.OnCombatStarted(this, new OnCombatStartedArgs { Owner = npc, Target = npc });
        }
        Ai.Param = Ai.Owner.Template.AiParams;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;

        if (!Validate() || !Throttle())
            return;

        // Ensure we have archer AI parameters
        Ai.Param ??= new ArcherAiParams("");
        if (Ai.Param is not ArcherAiParams aiParams)
            return;

        // Update target and check return conditions
        if (!UpdateTarget() || ShouldReturn)
        {
            Ai.OnNoAggroTarget();
            return;
        }

        // Process behavior based on current phase
        if (_currentPhase == ArcherPhase.NeedMakeAGap)
        {
            HandleMakeAGapPhase(aiParams, delta);
        }
        else
        {
            HandleCombatPhase(aiParams, delta);
        }
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"ArcherAttackBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }

        return true;
    }

    private void HandleMakeAGapPhase(ArcherAiParams aiParams, TimeSpan delta)
    {
        var idlePosition = Ai.IdlePosition;
        var npcPosition = Ai.Owner.Transform.World.Position;
        var targetPosition = Ai.Owner.CurrentTarget.Transform.World.Position;

        // Calculate movement parameters
        var moveSpeed = Ai.GetRealMovementSpeed(Ai.Owner.BaseMoveSpeed);
        var moveFlags = Ai.GetRealMovementFlags(moveSpeed);
        moveSpeed *= delta.Milliseconds / 1000.0;

        // Move towards idle position
        Ai.Owner.MoveTowards(idlePosition, (float)moveSpeed, moveFlags);

        // Check if we've created enough gap
        var distanceToIdle = MathUtil.CalculateDistance(npcPosition, idlePosition, true);
        var distanceToTarget = MathUtil.CalculateDistance(npcPosition, targetPosition, true);

        if (distanceToIdle < MinGapDistance || distanceToTarget > aiParams.PreferedCombatDist)
        {
            CompleteGapCreation();
        }
    }

    private void CompleteGapCreation()
    {
        Ai.Owner.StopMovement();
        _makeAGapCount++;
        _currentPhase = ArcherPhase.TryingRangedDefSkill;
        Logger.Debug($"Unit {Ai.Owner.ObjId} completed gap creation, attempts: {_makeAGapCount}");
    }

    private void HandleCombatPhase(ArcherAiParams aiParams, TimeSpan delta)
    {
        // Handle movement if possible
        if (CanStrafe && !IsUsingSkill)
        {
            MoveInRange(Ai.Owner.CurrentTarget, delta);
        }

        // Handle skill usage if possible
        if (!CanUseSkill)
            return;

        _maxWeaponRange = aiParams.PreferedCombatDist;
        var targetDist = Ai.Owner.GetDistanceTo(Ai.Owner.CurrentTarget);
        var selectedSkill = PickSkill(RequestAvailableSkills(aiParams, targetDist));

        UseSelectedSkill(selectedSkill);
    }

    private void UseSelectedSkill(uint skillId)
    {
        if (skillId == 0)
            return;

        var skillTemplate = SkillManager.Instance.GetSkillTemplate(skillId);
        if (skillTemplate == null)
            return;

        UseSkill(new Skill(skillTemplate), Ai.Owner.CurrentTarget, skillTemplate.CastingTime);
        Logger.Debug($"Unit {Ai.Owner.ObjId} using skill {skillId} in phase {_currentPhase}");
    }

    private List<uint> RequestAvailableSkills(ArcherAiParams aiParams, float targetDist)
    {
        var inMeleeRange = targetDist <= aiParams.MeleeAttackRange;
        var skillList = new List<uint>();

        // Handle different phases
        if (_currentPhase == ArcherPhase.UsedMeleeSkill)
        {
            var needMakeAGap = inMeleeRange && _makeAGapCount < aiParams.MaxMakeAGapCount;
            if (needMakeAGap)
            {
                foreach (var skill in aiParams.CombatSkills)
                {
                    skillList.AddRange(skill.MakeAGap.Where(skillId => !Ai.Owner.Cooldowns.CheckCooldown(skillId)));
                }
                _currentPhase = ArcherPhase.TryingMakeAGapSkill;
            }
            else
            {
                _currentPhase = ArcherPhase.Base;
            }
        }
        else if (_currentPhase == ArcherPhase.UsedRangedDefSkill)
        {
            foreach (var skill in aiParams.CombatSkills)
            {
                skillList.AddRange(skill.RangedStrong.Where(skillId => !Ai.Owner.Cooldowns.CheckCooldown(skillId)));
            }
        }

        // If no skills selected, pick based on range
        if (skillList.Count == 0)
        {
            if (inMeleeRange)
            {
                foreach (var skill in aiParams.CombatSkills)
                {
                    skillList.AddRange(skill.Melee.Where(skillId => !Ai.Owner.Cooldowns.CheckCooldown(skillId)));
                }
                _currentPhase = ArcherPhase.TryingMeleeSkill;
            }
            else
            {
                foreach (var skill in aiParams.CombatSkills)
                {
                    skillList.AddRange(skill.RangedDef.Where(skillId => !Ai.Owner.Cooldowns.CheckCooldown(skillId)));
                }
                _currentPhase = ArcherPhase.TryingRangedDefSkill;
            }
        }

        UpdatePhaseAfterSkillUse();
        return skillList;
    }

    private void UpdatePhaseAfterSkillUse()
    {
        _currentPhase = _currentPhase switch
        {
            ArcherPhase.TryingMeleeSkill => ArcherPhase.UsedMeleeSkill,
            ArcherPhase.TryingRangedDefSkill => ArcherPhase.UsedRangedDefSkill,
            ArcherPhase.TryingMakeAGapSkill => ArcherPhase.NeedMakeAGap,
            ArcherPhase.NeedMakeAGap => ArcherPhase.NeedMakeAGap,
            _ => ArcherPhase.Base
        };
    }

    private uint PickSkill(List<uint> skills)
    {
        if (skills.Count > 0)
            return skills[Rand.Next(0, skills.Count)];

        // Fall back to base skill if available
        if (!Ai.Owner.Cooldowns.CheckCooldown((uint)Ai.Owner.Template.BaseSkillId))
            return (uint)Ai.Owner.Template.BaseSkillId;

        return 0;
    }

    public override void Exit()
    {
        if (!_isInitialized || Ai?.Owner == null)
            return;

        Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting archer attack state");
        _isInitialized = false;
    }
}
