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

namespace AAEmu.Game.Models.Game.NPChar;

public class NpcSpawner : Spawner<Npc>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private int _scheduledCount;
    private int _spawnCount;
    private bool IsSpawnScheduled;
    private bool IsDespawnScheduled;
    private bool RespawnDenied;
    private static readonly object _spawnLock = new(); // Lock for thread safety

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    [DefaultValue(1f)]
    public uint Count { get; set; } = 1;

    public List<uint> NpcSpawnerIds { get; set; } = [];
    public NpcSpawnerTemplate Template { get; set; }
    public List<NpcSpawnerNpc> SpawnableNpcs { get; set; } = []; // List of NPCs that can be spawned
    public ConcurrentDictionary<uint, List<Npc>> SpawnedNpcs { get; set; } = new(); // <SpawnerId, List of spawned NPCs>
    private DateTime _lastSpawnTime = DateTime.MinValue;
    private readonly Dictionary<int, SpawnerPlayerCountCache> _playerCountCache = new();
    private readonly Dictionary<int, SpawnerPlayerInRadiusCache> _playerInRadiusCache = new();

    public NpcSpawner()
    {
        IsSpawnScheduled = false;
        IsDespawnScheduled = false;
    }

    /// <summary>
    /// Initializes the list of SpawnableNpcs based on Template.Npcs.
    /// </summary>
    internal void InitializeSpawnableNpcs(NpcSpawnerTemplate template)
    {
        if (template?.Npcs == null)
        {
            Logger.Warn("Template or template.Npcs is null. SpawnableNpcs will not be initialized.");
            return;
        }

        SpawnableNpcs = [.. template.Npcs];
    }

    /// <summary>
    /// Updates the NPC spawner by checking whether it should despawn or spawn NPCs.
    /// Despawning takes priority. If neither condition is met, it logs that no action was taken.
    /// Includes thread safety via locking.
    /// </summary>
    /// <summary>
    /// Updates the NPC spawner by checking whether it should despawn or spawn NPCs.
    /// Spawning is allowed only if conditions are met and the spawn delay has elapsed.
    /// Despawning has higher priority. All actions are logged.
    /// </summary>
    public void Update()
    {
        try
        {
            lock (_spawnLock)
            {
                var didAction = false;

                if (CanDespawnNpcs())
                {
                    //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Despawning NPCs...");
                    DespawnNpcs();
                    didAction = true;
                }
                else if (!IsPlayerInSpawnRadius() && _spawnCount > 0)
                {
                    //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Despawning NPCs...");
                    DespawnNpcsNow();
                    didAction = true;
                }

                if (!didAction && CanSpawnNpcs())
                {
                    //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Spawning NPCs...");
                    DoSpawn();
                    didAction = true;
                }

                if (!didAction)
                {
                    //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] No spawn or despawn actions performed.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error occurred during NpcSpawner update. [SpawnerId={SpawnerId}, UnitId={UnitId}]");
        }
    }

    /// <summary>
    /// Determines whether the NPCs can be despawned based on schedule and current presence.
    /// </summary>
    private bool CanDespawnNpcs()
    {
        if (!IsDespawningScheduleEnabled(SpawnerId))
        {
            //Logger.Debug($"[Despawn] Schedule does not allow despawning for SpawnerId={SpawnerId}.");
            return false;
        }

        return true;
    }

    private void DespawnNpcs()
    {
        if (IsDespawnScheduled)
        {
            Logger.Debug($"[Despawn] группа уже в стадии удаления for SpawnerId={SpawnerId}.");
            return; // группа уже в стадии удаления
        }

        if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcs))
            DoDespawns(npcs);
    }

    private void DespawnNpcsNow()
    {
        if (IsDespawnScheduled)
        {
            Logger.Debug($"[Despawn] группа уже в стадии удаления for SpawnerId={SpawnerId}.");
            return; // группа уже в стадии удаления
        }

        if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcs))
            DoDespawnsNow(npcs);
    }

    /// <summary>
    /// Determines whether the NPCs can be spawned based on schedule and current presence.
    /// </summary>
    /// <returns></returns>
    private bool CanSpawnNpcs()
    {
        if (!CanSpawn())
            return false;

        if (IsSpawnDelayNotElapsed())
        {
            //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Spawn delayed — waiting for cooldown.");
            return false;
        }

        return true;
    }

    private bool IsSpawnDelayNotElapsed()
    {
        if (_lastSpawnTime == DateTime.MinValue)
            return false;

        var elapsedSeconds = (DateTime.UtcNow - _lastSpawnTime).TotalSeconds;
        return elapsedSeconds < Template.SpawnDelayMin;
    }

    /// <summary>
    /// Checks if the NPC can be spawned based on various world, player, and schedule conditions.
    /// </summary>
    private bool CanSpawn()
    {
        if (Template == null)
        {
            Logger.Warn($"[Spawn [SpawnerId={SpawnerId}, UnitId={UnitId}] Template is null. Cannot determine if NPC can be spawned.");
            return false;
        }

        if (HasCorpse())
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC — corpse still present.");
            return false;
        }

        if (IsDespawnScheduled)
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] Despawn is scheduled. Spawning is blocked.");
            return false;
        }

        if (!Template.ActivationState)
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] Template activation state is false.");
            return false;
        }

        if (!IsPlayerInSpawnRadius())
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] No players in spawn radius.");
            return false;
        }

        if (!IsOptimalSpawner())
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] This is not the optimal spawner.");
            return false;
        }

        if (!IsSpawningScheduleEnabled())
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] Spawning schedule is not enabled.");
            return false;
        }

        if (!CheckSpawnCountCanSpawn())
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] Spawn count conditions not met.");
            return false;
        }

        //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] All spawn conditions met. NPC can be spawned.");
        return true;
    }

    /// <summary>
    /// Returns true if this is the optimal spawner
    /// </summary>
    private bool IsOptimalSpawner()
    {
        var optimalId = SelectSpawnerId();
        var result = optimalId != 0 && SpawnerId == optimalId;
        if (!result)
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] Not optimal (best is {optimalId})");
        }
        return result;
    }

    /// <summary>
    /// Selects the appropriate SpawnerId for an NPC based on the following conditions:
    /// 1. If the NPC has a schedule, selects a spawner with a suitable time window.
    /// 2. If the NPC has no schedule, selects an AutoCreated spawner with a single NPC.
    /// 3. If there is only one spawner for the NPC, selects that spawner.
    /// </summary>
    /// <returns>The selected SpawnerId, or null if no suitable spawner is found.</returns>
    private uint? SelectSpawnerId()
    {
        bool scheduled = false;

        // Condition 1: Check for a spawner with a suitable schedule
        // Condition 2: Check for an AutoCreated spawner without a scheduled NPC
        // Condition 3: If there is only one spawner, select it
        if (IsThereSpawningSchedule((int)SpawnerId))
        {
            //Logger.Info($"Selected SpawnerId={spawnerId} based on schedule.");
            return SpawnerId;
        }

        // Condition 2: Check for an AutoCreated spawner without a scheduled NPC
        if (Template.NpcSpawnerCategoryId == NpcSpawnerCategory.Autocreated && !HasScheduledSpawner())
        {
            //Logger.Info($"Selected AutoCreated SpawnerId={spawnerId} without a scheduled NPC.");
            return SpawnerId;
        }
        //}

        // Condition 3: If there is only one spawner, select it
        if (NpcSpawnerIds.Count == 1)
        {
            //Logger.Info($"Selected the only available SpawnerId={NpcSpawnerIds[0]}.");
            return NpcSpawnerIds[0];
        }

        //Logger.Warn("No suitable SpawnerId found for this NPC.");
        return null;
    }

    private bool IsThereSpawningSchedule(int spawnerId)
    {
        var scheduleStatus = GameScheduleManager.Instance.GetPeriodStatusNpc((int)SpawnerId);
        switch (scheduleStatus)
        {
            case GameScheduleManager.PeriodStatus.NotFound:
                //Logger.Debug($"[Spawn] No schedule found for NPC {npcId}. Falling back to time window.");
                break; // Переход к проверке времени

            case GameScheduleManager.PeriodStatus.InProgress:
            case GameScheduleManager.PeriodStatus.NotStarted:
            case GameScheduleManager.PeriodStatus.Ended:
                //Logger.Debug($"[Spawn] Расписание у NPC {npcId} имеется.");
                return true;

            default:
                Logger.Warn($"[Spawn] Unknown schedule status '{scheduleStatus}' for NPC {spawnerId}.");
                return false;
        }

        // Если расписания нет — проверим, задано ли время появления
        if (HasSpawningTime())
        {
            //Logger.Debug($"[Spawn] NPC {npcId} is within spawn time window — spawning enabled.");
            return true;
        }

        //Logger.Debug($"[Spawn] NPC {npcId} not in spawn time window.");
        return false;
    }

    private bool HasSpawningTime()
    {
        if (Template.StartTime > 0.0f || Template.EndTime > 0.0f)
        {
            //Logger.Debug($"[TimeCheck] NPC {Template.Id} checking time window: now={currentTime}, start={startTime}, end={endTime}, inside={result}");
            return true;
        }

        //Logger.Debug($"[TimeCheck] NPC {SpawnerId} has no time window defined.");
        return false;
    }

    /// <summary>
    /// Determines whether this spawner is part of a valid scheduled group with defined start and end times.
    /// </summary>
    private bool HasScheduledSpawner()
    {
        if (NpcSpawnerIds == null || NpcSpawnerIds.Count == 0)
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] No NpcSpawnerIds defined.");
            return true;
        }
        if (NpcSpawnerIds == null || NpcSpawnerIds.Count == 1)
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] Имеется только один спавнер.");
            return false;
        }

        var result = false;
        foreach (var spawnerId in NpcSpawnerIds)
        {
            if (spawnerId == 0)
                continue;

            var spawnerTemplate = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
            if (spawnerTemplate == null)
                continue;

            if (SpawnerId != spawnerId)
            {
                if (spawnerTemplate is { StartTime: > 0.0f, EndTime: > 0.0f } || CheckGameScheduleStatus())
                {
                    //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] имеет другой спавнер SpawnerId={spawnerId} с расписанием спавна.");
                    result = true;
                }
            }
            if (SpawnerId == spawnerId)
            {
                if (spawnerTemplate is { StartTime: > 0.0f, EndTime: > 0.0f } || CheckGameScheduleStatus())
                {
                    //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] имеет расписание спавна.");
                    return true;
                }
            }
        }

        return result;
    }

    private bool IsScheduledSpawner()
    {
        if (NpcSpawnerIds == null || NpcSpawnerIds.Count == 0)
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] No NpcSpawnerIds defined.");
            return true;
        }
        if (NpcSpawnerIds == null || NpcSpawnerIds.Count == 1)
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] Имеется только один спавнер.");
            return false;
        }

        foreach (var spawnerId in NpcSpawnerIds)
        {
            if (spawnerId == 0)
                continue;

            var spawnerTemplate = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
            if (spawnerTemplate == null)
                continue;

            if (SpawnerId == spawnerId)
            {
                if (spawnerTemplate is { StartTime: > 0.0f, EndTime: > 0.0f } || CheckGameScheduleStatus())
                {
                    //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] имеет расписание спавна.");
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if this NPC is allowed to spawn according to the current game schedule.
    /// Updates IsSpawnScheduled flag and returns true if spawning is allowed.
    /// </summary>
    private bool CheckGameScheduleStatus()
    {
        var npcId = (int)Template.Id;
        var status = GameScheduleManager.Instance.GetPeriodStatusNpc(npcId);

        switch (status)
        {
            case GameScheduleManager.PeriodStatus.NotStarted:
            case GameScheduleManager.PeriodStatus.Ended:
            case GameScheduleManager.PeriodStatus.InProgress:
                //Logger.Debug($"[Schedule] NPC TemplateId={npcId} имеет расписание спавна.");
                return true;

            case GameScheduleManager.PeriodStatus.NotFound:
            default:
                //Logger.Debug($"[Schedule] Unknown schedule status '{status}' for NPC TemplateId={npcId}. Не имеет расписание спавна.");
                return false;
        }
    }

    private bool CheckSpawnCountCanSpawn()
    {
        var minPopulation = Template.MinPopulation;
        var maxPopulation = Template.MaxPopulation;
        if (minPopulation == 0)
            minPopulation = 1;

        var playerCount = GetNumberOfPlayerInSpawnRadius(Template);

        if (playerCount == 0)
            playerCount = 1;

        if (playerCount < minPopulation)
            maxPopulation = (uint)playerCount;

        if (playerCount >= minPopulation && playerCount <= maxPopulation)
        {
            maxPopulation = (uint)playerCount;
        }

        //if (playerCount > maxPopulation)
        //    maxPopulation = maxPopulation;

        // Checks if SuspendSpawnCount is exceeded
        if (Template.SuspendSpawnCount > 0 && _spawnCount + AreOtherNpcsInSpawnZone().Item2 >= Template.SuspendSpawnCount)
        {
            //Logger.Debug($"Spawn count ({_spawnCount}:{AreOtherNpcsInSpawnZone().Item2}) for SpawnerId: {UnitId}:{SpawnerId} has reached the suspend limit ({Template.SuspendSpawnCount}). Spawning is blocked.");
            return false;
        }

        // Checks if the maximum number of NPCs has been reached
        if (_spawnCount + AreOtherNpcsInSpawnZone().Item2 >= maxPopulation)
        {
            //Logger.Debug($"Spawn count ({_spawnCount}:{AreOtherNpcsInSpawnZone().Item2}) for SpawnerId: {UnitId}:{SpawnerId} has reached the maximum population limit ({Template.MaxPopulation}). Spawning is blocked.");
            return false;
        }

        //// Checks if the minimum number of NPCs has been reached
        //if (_spawnCount + AreOtherNpcsInSpawnZone().Item2 >= Template.MinPopulation)
        //{
        //    //Logger.Debug($"Spawn count ({_spawnCount}:{AreOtherNpcsInSpawnZone().Item2}) for SpawnerId: {UnitId}:{SpawnerId} exceeds the minimum population limit ({Template.MinPopulation}). Spawning is blocked.");
        //    return false;
        //}

        return true;
    }

    private bool HasCorpse()
    {
        if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcs))
        {
            if (IsCorpse(npcs))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsSpawnCountExceeded()
    {
        if (Template.SuspendSpawnCount > 0 && _spawnCount >= Template.SuspendSpawnCount)
            return true;

        if (_spawnCount >= Template.MaxPopulation)
            return true;

        return false;
    }

    /// <summary>
    /// Returns true if any of the given NPCs are corpses (dead and persistent).
    /// </summary>
    private bool IsCorpse(List<Npc> npcs)
    {
        if (npcs == null || npcs.Count == 0)
            return false;

        foreach (var npc in npcs)
        {
            if (npc.IsDead) // или другой критерий мертвого NPC
            {
                //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] Found corpse NPC: ObjId={npc.ObjId}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Chooses an NPC to spawn based on SpawnableNpcs.
    /// </summary>
    private Npc ChooseNpcToSpawn()
    {
        if (SpawnableNpcs == null || SpawnableNpcs.Count == 0)
        {
            Logger.Warn("No spawnable NPCs available.");
            return null;
        }

        try
        {
            var totalWeight = SpawnableNpcs.Sum(n => n.Weight);
            var randomValue = Next(0, (int)totalWeight);

            foreach (var npcTemplate in SpawnableNpcs)
            {
                if (randomValue < npcTemplate.Weight)
                {
                    var npc = CreateNpcFromTemplate(npcTemplate);
                    if (npc != null)
                    {
                        return npc;
                    }
                    Logger.Error($"Failed to create NPC from template {npcTemplate.MemberId}.");
                }
                randomValue -= (int)npcTemplate.Weight;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while choosing NPC to spawn.");
        }

        Logger.Warn("No NPC was chosen to spawn.");
        return null;
    }

    /// <summary>
    /// Creates an NPC from the given template.
    /// </summary>
    private static Npc CreateNpcFromTemplate(NpcSpawnerNpc npcTemplate)
    {
        try
        {
            return NpcManager.Instance.Create(0, npcTemplate.MemberId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to create NPC from template {npcTemplate.MemberId}.");
            return null;
        }
    }

    /// <summary>
    /// Checks if an NPC is within the spawn radius.
    /// </summary>
    private bool IsNpcInSpawnRadius(Npc npc)
    {
        if (npc == null)
            return false;

        if (Template.TestRadiusNpc == 0)
            return true;

        var distance = MathUtil.CalculateDistance(npc.Transform.World.Position, new Vector3(Position.X, Position.Y, Position.Z));
        return distance <= Template.TestRadiusNpc * 3;
    }

    /// <summary>
    /// Checks if a player is within the spawn radius.
    /// </summary>
    private bool IsPlayerInSpawnRadius()
    {
        var testRadiusPc = Template.TestRadiusPc == 0 ? Template.TestRadiusNpc : Template.TestRadiusPc;

        // Проверяем, есть ли кэш для текущего SpawnerId
        if (_playerInRadiusCache.TryGetValue((int)SpawnerId, out var cache))
        {
            // Если с момента последнего обновления прошло меньше 10 секунд, возвращаем кэшированное значение
            if ((DateTime.UtcNow - cache.LastUpdate).TotalSeconds < 10)
            {
                return cache.IsPlayerInRadius;
            }
        }

        // Если кэш устарел или отсутствует, выполняем проверку
        var players = WorldManager.Instance.GetAllCharacters();
        foreach (var player in players)
        {
            var distance = MathUtil.CalculateDistance(player.Transform.World.Position, new Vector3(Position.X, Position.Y, Position.Z));
            if (distance <= testRadiusPc * 50f)
            {
                // Обновляем кэш
                _playerInRadiusCache[(int)SpawnerId] = new SpawnerPlayerInRadiusCache
                {
                    IsPlayerInRadius = true,
                    LastUpdate = DateTime.UtcNow
                };
                return true;
            }
        }

        // Обновляем кэш (игроков в радиусе нет)
        _playerInRadiusCache[(int)SpawnerId] = new SpawnerPlayerInRadiusCache
        {
            IsPlayerInRadius = false,
            LastUpdate = DateTime.UtcNow
        };
        return false;
    }

    // Структура для хранения кэшированных данных
    private struct SpawnerPlayerInRadiusCache
    {
        public bool IsPlayerInRadius { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Returns the number of players within the spawn radius.
    /// </summary>
    /// <param name="template">The spawner template containing the check radius.</param>
    /// <returns>The number of players within the radius.</returns>
    private int GetNumberOfPlayerInSpawnRadius(NpcSpawnerTemplate template)
    {
        // Проверяем, есть ли уже кэш для этого SpawnerId
        if (_playerCountCache.TryGetValue((int)SpawnerId, out var cache))
        {
            // Если прошло меньше 10 секунд с момента последнего обновления, возвращаем кэшированное значение
            if ((DateTime.UtcNow - cache.LastUpdate).TotalSeconds < 10)
            {
                return cache.PlayerCount;
            }
        }

        // Проверяем, что шаблон и радиус валидны
        if (template == null || template.TestRadiusNpc <= 0)
            return 0;

        var playerCount = 0;

        // Получаем позицию спавна (например, позицию первого NPC или центральную точку)
        if (SpawnedNpcs is { Count: > 0 })
        {
            var npcs = SpawnedNpcs.Values.FirstOrDefault();
            if (npcs?.Count > 0)
            {
                // Получаем количество игроков в радиусе
                var tmpPlayerCount = WorldManager.GetAround<Character>(npcs[0], template.TestRadiusNpc * 50).Count;
                if (playerCount < tmpPlayerCount)
                    playerCount = tmpPlayerCount;
            }
        }

        // Обновляем кэш для текущего SpawnerId
        _playerCountCache[(int)SpawnerId] = new SpawnerPlayerCountCache
        {
            PlayerCount = playerCount,
            LastUpdate = DateTime.UtcNow
        };

        return playerCount;
    }

    // Структура для хранения кэшированных данных
    private struct SpawnerPlayerCountCache
    {
        public int PlayerCount { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Picks the optimal spawner from the list, based on player count and spawner limits.
    /// </summary>
    private uint? GetOptimalSpawnerForPlayers()
    {
        if (NpcSpawnerIds == null || NpcSpawnerIds.Count == 0)
            return null;

        if (IsScheduledSpawner())
        {
            //Logger.Debug($"[Spawn SpawnerId={SpawnerId}, UnitId={UnitId}] Этот спавнер имеет другие спавнеры с расписанием.");
            return SpawnerId;
        }

        var playerCount = GetNumberOfPlayerInSpawnRadius(Template);
        if (playerCount == 0)
        {
            //var allPlayers = WorldManager.Instance.GetAllCharacters();
            //if (allPlayers.Count == 0)
            //    return null;
            playerCount = 1;
        }

        uint? bestSpawnerId = null;
        var bestDeviation = int.MaxValue;

        foreach (var spawnerId in NpcSpawnerIds)
        {
            var spawnerTemplate = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
            if (spawnerTemplate == null)
                continue;

            var isValid =
                //spawnerTemplate.NpcSpawnerCategoryId != NpcSpawnerCategory.Autocreated &&
                playerCount >= spawnerTemplate.MinPopulation &&
                playerCount <= spawnerTemplate.MaxPopulation;

            if (!isValid)
                continue;

            var deviation = Math.Min(
                Math.Abs(playerCount - (int)spawnerTemplate.MinPopulation),
                Math.Abs(playerCount - (int)spawnerTemplate.MaxPopulation)
            );

            if (deviation < bestDeviation)
            {
                bestDeviation = deviation;
                bestSpawnerId = spawnerId;
            }
        }

        //Logger.Debug($"[Spawn SpawnerId={SpawnerId}] Optimal spawner for {playerCount} players: {bestSpawnerId ?? SpawnerId}");
        return bestSpawnerId ?? SpawnerId;
    }

    // Структура для хранения кэшированных данных
    private struct SpawnerNpcsInZoneCache
    {
        public int Count { get; set; }
        public bool AreNpcsInZone { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    // Словарь для хранения кэша
    private readonly Dictionary<int, SpawnerNpcsInZoneCache> _npcsInZoneCache = new();

    /// <summary>
    /// Checks if there are NPCs in other spawners.
    /// </summary>
    /// <returns>
    /// <c>true</c> if there are NPCs in other spawners; 
    /// <c>false</c> if other spawners are empty.
    /// </returns>
    private (bool, int) AreOtherNpcsInSpawnZone()
    {
        var count = 0;

        // Проверяем, есть ли уже кэш для этого SpawnerId
        if (_npcsInZoneCache.TryGetValue((int)SpawnerId, out var cache))
        {
            // Если прошло меньше 60 секунд с момента последнего обновления, возвращаем кэшированное значение
            if ((DateTime.UtcNow - cache.LastUpdate).TotalSeconds < 10)
            {
                //Logger.Debug($"Using cached value for SpawnerId: {UnitId}:{SpawnerId}. AreOtherNpcsInSpawnZone: {cache.AreNpcsInZone}");
                return (cache.AreNpcsInZone, cache.Count);
            }
        }

        var areOtherNpcsInZone = false;

        // Итерируем по всем спавнерам
        foreach (var spawnerId in SpawnedNpcs.Keys)
        {
            // Исключаем текущий спавнер
            if (spawnerId == SpawnerId)
                continue;

            // Проверяем, есть ли NPC в этом спавнере
            if (SpawnedNpcs.TryGetValue(spawnerId, out var npcs) && npcs?.Count > 0)
            {
                //Logger.Debug($"spawn count={_spawnCount + _scheduledCount} for SpawnerId: {UnitId}:{SpawnerId}");
                count += _spawnCount + _scheduledCount;
                areOtherNpcsInZone = npcs.Count > 0; // В другом спавнере есть NPC
            }
        }

        // Обновляем кэш для текущего SpawnerId
        _npcsInZoneCache[(int)SpawnerId] = new SpawnerNpcsInZoneCache
        {
            Count = count,
            AreNpcsInZone = areOtherNpcsInZone,
            LastUpdate = DateTime.UtcNow
        };

        //Logger.Debug($"Updated cache for SpawnerId: {UnitId}:{SpawnerId}. AreOtherNpcsInSpawnZone: {areOtherNpcsInZone}");
        return (areOtherNpcsInZone, count);
    }

    /// <summary>
    /// Spawns all NPCs associated with this spawner.
    /// </summary>
    public void SpawnAll(bool beginning = false)
    {
        if (IsSpawningScheduleEnabled())
            return;

        DoSpawn();

        if (IsSpawnScheduled)
            IsDespawningScheduleEnabled(SpawnerId);

        return;
    }

    /// <summary>
    /// Spawns a single NPC with the specified object ID.
    /// </summary>
    public override Npc Spawn(uint objId)
    {
        //if (IsSpawningScheduleEnabled())
        //    return null;

        DoSpawn();

        //if (IsSpawnScheduled)
        //    IsDespawningScheduleEnabled(SpawnerId);

        return SpawnedNpcs[SpawnerId][0];
    }

    /// <summary>
    /// Force spawns a single NPC with the specified object ID.
    /// </summary>
    public override Npc ForceSpawn(uint objId)
    {
        if (SpawnedNpcs.Count == 0)
        {
            InitializeSpawnableNpcs(Template);
        }

        DoSpawn();

        if (IsSpawnScheduled)
            IsDespawningScheduleEnabled(SpawnerId);

        return SpawnedNpcs[SpawnerId][0];
    }

    /// <summary>
    /// Despawns the specified NPC.
    /// </summary>
    public override void Despawn(Npc npc)
    {
        if (npc == null)
        {
            Logger.Warn("Attempted to despawn a null NPC.");
            return;
        }

        try
        {
            lock (_spawnLock)
            {

                RemoveNpcFromSpawnedList(npc);
                UnregisterAndDeleteNpc(npc);

                npc.IsDespawnScheduled = false;
                IsDespawnScheduled = false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to despawn NPC {npc.TemplateId}.");
        }
    }

    private static void UnregisterAndDeleteNpc(Npc npc)
    {
        npc.UnregisterNpcEvents();
        npc.Delete();
    }

    private void RemoveNpcFromSpawnedList(Npc npc)
    {
        if (npc.Spawner == null)
        {
            Logger.Warn($"NPC {npc.TemplateId} has no associated Spawner.");
            return;
        }

        var id = npc.Spawner.SpawnerId;
        lock (_spawnLock)
        {
            if (SpawnedNpcs.TryGetValue(id, out var npcList))
            {
                lock (npcList)
                {
                    var removed = npcList.Remove(npc);
                    if (!removed)
                    {
                        Logger.Warn($"NPC {npc.TemplateId} not found in SpawnedNpcs for SpawnerId={id}.");
                    }

                    if (npcList.Count == 0)
                    {
                        var removedEntry = SpawnedNpcs.TryRemove(id, out _);
                        if (!removedEntry)
                        {
                            Logger.Warn($"Failed to remove empty SpawnerId={id} from SpawnedNpcs.");
                        }
                    }
                }
            }
            else
            {
                Logger.Warn($"SpawnerId={id} not found in SpawnedNpcs.");
            }
        }
    }

    private void RemoveNpc(uint spawnerId, Npc npc)
    {
        if (SpawnedNpcs.TryGetValue(spawnerId, out var npcList))
        {
            lock (_spawnLock)
            {
                lock (npcList)
                {
                    IsDespawnScheduled = false;
                    npc.IsDespawnScheduled = false;

                    npcList.Remove(npc);
                    //Logger.Debug($"Removed NPC {npc.ObjId} from list for SpawnerId={spawnerId}.");

                    // If the NPC list is empty, removes the entry from the dictionary
                    if (npcList.Count == 0)
                    {
                        var removedEntry = SpawnedNpcs.TryRemove(spawnerId, out _);
                        if (removedEntry)
                        {
                            //Logger.Debug($"Removed empty list for SpawnerId={spawnerId} from SpawnedNpcs.");
                        }
                        else
                        {
                            Logger.Warn($"Failed to remove empty SpawnerId={spawnerId} from SpawnedNpcs.");
                        }
                    }
                }
            }
        }
        else
        {
            Logger.Warn($"SpawnerId={spawnerId} not found in SpawnedNpcs.");
        }
    }

    /// <summary>
    /// Clears the last spawn count.
    /// </summary>
    public void ClearLastSpawnCount()
    {
        Interlocked.Exchange(ref _spawnCount, 0);
    }

    /// <summary>
    /// Decreases the spawn count and handles respawn logic for the specified NPC.
    /// </summary>
    private void DoDespawn(Npc npc)
    {
        try
        {
            lock (_spawnLock)
            {
                if (_spawnCount <= 0)
                {
                    return;
                }

                // RespawnDenied - запрещает респавн для Npc у которых есть расписание
                // Schedules respawn if necessary
                if (!RespawnDenied && RespawnTime > 0 && AreOtherNpcsInSpawnZone().Item2 + _scheduledCount < Template.MaxPopulation) // Count
                {
                    // Decreases the spawn count
                    DecrementCount(true);
                    //Logger.Info($"Decreased spawn count for NPC {UnitId}:{SpawnerId}:{npc.ObjId}. New count: {_spawnCount}, scheduled count: {_scheduledCount}.");

                    npc.Respawn = DateTime.UtcNow.AddSeconds(RespawnTime);
                    SpawnManager.Instance.AddRespawn(npc);
                    //Logger.Info($"Scheduled respawn for NPC {UnitId}:{SpawnerId}:{npc.ObjId} in {RespawnTime} seconds.");
                }
                else
                {
                    // Decreases the spawn count
                    DecrementCount(false);
                    //Logger.Info($"Decreased spawn count for NPC {UnitId}:{SpawnerId}:{npc.ObjId}. New count: {_spawnCount}, scheduled count: {_scheduledCount}.");
                }

                // Sets the despawn time
                npc.Despawn = DateTime.UtcNow.AddSeconds(DespawnTime);

                // Extends the despawn time if there are items in the container
                if (npc.LootingContainer != null && npc.LootingContainer.Items.Count > 0)
                {
                    npc.Despawn += TimeSpan.FromSeconds(LootingContainer.LootDespawnExtensionTime);
                    //Logger.Info($"Extended despawn time in {LootingContainer.LootDespawnExtensionTime} seconds for NPC {UnitId}:{SpawnerId}:{npc.ObjId} due to items in looting container.");
                }

                // Adds the NPC to the despawn list
                SpawnManager.Instance.AddDespawn(npc);
                //Logger.Info($"Added NPC {UnitId}:{SpawnerId}:{npc.ObjId} to despawn list. spawnCount={_spawnCount}, scheduledCount={_scheduledCount}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to decrease count for NPC {UnitId}:{SpawnerId}:{npc.ObjId}.");
        }
    }

    private void DoDespawnNow(Npc npc)
    {
        try
        {
            lock (_spawnLock)
            {
                if (_spawnCount <= 0)
                    return;

                // Decreases the spawn count
                DecrementCount(true);
                //Logger.Info($"Decreased spawn count for NPC {UnitId}:{SpawnerId}:{npc.ObjId}. New count: {_spawnCount}, scheduled count: {_scheduledCount}.");

                // Adds the NPC to the despawn list
                SpawnManager.Instance.AddDespawn(npc);
                //Logger.Info($"Added NPC {UnitId}:{SpawnerId}:{npc.ObjId} to despawn list. spawnCount={_spawnCount}, scheduledCount={_scheduledCount}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to decrease count for NPC {UnitId}:{SpawnerId}:{npc.ObjId}.");
        }
    }

    /// <summary>
    /// Despawns the specified NPC and schedules respawn if necessary.
    /// </summary>
    public void DespawnWithRespawn(Npc npc)
    {
        if (npc == null) return;

        npc.Delete();
        // Decreases the spawn count
        var newSpawnCount = Interlocked.Decrement(ref _spawnCount);
        if (_spawnCount < 0)
        {
            Interlocked.Exchange(ref _spawnCount, 0);
            newSpawnCount = 0;
        }

        // Schedules respawn if necessary
        if (RespawnTime > 0 && AreOtherNpcsInSpawnZone().Item2 < Template.MaxPopulation) // Count
        {
            npc.Respawn = DateTime.UtcNow.AddSeconds(RespawnTime);
            SpawnManager.Instance.AddRespawn(npc);
            var newScheduledCount = Interlocked.Increment(ref _scheduledCount);
            if (_scheduledCount < 0)
            {
                Interlocked.Exchange(ref _scheduledCount, 0);
                newScheduledCount = 0;
            }
            Logger.Info($"Scheduled respawn for NPC {UnitId}:{SpawnerId}:{npc.ObjId} in {RespawnTime} seconds. New scheduled count: {newScheduledCount}.");
        }
    }

    /// <summary>
    /// Despawns all NPCs, excluding those in combat.
    /// </summary>
    /// <param name="npcs">The list of NPCs to despawn.</param>
    public void DoDespawns(List<Npc> npcs)
    {
        if (npcs == null)
        {
            Logger.Warn("Attempted to despawn a null list of NPCs.");
            return;
        }

        lock (_spawnLock)
        {
            // Установка флага деспауна
            IsDespawnScheduled = true;

            // Creates a copy of the list for safe iteration
            var npcsToDespawn = npcs.ToList();

            foreach (var npc in npcsToDespawn)
            {
                try
                {
                    if (npc == null)
                    {
                        Logger.Warn("Attempted to despawn a null NPC.");
                        continue;
                    }

                    // будем деспавнить Npc в любом случае
                    // we'll despawn the Npc anyway
                    // Despawns the NPC if it is not in combat
                    //if (!npc.IsInBattle)
                    //{
                    DoDespawn(npc);
                    //Logger.Debug($"Despawned NPC {npc.ObjId}.");
                    //}
                    //else
                    //{
                    //    Logger.Debug($"Skipped despawn for NPC {npc.ObjId} because it is in battle.");
                    //}
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to despawn NPC {UnitId}:{SpawnerId}:{npc?.ObjId}.");
                }
            }
            // Сброс флага после завершения деспауна
            IsDespawnScheduled = false;
        }
    }

    public void DoDespawnsNow(List<Npc> npcs)
    {
        if (npcs == null)
        {
            Logger.Warn("Attempted to despawn a null list of NPCs.");
            return;
        }

        lock (_spawnLock)
        {
            // Установка флага деспауна
            IsDespawnScheduled = true;

            // Creates a copy of the list for safe iteration
            var npcsToDespawn = npcs.ToList();

            foreach (var npc in npcsToDespawn)
            {
                try
                {
                    if (npc == null)
                    {
                        Logger.Warn("Attempted to despawn a null NPC.");
                        continue;
                    }
                    DoDespawnNow(npc);
                    //Logger.Debug($"Despawned NPC {npc.ObjId}.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to despawn NPC {UnitId}:{SpawnerId}:{npc?.ObjId}.");
                }
            }
            // Сброс флага после завершения деспауна
            IsDespawnScheduled = false;
        }
    }

    /// <summary>
    /// Spawns NPCs.
    /// </summary>
    public void DoSpawn()
    {
        // Checks if there are NPCs to spawn
        if (SpawnableNpcs == null || SpawnableNpcs.Count == 0)
        {
            Logger.Warn("No spawnable NPCs available.");
            return;
        }

        // List to store spawned NPCs
        var spawnedNpcs = new List<Npc>();

        // Iterates through all NPC templates
        foreach (var npcTemplate in SpawnableNpcs)
        {
            try
            {
                if (npcTemplate == null)
                {
                    Logger.Warn("NPC template is null.");
                    continue;
                }

                //if (_spawnCount + _scheduledCount >= Template.MaxPopulation)
                //{
                //    Logger.Debug($"Spawn count ({_spawnCount}:{AreOtherNpcsInSpawnZone().Item2}) for SpawnerId: {UnitId}:{SpawnerId} has reached the maximum population limit ({Template.MaxPopulation}). Spawning is blocked.");
                //    return;
                //}

                //if (Template.SuspendSpawnCount > 0 && _spawnCount + _scheduledCount > Template.SuspendSpawnCount)
                //{
                //    Logger.Debug($"Spawn count ({_spawnCount}:{AreOtherNpcsInSpawnZone().Item2}) for SpawnerId: {UnitId}:{SpawnerId} has reached the suspend limit ({Template.SuspendSpawnCount}). Spawning is blocked.");
                //    return;
                //}

                lock (_spawnLock) // Synchronizes access to the list
                {
                    // Spawns the NPC
                    var spawned = npcTemplate.Spawn(this);
                    if (spawned == null || spawned.Count == 0)
                    {
                        Logger.Warn($"No NPCs spawned from template {npcTemplate.SpawnerId}:{npcTemplate.MemberId}");
                        continue;
                    }

                    // Adds the spawned NPCs to the list
                    spawnedNpcs.AddRange(spawned);
                    foreach (var npc in spawned)
                    {
                        // Adjust position to prevent overlapping
                        var newPos = AdjustSpawnPosition(npc);
                        npc.Transform.Local.Position = newPos;

                        AddNpcToSpawned(npc.Spawner.SpawnerId, npc);
                    }

                    // Increases the count of spawned NPCs
                    IncrementCount(spawnedNpcs);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to spawn NPC from template {npcTemplate?.SpawnerId}:{npcTemplate?.MemberId}");
            }
        }

        // Checks if any NPCs were spawned
        if (spawnedNpcs.Count == 0)
        {
            Logger.Error($"Can't spawn NPC {UnitId}:{SpawnerId}");
            return;
        }

        Logger.Info($"Mobs were spawned from SpawnerId={UnitId}:{SpawnerId} in the amount of {spawnedNpcs.Count}");
    }

    /// <summary>
    /// Schedules NPC spawning based on schedule status and optional time window.
    /// </summary>
    public bool IsSpawningScheduleEnabled()
    {
        if (Template == null)
        {
            Logger.Warn($"[Spawn] Can't spawn NPC {SpawnerId}:{UnitId} (index={Id}) — template is null.");
            return false;
        }

        IsSpawnScheduled = false;

        var scheduleStatus = GameScheduleManager.Instance.GetPeriodStatusNpc((int)SpawnerId);
        switch (scheduleStatus)
        {
            case GameScheduleManager.PeriodStatus.InProgress:
                //Logger.Debug($"[Spawn] NPC {npcId} has active schedule — spawning enabled.");
                RespawnDenied = true;
                IsSpawnScheduled = true;
                return true;

            case GameScheduleManager.PeriodStatus.NotFound:
                //Logger.Debug($"[Spawn] No schedule found for NPC {npcId}. Falling back to time window.");
                break; // Переход к проверке времени

            case GameScheduleManager.PeriodStatus.NotStarted:
                //Logger.Debug($"[Spawn] Schedule not started for NPC {npcId}.");
                return false;

            case GameScheduleManager.PeriodStatus.Ended:
                //Logger.Debug($"[Spawn] Schedule ended for NPC {npcId}.");
                return false;

            default:
                Logger.Debug($"[Spawn] Unknown schedule status '{scheduleStatus}' for NPC {SpawnerId}.");
                return false;
        }

        // Если расписания нет — проверим, задано ли время появления
        if (IsWithinSpawnTime())
        {
            //Logger.Debug($"[Spawn] NPC {npcId} is within spawn time window — spawning enabled.");
            RespawnDenied = true;
            IsSpawnScheduled = true;
            return true;
        }

        //Logger.Debug($"[Spawn] NPC {npcId} not in spawn time window.");
        return false;
    }

    /// <summary>
    /// Checks if the current time is between NPC spawn start and end time.
    /// </summary>
    private bool IsWithinSpawnTime()
    {
        if (Template.StartTime > 0.0f || Template.EndTime > 0.0f)
        {
            var curTime = TimeManager.Instance.GetTime;
            var startTime = TimeSpan.FromHours(Template.StartTime);
            var endTime = TimeSpan.FromHours(Template.EndTime);
            var currentTime = TimeSpan.FromHours(curTime);

            var result = IsTimeBetween(currentTime, startTime, endTime);
            //Logger.Debug($"[TimeCheck] NPC {Template.Id} checking time window: now={currentTime}, start={startTime}, end={endTime}, inside={result}");
            return result;
        }

        //Logger.Debug($"[TimeCheck] NPC {Template.Id} has no time window defined.");
        return true; // было false, но не совсем корректно, т.к. надо спавнить, если не в расписании
    }

    /// <summary>
    /// Checks if NPCs under the given spawner should remain spawned based on time or schedule.
    /// </summary>
    private bool IsDespawningScheduleEnabled(uint spawnerId)
    {
        if (!SpawnedNpcs.TryGetValue(spawnerId, out var npcs))
            return false;

        foreach (var npc in npcs)
        {
            if (IsWithinDespawnTime(npc))
            {
                //Logger.Debug($"[Despawn] NPC {npc.ObjId} not in allowed time window — stays.");
                return true;
            }

            if (IsNpcInProgress(npc))
            {
                //Logger.Debug($"[Despawn] NPC {npc.ObjId} is within active schedule — stays.");
                return true;
            }
        }

        //Logger.Debug($"[Despawn] All NPCs under Spawner {spawnerId} are outside of time/schedule — despawn allowed.");
        return false;
    }

    private static bool IsWithinDespawnTime(Npc npc)
    {
        var template = npc.Spawner?.Template;
        if (template == null || (template.StartTime <= 0.0f && template.EndTime <= 0.0f))
            return false;

        var curTime = TimeManager.Instance.GetTime;
        var startTime = TimeSpan.FromHours(template.StartTime);
        var endTime = TimeSpan.FromHours(template.EndTime);
        var currentTime = TimeSpan.FromHours(curTime);

        var outside = !IsTimeBetween(currentTime, startTime, endTime);
        //Logger.Debug($"[DespawnTime] NPC {npc.ObjId} time check: now={currentTime}, start={startTime}, end={endTime}, outside={outside}");
        return outside;
    }

    private static bool IsNpcInProgress(Npc npc)
    {
        var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)npc.Spawner.Template.Id);

        switch (status)
        {
            case GameScheduleManager.PeriodStatus.InProgress:
                return true;

            case GameScheduleManager.PeriodStatus.NotFound:
            case GameScheduleManager.PeriodStatus.NotStarted:
            case GameScheduleManager.PeriodStatus.Ended:
                return false;

            default:
                Logger.Warn($"[Schedule] Unknown schedule status '{status}' for NPC {npc.ObjId}. Assuming not in progress.");
                return false;
        }
    }

    /// <summary>
    /// Checks if the current time is between startTime and endTime, including wrapping over midnight.
    /// </summary>
    private static bool IsTimeBetween(TimeSpan currentTime, TimeSpan startTime, TimeSpan endTime)
    {
        if (startTime <= endTime)
        {
            var result = currentTime >= startTime && currentTime <= endTime;
            //Logger.Debug($"[TimeCheck] {currentTime} inside range {startTime}-{endTime}? {result}");
            return result;
        }

        var resultWrapped = currentTime >= startTime || currentTime <= endTime;
        //Logger.Debug($"[TimeCheck] {currentTime} inside wrapped range {startTime}-{endTime}? {resultWrapped}");
        return resultWrapped;
    }

    /// <summary>
    /// Spawns NPCs for an event.
    /// </summary>
    public void DoEventSpawn()
    {
        if (Template == null)
        {
            Logger.Error("Can't spawn npc {0} from spawnerId {1}", UnitId, Id);
            return;
        }

        if (_spawnCount >= Template.MaxPopulation)
            return;

        if (Template.SuspendSpawnCount > 0 && _spawnCount > Template.SuspendSpawnCount)
            return;

        var n = new List<Npc>();
        var nsnTask = Template.Npcs.FirstOrDefault(nsn => nsn.MemberId == UnitId);
        if (nsnTask != null)
        {
            n = nsnTask.Spawn(this);
        }

        try
        {
            foreach (var npc in n)
            {
                AddNpcToSpawned(SpawnerId, npc);
            }
        }
        catch (Exception)
        {
            Logger.Error("Can't spawn npc {0} from spawnerId {1}", UnitId, Template.Id);
        }

        if (n.Count == 0)
        {
            Logger.Error("Can't spawn npc {0} from spawnerId {1}", UnitId, Template.Id);
            return;
        }

        IncrementCount(n);
    }

    private void IncrementCount(List<Npc> n)
    {
        lock (_spawnLock)
        {
            if (_scheduledCount > 0)
                Interlocked.Add(ref _scheduledCount, -n.Count);

            if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcList))
            {
                lock (npcList)
                    Interlocked.Exchange(ref _spawnCount, npcList.Count);
            }
            else
                Interlocked.Exchange(ref _spawnCount, 0);

            if (_spawnCount < 0)
                Interlocked.Exchange(ref _spawnCount, 0);
        }
    }

    private void DecrementCount(bool respawn = false)
    {
        lock (_spawnLock)
        {
            if (respawn)
            {
                _ = Interlocked.Increment(ref _scheduledCount);
                if (_scheduledCount < 0)
                {
                    Interlocked.Exchange(ref _scheduledCount, 0);
                }
            }

            _ = Interlocked.Decrement(ref _spawnCount);
            if (_spawnCount < 0)
            {
                Interlocked.Exchange(ref _spawnCount, 0);
            }
        }
    }

    /// <summary>
    /// Spawns a random NPC, with optional ownerId (used with target_my_npc flag)
    /// </summary>
    public Npc DoRandomSpawn(uint spawnerId, uint ownerId = 0)
    {
        // Get the NPC spawner template
        var template = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
        if (template?.Npcs == null || template.Npcs.Count == 0)
        {
            Logger.Warn($"No NPC templates available for spawner {spawnerId}.");
            return null;
        }
        // Select a random NPC template from the template.Npcs
        var npcTemplate = template.Npcs.RandomElementByWeight(x => x.Weight);
        if (npcTemplate == null)
        {
            Logger.Warn($"Random template returned null on the NPC selection for spawner {spawnerId}.");
            return null;
        }

        try
        {
            // Creates the NPC
            var npc = NpcManager.Instance.Create(0, npcTemplate.MemberId);
            if (npc == null)
            {
                Logger.Warn($"Failed to create NPC from template {npcTemplate.SpawnerId}:{npcTemplate.MemberId}");
                return null;
            }
            // Spawns the NPC
            var spawned = npcTemplate.Spawn(this, ownerId);
            if (spawned == null || spawned.Count == 0)
            {
                Logger.Warn($"No NPCs spawned from template {npcTemplate.SpawnerId}:{npcTemplate.MemberId}");
                return null;
            }
            // Adds the spawned NPC to the list
            if (spawned.Count > 0)
            {
                var spawnedNpc = spawned.First();
                lock (_spawnLock) // Synchronizes access to the list
                {
                    AddNpcToSpawned(spawnedNpc.Spawner.SpawnerId, spawnedNpc);
                }

                spawnedNpc.Spawn();

                return spawnedNpc;
            }
            Logger.Warn($"Failed to retrieve spawned NPC from template {npcTemplate.SpawnerId}:{npcTemplate.MemberId}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to spawn NPC from template {npcTemplate.SpawnerId}:{npcTemplate.MemberId}");
            return null;
        }
    }

    /// <summary>
    /// Spawns NPCs with an effect.
    /// </summary>
    public void DoSpawnEffect(uint spawnerId, SpawnEffect effect, BaseUnit caster, BaseUnit target)
    {
        var template = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
        if (template?.Npcs == null)
            return;

        var n = new List<Npc>();
        var templateNsnTask2 = template.Npcs.FirstOrDefault(nsn => nsn != null && nsn.MemberId == UnitId);
        if (templateNsnTask2 != null)
        {
            n = templateNsnTask2.Spawn(this);
        }

        try
        {
            if (n == null) return;

            foreach (var npc in n)
            {
                if (npc.Spawner != null)
                {
                    npc.Spawner.RespawnTime = 0;
                }

                if (effect.UseSummonerFaction)
                {
                    npc.Faction = target is Npc ? target.Faction : caster.Faction;
                }

                if (effect.UseSummonerAggroTarget && !effect.UseSummonerFaction)
                {
                    if (target is Npc)
                    {
                        npc.Ai.Owner.AddUnitAggro(AggroKind.Damage, (Unit)target, 1);
                    }
                    else
                    {
                        npc.Ai.Owner.AddUnitAggro(AggroKind.Damage, (Unit)caster, 1);
                    }

                    npc.Ai.OnAggroTargetChanged();
                }

                if (effect.LifeTime > 0)
                {
                    TaskManager.Instance.Schedule(new NpcSpawnerDoDespawnTask(npc), TimeSpan.FromSeconds(effect.LifeTime));
                }
            }
        }
        catch (Exception)
        {
            Logger.Error("Can't spawn npc {0} from spawner {1}", UnitId, template.Id);
            return;
        }

        if (n.Count == 0)
        {
            Logger.Error("Can't spawn npc {0} from spawner {1}", UnitId, template.Id);
            return;
        }

        foreach (var npc in n)
        {
            AddNpcToSpawned(SpawnerId, npc);
        }

        if (_scheduledCount > 0)
        {
            Interlocked.Add(ref _scheduledCount, -n.Count);
        }

        if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcList))
        {
            lock (npcList)
            {
                Interlocked.Exchange(ref _spawnCount, npcList.Count);
            }
        }
        else
        {
            Interlocked.Exchange(ref _spawnCount, 0);
        }

        if (_spawnCount < 0)
        {
            Interlocked.Exchange(ref _spawnCount, 0);
        }
    }

    /// <summary>
    /// Clears the spawn count and all spawned NPCs.
    /// </summary>
    public void ClearSpawnCount()
    {
        lock (_spawnLock)
        {
            if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcList))
            {
                if (npcList.Count > 0)
                {
                    npcList.Clear();
                    Interlocked.Exchange(ref _spawnCount, 0);
                    //Logger.Info($"Cleared spawn count and all spawned NPCs for SpawnerId={SpawnerId}.");
                }
                else
                {
                    Logger.Warn($"No NPCs to clear for SpawnerId={SpawnerId}.");
                }
            }
            else
            {
                Logger.Warn($"SpawnerId={SpawnerId} not found in SpawnedNpcs.");
            }
        }
    }

    private void AddNpcToSpawned(uint key, Npc newNpc)
    {
        if (newNpc == null)
        {
            Logger.Warn("Attempted to add a null NPC to SpawnedNpcs.");
            return;
        }

        SpawnedNpcs.AddOrUpdate(
            key,
            k =>
            {
                var newNpcList = new List<Npc> { newNpc };
                //Logger.Debug($"Created new NPC list for key {k} and added NPC {newNpc.ObjId}.");
                return newNpcList;
            },
            (k, existingNpcList) =>
            {
                lock (existingNpcList)
                {
                    existingNpcList.Add(newNpc);
                    //Logger.Debug($"Added NPC {newNpc.ObjId} to existing list for key {k}.");
                    return existingNpcList;
                }
            }
        );
    }

    public static T Clone<T>(T obj)
    {
        var inst = obj.GetType().GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        return (T)inst?.Invoke(obj, null);
    }


    // Helper to handle NPC spawn position adjustments
    public static Vector3 AdjustSpawnPosition(Npc npc, int maxAttempts = 15)
    {
        var collisionRadius = GetCollisionRadiusForNpc(npc);
        var rmax = 0f;
        var rmin = 0f;
        rmin = npc.Spawner.Template.TestRadiusNpc == 0 ? 3 : npc.Spawner.Template.TestRadiusNpc;

        rmax = npc.Spawner.Template.TestRadiusPc == 0 ? rmin : npc.Spawner.Template.TestRadiusPc;

        collisionRadius += Rand.Next(rmin, rmax);

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

    private static float GetCollisionRadiusForNpc(Npc npc)
    {
        // Implement NPC size based logic here
        return npc.ModelSize > 0 ? npc.ModelSize : 1.5f;
    }
}
