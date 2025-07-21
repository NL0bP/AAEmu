using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Threading;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items.Containers;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Tasks.World;
using AAEmu.Game.Utils;

using Newtonsoft.Json;

using NLog;

using static AAEmu.Commons.Utils.Rand;

namespace AAEmu.Game.Models.Game.NPChar
{
    public class NpcSpawner : Spawner<Npc>
    {
        #region Static & Const
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly object SpawnLock = new();
        #endregion

        #region Fields
        private int _scheduledCount;
        private DateTime _lastSpawnTime = DateTime.MinValue;

        private readonly Dictionary<int, SpawnerPlayerCountCache> _playerCountCache = new();
        private readonly Dictionary<int, SpawnerPlayerInRadiusCache> _playerInRadiusCache = new();
        private readonly Dictionary<int, SpawnerNpcsInZoneCache> _npcsInZoneCache = new();
        #endregion

        #region Properties
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(1f)]
        public uint Count { get; set; } = 1;

        public List<uint> NpcSpawnerIds { get; set; } = [];
        public NpcSpawnerTemplate Template { get; set; }
        private List<NpcSpawnerNpc> SpawnableNpcs { get; set; } = [];
        private ConcurrentDictionary<uint, List<Npc>> SpawnedNpcs { get; set; } = new();

        private int CurrentSpawnCount => SpawnedNpcs.TryGetValue(SpawnerId, out var list) ? list.Count : 0;

        private bool IsSpawnScheduled { get; set; }
        private bool IsDespawnScheduled { get; set; }
        private bool RespawnDenied { get; set; }
        #endregion

        #region Ctors
        public NpcSpawner()
        {
            IsSpawnScheduled = false;
            IsDespawnScheduled = false;
        }
        #endregion

        #region Initialization
        internal void InitializeSpawnableNpcs(NpcSpawnerTemplate template)
        {
            if (template?.Npcs == null)
            {
                Logger.Warn("Template or template.Npcs is null. SpawnableNpcs will not be initialized.");
                return;
            }

            SpawnableNpcs = [..template.Npcs];
        }
        #endregion

