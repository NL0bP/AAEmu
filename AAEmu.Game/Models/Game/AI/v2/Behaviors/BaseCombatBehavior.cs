using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.AI.v2.Params;
using AAEmu.Game.Models.Game.AI.v2.Params.Almighty;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.SkillControllers;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Utils;

using Point = AAEmu.Game.Models.Game.AI.AStar.Point;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors;

/// <summary>
/// Base class for combat-related AI behaviors. Provides common functionality for combat movement,
/// target selection, and skill usage.
/// </summary>
public abstract class BaseCombatBehavior : Behavior
{
    // Distance and range constants
    private const float DefaultReturnDistance = 50f;
    private const float DefaultAbsoluteReturnDistance = 200f;
    private const float DefaultMeleeAttackRangeAdjustment = 1f;
    private const float DefaultFlyingHeightAdjustment = 15f;

    // Internal state
    internal bool _strafeDuringDelay;
    internal DateTime _combatStartTime;
    internal Queue<AiSkill> _skillQueue;
    internal string _pipeName;

    private uint _phaseType;
    private bool _startingSkillAlreadyUsed;

    protected BaseCombatBehavior()
    {
        _skillQueue = new Queue<AiSkill>();
        _combatStartTime = DateTime.UtcNow;
        _delayEnd = DateTime.MinValue;
    }

    /// <summary>
    /// Moves the AI owner towards the target while respecting combat conditions and range requirements
    /// </summary>
    /// <param name="target">The target unit to move towards</param>
    /// <param name="delta">Time elapsed since last update</param>
    protected void MoveInRange(BaseUnit target, TimeSpan delta)
    {
        if (!ValidateMoveConditions(target))
            return;

        var range = CalculateAttackRange();
        var currentPosition = Ai.Owner.Transform.Local.ClonePosition();
        var targetPosition = target.Transform.Local.ClonePosition();
        var (speed, moveFlags) = CalculateMovementParameters(delta);
        var distanceToTarget = Ai.Owner.GetDistanceTo(target, true);

        // Ensure the distance to the target is not less than 4 units before moving
        if (distanceToTarget < 1.5f)
            return;

        HandleMovement(currentPosition, targetPosition, speed, moveFlags, range, distanceToTarget);
    }

    private bool ValidateMoveConditions(BaseUnit target)
    {
        if (Ai?.Owner == null || target == null)
            return false;

        if (IsUnitImmobilized())
            return false;

        if (IsUnitCasting())
            return false;

        return true;
    }

    private bool IsUnitImmobilized()
    {
        return Ai.Owner.Buffs.HasEffectsMatchingCondition(e =>
                e.Template.Stun
                || e.Template.Sleep
                || e.Template.Root
                || e.Template.Knockdown
                || e.Template.Fastened)
            || Ai.Owner.IsDead
            || Ai.Owner.Buffs.CheckBuffs(SkillManager.Instance.GetBuffsByTagId((uint)SkillConstants.Shackle))
            || Ai.Owner.Buffs.CheckBuffs(SkillManager.Instance.GetBuffsByTagId((uint)SkillConstants.Snare));
    }

    private bool IsUnitCasting()
    {
        return (Ai.Owner.ActiveSkillController?.State ?? SkillController.SCState.Ended) == SkillController.SCState.Running;
    }

    private float CalculateAttackRange()
    {
        var range = Ai.Owner.Template.AttackStartRangeScale;
        if (Ai.Owner.Template.UseRangeMod && _maxWeaponRange != 0)
        {
            range *= _maxWeaponRange;
        }

        // Special case adjustment for melee attacks
        if (Ai.Owner.Template.BaseSkillId == 2 && Ai.Owner.Template.Skills.Count == 0 && range <= 4)
        {
            range -= DefaultMeleeAttackRangeAdjustment;
        }

        return range;
    }

    private (float speed, byte flags) CalculateMovementParameters(TimeSpan delta)
    {
        var speed = Ai.GetRealMovementSpeed(Ai.Owner.BaseMoveSpeed);
        var flags = Ai.GetRealMovementFlags(speed);
        speed *= delta.Milliseconds / 1000.0;
        return ((float)speed, flags);
    }

