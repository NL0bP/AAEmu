using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

using AAEmu.Commons.Utils.DB;

using NLog;

namespace AAEmu.Game.Models.Game.NavMesh
{
    /// <summary>
    /// Collects and manages navigation mesh triangles for pathfinding.
    /// </summary>
    public class NavigationMeshCollector : IDisposable
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<NavMeshTriangleKey, NavMeshTriangle> _rawCache = new();
        private readonly ConcurrentDictionary<NavMeshTriangleKey, NavMeshTriangle> _aggregatedCache = new();
        private readonly Timer _flushTimer;
        private bool _disposed;

        private const float MergeGridSize = 4.0f; // More aggressive aggregation: 4x4 meter grid
        private const float MaxZDeviation = 0.1f; // Allow more slope in terrain

        public NavigationMeshCollector()
        {
            _flushTimer = new Timer(FlushCacheToDatabase, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Adds a triangle to the navigation mesh.
        /// </summary>
        /// <param name="zoneId">The zone ID where the triangle is located.</param>
        /// <param name="a">First vertex of the triangle.</param>
        /// <param name="b">Second vertex of the triangle.</param>
        /// <param name="c">Third vertex of the triangle.</param>
        /// <exception cref="ArgumentException">Thrown when any of the vertex parameters are invalid.</exception>
        public void AddSample(uint zoneId, Vector3 a, Vector3 b, Vector3 c)
        {
            if (zoneId == 0)
                throw new ArgumentException("Zone ID cannot be 0", nameof(zoneId));
            if (a.X < 0f || a.Y < 0f ||b.X < 0f || b.Y < 0f ||c.X < 0f || c.Y < 0f)
            {
                Logger.Warn($"[NavMesh] Not add raw triangles {zoneId}, {a}, {b}, {c}");
                return;
            }

            var key = new NavMeshTriangleKey(zoneId, a, b, c);
            var triangle = new NavMeshTriangle
            {
                ZoneId = zoneId,
                A = a,
                B = b,
                C = c
            };

            Logger.Info($"[NavMesh] Add raw triangles {zoneId}, {a}, {b}, {c}");

            _rawCache.TryAdd(key, triangle);
        }

        /// <summary>
        /// Gets all triangles within a specified radius of a center point.
        /// </summary>
        /// <param name="zoneId">The zone ID to search in.</param>
        /// <param name="center">The center point of the search radius.</param>
        /// <param name="radius">The radius to search within.</param>
        /// <returns>A list of triangles within the specified radius.</returns>
        public List<NavMeshTriangle> GetTrianglesInRadius(uint zoneId, Vector3 center, float radius)
        {
            if (radius <= 0)
                throw new ArgumentException("Radius must be positive", nameof(radius));

            var result = new List<NavMeshTriangle>();
            var r2 = radius * radius;

            foreach (var tri in _aggregatedCache.Values)
            {
                if (tri.ZoneId != zoneId)
                    continue;

                var triCenter = (tri.A + tri.B + tri.C) / 3f;
                triCenter = triCenter with { Z = 0 };
                var distanceSquared = Vector3.DistanceSquared(triCenter, center);

                // Early exit if the triangle is too far away
                if (distanceSquared <= r2)
                {
                    result.Add(tri);
                }
            }
            return result;
        }

        public List<NavMeshTriangle> GetRawTrianglesInRadius(uint zoneId, Vector3 center, float radius)
        {
            if (radius <= 0)
                throw new ArgumentException("Radius must be positive", nameof(radius));

            var result = new List<NavMeshTriangle>();
            var r2 = radius * radius;

            foreach (var tri in _rawCache.Values)
            {
                if (tri.ZoneId != zoneId)
                    continue;

                var triCenter = (tri.A + tri.B + tri.C) / 3f;
                var distanceSquared = Vector3.DistanceSquared(triCenter, center);

                // Early exit if the triangle is too far away
                if (distanceSquared <= r2)
                {
                    result.Add(tri);
                }
            }
            return result;
        }

        public void LoadRawFromDatabaseAndCompact()
        {
            _rawCache.Clear();
            _aggregatedCache.Clear();

            using var connection = MySQL.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT `zone_id`, `ax`, `ay`, `az`, `bx`, `by`, `bz`, `cx`, `cy`, `cz` FROM navigation_mesh_raw";

            using var reader = command.ExecuteReader();
            var count = 0;

            while (reader.Read())
            {
                var zoneId = reader.GetUInt32("zone_id");
                var a = new Vector3(reader.GetFloat("ax"), reader.GetFloat("ay"), reader.GetFloat("az"));
                var b = new Vector3(reader.GetFloat("bx"), reader.GetFloat("by"), reader.GetFloat("bz"));
                var c = new Vector3(reader.GetFloat("cx"), reader.GetFloat("cy"), reader.GetFloat("cz"));

                var key = new NavMeshTriangleKey(zoneId, a, b, c);
                _rawCache.TryAdd(key, new NavMeshTriangle { ZoneId = zoneId, A = a, B = b, C = c });
                count++;
            }

            Logger.Info($"[NavMesh] Loaded {count} raw triangles from DB. Starting compaction.");
            CompactRawCache();
        }

        private void CompactRawCache()
        {
            _aggregatedCache.Clear();
            var groupedByZone = _rawCache.Values.GroupBy(t => t.ZoneId).ToList();
            var mergeGridSize = MergeGridSize;
            var maxZDeviation = MaxZDeviation;
            var totalMerged = 0;
            var totalGroups = 0;
            var aggLock = new object();

            System.Threading.Tasks.Parallel.ForEach(groupedByZone, zoneGroup =>
            {
                var zoneId = zoneGroup.Key;
                var grouped = new Dictionary<(int gx, int gy), List<NavMeshTriangle>>();

                foreach (var tri in zoneGroup)
                {
                    var center = (tri.A + tri.B + tri.C) / 3f;
                    var gx = (int)(center.X / mergeGridSize);
                    var gy = (int)(center.Y / mergeGridSize);
                    var key = (gx, gy);
                    if (!grouped.TryGetValue(key, out var list))
                    {
                        list = [];
                        grouped[key] = list;
                    }
                    list.Add(tri);
                }

                var localMerged = 0;
                var localGroups = 0;
                var toAdd = new List<(NavMeshTriangleKey, NavMeshTriangle)>();

                foreach (var group in grouped)
                {
                    var triangles = group.Value;
                    if (triangles.Count < 2)
                        continue;

                    var avgZ = triangles.Average(t => ((t.A.Z + t.B.Z + t.C.Z) / 3f));
                    var flat = triangles.All(t =>
                    {
                        var cz = (t.A.Z + t.B.Z + t.C.Z) / 3f;
                        return Math.Abs(cz - avgZ) <= maxZDeviation;
                    });

                    if (!flat) continue;

                    var bounds = triangles.SelectMany(t => new[] { t.A, t.B, t.C })
                        .Aggregate(new
                        {
                            Min = new Vector2(float.MaxValue, float.MaxValue),
                            Max = new Vector2(float.MinValue, float.MinValue)
                        },
                            (acc, v) => new
                            {
                                Min = new Vector2(MathF.Min(acc.Min.X, v.X), MathF.Min(acc.Min.Y, v.Y)),
                                Max = new Vector2(MathF.Max(acc.Max.X, v.X), MathF.Max(acc.Max.Y, v.Y))
                            });

                    var z = avgZ;
                    var min = bounds.Min;
                    var max = bounds.Max;

                    var a = new Vector3(min.X, min.Y, z);
                    var b = new Vector3(max.X, min.Y, z);
                    var c = new Vector3(max.X, max.Y, z);
                    var d = new Vector3(min.X, max.Y, z);

                    var merged1 = new NavMeshTriangle { ZoneId = zoneId, A = a, B = b, C = c };
                    var merged2 = new NavMeshTriangle { ZoneId = zoneId, A = a, B = c, C = d };

                    toAdd.Add((new NavMeshTriangleKey(zoneId, merged1.A, merged1.B, merged1.C), merged1));
                    toAdd.Add((new NavMeshTriangleKey(zoneId, merged2.A, merged2.B, merged2.C), merged2));

                    localMerged += triangles.Count - 2;
                    localGroups++;
                }

                lock (aggLock)
                {
                    foreach (var (k, t) in toAdd)
                        _aggregatedCache.TryAdd(k, t);
                    totalMerged += localMerged;
                    totalGroups += localGroups;
                }
            });

            Logger.Info($"[NavMesh] CompactRawCache completed: aggregated {_aggregatedCache.Count} triangles, merged {totalMerged} in {totalGroups} groups.");
        }

        private void FlushRawToDatabase()
        {
            if (_rawCache.IsEmpty)
                return;

            var count = 0;
            try
            {
                using var connection = MySQL.CreateConnection();
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();

                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO navigation_mesh_raw
                    (`zone_id`, `ax`, `ay`, `az`, `bx`, `by`, `bz`, `cx`, `cy`, `cz`)
                    VALUES (@zone, @ax, @ay, @az, @bx, @by, @bz, @cx, @cy, @cz)";

                foreach (var triangle in _rawCache.Values)
                {
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@zone", triangle.ZoneId);
                    command.Parameters.AddWithValue("@ax", triangle.A.X);
                    command.Parameters.AddWithValue("@ay", triangle.A.Y);
                    command.Parameters.AddWithValue("@az", triangle.A.Z);
                    command.Parameters.AddWithValue("@bx", triangle.B.X);
                    command.Parameters.AddWithValue("@by", triangle.B.Y);
                    command.Parameters.AddWithValue("@bz", triangle.B.Z);
                    command.Parameters.AddWithValue("@cx", triangle.C.X);
                    command.Parameters.AddWithValue("@cy", triangle.C.Y);
                    command.Parameters.AddWithValue("@cz", triangle.C.Z);

                    command.ExecuteNonQuery();
                    count++;
                }

                transaction.Commit();
                Logger.Info($"[NavMesh] Flushed {count} triangles to navigation_mesh_raw.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error flushing raw navigation mesh to database: {ex.Message}");
            }
        }

        private void FlushCacheToDatabase(object _)
        {
            try
            {
                FlushRawToDatabase(); // сохраняем необработанные
                CompactRawCache(); // уплотняем

                if (_aggregatedCache.IsEmpty)
                    return;

                var count = 0;
                using var connection = MySQL.CreateConnection();
                // Properly truncate table using a command
                using (var truncateCmd = connection.CreateCommand())
                {
                    truncateCmd.CommandText = "TRUNCATE TABLE navigation_mesh";
                    truncateCmd.ExecuteNonQuery();
                }

                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();

                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO navigation_mesh
                    (`zone_id`, `ax`, `ay`, `az`, `bx`, `by`, `bz`, `cx`, `cy`, `cz`)
                    VALUES (@zone, @ax, @ay, @az, @bx, @by, @bz, @cx, @cy, @cz)";

                foreach (var triangle in _aggregatedCache.Values)
                {
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@zone", triangle.ZoneId);
                    command.Parameters.AddWithValue("@ax", triangle.A.X);
                    command.Parameters.AddWithValue("@ay", triangle.A.Y);
                    command.Parameters.AddWithValue("@az", triangle.A.Z);
                    command.Parameters.AddWithValue("@bx", triangle.B.X);
                    command.Parameters.AddWithValue("@by", triangle.B.Y);
                    command.Parameters.AddWithValue("@bz", triangle.B.Z);
                    command.Parameters.AddWithValue("@cx", triangle.C.X);
                    command.Parameters.AddWithValue("@cy", triangle.C.Y);
                    command.Parameters.AddWithValue("@cz", triangle.C.Z);

                    command.ExecuteNonQuery();
                    count++;
                }

                transaction.Commit();
                Logger.Info($"[NavMesh] Flushed {count} triangles to navigation_mesh.");
                //_aggregatedCache.Clear();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error flushing navigation mesh cache to database: {ex.Message}");
            }
        }

        public void LoadFromDatabase()
        {
            _rawCache.Clear();
            _aggregatedCache.Clear();

            var count = 0;

            try
            {
                using var connection = MySQL.CreateConnection();
                using var command = connection.CreateCommand();

                command.CommandText = "SELECT `zone_id`, `ax`, `ay`, `az`, `bx`, `by`, `bz`, `cx`, `cy`, `cz` FROM navigation_mesh";
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    try
                    {
                        var zoneId = reader.GetUInt32("zone_id");
                        var a = new Vector3(
                            reader.GetFloat("ax"),
                            reader.GetFloat("ay"),
                            reader.GetFloat("az")
                        );
                        var b = new Vector3(
                            reader.GetFloat("bx"),
                            reader.GetFloat("by"),
                            reader.GetFloat("bz")
                        );
                        var c = new Vector3(
                            reader.GetFloat("cx"),
                            reader.GetFloat("cy"),
                            reader.GetFloat("cz")
                        );

                        var key = new NavMeshTriangleKey(zoneId, a, b, c);
                        var triangle = new NavMeshTriangle
                        {
                            ZoneId = zoneId,
                            A = a,
                            B = b,
                            C = c
                        };

                        if (_aggregatedCache.TryAdd(key, triangle))
                            count++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error loading navigation mesh triangle: {ex.Message}");
                        // Continue loading other triangles even if one fails
                    }
                }

                Logger.Info($"[NavMesh] Loaded {count} triangles from database before compaction.");
                CompactCache();
                Logger.Info($"[NavMesh] Cache size after compaction: {_aggregatedCache.Count} triangles.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading navigation mesh from database: {ex.Message}");
                throw; // Rethrow as this is a critical error
            }
        }

        private void CompactCache()
        {
            var totalMerged = 0;
            var totalGroups = 0;
            var cache = _rawCache;
            var mergeGridSize = MergeGridSize;
            var maxZDeviation = MaxZDeviation;

            var zoneGroups = cache.Values.GroupBy(t => t.ZoneId).ToList();
            var cacheLock = new object();

            System.Threading.Tasks.Parallel.ForEach(zoneGroups, zoneGroup =>
            {
                var zoneId = zoneGroup.Key;
                var grouped = new Dictionary<(int gx, int gy), List<NavMeshTriangle>>();

                // Группируем по координатам сетки
                foreach (var tri in zoneGroup)
                {
                    var center = GetTriangleCenter(tri);
                    var gx = (int)(center.X / mergeGridSize);
                    var gy = (int)(center.Y / mergeGridSize);
                    var key = (gx, gy);
                    if (!grouped.TryGetValue(key, out var list))
                    {
                        list = [];
                        grouped[key] = list;
                    }
                    list.Add(tri);
                }

                // Обрабатываем группы
                var localMerged = 0;
                var localGroups = 0;
                var toAdd = new List<(NavMeshTriangleKey, NavMeshTriangle)>();
                var toRemove = new List<NavMeshTriangleKey>();

                foreach (var group in grouped)
                {
                    var triangles = group.Value;
                    if (triangles.Count < 2)
                        continue;

                    var avgZ = triangles.Average(t => GetTriangleCenter(t).Z);
                    var flat = triangles.All(t => Math.Abs(GetTriangleCenter(t).Z - avgZ) <= maxZDeviation);
                    if (!flat)
                        continue;

                    // Находим границы
                    var allVerts = triangles.SelectMany(t => new[] { t.A, t.B, t.C });
                    var minX = allVerts.Min(v => v.X);
                    var minY = allVerts.Min(v => v.Y);
                    var maxX = allVerts.Max(v => v.X);
                    var maxY = allVerts.Max(v => v.Y);
                    var z = avgZ;

                    var a = new Vector3(minX, minY, z);
                    var b = new Vector3(maxX, minY, z);
                    var c = new Vector3(maxX, maxY, z);
                    var d = new Vector3(minX, maxY, z);

                    var merged1 = new NavMeshTriangle { ZoneId = zoneId, A = a, B = b, C = c };
                    var merged2 = new NavMeshTriangle { ZoneId = zoneId, A = a, B = c, C = d };

                    // Сначала собираем ключи для удаления
                    var keysToRemove = triangles.Select(t => new NavMeshTriangleKey(t.ZoneId, t.A, t.B, t.C)).ToList();
                    toRemove.AddRange(keysToRemove);
                    toAdd.Add((new NavMeshTriangleKey(zoneId, merged1.A, merged1.B, merged1.C), merged1));
                    toAdd.Add((new NavMeshTriangleKey(zoneId, merged2.A, merged2.B, merged2.C), merged2));

                    localMerged += triangles.Count - 2;
                    localGroups++;
                }

                // Применяем изменения к _cache потокобезопасно
                lock (cacheLock)
                {
                    foreach (var k in toRemove)
                        cache.TryRemove(k, out _);
                    foreach (var (k, t) in toAdd)
                        cache.TryAdd(k, t);
                    totalMerged += localMerged;
                    totalGroups += localGroups;
                }
            });

            if (totalMerged > 0)
            {
                Logger.Info($"[NavMesh] CompactCache: merged {totalMerged} triangles in {totalGroups} groups using grid {MergeGridSize}x{MergeGridSize} and Z tolerance ±{MaxZDeviation}");
            }

            static Vector3 GetTriangleCenter(NavMeshTriangle t) => (t.A + t.B + t.C) / 3f;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _flushTimer?.Dispose();
                _rawCache.Clear();
                _aggregatedCache.Clear();
            }

            _disposed = true;
        }

