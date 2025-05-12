using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;
using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.IO;
using AAEmu.Game.Models;
using AAEmu.Game.Models.ClientData;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Models.Game.World.Xml;
using AAEmu.Game.Models.Game.World.Zones;
using AAEmu.Game.Models.StaticValues;
using AAEmu.Game.Utils.DB;

using NLog;

using InstanceWorld = AAEmu.Game.Models.Game.World.World;

namespace AAEmu.Game.Core.Managers.World;

public class WorldManager : Singleton<WorldManager>, IWorldManager
{
    // Default World and Instance ID that will be assigned to all Transforms as a Default value
    public static uint DefaultWorldId { get; set; } // This will get reset to its proper value when loading world data (which is usually 0)
    public static uint DefaultInstanceId { get; set; } = 0;
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private bool _loaded;

    private Dictionary<uint, InstanceWorld> _worlds;
    private Dictionary<uint, uint> _worldIdByZoneId;
    private Dictionary<uint, List<uint>> _zonesByWorldId;
    private Dictionary<uint, WorldInteractionGroup> _worldInteractionGroups;
    public bool IsSnowing { get; set; }
    private readonly ConcurrentDictionary<uint, GameObject> _objects = new();
    private readonly ConcurrentDictionary<uint, BaseUnit> _baseUnits = new();
    private readonly ConcurrentDictionary<uint, Unit> _units = new();
    private readonly ConcurrentDictionary<uint, Doodad> _doodads = new();
    private readonly ConcurrentDictionary<uint, Npc> _npcs = new();
    private readonly ConcurrentDictionary<uint, Character> _characters = new();
    private readonly ConcurrentDictionary<uint, AreaShape> _areaShapes = new();
    private readonly ConcurrentDictionary<uint, Transfer> _transfers = new();
    private readonly ConcurrentDictionary<uint, Gimmick> _gimmicks = new();
    private readonly ConcurrentDictionary<uint, Slave> _slaves = new();
    private readonly ConcurrentDictionary<uint, Mate> _mates = new();
    private readonly ConcurrentDictionary<uint, IndunZone> _indunZones = new();

    // ReSharper disable InconsistentNaming
    public const int CELL_SIZE = 1024;
    /// <summary>
    /// Sector Size
    /// </summary>
    public const int REGION_SIZE = 64;
    public const int SECTORS_PER_CELL = CELL_SIZE / REGION_SIZE;
    public const int SECTOR_HMAP_RESOLUTION = REGION_SIZE / 2;
    public const int CELL_HMAP_RESOLUTION = CELL_SIZE / 2;

    /*
    REGION_NEIGHBORHOOD_SIZE (cell sector size) used for polling objects in your proximity
    Was originally set to 1, recommended 3 and max 5
    anything higher is overkill as you can't target it anymore in the client at that distance
    */
    public const sbyte REGION_NEIGHBORHOOD_SIZE = 1;
    // ReSharper enable InconsistentNaming

    public const float DefaultCombatTimeout = 15f;