    private void HandleMovement(Vector3 currentPosition, Vector3 targetPosition, float speed, byte moveFlags, float range, float distanceToTarget)
    {
        if (AppConfiguration.Instance.World.GeoDataMode && Ai.Owner.Transform.WorldId > 0)
        {
            HandleGeoDataMovement(currentPosition, targetPosition, speed, moveFlags, range, distanceToTarget);
        }
        else
        {
            HandleDirectMovement(targetPosition, speed, moveFlags, range, distanceToTarget);
        }
    }

    private void HandleGeoDataMovement(Vector3 currentPosition, Vector3 targetPosition, float speed, byte moveFlags, float range, float distanceToTarget)
    {
        UpdatePathIfNeeded(targetPosition);

        if (Ai.PathNode?.findPath.Count > 0 && !Ai.PathNode.findPath[0].Equals(Point.Zero))
        {
            HandlePathMovement(currentPosition, speed, moveFlags, range);
        }
        else
        {
            HandleDirectMovement(targetPosition, speed, moveFlags, range, distanceToTarget);
        }
    }

    private void UpdatePathIfNeeded(Vector3 targetPosition)
    {
        if (Ai.PathNode?.pos2 == null || !Ai.PathNode.pos2.Equals(new Point(targetPosition.X, targetPosition.Y, targetPosition.Z)))
        {
            if (Ai.Owner.CurrentTarget is Unit targetUnit)
            {
                var sw = Stopwatch.StartNew();
                Ai.Owner.FindPath(targetUnit);
                sw.Stop();

                if (sw.Elapsed.Ticks >= TimeSpan.TicksPerMillisecond)
                {
                    Logger.Warn($"FindPath took {sw.Elapsed} for Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId}");
                }
            }

            if (Ai.PathNode != null)
            {
                Ai.PathNode.pos2 = new Point(targetPosition.X, targetPosition.Y, targetPosition.Z);
            }
        }
    }

    private void HandlePathMovement(Vector3 currentPosition, float speed, byte moveFlags, float range)
    {
        var pathPosition = new Vector3(Ai.PathNode.Position.X, Ai.PathNode.Position.Y, Ai.PathNode.Position.Z);
        var distanceToPathPoint = MathUtil.CalculateDistance(currentPosition, pathPosition, true);

        if (distanceToPathPoint > range)
        {
            Ai.Owner.MoveTowards(pathPosition, speed, moveFlags);
        }
        else
        {
            AdvanceToNextPathPoint();
        }
    }

    private void AdvanceToNextPathPoint()
    {
        Ai.PathNode.Current++;
        if (Ai.PathNode.Current >= Ai.PathNode.findPath.Count)
        {
            Ai.Owner.StopMovement();
            Ai.PathNode.findPath = [];
            return;
        }

        Ai.PathNode.Position = Ai.PathNode.findPath[(int)Ai.PathNode.Current];
    }

    private void HandleDirectMovement(Vector3 targetPosition, float speed, byte moveFlags, float range, float distanceToTarget)
    {
        if (distanceToTarget > range)
        {
            Ai.Owner.MoveTowards(targetPosition, speed, moveFlags);
        }
        else
        {
            Ai.Owner.StopMovement();
        }
    }

    #region State Properties

    /// <summary>
    /// Gets whether the unit can strafe based on delay and configuration
    /// </summary>
    protected bool CanStrafe => DateTime.UtcNow > _delayEnd || _strafeDuringDelay;

    /// <summary>
    /// Gets whether the unit is currently using a skill
    /// </summary>
    protected bool IsUsingSkill => Ai.Owner.SkillTask != null || Ai.Owner.ActivePlotState != null;

    /// <summary>
    /// Gets whether the unit can use skills based on current state and conditions
    /// </summary>
    protected bool CanUseSkill
    {
        get
        {
            if (Ai?.Owner == null)
                return false;

            if (IsUsingSkill)
                return false;

            if ((Ai.Owner.ActiveSkillController?.State ?? SkillController.SCState.Ended) == SkillController.SCState.Running)
                return false;

            if (Ai.Owner.Buffs.HasEffectsMatchingCondition(e => e.Template.Stun || e.Template.Sleep || e.Template.Silence))
                return false;

            return DateTime.UtcNow >= _delayEnd && !Ai.Owner.IsGlobalCooldowned;
        }
    }

