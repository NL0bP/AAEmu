using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

using AAEmu.Commons.IO;
using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Json;
using AAEmu.Game.Utils;

using MySql.Data.MySqlClient;

using Newtonsoft.Json;

namespace AAEmu.Game.Models.Game.NPChar;

public partial class Npc
{
    #region Height Caching System

    private const float CacheGridSize = 1f;
    private const int CacheCleanupIntervalMinutes = 1;
    private const int CacheEntryLifetimeMinutes = 5;
    private const float NearbyCharactersSearchRadius = 4f;
    private const float NearbyNpcSearchRadius = 15f;
    private const float Tolerance = 0.5f; // порог допуска для корректировки высоты
    private const float FloorThreshold = 5.6f;

    private static readonly ConcurrentDictionary<(uint WorldId, uint ZoneId, int GridX, int GridY), CachedHeight> HeightCache = new();
    private static readonly Lazy<Timer> CacheCleanupTimer = new(() => new Timer(CleanupCache, null, TimeSpan.FromMinutes(CacheCleanupIntervalMinutes), TimeSpan.FromMinutes(CacheCleanupIntervalMinutes)));
    
    // Кэш для AdjustNpcFloor
    private static readonly ConcurrentDictionary<float, float> _adjustedFloorCache = new();
    // Кэш для высот. Ключ: (ZoneId, Округленный X, Округленный Y)
    private static readonly ConcurrentDictionary<(uint, int, int), CachedHeight> _heightCache = new();
    // Точность округления координат 1000 (3 знака = 0.001) - миллиметры
    // Точность округления координат 100 (2 знака = 0.01) - сантиметры
    // Точность округления координат 10 (1 знак = 0.1) - дециметры
    private const int COORDINATE_PRECISION = 10;
    private sealed class CachedHeight
    {
        public float Height { get; }
        public float HeightMin { get; }
        public float HeightMax { get; }
        public DateTime LastAccessTime { get; private set; }

        public CachedHeight(float height)
        {
            Height = height;
            LastAccessTime = DateTime.UtcNow;
        }

        public CachedHeight(float height, float heightMin, float heightMax)
        {
            Height = height;
            HeightMin = heightMin;
            HeightMax = heightMax;
            LastAccessTime = DateTime.UtcNow;
        }
    }

    static Npc() => _ = CacheCleanupTimer.Value;

    #endregion

    #region Public API

    internal float GetReferenceHeight(float x, float y, uint? zoneId = null, uint worldId = 0)
    {
        var cacheKey = GetCacheKey(x, y, zoneId, worldId);
        //if (TryGetFromCacheMultiple(cacheKey, x, y, out var cachedHeight))
        //    return cachedHeight;

        return CalculateAndCacheHeight(x, y, cacheKey);
    }

    public float GetReferenceHeight(uint zoneId, float x, float y, float z, float tolerance)
    {
        return GetCachedHeight(zoneId, x, y, z, Tolerance);
    }

    #endregion

    #region Private Implementation

    private float GetCachedHeight(uint zoneId, float x, float y, float z, float tolerance)
    {
        // Нормализация координат
        var roundedX = (int)Math.Round(x * COORDINATE_PRECISION);
        var roundedY = (int)Math.Round(y * COORDINATE_PRECISION);
        var key = (zoneId, roundedX, roundedY);

        // Получение или вычисление высоты с использованием CachedHeight
        var cached = _heightCache.GetOrAdd(key, k =>
        {
            var height = WorldManager.Instance.GetHeight(zoneId, x, y);
            return new CachedHeight(height);
        });

        // Для примера можно залогировать время кэширования
        Logger.Debug("Retrieved height {0} from cache at {1}", cached.Height, cached.LastAccessTime.ToString("o"));

        var candidate = cached.Height;
        if (candidate != 0 && Math.Abs(z - candidate) <= tolerance)
        {
            return AdjustNpcFloorWithCache(candidate);
        }

        return candidate; // или другое значение по умолчанию
    }

    private float AdjustNpcFloorWithCache(float height)
    {
        return _adjustedFloorCache.GetOrAdd(height, h => AdjustNpcFloor(h));
    }

