using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Faction;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Utils;

using NLog;

namespace AAEmu.Game.Models.Game.AI.v2.Framework;

/// <summary>
/// Represents an AI state/behavior. This is the base class for all AI state machine behaviors.
/// </summary>
public abstract class Behavior
{
    protected static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    // Timing and range fields for skill usage and AI logic
    protected DateTime _delayEnd;
    protected float _nextTimeToDelay;
    protected float _minWeaponRange;
    protected float _maxWeaponRange;
    private const float MeleeAttackRange = 4f;

    /// <summary>
    /// The AI context for this behavior (contains owner, parameters, etc).
    /// </summary>
    public NpcAi Ai { get; set; }

    /// <summary>
    /// Called when entering this behavior/state.
    /// </summary>
    public abstract void Enter();
    /// <summary>
    /// Called every tick to update this behavior/state.
    /// </summary>
    public abstract void Tick(TimeSpan delta);
    /// <summary>
    /// Called when exiting this behavior/state.
    /// </summary>
    public abstract void Exit();

    /// <summary>
    /// Adds a transition to another behavior on a specific event.
    /// </summary>
    public Behavior AddTransition(TransitionEvent on, BehaviorKind kind)
    {
        return AddTransition(new Transition(on, kind));
    }

    /// <summary>
    /// Adds a transition to another behavior.
    /// </summary>
    public Behavior AddTransition(Transition transition)
    {
        return Ai.AddTransition(this, transition);
    }

    /// <summary>
    /// Picks a skill based on the current state and uses it on the target.
    /// </summary>
    /// <param name="kind">Skill use condition kind</param>
    /// <param name="target">Target unit</param>
    /// <param name="targetDist">Distance to target</param>
    /// <returns>Result of skill usage</returns>
    public SkillResult PickSkillAndUseIt(SkillUseConditionKind kind, BaseUnit target, float targetDist)
    {
        var res = SkillResult.InvalidSkill;
        var skills = new List<NpcSkill>();
        if (Ai.Owner.Template.Skills.TryGetValue(kind, out var templateSkill))
        {
            skills = templateSkill;
        }
        if (skills.Count > 0)
        {
            skills = skills
                .Where(s => !Ai.Owner.Cooldowns.CheckCooldown(s.SkillId))
                .Where(s =>
                {
                    var template = SkillManager.Instance.GetSkillTemplate(s.SkillId);
                    return template != null && (targetDist >= template.MinRange && targetDist <= template.MaxRange || template.TargetType == SkillTargetType.Self);
                }).ToList();
        }

        if (targetDist == 0 && kind == SkillUseConditionKind.InIdle)
        {
            // Use self skill in idle state
            if (skills.Count <= 0)
            {
                return res;
            }
            var skillSelfId = skills[Rand.Next(skills.Count)].SkillId;
            var skillTemplateSelf = SkillManager.Instance.GetSkillTemplate(skillSelfId);
            var skillSelf = new Skill(skillTemplateSelf);

            var delay1 = (int)(Ai.Owner.Template.BaseSkillDelay * 1000);
            if (Ai.Owner.Template.BaseSkillDelay == 0)
            {
                const uint Delay1 = 10000u;
                const uint Delay2 = 13000u;
                delay1 = (int)Rand.Next(Delay1, Delay2);
            }

            if (this.CheckInterval(delay1))
            {
                Logger.Debug("PickSkillAndUseIt:UseSelfSkill Owner.ObjId {0}, Owner.TemplateId {1}, SkillId {2}", Ai.Owner.ObjId, Ai.Owner.TemplateId, skillTemplateSelf.Id);
                res = UseSkill(skillSelf, target);
            }
            return res;
        }

        // Use combat skill
        var pickedSkillId = (uint)Ai.Owner.Template.BaseSkillId;
        if (skills.Count > 0)
        {
            pickedSkillId = skills[Rand.Next(skills.Count)].SkillId;
        }

        // Hackfix for melee attack range
        if (pickedSkillId == 2 && targetDist > MeleeAttackRange)
        {
            return SkillResult.TooFarRange;
        }
        var skillTemplate = SkillManager.Instance.GetSkillTemplate(pickedSkillId);
        var skill = new Skill(skillTemplate);

        SetWeaponRange(skill, target); // Set max attack distance for the skill

        var delay2 = (int)(Ai.Owner.Template.BaseSkillDelay * 1000);
        if (Ai.Owner.Template.BaseSkillDelay == 0)
        {
            const uint Delay1 = 1500u;
            const uint Delay2 = 1550u;
            delay2 = (int)Rand.Next(Delay1, Delay2);
        }

        if (this.CheckInterval(delay2))
        {
            Logger.Debug("PickSkillAndUseIt:UseSkill Owner.ObjId {0}, Owner.TemplateId {1}, SkillId {2} on Target {3}", Ai.Owner.ObjId, Ai.Owner.TemplateId, skillTemplate.Id, target.ObjId);
            res = UseSkill(skill, target);
        }

        return res;
    }

