using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.AI.AStar;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Controls;
using AAEmu.Game.Models.Game.AI.v2.Params;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.StaticValues;

using NLog;

namespace AAEmu.Game.Models.Game.AI.v2.Framework
{
    /// <summary>
    /// This is the basics of a unit's AI: The state machine. It also carries data about which unit owns it
    /// </summary>
    public abstract class NpcAi
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        public bool ShouldTick { get; set; }
        public bool AlreadyTargeted { get; set; }

        public Npc Owner { get; init; }
        public Vector3 IdlePosition { get; set; }
        public Vector3 HomePosition { get; set; }
        public AiParams Param { get; set; }
        public PathNode PathNode { get; set; }

        private readonly Dictionary<BehaviorKind, Behavior> _behaviors;
        private readonly Dictionary<Behavior, List<Transition>> _transitions;
        private Behavior _currentBehavior;
        private Behavior _defaultBehavior;
        public DateTime _nextAlertCheckTime = DateTime.MinValue;
        public DateTime _alertEndTime = DateTime.MinValue;

        #region ai_commands
        /// <summary>
        /// A list of AiCommands that should take priority over any other behavior
        /// </summary>
        public Queue<AiCommands> AiCommandsQueue { get; set; } = new();

        /// <summary>
        /// Currently executing command
        /// </summary>
        public AiCommands AiCurrentCommand { get; set; }

        /// <summary>
        /// Time that AiCurrentCommand started
        /// </summary>
        public DateTime AiCurrentCommandStartTime { get; set; } = DateTime.MinValue;
        public TimeSpan AiCurrentCommandRunTime { get; set; } = TimeSpan.Zero;
        #endregion ai_commands

        public AiPathHandler PathHandler { get; set; }
        public Unit AiFollowUnitObj { get; set; }

        // Persistent arguments for AiCommands queue
        public string AiFileName { get; set; } = string.Empty;
        public string AiFileName2 { get; set; } = string.Empty;
        public uint AiSkillId { get; set; }
        public uint AiTimeOut { get; set; }

        public NpcAi()
        {
            _behaviors = new Dictionary<BehaviorKind, Behavior>();
            _transitions = new Dictionary<Behavior, List<Transition>>();
            PathNode = new PathNode();
            PathHandler = new AiPathHandler(this);
        }

        public void Start()
        {
            Build();
            CheckValid();
            // GoToSpawn();
        }

        protected abstract void Build();

        private void CheckValid()
        {
            // обход всех переходов без LINQ для минимизации затрат на аллокацию
            foreach (var transitions in _transitions.Values)
            {
                foreach (var transition in transitions)
                {
                    if (!_behaviors.ContainsKey(transition.Kind))
                    {
                        Logger.Error($"Transition is invalid. Type {transition.Kind.GetType().Name} missing, while used in transition on {transition.On}");
                    }
                }
            }
        }

        protected Behavior AddBehavior(BehaviorKind kind, Behavior behavior)
        {
            behavior.Ai = this;
            _behaviors.Add(kind, behavior);
            return behavior;
        }

        public Behavior GetCurrentBehavior() => _currentBehavior;

        private Behavior GetBehavior(BehaviorKind kind) => _behaviors.GetValueOrDefault(kind);

        private void SetCurrentBehavior(Behavior behavior)
        {
            Logger.Trace(
                $"Npc {Owner.TemplateId}:{Owner.ObjId} leaving behavior {_currentBehavior?.GetType().Name ?? "none"}, Entering behavior {behavior?.GetType().Name ?? "none"}");
            _currentBehavior?.Exit();
            _currentBehavior = behavior;
            _currentBehavior?.Enter();
        }

        protected void SetCurrentBehavior(BehaviorKind kind)
        {
            if (!_behaviors.ContainsKey(kind))
            {
                Logger.Trace(
                    $"Trying to set Npc {Owner.TemplateId}:{Owner.ObjId} current behavior, but it is not valid. Missing behavior: {kind}");
                return;
            }

            Logger.Trace($"Set Npc {Owner.TemplateId}:{Owner.ObjId} current behavior: {kind}");
            SetCurrentBehavior(_behaviors[kind]);
        }

