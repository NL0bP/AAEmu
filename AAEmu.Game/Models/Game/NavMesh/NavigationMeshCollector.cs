using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using AAEmu.Commons.Utils.DB;

using NLog;

using TriangleNet.Geometry;
using TriangleNet.Meshing;

namespace AAEmu.Game.Models.Game.NavMesh
{
    /// <summary>
    /// Collects and manages navigation mesh triangles for pathfinding with advanced optimization
    /// </summary>
    public class NavigationMeshCollector : IDisposable
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<NavMeshTriangleKey, NavMeshTriangle> _rawCache = new();
        private readonly ConcurrentDictionary<NavMeshTriangleKey, NavMeshTriangle> _aggregatedCache = new();
        private readonly Timer _flushTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _compactionLock;
        private bool _disposed;

        // Statistics tracking
        private long _totalTrianglesProcessed;
        private long _totalCompactions;
        private DateTime _lastCompactionTime;

        // Optimization parameters
        private const float MergeGridSize = 10.0f;
        private const float MaxZDeviation = 0.1f;
        private const float GapFillTolerance = 0.3f;
        private const int MaxCacheSize = 1000000; // 1 million triangles limit

        public NavigationMeshCollector()
        {
            _flushTimer = new Timer(FlushCacheToDatabase, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            _cancellationTokenSource = new CancellationTokenSource();
            _compactionLock = new SemaphoreSlim(1, 1);
            _lastCompactionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets current cache statistics
        /// </summary>
        public (int RawCount, int AggregatedCount, long TotalProcessed, long TotalCompactions, DateTime LastCompaction) GetStatistics()
        {
            return (_rawCache.Count, _aggregatedCache.Count, _totalTrianglesProcessed, _totalCompactions, _lastCompactionTime);
        }

        /// <summary>
        /// Adds a triangle to the navigation mesh with cache size checks
        /// </summary>
        public bool AddSample(uint zoneId, Vector3 a, Vector3 b, Vector3 c)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NavigationMeshCollector));

            if (zoneId == 0)
                throw new ArgumentException("Zone ID cannot be 0", nameof(zoneId));
            if (a.X < 0f || a.Y < 0f || b.X < 0f || b.Y < 0f || c.X < 0f || c.Y < 0f)
                return false;

            // Check cache size limit
            if (_rawCache.Count >= MaxCacheSize)
            {
                Logger.Warn($"[NavMesh] Raw cache size limit ({MaxCacheSize}) reached, forcing compaction");
                Task.Run(async () => await ForceCompactionAsync());
                return false;
            }

            var key = new NavMeshTriangleKey(zoneId, a, b, c);
            var triangle = new NavMeshTriangle { ZoneId = zoneId, A = a, B = b, C = c };

            if (_rawCache.TryAdd(key, triangle))
            {
                Interlocked.Increment(ref _totalTrianglesProcessed);
                Logger.Debug($"[NavMesh] Added triangle {zoneId}, {a}, {b}, {c}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets optimized triangles within radius with bounds checking
        /// </summary>
        public List<NavMeshTriangle> GetTrianglesInRadius(uint zoneId, Vector3 center, float radius)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NavigationMeshCollector));
            if (radius <= 0)
                throw new ArgumentException("Radius must be positive", nameof(radius));