    /// <summary>
    /// Uses a skill on a target with an optional delay.
    /// </summary>
    /// <param name="skill">Skill object to use</param>
    /// <param name="target">Target unit</param>
    /// <param name="delay">Delay (in seconds) after this skill is used before the next one is allowed</param>
    /// <returns>Skill result of the used skill</returns>
    public SkillResult UseSkill(Skill skill, BaseUnit target, float delay = 0)
    {
        if (target == null)
            return SkillResult.NoTarget;
        if (skill == null)
            return SkillResult.Failure;
        if (Ai.Owner.Cooldowns.CheckCooldown(skill.Id))
            return SkillResult.CooldownTime;

        var targetDist = Ai.Owner.GetDistanceTo(target);
        if (targetDist < skill.Template.MinRange)
            return SkillResult.TooCloseRange;
        if (targetDist > skill.Template.MaxRange)
            return SkillResult.TooFarRange;

        _nextTimeToDelay = delay;
        var skillCaster = SkillCaster.GetByType(SkillCasterType.Unit);
        skillCaster.ObjId = Ai.Owner.ObjId;

        SkillCastTarget skillCastTarget;
        switch (skill.Template.TargetType)
        {
            case SkillTargetType.Pos:
                var pos = Ai.Owner.Transform.World.Position;
                skillCastTarget = new SkillCastPositionTarget()
                {
                    ObjId = Ai.Owner.ObjId,
                    PosX = pos.X,
                    PosY = pos.Y,
                    PosZ = pos.Z,
                    PosRot = Ai.Owner.Transform.World.ToRollPitchYawDegrees().Z
                };
                break;
            default:
                skillCastTarget = SkillCastTarget.GetByType(SkillCastTargetType.Unit);
                skillCastTarget.ObjId = target.ObjId;
                break;
        }

        var skillObject = SkillObject.GetByType(SkillObjectType.None);
        skill.Callback = OnSkillEnded;
        var result = skill.Use(Ai.Owner, skillCaster, skillCastTarget, skillObject, false, out _);
        // Fix the eastward turn when using SelfSkill
        if (skill.Template.TargetType != SkillTargetType.Self && result == SkillResult.Success)
            Ai.Owner.LookTowards(target.Transform.World.Position);
        return result;
    }