    // Очистка кэша для зоны
    private static void ClearCacheForZone(int zoneId)
    {
        var keysToRemove = _heightCache.Keys
            .Where(k => k.Item1 == zoneId)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _heightCache.TryRemove(key, out _);
        }
    }

    internal static (uint WorldId, uint ZoneId, int GridX, int GridY) GetCacheKey(float x, float y, uint? zoneId = null, uint worldId = 0)
    {
        var gridX = (int)Math.Floor(x / CacheGridSize);
        var gridY = (int)Math.Floor(y / CacheGridSize);
        return (worldId, zoneId ?? WorldManager.Instance.GetZoneId(worldId, x, y), gridX, gridY);
    }

    private static bool TryGetFromCacheMultiple((uint WorldId, uint ZoneId, int GridX, int GridY) centerKey, float x, float y, out float height)
    {
        var worldId = centerKey.WorldId;
        var zoneId = centerKey.ZoneId;
        var gridX = centerKey.GridX;
        var gridY = centerKey.GridY;

        var has00 = HeightCache.TryGetValue((worldId, zoneId, gridX, gridY), out var cell00);
        var has10 = HeightCache.TryGetValue((worldId, zoneId, gridX + 1, gridY), out var cell10);
        var has01 = HeightCache.TryGetValue((worldId, zoneId, gridX, gridY + 1), out var cell01);
        var has11 = HeightCache.TryGetValue((worldId, zoneId, gridX + 1, gridY + 1), out var cell11);

        if (!has00 && !has10 && !has01 && !has11)
        {
            height = 0;
            return false;
        }

        if (has00 && has10 && has01 && has11)
        {
            var localX = (x - gridX * CacheGridSize) / CacheGridSize;
            var localY = (y - gridY * CacheGridSize) / CacheGridSize;
            height = MathUtil.BilinearInterpolation(cell00.Height, cell10.Height, cell01.Height, cell11.Height, localX, localY);
            return true;
        }

        float sum = 0;
        var count = 0;
        if (has00) { sum += cell00.Height; count++; }
        if (has10) { sum += cell10.Height; count++; }
        if (has01) { sum += cell01.Height; count++; }
        if (has11) { sum += cell11.Height; count++; }

        height = sum / count;
        return true;
    }

    private float GetHeightFromNearbyCharacters(float x, float y, uint zoneId)
    {
        var nearbyCharacters = WorldManager.GetAround<Character>(this, NearbyCharactersSearchRadius);
        if (!nearbyCharacters.Any())
            return 0f;

        foreach (var character in nearbyCharacters)
        {
            var pos = character.Transform.World.Position;
            var key = GetCacheKey(pos.X, pos.Y, character.Transform.ZoneId);
            UpdateHeightMapInDatabase(character.Transform.ZoneId, key.GridX, key.GridY, pos.Z);
        }

        return nearbyCharacters
            .Select(c => c.Transform.World.Position)
            .OrderBy(p => MathUtil.CalculateDistance(p, new Vector3(x, y, 0)))
            .First().Z;
    }

    private float CalculateAndCacheHeight2(float x, float y, (uint WorldId, uint ZoneId, int GridX, int GridY) cacheKey)
    {
        var pos = Transform.World.Position;

        // 1. Попытка получить высоту от ближайших персонажей
        float candidate;
        candidate = GetHeightFromNearbyCharacters(x, y, cacheKey.ZoneId);
        if (candidate != 0f)
        {
            candidate = AdjustNpcFloor(candidate);
            HeightCacheAddOrUpdate(candidate, cacheKey);
            return candidate;
        }

        // 2. Получение высоты из базы данных
        //var heights = GetHeightsFromDatabase(x, y, cacheKey.ZoneId, cacheKey.WorldId);
        //if (heights.Item1 != 0/* && Math.Abs(pos.Z - heights.Item1) <= Tolerance*/)
        //{
        //    candidate = AdjustNpcFloor(heights.Item1, heights.Item2, heights.Item3);
        //    HeightCacheAddOrUpdate(candidate, cacheKey);
        //    return candidate;
        //}

        // 3. Получение высоты из WorldManager
        candidate = WorldManager.Instance.GetHeight(cacheKey.ZoneId, x, y);
        if (candidate != 0 && Math.Abs(Spawner.Position.Z - candidate) <= Tolerance)
        {
            candidate = AdjustNpcFloor(candidate);
            return candidate;
        }

        // 4. Берем высоту по умолчанию
        return Spawner.Position.Z;
    }

    // Возвращает высоту и расстояние до ближайшего персонажа
    private (float height, float distance) GetHeightAndDistanceFromNearbyCharacters(float x, float y, uint zoneId)
    {
        var nearbyCharacters = WorldManager.GetAround<Character>(this, NearbyNpcSearchRadius);
        if (!nearbyCharacters.Any())
            return (0f, float.MaxValue);

        var minDistance = float.MaxValue;
        var nearestHeight = 0f;
        foreach (var character in nearbyCharacters)
        {
            var pos = character.Transform.World.Position;
            var dist = MathUtil.CalculateDistance(pos, new Vector3(x, y, 0));
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestHeight = pos.Z;
            }
        }
        return (nearestHeight, minDistance);
    }

    private float CalculateAndCacheHeight(float x, float y, (uint WorldId, uint ZoneId, int GridX, int GridY) cacheKey)
    {
        var pos = Transform.World.Position;

        // Получаем высоту местности
        var worldHeight = WorldManager.Instance.GetHeight(cacheKey.ZoneId, x, y);
        // Получаем высоту и расстояние до ближайшего персонажа
        var (characterHeight, minDistance) = GetHeightAndDistanceFromNearbyCharacters(x, y, cacheKey.ZoneId);

        var finalHeight = worldHeight;
        // Если есть персонаж и его высота больше высоты местности
        if (characterHeight > worldHeight)
        {
            if (minDistance <= NearbyCharactersSearchRadius)
            {
                finalHeight = characterHeight;
            }
            else
            {
                var t = 1f - Math.Clamp(minDistance / NearbyNpcSearchRadius, 0f, 1f);
                finalHeight = worldHeight + (characterHeight - worldHeight) * t;
            }
        }

        if (Spawner.Position.Z - finalHeight > Tolerance)
        {
            finalHeight = Spawner.Position.Z;
        }

        return finalHeight;
    }
    
    internal float AdjustNpcFloor(float candidate, float? minZ = null, float? maxZ = null)
    {
        var actualMinZ = minZ ?? Math.Min(Spawner.Position.Z, candidate);
        var actualMaxZ = maxZ ?? Math.Max(Spawner.Position.Z, candidate);

        if (actualMaxZ - actualMinZ >= FloorThreshold)
        {
            return candidate;
            //return Building.GetFloorHeight(actualMinZ, actualMaxZ, Spawner.Position.Z);
        }
        return candidate;
    }

    public static float HeightCacheAddOrUpdate(float height, (uint WorldId, uint ZoneId, int GridX, int GridY) cacheKey)
    {
        var entry = new CachedHeight(height);
        HeightCache.AddOrUpdate(cacheKey, entry, (_, __) => entry);
        return height;
    }

    private (float avg, float min, float max) GetHeightsFromDatabase(float x, float y, uint zoneId, uint worldId = 0)
    {
        var key = GetCacheKey(x, y, zoneId, worldId);
        try
        {
            using var connection = MySQL.CreateConnection();
            using var transaction = connection.BeginTransaction();
            var heightCells = QueryHeightsMapCells(connection, transaction, key.ZoneId, key.GridX, key.GridY);
            transaction.Commit();

            if (heightCells.Count > 0)
            {
                var avgHeights = heightCells.ToDictionary(kv => kv.Key, kv => kv.Value.Height);
                var minHeights = heightCells.ToDictionary(kv => kv.Key, kv => kv.Value.HeightMin);
                var maxHeights = heightCells.ToDictionary(kv => kv.Key, kv => kv.Value.HeightMax);

                var avg = BilinearInterpolation(x, y, avgHeights, key.GridX, key.GridY);
                var min = BilinearInterpolation(x, y, minHeights, key.GridX, key.GridY);
                var max = BilinearInterpolation(x, y, maxHeights, key.GridX, key.GridY);

                return (avg, min, max);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get heights from database");
        }
        return (0f, 0f, 0f);
    }

    private static Dictionary<(int x, int y), CachedHeight> QueryHeightsMapCells(MySqlConnection connection, MySqlTransaction transaction, uint zoneId, int gridX, int gridY)
    {
        var heights = new Dictionary<(int, int), CachedHeight>();
        using var cmd = new MySqlCommand(
            @"SELECT cell_x, cell_y, avg_z, min_z, max_z 
                  FROM height_map_cells 
                  WHERE zone_id = @zoneId 
                    AND cell_x BETWEEN @minX AND @maxX 
                    AND cell_y BETWEEN @minY AND @maxY",
            connection, transaction);

        cmd.Parameters.AddWithValue("@zoneId", zoneId);
        cmd.Parameters.AddWithValue("@minX", gridX);
        cmd.Parameters.AddWithValue("@maxX", gridX + 1);
        cmd.Parameters.AddWithValue("@minY", gridY);
        cmd.Parameters.AddWithValue("@maxY", gridY + 1);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var x = reader.GetInt32("cell_x");
            var y = reader.GetInt32("cell_y");
            var avgZ = reader.GetFloat("avg_z");
            var minZ = reader.GetFloat("min_z");
            var maxZ = reader.GetFloat("max_z");
            heights[(x, y)] = new CachedHeight(avgZ, minZ, maxZ);
        }
        return heights;
    }

    private static float BilinearInterpolation(float x, float y, IReadOnlyDictionary<(int x, int y), float> heights, int gridX, int gridY)
    {
        var localX = (x - gridX * CacheGridSize) / CacheGridSize;
        var localY = (y - gridY * CacheGridSize) / CacheGridSize;

        var corners = new[]
        {
            (cellX: gridX,     cellY: gridY,     weight: (1 - localX) * (1 - localY)),
            (cellX: gridX + 1, cellY: gridY,     weight: localX * (1 - localY)),
            (cellX: gridX,     cellY: gridY + 1, weight: (1 - localX) * localY),
            (cellX: gridX + 1, cellY: gridY + 1, weight: localX * localY)
        };

        var hasAllCorners = corners.All(c => heights.ContainsKey((c.cellX, c.cellY)));

        if (hasAllCorners)
        {
            var h00 = heights[(corners[0].cellX, corners[0].cellY)];
            var h10 = heights[(corners[1].cellX, corners[1].cellY)];
            var h01 = heights[(corners[2].cellX, corners[2].cellY)];
            var h11 = heights[(corners[3].cellX, corners[3].cellY)];

            return h00 * corners[0].weight +
                   h10 * corners[1].weight +
                   h01 * corners[2].weight +
                   h11 * corners[3].weight;
        }

        var weightedSum = 0f;
        var totalWeight = 0f;
        var availableCorners = 0;

        foreach (var corner in corners)
        {
            if (heights.TryGetValue((corner.cellX, corner.cellY), out var height))
            {
                weightedSum += height * corner.weight;
                totalWeight += corner.weight;
                availableCorners++;
            }
        }

        if (availableCorners > 0)
        {
            if (totalWeight < 0.1f)
            {
                return weightedSum / availableCorners;
            }
            return weightedSum / totalWeight;
        }

        var nearestCorner = corners
            .OrderBy(c => Math.Pow(localX - (c.cellX - gridX), 2) +
                          Math.Pow(localY - (c.cellY - gridY), 2))
            .FirstOrDefault(c => heights.ContainsKey((c.cellX, c.cellY)));

        return nearestCorner != default ?
            heights[(nearestCorner.cellX, nearestCorner.cellY)] :
            0f;
    }

    public static void UpdateHeightMapInDatabase(uint zoneId, int cellX, int cellY, float height)
    {
        try
        {
            using var connection = MySQL.CreateConnection();
            using var cmd = new MySqlCommand(
                @"INSERT INTO height_map_cells 
                      (zone_id, cell_x, cell_y, avg_z, min_z, max_z, point_count, last_update) 
                      VALUES (@zoneId, @cellX, @cellY, @z, @z, @z, 1, NOW()) 
                      ON DUPLICATE KEY UPDATE 
                        avg_z = (avg_z * point_count + @z) / (point_count + 1), 
                        point_count = point_count + 1, 
                        min_z = LEAST(min_z, @z), 
                        max_z = GREATEST(max_z, @z), 
                        last_update = NOW()",
                connection);
            cmd.Parameters.AddWithValue("@zoneId", zoneId);
            cmd.Parameters.AddWithValue("@cellX", cellX);
            cmd.Parameters.AddWithValue("@cellY", cellY);
            cmd.Parameters.AddWithValue("@z", height);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update height map in database");
        }
    }

    private static void CleanupCache0(object state)
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(CacheEntryLifetimeMinutes);
        foreach (var key in HeightCache.Keys)
        {
            if (HeightCache.TryGetValue(key, out var entry) && entry.LastAccessTime < cutoff)
            {
                HeightCache.TryRemove(key, out _);
                Logger.Debug("Removed stale cache entry at {0}", key);
            }
        }
    }
    private static void CleanupCache(object state)
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(CacheEntryLifetimeMinutes);
        foreach (var key in _heightCache.Keys)
        {
            if (_heightCache.TryGetValue(key, out var entry) && entry.LastAccessTime < cutoff)
            {
                _heightCache.TryRemove(key, out _);
                Logger.Debug("Removed stale cache entry at {0}", key);
            }
        }
    }

    /// <summary>
    /// Загружает данные спавна NPC из файла
    /// </summary>
    public static List<JsonNpcSpawns> LoadSpawnFile()
    {
        var spawnsFilePath = Path.Combine(FileManager.AppPath, "Data", "Worlds", "main_world", "npc_spawns_main.json");
        if (!File.Exists(spawnsFilePath))
        {
            Logger.Warn($"Spawn file not found at {spawnsFilePath}");
            return null;
        }

        try
        {
            var spawnsJson = File.ReadAllText(spawnsFilePath);
            return JsonConvert.DeserializeObject<List<JsonNpcSpawns>>(spawnsJson);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load spawn file");
            return null;
        }
    }

    /// <summary>
    /// Находит и обновляет высоту для указанного NPC
    /// </summary>
    public static bool UpdateSpawnHeight(List<JsonNpcSpawns> spawns, uint unitId, float x, float y, float newZ)
    {
        if (spawns == null)
            return false;

        var spawnToUpdate = spawns.FirstOrDefault(s =>
            Math.Abs(s.Position.X - x) < 0.1f &&
            Math.Abs(s.Position.Y - y) < 0.1f &&
            s.UnitId == unitId);

        if (spawnToUpdate == null)
        {
            Logger.Warn($"No matching spawn found for NPC {unitId} at ({x}, {y})");
            return false;
        }

        spawnToUpdate.Position.Z = newZ;
        return true;
    }

    /// <summary>
    /// Сохраняет обновленные данные спавна обратно в файл
    /// </summary>
    public static void SaveSpawnFile(List<JsonNpcSpawns> spawns)
    {
        if (spawns == null)
            return;

        var spawnsFilePath = Path.Combine(FileManager.AppPath, "Data", "Worlds", "main_world", "npc_spawns_main.json");

        try
        {
            var updatedJson = JsonConvert.SerializeObject(spawns, Formatting.Indented);
            File.WriteAllText(spawnsFilePath, updatedJson);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save spawn file");
        }
    }

    /// <summary>
    /// Основной метод для обновления высоты в файле спавна
    /// </summary>
    public static void UpdateSpawnFileHeight(uint unitId, float x, float y, float newZ)
    {
        var spawns = LoadSpawnFile();
        if (spawns == null)
            return;

        if (UpdateSpawnHeight(spawns, unitId, x, y, newZ))
        {
            SaveSpawnFile(spawns);
            Logger.Info($"Updated spawn height for NPC {unitId} at ({x}, {y}) to Z={newZ}");
        }
    }
    #endregion
}