        public string ExportAggregatedToJson()
        {
            var all = _aggregatedCache.Values.ToList();
            return System.Text.Json.JsonSerializer.Serialize(all, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }

    public class NavMeshTriangle
    {
        public uint ZoneId { get; set; }
        public Vector3 A { get; set; }
        public Vector3 B { get; set; }
        public Vector3 C { get; set; }
    }

    public readonly struct NavMeshTriangleKey : IEquatable<NavMeshTriangleKey>
    {
        private readonly uint _zoneId;
        private readonly Vector3 _a;
        private readonly Vector3 _b;
        private readonly Vector3 _c;

        public NavMeshTriangleKey(uint zoneId, Vector3 a, Vector3 b, Vector3 c)
        {
            _zoneId = zoneId;
            _a = a;
            _b = b;
            _c = c;
        }

        public bool Equals(NavMeshTriangleKey other)
        {
            return _zoneId == other._zoneId && CompareVertices(_a, _b, _c, other._a, other._b, other._c);
        }

        public override bool Equals(object? obj)
        {
            return obj is NavMeshTriangleKey key && Equals(key);
        }

        public override int GetHashCode()
        {
            var points = new[] { _a, _b, _c };
            Array.Sort(points, (v1, v2) => v1.X.CompareTo(v2.X));
            unchecked
            {
                var hash = 17;
                foreach (var p in points)
                {
                    hash = hash * 31 + p.GetHashCode();
                }
                hash = hash * 31 + (int)_zoneId;
                return hash;
            }
        }

        private static bool CompareVertices(Vector3 a1, Vector3 b1, Vector3 c1, Vector3 a2, Vector3 b2, Vector3 c2)
        {
            var list1 = new List<Vector3> { a1, b1, c1 };
            var list2 = new List<Vector3> { a2, b2, c2 };

            list1.Sort((v1, v2) => v1.X.CompareTo(v2.X));
            list2.Sort((v1, v2) => v1.X.CompareTo(v2.X));

            for (var i = 0; i < 3; i++)
            {
                if (!list1[i].Equals(list2[i]))
                    return false;
            }
            return true;
        }
    }
}