        public Behavior AddTransition(Behavior source, Transition target)
        {
            if (!_transitions.ContainsKey(source))
                _transitions.Add(source, new List<Transition>());
            _transitions[source].Add(target);
            return source;
        }

        // Вычисляемое свойство вместо метода, чтобы сделать код более декларативным
        private bool HasPersistentAi => PathHandler.AiPathPoints.Count > 0 ||
                                          PathHandler.AiPathPointsRemaining.Count > 0 ||
                                          AiFollowUnitObj != null ||
                                          AiCommandsQueue.Count > 0;

        public void Tick(TimeSpan delta)
        {
            var owner = Owner;
            if (owner == null)
                return;

            var region = owner.Region;

            if (HasPersistentAi || (region?.HasPlayerActivity() ?? false))
            {
                _currentBehavior?.Tick(delta);

                var aggroTable = owner.AggroTable;
                if (aggroTable != null)
                {
                    if (aggroTable.Count <= 0)
                    {
                        if (owner.IsDead || GetCurrentBehavior() is DeadBehavior)
                            return;

                        OnNoAggroTarget();
                        return;
                    }

                    List<Unit> toRemove = null;
                    foreach (var pair in aggroTable)
                    {
                        var aggro = pair.Value;
                        if (aggro.Owner.Buffs.CheckBuffTag((uint)TagsEnum.NoFight) ||
                            aggro.Owner.Buffs.CheckBuffTag((uint)TagsEnum.Returning) ||
                            !owner.CanAttack(aggro.Owner))
                        {
                            toRemove ??= new List<Unit>(aggroTable.Count / 2);
                            toRemove.Add(aggro.Owner);
                        }
                    }

                    if (toRemove != null)
                    {
                        foreach (var unit in toRemove)
                            owner.ClearAggroOfUnit(unit);
                    }
                }
            }
        }

        public void StopAi()
        {
            if (Owner == null)
                return;
            SetCurrentBehavior(BehaviorKind.Idle);
            PathHandler.AiPathPoints.Clear();
            PathHandler.AiPathPointsRemaining.Clear();
            AiFollowUnitObj = null;
            AiCommandsQueue.Clear();
        }

        private void Transition(TransitionEvent on)
        {
            if (!_transitions.TryGetValue(_currentBehavior, out var transitionList))
                return;

            Transition foundTransition = null;
            foreach (var t in transitionList)
            {
                if (t.On == on)
                {
                    foundTransition = t;
                    break;
                }
            }
            if (foundTransition == null)
                return;

            var newBehavior = GetBehavior(foundTransition.Kind);
            SetCurrentBehavior(newBehavior);
        }

        #region Events

        public void OnNoAggroTarget()
        {
            Transition(TransitionEvent.OnNoAggroTarget);
        }

        public void OnAggroTargetChanged()
        {
            Transition(TransitionEvent.OnAggroTargetChanged);
        }

        #endregion

        #region Go to X

        public virtual void GoToSpawn()
        {
            SetCurrentBehavior(BehaviorKind.Spawning);
        }

        public virtual void GoToIdle()
        {
            SetCurrentBehavior(BehaviorKind.Idle);
        }

        public virtual void GoToRunCommandSet()
        {
            SetCurrentBehavior(BehaviorKind.RunCommandSet);
        }

        public virtual void GoToTalk()
        {
            SetCurrentBehavior(BehaviorKind.Talk);
        }

        public virtual void GoToAlert()
        {
            SetCurrentBehavior(BehaviorKind.Alert);
        }

        public virtual void GoToCombat()
        {
            SetCurrentBehavior(BehaviorKind.Attack);
        }

        public virtual void GoToFollowPath()
        {
            SetCurrentBehavior(BehaviorKind.FollowPath);
        }

        public virtual void GoToFollowUnit()
        {
            SetCurrentBehavior(BehaviorKind.FollowUnit);
        }

        public virtual void GoToReturn()
        {
            SetCurrentBehavior(BehaviorKind.ReturnState);
        }

        public virtual void GoToDead()
        {
            SetCurrentBehavior(BehaviorKind.Dead);
        }

        public virtual void GoToDespawn()
        {
            SetCurrentBehavior(BehaviorKind.Despawning);
        }

