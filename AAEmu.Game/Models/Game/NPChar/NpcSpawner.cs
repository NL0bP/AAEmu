using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Threading;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
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
    private readonly object _spawnLock = new(); // Lock for thread safety

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
    /// Updates the state of the spawner.
    /// </summary>
    public void Update()
    {
        try
        {
            lock (_spawnLock)
            {

                if (CanDespawnNpcs())
                {
                    DespawnNpcs();
                    return;
                }

                if (CanSpawnNpcs())
                {
                    DoSpawn();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred during NpcSpawner update.");
        }
    }
    private bool CanDespawnNpcs()
    {
        return CanDespawn() && SpawnedNpcs.TryGetValue(SpawnerId, out var npcs);
    }

    private void DespawnNpcs()
    {
        if (IsDespawnScheduled)
            return; // группа уже в стадии удаления

        IsDespawnScheduled = true;

        if (SpawnedNpcs.TryGetValue(SpawnerId, out var npcs))
        {
            DoDespawns(npcs);
        }
    }

    private bool CanSpawnNpcs()
    {
        return CanSpawn() && !IsSpawnDelayNotElapsed();
    }

    private bool IsSpawnDelayNotElapsed()
    {
        if (_lastSpawnTime == DateTime.MinValue)
            return false;

        var elapsedSeconds = (DateTime.UtcNow - _lastSpawnTime).TotalSeconds;
        return elapsedSeconds < Template.SpawnDelayMin;
    }

    private bool CanSpawn()
    {
        if (Template == null)
        {
            Logger.Warn($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Template is null. Cannot determine if NPC can be spawned.");
            return false;
        }

        if (HasCorpse())
        {
            //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC because there is a corpse present.");
            return false;
        }

        if (IsDespawnScheduled)
        {
            //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Is Despawn Scheduled. Spawning is blocked.");
            return false;
        }

        if (!Template.ActivationState)
        {
            //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC because the template's activation state is false.");
            return false;
        }

        //if (IsSpawnCountExceeded())
        //{
        //    Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC because the spawn count has been exceeded.");
        //    return false;
        //}

        if (!IsSpawningScheduleEnabled())
        {
            //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC because the spawning schedule is not enabled.");
            return false;
        }

        //if (AreOtherNpcsInSpawnZone())
        //{
        //    Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC because there are other NPCs in the spawn zone.");
        //    return false;
        //}

        if (!IsOptimalSpawner() || Template.NpcSpawnerCategoryId != NpcSpawnerCategory.Autocreated)
        {
            //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC because this is not the optimal spawner.");
            return false;
        }

        if (!CheckSpawnCountCanSpawn())
        {
            //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC because the spawn count has been exceeded.");
            return false;
        }

        if (!IsPlayerInSpawnRadius())
        {
            //Logger.Debug($"[SpawnerId={SpawnerId}, UnitId={UnitId}] Cannot spawn NPC because there are no players in the spawn radius.");
            return false;
        }

        //Logger.Debug($"All spawn conditions are met for SpawnerId: {UnitId}:{SpawnerId}. NPC can be spawned.");
        return true;
    }

    private bool CheckSpawnCountCanSpawn()
    {
        // Checks if SuspendSpawnCount is exceeded
        if (Template.SuspendSpawnCount > 0 && _spawnCount + AreOtherNpcsInSpawnZone().Item2 >= Template.SuspendSpawnCount)
        {
            //Logger.Debug($"Spawn count ({_spawnCount}:{AreOtherNpcsInSpawnZone().Item2}) for SpawnerId: {UnitId}:{SpawnerId} has reached the suspend limit ({Template.SuspendSpawnCount}). Spawning is blocked.");
            return false;
        }

        // Checks if the maximum number of NPCs has been reached
        if (_spawnCount + AreOtherNpcsInSpawnZone().Item2 >= Template.MaxPopulation)
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

        if (_spawnCount > Template.MinPopulation)
            return true;

        return false;
    }

    private bool IsOptimalSpawner()
    {
        var spawnerId = GetOptimalSpawnerForPlayers();
        return spawnerId != 0 && SpawnerId == spawnerId;
    }

    /// <summary>
    /// Checks if NPCs can be despawned.
    /// </summary>
    private bool CanDespawn()
    {
        if (IsDespawningScheduleEnabled(SpawnerId))
            return true;

        return false; // !IsPlayerInSpawnRadius();
    }

    /// <summary>
    /// Checks if there is a NPC that is a corpse
    /// </summary>
    private bool IsCorpse(List<Npc> npcs)
    {
        return npcs.Any(npc => npc.IsDead);
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
    /// Returns the optimal spawner for spawning NPCs based on the number of players and MinPopulation/MaxPopulation parameters.
    /// </summary>
    /// <returns>
    /// The ID of the selected spawner or <c>null</c> if no suitable spawner is found.
    /// </returns>
    private uint? GetOptimalSpawnerForPlayers()
    {
        // If the list of spawners is empty, return null
        if (NpcSpawnerIds == null || NpcSpawnerIds.Count == 0)
        {
            return null;
        }

        // Get the number of players within the spawn radius
        var playerCount = GetNumberOfPlayerInSpawnRadius(Template);
        if (playerCount == 0)
        {
            return NpcSpawnerIds[0]; // SpawnerId;
        }

        uint? optimalSpawnerId = null;
        var minDeviation = int.MaxValue;

        // Iterate through all spawners and select the suitable one
        foreach (var spawnerId in NpcSpawnerIds)
        {
            // Get the template for the current spawner
            var spawnerTemplate = NpcGameData.Instance.GetNpcSpawnerTemplate(spawnerId);
            // Check if the number of players is suitable for this spawner
            if (playerCount >= spawnerTemplate.MinPopulation && playerCount <= spawnerTemplate.MaxPopulation)
            {
                // Calculate the deviation from MinPopulation and MaxPopulation
                var deviation = Math.Min(Math.Abs(playerCount - (int)spawnerTemplate.MinPopulation), Math.Abs(playerCount - (int)spawnerTemplate.MaxPopulation));

                // If the current deviation is less than the minimum, update the optimal spawner
                if (deviation < minDeviation)
                {
                    minDeviation = deviation;
                    optimalSpawnerId = spawnerId;
                }
            }
        }

        // If no suitable spawner is found, return null
        return optimalSpawnerId ?? SpawnerId;
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
                Logger.Debug($"spawn count={npcs[0].Spawner._spawnCount + npcs[0].Spawner._scheduledCount} for SpawnerId: {UnitId}:{SpawnerId}");
                count += npcs[0].Spawner._spawnCount + npcs[0].Spawner._scheduledCount;
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
    public List<Npc> SpawnAll(bool beginning = false)
    {
        if (IsSpawningScheduleEnabled())
            return null;

        DoSpawn();

        if (IsSpawnScheduled)
            IsDespawningScheduleEnabled(SpawnerId);

        return SpawnedNpcs[SpawnerId];
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

        //if (npc.Respawn > DateTime.UtcNow)
        //{
        //    npc.Hide();
        //}
        //else
        //{
        //    npc.Delete();
        //}
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
                    Logger.Trace($"Removed NPC {npc.ObjId} from list for SpawnerId={spawnerId}.");

                    // If the NPC list is empty, removes the entry from the dictionary
                    if (npcList.Count == 0)
                    {
                        var removedEntry = SpawnedNpcs.TryRemove(spawnerId, out _);
                        if (removedEntry)
                        {
                            Logger.Trace($"Removed empty list for SpawnerId={spawnerId} from SpawnedNpcs.");
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
    public void DecreaseCount(Npc npc)
    {
        if (npc == null)
        {
            Logger.Warn("Attempted to decrease count for a null NPC.");
            return;
        }

        try
        {
            lock (_spawnLock)
            {
                if (_spawnCount <= 0)
                {
                    return;
                }
                //// Decreases the spawn count
                //var newSpawnCount = RemoveNpcFromSpawned(SpawnerId);
                //Logger.Info($"Decreased spawn count for NPC {UnitId}:{SpawnerId}:{npc.ObjId}. New count: {newSpawnCount}.");

                // Schedules respawn if necessary
                if (RespawnTime > 0 && AreOtherNpcsInSpawnZone().Item2 + _scheduledCount < Template.MaxPopulation) // Count
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

                // Sets the despawn time
                npc.Despawn = DateTime.UtcNow.AddSeconds(DespawnTime);

                // Extends the despawn time if there are items in the container
                if (npc.LootingContainer != null && npc.LootingContainer.Items.Count > 0)
                {
                    npc.Despawn += TimeSpan.FromSeconds(LootingContainer.LootDespawnExtensionTime);
                    Logger.Info($"Extended despawn time in {LootingContainer.LootDespawnExtensionTime} seconds for NPC {UnitId}:{SpawnerId}:{npc.ObjId} due to items in looting container.");
                }

                // Adds the NPC to the despawn list
                SpawnManager.Instance.AddDespawn(npc);
                Logger.Info($"Added NPC {UnitId}:{SpawnerId}:{npc.ObjId} to despawn list.");
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

                npc.IsDespawnScheduled = true;

                // будем деспавнить Npc в любом случае
                // we'll despawn the Npc anyway
                // Despawns the NPC if it is not in combat
                //if (!npc.IsInBattle)
                //{
                DecreaseCount(npc);
                Logger.Debug($"Despawned NPC {npc.ObjId}.");
                //}
                //else
                //{
                //    Logger.Trace($"Skipped despawn for NPC {npc.ObjId} because it is in battle.");
                //}
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to despawn NPC {UnitId}:{SpawnerId}:{npc?.ObjId}.");
            }
        }
    }

    /// <summary>
    /// Despawn one NPC
    /// </summary>
    /// <param name="npc">The NPC to despawn.</param>
    public void DoDespawn(Npc npc)
    {
        try
        {
            if (npc == null)
            {
                Logger.Warn("Attempted to despawn a null NPC.");
                return;
            }

            // будем деспавнить Npc в любом случае
            // we'll despawn the Npc anyway
            DecreaseCount(npc);
            Logger.Info($"Despawned NPC {UnitId}:{SpawnerId}:{npc.ObjId}.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to despawn NPC {UnitId}:{SpawnerId}:{npc?.ObjId}.");
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
    /// Schedules NPC spawning.
    /// </summary>
    private bool IsSpawningScheduleEnabled()
    {
        if (Template == null)
        {
            Logger.Warn($"Can't spawn npc {SpawnerId}:{UnitId} from index={Id}");
            return false;
        }

        IsSpawnScheduled = false;

        if (IsWithinSpawnTime())
        {
            IsSpawnScheduled = true;
            return true;
        }

        return CheckGameScheduleStatus();
    }

    private bool IsWithinSpawnTime()
    {
        if (Template.StartTime > 0.0f || Template.EndTime > 0.0f)
        {
            var curTime = TimeManager.Instance.GetTime;
            var startTime = TimeSpan.FromHours(Template.StartTime);
            var endTime = TimeSpan.FromHours(Template.EndTime);
            var currentTime = TimeSpan.FromHours(curTime);

            return IsTimeBetween(currentTime, startTime, endTime);
        }
        return false;
    }

    private bool CheckGameScheduleStatus()
    {
        var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)Template.Id);
        switch (status)
        {
            case GameScheduleManager.PeriodStatus.NotFound:
                IsSpawnScheduled = false;
                return true;
            case GameScheduleManager.PeriodStatus.NotStarted:
            case GameScheduleManager.PeriodStatus.Ended:
                IsSpawnScheduled = false;
                return false;
            case GameScheduleManager.PeriodStatus.InProgress:
                IsSpawnScheduled = true;
                return true;
            default:
                IsSpawnScheduled = false;
                return false;
        }
    }

    private static bool IsTimeBetween(TimeSpan currentTime, TimeSpan startTime, TimeSpan endTime)
    {
        if (startTime <= endTime)
            return currentTime >= startTime && currentTime <= endTime;

        return currentTime >= startTime || currentTime <= endTime;
    }

    /// <summary>
    /// Schedules NPC despawning.
    /// </summary>
    private bool IsDespawningScheduleEnabled(uint spawnerId)
    {
        if (!SpawnedNpcs.TryGetValue(spawnerId, out var npcs))
            return true;

        foreach (var npc in npcs)
        {
            if (IsWithinDespawnTime(npc) || IsNpcInProgress(npc))
                return false;
        }

        return true;
    }

    private static bool IsWithinDespawnTime(Npc npc)
    {
        if (npc.Spawner.Template.StartTime > 0.0f || npc.Spawner.Template.EndTime > 0.0f)
        {
            var curTime = TimeManager.Instance.GetTime;
            var startTime = TimeSpan.FromHours(npc.Spawner.Template.StartTime);
            var endTime = TimeSpan.FromHours(npc.Spawner.Template.EndTime);
            var currentTime = TimeSpan.FromHours(curTime);

            return !IsTimeBetween(currentTime, startTime, endTime);
        }
        return false;
    }

    private static bool IsNpcInProgress(Npc npc)
    {
        var status = GameScheduleManager.Instance.GetPeriodStatusNpc((int)npc.Spawner.Template.Id);
        switch (status)
        {
            case GameScheduleManager.PeriodStatus.NotFound:
                return true;
            case GameScheduleManager.PeriodStatus.NotStarted:
            case GameScheduleManager.PeriodStatus.Ended:
                return false;
            case GameScheduleManager.PeriodStatus.InProgress:
                return true;
            default:
                return true;
        }
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

    private void DecrementCount()
    {
        lock (_spawnLock)
        {
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
                Logger.Trace($"Created new NPC list for key {k} and added NPC {newNpc.ObjId}.");
                return newNpcList;
            },
            (k, existingNpcList) =>
            {
                lock (existingNpcList)
                {
                    existingNpcList.Add(newNpc);
                    Logger.Trace($"Added NPC {newNpc.ObjId} to existing list for key {k}.");
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
    private static Vector3 AdjustSpawnPosition(Npc npc, int maxAttempts = 10)
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
                if (i > 0)
                {
                    Logger.Debug($"Adjusted NPC position after {i + 1} attempts");
                }
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