            var r2 = radius * radius;
            var center2d = new Vector2(center.X, center.Y);
            try
            {
                return _aggregatedCache.Values
                    .Where(tri => tri.ZoneId == zoneId)
                    .Where(tri =>
                    {
                        // Проверка: точка внутри треугольника (XY)
                        if (PointInTriangle2D(center2d,
                            new Vector2(tri.A.X, tri.A.Y),
                            new Vector2(tri.B.X, tri.B.Y),
                            new Vector2(tri.C.X, tri.C.Y)))
                            return true;
                        // Или хотя бы одна вершина в радиусе
                        if (Vector2.DistanceSquared(center2d, new Vector2(tri.A.X, tri.A.Y)) <= r2) return true;
                        if (Vector2.DistanceSquared(center2d, new Vector2(tri.B.X, tri.B.Y)) <= r2) return true;
                        if (Vector2.DistanceSquared(center2d, new Vector2(tri.C.X, tri.C.Y)) <= r2) return true;
                        return false;
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.Error($"[NavMesh] Error getting triangles in radius: {ex.Message}");
                return [];
            }
        }

        // Проверка попадания точки в треугольник на плоскости XY
        private static bool PointInTriangle2D(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Barycentric method
            var v0 = c - a;
            var v1 = b - a;
            var v2 = p - a;

            float dot00 = Vector2.Dot(v0, v0);
            float dot01 = Vector2.Dot(v0, v1);
            float dot02 = Vector2.Dot(v0, v2);
            float dot11 = Vector2.Dot(v1, v1);
            float dot12 = Vector2.Dot(v1, v2);

            float denom = dot00 * dot11 - dot01 * dot01;
            if (Math.Abs(denom) < 1e-10f)
                return false;
            float invDenom = 1f / denom;
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;
            return (u >= 0) && (v >= 0) && (u + v <= 1);
        }

        /// <summary>
        /// Gets raw triangles within radius with bounds checking
        /// </summary>
        public List<NavMeshTriangle> GetRawTrianglesInRadius(uint zoneId, Vector3 center, float radius)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NavigationMeshCollector));

            if (radius <= 0)
                throw new ArgumentException("Radius must be positive", nameof(radius));

            var r2 = radius * radius;
            try
            {
                return _rawCache.Values
                    .Where(tri => tri.ZoneId == zoneId)
                    .Where(tri => Vector3.DistanceSquared((tri.A + tri.B + tri.C) / 3f, center) <= r2)
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.Error($"[NavMesh] Error getting triangles in radius: {ex.Message}");
                return [];
            }
        }

        /// <summary>
        /// Forces an immediate compaction of the cache
        /// </summary>
        public async Task ForceCompactionAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NavigationMeshCollector));

            await _compactionLock.WaitAsync();
            try
            {
                CompactRawCacheExtended();
                _lastCompactionTime = DateTime.UtcNow;
                Interlocked.Increment(ref _totalCompactions);
            }
            finally
            {
                _compactionLock.Release();
            }
        }

        /// <summary>
        /// Advanced compaction algorithm with polygon merging
        /// </summary>
        private void CompactRawCacheExtended()
        {
            _aggregatedCache.Clear();
            var groupedByZone = _rawCache.Values.GroupBy(t => t.ZoneId).ToList();
            var aggLock = new object();

            Parallel.ForEach(groupedByZone, zoneGroup =>
            {
                var zoneId = zoneGroup.Key;
                var gridGroups = new Dictionary<(int gx, int gy), List<NavMeshTriangle>>();

                // Grid-based grouping
                foreach (var tri in zoneGroup)
                {
                    var center = (tri.A + tri.B + tri.C) / 3f;
                    var gx = (int)(center.X / MergeGridSize);
                    var gy = (int)(center.Y / MergeGridSize);
                    gridGroups.GetOrCreate((gx, gy)).Add(tri);
                }

                // Process each grid cell
                foreach (var group in gridGroups)
                {
                    var triangles = group.Value;

                    // Если один треугольник, добавляем его как есть
                    if (triangles.Count == 1)
                    {
                        var tri = triangles[0];
                        lock (aggLock)
                        {
                            var key = new NavMeshTriangleKey(zoneId, tri.A, tri.B, tri.C);
                            _aggregatedCache.TryAdd(key, tri);
                        }
                        continue;
                    }

                    // Для нескольких треугольников используем объединение в выпуклый многоугольник
                    if (triangles.Count >= 2)
                    {
                        // Flatness check
                        var avgZ = triangles.Average(t => (t.A.Z + t.B.Z + t.C.Z) / 3f);
                        if (!triangles.All(t => Math.Abs((t.A.Z + t.B.Z + t.C.Z) / 3f - avgZ) <= MaxZDeviation))
                        {
                            // Если проверка на плоскость не прошла, добавляем каждый треугольник отдельно
                            foreach (var tri in triangles)
                            {
                                lock (aggLock)
                                {
                                    var key = new NavMeshTriangleKey(zoneId, tri.A, tri.B, tri.C);
                                    _aggregatedCache.TryAdd(key, tri);
                                }
                            }
                            continue;
                        }

                        // Собираем все уникальные вершины
                        var points = new HashSet<Vector2>();
                        foreach (var tri in triangles)
                        {
                            points.Add(new Vector2(tri.A.X, tri.A.Y));
                            points.Add(new Vector2(tri.B.X, tri.B.Y));
                            points.Add(new Vector2(tri.C.X, tri.C.Y));
                        }
                        if (points.Count < 3)
                            continue;

                        // Строим выпуклый многоугольник (Convex Hull)
                        var hull = BuildConvexHull(points);
                        if (hull.Count < 3)
                            continue;

                        // Триангулируем выпуклый многоугольник (fan triangulation)
                        for (int i = 1; i < hull.Count - 1; i++)
                        {
                            var v1 = hull[0];
                            var v2 = hull[i];
                            var v3 = hull[i + 1];
                            var tri = new NavMeshTriangle
                            {
                                ZoneId = zoneId,
                                A = new Vector3(v1.X, v1.Y, avgZ),
                                B = new Vector3(v2.X, v2.Y, avgZ),
                                C = new Vector3(v3.X, v3.Y, avgZ)
                            };
                            lock (aggLock)
                            {
                                var key = new NavMeshTriangleKey(zoneId, tri.A, tri.B, tri.C);
                                _aggregatedCache.TryAdd(key, tri);
                            }
                        }
                    }
                }
            });

            FillGapsInGrid();
            Logger.Info($"[NavMesh] Compaction complete. Triangles: {_aggregatedCache.Count}");
        }

        // Graham scan convex hull for 2D points
        private static List<Vector2> BuildConvexHull(IEnumerable<Vector2> points)
        {
            var pts = points.Distinct().ToList();
            if (pts.Count < 3)
                return pts;
            pts.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            var lower = new List<Vector2>();
            foreach (var p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }
            var upper = new List<Vector2>();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                var p = pts[i];
                while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        private void FillGapsInGrid()
        {
            if (_aggregatedCache.Count == 0)
                return;

            try
            {
                // 1. Собираем все рёбра и считаем их количество
                var edgeCount = new Dictionary<(Vector2, Vector2), int>();
                foreach (var tri in _aggregatedCache.Values)
                {
                    var verts = new[] {
                        new Vector2(tri.A.X, tri.A.Y),
                        new Vector2(tri.B.X, tri.B.Y),
                        new Vector2(tri.C.X, tri.C.Y)
                    };
                    for (int i = 0; i < 3; i++)
                    {
                        var a = verts[i];
                        var b = verts[(i + 1) % 3];
                        // Упорядочиваем для уникальности
                        var edge = (a, b);
                        if (a.X > b.X || (a.X == b.X && a.Y > b.Y))
                            edge = (b, a);
                        if (!edgeCount.TryAdd(edge, 1))
                            edgeCount[edge]++;
                    }
                }

                // 2. Оставляем только внешние рёбра (те, что встречаются 1 раз)
                var borderEdges = edgeCount.Where(e => e.Value == 1).Select(e => e.Key).ToList();
                if (borderEdges.Count < 3)
                    return;

                // 3. Восстанавливаем замкнутую границу (упорядоченный список точек)
                var borderLoop = new List<Vector2>();
                var edgeMap = new Dictionary<Vector2, Vector2>();
                foreach (var (a, b) in borderEdges)
                    edgeMap[a] = b;
                // Стартуем с любой точки
                var start = borderEdges[0].Item1;
                borderLoop.Add(start);
                var current = start;
                while (true)
                {
                    if (!edgeMap.TryGetValue(current, out var next) || next == start)
                        break;
                    borderLoop.Add(next);
                    current = next;
                }
                if (borderLoop.Count < 3)
                    return;

                // 4. Строим Polygon только по внешнему контуру
                var polygon = new Polygon();
                var zoneId = _aggregatedCache.Values.First().ZoneId;
                var vertexDict = new Dictionary<(float X, float Y), Vertex>();
                foreach (var v in borderLoop)
                {
                    var vert = new Vertex(v.X, v.Y);
                    vertexDict[(v.X, v.Y)] = vert;
                    polygon.Add(vert);
                }
                for (int i = 0; i < borderLoop.Count; i++)
                {
                    var v1 = borderLoop[i];
                    var v2 = borderLoop[(i + 1) % borderLoop.Count];
                    polygon.Add(new Segment(vertexDict[(v1.X, v1.Y)], vertexDict[(v2.X, v2.Y)]));
                }

                // 5. Триангуляция внутренней области
                var options = new ConstraintOptions { ConformingDelaunay = true, Convex = false };
                var quality = new QualityOptions { MinimumAngle = 20.0, MaximumArea = MergeGridSize * MergeGridSize / 2 };
                var mesh = polygon.Triangulate(options, quality);

                // Среднее Z для всех исходных треугольников
                var avgZ = _aggregatedCache.Values.Average(t => (t.A.Z + t.B.Z + t.C.Z) / 3f);

                // 6. Добавляем новые треугольники
                var newTriangles = new List<NavMeshTriangle>();
                foreach (var triangle in mesh.Triangles)
                {
                    var v1 = triangle.GetVertex(0);
                    var v2 = triangle.GetVertex(1);
                    var v3 = triangle.GetVertex(2);
                    if (v1 == null || v2 == null || v3 == null)
                        continue;
                    if (IsDegenerate(v1, v2, v3))
                        continue;
                    var newTri = new NavMeshTriangle
                    {
                        ZoneId = zoneId,
                        A = new Vector3((float)v1.X, (float)v1.Y, avgZ),
                        B = new Vector3((float)v2.X, (float)v2.Y, avgZ),
                        C = new Vector3((float)v3.X, (float)v3.Y, avgZ)
                    };
                    if (IsValidTriangle(newTri))
                    {
                        var key = new NavMeshTriangleKey(zoneId, newTri.A, newTri.B, newTri.C);
                        _aggregatedCache.TryAdd(key, newTri);
                        newTriangles.Add(newTri);
                    }
                }
                Logger.Info($"[NavMesh] Gap filling complete. Added {newTriangles.Count} new triangles.");
            }
            catch (Exception ex)
            {
                Logger.Error($"[NavMesh] Error during gap filling: {ex.Message}");
            }
        }

        private static bool IsDegenerate(Vertex v1, Vertex v2, Vertex v3)
        {
            const double epsilon = 1e-10;
            
            // Check if points are too close together
            if (Math.Abs(v1.X - v2.X) < epsilon && Math.Abs(v1.Y - v2.Y) < epsilon) return true;
            if (Math.Abs(v2.X - v3.X) < epsilon && Math.Abs(v2.Y - v3.Y) < epsilon) return true;
            if (Math.Abs(v3.X - v1.X) < epsilon && Math.Abs(v3.Y - v1.Y) < epsilon) return true;

            // Check if points are collinear
            var area = Math.Abs((v2.X - v1.X) * (v3.Y - v1.Y) - (v3.X - v1.X) * (v2.Y - v1.Y));
            return area < epsilon;
        }

        private static bool IsValidTriangle(NavMeshTriangle tri)
        {
            const float minArea = 0.01f; // Minimum area threshold
            const float maxZDiff = 5.0f; // Maximum allowed Z difference

            // Calculate triangle area
            var v1 = tri.B - tri.A;
            var v2 = tri.C - tri.A;
            var area = Vector3.Cross(v1, v2).Length() / 2f;

            // Check area
            if (area < minArea)
                return false;

            // Check Z differences
            var zDiff1 = Math.Abs(tri.A.Z - tri.B.Z);
            var zDiff2 = Math.Abs(tri.B.Z - tri.C.Z);
            var zDiff3 = Math.Abs(tri.C.Z - tri.A.Z);

            return zDiff1 <= maxZDiff && zDiff2 <= maxZDiff && zDiff3 <= maxZDiff;
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
                    VALUES (@zone, @ax, @ay, @az, @bx, @by, @bz, @cx, @cy, @cz)
                    ON DUPLICATE KEY UPDATE
                        `ax` = VALUES(`ax`),
                        `ay` = VALUES(`ay`),
                        `az` = VALUES(`az`),
                        `bx` = VALUES(`bx`),
                        `by` = VALUES(`by`),
                        `bz` = VALUES(`bz`),
                        `cx` = VALUES(`cx`),
                        `cy` = VALUES(`cy`),
                        `cz` = VALUES(`cz`)";

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

        private async void FlushCacheToDatabase(object _)
        {
            if (_disposed) return;

            try
            {
                await _compactionLock.WaitAsync();
                try
                {
                    FlushRawToDatabase();
                    CompactRawCacheExtended();
                    _lastCompactionTime = DateTime.UtcNow;
                    Interlocked.Increment(ref _totalCompactions);
                }
                finally
                {
                    _compactionLock.Release();
                }

                if (_aggregatedCache.IsEmpty)
                    return;

                var count = 0;
                await using var connection = MySQL.CreateConnection();
                // Properly truncate table using a command
                await using (var truncateCmd = connection.CreateCommand())
                {
                    truncateCmd.CommandText = "TRUNCATE TABLE navigation_mesh";
                    await truncateCmd.ExecuteNonQueryAsync();
                }

                await using var transaction = await connection.BeginTransactionAsync();
                await using var command = connection.CreateCommand();

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

                await transaction.CommitAsync();
                Logger.Info($"[NavMesh] Flushed {count} triangles to navigation_mesh.");
            }
            catch (Exception ex)
            {
                Logger.Error($"[NavMesh] Error in flush operation: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads raw data from DB and runs compaction
        /// </summary>
        public void LoadRawFromDatabaseAndCompact()
        {
            _rawCache.Clear();

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

            Logger.Info($"[NavMesh] Loaded {count} raw triangles from DB");
            CompactRawCacheExtended();
            Logger.Info($"[NavMesh] Cache size after compaction: {_aggregatedCache.Count} triangles.");
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cancellationTokenSource.Cancel();
            _flushTimer?.Dispose();
            _cancellationTokenSource.Dispose();
            _compactionLock.Dispose();
            _rawCache.Clear();
            _aggregatedCache.Clear();
            _disposed = true;
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

    public static class DictionaryExtensions
    {
        public static List<T> GetOrCreate<T>(this Dictionary<(int, int), List<T>> dict, (int, int) key)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = [];
                dict[key] = list;
            }
            return list;
        }
    }
}