        public virtual void GoToDefaultBehavior()
        {
            if (_defaultBehavior != null)
                SetCurrentBehavior(_defaultBehavior);
        }

        /// <summary>
        /// Adds a list of AI commands to the execution Queue and goes to the RunCommandSet behavior if there are items in the queue
        /// </summary>
        /// <param name="aiCommandsList">List of commands</param>
        /// <param name="addOnly">If true, will not go to the RunCommandSet behavior</param>
        public void EnqueueAiCommands(IEnumerable<AiCommands> aiCommandsList, bool addOnly = false)
        {
            foreach (var aiCommand in aiCommandsList)
                AiCommandsQueue.Enqueue(aiCommand);
            if (addOnly)
                return;
            if (AiCommandsQueue.Count > 0)
                GoToRunCommandSet();
        }

        #endregion

        /// <summary>
        /// Extracted method for processing loaded path points.
        /// </summary>
        private void ProcessLoadedPathPoints(List<AiPathPoint> points, bool addToQueueOnly)
        {
            if (!addToQueueOnly)
            {
                var aiPathPoints = PathHandler.AiPathPoints;
                aiPathPoints.Clear();
                PathHandler.AiPathLooping = true;
                if (aiPathPoints.Capacity < points.Count)
                    aiPathPoints.Capacity = points.Count;
                aiPathPoints.AddRange(points);
            }
            else
            {
                var remainingQueue = PathHandler.AiPathPointsRemaining;
                foreach (var point in points)
                    remainingQueue.Enqueue(point);
            }
        }

        public bool LoadAiPathPoints(string aiPathFileName, bool addToQueueOnly)
        {
            var points = AiPathsManager.Instance.LoadAiPathPoints(aiPathFileName);
            if (points.Count <= 0)
                return false;

            ProcessLoadedPathPoints(points, addToQueueOnly);
            return true;
        }

        public async Task<bool> LoadAiPathPointsAsync(string aiPathFileName, bool addToQueueOnly, CancellationToken cancellationToken = default)
        {
            var points = await Task.Run(() => AiPathsManager.Instance.LoadAiPathPoints(aiPathFileName), cancellationToken)
                                  .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (points.Count <= 0)
                return false;

            ProcessLoadedPathPoints(points, addToQueueOnly);
            return true;
        }

        public bool DoFollowDefaultNearestNpc()
        {
            if (Owner.Spawner?.FollowNpc > 0)
            {
                return DoFollowNearestNpc(Owner.Spawner.FollowNpc, 100f);
            }
            return false;
        }

        public bool DoFollowNearestNpc(uint followNpc, float maxRange)
        {
            var maxRangeSquared = maxRange * maxRange;
            var closestDistanceSquared = maxRangeSquared;
            Npc nearestNpc = null;
            var myPosition = Owner.Transform.World.Position;

            foreach (var npc in WorldManager.GetAround<Npc>(Owner, maxRange, true))
            {
                if (npc.TemplateId != followNpc)
                    continue;
                var npcPosition = npc.Transform.World.Position;
                var distanceSquared = Vector3.DistanceSquared(npcPosition, myPosition);
                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    nearestNpc = npc;
                }
            }

            if (nearestNpc != null)
            {
                AiFollowUnitObj = nearestNpc;
                GoToFollowUnit();
                return true;
            }

            return false;
        }

        public double GetRealMovementSpeed(double baseMoveSpeed)
        {
            var speedMul = (Owner.CalculateWithBonuses(0, UnitAttribute.MoveSpeedMul) / 1000.0) + 1.0;
            if (Math.Abs(speedMul - 1.0) > double.Epsilon)
                baseMoveSpeed *= speedMul;

            return baseMoveSpeed;
        }

        public byte GetRealMovementFlags(double moveSpeed)
        {
            return (byte)(moveSpeed < 0.1 ? 3 : moveSpeed < 2.0 ? 5 : 4);
        }

        public virtual void GoToDummy()
        {
            SetCurrentBehavior(BehaviorKind.Dummy);
        }

        public Dictionary<BehaviorKind, Behavior> GetAiBehaviorList()
        {
            return _behaviors;
        }

        public void SetDefaultBehavior(Behavior behavior)
        {
            _defaultBehavior = behavior;
        }
    }
}
