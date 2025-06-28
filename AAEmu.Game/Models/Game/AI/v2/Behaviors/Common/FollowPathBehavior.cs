using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.AI.v2.Params.Almighty;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

/// <summary>
/// Represents behavior for NPCs following a predefined path.
/// Handles path following, combat interruption, and skill usage during patrolling.
/// </summary>
public class FollowPathBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms minimum between ticks
    private const float LowHealthThreshold = 80f;
    private const int DefaultSkillDelay = 150;

    private DateTime _lastTick;
    private bool _isInitialized;
    private AlmightyNpcAiParams _aiParams;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializePathFollowing();
        _isInitialized = true;
        Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered path following state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"FollowPathBehavior.Enter called with null Ai or Owner");
            return false;
        }

        if (!(Ai.Owner.Template.AiParams is AlmightyNpcAiParams aiParams))
        {
            Logger.Warn($"FollowPathBehavior.Enter called with invalid AI params for unit {Ai.Owner.ObjId}");
            return false;
        }

        _aiParams = aiParams;
        return true;
    }

    private void InitializePathFollowing()
    {
        // Stop current actions and initialize state
        Ai.Owner.InterruptSkills();
        _skillQueue = new Queue<AiSkill>();

        // Set movement state
        Ai.Owner.CurrentGameStance = GameStanceType.Relaxed;
        Ai.Owner.CurrentAlertness = MoveTypeAlertness.Idle;

        // Initialize combat time for skill selection
        _combatStartTime = DateTime.UtcNow;
        _lastTick = DateTime.UtcNow;

        // Handle combat state if needed
        if (Ai.Owner is { IsInBattle: false } npc)
        {
            npc.Events.OnCombatStarted(this, new OnCombatStartedArgs { Owner = npc, Target = npc });
        }

        // Set path following state
        Ai.Param = _aiParams;
        EnablePathFollowing();
    }

    private void EnablePathFollowing()
    {
        Ai.Owner.IsInPatrol = true;
        Ai.Owner.Simulation.MoveToPathEnabled = true;
        Ai.Owner.Simulation.GoToPath(Ai.Owner, true);
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
            //Logger.Warn($"FollowPathBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }

        if (Ai?.Owner == null)
        {
            Logger.Warn($"FollowPathBehavior.Tick called with null Ai or Owner");
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
        // Update target and check combat conditions
        if (!HandleCombatState())
            return;

        // Process path following
        if (!ProcessPathFollowing(delta))
            return;

        // Check for health-based state changes
        CheckHealthState();
    }

    private bool HandleCombatState()
    {
        // Update and validate target
        if (!UpdateTarget())
        {
            Ai.Owner.SetTarget(null);
        }

        // Check for combat triggers
        if (CheckAggression())
            return false;

        if (CheckAlert())
            return false;

        // Handle active combat state
        if (Ai.Owner.IsInBattle && !Ai.Owner.AggroTable.IsEmpty)
        {
            Logger.Debug($"Unit {Ai.Owner.ObjId} has aggro, transitioning to combat");
            Ai.GoToCombat();
            return false;
        }

        return true;
    }

    private bool ProcessPathFollowing(TimeSpan delta)
    {
        // Process current path segment
        if (!Ai.PathHandler.RunCurrentPath(delta))
        {
            Logger.Debug($"Unit {Ai.Owner.ObjId} reached end of path, transitioning to idle");
            Ai.GoToIdle();
            return false;
        }

        // Check if we have any path points left
        if (IsPathComplete())
        {
            Logger.Debug($"Unit {Ai.Owner.ObjId} completed path, transitioning to idle");
            Ai.GoToIdle();
            return false;
        }

        return true;
    }

    private bool IsPathComplete()
    {
        return Ai.PathHandler.TargetPosition == Vector3.Zero &&
               Ai.PathHandler.AiPathPoints.Count <= 0 &&
               Ai.PathHandler.AiPathPointsRemaining.Count <= 0;
    }

    private void CheckHealthState()
    {
        var healthRatio = (float)Ai.Owner.Hp / Ai.Owner.MaxHp * 100;
        if (healthRatio <= LowHealthThreshold)
        {
            Logger.Debug($"Unit {Ai.Owner.ObjId} health below threshold ({healthRatio:F1}%), stopping patrol");
            DisablePathFollowing();
            Ai.GoToDefaultBehavior();
        }
    }

    private void DisablePathFollowing()
    {
        if (Ai.Owner == null)
            return;

        Ai.Owner.IsInPatrol = false;
        Ai.Owner.Simulation.MoveToPathEnabled = false;
        Ai.Owner.StopMovement();
    }

    public override void Exit()
    {
        if (!_isInitialized)
            return;

        DisablePathFollowing();
        //Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting path following state");
        _isInitialized = false;
    }

    private bool RefreshSkillQueue(List<AiSkillList> skillLists)
    {
        if (skillLists == null || Ai?.Owner == null)
            return false;

        var targetDist = Ai.Owner.GetDistanceTo(Ai.Owner.CurrentTarget);
        var aiSkillLists = RequestAvailableAiSkillList(skillLists);

        if (aiSkillLists.Count <= 0)
            return TryUseBaseSkill();

        return ProcessSkillLists(aiSkillLists, targetDist);
    }

    private bool ProcessSkillLists(List<AiSkillList> aiSkillLists, float targetDist)
    {
        var selectedSkillList = aiSkillLists.RandomElementByWeight(s => s.Dice);
        if (selectedSkillList == null)
            return false;

        UpdateAiParams(selectedSkillList);
        LogSkillSelection(selectedSkillList);

        if (!ProcessStartingSkills(selectedSkillList))
            return ProcessMainSkills(selectedSkillList, targetDist);

        return true;
    }

    private void UpdateAiParams(AiSkillList selectedSkillList)
    {
        if (_aiParams == null)
            return;

        _aiParams.RestorationOnReturn = selectedSkillList.Restoration;
        _aiParams.GoReturnState = selectedSkillList.GoReturn;
    }

    private void LogSkillSelection(AiSkillList skillList)
    {
        Logger.Debug($"Selected skill list for Unit {Ai.Owner.ObjId}, " +
                    $"HP Range: [{skillList.HealthRangeMin}-{skillList.HealthRangeMax}], " +
                    $"Time Range: [{skillList.TimeRangeStart}-{skillList.TimeRangeEnd}], " +
                    $"Skills: {skillList.SkillLists.Count}, Dice: {skillList.Dice}");
    }

    private bool ProcessStartingSkills(AiSkillList selectedSkillList)
    {
        if (selectedSkillList.StartAiSkills.Count <= 0)
            return false;

        foreach (var skill in selectedSkillList.StartAiSkills)
        {
            if (Ai.Owner.Cooldowns.CheckCooldown(skill.SkillId))
                continue;

            Logger.Debug($"Unit {Ai.Owner.ObjId} queuing starting skill {skill.SkillId}");
            _skillQueue.Enqueue(skill);
        }

        return true;
    }

    private bool ProcessMainSkills(AiSkillList selectedSkillList, float targetDist)
    {
        var availableSkillList = RequestAvailableSkillList(selectedSkillList.SkillLists);
        var skillList = availableSkillList.RandomElementByWeight(s => s.Dice);

        if (skillList == null)
            return false;

        foreach (var skill in skillList.Skills)
        {
            if (!IsSkillUsable(skill, targetDist))
                continue;

            Logger.Debug($"Unit {Ai.Owner.ObjId} queuing skill {skill.SkillId}");
            _skillQueue.Enqueue(skill);
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

        return (targetDist >= template.MinRange && targetDist <= template.MaxRange) ||
               template.TargetType == SkillTargetType.Self;
    }

    private bool TryUseBaseSkill()
    {
        if (Ai?.Owner?.Template.BaseSkillId == 0)
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

    private List<AiSkillList> RequestAvailableAiSkillList(List<AiSkillList> aiSkillLists)
    {
        var healthRatio = (int)((float)Ai.Owner.Hp / Ai.Owner.MaxHp * 100);

        var baseList = aiSkillLists.AsEnumerable();
        var timeElapsed = (DateTime.UtcNow - _combatStartTime).TotalSeconds;

        var availableSkillLists = new List<AiSkillList>();
        foreach (var s in baseList)
        {
            // first, let's select the allowed skills based on life value
            if ((s.HealthRangeMin == 0 && s.HealthRangeMax == 0) || (s.HealthRangeMin < healthRatio && healthRatio <= s.HealthRangeMax))
            {
                Logger.Info($"RequestAvailableSkillList: HealthCheck passed successfully for Ai.Owner={Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, health={healthRatio}, healthRange=[{s.HealthRangeMin}.{s.HealthRangeMax}], timeElapsed={timeElapsed}, timeRange=[{s.TimeRangeStart}.{s.TimeRangeEnd}], SkillLists Count={s.SkillLists.Count}, Dice={s.Dice}");

                // then, select the allowed skills by time
                if ((s.TimeRangeStart >= 0 && s.TimeRangeEnd > 0) || (s.TimeRangeStart > 0 && s.TimeRangeEnd >= 0))
                {
                    if (s.TimeRangeStart <= timeElapsed && s.TimeRangeEnd == 0)
                    {
                        Logger.Info($"RequestAvailableSkillList: TimeCheck passed successfully for Ai.Owner={Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, health={healthRatio}, healthRange=[{s.HealthRangeMin}.{s.HealthRangeMax}], timeElapsed={timeElapsed}, timeRange=[{s.TimeRangeStart}.{s.TimeRangeEnd}], SkillLists Count={s.SkillLists.Count}, Dice= {s.Dice}");

                        availableSkillLists.Add(s);
                    }
                    else if (s.TimeRangeStart <= timeElapsed && timeElapsed <= s.TimeRangeEnd)
                    {
                        Logger.Info($"RequestAvailableSkillList: TimeCheck passed successfully for Ai.Owner={Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, health={healthRatio}, healthRange=[{s.HealthRangeMin}.{s.HealthRangeMax}], timeElapsed={timeElapsed}, timeRange=[{s.TimeRangeStart}.{s.TimeRangeEnd}], SkillLists Count={s.SkillLists.Count}, Dice= {s.Dice}");

                        availableSkillLists.Add(s);
                    }
                }
                else if (s.TimeRangeStart == 0 && s.TimeRangeEnd == 0)
                {
                    Logger.Info($"RequestAvailableSkillList: TimeCheck passed successfully for Ai.Owner={Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, health={healthRatio}, healthRange=[{s.HealthRangeMin}.{s.HealthRangeMax}], timeElapsed={timeElapsed}, timeRange=[{s.TimeRangeStart}.{s.TimeRangeEnd}], SkillLists Count={s.SkillLists.Count}, Dice= {s.Dice}");

                    availableSkillLists.Add(s);
                }
            }
        }

        return availableSkillLists;
    }

    private List<SkillList> RequestAvailableSkillList(List<SkillList> skillLists)
    {
        var healthRatio = (int)((float)Ai.Owner.Hp / Ai.Owner.MaxHp * 100);

        var baseList = skillLists.AsEnumerable();
        var timeElapsed = (DateTime.UtcNow - _combatStartTime).TotalSeconds;

        var availableSkillLists = new List<SkillList>();
        foreach (var s in baseList)
        {
            // first, let's select the allowed skills based on life value
            if ((s.HealthRangeMin == 0 && s.HealthRangeMax == 0) || (s.HealthRangeMin < healthRatio && healthRatio <= s.HealthRangeMax))
            {
                Logger.Info($"RequestAvailableSkillList: HealthCheck passed successfully for Ai.Owner={Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, health={healthRatio}, healthRange=[{s.HealthRangeMin}.{s.HealthRangeMax}], timeElapsed={timeElapsed}, timeRange=[{s.TimeRangeStart}.{s.TimeRangeEnd}], skills Count={s.Skills.Count}, Dice={s.Dice}");

                // then, select the allowed skills by time
                if ((s.TimeRangeStart >= 0 && s.TimeRangeEnd > 0) || (s.TimeRangeStart > 0 && s.TimeRangeEnd >= 0))
                {
                    if (s.TimeRangeStart <= timeElapsed && s.TimeRangeEnd == 0)
                    {
                        Logger.Info($"RequestAvailableSkillList: TimeCheck passed successfully for Ai.Owner={Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, health={healthRatio}, healthRange=[{s.HealthRangeMin}.{s.HealthRangeMax}], timeElapsed={timeElapsed}, timeRange=[{s.TimeRangeStart}.{s.TimeRangeEnd}], skills Count={s.Skills.Count}, Dice= {s.Dice}");

                        availableSkillLists.Add(s);
                    }
                    else if (s.TimeRangeStart <= timeElapsed && timeElapsed <= s.TimeRangeEnd)
                    {
                        Logger.Info($"RequestAvailableSkillList: TimeCheck passed successfully for Ai.Owner={Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, health={healthRatio}, healthRange=[{s.HealthRangeMin}.{s.HealthRangeMax}], timeElapsed={timeElapsed}, timeRange=[{s.TimeRangeStart}.{s.TimeRangeEnd}], skills Count={s.Skills.Count}, Dice= {s.Dice}");

                        availableSkillLists.Add(s);
                    }
                }
                else if (s.TimeRangeStart == 0 && s.TimeRangeEnd == 0)
                {
                    Logger.Info($"RequestAvailableSkillList: TimeCheck passed successfully for Ai.Owner={Ai.Owner.ObjId}:{Ai.Owner.TemplateId}, health={healthRatio}, healthRange=[{s.HealthRangeMin}.{s.HealthRangeMax}], timeElapsed={timeElapsed}, timeRange=[{s.TimeRangeStart}.{s.TimeRangeEnd}], skills Count={s.Skills.Count}, Dice= {s.Dice}");

                    availableSkillLists.Add(s);
                }
            }
        }

        return availableSkillLists;
    }
}
