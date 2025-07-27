using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

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
// ReSharper disable ForCanBeConvertedToForeach - Используется для оптимизации циклов
// ReSharper disable ForCanBeConvertedToForeach - Used for loop optimization

namespace AAEmu.Game.Models.Game.NPChar
{
    /// <summary>
    /// Manages the spawning and despawning of NPCs based on various conditions like schedules, player proximity, and population limits.
    /// </summary>
    public class NpcSpawner : Spawner<Npc>
    {
        #region Static & Const

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly object SpawnLock = new(); // Глобальная блокировка для операций спавна/деспавна

        #endregion

        #region Fields

        private int _scheduledCount; // Количество запланированных к спавну NPC
        private DateTime _lastSpawnTime = DateTime.MinValue; // Время последнего спавна
        private readonly TimeSpan _cacheLifetime = TimeSpan.FromSeconds(10); // Время жизни кэша
        private readonly ReaderWriterLockSlim _spawnLock = new(); // Блокировка для основного Update
        private readonly ConcurrentDictionary<int, SpawnerPlayerCountCache> _playerCountCache = new(); // Кэш количества игроков
        private readonly ConcurrentDictionary<int, SpawnerPlayerInRadiusCache> _playerInRadiusCache = new(); // Кэш наличия игроков рядом
        private readonly ConcurrentDictionary<int, SpawnerNpcsInZoneCache> _npcsInZoneCache = new(); // Кэш других NPC в зоне

        #endregion

        #region Properties

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(1f)]
        public uint Count { get; set; } = 1;

        public List<uint> NpcSpawnerIds { get; set; } = [];

        public NpcSpawnerTemplate Template { get; set; }

        private List<NpcSpawnerNpc> SpawnableNpcs { get; set; } = [];

        // Хранит список заспавненных NPC по SpawnerId
        private ConcurrentDictionary<uint, List<Npc>> SpawnedNpcs { get; } = new();

        // Текущее количество заспавненных NPC для этого спавнера
        private int CurrentSpawnCount => SpawnedNpcs.TryGetValue(SpawnerId, out var list) ? list.Count : 0;

        private bool IsSpawnScheduled { get; set; } // Флаг, установлен ли спавн по расписанию
        private bool IsDespawnScheduled { get; set; } // Флаг, установлен ли деспавн
        private bool RespawnDenied { get; set; } // Флаг, запрещен ли респавн (не используется в текущем коде)

        #endregion

        #region Ctors