    /// <summary>
    /// Callback for when a skill ends. Sets the delay for the next skill.
    /// </summary>
    public virtual void OnSkillEnded()
    {
        try
        {
            _delayEnd = DateTime.UtcNow.AddSeconds(_nextTimeToDelay);
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Called when an enemy is seen and the AI should enter combat mode.
    /// </summary>
    /// <param name="target">The enemy unit seen</param>
    public void OnEnemySeen(Unit target)
    {
        Ai.Owner.AddUnitAggro(AggroKind.Damage, target, 1);
        Ai.GoToCombat();
    }

    /// <summary>
    /// Checks for aggression and triggers combat if an enemy is found.
    /// </summary>
    /// <returns>True if aggression was triggered</returns>
    public bool CheckAggression()
    {
        if (!Ai.Owner.Template.Aggression)
            return false;

        var res = false;
        var nearbyUnits = WorldManager.GetAround<Unit>(Ai.Owner, Ai.Owner.Template.AttackStartRangeScale * 10f);
        var unitsWithDistance = new List<(Unit, float)>();
        foreach (var nearbyUnit in nearbyUnits)
        {
            var rangeOfUnit = MathUtil.CalculateDistance(Ai.Owner, nearbyUnit, true);
            unitsWithDistance.Add((nearbyUnit, rangeOfUnit));
        }
        unitsWithDistance.Sort((p, q) => p.Item2.CompareTo(q.Item2));

        foreach (var (unit, rangeOfUnit) in unitsWithDistance)
        {
            if (unit.IsDead || unit.Hp <= 0)
                continue;

            var maxHeightGap = Ai.Owner.CanFly ? (Ai.Owner.ModelSize * Ai.Owner.Scale * 3.5f) : (Ai.Owner.ModelSize * Ai.Owner.Scale * 1.5f);
            if (MathUtil.IsFront(Ai.Owner, unit, Ai.Owner.Template.SightFovScale) &&
                Math.Abs(Ai.Owner.Transform.World.Position.Z - unit.Transform.World.Position.Z) < maxHeightGap)
            {
                if (Ai.Owner.CanAttack(unit) && (rangeOfUnit < 1f || Ai.Owner.CanSeeTarget(unit)))
                {
                    OnEnemySeen(unit);
                    res = true;
                    break;
                }
            }
            else
            {
                if (rangeOfUnit < 1.5f * Ai.Owner.Template.SightRangeScale)
                {
                    if (Ai.Owner.CanAttack(unit) && (rangeOfUnit < 0.5f || Ai.Owner.CanSeeTarget(unit)))
                    {
                        OnEnemySeen(unit);
                        res = true;
                        break;
                    }
                }
            }
        }
        return res;
    }

    /// <summary>
    /// Called when an enemy is seen in alert mode.
    /// </summary>
    /// <param name="target">The enemy unit seen</param>
    public void OnEnemyAlert(Unit target)
    {
        Ai._alertEndTime = DateTime.UtcNow.AddSeconds(5);
        Ai._nextAlertCheckTime = DateTime.UtcNow.AddSeconds(7);
        Ai.Owner.SetTarget(target);
        Ai.GoToAlert();
    }

    /// <summary>
    /// Checks for alert state and triggers alert if an enemy is found.
    /// </summary>
    /// <returns>True if alert was triggered</returns>
    public bool CheckAlert()
    {
        if (Ai._nextAlertCheckTime > DateTime.UtcNow)
            return false;
        if (Ai.Owner.IsInBattle)
            return false;

        var res = false;
        var nearbyUnits = WorldManager.GetAround<Unit>(Ai.Owner, Ai.Owner.Template.SightRangeScale * 15f);
        var unitsWithDistance = new List<(Unit, float)>();
        foreach (var nearbyUnit in nearbyUnits)
        {
            var rangeOfUnit = MathUtil.CalculateDistance(Ai.Owner, nearbyUnit, true);
            unitsWithDistance.Add((nearbyUnit, rangeOfUnit));
        }
        unitsWithDistance.Sort((p, q) => p.Item2.CompareTo(q.Item2));

        foreach (var (unit, rangeOfUnit) in unitsWithDistance)
        {
            if (unit.IsDead || unit.Hp <= 0)
                continue;

            var maxHeightGap = Ai.Owner.CanFly ? (Ai.Owner.ModelSize * Ai.Owner.Scale * MeleeAttackRange) : (Ai.Owner.ModelSize * Ai.Owner.Scale * 1.75f);
            if (MathUtil.IsFront(Ai.Owner, unit, Ai.Owner.Template.SightFovScale) &&
                Math.Abs(Ai.Owner.Transform.World.Position.Z - unit.Transform.World.Position.Z) < maxHeightGap)
            {
                if (Ai.Owner.CanAttack(unit) && (rangeOfUnit < 1f || Ai.Owner.CanSeeTarget(unit)))
                {
                    OnEnemyAlert(unit);
                    res = true;
                    break;
                }
            }
            else
            {
                if (rangeOfUnit < 2f * Ai.Owner.Template.SightRangeScale)
                {
                    if (Ai.Owner.CanAttack(unit) && (rangeOfUnit < 0.5f || Ai.Owner.CanSeeTarget(unit)))
                    {
                        OnEnemyAlert(unit);
                        res = true;
                        break;
                    }
                }
            }
        }
        return res;
    }

    /// <summary>
    /// Updates aggro for nearby NPCs to help attack the abuser.
    /// </summary>
    /// <param name="abuser">The unit causing aggro</param>
    /// <param name="radius">Radius to check for help</param>
    public void UpdateAggroHelp(Unit abuser, int radius = 200)
    {
        bool needHelp;
        var npcs = WorldManager.GetAround<Npc>(Ai.Owner, Ai.Owner.Template.AttackStartRangeScale * radius);
        if (npcs == null)
            return;

        foreach (var npc in npcs
                     .Where(npc => !npc.IsInBattle && npc.Template.AcceptAggroLink)
                     .Where(npc => npc.GetDistanceTo(Ai.Owner) <= npc.Template.AggroLinkHelpDist))
        {
            if (npc.Template.Aggression && npc.Template.AggroLinkSpecialRuleId == AggroLinkSpecialRuleKind.None)
            {
                needHelp = true;
            }
            else
            {
                if (!(npc.Template.AggroLinkSightCheck && npc.CanSeeTarget(abuser)))
                {
                    continue;
                }

                switch (npc.Template.AggroLinkSpecialRuleId)
                {
                    case AggroLinkSpecialRuleKind.FactionHelp when npc.Faction.Id == Ai.Owner.Faction.Id:
                    case AggroLinkSpecialRuleKind.FriendlyHelp when npc.GetRelationStateTo(Ai.Owner) == RelationState.Friendly:
                    case AggroLinkSpecialRuleKind.NeutralHelp when npc.GetRelationStateTo(Ai.Owner) == RelationState.Neutral:
                    case AggroLinkSpecialRuleKind.EveryoneHelp:
                        needHelp = true;
                        break;
                    case AggroLinkSpecialRuleKind.None:
                    default:
                        needHelp = false;
                        break;
                }
            }

            if (!needHelp)
                continue;

            npc.Ai.Owner.AddUnitAggro(AggroKind.Damage, abuser, 1);
            npc.Ai.OnAggroTargetChanged();
        }
    }

    /// <summary>
    /// Sets the weapon range for a skill based on the target's equipment.
    /// </summary>
    public void SetWeaponRange(Skill skill, BaseUnit target)
    {
        var unit = (Unit)target;
        var skillRange = Ai.Owner.ApplySkillModifiers(skill, SkillAttribute.Range, skill.Template.MaxRange);
        var minRangeCheck = skill.Template.MinRange * 1.0;
        var maxRangeCheck = skillRange;

        // If weapon is used to calculate range, use that
        if (skill.Template.WeaponSlotForRangeId > 0)
        {
            var minWeaponRange = 0.0f;
            var maxWeaponRange = 3.0f;
            if (unit.Equipment.GetItemBySlot(skill.Template.WeaponSlotForRangeId)?.Template is WeaponTemplate weaponTemplate)
            {
                minWeaponRange = weaponTemplate.HoldableTemplate.MinRange;
                maxWeaponRange = weaponTemplate.HoldableTemplate.MaxRange;
            }
            minRangeCheck = minWeaponRange;
            maxRangeCheck = maxWeaponRange;
        }

        _minWeaponRange = (float)minRangeCheck;
        _maxWeaponRange = (float)maxRangeCheck;
    }

    /// <summary>
    /// Checks if the AI is currently following a path.
    /// </summary>
    public bool CheckFollowPath()
    {
        return Ai.PathHandler.HasPathMovementData();
    }

    /// <summary>
    /// Sets this behavior as the default for the AI.
    /// </summary>
    public Behavior SetDefaultBehavior()
    {
        Ai.SetDefaultBehavior(this);
        return this;
    }
    /// <summary>
    /// Validates whether the associated AI instance has a defined owner.
    /// </summary>
    /// <returns><see langword="true"/> if the <c>Ai</c> property is not <see langword="null"/> and its <c>Owner</c> property is
    /// defined; otherwise, <see langword="false"/>.</returns>
    protected bool Validate() => Ai?.Owner != null;

    /// <summary>
    /// Last tick timestamp for throttling.
    /// </summary>
    private DateTime _lastTick;
    protected bool Throttle(float interval = 0.1f)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTick).TotalSeconds < interval) return false;
        _lastTick = now;
        return true;
    }
}
