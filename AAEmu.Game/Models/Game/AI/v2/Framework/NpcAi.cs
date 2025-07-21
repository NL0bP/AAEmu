using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    /// Represents the core AI state machine for an NPC.
    /// Handles behavior transitions, movement, targeting, and command execution.
    /// </summary>
    public abstract class NpcAi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Behavior and state management
        private readonly Dictionary<BehaviorKind, Behavior> _behaviors;
        private readonly Dictionary<Behavior, List<Transition>> _transitions;
        private Behavior _currentBehavior;
        private Behavior _defaultBehavior;

        // Alert state timing
        internal DateTime _nextAlertCheckTime = DateTime.MinValue;
        internal DateTime _alertEndTime = DateTime.MinValue;

        // Core properties
        public bool ShouldTick { get; set; }
        public bool AlreadyTargeted { get; set; }
        public Npc Owner { get; set; }
        public Vector3 IdlePosition { get; set; }
        public Vector3 HomePosition { get; set; }
        public AiParams Param { get; set; }
        public PathNode PathNode { get; set; }

        #region AI Commands Management
        /// <summary>
        /// Queue of AI commands that take priority over normal behaviors
        /// </summary>
        public ConcurrentQueue<AiCommands> AiCommandsQueue { get; set; } = new();

        /// <summary>
        /// Currently executing AI command
        /// </summary>
        public AiCommands AiCurrentCommand { get; set; }

        /// <summary>
        /// Timing information for current command execution
        /// </summary>
        public DateTime AiCurrentCommandStartTime { get; set; } = DateTime.MinValue;
        public TimeSpan AiCurrentCommandRunTime { get; set; } = TimeSpan.Zero;
        #endregion

        #region Movement and Path Management
        public AiPathHandler PathHandler { get; set; }
        public Unit AiFollowUnitObj { get; set; }

        // Persistent command arguments
        public string AiFileName { get; set; } = string.Empty;
        public string AiFileName2 { get; set; } = string.Empty;
        public uint AiSkillId { get; set; }
        public uint AiTimeOut { get; set; }
        #endregion

        protected NpcAi()
        {
            _behaviors = new Dictionary<BehaviorKind, Behavior>();
            _transitions = new Dictionary<Behavior, List<Transition>>();
            PathNode = new PathNode();
            PathHandler = new AiPathHandler(this);
        }

        /// <summary>
        /// Initializes the AI state machine and starts its operation
        /// </summary>
        public void Start()
        {
            Build();
            ValidateBehaviors();
        }

        /// <summary>
        /// Builds the state machine structure. Must be implemented by derived classes.
        /// </summary>
        protected abstract void Build();

        /// <summary>
        /// Validates all behavior transitions to ensure consistency
        /// </summary>
        private void ValidateBehaviors()
        {
            foreach (var transitions in _transitions.Values)
            {
                foreach (var transition in transitions)
                {
                    if (!_behaviors.ContainsKey(transition.Kind))
                    {
                        Logger.Error($"Invalid transition: Type {transition.Kind} missing, used in transition on {transition.On}");
                    }
                }
            }
        }

        #region Behavior Management
        /// <summary>
        /// Adds a new behavior to the state machine
        /// </summary>
        protected Behavior AddBehavior(BehaviorKind kind, Behavior behavior)
        {
            behavior.Ai = this;
            _behaviors.Add(kind, behavior);
            return behavior;
        }

        /// <summary>
        /// Gets the currently active behavior
        /// </summary>
        public Behavior GetCurrentBehavior() => _currentBehavior;

        /// <summary>
        /// Gets a behavior by its kind
        /// </summary>
        private Behavior GetBehavior(BehaviorKind kind) => _behaviors.GetValueOrDefault(kind);

        /// <summary>
        /// Changes the current behavior to a new one
        /// </summary>
        private void SetCurrentBehavior(Behavior behavior)
        {
            Logger.Trace($"NPC {Owner.TemplateId}:{Owner.ObjId} transitioning from {_currentBehavior?.GetType().Name ?? "none"} to {behavior?.GetType().Name ?? "none"}");
            _currentBehavior?.Exit();
            _currentBehavior = behavior;
            _currentBehavior?.Enter();
        }

        /// <summary>
        /// Changes the current behavior to one specified by kind
        /// </summary>
        protected void SetCurrentBehavior(BehaviorKind kind)
        {
            if (!_behaviors.ContainsKey(kind))
            {
                Logger.Trace($"Cannot set behavior for NPC {Owner.TemplateId}:{Owner.ObjId}, missing behavior: {kind}");
                return;
            }
            Logger.Trace($"Setting NPC {Owner.TemplateId}:{Owner.ObjId} behavior to: {kind}");
            SetCurrentBehavior(_behaviors[kind]);
        }

        /// <summary>
        /// Adds a transition between behaviors
        /// </summary>
        public Behavior AddTransition(Behavior source, Transition target)
        {
            if (!_transitions.ContainsKey(source))
                _transitions.Add(source, []);
            _transitions[source].Add(target);
            return source;
        }
        #endregion

        #region Update and State Management
        /// <summary>
        /// Whether the AI has any persistent tasks to execute
        /// </summary>
        private bool HasPersistentAi => PathHandler.AiPathPoints.Count > 0 ||
                                       PathHandler.AiPathPointsRemaining.Count > 0 ||
                                       AiFollowUnitObj != null ||
                                       AiCommandsQueue.Count > 0;

        /// <summary>
        /// Updates the AI state machine
        /// </summary>
        public void Tick(TimeSpan delta)
        {
            if (!ValidateTickState())
                return;

            if (HasPersistentAi || (Owner.Region?.HasPlayerActivity() ?? false))
            {
                ProcessTick(delta);
            }
        }

        private bool ValidateTickState()
        {
            return Owner is { Region: not null };
        }

        private void ProcessTick(TimeSpan delta)
        {
            _currentBehavior?.Tick(delta);

            if (Owner.AggroTable == null || Owner.AggroTable.Count == 0)
            {
                if (!Owner.IsDead && GetCurrentBehavior() is not DeadBehavior)
                    OnNoAggroTarget();
                return;
            }

            ProcessAggroTable();
        }

        private void ProcessAggroTable()
        {
            var toRemove = Owner.AggroTable.Values
                .Where(a => ShouldRemoveAggro(a.Owner))
                .Select(a => a.Owner)
                .ToList();

            foreach (var unit in toRemove)
                Owner.ClearAggroOfUnit(unit);

            if (Owner.AggroTable.Count == 0 && !Owner.IsDead && GetCurrentBehavior() is not DeadBehavior)
                OnNoAggroTarget();
        }

        private bool ShouldRemoveAggro(Unit unit)
        {
            return unit.Buffs.CheckBuffTag((uint)TagsEnum.NoFight) ||
                   unit.Buffs.CheckBuffTag((uint)TagsEnum.Returning) ||
                   !Owner.CanAttack(unit);
        }
        #endregion

        #region State Transitions
        private void Transition(TransitionEvent on)
        {
            if (!_transitions.TryGetValue(_currentBehavior, out var transitionList))
                return;

            var foundTransition = transitionList.Find(t => t.On == on);
            if (foundTransition == null)
                return;

            var newBehavior = GetBehavior(foundTransition.Kind);
            SetCurrentBehavior(newBehavior);
        }

        #region Event Handlers
        /// <summary>
        /// Called when NPC loses all aggro targets
        /// </summary>
        public void OnNoAggroTarget() => Transition(TransitionEvent.OnNoAggroTarget);

        /// <summary>
        /// Called when NPC's aggro target changes
        /// </summary>
        public void OnAggroTargetChanged() => Transition(TransitionEvent.OnAggroTargetChanged);
        #endregion

        #region State Transition Methods
        public virtual void GoToSpawn() => SetCurrentBehavior(BehaviorKind.Spawning);
        public virtual void GoToIdle() => SetCurrentBehavior(BehaviorKind.Idle);
        public virtual void GoToRunCommandSet() => SetCurrentBehavior(BehaviorKind.RunCommandSet);
        public virtual void GoToTalk() => SetCurrentBehavior(BehaviorKind.Talk);
        public virtual void GoToAlert() => SetCurrentBehavior(BehaviorKind.Alert);
        public virtual void GoToCombat() => SetCurrentBehavior(BehaviorKind.Attack);
        public virtual void GoToFollowPath() => SetCurrentBehavior(BehaviorKind.FollowPath);
        public virtual void GoToFollowUnit() => SetCurrentBehavior(BehaviorKind.FollowUnit);
        public virtual void GoToReturn() => SetCurrentBehavior(BehaviorKind.ReturnState);
        public virtual void GoToDead() => SetCurrentBehavior(BehaviorKind.Dead);
        public virtual void GoToDespawn() => SetCurrentBehavior(BehaviorKind.Despawning);
        public virtual void GoToDefaultBehavior() => SetCurrentBehavior(_defaultBehavior);
        public virtual void GoToDummy() => SetCurrentBehavior(BehaviorKind.Dummy);
        #endregion
        #endregion

        #region Command and Path Management
        /// <summary>
        /// Enqueues AI commands for execution
        /// </summary>
        /// <param name="aiCommandsList">Commands to execute</param>
        /// <param name="addOnly">If true, won't switch to RunCommandSet behavior</param>
        public void EnqueueAiCommands(IEnumerable<AiCommands> aiCommandsList, bool addOnly = false)
        {
            foreach (var cmd in aiCommandsList)
                AiCommandsQueue.Enqueue(cmd);

            if (!addOnly && !AiCommandsQueue.IsEmpty)
                GoToRunCommandSet();
        }

        /// <summary>
        /// Loads AI path points from a file
        /// </summary>
        public bool LoadAiPathPoints(string aiPathFileName, bool addToQueueOnly)
        {
            var points = AiPathsManager.Instance.LoadAiPathPoints(aiPathFileName);
            if (points.Count <= 0)
                return false;

            ProcessLoadedPathPoints(points, addToQueueOnly);
            return true;
        }

        /// <summary>
        /// Asynchronously loads AI path points from a file
        /// </summary>
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

        private void ProcessLoadedPathPoints(List<AiPathPoint> points, bool addToQueueOnly)
        {
            PathHandler.ClearPath();
            if (!addToQueueOnly)
            {
                var list = PathHandler.AiPathPoints;
                PathHandler.AiPathLooping = true;
                list.Capacity = points.Count;
                list.AddRange(points);
            }
            else
            {
                var queue = PathHandler.AiPathPointsRemaining;
                foreach (var p in points)
                    queue.Enqueue(p);
            }
        }
        #endregion

        #region Movement and Following
        /// <summary>
        /// Attempts to follow the default NPC type specified in the spawner
        /// </summary>
        public bool DoFollowDefaultNearestNpc()
        {
            if (Owner.Spawner?.FollowNpc > 0)
            {
                return DoFollowNearestNpc(Owner.Spawner.FollowNpc, 100f);
            }
            return false;
        }

        /// <summary>
        /// Attempts to follow the nearest NPC of a specific type within range
        /// </summary>
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

        /// <summary>
        /// Calculates real movement speed including bonuses
        /// </summary>
        public double GetRealMovementSpeed(double baseMoveSpeed)
        {
            var speedMul = (Owner.CalculateWithBonuses(0, UnitAttribute.MoveSpeedMul) / 1000.0) + 1.0;
            if (Math.Abs(speedMul - 1.0) > double.Epsilon)
                baseMoveSpeed *= speedMul;
            return baseMoveSpeed;
        }

        /// <summary>
        /// Gets movement flags based on speed
        /// </summary>
        public byte GetRealMovementFlags(double moveSpeed)
        {
            return (byte)(moveSpeed < 0.1 ? 3 : moveSpeed < 2.0 ? 5 : 4);
        }
        #endregion

        #region Behavior Information
        /// <summary>
        /// Gets all behaviors registered in the AI
        /// </summary>
        public Dictionary<BehaviorKind, Behavior> GetAiBehaviorList() => _behaviors;

        /// <summary>
        /// Sets the default behavior to return to
        /// </summary>
        public void SetDefaultBehavior(Behavior behavior) => _defaultBehavior = behavior;

        /// <summary>
        /// Stops all AI activity and returns to idle
        /// </summary>
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
        #endregion
    }
}
