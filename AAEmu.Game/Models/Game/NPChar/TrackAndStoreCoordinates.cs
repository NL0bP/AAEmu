using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils;

using MySql.Data.MySqlClient;

namespace AAEmu.Game.Models.Game.NPChar;

public partial class Npc
{
    #region дополнительное кэширование данных
    // Кэш для хранения результатов запросов высот
    private static ConcurrentDictionary<(uint zoneId, int gridX, int gridY), CachedHeight> HeightCache = new();

    // Размер сетки для кэширования (5x5 метров)
    private const float CacheGridSize = 5f; // Размер ячейки в метрах

    private class CachedHeight
    {
        public float Height { get; set; }
    }

    // Метод для получения высоты с кэшированием
    internal float GetReferenceHeight(float x, float y)
    {
        var gridKey = GetCacheGridKey(x, y);

        // 1.Пытаемся получить данные из кэша
        if (HeightCache.TryGetValue((Transform.ZoneId, gridKey.x, gridKey.y), out var cached))
            return cached.Height;

        // 2. Если нет в кэше, вычисляем и сохраняем
        var height = CalculateActualHeight(x, y);

        HeightCache[(Transform.ZoneId, gridKey.x, gridKey.y)] = new CachedHeight
        {
            Height = height,
        };

        return height;
    }

    // Вычисление реальной высоты (без кэша)
    private float CalculateActualHeight(float x, float y)
    {
        // 1. Проверяем стандартную высоту
        var height = WorldManager.Instance.GetHeight(Transform.ZoneId, x, y);
        if (height != 0) return height;

        // 2. Проверяем ближайшие точки в базе
        var dbHeight = GetHeightFromDatabase(x, y);
        if (dbHeight != 0) return dbHeight;

        // 3. Проверяем персонажей в памяти
        return GetHeightFromNearbyCharacters(x, y);
    }

    // Получение ключа для сетки кэширования
    private static (int x, int y) GetCacheGridKey(float x, float y)
    {
        return ((int)(x / CacheGridSize), (int)(y / CacheGridSize));
    }

    private float GetHeightFromDatabase(float x, float y)
    {
        var (centerCellX, centerCellY) = GetCellKey(x, y);
        try
        {
            using var connection = MySQL.CreateConnection();
            // Запрашиваем данные для 9 ячеек (3x3 сетка)
            var cmd = new MySqlCommand(
                "SELECT cell_x, cell_y, avg_z FROM height_map_cells " +
                "WHERE zone_id = @zoneId AND " +
                "cell_x BETWEEN @minX AND @maxX AND " +
                "cell_y BETWEEN @minY AND @maxY",
                connection);
            cmd.Parameters.AddWithValue("@zoneId", Transform.ZoneId);
            cmd.Parameters.AddWithValue("@minX", centerCellX - 1);
            cmd.Parameters.AddWithValue("@maxX", centerCellX + 1);
            cmd.Parameters.AddWithValue("@minY", centerCellY - 1);
            cmd.Parameters.AddWithValue("@maxY", centerCellY + 1);

            var heights = new Dictionary<(int x, int y), float>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    heights[(reader.GetInt32("cell_x"), reader.GetInt32("cell_y"))] = reader.GetFloat("avg_z");
                }
            }

            // Если нашли данные для ячеек, выполняем билинейную интерполяцию
            if (heights.Count > 0)
            {
                return BilinearInterpolation(x, y, heights, centerCellX, centerCellY);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка получения высот из БД: {ex.Message}");
        }