    /// <summary>
    /// Determines if the unit should return to its idle position based on distance conditions
    /// </summary>
    protected bool ShouldReturn
    {
        get
        {
            if (Ai?.Owner == null)
                return true;

            var returnDistance = Ai.Owner.Template.ReturnDistance > 0 ?
                Ai.Owner.Template.ReturnDistance : DefaultReturnDistance;

            var absoluteReturnDistance = Ai.Owner.Template.AbsoluteReturnDistance > 0 ?
                Ai.Owner.Template.AbsoluteReturnDistance : DefaultAbsoluteReturnDistance;

            if (Ai.Owner.CurrentTarget == null)
                return true;

            var distanceToTarget = Vector3.Distance(Ai.Owner.Transform.World.Position, Ai.Owner.CurrentTarget.Transform.World.Position);
            var distanceToIdlePosition = Vector3.Distance(Ai.Owner.Transform.World.Position, Ai.IdlePosition);

            return (distanceToTarget > returnDistance || distanceToIdlePosition > returnDistance)
                && distanceToIdlePosition <= absoluteReturnDistance;
        }
    }

    #endregion

    #region Target Management

    /// <summary>
    /// Updates the current target based on aggro table and visibility
    /// </summary>
    /// <returns>True if a valid target was found and set, false otherwise</returns>
    public bool UpdateTarget()
    {
        if (Ai?.Owner == null)
            return false;

        var aggroList = Ai.Owner.AggroTable.Values;
        var potentialTargets = aggroList
            .OrderByDescending(o => o.TotalAggro)
            .Select(o => o.Owner)
            .OfType<Unit>()
            .ToList();

        foreach (var target in potentialTargets)
        {
            if (ProcessTargetSelection(target))
                return true;
        }

        ClearInvalidTarget();
        return false;
    }

    private bool ProcessTargetSelection(Unit target)
    {
        if (!IsValidTarget(target))
            return false;

        UpdateTargetAndAggro(target);
        return true;
    }

    private bool IsValidTarget(Unit target)
    {
        if (!Ai.Owner.UnitIsVisible(target) || target.Hp <= 0)
            return false;

        if (AppConfiguration.Instance.World.GeoDataMode && Ai.Owner.Transform.WorldId > 0)
            return true;

        return WorldManager.Instance.GetUnit(target.ObjId) != null;
    }

    private void UpdateTargetAndAggro(Unit target)
    {
        if (Ai.Owner.CurrentAggroTarget != target && !Ai.AlreadyTargeted)
        {
            if (AppConfiguration.Instance.World.GeoDataMode && Ai.Owner.Transform.WorldId > 0)
            {
                Ai.Owner.FindPath(target);
            }
        }

        Ai.Owner.CurrentAggroTarget = target;
        Ai.Owner.SetTarget(target);
        UpdateAggroHelp(target);
    }

    private void ClearInvalidTarget()
    {
        if (Ai.Owner.CurrentTarget is not Unit currentTarget)
        {
            Ai.Owner.CurrentAggroTarget = null;
            Ai.Owner.SetTarget(null);
            return;
        }

        if (currentTarget.Hp <= 0)
        {
            Ai.Owner.CurrentAggroTarget = null;
            Ai.Owner.SetTarget(null);
        }
    }

    #endregion

    #region Combat State Management

    /// <summary>
    /// Updates phase-specific behavior based on pipe name
    /// </summary>
    protected void CheckPipeName()
    {
        switch (_pipeName)
        {
            case "phase_dragon_ground" when _phaseType == 1:
                var groundHeight = WorldManager.Instance.GetHeight(Ai.Owner.Transform.ZoneId,
                    Ai.Owner.Transform.Local.Position.X,
                    Ai.Owner.Transform.Local.Position.Y);
                Ai.Owner.Transform.Local.SetHeight(groundHeight);
                break;

            case "phase_dragon_fly_hovering" when _phaseType == 2:
                Ai.Owner.Transform.Local.SetHeight(Ai.Owner.Transform.Local.Position.Z + DefaultFlyingHeightAdjustment);
                Ai.Owner.StopMovement();
                break;

            case "phase_dragon_fly_path":
                Ai.GoToFollowPath();
                break;
        }
    }