public class Building
{
    private const float FloorThreshold = 5.6f;

    private static int ComputeFloorCount(float minZ, float maxZ)
    {
        var verticalDiff = maxZ - minZ;
        if (verticalDiff < FloorThreshold)
            return 1;
        return (int)(verticalDiff / FloorThreshold) + 1;
    }

    public static float GetFloorHeight(float minZ, float maxZ, int floorIndex)
    {
        var totalFloors = ComputeFloorCount(minZ, maxZ);
        if (floorIndex < 0 || floorIndex >= totalFloors)
            throw new ArgumentOutOfRangeException(nameof(floorIndex), "Номер этажа вне диапазона");

        if (totalFloors == 1)
            return (minZ + maxZ) / 2;

        var delta = (maxZ - minZ) / (totalFloors - 1);
        return minZ + floorIndex * delta;
    }

    public static float GetFloorHeight(float minZ, float maxZ, float referenceZ)
    {
        var totalFloors = ComputeFloorCount(minZ, maxZ);
        if (totalFloors == 1)
            return (minZ + maxZ) / 2;

        var delta = (maxZ - minZ) / (totalFloors - 1);
        var floorIndex = (int)Math.Round((referenceZ - minZ) / delta);

        floorIndex = Math.Max(0, Math.Min(floorIndex, totalFloors - 1));
        return minZ + floorIndex * delta;
    }
}
