using System;
using System.Linq;
using System.Numerics;

using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI.AStar;
using AAEmu.Game.Models.Game.AI.v2.Params.Flytrap;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Flytrap;

/// <summary>
/// Represents attack behavior for flytrap-type NPCs.
/// Handles gimmick movement, target selection, and combat mechanics.
/// </summary>
public class FlytrapAttackBehavior : BaseCombatBehavior
{
    private const float MinimumTickInterval = 0.1f; // 100ms between ticks
    private const float GimmickMovementRange = 0.1f;
    private const float ReturnDistanceThreshold = 3.0f;

    private FlytrapAiParams _aiParams;
    private DateTime _lastTick;
    private bool _isInitialized;

    public override void Enter()
    {
        if (!ValidateEnterState())
            return;

        InitializeCombatState();
        _isInitialized = true;
        Logger.Debug($"Unit {Ai.Owner.ObjId}:{Ai.Owner.TemplateId} entered flytrap attack state");
    }

    private bool ValidateEnterState()
    {
        if (Ai?.Owner == null)
        {
            Logger.Warn($"FlytrapAttackBehavior.Enter: Ai or Owner is null");
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

        // Trigger combat event
        if (Ai.Owner is { } npc)
        {
            npc.Events.OnCombatStarted(this, new OnCombatStartedArgs { Owner = npc, Target = npc });
        }

        // Initialize parameters
        Ai.Param = Ai.Owner.Template.AiParams;
        _lastTick = DateTime.UtcNow;
    }

    public override void Tick(TimeSpan delta)
    {
        if (!ValidateTickState())
            return;
        if (!ThrottleTick())
            return;

        // Ensure we have flytrap AI parameters
        Ai.Param ??= new FlytrapAiParams("");
        if (Ai.Param is not FlytrapAiParams aiParams)
            return;
        _aiParams = aiParams;

        // Update target and process combat
        if (!ProcessCombatState())
            return;

        // Handle gimmick movement if needed
        if (Ai.Owner.Gimmick?.CurrentTarget != null)
        {
            MoveInRange(Ai.Owner.Gimmick.CurrentTarget, delta);
        }

        // Process combat actions
        ProcessCombatActions();

        // Update position and range checks
        Update();
    }

    private bool ValidateTickState()
    {
        if (!_isInitialized)
        {
            Logger.Warn($"FlytrapAttackBehavior.Tick called before initialization for unit {Ai?.Owner?.ObjId}");
            return false;
        }
        if (Ai?.Owner == null)
        {
            Logger.Warn($"FlytrapAttackBehavior.Tick called with null Ai or Owner");
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

    private bool ProcessCombatState()
    {
        if (!HandleTargetSelection())
        {
            Ai.OnNoAggroTarget();
            return false;
        }

        if (Ai.Owner.CurrentTarget == null)
            return false;

        Ai.Owner.IsInBattle = true;
        return true;
    }

    private void ProcessCombatActions()
    {
        var targetDist = Ai.Owner.GetDistanceTo(Ai.Owner.CurrentTarget);
        PickSkillAndUseIt(SkillUseConditionKind.InCombat, Ai.Owner.CurrentTarget, targetDist);
    }

    private void MoveInRange(BaseUnit target, TimeSpan delta)
    {
        if (Ai?.Owner?.Gimmick == null)
            return;

        var gimmick = Ai.Owner.Gimmick;
        var gimmickPosition = gimmick.Transform.World.Position;

        // Initialize target position if needed
        if (gimmick.Target == Vector3.Zero)
        {
            gimmick.Target = target.Transform.World.Position;
        }

        // Calculate movement parameters
        var moveDistance = gimmick.BaseMoveSpeed * (delta.Milliseconds / 1000.0f) + 1f;
        var moveDistanceZ = gimmick.Template.Gravity * (delta.Milliseconds / 1000.0f);
        var distanceToTarget = MathUtil.CalculateDistance(gimmickPosition, gimmick.Target, true);

        if (AppConfiguration.Instance.World.GeoDataMode && Ai.Owner.Transform.WorldId > 0)
        {
            ProcessGeoDataMovement(target, gimmickPosition, moveDistance, moveDistanceZ, distanceToTarget);
        }
        else
        {
            ProcessDirectMovement(gimmick.Target, moveDistance, moveDistanceZ, distanceToTarget);
        }
    }

    private void ProcessGeoDataMovement(BaseUnit target, Vector3 gimmickPosition, float moveDistance, float moveDistanceZ, float distanceToTarget)
    {
        // Update path if needed
        if (ShouldUpdatePath(target))
        {
            UpdatePath(target as Unit);
        }

        // Follow current path if available
        if (Ai.PathNode?.findPath?.Count > 0 && !Ai.PathNode.findPath[0].Equals(Point.Zero))
        {
            ProcessPathMovement(gimmickPosition, moveDistance, moveDistanceZ);
        }
        else
        {
            ProcessDirectMovement(Ai.Owner.Gimmick.Target, moveDistance, moveDistanceZ, distanceToTarget);
        }
    }

    private bool ShouldUpdatePath(BaseUnit target)
    {
        return Ai.PathNode?.findPath?.Count == 0 && target != null && Ai.PathNode?.pos2 != null;
    }

    private void UpdatePath(Unit target)
    {
        if (target == null)
            return;

        Ai.Owner.FindPath(target);
        Ai.PathNode.pos2 = new Point(target.Transform.World.Position.X, target.Transform.World.Position.Y, target.Transform.World.Position.Z);
        Ai.Owner.Gimmick.Target = target.Transform.World.Position;
    }

    private void ProcessPathMovement(Vector3 gimmickPosition, float moveDistance, float moveDistanceZ)
    {
        var routePoint = new Vector3(Ai.PathNode.Position.X, Ai.PathNode.Position.Y, Ai.PathNode.Position.Z);
        var distanceToPoint = MathUtil.CalculateDistance(gimmickPosition, routePoint, true);

        if (distanceToPoint > GimmickMovementRange)
        {
            Ai.Owner.Gimmick.MoveTowards(routePoint, moveDistance, moveDistanceZ);
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
            Ai.Owner.Gimmick.StopMovement();
            Ai.PathNode.findPath = [];
            return;
        }

        Ai.PathNode.Position = Ai.PathNode.findPath[(int)Ai.PathNode.Current];
    }

    private void ProcessDirectMovement(Vector3 targetPosition, float moveDistance, float moveDistanceZ, float distanceToTarget)
    {
        if (distanceToTarget > GimmickMovementRange)
            Ai.Owner.Gimmick.MoveTowards(targetPosition, moveDistance, moveDistanceZ);
        else
            Ai.Owner.Gimmick.StopMovement();
    }

    private bool HandleTargetSelection()
    {
        var aggroList = Ai.Owner.AggroTable.Values;
        var abusers = aggroList.OrderByDescending(o => o.TotalAggro).Select(o => o.Owner).OfType<Unit>().ToList();

        foreach (var abuser in abusers)
        {
            if (ProcessTargetSelection(abuser))
                return true;
        }

        ClearInvalidTarget();
        return false;
    }

    private bool ProcessTargetSelection(Unit abuser)
    {
        Ai.Owner.LookTowards(abuser.Transform.World.Position);
        if (Ai.AlreadyTargeted)
            return true;

        if (IsValidTarget(abuser))
        {
            SetTarget(abuser);
            return true;
        }

        Ai.Owner.ClearAggroOfUnit(abuser);
        return false;
    }

    private bool IsValidTarget(Unit target)
    {
        if (!AppConfiguration.Instance.World.GeoDataMode || Ai.Owner.Transform.WorldId <= 0)
            return Ai.Owner.UnitIsVisible(target) && !target.IsDead;

        return Ai.Owner.UnitIsVisible(target) && !target.IsDead;
    }

    private void SetTarget(Unit target)
    {
        Ai.Owner.CurrentAggroTarget = target;
        Ai.Owner.SetTarget(target);
        UpdateAggroHelp(target);
        Ai.Owner.FindPath(target);
    }

    private void ClearInvalidTarget()
    {
        if (Ai.Owner.CurrentTarget is not Unit currentTargetUnit)
            Ai.Owner.SetTarget(null);
        else if ((currentTargetUnit.Hp <= 0) || (currentTargetUnit.IsDead))
            Ai.Owner.SetTarget(null);
    }

    public void Update()
    {
        if (Ai.Owner.CurrentTarget is not Unit abuser)
            return;

        var abuserPos = abuser.Transform.World.Position;
        var currentPos = Ai.Owner.Transform.World.Position;
        var idlePos = Ai.IdlePosition;

        // Check if too far from idle position
        if (Ai.Param.AlwaysTeleportOnReturn &&
            MathUtil.DistanceSqVectors(currentPos, idlePos) > ReturnDistanceThreshold * ReturnDistanceThreshold)
        {
            HandleReturnToIdle(abuser);
            return;
        }

        // Check if target is out of attack range
        if (MathUtil.DistanceSqVectors(abuserPos, idlePos) > _aiParams.AttackEndDistance * _aiParams.AttackEndDistance)
        {
            HandleReturnToIdle(abuser);
        }
    }

    private void HandleReturnToIdle(Unit abuser)
    {
        Ai.Owner.ClearAggroOfUnit(abuser);
        Ai.OnNoAggroTarget();
    }

    public override void Exit()
    {
        if (!_isInitialized)
            return;

        Logger.Debug($"Unit {Ai.Owner?.ObjId}:{Ai.Owner?.TemplateId} exiting flytrap attack state");
        _isInitialized = false;
    }
}