        #region Public Update
        /// <summary>
        /// Main update method for the NpcSpawner.
        /// </summary>
        public void Update()
        {
            try
            {
                lock (SpawnLock)
                {
                    var didAction = false;

                    if (CanDespawnNpcs())
                    {
                        DespawnNpcs();
                        didAction = true;
                    }
                    else if (!IsPlayerInSpawnRadius() && CurrentSpawnCount > 0)
                    {
                        DespawnNpcsNow();
                        didAction = true;
                    }

                    if (!didAction && CanSpawnNpcs())
                    {
                        DoSpawn();
                        didAction = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error during NpcSpawner update [SpawnerId={SpawnerId}, UnitId={UnitId}]");
            }
        }
        #endregion

        #region Spawn Logic
        /// <summary>
        /// Determines whether spawning is allowed based on various conditions.
        /// </summary>
        /// <remarks>This method evaluates multiple conditions to determine if spawning is allowed,
        /// including the presence of a valid template,  the absence of a corpse, and whether spawning schedules and
        /// counts are optimal and enabled.</remarks>
        /// <returns><see langword="true"/> if spawning is permitted; otherwise, <see langword="false"/>.</returns>
        private bool CanSpawn() => Template != null &&
                                   !HasCorpse() &&
                                   !IsDespawnScheduled &&
                                   !IsSpawnScheduled &&
                                   IsOptimalSpawner() &&
                                   IsSpawningScheduleEnabled() &&
                                   CheckSpawnCountCanSpawn();

        /// <summary>
        /// Determines whether spawning NPCs is allowed based on various conditions.
        /// </summary>
        private bool CanSpawnNpcs()
        {
            if (!CanSpawn()) return false;
            if (IsSpawnDelayNotElapsed()) return false;
            return true;
        }

        private bool IsSpawnDelayNotElapsed()
        {
            if (_lastSpawnTime == DateTime.MinValue) return false;
            return (DateTime.UtcNow - _lastSpawnTime).TotalSeconds < Template.SpawnDelayMin;
        }

        /// <summary>
        /// Spawns NPCs based on the current spawner configuration and conditions.
        /// </summary>
        /// <remarks>This method attempts to spawn NPCs using the available templates and spawner
        /// settings.  It checks preconditions such as the maximum population limit and suspended spawn count  before
        /// proceeding. If spawning is successful, the spawned NPCs are added to the active  list and the spawn count is
        /// updated.</remarks>
        public void DoSpawn()
        {
            if (Template == null)
            {
                Logger.Error($"[Spawn] Can't spawn npc {UnitId} from spawnerId {Id} - Template is null");
                return;
            }

            if (CurrentSpawnCount >= Template.MaxPopulation) return;
            if (Template.SuspendSpawnCount > 0 && CurrentSpawnCount > Template.SuspendSpawnCount) return;
            if (SpawnableNpcs == null || SpawnableNpcs.Count == 0) return;

            var spawnedNpcs = new List<Npc>();
            foreach (var npcTemplate in SpawnableNpcs)
            {
                try
                {
                    lock (SpawnLock)
                    {
                        var spawned = npcTemplate.Spawn(this);
                        if (spawned == null || spawned.Count == 0) continue;

                        spawnedNpcs.AddRange(spawned);
                        foreach (var npc in spawned)
                        {
                            npc.Transform.Local.Position = AdjustSpawnPosition(npc);
                            AddNpcToSpawned(npc.Spawner.SpawnerId, npc);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"[Spawn] Failed to spawn NPC from template {npcTemplate?.SpawnerId}:{npcTemplate?.MemberId}");
                }
            }

            if (spawnedNpcs.Count == 0) return;

            DecrementCount(spawnedNpcs);
            _lastSpawnTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Initiates the spawning process for all entities.
        /// </summary>
        /// <remarks>This method does not perform any action if the spawning schedule is enabled. Ensure
        /// that the spawning schedule is disabled before calling this method.</remarks>
        /// <param name="beginning">A value indicating whether the spawning process is starting from the beginning. If <see langword="true"/>,
        /// the spawning process starts from the initial state; otherwise, it continues from the current state.</param>
        public void SpawnAll(bool beginning = false)
        {
            if (IsSpawningScheduleEnabled()) return;
            DoSpawn();
        }

        /// <summary>
        /// Spawns an NPC associated with the current spawner.
        /// </summary>
        /// <remarks>This method attempts to spawn an NPC and retrieve the first NPC associated with the
        /// spawner. If no NPCs are available, the method returns <see langword="null"/>.</remarks>
        /// <param name="objId">The unique identifier for the object to be spawned.</param>
        /// <returns>The first NPC spawned by the spawner if successful; otherwise, <see langword="null"/> if no NPCs were
        /// spawned.</returns>
        public override Npc Spawn(uint objId)
        {
            DoSpawn();
            return SpawnedNpcs.TryGetValue(SpawnerId, out var list) && list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// Forces the spawning of an NPC associated with the specified object ID.
        /// </summary>
        /// <remarks>If no NPCs have been initialized, this method will ensure that spawnable NPCs are
        /// prepared before attempting to spawn.</remarks>
        /// <param name="objId">The unique identifier of the object for which the NPC should be spawned.</param>
        /// <returns>The first spawned NPC associated with the spawner, or <see langword="null"/> if no NPCs were successfully
        /// spawned.</returns>
        public override Npc ForceSpawn(uint objId)
        {
            if (SpawnedNpcs.Count == 0) InitializeSpawnableNpcs(Template);
            DoSpawn();
            return SpawnedNpcs.TryGetValue(SpawnerId, out var list) && list.Count > 0 ? list[0] : null;
        }

        private void AddNpcToSpawned(uint key, Npc newNpc)
        {
            if (newNpc == null) return;
            SpawnedNpcs.AddOrUpdate(key, _ =>
                    [newNpc], updateValueFactory: (_, existing) => {
                    lock (existing)
                    {
                        existing.Add(newNpc);
                    }

                    return existing; });
        }
 
        public void SetSpawnScheduled(bool value)
        {
            IsSpawnScheduled = value;
        }
        #endregion

        #region Despawn Logic
        private bool CanDespawnNpcs() =>
            IsDespawningScheduleEnabled(SpawnerId);

        private void DespawnNpcs()
        {
            if (IsDespawnScheduled) return;
            if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcs)) DoDespawns(npcs);
        }

        private void DespawnNpcsNow()
        {
            if (IsDespawnScheduled) return;
            if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcs)) DoDespawnsNow(npcs);
        }

        /// <summary>
        /// Schedules despawn for a list of NPCs.
        /// </summary>
        /// <param name="npcs"></param>
        internal void DoDespawns(List<Npc> npcs)
        {
            if (npcs == null) return;
            lock (SpawnLock)
            {
                IsDespawnScheduled = true;
                foreach (var npc in npcs.ToList())
                {
                    try { DoDespawn(npc); }
                    catch (Exception ex) { Logger.Error(ex, $"DoDespawns failed for {npc?.ObjId}"); }
                }
                IsDespawnScheduled = false;
            }
        }

        private void DoDespawnsNow(List<Npc> npcs)
        {
            if (npcs == null) return;
            lock (SpawnLock)
            {
                IsDespawnScheduled = true;
                foreach (var npc in npcs.ToList())
                {
                    try { DoDespawnNow(npc); }
                    catch (Exception ex) { Logger.Error(ex, $"DoDespawnsNow failed for {npc?.ObjId}"); }
                }
                IsDespawnScheduled = false;
            }
        }

        /// <summary>
        /// Despawns the NPC and schedules it for respawn if conditions are met.
        /// </summary>
        /// <param name="npc"></param>
        public override void Despawn(Npc npc)
        {
            if (npc == null) return;
            try
            {
                lock (SpawnLock)
                {
                    RemoveNpcFromSpawnedList(npc);
                    UnregisterAndDeleteNpc(npc);
                    npc.IsDespawnScheduled = false;
                    IsDespawnScheduled = false;
                }
            }
            catch (Exception ex) { Logger.Error(ex, $"Failed to despawn NPC {npc.TemplateId}"); }
        }

        /// <summary>
        /// Despawns the NPC and schedules it for respawn if conditions are met.
        /// </summary>
        /// <param name="npc"></param>
        public void DespawnWithRespawn(Npc npc)
        {
            if (npc == null) return;
            npc.Delete();
            if (RespawnTime > 0 && AreOtherNpcsInSpawnZone().Item2 < Template.MaxPopulation)
            {
                npc.Respawn = DateTime.UtcNow.AddSeconds(RespawnTime);
                SpawnManager.Instance.AddRespawn(npc);
                IncrementCount(true);
            }
        }

        private void RemoveNpcFromSpawnedList(Npc npc)
        {
            if (npc.Spawner == null) return;
            var id = npc.Spawner.SpawnerId;
            lock (SpawnLock)
            {
                if (SpawnedNpcs.TryGetValue(id, out var list))
                {
                    lock (list)
                    {
                        list.Remove(npc);
                        if (list.Count == 0) SpawnedNpcs.TryRemove(id, out _);
                    }
                }
            }
        }

        private void DoDespawn(Npc npc)
        {
            lock (SpawnLock)
            {
                if (RespawnTime > 0 &&
                    AreOtherNpcsInSpawnZone().Item2 + _scheduledCount < Template.MaxPopulation)
                {
                    IncrementCount(true);
                    npc.Respawn = DateTime.UtcNow.AddSeconds(RespawnTime);
                    SpawnManager.Instance.AddRespawn(npc);
                }
                else IncrementCount(false);

                npc.Despawn = DateTime.UtcNow.AddSeconds(DespawnTime);
                if (npc.LootingContainer?.Items.Count > 0)
                    npc.Despawn += TimeSpan.FromSeconds(LootingContainer.LootDespawnExtensionTime);

                SpawnManager.Instance.AddDespawn(npc);
            }
        }

        private void DoDespawnNow(Npc npc)
        {
            lock (SpawnLock)
            {
                if (AreOtherNpcsInSpawnZone().Item2 + _scheduledCount < Template.MaxPopulation)
                    IncrementCount(true);
                SpawnManager.Instance.AddDespawn(npc);
            }
        }
        
        private static void UnregisterAndDeleteNpc(Npc npc)
        {
            npc.UnregisterNpcEvents();
            npc.Delete();
        }
        #endregion

        #region Schedule Helpers
        private bool IsSpawningScheduleEnabled()
        {
            if (Template == null) return false;

            IsSpawnScheduled = false;
            var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)SpawnerId);

