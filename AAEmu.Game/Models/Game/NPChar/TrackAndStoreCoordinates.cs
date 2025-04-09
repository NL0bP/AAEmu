using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Numerics;

using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;

using MySql.Data.MySqlClient;

namespace AAEmu.Game.Models.Game.NPChar;

public partial class Npc
{
    // Method to track and store character coordinates
    public void TrackAndStoreCharacterCoordinates(Character character)
    {
        if (character == null)
            return;

        var position = character.Transform.World.Position;

        try
        {
            using var connection = MySQL.CreateConnection();
            // Check if record exists
            var checkCmd = new MySqlCommand(
                "SELECT COUNT(*) FROM height_maps WHERE character_id = @characterId AND zone_id = @zoneId",
                connection);
            checkCmd.Parameters.AddWithValue("@characterId", character.Id);
            checkCmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);

            var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

            if (exists)
            {
                // Update existing record
                var updateCmd = new MySqlCommand(
                    "UPDATE height_maps SET x = @x, y = @y, z = @z, last_update = NOW() " +
                    "WHERE character_id = @characterId AND zone_id = @zoneId",
                    connection);
                updateCmd.Parameters.AddWithValue("@x", position.X);
                updateCmd.Parameters.AddWithValue("@y", position.Y);
                updateCmd.Parameters.AddWithValue("@z", position.Z);
                updateCmd.Parameters.AddWithValue("@characterId", character.Id);
                updateCmd.Parameters.AddWithValue("@zoneId", character.Transform.ZoneId);
                updateCmd.ExecuteNonQuery();
            }
            else
            {
                // Insert new record
                var insertCmd = new MySqlCommand(
                    "INSERT INTO height_maps (character_id, zone_id, x, y, z, last_update) " +
                    "VALUES (@characterId, @zoneId, @x, @y, @z, NOW())",
                    connection);
                insertCmd.Parameters.AddWithValue("@characterId", character.Id);
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

    private float Lerp(float start, float end, float t)
    {
        return start + (end - start) * t;
    }

    //private float GetReferenceHeight(float x, float y)
    //{
    //    // Сначала пытаемся получить высоту ландшафта
    //    var landscapeHeight = WorldManager.Instance.GetHeight(Transform.ZoneId, x, y);

    //    // Если высота ландшафта равна 0, ищем ближайшего персонажа
    //    if (landscapeHeight == 0)
    //    {
    //        const float searchRadius = 5f;
    //        var nearbyCharacters = WorldManager.GetAround<Character>(this, searchRadius);

    //        if (nearbyCharacters.Any())
    //        {
    //            // Выбираем ближайшего персонажа по горизонтали
    //            var nearest = nearbyCharacters.OrderBy(c => Vector3.DistanceSquared(new Vector3(x, y, 0), c.Transform.World.Position with { Z = 0 })).First();
    //            return nearest.Transform.World.Position.Z;
    //        }
    //    }

    //    // Возвращаем высоту ландшафта, если она не равна 0, или 0, если ничего не найдено
    //    return landscapeHeight;
    //}

    // Modified GetReferenceHeight method
    //private float GetReferenceHeight(float x, float y)
    //{
    //    // First try to get landscape height
    //    var landscapeHeight = WorldManager.Instance.GetHeight(Transform.ZoneId, x, y);

    //    // If landscape height is 0, try to get height from database
    //    if (landscapeHeight == 0)
    //    {
    //        try
    //        {
    //            using var connection = MySQL.CreateConnection();
    //            // Find nearest character position in database within 5m radius
    //            var cmd = new MySqlCommand(
    //                "SELECT x, y, z FROM height_maps " +
    //                "WHERE zone_id = @zoneId AND " +
    //                "POW(x - @x, 2) + POW(y - @y, 2) <= POW(5, 2) " + // 5m radius squared
    //                "ORDER BY (POW(x - @x, 2) + POW(y - @y, 2)) ASC " + // Order by distance
    //                "LIMIT 1",
    //                connection);
    //            cmd.Parameters.AddWithValue("@zoneId", Transform.ZoneId);
    //            cmd.Parameters.AddWithValue("@x", x);
    //            cmd.Parameters.AddWithValue("@y", y);

    //            using var reader = cmd.ExecuteReader();
    //            if (reader.Read())
    //            {
    //                var storedX = reader.GetFloat("x");
    //                var storedY = reader.GetFloat("y");
    //                var storedZ = reader.GetFloat("z");

    //                // Verify the position is reasonably close
    //                var distance = MathF.Sqrt(MathF.Pow(storedX - x, 2) + MathF.Pow(storedY - y, 2));
    //                if (distance <= 5f) // within 5m
    //                {
    //                    return storedZ;
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Logger.Error($"Error retrieving character coordinates: {ex.Message}");
    //        }

    //        // If no database record found, fall back to checking nearby characters in memory
    //        const float searchRadius = 5f;
    //        var nearbyCharacters = WorldManager.GetAround<Character>(this, searchRadius);

    //        if (nearbyCharacters.Any())
    //        {
    //            // Track coordinates of nearby characters
    //            foreach (var character in nearbyCharacters)
    //            {
    //                TrackAndStoreCharacterCoordinates(character);
    //            }

    //            // Get height from nearest character
    //            var nearest = nearbyCharacters
    //                .OrderBy(c => Vector3.DistanceSquared(new Vector3(x, y, 0),
    //                    c.Transform.World.Position with { Z = 0 }))
    //                .First();
    //            return nearest.Transform.World.Position.Z;
    //        }
    //    }

    //    // Return landscape height if it's not 0, or 0 if nothing found
    //    return landscapeHeight;
    //}


    // Метод для сохранения координат персонажа
    public void StoreCharacterHeight(Character character)
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
    private float GetReferenceHeight(float x, float y)
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

    private float GetHeightFromDatabase(float x, float y)
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

            return CalculateHeightFromPoints(x, y, points);
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка чтения высот из БД: {ex.Message}");
            return 0;
        }
    }

    private float GetHeightFromNearbyCharacters(float x, float y)
    {
        const float searchRadius = 5f;
        var characters = WorldManager.GetAround<Character>(this, searchRadius)
            .Select(c => c.Transform.World.Position)
            .ToList();

        // Сохраняем найденные точки
        foreach (var pos in characters)
        {
            StoreCharacterPosition(pos.X, pos.Y, pos.Z);
        }

        return CalculateHeightFromPoints(x, y, characters);
    }

    private void StoreCharacterPosition(float x, float y, float z)
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

    private float CalculateHeightFromPoints(float x, float y, List<Vector3> points)
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
}
