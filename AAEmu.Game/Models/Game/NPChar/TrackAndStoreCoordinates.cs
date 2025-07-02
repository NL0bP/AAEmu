using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AAEmu.Commons.IO;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.AI.v2.AiCharacters;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Json;

using Newtonsoft.Json;

namespace AAEmu.Game.Models.Game.NPChar;

public partial class Npc
{
    #region Height Caching System

    private const float NearbyCharactersSearchRadius = 4f;
    private const float NearbyNpcSearchRadius = 15f;
    private const float Tolerance = 0.5f; // порог допуска для корректировки высоты
    private const float FloorThreshold = 5.6f;

    #endregion

    #region Public API

    public float GetReferenceHeight(float x, float y, float z, uint zoneId)
    {
        float finalHeight;

        // 1. Если NPC может летать, то высота берется из позиции спавнера
        if (CanFly)
        {
            finalHeight = Spawner.Position.Z;
            return finalHeight;
        }

        // 2. Для HoldPositionBehavior и IdleBehavior высота берется из спавнера
        if (Ai != null)
        {
            switch (Ai.GetCurrentBehavior())
            {
                case HoldPositionBehavior:
                case IdleBehavior:
                    finalHeight = Spawner.Position.Z;
                    return finalHeight;
            }
        }

        // 3. Получение высоты из базы данных NavMesh
        var navMeshHeight = WorldManager.Instance.GetCorrectNpcHeight(zoneId, x, y, z);
        if (!float.IsNaN(navMeshHeight))
        {
            finalHeight = navMeshHeight;
            //Logger.Debug($"Получили данные по высоте из NavMesh");
            return finalHeight;
        }

        // 4. Получение высоты местности
        var worldHeight = WorldManager.Instance.GetHeight(zoneId, x, y);
        if (worldHeight != 0/* && Math.Abs(worldHeight - Spawner.Position.Z) <= 0.1f*/)
        {
            finalHeight = worldHeight;
            //Logger.Debug($"Получили данные по высоте местности");
            return finalHeight;
        }

        // 5. Берем высоту по умолчанию
        return Spawner?.Position.Z ?? Transform.World.Position.Z;
    }

    #endregion

    #region Private Implementation

    internal float AdjustNpcFloor(float candidate, float? minZ = null, float? maxZ = null)
    {
        var actualMinZ = minZ ?? Math.Min(Spawner.Position.Z, candidate);
        var actualMaxZ = maxZ ?? Math.Max(Spawner.Position.Z, candidate);

        if (actualMaxZ - actualMinZ >= FloorThreshold)
        {
            return Building.GetFloorHeight(actualMinZ, actualMaxZ, Spawner.Position.Z);
        }
        return candidate;
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
    public static bool UpdateSpawnFileHeight(uint unitId, float x, float y, float newZ)
    {
        var spawns = LoadSpawnFile();
        if (spawns == null)
            return false;

        if (UpdateSpawnHeight(spawns, unitId, x, y, newZ))
        {
            SaveSpawnFile(spawns);
            Logger.Info($"Updated spawn height for NPC {unitId} at ({x}, {y}) to Z={newZ}");
            return true;
        }

        return false;
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