    /// <summary>
    /// Refreshes the skill queue based on available skill lists and parameters
    /// </summary>
    protected bool RefreshSkillQueue(List<AiSkillList> skillLists, AiParams aiParams)
    {
        if (!ValidateSkillQueueParameters(skillLists, aiParams))
            return false;

        var targetDist = Ai.Owner.GetDistanceTo(Ai.Owner.CurrentTarget);

        if (skillLists.Count > 0)
        {
            return ProcessSkillLists(skillLists, aiParams, targetDist);
        }

        return TryUseBaseSkill();
    }

    private bool ValidateSkillQueueParameters(List<AiSkillList> skillLists, AiParams aiParams)
    {
        return skillLists != null && aiParams != null && Ai?.Owner?.CurrentTarget != null;
    }

    private bool ProcessSkillLists(List<AiSkillList> skillLists, AiParams aiParams, float targetDist)
    {
        var selectedSkillList = skillLists.RandomElementByWeight(s => s.Dice);
        if (selectedSkillList == null)
            return false;

        UpdateAiParameters(selectedSkillList, aiParams);
        LogSkillListSelection(selectedSkillList);

        if (ProcessStartingSkills(selectedSkillList))
            return true;

        return ProcessAvailableSkills(selectedSkillList.SkillLists, targetDist);
    }

    private void UpdateAiParameters(AiSkillList skillList, AiParams aiParams)
    {
        _pipeName = skillList.PipeName;
        _phaseType = skillList.PhaseType;
        aiParams.RestorationOnReturn = skillList.Restoration;
        aiParams.GoReturnState = skillList.GoReturn;
    }

    private void LogSkillListSelection(AiSkillList skillList)
    {
        Logger.Debug($"RefreshSkillQueue: Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, " +
                    $"HP Range=[{skillList.HealthRangeMin}-{skillList.HealthRangeMax}], " +
                    $"Time Range=[{skillList.TimeRangeStart}-{skillList.TimeRangeEnd}], " +
                    $"Skills={skillList.SkillLists.Count}, Dice={skillList.Dice}");
    }

    private bool ProcessStartingSkills(AiSkillList skillList)
    {
        if (skillList.StartAiSkills.Count == 0 || _startingSkillAlreadyUsed)
            return false;

        foreach (var skill in skillList.StartAiSkills)
        {
            if (Ai.Owner.Cooldowns.CheckCooldown(skill.SkillId))
                continue;

            Logger.Debug($"Unit {Ai.Owner.ObjId} using start skill {skill.SkillId}");
            _skillQueue.Enqueue(skill);
            _startingSkillAlreadyUsed = true;
        }

        return _skillQueue.Count > 0;
    }

    private bool ProcessAvailableSkills(List<SkillList> skillLists, float targetDist)
    {
        var skillList = skillLists.RandomElementByWeight(s => s.Dice);
        if (skillList == null)
            return false;

        foreach (var skill in skillList.Skills.Where(skill => IsSkillUsable(skill, targetDist)))
        {
            _skillQueue.Enqueue(skill);
            Logger.Debug($"Unit {Ai.Owner.ObjId} adding skill {skill.SkillId} to queue");
        }

        return _skillQueue.Count > 0;
    }

    private bool IsSkillUsable(AiSkill skill, float targetDist)
    {
        if (Ai.Owner.Cooldowns.CheckCooldown(skill.SkillId))
            return false;

        var template = SkillManager.Instance.GetSkillTemplate(skill.SkillId);
        if (template == null)
            return false;

        return targetDist >= template.MinRange && targetDist <= template.MaxRange ||
               template.TargetType == SkillTargetType.Self;
    }

    private bool TryUseBaseSkill()
    {
        if (Ai.Owner.Template.BaseSkillId == 0)
            return false;

        var baseSkill = new AiSkill
        {
            SkillId = (uint)Ai.Owner.Template.BaseSkillId,
            Strafe = Ai.Owner.Template.BaseSkillStrafe,
            Delay = Ai.Owner.Template.BaseSkillDelay
        };

        Logger.Debug($"Unit {Ai.Owner.ObjId} using base skill {baseSkill.SkillId}");
        _skillQueue.Enqueue(baseSkill);

        return true;
    }

    #endregion
}