        return 0;
    }
    
    private float GetHeightFromNearbyCharacters(float x, float y)
    {
        const float searchRadius = 5f;
        var nearbyCharacters = WorldManager.GetAround<Character>(this, searchRadius);
        if (nearbyCharacters.Any())
        {
            foreach (var character in nearbyCharacters)
            {
                TrackCharacterCoordinates(character);
            }

            // Берем высоту ближайшего персонажа
            var nearest = nearbyCharacters
                .OrderBy(c => MathUtil.CalculateDistance(c.Transform.World.Position, new Vector3(x, y, 0)))
                .First();
            return nearest.Transform.World.Position.Z;
        }
    
        return 0f; // Если ничего не найдено
    }
    #endregion

    #region метод по квадратам
    // Размер сетки для кэширования (5x5 метров)
    private const float CellSize = CacheGridSize; // Размер ячейки в метрах

    // Метод для получения ключа ячейки
    private static (int cellX, int cellY) GetCellKey(float x, float y)
    {
        return ((int)(x / CellSize), (int)(y / CellSize));
    }

    // Метод для сохранения координат персонажа с учетом ячеек
    public static void TrackCharacterCoordinates(Character character)
    {
        if (character == null) return;

        var pos = character.Transform.World.Position;
        var (cellX, cellY) = GetCellKey(pos.X, pos.Y);

        try
        {
            using var connection = MySQL.CreateConnection();
            // Проверяем существование записи для этой ячейки
            var checkCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM height_map_cells WHERE " +
                "zone_id = @zoneId AND cell_x = @cellX AND cell_y = @cellY",
                connection);
            checkCmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);
            checkCmd.Parameters.AddWithValue("@cellX", cellX);
            checkCmd.Parameters.AddWithValue("@cellY", cellY);

            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

            if (exists)
            {
                // Обновляем только если новая Z-координата отличается
                var updateCmd = new MySqlCommand(
                    "UPDATE height_map_cells SET " +
                    "avg_z = (avg_z * point_count + @z) / (point_count + 1), " +
                    "point_count = point_count + 1, " +
                    "min_z = LEAST(min_z, @z), " +
                    "max_z = GREATEST(max_z, @z), " +
                    "last_update = NOW() " +
                    "WHERE zone_id = @zoneId AND cell_x = @cellX AND cell_y = @cellY",
                    connection);
                updateCmd.Parameters.AddWithValue("@z", pos.Z);
                updateCmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);
                updateCmd.Parameters.AddWithValue("@cellX", cellX);
                updateCmd.Parameters.AddWithValue("@cellY", cellY);
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                // Вставляем новую запись для ячейки
                var insertCmd = new MySqlCommand(
                    "INSERT INTO height_map_cells " +
                    "(zone_id, cell_x, cell_y, avg_z, min_z, max_z, point_count, last_update) " +
                    "VALUES (@zoneId, @cellX, @cellY, @z, @z, @z, 1, NOW())",
                    connection);
                insertCmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);
                insertCmd.Parameters.AddWithValue("@cellX", cellX);
                insertCmd.Parameters.AddWithValue("@cellY", cellY);
                insertCmd.Parameters.AddWithValue("@z", pos.Z);
                insertCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка сохранения координат персонажа: {ex.Message}");
        }
    }

    // Модифицированный метод GetReferenceHeight с интерполяцией
    internal float GetReferenceHeight2(float x, float y)
    {
        // 1. Сначала получаем высоту ландшафта
        var landscapeHeight = WorldManager.Instance.GetHeight(Transform.ZoneId, x, y);
        if (landscapeHeight != 0)
            return landscapeHeight;

        // 2. Получаем ключи для текущей и соседних ячеек
        var (centerCellX, centerCellY) = GetCellKey(x, y);
        try
        {
            using var connection = MySQL.CreateConnection();
            // Запрашиваем данные для 9 ячеек (3x3 сетка)
            var cmd = new MySqlCommand(
                "SELECT cell_x, cell_y, avg_z FROM height_map_cells " +
                "WHERE zone_id = @zoneId AND " +
                "cell_x BETWEEN @minX AND @maxX AND " +
                "cell_y BETWEEN @minY AND @maxY",
                connection);
            cmd.Parameters.AddWithValue("@zoneId", Transform.ZoneId);
            cmd.Parameters.AddWithValue("@minX", centerCellX - 1);
            cmd.Parameters.AddWithValue("@maxX", centerCellX + 1);
            cmd.Parameters.AddWithValue("@minY", centerCellY - 1);
            cmd.Parameters.AddWithValue("@maxY", centerCellY + 1);

            var heights = new Dictionary<(int x, int y), float>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    heights[(reader.GetInt32("cell_x"), reader.GetInt32("cell_y"))] =
                        reader.GetFloat("avg_z");
                }
            }

            // Если нашли данные для ячеек, выполняем билинейную интерполяцию
            if (heights.Count > 0)
            {
                return BilinearInterpolation(x, y, heights, centerCellX, centerCellY);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка получения высот из БД: {ex.Message}");
        }

        // 3. Fallback: проверяем персонажей в памяти
        const float searchRadius = 5f;
        var nearbyCharacters = WorldManager.GetAround<Character>(this, searchRadius);
        if (nearbyCharacters.Any())
        {
            foreach (var character in nearbyCharacters)
            {
                TrackCharacterCoordinates(character);
            }

            // Берем высоту ближайшего персонажа
            var nearest = nearbyCharacters
                .OrderBy(c => MathUtil.CalculateDistance(c.Transform.World.Position, new Vector3(x, y, 0)))
                .First();
            return nearest.Transform.World.Position.Z;
        }

        return 0f; // Если ничего не найдено
    }

    // Билинейная интерполяция для высот
    private float BilinearInterpolation(float x, float y, Dictionary<(int x, int y), float> heights, int centerCellX, int centerCellY)
    {
        // Координаты относительно центральной ячейки
        float localX = (x - centerCellX * CellSize) / CellSize;
        float localY = (y - centerCellY * CellSize) / CellSize;

        // Получаем высоты для 4 ближайших ячеек
        float? z00 = heights.TryGetValue((centerCellX, centerCellY), out var val00) ? val00 : (float?)null;
        float? z01 = heights.TryGetValue((centerCellX, centerCellY + 1), out var val01) ? val01 : (float?)null;
        float? z10 = heights.TryGetValue((centerCellX + 1, centerCellY), out var val10) ? val10 : (float?)null;
        float? z11 = heights.TryGetValue((centerCellX + 1, centerCellY + 1), out var val11) ? val11 : (float?)null;

        // Если есть все 4 точки, делаем полную интерполяцию
        if (z00.HasValue && z01.HasValue && z10.HasValue && z11.HasValue)
        {
            return MathUtil.BilinearInterpolation(z00.Value, z10.Value, z01.Value, z11.Value, localX, localY);
        }

        // Если есть только центральная точка, возвращаем её
        if (z00.HasValue) return z00.Value;

        // Если есть соседние точки, делаем частичную интерполяцию
        if (z01.HasValue && z10.HasValue)
        {
            //return MathUtil.Lerp(MathUtil.Lerp(z10.Value, z01.Value, localY), 0.5f);
            return MathUtil.Lerp(z10.Value, z01.Value, localY);
        }

        // Возвращаем первую найденную высоту
        return heights.Values.FirstOrDefault();
    }
    #endregion

    #region упрощенный метод

    // Method to track and store character coordinates
    public static void TrackAndStoreCharacterCoordinates1(Character character)
    {
        if (character == null)
            return;

        var position = character.Transform.World.Position;

        try
        {
            using var connection = MySQL.CreateConnection();
            // Check if record exists
            var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM height_maps WHERE zone_id = @zoneId", connection);
            checkCmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);

            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

            if (exists)
            {
                // Update existing record
                var updateCmd = new MySqlCommand("UPDATE height_maps SET x = @x, y = @y, z = @z, timestamp = NOW() WHERE zone_id = @zoneId", connection);
                updateCmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);
                updateCmd.Parameters.AddWithValue("@x", position.X);
                updateCmd.Parameters.AddWithValue("@y", position.Y);
                updateCmd.Parameters.AddWithValue("@z", position.Z);
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                // Insert new record
                var insertCmd = new MySqlCommand("INSERT INTO height_maps (zone_id, x, y, z, timestamp) VALUES (@zoneId, @x, @y, @z, NOW())", connection);
                insertCmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);
                insertCmd.Parameters.AddWithValue("@x", position.X);
                insertCmd.Parameters.AddWithValue("@y", position.Y);
                insertCmd.Parameters.AddWithValue("@z", position.Z);
                insertCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error storing character coordinates: {ex.Message}");
        }
    }
    public void StoreCharacterHeight1(Character character)
    {
        if (character == null) return;

        var pos = character.Transform.World.Position;

        try
        {
            using var connection = MySQL.CreateConnection();
            // Просто вставляем новую точку (без ячеек и агрегации)
            var cmd = new MySqlCommand(
                "INSERT INTO height_maps " +
                "(zone_id, x, y, z, timestamp) " +
                "VALUES (@zoneId, @x, @y, @z, NOW())",
                connection);
            cmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);
            cmd.Parameters.AddWithValue("@x", pos.X);
            cmd.Parameters.AddWithValue("@y", pos.Y);
            cmd.Parameters.AddWithValue("@z", pos.Z);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка сохранения высоты персонажа: {ex.Message}");
        }
    }

    // Упрощённый метод получения высоты
    internal float GetReferenceHeight1(float x, float y)
    {
        // 1. Проверяем стандартную высоту
        var height = WorldManager.Instance.GetHeight(Transform.ZoneId, x, y);
        if (height != 0) return height;

        // 2. Проверяем ближайшие точки в базе
        var dbHeight = GetHeightFromDatabase1(x, y);
        if (dbHeight != 0) return dbHeight;

        // 3. Проверяем персонажей в памяти
        return GetHeightFromNearbyCharacters1(x, y);
    }

    private float GetHeightFromDatabase1(float x, float y)
    {
        const float searchRadius = 5f; // Ищем в радиусе 5 метров
        const int maxPoints = 4; // Максимум 4 ближайшие точки

        try
        {
            using var connection = MySQL.CreateConnection();
            var cmd = new MySqlCommand(
                "SELECT x, y, z FROM height_maps " +
                "WHERE zone_id = @zoneId AND " +
                "POW(x - @x, 2) + POW(y - @y, 2) <= @radiusSq " +
                "ORDER BY (POW(x - @x, 2) + POW(y - @y, 2)) ASC " +
                "LIMIT @limit",
                connection);
            cmd.Parameters.AddWithValue("@zoneId", Transform.ZoneId);
            cmd.Parameters.AddWithValue("@x", x);
            cmd.Parameters.AddWithValue("@y", y);
            cmd.Parameters.AddWithValue("@radiusSq", searchRadius * searchRadius);
            cmd.Parameters.AddWithValue("@limit", maxPoints);

            var points = new List<Vector3>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    points.Add(new Vector3(
                        reader.GetFloat("x"),
                        reader.GetFloat("y"),
                        reader.GetFloat("z")
                    ));
                }
            }

            return CalculateHeightFromPoints1(x, y, points);
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка чтения высот из БД: {ex.Message}");
            return 0;
        }
    }

    private float GetHeightFromNearbyCharacters1(float x, float y)
    {
        const float searchRadius = 5f;
        var characters = WorldManager.GetAround<Character>(this, searchRadius)
            .Select(c => c.Transform.World.Position)
            .ToList();

        // Сохраняем найденные точки
        foreach (var pos in characters)
        {
            StoreCharacterPosition1(pos.X, pos.Y, pos.Z);
        }

        return CalculateHeightFromPoints1(x, y, characters);
    }

    private void StoreCharacterPosition1(float x, float y, float z)
    {
        try
        {
            using var connection = MySQL.CreateConnection();
            var cmd = new MySqlCommand(
                "INSERT INTO height_maps " +
                "(zone_id, x, y, z, timestamp) " +
                "VALUES (@zoneId, @x, @y, @z, NOW())",
                connection);
            cmd.Parameters.AddWithValue("@zoneId", Transform.ZoneId);
            cmd.Parameters.AddWithValue("@x", x);
            cmd.Parameters.AddWithValue("@y", y);
            cmd.Parameters.AddWithValue("@z", z);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка сохранения позиции: {ex.Message}");
        }
    }

    private float CalculateHeightFromPoints1(float x, float y, List<Vector3> points)
    {
        if (points.Count == 0) return 0;

        // Для 1 точки просто возвращаем её высоту
        if (points.Count == 1) return points[0].Z;

        // Для нескольких точек - взвешенное среднее по расстоянию
        float sum = 0;
        float totalWeight = 0;

        foreach (var point in points)
        {
            var dx = point.X - x;
            var dy = point.Y - y;
            var distanceSq = dx * dx + dy * dy;
            var weight = 1.0f / (distanceSq + 0.0001f); // Добавляем малую величину чтобы избежать деления на 0

            sum += point.Z * weight;
            totalWeight += weight;
        }

        return sum / totalWeight;
    }
    #endregion
}