        public NpcSpawner()
        {
            IsSpawnScheduled = false;
            IsDespawnScheduled = false;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the list of NPCs that can be spawned from this spawner template.
        /// </summary>
        /// <param name="template">The spawner template containing NPC definitions.</param>
        internal void InitializeSpawnableNpcs(NpcSpawnerTemplate template)
        {
            if (template?.Npcs == null)
            {
                Logger.Warn("Template or template.Npcs is null. SpawnableNpcs will not be initialized.");
                return;
            }

            SpawnableNpcs = [.. template.Npcs];
        }

        #endregion

        #region Public Update

        /// <summary>
        /// Main update method for the NpcSpawner. Checks conditions and performs spawn/despawn actions.
        /// </summary>
        public void Update()
        {
            try
            {
                _spawnLock.EnterUpgradeableReadLock();
                try
                {
                    var didAction = false;

                    if (CanDespawnNpcs())
                    {
                        _spawnLock.EnterWriteLock();
                        try
                        {
                            DespawnNpcs();
                            didAction = true;
                        }
                        finally
                        {
                            _spawnLock.ExitWriteLock();
                        }
                    }
                    else if (!IsPlayerInSpawnRadius() && CurrentSpawnCount > 0)
                    {
                        _spawnLock.EnterWriteLock();
                        try
                        {
                            DespawnNpcsNow();
                            didAction = true;
                        }
                        finally
                        {
                            _spawnLock.ExitWriteLock();
                        }
                    }

                    if (!didAction && CanSpawnNpcs())
                    {
                        _spawnLock.EnterWriteLock();
                        try
                        {
                            DoSpawn();
                            didAction = true;
                        }
                        finally
                        {
                            _spawnLock.ExitWriteLock();
                        }
                    }

                    // Периодически очищаем устаревший кэш
                    if (!didAction)
                    {
                        CleanupCache();
                    }
                }
                finally
                {
                    _spawnLock.ExitUpgradeableReadLock();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error during NpcSpawner update [SpawnerId={SpawnerId}, UnitId={UnitId}]");
            }
        }

        /// <summary>
        /// Cleans up expired cache entries.
        /// </summary>
        private void CleanupCache()
        {
            var now = DateTime.UtcNow;

            // Очистка кэша количества игроков
            var expiredPlayerCountKeys = _playerCountCache
                .Where(kvp => (now - kvp.Value.LastUpdate) > _cacheLifetime)
                .Select(kvp => kvp.Key)
                .ToList(); // Создаем список для безопасного перебора

            foreach (var key in expiredPlayerCountKeys)
            {
                _playerCountCache.TryRemove(key, out _);
            }

            // Очистка кэша игроков в радиусе
            var expiredPlayerInRadiusKeys = _playerInRadiusCache
                .Where(kvp => (now - kvp.Value.LastUpdate) > _cacheLifetime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredPlayerInRadiusKeys)
            {
                _playerInRadiusCache.TryRemove(key, out _);
            }

            // Очистка кэша других NPC в зоне
            var expiredNpcsInZoneKeys = _npcsInZoneCache
                .Where(kvp => (now - kvp.Value.LastUpdate) > _cacheLifetime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredNpcsInZoneKeys)
            {
                _npcsInZoneCache.TryRemove(key, out _);
            }
        }

        #endregion

        #region Spawn Logic

        /// <summary>
        /// Determines whether basic spawning conditions are met.
        /// </summary>
        /// <returns>True if basic conditions allow spawning.</returns>
        private bool CanSpawn() => Template != null &&
                                   !HasCorpse() &&
                                   !IsDespawnScheduled &&
                                   !IsSpawnScheduled &&
                                   IsOptimalSpawner() &&
                                   IsSpawningScheduleEnabled() &&
                                   CheckSpawnCountCanSpawn();

        /// <summary>
        /// Determines whether all conditions for spawning NPCs are met.
        /// </summary>
        /// <returns>True if spawning is allowed.</returns>
        private bool CanSpawnNpcs()
        {
            if (!CanSpawn()) return false;
            if (IsSpawnDelayNotElapsed()) return false;
            return true;
        }

        /// <summary>
        /// Checks if the minimum spawn delay has elapsed since the last spawn.
        /// </summary>
        /// <returns>True if the delay has not elapsed.</returns>
        private bool IsSpawnDelayNotElapsed()
        {
            if (_lastSpawnTime == DateTime.MinValue) return false;
            return (DateTime.UtcNow - _lastSpawnTime).TotalSeconds < Template.SpawnDelayMin;
        }

        /// <summary>
        /// Spawns NPCs based on the current spawner configuration and conditions.
        /// </summary>
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
                    lock (SpawnLock) // Блокируем глобально для операции спавна
                    {
                        var spawned = npcTemplate.Spawn(this);
                        if (spawned == null || spawned.Count == 0) continue;

                        spawnedNpcs.AddRange(spawned);
                        foreach (var npc in spawned)
                        {
                            // Корректируем позицию для избежания коллизий
                            npc.Transform.Local.Position = AdjustSpawnPosition(npc);
                            AddNpcToSpawned(npc.Spawner.SpawnerId, npc);
                            RaiseNpcSpawned(npc); // Вызываем событие спавна
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"[Spawn] Failed to spawn NPC from template {npcTemplate?.SpawnerId}:{npcTemplate?.MemberId}");
                }
            }

            if (spawnedNpcs.Count == 0) return;

            // Уменьшаем счетчик запланированных NPC
            DecrementCount(spawnedNpcs.Count);
            _lastSpawnTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Initiates the spawning process for all entities, unless a schedule is active.
        /// </summary>
        /// <param name="beginning">Unused parameter.</param>
        public void SpawnAll(bool beginning = false)
        {
            if (IsSpawningScheduleEnabled()) return;
            DoSpawn();
        }

        /// <summary>
        /// Spawns an NPC associated with the current spawner.
        /// </summary>
        /// <param name="objId">The unique identifier for the object to be spawned.</param>
        /// <returns>The first NPC spawned by the spawner if successful; otherwise, null.</returns>
        public override Npc Spawn(uint objId)
        {
            DoSpawn();
            return SpawnedNpcs.TryGetValue(SpawnerId, out var list) && list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// Forces the spawning of an NPC associated with the specified object ID.
        /// </summary>
        /// <param name="objId">The unique identifier of the object for which the NPC should be spawned.</param>
        /// <returns>The first spawned NPC associated with the spawner, or null if no NPCs were successfully spawned.</returns>
        public override Npc ForceSpawn(uint objId)
        {
            if (SpawnedNpcs.Count == 0) InitializeSpawnableNpcs(Template);
            DoSpawn();
            return SpawnedNpcs.TryGetValue(SpawnerId, out var list) && list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// Asynchronously spawns an NPC.
        /// </summary>
        public async Task<Npc> SpawnAsync(uint objId)
        {
            await Task.Run(() => DoSpawn());
            return SpawnedNpcs.TryGetValue(SpawnerId, out var list) && list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// Asynchronously forces the spawning of an NPC.
        /// </summary>
        public async Task<Npc> ForceSpawnAsync(uint objId)
        {
            if (SpawnedNpcs.Count == 0) InitializeSpawnableNpcs(Template);
            await Task.Run(() => DoSpawn());
            return SpawnedNpcs.TryGetValue(SpawnerId, out var list) && list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// Adds a newly spawned NPC to the internal tracking list.
        /// </summary>
        private void AddNpcToSpawned(uint key, Npc newNpc)
        {
            if (newNpc == null) return;

            SpawnedNpcs.AddOrUpdate(key,
                _ => [newNpc],
                (_, existing) =>
                {
                    lock (existing) // Блокируем список для безопасного добавления
                    {
                        existing.Add(newNpc);
                    }
                    return existing;
                });
        }

        /// <summary>
        /// Sets the spawn scheduled flag.
        /// </summary>
        public void SetSpawnScheduled(bool value)
        {
            IsSpawnScheduled = value;
        }

        #endregion

        #region Despawn Logic

        /// <summary>
        /// Checks if despawning conditions are met.
        /// </summary>
        private bool CanDespawnNpcs() => IsDespawningScheduleEnabled(SpawnerId);

        /// <summary>
        /// Schedules despawning of NPCs if conditions are met.
        /// </summary>
        private void DespawnNpcs()
        {
            if (IsDespawnScheduled) return;
            if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcs)) DoDespawns(npcs);
        }

        /// <summary>
        /// Immediately despawns NPCs if no players are in range.
        /// </summary>
        private void DespawnNpcsNow()
        {
            if (IsDespawnScheduled) return;
            if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcs)) DoDespawnsNow(npcs);
        }

        /// <summary>
        /// Schedules despawn for a list of NPCs.
        /// </summary>
        internal void DoDespawns(List<Npc> npcs)
        {
            if (npcs == null) return;

            lock (SpawnLock) // Блокируем глобально для операции деспавна
            {
                IsDespawnScheduled = true;
                // Используем ToList() для создания копии списка, чтобы избежать модификации во время итерации
                foreach (var npc in npcs.ToList())
                {
                    try
                    {
                        DoDespawn(npc);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"DoDespawns failed for {npc?.ObjId}");
                    }
                }
                IsDespawnScheduled = false;
            }
        }

