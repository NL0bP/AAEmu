using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AAEmu.Commons.Exceptions;
using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;
using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.CommonFarm.Static;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.Items.Containers;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Slaves;
using AAEmu.Game.Models.Game.Transfers;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.slaves;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Utils;

using NLog;

namespace AAEmu.Game.Core.Managers.World;

public class SpawnManager : Singleton<SpawnManager>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private bool _loaded;

    private bool _work = true;
    private readonly ConcurrentBag<GameObject> _respawns = [];
    private readonly ConcurrentBag<GameObject> _despawns = [];

    private readonly ConcurrentDictionary<byte, ConcurrentDictionary<uint, List<NpcSpawner>>> _npcSpawners = new();
    private readonly ConcurrentDictionary<byte, ConcurrentDictionary<uint, List<NpcSpawner>>> _npcEventSpawners = new();
    private readonly ConcurrentDictionary<byte, ConcurrentDictionary<uint, DoodadSpawner>> _doodadSpawners = new();
    private readonly ConcurrentDictionary<byte, ConcurrentDictionary<uint, TransferSpawner>> _transferSpawners = new();
    private readonly ConcurrentDictionary<byte, ConcurrentDictionary<uint, GimmickSpawner>> _gimmickSpawners = new();
    private readonly ConcurrentDictionary<byte, ConcurrentDictionary<uint, SlaveSpawner>> _slaveSpawners = new();
    private readonly ConcurrentBag<Doodad> _playerDoodads = [];

    private uint _nextId = 1u;
    private uint _fakeSpawnerId = 9000001u;

    public void Update()
    {
        foreach (var (worldId, worldSpawners) in _npcSpawners)
        {
            // Фильтрация спавнеров
            //Logger.Debug($"Processed in world {worldId} {worldSpawners.Values.Count} spawners...");
            var activeSpawners = worldSpawners.Values.SelectMany(x => x)
                .Where(spawner => spawner.Template != null && IsSpawnerActive(spawner))
                .ToList();

            //// Последовательная обработка спавнеров
            //if (worldId == 0)
            //{
            //    Logger.Debug($"Processed {activeSpawners.Count} active spawners...");
            //}
            foreach (var npcSpawner in activeSpawners)
            {
                npcSpawner.Update();
            }
        }
    }

    private bool IsSpawnerActive(NpcSpawner spawner)
    {
        if (spawner.IsPlayerInSpawnRadius())
        {
            return true;
        }

        //Logger.Debug($"[SpawnerId={spawner.SpawnerId}, UnitId={spawner.UnitId}] Despawning NPCs...");
        //spawner.DespawnNpcsNow();

        //return spawner.IsThereSpawningSchedule();
        return false;
    }

    /// <summary>
    /// Initializes the SpawnManager and loads all spawn data.
    /// </summary>
    public void Load()
    {
        if (_loaded)
            return;

        InitializeCollections();
        LoadWorldSpawns();
        LoadPersistentDoodads();
        StartRespawnThread();

        _loaded = true;
    }

    /// <summary>
    /// Initializes all collections used by the SpawnManager.
    /// </summary>
    private void InitializeCollections()
    {
        foreach (var world in WorldManager.Instance.GetWorlds())
        {
            _npcSpawners.TryAdd((byte)world.Id, new ConcurrentDictionary<uint, List<NpcSpawner>>());
            _npcEventSpawners.TryAdd((byte)world.Id, new ConcurrentDictionary<uint, List<NpcSpawner>>());
            _doodadSpawners.TryAdd((byte)world.Id, new ConcurrentDictionary<uint, DoodadSpawner>());
            _transferSpawners.TryAdd((byte)world.Id, new ConcurrentDictionary<uint, TransferSpawner>());
            _gimmickSpawners.TryAdd((byte)world.Id, new ConcurrentDictionary<uint, GimmickSpawner>());
            _slaveSpawners.TryAdd((byte)world.Id, new ConcurrentDictionary<uint, SlaveSpawner>());
        }
    }

    /// <summary>
    /// Loads spawn data for all worlds.
    /// </summary>
    private void LoadWorldSpawns()
    {
        Logger.Info("Loading spawns...");
        foreach (var world in WorldManager.Instance.GetWorlds())
        {
            var worldPath = Path.Combine(FileManager.AppPath, "Data", "Worlds", world.Name);
            LoadNpcSpawns(world, worldPath);
            _doodadSpawners[(byte)world.Id] = LoadDoodadSpawns(world, worldPath);
            _transferSpawners[(byte)world.Id] = LoadTransferSpawns(world, worldPath);
            _gimmickSpawners[(byte)world.Id] = LoadGimmickSpawns(world, worldPath);
            _slaveSpawners[(byte)world.Id] = LoadSlaveSpawns(world, worldPath);
        }
    }

    /// <summary>
    /// Loads persistent doodads from the database.
    /// </summary>
    private void LoadPersistentDoodads()
    {
        Logger.Info("Loading persistent doodads...");
        var doodadsSpawned = SpawnPersistentDoodads(DoodadOwnerType.Housing) +
                             SpawnPersistentDoodads(DoodadOwnerType.System) +
                             SpawnPersistentDoodads(DoodadOwnerType.Character);
        Logger.Info($"{doodadsSpawned} doodads loaded.");
    }

    /// <summary>
    /// Starts the respawn thread to handle respawning and despawning of objects.
    /// </summary>
    private void StartRespawnThread()
    {
        var respawnThread = new Thread(CheckRespawns) { Name = "RespawnThread" };
        respawnThread.Start();
    }

    /// <summary>
    /// Loads NPC spawns for a specific world.
    /// </summary>
    private void LoadNpcSpawns(Models.Game.World.World world, string worldPath)
    {
        var npcFiles = GetSpawnFiles(worldPath, "npc_spawns*.json");
        if (npcFiles == null || npcFiles.Length == 0)
            return;

        foreach (var jsonFileName in npcFiles)
        {
            if (!File.Exists(jsonFileName))
            {
                Logger.Info($"World {world.Name} is missing {Path.GetFileName(jsonFileName)}");
                continue;
            }

            var contents = FileManager.GetFileContents(jsonFileName);
            if (string.IsNullOrWhiteSpace(contents))
            {
                Logger.Warn($"File {jsonFileName} is empty.");
                continue;
            }

            if (JsonHelper.TryDeserializeObject(contents, out List<NpcSpawner> npcSpawnersFromFile, out _))
            {
                ProcessNpcSpawners(world, jsonFileName, npcSpawnersFromFile);
            }
            else
            {
                throw new GameException($"SpawnManager: Parse {jsonFileName} file");
            }
        }
    }

    /// <summary>
    /// Processes NPC spawners from a file.
    /// </summary>
    private void ProcessNpcSpawners(Models.Game.World.World world, string jsonFileName, List<NpcSpawner> npcSpawnersFromFile)
    {
        var entry = 0;
        foreach (var npcSpawnerFromFile in npcSpawnersFromFile)
        {
            entry++;

            //if (IsDuplicateNpcSpawner(world, npcSpawnerFromFile))
            //    continue;

            if (!NpcManager.Instance.Exist(npcSpawnerFromFile.UnitId))
            {
                Logger.Trace($"Npc Template {npcSpawnerFromFile.UnitId} (file entry {entry}) doesn't exist - {jsonFileName}");
                continue;
            }

            SetupNpcSpawnerPosition(world, npcSpawnerFromFile);
            AddNpcSpawner(npcSpawnerFromFile);
        }
    }

    /// <summary>
    /// Checks if an NPC spawner is a duplicate.
    /// </summary>
    private bool IsDuplicateNpcSpawner(Models.Game.World.World world, NpcSpawner npcSpawner)
    {
        foreach (var spawners in _npcSpawners[(byte)world.Id].Values)
            foreach (var spawner in spawners)
            {
                if (spawner.UnitId == npcSpawner.UnitId &&
                    Math.Abs(spawner.Position.X - npcSpawner.Position.X) < 0.1f &&
                    Math.Abs(spawner.Position.Y - npcSpawner.Position.Y) < 0.1f)
                {
                    Logger.Warn($"Duplicate NPC spawner found in (UnitId: {spawner.UnitId}, Position: {spawner.Position})\n" +
                                $"                                                              (UnitId: {npcSpawner.UnitId}, Position: {npcSpawner.Position})");
                    return true;
                }
            }

        return false;
    }

    /// <summary>
    /// Sets up the position for an NPC spawner.
    /// </summary>
    private static void SetupNpcSpawnerPosition(Models.Game.World.World world, NpcSpawner npcSpawner)
    {
        npcSpawner.Position.WorldId = world.Id;
        npcSpawner.Position.ZoneId = WorldManager.Instance.GetZoneId(world.Id, npcSpawner.Position.X, npcSpawner.Position.Y);
        npcSpawner.Position.Yaw = npcSpawner.Position.Yaw.DegToRad();
        npcSpawner.Position.Pitch = npcSpawner.Position.Pitch.DegToRad();
        npcSpawner.Position.Roll = npcSpawner.Position.Roll.DegToRad();
    }

    /// <summary>
    /// Gets spawn files from a directory.
    /// </summary>
    public static string[] GetSpawnFiles(string worldPath, string searchPattern)
    {
        try
        {
            return Directory.GetFiles(worldPath, searchPattern);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to get spawn files in {worldPath}");
            return null;
        }
    }

    /// <summary>
    /// Adds an NPC spawner to the manager.
    /// </summary>
    public void AddNpcSpawner(NpcSpawner npcSpawner)
    {
        if (npcSpawner == null)
        {
            Logger.Warn("Attempted to add a null NPC spawner.");
            return;
        }

        if (npcSpawner.NpcSpawnerIds is [0])
            npcSpawner.NpcSpawnerIds = [];

        // check for manually entered NpcSpawnerId
        if (npcSpawner.NpcSpawnerIds.Count == 0)
        {
            var npcSpawnerIds = NpcGameData.Instance.GetSpawnerIds(npcSpawner.UnitId);
            var spawners = new List<NpcSpawner>();
            if (npcSpawnerIds == null)
            {
                Logger.Trace($"SpawnerIds for Npc={npcSpawner.UnitId} doesn't exist");
                Logger.Trace($"Generate Spawner for Npc={npcSpawner.UnitId}...");
                var id = _fakeSpawnerId;
                npcSpawner.NpcSpawnerIds.Add(id);
                npcSpawner.Id = id;
                npcSpawner.SpawnerId = id;
                var tmpTemplate = NpcGameData.Instance.GetNpcSpawnerTemplate(1); // id=1 Test Warrior
                npcSpawner.Template = Helpers.Clone(tmpTemplate);
                npcSpawner.Template.Id = id;

                var tmpNpc = new NpcSpawnerNpc();
                tmpNpc.Position = npcSpawner.Position;
                tmpNpc.MemberId = npcSpawner.UnitId;
                tmpNpc.SpawnerId = id;
                tmpNpc.Id = id;
                tmpNpc.MemberType = "Npc";
                tmpNpc.Weight = 1f;
                tmpNpc.NpcSpawnerTemplateId = id;
                npcSpawner.Template.Npcs = [tmpNpc];
                NpcGameData.Instance.AddNpcSpawnerNpc(tmpNpc);
                NpcGameData.Instance.AddMemberAndSpawnerTemplateIds(tmpNpc);
                NpcGameData.Instance.AddNpcSpawner(npcSpawner.Template);
                _fakeSpawnerId++;
                _nextId++;
                npcSpawner.InitializeSpawnableNpcs(npcSpawner.Template);
                spawners.Add(npcSpawner);
            }
            else
            {
                foreach (var id in npcSpawnerIds)
                {
                    var spawner = NpcSpawner.Clone(npcSpawner);
                    var template = NpcGameData.Instance.GetNpcSpawnerTemplate(id);
                    spawner.InitializeSpawnableNpcs(template);
                    spawner.NpcSpawnerIds.Add(id);
                    spawner.Id = _nextId;
                    spawner.SpawnerId = id;
                    spawner.Template = template;
                    foreach (var n in spawner.Template.Npcs)
                    {
                        n.Position = spawner.Position;
                    }

                    spawners.Add(spawner);
                    _nextId++;
                }
            }
            _npcSpawners[(byte)npcSpawner.Position.WorldId].TryAdd(_nextId, spawners);
        }
        else
        {
            var spawners = new List<NpcSpawner>();
            foreach (var id in npcSpawner.NpcSpawnerIds)
            {
                npcSpawner.Id = id;
                npcSpawner.Template = new NpcSpawnerTemplate(id, npcSpawner.UnitId);
                foreach (var n in npcSpawner.Template.Npcs)
                {
                    n.Position = npcSpawner.Position;
                }
            }
            spawners.Add(npcSpawner);
            _npcEventSpawners[(byte)npcSpawner.Position.WorldId].TryAdd(_nextId, spawners);
            _nextId++;
        }

        Logger.Trace($"Added NPC spawner {npcSpawner.SpawnerId} for unit {npcSpawner.UnitId}.");
    }

    /// <summary>
    /// Spawns all NPCs in a specific world.
    /// </summary>
    internal void SpawnAllNpcs(byte worldId)
    {
        Logger.Info($"Spawning {_npcSpawners[worldId].Count} NPC spawners in world {worldId}");
        var count = 0;
        foreach (var spawners in _npcSpawners[worldId].Values)
        {
            foreach (var spawner in spawners)
            {
                if (spawner.Template == null)
                {
                    Logger.Warn($"Templates not found for Npc templateId {spawner.UnitId} in world {worldId}");
                }
                else
                {
                    spawner.Update();
                    count++;
                    if (count % 5000 == 0)
                    {
                        Logger.Debug($"{count} NPC spawners spawned in world {worldId}");
                    }
                }
            }
        }
        Logger.Info($"{count} NPC spawners spawned in world {worldId}");

        //if (worldId == 0)
        //{
        //    TickManager.Instance.OnTick.Subscribe(Update, TimeSpan.FromSeconds(1));
        //}
    }

    /// <summary>
    /// Despawns all objects in a specific world.
    /// </summary>
    public int DeSpawnAll(byte worldId)
    {
        var world = WorldManager.Instance.GetWorlds().FirstOrDefault(x => x.Id == worldId);
        if (world == null)
        {
            Logger.Warn($"World with ID {worldId} not found.");
            return -1;
        }

        var res = 0;
        foreach (var npc in WorldManager.Instance.GetAllNpcs().ToList())
        {
            if (npc.Spawner != null)
            {
                npc.Spawner.RespawnTime = 9999999;
                npc.Spawner.DoDespawns([npc]);
            }
            else
            {
                npc.Hide();
            }
            res++;
        }

        foreach (var doodad in WorldManager.Instance.GetAllDoodads().ToList())
        {
            if (doodad.Spawner != null)
            {
                doodad.Spawner.RespawnTime = 9999999;
                doodad.Spawner.Despawn(doodad);
            }
            else
            {
                doodad.Hide();
            }
            res++;
        }

        Logger.Info($"Despawned {res} objects in world {worldId}.");
        return res;
    }

    /// <summary>
    /// Handles timed re-spawning and de-spawning of objects.
    /// </summary>
    private void CheckRespawns()
    {
        while (_work)
        {
            var respawns = GetRespawnsReady();
            if (respawns.Count > 0)
            {
                foreach (var obj in respawns)
                {
                    if (obj.Respawn >= DateTime.UtcNow)
                        continue;
                    if (obj is Npc npc)
                    {
                        //Logger.Debug($"Respawning NPC {npc.ObjId}");
                        //npc.Spawner.Respawn(npc);
                        npc.Spawner.SetSpawnScheduled(false); // in the Update() method, enable spawn
                    }
                    else if (obj is Doodad doodad)
                    {
                        //Logger.Trace($"Respawning Doodad {doodad.ObjId}");
                        doodad.Spawner.Respawn(doodad);
                    }
                    else if (obj is Transfer transfer)
                    {
                        //Logger.Trace($"Respawning Transfer {transfer.ObjId}");
                        transfer.Spawner.Respawn(transfer);
                    }
                    else if (obj is Gimmick gimmick)
                    {
                        Logger.Trace($"Respawning Gimmick {gimmick.ObjId}");
                        gimmick.Spawner.Respawn(gimmick);
                    }
                    RemoveRespawn(obj);
                }
            }

            var deSpawns = GetDespawnsReady();
            if (deSpawns.Count > 0)
            {
                Logger.Debug($"Despawning {deSpawns.Count}...");
                foreach (var obj in deSpawns)
                {
                    if (obj.Despawn >= DateTime.UtcNow)
                        continue;
                    if (obj is Npc { Spawner: not null } npc)
                    {
                        //Logger.Debug($"Despawning NPC {npc.ObjId}");
                        npc.Spawner.Despawn(npc);
                    }
                    else if (obj is Doodad { Spawner: not null } doodad)
                    {
                        //Logger.Trace($"Despawning Doodad {doodad.ObjId}");
                        doodad.Spawner.Despawn(doodad);
                    }
                    else if (obj is Transfer { Spawner: not null } transfer)
                    {
                        //Logger.Trace($"Despawning Transfer {transfer.ObjId}");
                        transfer.Spawner.Despawn(transfer);
                    }
                    else if (obj is Gimmick { Spawner: not null } gimmick)
                    {
                        //Logger.Trace($"Despawning Gimmick {gimmick.ObjId}");
                        gimmick.Spawner.Despawn(gimmick);
                    }
                    else if (obj is Slave slave)
                    {
                        //Logger.Trace($"Deleting Slave {slave.ObjId}");
                        slave.Delete();
                    }
                    else if (obj is Doodad doodad2)
                    {
                        //Logger.Trace($"Deleting Doodad {doodad2.ObjId}");
                        doodad2.Delete();
                    }
                    else
                    {
                        //Logger.Trace($"Deleting GameObject {obj.ObjId}");
                        obj.Delete();
                    }

                    ObjectIdManager.Instance.ReleaseId(obj.ObjId);
                    RemoveDespawn(obj);
                }
            }

            Thread.Sleep(1000);
        }
    }

    /// <summary>
    /// Gets a list of objects ready to respawn.
    /// </summary>
    private HashSet<GameObject> GetRespawnsReady()
    {
        var res = new HashSet<GameObject>();
        foreach (var obj in _respawns)
        {
            if (obj.Respawn <= DateTime.UtcNow)
            {
                res.Add(obj);
            }
        }
        return res;
    }

    /// <summary>
    /// Gets a list of objects ready to despawn.
    /// </summary>
    private HashSet<GameObject> GetDespawnsReady()
    {
        var res = new HashSet<GameObject>();
        foreach (var obj in _despawns)
        {
            if (obj.Despawn <= DateTime.UtcNow)
            {
                res.Add(obj);
            }
        }

        return res;
    }

    /// <summary>
    /// Adds an object to the respawn list.
    /// </summary>
    public void AddRespawn(GameObject obj)
    {
        if (obj == null)
        {
            Logger.Warn("Attempted to add a null object to respawn list.");
            return;
        }

        _respawns.Add(obj);
        Logger.Trace($"Added object {obj.ObjId} to respawn list.");
    }

    /// <summary>
    /// Removes an object from the respawn list.
    /// </summary>
    public void RemoveRespawn(GameObject obj)
    {
        if (obj == null)
        {
            Logger.Warn("Attempted to remove a null object from respawn list.");
            return;
        }

        var tempList = new List<GameObject>();
        var removed = false;

        while (_respawns.TryTake(out var item))
        {
            if (!removed && item == obj)
            {
                removed = true;
            }
            else
            {
                tempList.Add(item);
            }
        }

        foreach (var item in tempList)
        {
            _respawns.Add(item);
        }

        if (!removed)
        {
            Logger.Warn($"Failed to remove object {obj.ObjId} from respawn list.");
        }
        else
        {
            Logger.Trace($"Removed object {obj.ObjId} from respawn list.");
        }
    }

    /// <summary>
    /// Adds an object to the despawn list.
    /// </summary>
    public void AddDespawn(GameObject obj)
    {
        if (obj == null)
        {
            Logger.Warn("Attempted to add a null object to despawn list.");
            return;
        }

        _despawns.Add(obj);
        Logger.Trace($"Added object {obj.ObjId} to despawn list.");
    }

    /// <summary>
    /// Removes an object from the despawn list.
    /// </summary>
    public void RemoveDespawn(GameObject obj)
    {
        if (obj == null)
        {
            Logger.Warn("Attempted to remove a null object from despawn list.");
            return;
        }

        var tempList = new List<GameObject>();
        var removed = false;

        while (_despawns.TryTake(out var item))
        {
            if (!removed && item == obj)
            {
                removed = true;
            }
            else
            {
                tempList.Add(item);
            }
        }

        foreach (var item in tempList)
        {
            _despawns.Add(item);
        }

        if (!removed)
        {
            Logger.Warn($"Failed to remove object {obj.ObjId} from despawn list.");
        }
        else
        {
            Logger.Trace($"Removed object {obj.ObjId} from despawn list.");
        }
    }

    /// <summary>
    /// Stops the respawn thread.
    /// </summary>
    public void Stop()
    {
        _work = false;
    }

    /// <summary>
    /// Loads doodad spawns for a specific world.
    /// </summary>
    private ConcurrentDictionary<uint, DoodadSpawner> LoadDoodadSpawns(Models.Game.World.World world, string worldPath)
    {
        var count = 0;
        var countMax = 0;

        var doodadSpawners = new ConcurrentDictionary<uint, DoodadSpawner>();
        var doodadFiles = GetSpawnFiles(worldPath, "doodad_spawns*.json");
        if (doodadFiles == null || doodadFiles.Length == 0)
            return doodadSpawners;

        foreach (var jsonFileName in doodadFiles)
        {
            if (!File.Exists(jsonFileName))
            {
                Logger.Info($"World {world.Name} is missing {Path.GetFileName(jsonFileName)}");
                continue;
            }

            var contents = FileManager.GetFileContents(jsonFileName);
            if (string.IsNullOrWhiteSpace(contents))
            {
                Logger.Warn($"File {jsonFileName} is empty.");
                continue;
            }

            if (JsonHelper.TryDeserializeObject(contents, out List<DoodadSpawner> spawners, out _))
            {
                countMax += spawners.Count;
                var entry = 0;
                foreach (var spawner in spawners)
                {
                    entry++;

                    if (!DoodadManager.Instance.Exist(spawner.UnitId))
                    {
                        Logger.Trace($"Doodad Template {spawner.UnitId} (file entry {entry}) doesn't exist - {jsonFileName}");
                        continue;
                    }

                    // TODO спавнить груз из файла спавна не нужно
                    var doodadTemplate = DoodadManager.Instance.GetTemplate(spawner.UnitId);
                    if (doodadTemplate.GroupId is 38)
                    {
                        Logger.Warn($"Doodad Template {spawner.UnitId} (file entry {entry}) is backpack - {jsonFileName}, Position={spawner.Position}");
                        continue;
                    }

                    //if (IsDuplicateDoodadSpawner(spawner, world.Id))
                    //    continue;

                    spawner.Id = _nextId;
                    spawner.Position.WorldId = world.Id;
                    spawner.Position.ZoneId = WorldManager.Instance.GetZoneId(world.Id, spawner.Position.X, spawner.Position.Y);
                    spawner.Position.Yaw = spawner.Position.Yaw.DegToRad();
                    spawner.Position.Pitch = spawner.Position.Pitch.DegToRad();
                    spawner.Position.Roll = spawner.Position.Roll.DegToRad();
                    doodadSpawners.TryAdd(_nextId, spawner);
                    _nextId++;
                }
            }
            else
            {
                throw new GameException($"SpawnManager: Parse {jsonFileName} file");
            }
        }

        return doodadSpawners;
        bool IsDuplicateDoodadSpawner(DoodadSpawner doodadSpawner, uint worldId)
        {
            foreach (var existingSpawner in doodadSpawners.Values)
            {
                if (existingSpawner.UnitId == doodadSpawner.UnitId &&
                    Math.Abs(existingSpawner.Position.X - doodadSpawner.Position.X) < 0.1f &&
                    Math.Abs(existingSpawner.Position.Y - doodadSpawner.Position.Y) < 0.1f)
                {
                    Logger.Warn($"Duplicate Doodad spawner found in (UnitId: {existingSpawner.UnitId}, Position: {existingSpawner.Position})\n" +
                                $"                                                                 (UnitId: {doodadSpawner.UnitId}, Position: {doodadSpawner.Position})");
                    return true;
                }
            }

            count++;
            if (count % 5000 == 0)
            {
                Logger.Debug($"{count}:{countMax} Doodads loaded in world {worldId}");
            }

            return false;
        }
    }

    /// <summary>
    /// Loads transfer spawns for a specific world.
    /// </summary>
    private ConcurrentDictionary<uint, TransferSpawner> LoadTransferSpawns(Models.Game.World.World world, string worldPath)
    {
        var transferSpawners = new ConcurrentDictionary<uint, TransferSpawner>();
        var transferFiles = GetSpawnFiles(worldPath, "transfer_spawns*.json");
        if (transferFiles == null || transferFiles.Length == 0)
            return transferSpawners;

        foreach (var jsonFileName in transferFiles)
        {
            if (!File.Exists(jsonFileName))
            {
                Logger.Info($"World {world.Name} is missing {Path.GetFileName(jsonFileName)}");
                continue;
            }

            var contents = FileManager.GetFileContents(jsonFileName);
            if (string.IsNullOrWhiteSpace(contents))
            {
                Logger.Warn($"File {jsonFileName} is empty.");
                continue;
            }

            if (JsonHelper.TryDeserializeObject(contents, out List<TransferSpawner> spawners, out _))
            {
                var entry = 0;
                foreach (var spawner in spawners)
                {
                    entry++;

                    if (!TransferManager.Instance.Exist(spawner.UnitId))
                    {
                        Logger.Warn($"Transfer Template {spawner.UnitId} (file entry {entry}) doesn't exist - {jsonFileName}");
                        continue;
                    }

                    spawner.Id = _nextId;
                    spawner.Position.WorldId = world.Id;
                    spawner.Position.ZoneId = WorldManager.Instance.GetZoneId(world.Id, spawner.Position.X, spawner.Position.Y);
                    spawner.Position.Yaw = spawner.Position.Yaw.DegToRad();
                    spawner.Position.Pitch = spawner.Position.Pitch.DegToRad();
                    spawner.Position.Roll = spawner.Position.Roll.DegToRad();
                    transferSpawners.TryAdd(_nextId, spawner);
                    _nextId++;
                }
            }
            else
            {
                throw new GameException($"SpawnManager: Parse {jsonFileName} file");
            }
        }

        return transferSpawners;
    }

    /// <summary>
    /// Loads gimmick spawns for a specific world.
    /// </summary>
    private ConcurrentDictionary<uint, GimmickSpawner> LoadGimmickSpawns(Models.Game.World.World world, string worldPath)
    {
        var gimmickSpawners = new ConcurrentDictionary<uint, GimmickSpawner>();
        var gimmickFiles = GetSpawnFiles(worldPath, "gimmick_spawns*.json");
        if (gimmickFiles == null || gimmickFiles.Length == 0)
            return gimmickSpawners;

        foreach (var jsonFileName in gimmickFiles)
        {
            if (!File.Exists(jsonFileName))
            {
                Logger.Info($"World {world.Name} is missing {Path.GetFileName(jsonFileName)}");
                continue;
            }

            var contents = FileManager.GetFileContents(jsonFileName);
            if (string.IsNullOrWhiteSpace(contents))
            {
                Logger.Warn($"File {jsonFileName} is empty.");
                continue;
            }

            if (JsonHelper.TryDeserializeObject(contents, out List<GimmickSpawner> spawners, out _))
            {
                var entry = 0;
                foreach (var spawner in spawners)
                {
                    entry++;

                    if (spawner.UnitId != 0 && !GimmickManager.Instance.Exist(spawner.UnitId))
                    {
                        Logger.Error($"Gimmick Template {spawner.UnitId} (file entry {entry}) doesn't exist - {jsonFileName}");
                        continue;
                    }

                    spawner.Id = _nextId;
                    spawner.Position.WorldId = world.Id;
                    spawner.Position.ZoneId = WorldManager.Instance.GetZoneId(world.Id, spawner.Position.X, spawner.Position.Y);
                    gimmickSpawners.TryAdd(_nextId, spawner);
                    _nextId++;
                }
            }
            else
            {
                throw new GameException($"SpawnManager: Parse {jsonFileName} file");
            }
        }

        return gimmickSpawners;
    }

    /// <summary>
    /// Loads slave spawns for a specific world.
    /// </summary>
    private ConcurrentDictionary<uint, SlaveSpawner> LoadSlaveSpawns(Models.Game.World.World world, string worldPath)
    {
        var slaveSpawners = new ConcurrentDictionary<uint, SlaveSpawner>();
        var slaveFiles = GetSpawnFiles(worldPath, "slave_spawns*.json");
        if (slaveFiles == null || slaveFiles.Length == 0)
            return slaveSpawners;

        foreach (var jsonFileName in slaveFiles)
        {
            if (!File.Exists(jsonFileName))
            {
                Logger.Info($"World {world.Name} is missing {Path.GetFileName(jsonFileName)}");
                continue;
            }

            var contents = FileManager.GetFileContents(jsonFileName);
            if (string.IsNullOrWhiteSpace(contents))
            {
                Logger.Warn($"File {jsonFileName} is empty.");
                continue;
            }

            if (JsonHelper.TryDeserializeObject(contents, out List<SlaveSpawner> spawners, out _))
            {
                var entry = 0;
                foreach (var spawner in spawners)
                {
                    entry++;

                    if (!SlaveManager.Instance.Exist(spawner.UnitId))
                    {
                        Logger.Warn($"Slave Template {spawner.UnitId} (file entry {entry}) doesn't exist - {jsonFileName}");
                        continue;
                    }

                    spawner.Id = _nextId;
                    spawner.Position.WorldId = world.Id;
                    spawner.Position.ZoneId = WorldManager.Instance.GetZoneId(world.Id, spawner.Position.X, spawner.Position.Y);
                    spawner.Position.Yaw = spawner.Position.Yaw.DegToRad();
                    spawner.Position.Pitch = spawner.Position.Pitch.DegToRad();
                    spawner.Position.Roll = spawner.Position.Roll.DegToRad();
                    slaveSpawners.TryAdd(_nextId, spawner);
                    _nextId++;
                }
            }
            else
            {
                throw new GameException($"SpawnManager: Parse {jsonFileName} file");
            }
        }

        return slaveSpawners;
    }

    /// <summary>
    /// Spawns persistent doodads from the database.
    /// </summary>
    public int SpawnPersistentDoodads(DoodadOwnerType ownerTypeToSpawn, int ownerToSpawnId = -1, GameObject useParentObject = null, bool doSpawn = false)
    {
        var spawnCount = 0;
        var newCoffers = new ConcurrentBag<Doodad>();
        using var connection = MySQL.CreateConnection();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM doodads WHERE owner_type = @OwnerType";
            if (ownerToSpawnId >= 0)
                command.CommandText += " AND house_id = @OwnerId";
            command.CommandText += " ORDER BY `plant_time`";
            command.Parameters.AddWithValue("OwnerType", (byte)ownerTypeToSpawn);
            if (ownerToSpawnId >= 0)
                command.Parameters.AddWithValue("OwnerId", ownerToSpawnId);
            command.Prepare();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var templateId = reader.GetUInt32("template_id");
                    var dbId = reader.GetUInt32("id");
                    var phaseId = reader.GetUInt32("current_phase_id");
                    var x = reader.GetFloat("x");
                    var y = reader.GetFloat("y");
                    var z = reader.GetFloat("z");
                    var roll = reader.GetFloat("roll");
                    var pitch = reader.GetFloat("pitch");
                    var yaw = reader.GetFloat("yaw");
                    var scale = reader.GetFloat("scale");
                    var plantTime = reader.GetDateTime("plant_time");
                    var growthTime = reader.GetDateTime("growth_time");
                    var phaseTime = reader.GetDateTime("phase_time");
                    var freshnessTime = reader.GetDateTime("freshness_time");
                    var ownerId = reader.GetUInt32("owner_id");
                    var ownerType = (DoodadOwnerType)reader.GetByte("owner_type");
                    var attachPoint = (AttachPointKind)reader.GetUInt32("attach_point");
                    var itemId = reader.GetUInt64("item_id");
                    var houseId = reader.GetUInt32("house_id");
                    var parentDoodad = reader.GetUInt32("parent_doodad");
                    var itemTemplateId = reader.GetUInt32("item_template_id");
                    var itemContainerId = reader.GetUInt64("item_container_id");
                    var data = reader.GetInt32("data");
                    var farmType = (FarmType)reader.GetUInt32("farm_type");

                    var doodad = DoodadManager.Instance.Create(0, templateId, null, true);
                    doodad.IsPersistent = true;
                    doodad.DbId = dbId;
                    doodad.FuncGroupId = phaseId;
                    doodad.OwnerId = ownerId;
                    doodad.OwnerType = ownerType;
                    doodad.AttachPoint = attachPoint;
                    doodad.PlantTime = plantTime;
                    doodad.GrowthTime = growthTime;
                    doodad.OverridePhaseTime = phaseTime;
                    doodad.PhaseTime = phaseTime;
                    doodad.FreshnessTime = freshnessTime;
                    doodad.ItemId = itemId;
                    doodad.OwnerDbId = houseId;
                    doodad.SetScale(scale != 0f ? scale : 1f);
                    var sourceItem = ItemManager.Instance.GetItemByItemId(itemId);
                    doodad.ItemTemplateId = sourceItem?.TemplateId ?? itemTemplateId;
                    doodad.UccId = sourceItem?.UccId ?? 0;
                    doodad.SetData(data);
                    doodad.FarmType = farmType;

                    if (parentDoodad > 0)
                    {
                        var pDoodad = _playerDoodads.FirstOrDefault(d => d.DbId == parentDoodad);
                        if (pDoodad == null)
                        {
                            Logger.Warn($"Unable to place doodad {dbId} can't find it's parent doodad {parentDoodad}");
                        }
                        else
                        {
                            doodad.Transform.Parent = pDoodad.Transform;
                            doodad.ParentObj = pDoodad;
                            doodad.ParentObjId = pDoodad.ObjId;
                        }
                    }

                    if (houseId > 0 && doodad.ParentObjId <= 0)
                    {
                        var owningHouse = HousingManager.Instance.GetHouseById(doodad.OwnerDbId);
                        if (owningHouse == null)
                        {
                            Logger.Warn($"Unable to place doodad {dbId} can't find it's owning house {houseId}");
                        }
                        else
                        {
                            doodad.Transform.Parent = owningHouse.Transform;
                            doodad.ParentObj = owningHouse;
                            doodad.ParentObjId = owningHouse.ObjId;
                        }
                    }

                    if (useParentObject != null)
                    {
                        doodad.ParentObj = useParentObject;
                        doodad.ParentObjId = useParentObject.ObjId;
                        doodad.Transform.Parent = useParentObject.Transform;
                    }

                    doodad.Transform.Local.SetPosition(x, y, z);
                    doodad.Transform.Local.SetRotation(roll, pitch, yaw);

                    if (doodad is DoodadCoffer coffer)
                    {
                        if (itemContainerId > 0)
                        {
                            var itemContainer = ItemManager.Instance.GetItemContainerByDbId(itemContainerId);
                            if (itemContainer is CofferContainer cofferContainer)
                                coffer.ItemContainer = cofferContainer;
                            else
                                Logger.Error($"Unable to attach ItemContainer {itemContainerId} to DoodadCoffer, objId: {doodad.ObjId}, DbId: {doodad.DbId}");
                        }
                        else
                        {
                            Logger.Warn($"DoodadCoffer has no persistent ItemContainer assigned to it, creating new one, objId: {doodad.ObjId}, DbId: {doodad.DbId}");
                            coffer.InitializeCoffer(ownerId);
                            newCoffers.Add(coffer);
                        }
                    }

                    if (ownerTypeToSpawn == DoodadOwnerType.Slave && useParentObject is Slave parentSlave)
                    {
                        parentSlave.AttachedDoodads.Add(doodad);
                    }

                    doodad.InitDoodad();
                    _playerDoodads.Add(doodad);
                    spawnCount++;

                    if (doSpawn)
                        doodad.Spawn();
                }
            }
        }

        foreach (var coffer in newCoffers)
            coffer.Save();

        return spawnCount;
    }

    /// <summary>
    /// Spawns all objects in the world.
    /// </summary>
    public void SpawnAll()
    {
        //Logger.Info("Spawning NPCs...");
        //foreach (var (worldId, worldSpawners) in _npcSpawners)
        //{
        //    Task.Run(() =>
        //    {
        //        SpawnAllNpcs(worldId);
        //    });
        //}

        Logger.Info("Spawning Doodads...");
        foreach (var (worldId, worldSpawners) in _doodadSpawners)
        {
            Task.Run(() =>
            {
                Logger.Info($"Spawning {worldSpawners.Count} Doodads in world {worldId}");
                var count = 0;
                foreach (var spawner in worldSpawners.Values)
                {
                    spawner.Spawn(0);
                    count++;
                    if (count % 1000 == 0 && worldId == 0)
                    {
                        Logger.Debug($"in world {worldId} Doodads spawned: {count}...");
                    }
                }
                Logger.Info($"in world {worldId} Doodads spawned: {count}");

                // необходимо дождаться спавна всех doodads
                FishSchoolManager.Instance.Load(worldId);
            });
        }

        Logger.Info("Spawning Transfers...");
        foreach (var (worldId, worldSpawners) in _transferSpawners)
        {
            Task.Run(() =>
            {
                Logger.Info($"Spawning {worldSpawners.Count} Transfers in world {worldId}");
                var count = 0;
                foreach (var spawner in worldSpawners.Values)
                {
                    spawner.SpawnAll();
                    count++;
                    if (count % 10 == 0 && worldId == 0)
                    {
                        Logger.Debug($"in world {worldId} Transfers spawned: {count}...");
                    }
                }
                Logger.Info($"in world {worldId} Transfers spawned: {count}");
            });
        }

        Logger.Info("Spawning Gimmicks...");
        foreach (var (worldId, worldSpawners) in _gimmickSpawners)
        {
            Task.Run(() =>
            {
                Logger.Info($"Spawning {worldSpawners.Count} Gimmicks in world {worldId}");
                var count = 0;
                foreach (var spawner in worldSpawners.Values)
                {
                    spawner.Spawn(0);
                    count++;
                    if (count % 5 == 0 && worldId == 0)
                    {
                        Logger.Debug($"in world {worldId} Gimmicks spawned: {count}...");
                    }
                }
                Logger.Info($"in world {worldId} Gimmicks spawned: {count}");
            });
        }

        Logger.Info("Spawning Slaves...");
        foreach (var (worldId, worldSpawners) in _slaveSpawners)
        {
            Task.Run(() =>
            {
                Logger.Info($"Spawning {worldSpawners.Count} Slaves in world {worldId}");
                var count = 0;
                foreach (var spawner in worldSpawners.Values)
                {
                    spawner.Spawn(0);
                    count++;
                    if (count % 5 == 0 && worldId == 0)
                    {
                        Logger.Debug($"in world {worldId} Slaves spawned: {count}...");
                    }
                }
                Logger.Info($"in world {worldId} slaves spawned: {count}");
            });
        }

        Logger.Info("Spawning Player Doodads asynchronously...");
        Task.Run(() =>
        {
            Logger.Info($"Spawning {_playerDoodads.Count} Player Doodads");
            foreach (var doodad in _playerDoodads)
            {
                if (doodad.Spawner == null)
                {
                    doodad.Spawn();
                }
                else
                {
                    if (doodad.Spawner?.Spawn(doodad.ObjId) == null)
                        Logger.Error($"Failed to spawn player doodad DbId:{doodad.DbId}, TemplateId: {doodad.TemplateId}");
                }
            }
        });
    }

    /// <summary>
    /// Spawns all objects in the world.
    /// </summary>
    public List<Npc> SpawnAll(uint worldId, uint worldTemplateId)
    {
        var npcList = new List<Npc>();
        if (_npcSpawners.TryGetValue((byte)worldTemplateId, out var npcSpawners))
        {
            //Task.Run(() =>
            //{
            foreach (var spawners in npcSpawners.Values)
            {
                foreach (var spawner in spawners)
                {
                    spawner.Position.WorldId = worldId;
                    npcList.Add(spawner.Spawn(0));
                    spawner.Position.WorldId = worldTemplateId;
                }
            }
            //});
        }
        if (_doodadSpawners.TryGetValue((byte)worldTemplateId, out var doodadSpawners))
        {
            //Task.Run(() =>
            //{
            foreach (var spawner in doodadSpawners.Values)
            {
                spawner.Position.WorldId = worldId;
                spawner.Spawn(0);
                spawner.Position.WorldId = worldTemplateId;
            }
            //});
        }
        if (_slaveSpawners.TryGetValue((byte)worldTemplateId, out var slaveSpawners))
        {
            //Task.Run(() =>
            //{
            foreach (var spawner in slaveSpawners.Values)
            {
                spawner.Position.WorldId = worldId;
                spawner.Spawn(0);
                spawner.Position.WorldId = worldTemplateId;
            }
            //});
        }
        if (_gimmickSpawners.TryGetValue((byte)worldTemplateId, out var gimmickSpawners))
        {
            //Task.Run(() =>
            //{
            foreach (var spawner in gimmickSpawners.Values)
            {
                spawner.Position.WorldId = worldId;
                spawner.Spawn(0);
                spawner.Position.WorldId = worldTemplateId;
            }
            //});
        }
        return npcList;
    }

    /// <summary>
    /// Removes a player doodad from the manager.
    /// </summary>
    public void RemovePlayerDoodad(Doodad doodad)
    {
        if (doodad == null)
        {
            Logger.Warn("Attempted to remove a null doodad from player doodads list.");
            return;
        }

        var tempList = new List<Doodad>();
        var removed = false;

        while (_playerDoodads.TryTake(out var item))
        {
            if (!removed && item == doodad)
            {
                removed = true;
            }
            else
            {
                tempList.Add(item);
            }
        }

        foreach (var item in tempList)
        {
            _playerDoodads.Add(item);
        }

        if (!removed)
        {
            Logger.Trace($"Failed to remove doodad {doodad.ObjId} from player doodads list.");
        }
        else
        {
            Logger.Debug($"Removed doodad {doodad.ObjId} from player doodads list.");
        }
    }

    /// <summary>
    /// Gets an NPC spawner by its ID and world ID.
    /// </summary>
    public List<NpcSpawner> GetNpcSpawner(uint spawnerId, byte worldId)
    {
        var ret = new List<NpcSpawner>();
        if (_npcEventSpawners.TryGetValue(worldId, out var npcEventSpawners))
        {
            foreach (var spawners in npcEventSpawners.Values)
            {
                foreach (var spawner in spawners)
                {
                    if (spawner.Id != spawnerId) continue;
                    spawner.Template.Npcs[^1].MemberId = spawner.UnitId;
                    spawner.Template.Npcs[^1].UnitId = spawner.UnitId;
                    spawner.Template.Npcs[^1].MemberType = "Npc";
                    ret.Add(spawner);
                }
            }
        }
        return ret;
    }

    /// <summary>
    /// Gets an NPC spawner by its ID and world ID.
    /// </summary>
    public NpcSpawner GetNpcSpawner(uint unitId, BaseUnit unit)
    {
        var spawner = new NpcSpawner();
        var npcSpawnersIds = NpcGameData.Instance.GetSpawnerIds(unitId);
        if (npcSpawnersIds == null)
        {
            spawner.UnitId = unitId;
            spawner.Id = ObjectIdManager.Instance.GetNextId();
            spawner.NpcSpawnerIds = [spawner.Id];
            spawner.Template = new NpcSpawnerTemplate(spawner.Id);
            spawner.Template.Npcs[0].MemberId = spawner.UnitId;
            spawner.Template.Npcs[0].UnitId = spawner.UnitId;
            spawner.Template.Npcs[0].MemberType = "Npc";
        }
        else
        {
            spawner.UnitId = unitId;
            spawner.Id = npcSpawnersIds[0];
            spawner.NpcSpawnerIds = [spawner.Id];
            spawner.Template = NpcGameData.Instance.GetNpcSpawnerTemplate(spawner.Id);
            if (spawner.Template == null)
            {
                return null;
            }

            spawner.Template.Npcs = [];
            var nsn = NpcGameData.Instance.GetNpcSpawnerNpc(spawner.Id);
            if (nsn == null)
            {
                return null;
            }

            spawner.Template.Npcs.Add(nsn);
            spawner.Template.Npcs[0].MemberId = spawner.UnitId;
            spawner.Template.Npcs[0].UnitId = spawner.UnitId;
        }

        spawner.Position = new WorldSpawnPosition();
        spawner.Position.WorldId = unit.Transform.WorldId;
        spawner.Position.ZoneId = unit.Transform.ZoneId;
        spawner.Position.X = unit.Transform.World.Position.X;
        spawner.Position.Y = unit.Transform.World.Position.Y;
        spawner.Position.Z = unit.Transform.World.Position.Z;
        spawner.Position.Yaw = unit.Transform.World.Rotation.Z;
        spawner.Position.Pitch = 0;
        spawner.Position.Roll = 0;

        return spawner;
    }

    /// <summary>
    /// Gets a list of all treasure chest doodad spawners.
    /// </summary>
    public List<DoodadSpawner> GetTreasureChestDoodadSpawners()
    {
        var chestTemplateIds = DoodadManager.Instance.GetTreasureChestTemplateIds();
        if (chestTemplateIds == null)
            return [];

        var spawnerList = _doodadSpawners.GetValueOrDefault((byte)WorldManager.DefaultWorldId)?.Values
            .Where(ds => chestTemplateIds.Contains(ds.RespawnDoodadTemplateId) || chestTemplateIds.Contains(ds.UnitId))
            .ToList();

        return spawnerList ?? [];
    }

    /// <summary>
    /// Gets all player doodads.
    /// </summary>
    public List<Doodad> GetAllPlayerDoodads()
    {
        return _playerDoodads.ToList();
    }

    /// <summary>
    /// Gets player doodads by character ID.
    /// </summary>
    public List<Doodad> GetPlayerDoodads(uint charId)
    {
        return _playerDoodads.Where(d => d.OwnerId == charId).ToList();
    }

    /// <summary>
    /// Adds a player doodad to the manager.
    /// </summary>
    public void AddPlayerDoodad(Doodad doodad)
    {
        if (doodad == null)
        {
            Logger.Warn("Attempted to add a null doodad to player doodads list.");
            return;
        }

        _playerDoodads.Add(doodad);
        Logger.Trace($"Added doodad {doodad.ObjId} to player doodads list.");
    }
}