            switch (status)
            {
                case GameScheduleManager.PeriodStatus.InProgress:
                    IsSpawnScheduled = true;
                    return true;
                case GameScheduleManager.PeriodStatus.NotStarted:
                case GameScheduleManager.PeriodStatus.Ended:
                    return false;
                case GameScheduleManager.PeriodStatus.NotFound:
                    break;
                default: return false;
            }

            if (IsWithinSpawnTime())
            {
                IsSpawnScheduled = true;
                return true;
            }

            return false;
        }

        private bool IsDespawningScheduleEnabled(uint spawnerId)
        {
            if (!SpawnedNpcs.TryGetValue(spawnerId, out var npcs)) return false;
            return npcs.Any(npc => IsWithinDespawnTime(npc) || IsNpcInTimeWindow(npc));
        }

        private bool IsWithinSpawnTime()
        {
            if (Template.StartTime <= 0 && Template.EndTime <= 0) return true;
            var curTime = TimeSpan.FromHours(TimeManager.Instance.GetTime);
            var start = TimeSpan.FromHours(Template.StartTime);
            var end = TimeSpan.FromHours(Template.EndTime);
            return IsTimeBetween(curTime, start, end);
        }

        private static bool IsWithinDespawnTime(Npc npc)
        {
            var t = npc.Spawner?.Template;
            if (t == null || (t.StartTime <= 0 && t.EndTime <= 0)) return false;
            var cur = TimeSpan.FromHours(TimeManager.Instance.GetTime);
            var s = TimeSpan.FromHours(t.StartTime);
            var e = TimeSpan.FromHours(t.EndTime);
            return !IsTimeBetween(cur, s, e);
        }

        private static bool IsNpcInTimeWindow(Npc npc)
        {
            var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)npc.Spawner.Template.Id);
            return status == GameScheduleManager.PeriodStatus.InProgress;
        }

        private static bool IsTimeBetween(TimeSpan now, TimeSpan start, TimeSpan end) =>
            start <= end ? now >= start && now <= end : now >= start || now <= end;
        #endregion

        #region Optimal Spawner Selection

        private bool IsOptimalSpawner() => SpawnerId == SelectSpawnerId();

        private uint? SelectSpawnerId()
        {
            if (NpcSpawnerIds.Count == 1) return SpawnerId;

            if (Template.NpcSpawnerCategoryId == NpcSpawnerCategory.Autocreated && !HasScheduledSpawner())
                return SpawnerId;

            return IsThereSpawningSchedule() ? SpawnerId : null;
        }

        private bool IsThereSpawningSchedule()
        {
            var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)SpawnerId);
            if (status != GameScheduleManager.PeriodStatus.NotFound) return true;
            return HasSpawningTime();
        }

        private bool HasSpawningTime() => Template.StartTime > 0 || Template.EndTime > 0;

        private bool HasScheduledSpawner()
        {
            foreach (var id in NpcSpawnerIds)
            {
                var t = NpcGameData.Instance.GetNpcSpawnerTemplate(id);
                if (t != null && (t.StartTime > 0 || t.EndTime > 0 || CheckGameScheduleStatus(t)))
                    return true;
            }
            return false;
        }

        private static bool CheckGameScheduleStatus(NpcSpawnerTemplate t)
        {
            var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)t.Id);
            return status != GameScheduleManager.PeriodStatus.NotFound;
        }
        #endregion

        #region Count & Cache Helpers
        private bool CheckSpawnCountCanSpawn()
        {
            var min = Template.MinPopulation == 0 ? 1 : Template.MinPopulation;
            var max = Template.MaxPopulation;
            var pc = Math.Max(GetNumberOfPlayerInSpawnRadius(Template), 1);

            if (pc < min) max = (uint)pc;
            else if (pc >= min && pc <= max) max = (uint)pc;

            var total = CurrentSpawnCount + AreOtherNpcsInSpawnZone().Item2;
            if (Template.SuspendSpawnCount > 0 && total >= Template.SuspendSpawnCount) return false;
            return total < max;
        }

        private int GetNumberOfPlayerInSpawnRadius(NpcSpawnerTemplate template)
        {
            if (_playerCountCache.TryGetValue((int)SpawnerId, out var c) &&
                (DateTime.UtcNow - c.LastUpdate).TotalSeconds < 10)
                return c.PlayerCount;

            var count = 0;
            if (template?.TestRadiusNpc > 0)
            {
                var npcs = SpawnedNpcs.Values.FirstOrDefault();
                if (npcs?.Count > 0)
                    count = WorldManager.GetAround<Character>(npcs[0], template.TestRadiusNpc * 50).Count;
            }

            _playerCountCache[(int)SpawnerId] = new SpawnerPlayerCountCache
            {
                PlayerCount = count,
                LastUpdate = DateTime.UtcNow
            };
            return count;
        }

        private (bool, int) AreOtherNpcsInSpawnZone()
        {
            if (_npcsInZoneCache.TryGetValue((int)SpawnerId, out var c) &&
                (DateTime.UtcNow - c.LastUpdate).TotalSeconds < 10)
                return (c.AreNpcsInZone, c.Count);

            var count = 0;
            var any = false;
            foreach (var kv in SpawnedNpcs)
            {
                if (kv.Key == SpawnerId) continue;
                count += kv.Value.Count;
                if (kv.Value.Count > 0) any = true;
            }

            _npcsInZoneCache[(int)SpawnerId] = new SpawnerNpcsInZoneCache
            {
                AreNpcsInZone = any,
                Count = count,
                LastUpdate = DateTime.UtcNow
            };
            return (any, count);
        }

        internal bool IsPlayerInSpawnRadius()
        {
            var radius = Template.TestRadiusPc == 0 ? Template.TestRadiusNpc : Template.TestRadiusPc;
            if (_playerInRadiusCache.TryGetValue((int)SpawnerId, out var c) &&
                (DateTime.UtcNow - c.LastUpdate).TotalSeconds < 10)
                return c.IsPlayerInRadius;

            foreach (var p in WorldManager.Instance.GetAllCharacters())
            {
                var dist = MathUtil.CalculateDistance(p.Transform.World.Position,
                    new Vector3(Position.X, Position.Y, Position.Z));
                if (dist <= radius * 50f)
                {
                    _playerInRadiusCache[(int)SpawnerId] = new SpawnerPlayerInRadiusCache
                    {
                        IsPlayerInRadius = true,
                        LastUpdate = DateTime.UtcNow
                    };
                    return true;
                }
            }

            _playerInRadiusCache[(int)SpawnerId] = new SpawnerPlayerInRadiusCache
            {
                IsPlayerInRadius = false,
                LastUpdate = DateTime.UtcNow
            };
            return false;
        }

        private bool HasCorpse()
        {
            if (!SpawnedNpcs.TryGetValue(SpawnerId, out var npcs)) return false;
            return npcs.Any(npc => npc.IsDead);
        }

        private void DecrementCount(List<Npc> n)
        {
            lock (SpawnLock)
            {
                if (_scheduledCount > 0)
                    Interlocked.Add(ref _scheduledCount, -n.Count);
            }
        }

        private void IncrementCount(bool respawn = false)
        {
            if (!respawn) return;
            lock (SpawnLock)
            {
                var val = Interlocked.Increment(ref _scheduledCount);
                if (val < 0) Interlocked.Exchange(ref _scheduledCount, 0);
            }
        }
        #endregion

        #region Event / Random / Effect Spawns
        /// <summary>
        /// Initiates the spawning of NPCs based on the current template and spawn conditions.
        /// </summary>
        /// <remarks>This method checks the current spawn count and template constraints before attempting
        /// to spawn NPCs. If the conditions are met, it retrieves the appropriate NPC configuration and spawns NPC
        /// instances. The spawned NPCs are then added to the active spawn list, and the spawn count is updated
        /// accordingly.</remarks>
        public void DoEventSpawn()
        {
            if (Template == null || CurrentSpawnCount >= Template.MaxPopulation) return;
            var nsn = Template.Npcs.FirstOrDefault(n => n.MemberId == UnitId);
            if (nsn == null) return;

            var spawned = nsn.Spawn(this);
            foreach (var npc in spawned) AddNpcToSpawned(SpawnerId, npc);
            DecrementCount(spawned);
        }

        /// <summary>
        /// Spawns a random NPC from the specified spawner template.
        /// </summary>
        /// <remarks>This method selects an NPC template from the specified spawner based on weighted
        /// probabilities and attempts to spawn one or more NPCs. If successful, the first spawned NPC is returned. If
        /// the spawner template is invalid, contains no NPCs, or an error occurs during spawning, the method returns
        /// <see langword="null"/>.</remarks>
        /// <param name="spawnerId">The unique identifier of the spawner template to use for spawning.</param>
        /// <param name="ownerId">The unique identifier of the owner associated with the spawned NPC. Defaults to 0 if not specified.</param>
        /// <returns>The first NPC spawned from the template, or <see langword="null"/> if spawning fails or no NPCs are
        /// available.</returns>
        public Npc DoRandomSpawn(uint spawnerId, uint ownerId = 0)
        {
            var template = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
            if (template?.Npcs == null) return null;

            var npcTemplate = template.Npcs.RandomElementByWeight(x => x.Weight);
            if (npcTemplate == null) return null;

            try
            {
                var npc = NpcManager.Instance.Create(0, npcTemplate.MemberId);
                if (npc == null) return null;

                var spawned = npcTemplate.Spawn(this, ownerId);
                if (spawned == null || spawned.Count == 0) return null;

                var spawnedNpc = spawned.First();
                lock (SpawnLock) AddNpcToSpawned(spawnedNpc.Spawner.SpawnerId, spawnedNpc);
                spawnedNpc.Spawn();
                return spawnedNpc;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "DoRandomSpawn failed");
                return null;
            }
        }

        /// <summary>
        /// Spawns NPCs based on the specified spawner ID and applies the given spawn effect.
        /// </summary>
        /// <remarks>This method retrieves the NPC spawner template associated with the given <paramref
        /// name="spawnerId"/> and spawns NPCs according to the template configuration. The spawned NPCs can have their
        /// faction and aggro behavior modified based on the properties of the <paramref name="effect"/> and the
        /// provided <paramref name="caster"/> and <paramref name="target"/>.  If the <paramref name="effect"/>
        /// specifies a lifetime, the spawned NPCs will be automatically despawned after the specified
        /// duration.</remarks>
        /// <param name="spawnerId">The unique identifier of the NPC spawner to use.</param>
        /// <param name="effect">The spawn effect to apply to the spawned NPCs, including faction and aggro settings.</param>
        /// <param name="caster">The unit responsible for initiating the spawn effect. This may influence faction or aggro behavior.</param>
        /// <param name="target">The target unit that may influence the spawned NPCs' faction or aggro behavior.</param>
        public void DoSpawnEffect(uint spawnerId, SpawnEffect effect, BaseUnit caster, BaseUnit target)
        {
            var template = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
            if (template?.Npcs == null) return;

            var nsn = template.Npcs.FirstOrDefault(n => n.MemberId == UnitId);
            if (nsn == null) return;

            var npcs = nsn.Spawn(this);
            foreach (var npc in npcs)
            {
                if (effect.UseSummonerFaction)
                    npc.Faction = target is Npc ? target.Faction : caster.Faction;

                if (effect.UseSummonerAggroTarget && !effect.UseSummonerFaction)
                {
                    var aggroTarget = target is Npc ? target : caster;
                    npc.Ai.Owner.AddUnitAggro(AggroKind.Damage, (Unit)aggroTarget, 1);
                    npc.Ai.OnAggroTargetChanged();
                }

                if (effect.LifeTime > 0)
                    TaskManager.Instance.Schedule(new NpcSpawnerDoDespawnTask(npc),
                        TimeSpan.FromSeconds(effect.LifeTime));

                AddNpcToSpawned(SpawnerId, npc);
            }

            if (_scheduledCount > 0)
                Interlocked.Add(ref _scheduledCount, -npcs.Count);
        }
        #endregion

        #region Position Helpers

        private static Vector3 AdjustSpawnPosition(Npc npc, int maxAttempts = 15)
        {
            var radius = GetCollisionRadiusForNpc(npc);
            var original = npc.Transform.CloneAsSpawnPosition().ToVector3();
            for (var i = 0; i < maxAttempts; i++)
            {
                var pos = Vector3.Add(original,
                    new Vector3((float)(NextDouble() * radius * 2 - radius),
                                (float)(NextDouble() * radius * 2 - radius), 0));

                if (!WorldManager.GetAround<Npc>(npc, radius * 2)
                    .Any(n => CheckCollision(pos, n.Transform.Local.Position, radius)))
                    return pos;
            }
            return original;
        }

        /// <summary>
        /// Adjusts the NPC's position to avoid collisions with other NPCs in the vicinity.
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="maxAttempts"></param>
        /// <returns></returns>
        public static Vector3 AdjustMovePosition(Npc npc, int maxAttempts = 5)
        {
            var collisionRadius = GetCollisionRadiusForNpc(npc);
            var originalPos = npc.Transform.CloneAsSpawnPosition();
            var currentPos = originalPos.ToVector3();

            for (var i = 0; i < maxAttempts; i++)
            {
                // Check collisions with existing entities
                var hasCollision = WorldManager.GetAround<Npc>(npc, collisionRadius * 2)
                    .Where(n => n is not null)
                    .Any(n => CheckCollision(currentPos, n.Transform.Local.Position, collisionRadius));

                if (!hasCollision)
                {
                    //if (i > 0)
                    //{
                    //    Logger.Debug($"Adjusted NPC position after {i + 1} attempts");
                    //}
                    return currentPos;
                }

                // Generate new position with random offset
                currentPos = Vector3.Add(originalPos.ToVector3(), new Vector3((float)(NextDouble() * collisionRadius * 2 - collisionRadius), (float)(NextDouble() * collisionRadius * 2 - collisionRadius), 0f));
            }

            return currentPos;
        }

        private static bool CheckCollision(Vector3 pos1, Vector3 pos2, float radius)
        {
            // Calculate horizontal distance (ignore Z-axis)
            var dx = pos1.X - pos2.X;
            var dy = pos1.Y - pos2.Y;
            return dx * dx + dy * dy < radius * radius;
        }

        private static float GetCollisionRadiusForNpc(Npc npc) => npc.ModelSize > 0 ? npc.ModelSize : 1.5f;
        #endregion

        #region Clone Helper
        public static T Clone<T>(T obj) =>
            (T)obj.GetType()
                  .GetMethod("MemberwiseClone",
                      System.Reflection.BindingFlags.Instance |
                      System.Reflection.BindingFlags.NonPublic)?
                  .Invoke(obj, null);
        #endregion

        #region Cache Structs
        private struct SpawnerPlayerCountCache
        {
            public int PlayerCount;
            public DateTime LastUpdate;
        }

        private struct SpawnerPlayerInRadiusCache
        {
            public bool IsPlayerInRadius;
            public DateTime LastUpdate;
        }

        private struct SpawnerNpcsInZoneCache
        {
            public int Count;
            public bool AreNpcsInZone;
            public DateTime LastUpdate;
        }
        #endregion
    }
}