        /// <summary>
        /// Immediately despawns a list of NPCs.
        /// </summary>
        private void DoDespawnsNow(List<Npc> npcs)
        {
            if (npcs == null) return;

            lock (SpawnLock)
            {
                IsDespawnScheduled = true;
                foreach (var npc in npcs.ToList())
                {
                    try
                    {
                        DoDespawnNow(npc);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"DoDespawnsNow failed for {npc?.ObjId}");
                    }
                }
                IsDespawnScheduled = false;
            }
        }

        /// <summary>
        /// Despawns the NPC and schedules it for respawn if conditions are met.
        /// </summary>
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
                    RaiseNpcDespawned(npc); // Вызываем событие деспавна
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to despawn NPC {npc.TemplateId}");
            }
        }

        /// <summary>
        /// Despawns the NPC and schedules it for respawn if conditions are met.
        /// </summary>
        public void DespawnWithRespawn(Npc npc)
        {
            if (npc == null) return;

            npc.Delete();
            if (RespawnTime > 0 && AreOtherNpcsInSpawnZone().Item2 < Template.MaxPopulation)
            {
                npc.Respawn = DateTime.UtcNow.AddSeconds(RespawnTime);
                SpawnManager.Instance.AddRespawn(npc);
                IncrementCount(true); // Увеличиваем счетчик для респавна
            }
        }

        /// <summary>
        /// Removes an NPC from the internal tracking list.
        /// </summary>
        private void RemoveNpcFromSpawnedList(Npc npc)
        {
            if (npc.Spawner == null) return;

            var id = npc.Spawner.SpawnerId;
            lock (SpawnLock)
            {
                if (SpawnedNpcs.TryGetValue(id, out var list))
                {
                    lock (list) // Блокируем список для безопасного удаления
                    {
                        list.Remove(npc);
                        if (list.Count == 0) SpawnedNpcs.TryRemove(id, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Schedules an NPC for despawn and potentially respawn.
        /// </summary>
        private void DoDespawn(Npc npc)
        {
            lock (SpawnLock)
            {
                // Проверяем, нужно ли планировать респавн
                if (RespawnTime > 0 &&
                    AreOtherNpcsInSpawnZone().Item2 + _scheduledCount < Template.MaxPopulation)
                {
                    IncrementCount(true); // Увеличиваем счетчик для респавна
                    npc.Respawn = DateTime.UtcNow.AddSeconds(RespawnTime);
                    SpawnManager.Instance.AddRespawn(npc);
                }
                else
                {
                    IncrementCount(false); // Просто увеличиваем счетчик
                }

                npc.Despawn = DateTime.UtcNow.AddSeconds(DespawnTime);
                // Увеличиваем время деспавна, если есть лут
                if (npc.LootingContainer?.Items.Count > 0)
                    npc.Despawn += TimeSpan.FromSeconds(LootingContainer.LootDespawnExtensionTime);

                SpawnManager.Instance.AddDespawn(npc);
            }
        }

        /// <summary>
        /// Immediately schedules an NPC for despawn.
        /// </summary>
        private void DoDespawnNow(Npc npc)
        {
            lock (SpawnLock)
            {
                // Проверяем, нужно ли планировать респавн
                if (AreOtherNpcsInSpawnZone().Item2 + _scheduledCount < Template.MaxPopulation)
                    IncrementCount(true); // Увеличиваем счетчик для респавна

                SpawnManager.Instance.AddDespawn(npc);
            }
        }

        /// <summary>
        /// Unregisters NPC events and deletes the NPC object.
        /// </summary>
        private static void UnregisterAndDeleteNpc(Npc npc)
        {
            npc.UnregisterNpcEvents();
            npc.Delete();
        }

        #endregion

        #region Schedule Helpers

        /// <summary>
        /// Checks if the spawning schedule is currently enabled.
        /// </summary>
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
                default:
                    return false;
            }

            if (IsWithinSpawnTime())
            {
                IsSpawnScheduled = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the despawning schedule is enabled for a specific spawner.
        /// </summary>
        private bool IsDespawningScheduleEnabled(uint spawnerId)
        {
            if (!SpawnedNpcs.TryGetValue(spawnerId, out var npcs)) return false;
            return npcs.Any(npc => IsWithinDespawnTime(npc) || IsNpcInTimeWindow(npc));
        }

        /// <summary>
        /// Checks if the current time is within the spawner's defined spawn time window.
        /// </summary>
        private bool IsWithinSpawnTime()
        {
            if (Template.StartTime <= 0 && Template.EndTime <= 0) return true;

            var curTime = TimeSpan.FromHours(TimeManager.Instance.GetTime);
            var start = TimeSpan.FromHours(Template.StartTime);
            var end = TimeSpan.FromHours(Template.EndTime);

            return IsTimeBetween(curTime, start, end);
        }

        /// <summary>
        /// Checks if an NPC's despawn time is outside its spawner's time window.
        /// </summary>
        private static bool IsWithinDespawnTime(Npc npc)
        {
            var t = npc.Spawner?.Template;
            if (t == null || (t.StartTime <= 0 && t.EndTime <= 0)) return false;

            var cur = TimeSpan.FromHours(TimeManager.Instance.GetTime);
            var s = TimeSpan.FromHours(t.StartTime);
            var e = TimeSpan.FromHours(t.EndTime);

            return !IsTimeBetween(cur, s, e); // Возвращаем true, если ВНЕ времени спавна (т.е. пора деспавнить)
        }

        /// <summary>
        /// Checks if an NPC is within an active game schedule time window.
        /// </summary>
        private static bool IsNpcInTimeWindow(Npc npc)
        {
            var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)npc.Spawner.Template.Id);
            return status == GameScheduleManager.PeriodStatus.InProgress;
        }

        /// <summary>
        /// Helper to determine if a time is between a start and end time, handling day rollovers.
        /// </summary>
        private static bool IsTimeBetween(TimeSpan now, TimeSpan start, TimeSpan end) =>
            start <= end ? now >= start && now <= end : now >= start || now <= end;

        #endregion

        #region Optimal Spawner Selection

        /// <summary>
        /// Checks if this is the optimal spawner to use.
        /// </summary>
        private bool IsOptimalSpawner() => SpawnerId == SelectSpawnerId();

        /// <summary>
        /// Selects the most appropriate spawner ID based on conditions.
        /// </summary>
        private uint? SelectSpawnerId()
        {
            if (NpcSpawnerIds.Count == 1) return SpawnerId;

            if (Template.NpcSpawnerCategoryId == NpcSpawnerCategory.Autocreated && !HasScheduledSpawner())
                return SpawnerId;

            return IsThereSpawningSchedule() ? SpawnerId : null;
        }

        /// <summary>
        /// Checks if there is an active spawning schedule for this spawner.
        /// </summary>
        private bool IsThereSpawningSchedule()
        {
            var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)SpawnerId);
            if (status != GameScheduleManager.PeriodStatus.NotFound) return true;

            return HasSpawningTime();
        }

        /// <summary>
        /// Checks if the spawner template defines specific spawn times.
        /// </summary>
        private bool HasSpawningTime() => Template.StartTime > 0 || Template.EndTime > 0;

        /// <summary>
        /// Checks if any of the associated spawners have a schedule defined.
        /// </summary>
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

        /// <summary>
        /// Checks the game schedule status for a specific template.
        /// </summary>
        private static bool CheckGameScheduleStatus(NpcSpawnerTemplate t)
        {
            var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)t.Id);
            return status != GameScheduleManager.PeriodStatus.NotFound;
        }

        #endregion

        #region Count & Cache Helpers

        /// <summary>
        /// Checks if the spawn count conditions allow for new spawns.
        /// </summary>
        private bool CheckSpawnCountCanSpawn()
        {
            var min = Template.MinPopulation == 0 ? 1 : Template.MinPopulation;
            var max = Template.MaxPopulation;
            var pc = Math.Max(GetNumberOfPlayerInSpawnRadius(Template), 1);

            if (pc < min)
                max = (uint)pc;
            else if (pc >= min && pc <= max)
                max = (uint)pc;

            var total = CurrentSpawnCount + AreOtherNpcsInSpawnZone().Item2;

            if (Template.SuspendSpawnCount > 0 && total >= Template.SuspendSpawnCount)
                return false;

            return total < max;
        }

        /// <summary>
        /// Gets the number of players within the NPC spawn radius, using cache.
        /// </summary>
        private int GetNumberOfPlayerInSpawnRadius(NpcSpawnerTemplate template)
        {
            if (_playerCountCache.TryGetValue((int)SpawnerId, out var c) &&
                (DateTime.UtcNow - c.LastUpdate).TotalSeconds < _cacheLifetime.TotalSeconds)
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

        /// <summary>
        /// Checks if there are other NPCs in the spawn zone, using cache.
        /// </summary>
        private (bool, int) AreOtherNpcsInSpawnZone()
        {
            if (_npcsInZoneCache.TryGetValue((int)SpawnerId, out var c) &&
                (DateTime.UtcNow - c.LastUpdate).TotalSeconds < _cacheLifetime.TotalSeconds)
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

        /// <summary>
        /// Checks if any player is within the spawn radius, using cache.
        /// </summary>
        internal bool IsPlayerInSpawnRadius()
        {
            var radius = Template.TestRadiusPc == 0 ? Template.TestRadiusNpc : Template.TestRadiusPc;

            if (_playerInRadiusCache.TryGetValue((int)SpawnerId, out var c) &&
                (DateTime.UtcNow - c.LastUpdate).TotalSeconds < _cacheLifetime.TotalSeconds)
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

        /// <summary>
        /// Checks if any spawned NPCs from this spawner are dead (have corpses).
        /// </summary>
        private bool HasCorpse()
        {
            if (!SpawnedNpcs.TryGetValue(SpawnerId, out var npcs)) return false;
            return npcs.Any(npc => npc.IsDead);
        }

        /// <summary>
        /// Decrements the scheduled spawn count.
        /// </summary>
        private void DecrementCount(int count)
        {
            if (count <= 0) return; // Добавлена проверка на отрицательное значение

            lock (SpawnLock)
            {
                if (_scheduledCount > 0)
                    Interlocked.Add(ref _scheduledCount, -count);
            }
        }

        /// <summary>
        /// Increments the scheduled spawn count, typically for respawning.
        /// </summary>
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
        /// Initiates the spawning of NPCs based on the current template and spawn conditions, typically for events.
        /// </summary>
        public void DoEventSpawn()
        {
            if (Template == null || CurrentSpawnCount >= Template.MaxPopulation) return;

            var nsn = Template.Npcs.FirstOrDefault(n => n.MemberId == UnitId);
            if (nsn == null) return;

            var spawned = nsn.Spawn(this);
            foreach (var npc in spawned) AddNpcToSpawned(SpawnerId, npc);

            DecrementCount(spawned.Count);
        }

        /// <summary>
        /// Spawns a random NPC from the specified spawner template.
        /// </summary>
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
        public void DoSpawnEffect(uint spawnerId, SpawnEffect effect, BaseUnit caster, BaseUnit target)
        {
            var template = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
            if (template?.Npcs == null) return;

            var nsn = template.Npcs.FirstOrDefault(n => n.MemberId == UnitId);
            if (nsn == null) return;

            var npcs = nsn.Spawn(this);
            foreach (var npc in npcs)
            {
                // Применяем эффекты
                if (effect.UseSummonerFaction)
                    npc.Faction = target is Npc ? target.Faction : caster.Faction;

                if (effect.UseSummonerAggroTarget && !effect.UseSummonerFaction)
                {
                    var aggroTarget = target is Npc ? target : caster;
                    npc.Ai.Owner.AddUnitAggro(AggroKind.Damage, (Unit)aggroTarget, 1);
                    npc.Ai.OnAggroTargetChanged();
                }

                // Планируем автоматический деспавн, если задано время жизни
                if (effect.LifeTime > 0)
                    TaskManager.Instance.Schedule(new NpcSpawnerDoDespawnTask(npc),
                        TimeSpan.FromSeconds(effect.LifeTime));

                AddNpcToSpawned(SpawnerId, npc);
            }

            if (npcs.Count > 0) // Уменьшаем счетчик только если были заспавнены NPC
                DecrementCount(npcs.Count);
        }

        #endregion

        #region Position Helpers

        /// <summary>
        /// Adjusts the spawn position to avoid collisions with other NPCs.
        /// </summary>
        private static Vector3 AdjustSpawnPosition(Npc npc, int maxAttempts = 15)
        {
            var radius = GetCollisionRadiusForNpc(npc);
            var original = npc.Transform.CloneAsSpawnPosition().ToVector3();

            for (var i = 0; i < maxAttempts; i++)
            {
                var pos = Vector3.Add(original,
                    new Vector3(
                        (float)(Rand.NextDouble() * radius * 2 - radius),
                        (float)(Rand.NextDouble() * radius * 2 - radius),
                        0));

                bool hasCollision = false;
                // Проверяем коллизии с другими NPC в радиусе
                foreach (var otherNpc in WorldManager.GetAround<Npc>(npc, radius * 2))
                {
                    if (CheckCollision(pos, otherNpc.Transform.Local.Position, radius))
                    {
                        hasCollision = true;
                        break;
                    }
                }

                if (!hasCollision)
                {
                    return pos;
                }
            }

            return original; // Если не удалось найти свободное место, возвращаем оригинальную позицию
        }

        /// <summary>
        /// Adjusts the NPC's position to avoid collisions with other NPCs in the vicinity.
        /// </summary>
        public static Vector3 AdjustMovePosition(Npc npc, int maxAttempts = 5)
        {
            var collisionRadius = GetCollisionRadiusForNpc(npc);
            var originalPos = npc.Transform.CloneAsSpawnPosition();
            var currentPos = originalPos.ToVector3();

            for (var i = 0; i < maxAttempts; i++)
            {
                bool hasCollision = false;
                foreach (var otherNpc in WorldManager.GetAround<Npc>(npc, collisionRadius * 2))
                {
                    if (CheckCollision(currentPos, otherNpc.Transform.Local.Position, collisionRadius))
                    {
                        hasCollision = true;
                        break;
                    }
                }

                if (!hasCollision)
                {
                    return currentPos;
                }

                currentPos = Vector3.Add(originalPos.ToVector3(),
                    new Vector3(
                        (float)(Rand.NextDouble() * collisionRadius * 2 - collisionRadius),
                        (float)(Rand.NextDouble() * collisionRadius * 2 - collisionRadius),
                        0f));
            }

            return currentPos;
        }

        /// <summary>
        /// Simple 2D circle collision check.
        /// </summary>
        private static bool CheckCollision(Vector3 pos1, Vector3 pos2, float radius)
        {
            var dx = pos1.X - pos2.X;
            var dy = pos1.Y - pos2.Y;
            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>
        /// Gets the collision radius for an NPC, defaulting to 1.5 if not specified.
        /// </summary>
        private static float GetCollisionRadiusForNpc(Npc npc) => npc.ModelSize > 0 ? npc.ModelSize : 1.5f;

        #endregion

        #region Clone Helper

        /// <summary>
        /// Creates a shallow copy of an object.
        /// </summary>
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

        #region Events

        public event EventHandler<NpcSpawnedEventArgs> OnNpcSpawned;
        public event EventHandler<NpcDespawnedEventArgs> OnNpcDespawned;

        protected virtual void RaiseNpcSpawned(Npc npc)
        {
            OnNpcSpawned?.Invoke(this, new NpcSpawnedEventArgs(npc));
        }

        protected virtual void RaiseNpcDespawned(Npc npc)
        {
            OnNpcDespawned?.Invoke(this, new NpcDespawnedEventArgs(npc));
        }

        #endregion

        #region Event Args

        public class NpcSpawnedEventArgs : EventArgs
        {
            public Npc Npc { get; }
            public NpcSpawnedEventArgs(Npc npc) => Npc = npc;
        }

        public class NpcDespawnedEventArgs : EventArgs
        {
            public Npc Npc { get; }
            public NpcDespawnedEventArgs(Npc npc) => Npc = npc;
        }

        #endregion
    }
}