    private void ActiveRegionTick(TimeSpan delta)
    {
        var sw = new Stopwatch();
        sw.Start();

        // Players
        foreach (var character in GetAllCharacters())
        {
            CombatTick(character);
            RegenTick(character);
            BreathTick(character);
        }

        // Pets
        foreach (var mate in GetAllMates())
        {
            CombatTick(mate);
            RegenTick(mate);
        }

        // Vehicles
        foreach (var slave in GetAllSlaves())
        {
            CombatTick(slave);
            RegenTick(slave);
        }

        SpawnManager.Instance.Update();

        sw.Stop();
        Logger.Warn("ActiveRegionTick took {0}ms", sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Handle is still in combat related things
    /// </summary>
    /// <param name="unit"></param>
    private static void CombatTick(Unit unit)
    {
        // TODO: Make it so you can also become out of combat if you are not on any aggro lists
        if (unit.IsInBattle && unit.LastCombatActivity.AddSeconds(DefaultCombatTimeout) < DateTime.UtcNow)
        {
            unit.IsInBattle = false;
        }

        if ((unit is Character { IsInPostCast: true } character) && character.LastCast.AddSeconds(5) < DateTime.UtcNow)
        {
            character.IsInPostCast = false;
        }
    }

    /// <summary>
    /// Call regeneration function of the unit
    /// </summary>
    /// <param name="unit"></param>
    private static void RegenTick(Unit unit)
    {
        unit.Regenerate();
    }

    /// <summary>
    /// Handle player's Breath updates
    /// </summary>
    /// <param name="character"></param>
    private static void BreathTick(Character character)
    {
        if (character.IsDead || !character.IsUnderWater)
        {
            return;
        }

        character.DoChangeBreath();
    }

    public WorldInteractionGroup? GetWorldInteractionGroup(uint worldInteractionType)
    {
        if (_worldInteractionGroups.TryGetValue(worldInteractionType, out var group))
            return group;
        return null;
    }

    public void Load()
    {
        if (_loaded)
            return;

        _worlds = [];
        _worldIdByZoneId = [];
        _worldInteractionGroups = [];
        _zonesByWorldId = [];

        Logger.Info("Loading world data...");

        #region LoadClientData

        var worldXmlPaths = ClientFileManager.GetFilesInDirectory(Path.Combine("game", "worlds"), "world.xml", true);

        if (worldXmlPaths.Count <= 0)
        {
            throw new OperationCanceledException("No client worlds data has been found, please check the readme.txt file inside the ClientData folder for more info.");
        }
        var worldNames = new List<string>
        {
            "main_world" // Make sure main_world is the first even if it wouldn't exist
        };

        // Grab world_spawns.json info
        var spawnPositionFile = Path.Combine(FileManager.AppPath, "Data", "Worlds", "world_spawns.json");
        var contents = File.Exists(spawnPositionFile) ? File.ReadAllText(spawnPositionFile) : "";
        var worldSpawnLookup = new List<WorldSpawnLocation>();
        if (string.IsNullOrWhiteSpace(contents))
            Logger.Error($"File {spawnPositionFile} doesn't exists or is empty.");
        else
            if (!JsonHelper.TryDeserializeObject(contents, out List<WorldSpawnLocation> worldSpawnLookupFromJson, out _))
            Logger.Error($"Error in {spawnPositionFile}.");
        else
            worldSpawnLookup = worldSpawnLookupFromJson;

        foreach (var worldXmlPath in worldXmlPaths)
        {
            var worldName = Path.GetFileName(Path.GetDirectoryName(worldXmlPath)); // the base name of the current directory
            if (!worldNames.Contains(worldName))
                worldNames.Add(worldName);
        }

        for (uint id = 0; id < worldNames.Count; id++)
        {
            var worldName = worldNames[(int)id];
            if (worldName == "main_world")
                DefaultWorldId = id; // prefer to do it like this, in case we change order or IDs later on

            using var worldXmlData = ClientFileManager.GetFileStream(Path.Combine("game", "worlds", worldName, "world.xml"));
            var xml = new XmlDocument();
            xml.Load(worldXmlData);
            var worldNode = xml.SelectSingleNode("/World");
            if (worldNode != null)
            {
                var xmlWorld = new XmlWorld();
                var world = new InstanceWorld();
                world.Id = id;
                world.TemplateId = id;
                xmlWorld.ReadNode(worldNode, world);
                world.SpawnPosition = worldSpawnLookup.FirstOrDefault(w => w.Name == world.Name)?.SpawnPosition ?? new WorldSpawnPosition();
                world.SpawnPosition.WorldId = id;
                // add coordinates for zones
                foreach (var worldZones in world.XmlWorldZones.Values)
                {
                    foreach (var wsl in worldSpawnLookup)
                    {
                        if (wsl.Name == worldZones.Name)
                        {
                            worldZones.SpawnPosition = wsl.SpawnPosition;
                            worldZones.SpawnPosition.WorldId = id;
                            break;
                        }
                    }
                }

                _worlds.Add(id, world);

                // cache zone keys to world reference
                foreach (var zoneKey in world.ZoneKeys)
                {
                    _worldIdByZoneId.Add(zoneKey, id);

                    if (!_zonesByWorldId.ContainsKey(id))
                        _zonesByWorldId.Add(world.Id, []);
                    _zonesByWorldId[id].Add(zoneKey);
                }

                world.Water = new WaterBodies();
            }
        }

        #endregion

        #region LoadServerDB

        using (var connection2 = SQLite.CreateConnection("Data", "compact.server.table.sqlite3"))
        using (var connection = SQLite.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM indun_zones";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var idz = new IndunZone();
                        idz.ZoneGroupId = reader.GetUInt16("zone_group_id");
                        idz.ClientDriven = reader.GetBoolean("client_driven");
                        idz.Duel = reader.GetBoolean("duel");
                        idz.EnterCount = reader.GetUInt32("enter_count");
                        idz.ExpPanelty = reader.GetBoolean("exp_panelty");
                        idz.HasGraveyard = reader.GetBoolean("has_graveyard");
                        idz.ItemId = reader.IsDBNull("item_id") ? 0 : reader.GetUInt32("item_id");
                        //idz.Name = reader.GetString("name");
                        //idz.Comment = reader.GetString("comment");
                        idz.LevelMax = reader.GetUInt32("level_max");
                        idz.LevelMin = reader.GetUInt32("level_min");
                        idz.MaxPlayers = reader.GetUInt32("max_players");
                        //idz.Option = reader.GetString("option");
                        idz.PartyOnly = reader.GetBoolean("party_only");
                        idz.PvP = reader.GetBoolean("pvp");
                        idz.RestoreItemTime = reader.GetUInt32("restore_item_time");
                        idz.SelectChannel = reader.GetBoolean("select_channel");
                        //idz.LocalizedName = LocalizationManager.Instance.Get("indun_zones", "name", idz.ZoneGroupId, idz.Name);

                        if (!_indunZones.TryAdd(idz.ZoneGroupId, idz))
                            Logger.Fatal($"Unable to add zone_group_id: {idz.ZoneGroupId} from indun_zone");
                    }
                }
            }

            Logger.Debug($"Loaded {_indunZones.Count} dungeon zones");
            /*
            // add dummy main world as ID 0
            if (!_indunZones.TryAdd(0, new IndunZone() { ZoneGroupId = 0, Name = "Main World", LocalizedName = "Erenor" }))
            {
                Logger.Fatal("Failed to add main world");
                return;
            }
            */

            using (var command = connection2.CreateCommand())
            {
                command.CommandText = "SELECT * FROM wi_group_wis";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var id = reader.GetUInt32("wi_id");
                        var group = (WorldInteractionGroup)reader.GetUInt32("wi_group_id");
                        _worldInteractionGroups.Add(id, group);
                    }
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM aoe_shapes";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var shape = new AreaShape();
                        shape.Id = reader.GetUInt32("id");
                        shape.AdjustAngle = reader.GetBoolean("adjust_angle");
                        shape.AreaTargetKindId = reader.GetInt32("area_target_kind_id");
                        shape.CalcDistance = reader.GetBoolean("calc_distance");
                        shape.KindId = reader.GetInt32("kind_id");
                        shape.Type = (AreaShapeType)reader.GetUInt32("kind_id");
                        shape.Value1 = reader.GetFloat("value1");
                        shape.Value2 = reader.GetFloat("value2");
                        shape.Value3 = reader.GetFloat("value3");

                        _areaShapes.TryAdd(shape.Id, shape);
                    }
                }
            }
        }
        #endregion

        TickManager.Instance.OnTick.Subscribe(ActiveRegionTick, TimeSpan.FromSeconds(1));

        _loaded = true;
    }

    public static bool LoadHeightMapFromDatFile(InstanceWorld world)
    {
        var heightMap = Path.Combine(FileManager.AppPath, "Data", "Worlds", world.Name, "hmap.dat");
        if (!File.Exists(heightMap))
        {
            Logger.Trace($"HeightMap for `{world.Name}` not found");
            return false;
        }

        using (var stream = new FileStream(heightMap, FileMode.Open, FileAccess.Read, FileShare.None, 2 << 20))
        using (var br = new BinaryReader(stream))
        {
            var version = br.ReadInt32();
            if (version == 1)
            {
                var hMapCellX = br.ReadInt32();
                var hMapCellY = br.ReadInt32();
                br.ReadDouble(); // heightMaxCoefficient
                br.ReadInt32(); // count

                if (hMapCellX == world.CellX && hMapCellY == world.CellY)
                {
                    for (var cellX = 0; cellX < world.CellX; cellX++)
                    {
                        for (var cellY = 0; cellY < world.CellY; cellY++)
                        {
                            if (br.ReadBoolean())
                                continue;
                            for (var i = 0; i < SECTORS_PER_CELL; i++)
                                for (var j = 0; j < SECTORS_PER_CELL; j++)
                                    for (var x = 0; x < SECTOR_HMAP_RESOLUTION; x++)
                                        for (var y = 0; y < SECTOR_HMAP_RESOLUTION; y++)
                                        {
                                            var sx = cellX * CELL_HMAP_RESOLUTION + i * SECTOR_HMAP_RESOLUTION + x;
                                            var sy = cellY * CELL_HMAP_RESOLUTION + j * SECTOR_HMAP_RESOLUTION + y;

                                            world.HeightMaps[sx, sy] = br.ReadUInt16();
                                        }
                        }
                    }
                }
                else
                {
                    Logger.Warn($"{world.Name}: Invalid heightmap cells, does not match world definition ...");
                    return false;
                }
            }
            else
            {
                Logger.Warn($"{world.Name}: Heightmap version not supported {version}");
                return false;
            }
        }

        Logger.Info($"{world.Name} heightmap loaded");
        return true;
    }

    public static bool LoadHeightMapFromClientData(InstanceWorld world)
    {
        // Use world.xml to check if we have client data enabled
        var worldXmlTest = Path.Combine("game", "worlds", world.Name, "world.xml");
        if (!ClientFileManager.FileExists(worldXmlTest))
            return false;

        var version = VersionCalc.Draft;

        for (var cellY = 0; cellY < world.CellY; cellY++)
            for (var cellX = 0; cellX < world.CellX; cellX++)
            {
                var cellFileName = $"{cellX:000}_{cellY:000}";
                var heightMapFile = Path.Combine("game", "worlds", world.Name, "cells", cellFileName, "client",
                    "terrain", "heightmap.dat");
                if (ClientFileManager.FileExists(heightMapFile))
                    using (var stream = ClientFileManager.GetFileStream(heightMapFile))
                    {
                        if (stream == null)
                        {
                            //Logger.Trace($"Cell {cellFileName} not found or not used in {world.Name}");
                            continue;
                        }

                        // Read the cell hmap data
                        using (var br = new BinaryReader(stream))
                        {
                            var hmap = new Hmap();

                            var disableReCalc = false; // (version == VersionCalc.V1) // Version is never VersionCalc.V1
                            if (hmap.Read(br, disableReCalc) < 0)
                            {
                                Logger.Error($"Error reading {heightMapFile}");
                                continue;
                            }

                            var nodes = hmap.Nodes
                                .OrderBy(cell => cell.BoxHeightmap.Min.X)
                                .ThenBy(cell => cell.BoxHeightmap.Min.Y)
                                .Where(x => x.pHMData.Length > 0)
                                .ToList();

                            // Read nodes into heightmap array

                            #region ReadNodes

                            for (ushort sectorX = 0; sectorX < SECTORS_PER_CELL; sectorX++) // 16x16 sectors / cell
                                for (ushort sectorY = 0; sectorY < SECTORS_PER_CELL; sectorY++)
                                    for (ushort unitX = 0; unitX < SECTOR_HMAP_RESOLUTION; unitX++) // sector = 32x32 unit size
                                        for (ushort unitY = 0; unitY < SECTOR_HMAP_RESOLUTION; unitY++)
                                        {
                                            var node = nodes[sectorX * SECTORS_PER_CELL + sectorY];
                                            var oX = cellX * CELL_HMAP_RESOLUTION + sectorX * SECTOR_HMAP_RESOLUTION + unitX;
                                            var oY = cellY * CELL_HMAP_RESOLUTION + sectorY * SECTOR_HMAP_RESOLUTION + unitY;

                                            ushort value;
                                            switch (version)
                                            {
                                                case VersionCalc.V1:
                                                    {
                                                        var doubleValue = node.fRange * 100000d;
                                                        var rawValue = node.RawDataByIndex(unitX, unitY);

                                                        value = (ushort)((doubleValue / 1.52604335620711f) *
                                                                         world.HeightMaxCoefficient /
                                                                         ushort.MaxValue * rawValue +
                                                                         node.BoxHeightmap.Min.Z * world.HeightMaxCoefficient);
                                                    }
                                                    break;
                                                case VersionCalc.V2:
                                                    {
                                                        value = node.RawDataByIndex(unitX, unitY);
                                                        /* var height */
                                                        _ = node.RawDataToHeight(value);
                                                    }
                                                    break;
                                                case VersionCalc.Draft:
                                                    {
                                                        var height = node.GetHeight(unitX, unitY);
                                                        value = (ushort)(height * world.HeightMaxCoefficient);
                                                    }
                                                    break;
                                                default:
                                                    throw new NotSupportedException(nameof(version));
                                            }

                                            world.HeightMaps[oX, oY] = value;
                                        }

                            #endregion
                        }
                    }
            }

        Logger.Info($"{world.Name} heightmap loaded");
        return true;
    }

    public void LoadHeightmaps()
    {
        if (AppConfiguration.Instance.HeightMapsEnable) // TODO fastboot if HeightMapsEnable = false!
        {
            Logger.Info("Loading heightmaps...");

            var loaded = 0;
            foreach (var world in _worlds.Values)
            {
                if (AppConfiguration.Instance.ClientData.PreferClientHeightMap && LoadHeightMapFromClientData(world))
                    loaded++;
                else if (LoadHeightMapFromDatFile(world))
                    loaded++;
                else if (LoadHeightMapFromClientData(world))
                    loaded++;
            }

            Logger.Info($"Loaded {loaded}/{_worlds.Count} heightmaps");
        }
    }

    public void LoadWaterBodies()
    {
        foreach (var world in _worlds.Values)
        {
            // Try to load from saved json data
            var customFile = Path.Combine(FileManager.AppPath, "Data", "Worlds", world.Name, "water_bodies.json");
            if (File.Exists(customFile))
            {
                if (WaterBodies.Load(customFile, out var newWater))
                {
                    world.Water = newWater;
                }
            }
        }
    }

    public virtual InstanceWorld GetWorld(uint worldId)
    {
        if (_worlds.TryGetValue(worldId, out var res))
            return res;
        Logger.Fatal($"GetWorld(): No such WorldId {worldId}");
        return null;
    }

    public InstanceWorld[] GetWorlds()
    {
        return _worlds.Values.ToArray();
    }

    public uint GetWorldIdByZone(uint zoneId)
    {
        if (_worldIdByZoneId.TryGetValue(zoneId, out var worldId))
            return worldId;
        Logger.Fatal($"GetWorldByZone(): No world defined for ZoneId {zoneId}");
        return 0xffffffff; // -1
    }
    public InstanceWorld GetWorldByZone(uint zoneId)
    {
        if (_worldIdByZoneId.TryGetValue(zoneId, out var worldId))
            return GetWorld(worldId);
        Logger.Fatal($"GetWorldByZone(): No world defined for ZoneId {zoneId}");
        return null;
    }

    public List<uint> GetZonesByWorldId(uint worldId)
    {
        if (_zonesByWorldId.TryGetValue(worldId, out var value))
            return value;
        return [];
    }

    public uint GetZoneId(uint worldId, float x, float y)
    {
        if (!_worlds.TryGetValue(worldId, out var world))
        {
            Logger.Fatal($"GetZoneId(): No such WorldId {worldId}");
            return 0;
        }
        var sx = (int)(x / REGION_SIZE);
        var sy = (int)(y / REGION_SIZE);

        if (!world.ValidRegion(sx, sy))
        {
            Logger.Fatal($"GetZoneId(): Coordinates out of bounds for WorldId {worldId} - x:{x:#,0.#} - y: {y:#,0.#}");
            return 0;
        }

        var region = world.GetRegion(sx, sy);
        return region.ZoneKey;
    }

    /// <summary>
    /// Returns the ground height for given coordinates in the specified zone.
    /// </summary>
    /// <param name="zoneId">Zone ID.</param>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <returns>Height value.</returns>
    public float GetHeight(uint zoneId, float x, float y)
    {
        // try to find Z first in GeoData, and then in HeightMaps, if not found, leave Z as it is
        var height = 0f;
        var world = GetWorldByZone(zoneId);

        if (AppConfiguration.Instance.World.GeoDataMode && world.Id > 0)
        {
            var position = new WorldSpawnPosition { WorldId = 0, ZoneId = zoneId, X = x, Y = y, Z = 0, Yaw = 0, Pitch = 0, Roll = 0 };
            height = AiGeoDataManager.Instance.GetHeight(zoneId, position);
        }

        // check, as there is no geodata for main_world yet
        if (height == 0)
        {
            if (AppConfiguration.Instance.HeightMapsEnable)
            {
                try
                {
                    //var world = GetWorldByZone(zoneId);
                    height = world?.GetHeight(x, y) ?? 0f;
                }
                catch
                {
                    height = 0f;
                }
            }
        }

        return height;
    }

    /// <summary>
    /// Returns target height of World position of transform according to loaded heightmaps
    /// </summary>
    /// <param name="transform"></param>
    /// <returns>Height at target world transform, or transform.World.Position.Z if no heightmap could be found</returns>
    public float GetHeight(Transform transform)
    {
        // try to find Z first in GeoData, and then in HeightMaps, if not found, leave Z as it is
        var height = 0f;
        if (AppConfiguration.Instance.World.GeoDataMode && transform.WorldId > 0)
        {
            height = AiGeoDataManager.Instance.GetHeight(transform.ZoneId, transform.World.Position);
        }

        // check, as there is no geodata for main_world yet
        if (height == 0)
        {
            if (AppConfiguration.Instance.HeightMapsEnable)
            {
                try
                {
                    var world = GetWorld(transform.WorldId);
                    height = world?.GetHeight(transform.World.Position.X, transform.World.Position.Y) ?? transform.World.Position.Z;
                }
                catch
                {
                    height = transform.World.Position.Z;
                }
            }
            else
            {
                height = transform.World.Position.Z;
            }
        }

        return height;
    }

    private static GameObject GetRootObj(GameObject obj)
    {
        if (obj.ParentObj == null)
        {
            return obj;
        }
        else
        {
            return GetRootObj(obj.ParentObj);
        }
    }

    public Region GetRegion(GameObject obj)
    {
        obj = GetRootObj(obj);
        var world = GetWorld(obj.Transform.WorldId);
        return GetRegion(world, obj.Transform.World.Position.X, obj.Transform.World.Position.Y);
    }

    public Region[] GetNeighbors(uint worldId, int x, int y)
    {
        var world = _worlds[worldId];

        var result = new List<Region>();
        for (var a = -REGION_NEIGHBORHOOD_SIZE; a <= REGION_NEIGHBORHOOD_SIZE; a++)
            for (var b = -REGION_NEIGHBORHOOD_SIZE; b <= REGION_NEIGHBORHOOD_SIZE; b++)
                if (ValidRegion(world.Id, x + a, y + b) && world.Regions[x + a, y + b] != null)
                    result.Add(world.Regions[x + a, y + b]);

        return result.ToArray();
    }

    public GameObject GetGameObject(uint objId)
    {
        return _objects.GetValueOrDefault(objId);
    }

    public BaseUnit GetBaseUnit(uint objId)
    {
        return _baseUnits.GetValueOrDefault(objId);
    }

    public Doodad GetDoodad(uint objId)
    {
        return _doodads.GetValueOrDefault(objId);
    }

    public Doodad GetDoodadByDbId(uint dbId)
    {
        var ret = _doodads.FirstOrDefault(x => x.Value.DbId == dbId).Value;
        return ret;
    }

    public List<Doodad> GetDoodadByHouseDbId(uint houseDbId)
    {
        var ret = _doodads.Where(x => x.Value.OwnerDbId == houseDbId).Select(y => y.Value).ToList();
        return ret;
    }

    /// <summary>
    /// Get Active Unit by ObjId
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public Unit GetUnit(uint objId)
    {
        return _units.GetValueOrDefault(objId);
    }

    /// <summary>
    /// Get active NPC by ObjId
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public Npc GetNpc(uint objId)
    {
        return _npcs.GetValueOrDefault(objId);
    }

    /// <summary>
    /// Gets the first active NPC with a specific TemplateId
    /// </summary>
    /// <param name="templateId"></param>
    /// <returns></returns>
    public Npc GetNpcByTemplateId(uint templateId)
    {
        return _npcs.Values.FirstOrDefault(x => x.TemplateId == templateId);
    }

    internal void SetNpc(uint objId, Npc npc)
    {
        _npcs[objId] = npc;
    }

    public Character GetCharacter(string name)
    {
        foreach (var player in _characters.Values)
            if (name.ToLower().Equals(player.Name.ToLower()))
                return player;
        return null;
    }

    /// <summary>
    /// Returns the target character if valid; otherwise returns the current target or self.
    /// </summary>
    /// <param name="character">The source character.</param>
    /// <param name="TargetName">The target name input.</param>
    /// <param name="FirstNonNameArgument">
    /// Returns 1 if TargetName was a valid online character, 0 otherwise.
    /// </param>
    /// <returns>The target character.</returns>
    public static Character GetTargetOrSelf(Character character, string TargetName, out int FirstNonNameArgument)
    {
        FirstNonNameArgument = 0;
        if (!string.IsNullOrWhiteSpace(TargetName))
        {
            var player = Instance.GetCharacter(TargetName);
            if (player != null)
            {
                FirstNonNameArgument = 1;
                return player;
            }
        }
        if (character.CurrentTarget is Character targetCharacter)
            return targetCharacter;
        return character;
    }

    public Character GetCharacterByObjId(uint id)
    {
        return _characters.GetValueOrDefault(id);
    }

    public Character GetCharacterById(uint id)
    {
        return _characters.Values.FirstOrDefault(player => player.Id.Equals(id));
    }

    public Character GetOfflineCharacterInfo(uint characterId)
    {
        var characterInfo = new Character(new UnitCustomModelParams());
        using var connection = MySQL.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM characters WHERE id IN(" + string.Join(",", characterId) + ")";
        command.Prepare();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            characterInfo.Id = reader.GetUInt32("id");
            characterInfo.Name = reader.GetString("name");
            characterInfo.Level = reader.GetByte("level");
            var expeditionId = (FactionsEnum)reader.GetUInt32("expedition_id");
            if (expeditionId != 0)
            {
                var expedition = ExpeditionManager.Instance.GetExpedition(expeditionId);
                characterInfo.Expedition = expedition;
            }
            characterInfo.Family = reader.GetUInt32("family");
            characterInfo.Ability1 = (AbilityType)reader.GetByte("ability1");
            characterInfo.Ability2 = (AbilityType)reader.GetByte("ability2");
            characterInfo.Ability3 = (AbilityType)reader.GetByte("ability3");
            var position = new Transform(null, null, reader.GetUInt32("world_id"), reader.GetUInt32("zone_id"), 1, reader.GetFloat("x"), reader.GetFloat("y"), reader.GetFloat("z"), 0, 0, 0);
            characterInfo.Transform = position;
            characterInfo.Transform.ZoneId = position.ZoneId;
            characterInfo.InParty = false;
            characterInfo.IsOnline = false;
        }

        return characterInfo;
    }

    /// <summary>
    /// Get ChildSlave by ObjId
    /// </summary>
    /// <param name="objId"></param>
    /// <returns></returns>
    public List<Slave> GetChildSlave(uint objId)
    {
        var res = new List<Slave>();
        foreach (var slave in _slaves)
        {
            if (slave.Key == objId)
            {
                res.Add(slave.Value);
            }
        }
        return res;
    }
    public List<Slave> GetAttachedSlavesByObjId(uint objId)
    {
        var res = new List<Slave>();
        foreach (var slave in _slaves)
        {
            if (slave.Key == objId)
            {
                res.AddRange(slave.Value.AttachedSlaves);
            }
        }
        return res;
    }
    public List<Doodad> GetAttachedDoodadsByObjId(uint objId, uint templateId)
    {
        var res = new List<Doodad>();
        foreach (var slave in _slaves)
        {
            if (slave.Key == objId)
            {
                foreach (var doodad in slave.Value.AttachedDoodads)
                {
                    if (doodad.TemplateId == templateId)
                    {
                        res.Add(doodad);
                    }
                }
            }
        }
        return res;
    }

    /// <summary>
    /// Adds a GameObject to the list of existing objects on the server
    /// </summary>
    /// <param name="obj"></param>
    public void AddObject(GameObject obj)
    {
        if (obj == null)
            return;

        _objects.TryAdd(obj.ObjId, obj);

        if (obj is BaseUnit baseUnit)
            _baseUnits.TryAdd(baseUnit.ObjId, baseUnit);
        if (obj is Unit unit)
            _units.TryAdd(unit.ObjId, unit);
        if (obj is Doodad doodad)
            _doodads.TryAdd(doodad.ObjId, doodad);
        if (obj is Npc npc)
            _npcs.TryAdd(npc.ObjId, npc);
        if (obj is Character character)
            _characters.TryAdd(character.ObjId, character);
        if (obj is Transfer transfer)
            _transfers.TryAdd(transfer.ObjId, transfer);
        if (obj is Gimmick gimmick)
            _gimmicks.TryAdd(gimmick.ObjId, gimmick);
        if (obj is Slave slave)
            _slaves.TryAdd(slave.ObjId, slave);
        if (obj is Mate mate)
            _mates.TryAdd(mate.ObjId, mate);
    }

    /// <summary>
    /// Removes a GameObject from all relevant collections by its ID.
    /// </summary>
    /// <param name="objId">The ID of the object to remove.</param>
    /// <returns>True if at least one collection was updated, otherwise false.</returns>
    public bool RemoveObject(uint objId)
    {
        if (objId == 0)
            return false;

        bool removed = RemoveFromCollection(_objects, objId, "object");
        removed |= RemoveFromCollection(_baseUnits, objId, "base unit");
        removed |= RemoveFromCollection(_units, objId, "unit");
        removed |= RemoveFromCollection(_npcs, objId, "NPC");

        return removed;
    }

    /// <summary>
    /// Tries to remove an object from a given collection and logs the removal.
    /// </summary>
    private bool RemoveFromCollection<T>(ConcurrentDictionary<uint, T> collection, uint objId, string collectionName)
    {
        if (collection.TryRemove(objId, out _))
        {
            Logger.Debug($"WorldManager: object {objId} removed from {collectionName}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a GameObject from the list of "existing" objects on the server
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public void RemoveObject(GameObject obj)
    {
        if (obj == null)
            return;

        _objects.TryRemove(obj.ObjId, out _);

        if (obj is BaseUnit)
            _baseUnits.TryRemove(obj.ObjId, out _);
        if (obj is Unit)
            _units.TryRemove(obj.ObjId, out _);
        if (obj is Doodad)
            _doodads.TryRemove(obj.ObjId, out _);
        if (obj is Npc)
            _npcs.TryRemove(obj.ObjId, out _);
        if (obj is Character)
            _characters.TryRemove(obj.ObjId, out _);
        if (obj is Transfer)
            _transfers.TryRemove(obj.ObjId, out _);
        if (obj is Gimmick)
            _gimmicks.TryRemove(obj.ObjId, out _);
        if (obj is Slave)
            _slaves.TryRemove(obj.ObjId, out _);
        if (obj is Mate mate)
            _mates.TryRemove(mate.ObjId, out _);
    }

    // --------------------------------------------------------------------
    // Дополнительные методы для работы с регионами и видимостью объектов
    // Эти изменения позволяют централизовать общую логику обновления списков соседних регионов,
    // снизить дублирование кода в таких методах, как SwitchRegion и UpdateObjectRegion,
    // а также улучшить читаемость и удобство сопровождения класса WorldManager.
    // --------------------------------------------------------------------

    /// <summary>
    /// 1.	Вынести общую логику обновления видимости между старым и новым регионом в хелпер.
    /// Например, добавить приватный метод, который вычисляет разницу между соседними областями и обновляет видимость объекта:
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="oldRegion"></param>
    /// <param name="newRegion"></param>
    private void UpdateRegionVisibility(GameObject obj, Region oldRegion, Region newRegion)
    {
        // Получаем регионы, из которых надо удалить объект.
        var regionsToRemove = oldRegion.FindDifferenceBetweenRegions(newRegion) ?? Enumerable.Empty<Region>();
        // Получаем регионы, в которые надо добавить объект.
        var regionsToAdd = newRegion.FindDifferenceBetweenRegions(oldRegion) ?? Enumerable.Empty<Region>();

        foreach (var region in regionsToRemove)
        {
            region?.RemoveFromCharacters(obj);
        }
        foreach (var region in regionsToAdd)
        {
            if (obj.IsVisible)
                region?.AddToCharacters(obj);
        }
    }

    /// <summary>
    /// 2.	Объединить логику добавления объекта в регион и его соседей (вместо отдельного метода AddToRegion):
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="region"></param>
    private void AddObjectToRegionWithNeighbors(GameObject obj, Region region)
    {
        // Добавляем объект в текущий регион
        region.AddObject(obj);
        obj.Region = region;

        // Добавляем в соседние регионы согласно размеру окрестности
        foreach (var neighbor in region.GetNeighbors())
        {
            neighbor.AddToCharacters(obj);
        }

        // Если нужно добавить в сам регион (например, для персонажа)
        if (obj is Character)
        {
            region.AddToCharacters(obj);
        }
    }

    /// <summary>
    /// 3.	Переработать методы, использующие повторяющуюся логику. Например, метод SwitchRegion можно переписать так:
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="oldRegion"></param>
    /// <param name="newRegion"></param>
    private void SwitchRegion(GameObject obj, Region oldRegion, Region newRegion)
    {
        UpdateRegionVisibility(obj, oldRegion, newRegion);
        newRegion.AddObject(obj);
        obj.Region = newRegion;
        oldRegion.RemoveObject(obj);
    }

    /// <summary>
    /// Аналогично, метод UpdateObjectRegion станет:
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="oldRegion"></param>
    /// <param name="newRegion"></param>
    private void UpdateObjectRegion(GameObject obj, Region oldRegion, Region newRegion)
    {
        UpdateRegionVisibility(obj, oldRegion, newRegion);
        newRegion.AddObject(obj);
        obj.Region = newRegion;
        oldRegion.RemoveObject(obj);
    }

    /// <summary>
    /// Рекурсивно обновляет видимость для дочерних объектов.
    /// </summary>
    /// <param name="obj">Объект, чьи дочерние объекты необходимо обработать.</param>
    private void ProcessChildrenVisibility(GameObject obj)
    {
        if (obj.Transform?.Children == null)
            return;

        // Создаём копию списка, чтобы избежать ошибок при итерации
        foreach (var child in obj.Transform.Children.ToList())
        {
            if (child != null)
            {
                //UpdateObjectVisibility(child.GameObject);
                AddVisibleObject(child.GameObject);
            }
        }
    }

    // --------------------------------------------------------------------
    // Главный метод для работы с регионами и видимостью объектов
    // --------------------------------------------------------------------
    /// <summary>
    /// Обновляет видимость объекта в его регионах, если изменился регион.
    /// </summary>
    /// <param name="obj">Объект, для которого необходимо обновить видимость.</param>
    //public void UpdateObjectVisibility(GameObject obj)
    public void AddVisibleObject(GameObject obj)
    {
        if (obj == null)
            return;

        var targetRegion = GetRegion(obj); // целевой регион (учитывая корневой объект)
        var currentRegion = obj.Region;

        // Если регион не изменился, ничего не делаем
        if (targetRegion == null || (currentRegion != null && currentRegion.Equals(targetRegion)))
            return;

        if (currentRegion != null)
            SwitchRegion(obj, currentRegion, targetRegion);
        else
        {
            //AddToRegion0(obj, targetRegion);
            AddObjectToRegionWithNeighbors(obj, targetRegion);
        }

        ProcessChildrenVisibility(obj);
    }

    /// <summary>
    /// Recursively adds visibility for object's children.
    /// </summary>
    private void AddVisibleChildren(GameObject obj)
    {
        if (obj.Transform?.Children == null)
            return;

        // Create a copy to avoid modification issues during iteration.
        var copy = obj.Transform.Children.ToList();
        foreach (var child in copy)
        {
            if (child != null)
            {
                AddVisibleObject(child.GameObject);
            }
        }
    }

    /// <summary>
    /// Removes a GameObject from its region object list
    /// </summary>
    /// <param name="obj"></param>
    public static void RemoveVisibleObject(GameObject obj)
    {
        if (obj?.Region == null)
            return;

        var neighbors = obj.Region.GetNeighbors();
        obj.Region?.RemoveObject(obj);

        if (neighbors == null)
            return;

        if (neighbors.Length > 0)
            foreach (var neighbor in neighbors)
                neighbor?.RemoveFromCharacters(obj);

        obj.Region = null;

        // Also remove children
        if (obj.Transform is null)
            return;

        if (obj.Transform.Children?.Count > 0)
            foreach (var child in obj.Transform.Children)
                if (child != null)
                    RemoveVisibleObject(child.GameObject);
    }

    public static List<T> GetAround<T>(GameObject obj) where T : class
    {
        var result = new List<T>();
        if (obj?.Region == null)
            return result;

        foreach (var neighbor in obj.Region.GetNeighbors())
            neighbor?.GetList(result, obj.ObjId);

        return result;
    }

    public static List<T> GetAround<T>(GameObject obj, float radius, bool useModelSize = false) where T : class
    {
        var result = new List<T>();
        if (radius <= 0f)
            return result;
        if (obj?.Region == null)
            return result;

        if (useModelSize)
            radius += obj.ModelSize;

        if (radius > 0.0f && RadiusFitsCurrentRegion(obj, radius))
        {
            obj.Region.GetList(result, obj.ObjId, obj.Transform.World.Position.X, obj.Transform.World.Position.Y, radius * radius, useModelSize);
        }
        else
        {
            foreach (var neighbor in obj.Region.GetNeighbors())
                neighbor?.GetList(result, obj.ObjId, obj.Transform.World.Position.X, obj.Transform.World.Position.Y, radius * radius, useModelSize);
        }

        return result;
    }

    private static List<T> GetNeighborRegionsObjs<T>(GameObject obj) where T : class
    {
        var result = new List<T>();

        if (obj?.Region == null) return result;

        foreach (var neighbor in obj.Region.GetNeighbors())
            neighbor?.GetList(result, obj.ObjId);

        return result;
    }

    private static bool RadiusFitsCurrentRegion(GameObject obj, float radius)
    {
        var xMod = obj?.Transform?.World?.Position.X % REGION_SIZE;
        if (xMod - radius < 0 || xMod + radius > REGION_SIZE)
            return false;

        var yMod = obj?.Transform?.World?.Position.Y % REGION_SIZE;
        if (yMod - radius < 0 || yMod + radius > REGION_SIZE)
            return false;
        return true;
    }

    public static List<T> GetAroundByShape<T>(GameObject obj, AreaShape shape) where T : GameObject
    {
        switch (shape.Type)
        {
            case AreaShapeType.Sphere:
                {
                    var radius = shape.Value1 > 0 ? shape.Value1 : 40f;
                    return GetAround<T>(obj, radius, true);
                }
            case AreaShapeType.Cuboid:
                {
                    var diagonal = Math.Sqrt(shape.Value1 * shape.Value1 + shape.Value2 * shape.Value2);
                    var res = GetAround<T>(obj, (float)diagonal, true);
                    res = shape.ComputeCuboid(obj, res);
                    return res;
                }
            default:
                {
                    Logger.Error("AreaShape had impossible type");
                    //throw new ArgumentNullException(nameof(shape), "AreaShape type does not exist!");
                    break;
                }
        }

        return null;
    }

    public List<T> GetInCell<T>(uint worldId, int x, int y) where T : class
    {
        var result = new List<T>();
        var regions = new List<Region>();
        for (var a = x * SECTORS_PER_CELL; a < (x + 1) * SECTORS_PER_CELL; a++)
            for (var b = y * SECTORS_PER_CELL; b < (y + 1) * SECTORS_PER_CELL; b++)
            {
                if (ValidRegion(worldId, a, b) && _worlds[worldId].Regions[a, b] != null)
                    regions.Add(_worlds[worldId].Regions[a, b]);
            }

        foreach (var region in regions)
            region.GetList(result, 0);
        return result;
    }

    [Obsolete("Please use ChatManager.Instance.GetNationChat(race).SendPacker(packet) instead.")]
    public void BroadcastPacketToNation(GamePacket packet, Race race)
    {
        var mRace = (((byte)race - 1) & 0xFC); // some bit magic that makes raceId into some kind of birth continent id
        foreach (var character in _characters.Values)
        {
            var cmRace = (((byte)character.Race - 1) & 0xFC);
            if (mRace != cmRace)
                continue;
            character.SendPacket(packet);
        }
    }

    public void BroadcastPacketToServer(GamePacket packet)
    {
        foreach (var character in _characters.Values)
        {
            character.SendPacket(packet);
        }
    }

    public Region GetRegion(uint zoneId, float x, float y)
    {
        var world = GetWorldByZone(zoneId);
        var sx = (int)(x / REGION_SIZE);
        var sy = (int)(y / REGION_SIZE);
        return world.GetRegion(sx, sy);
    }

    public static Region GetRegion(InstanceWorld world, float x, float y)
    {
        var sx = (int)Math.Floor(x / REGION_SIZE); // Используем Floor для корректного определения региона
        var sy = (int)Math.Floor(y / REGION_SIZE);
        return world.GetRegion(sx, sy);
    }

    private bool ValidRegion(uint worldId, int x, int y)
    {
        var world = GetWorld(worldId);
        return world != null && world.ValidRegion(x, y);
    }

    public void OnPlayerJoin(Character character)
    {
        //turn snow on off 
        Snow(character);

        //family stuff
        if (character.Family > 0)
        {
            FamilyManager.Instance.OnCharacterLogin(character);
        }
        
        //StartingFirstJourney(character);
    }

    private void StartingFirstJourney(Character character)
    {
        var questId = 0u;
        var sphereId = 0u;
        switch (character.Race)
        {
            case Race.Nuian: // Nuian
                questId = 6839;
                sphereId = 2321;
                break;
            case Race.Dwarf: // Dwarf
                questId = 5811;
                sphereId = 2613;
                break;
            case Race.Elf: // Elf
                questId = 6840;
                sphereId = 2322;
                break;
            case Race.Hariharan: // Hariharan
                questId = 6842;
                sphereId = 2324;
                break;
            case Race.Ferre: // Ferre
                questId = 6841;
                sphereId = 2323;
                break;
            case Race.Warborn: // Warborn
                questId = 8228;
                sphereId = 2600;
                break;
            case Race.Fairy:
                break;
            case Race.Returned:
                break;
        }
        // showing it once
        if (character.Updated - character.Created < TimeSpan.FromMinutes(1))
        {
            character.Quests.AddQuestFromSphere(questId, sphereId);
        }
    }

    public void Snow(Character character)
    {
        //send the char the packet
        character.SendPacket(new SCOnOffSnowPacket(IsSnowing));
    }

    public static void ResendVisibleObjectsToCharacter(Character character)
    {
        // Re-send visible flags to character getting out of cinema
        var stuffs = GetNeighborRegionsObjs<GameObject>(character);
        var doodads = new List<Doodad>();
        foreach (var stuff in stuffs)
        {
            if (stuff is Doodad d)
                doodads.Add(d);
            else
                stuff.AddVisibleObject(character);
        }

        for (var i = 0; i < doodads.Count; i += SCDoodadsCreatedPacket.MaxCountPerPacket)
        {
            var count = Math.Min(doodads.Count - i, SCDoodadsCreatedPacket.MaxCountPerPacket);
            var temp = doodads.GetRange(i, count).ToArray();
            character.SendPacket(new SCDoodadsCreatedPacket(temp));
        }
    }

    public List<Character> GetAllCharacters()
    {
        return _characters.Values.ToList();
    }

    public List<Npc> GetAllNpcs()
    {
        return _npcs.Values.ToList();
    }

    public List<Npc> GetAllNpcsFromWorld(uint worldId)
    {
        return _npcs.Values.Where(n => n.Transform.WorldId == worldId).ToList();
    }

    public List<Doodad> GetAllDoodadsFromWorld(uint worldId)
    {
        return _doodads.Values.Where(d => d.Transform.WorldId == worldId).ToList();
    }

    public List<Slave> GetAllSlaves()
    {
        return _slaves.Values.ToList();
    }

    public List<Mate> GetAllMates()
    {
        return _mates.Values.ToList();
    }

    public List<Doodad> GetAllDoodads()
    {
        return _doodads.Values.ToList();
    }

    public List<Gimmick> GetAllGimmicks()
    {
        return _gimmicks.Values.ToList();
    }

    public List<Slave> GetAllSlavesFromWorld(uint worldId)
    {
        return _slaves.Values.Where(n => n.Transform.WorldId == worldId).ToList();
    }

    public AreaShape GetAreaShapeById(uint id)
    {
        return _areaShapes.GetValueOrDefault(id);
    }

    public void Stop()
    {
        if (_worlds is not null)
        {
            foreach (var world in _worlds)
            {
                world.Value?.Physics?.Stop();
            }
        }
    }

    public void StartPhysics()
    {
        foreach (var (_, world) in _worlds)
        {
            world.Physics = new BoatPhysicsManager();
            world.Physics.SimulationWorld = world;
            world.Physics.Initialize();
            world.Physics.StartPhysics();
        }
    }

    /// <summary>
    /// Creates a new instance of a world based on an original world template.
    /// </summary>
    /// <param name="originalWorld">The original world to clone.</param>
    /// <returns>The new world instance.</returns>
    public InstanceWorld CreateWorld(InstanceWorld originalWorld)
    {
        if (originalWorld == null)
            return null;

        // Apply Data to world
        // ReSharper disable once UseObjectOrCollectionInitializer
        var newInstance = new InstanceWorld();
        newInstance.Id = WorldIdManager.Instance.GetNextId();
        newInstance.TemplateId = originalWorld.TemplateId;
        newInstance.Name = originalWorld.Name;
        newInstance.CellX = originalWorld.CellX;
        newInstance.CellY = originalWorld.CellY;
        newInstance.OceanLevel = originalWorld.OceanLevel;
        newInstance.MaxHeight = originalWorld.MaxHeight;
        newInstance.HeightMaxCoefficient = originalWorld.HeightMaxCoefficient;
        newInstance.SpawnPosition = originalWorld.SpawnPosition.Clone();
        newInstance.SpawnPosition.WorldId = newInstance.Id;
        newInstance.ZoneKeys = originalWorld.ZoneKeys;
        newInstance.HeightMaps = originalWorld.HeightMaps; // TODO: takes too long to copy, client disconnects .CloneJson();
        newInstance.XmlWorldZones = originalWorld.XmlWorldZones; // TODO: copy loop
        newInstance.Physics = originalWorld.Physics;  // TODO: copy is looped .CloneJson();
        newInstance.Physics.SimulationWorld.Id = newInstance.Id;
        newInstance.Water = originalWorld.Water; // TODO: .CloneJson();
        var dx = originalWorld.CellX * SECTORS_PER_CELL;
        var dy = originalWorld.CellY * SECTORS_PER_CELL;
        newInstance.Regions = new Region[dx, dy];
        for (var y = 0; y < dy; y++)
        {
            for (var x = 0; x < dx; x++)
            {
                newInstance.Regions[x, y] = new Region(newInstance.Id, x, y, originalWorld.ZoneKeys[0]);
            }
        }

        newInstance.Physics.SimulationWorld.Regions = newInstance.Regions;
        //SpawnManager.Instance.CloneNpcEventSpawners((byte)originalWorld.TemplateId, (byte)newInstance.Id);

        _worlds.Add(newInstance.Id, newInstance);

        return newInstance;
    }

    public void RemoveWorld(uint worldId)
    {
        if (!_worlds.Remove(worldId))
        {
            Logger.Info($"[Dungeon] couldn't remove the dungeon id={worldId}!");
        }
        //if (!SpawnManager.Instance.RemoveNpcEventSpawners((byte)worldId))
        //{
        //    Logger.Info($"[Dungeon] could not delete the list of NpcEventSpawners for dungeon id={worldId}!");
        //}
    }

    /// <summary>
    /// Get a list of NPCs that have loot and are past the "make public" time
    /// </summary>
    /// <returns></returns>
    public HashSet<Npc> GetNpcsToMakePublicLooting()
    {
        HashSet<Npc> temp;
        lock (_npcs)
        {
            temp = [.. _npcs.Values];
        }

        var res = new HashSet<Npc>();
        foreach (var item in temp.Where(item => item.LootingContainer.CanMakePublic()))
            res.Add(item);
        return res;
    }
}
