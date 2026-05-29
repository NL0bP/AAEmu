using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using AAEmu.Commons.Utils;
using Newtonsoft.Json;

namespace AAEmu.Game.Models.Game.World;

public class WaterBodies
{
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public float OceanLevel { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
    public List<WaterBodyArea> Areas { get; set; } = [];

    [JsonIgnore] public object _lock = new();

    /// <summary>
    /// Returns a stable snapshot of <see cref="Areas"/> for debug/commands without exposing the internal lock.
    /// </summary>
    public List<WaterBodyArea> GetAreasSnapshot()
    {
        lock (_lock)
            return [.. Areas];
    }

    /// <summary>
    /// Checks if a given point falls with a body of water
    /// </summary>
    /// <param name="point">Position to check</param>
    /// <param name="flowDirection">The direction the water is flowing if it has a flow</param>
    /// <returns></returns>
    public bool IsWater(Vector3 point, out Vector3 flowDirection)
    {
        flowDirection = Vector3.Zero;
        
        if (point.Z <= OceanLevel)
            return true;

        lock (_lock)
        {
            // TODO: take the top-most water area in case of overlaps
            foreach (var area in Areas)
            {
                if (area.GetSurface(point, out var surfacePoint, out flowDirection) &&
                    (point.Z <= surfacePoint.Z) &&
                    (point.Z >= surfacePoint.Z - area.Depth))
                    return true;
            }

        }
        flowDirection = Vector3.Zero;
        return false;
    }

    /// <summary>
    /// Gets the surface height of a point within a body of water
    /// </summary>
    /// <param name="point">Position to check</param>
    /// <param name="flowDirection">The direction the water is flowing if it has a flow</param>
    /// <returns>Returns the surface height of the water body, with a minimum of ocean level</returns>
    public float GetWaterSurface(Vector3 point, out Vector3 flowDirection)
    {
        flowDirection = Vector3.Zero;
        
        if (point.Z <= OceanLevel)
            return OceanLevel;

        lock (_lock)
        {
            foreach (var area in Areas)
                if (area.GetSurface(point, out var surfacePoint, out flowDirection))
                    return surfacePoint.Z;
        }

        return OceanLevel;
    }

    public static bool Save(string fileName, WaterBodies waterBodies)
    {
        try
        {
            lock (waterBodies._lock)
            {
                var jsonString = JsonConvert.SerializeObject(waterBodies, Formatting.Indented);
                File.WriteAllText(fileName, jsonString);
            }
        }
        catch
        {
            return false;
            // Ignore
        }
        return true;
    }

    public static bool Load(string fileName, out WaterBodies waterBodies)
    {
        waterBodies = null;
        try
        {
            var jsonString = File.ReadAllText(fileName);
            if (!JsonHelper.TryDeserializeObject<WaterBodies>(jsonString, out var newData, out _))
                return false;
            
            foreach (var area in newData.Areas)
            {
                // In effort to removing Height in favor of Depth, recalculate Z
                if (area.Height > 0f)
                {
                    area.Depth = area.Height;
                    area.Height = 0f;
                    for (var i = 0; i <= area.Points.Count - 1; i++)
                    {
                        var p = area.Points[i];
                        area.Points[i] = new Vector3(p.X, p.Y, p.Z + area.Depth);
                    }
                }
                
                // To fix issues with endpoints of rivers looping back to the start, remove the obsolete point from the data.
                // This doesn't really give an issue with water itself due to how it's handled, but is wrong nonetheless.
                if ((area.AreaType == WaterBodyAreaType.LineArray) && (area.Points.Count > 2) && (area.Points[^1].Equals(area.Points[0])))
                    area.Points.RemoveAt(area.Points.Count-1);
                
                area.UpdateBounds();
            }

            waterBodies = newData;
        }
        catch
        {
            return false;
            // Ignore
        }
        return true;
    }

    public uint GetNewId()
    {
        var res = 1000000u;

        foreach (var area in Areas)
        {
            if (area.Id >= res)
                res = area.Id + 1;
        }

        return res;
    }

    internal static bool TryGetRiverLikePolygonMetrics(List<Vector3> points, out float lengthMeters,
        out float maxHalfWidthMeters, out float meanFullWidthMeters, out float areaSqm, out float aspect,
        out Vector2 principalAxisUnit)
    {
        lengthMeters = 0f;
        maxHalfWidthMeters = 0f;
        meanFullWidthMeters = 0f;
        areaSqm = 0f;
        aspect = 0f;
        principalAxisUnit = Vector2.UnitX;
        if (points is null || points.Count < 3)
            return false;

        // Ensure closed ring for metrics that need edges.
        var n = points.Count;
        if (n >= 2 && points[0] != points[^1])
        {
            points = new List<Vector3>(points);
            points.Add(points[0]);
            n = points.Count;
        }

        // Many callers already pass closed rings (last == first). Avoid double-counting the closing vertex in PCA metrics.
        var pcaCount = n >= 2 && points[0] == points[^1] ? n - 1 : n;

        // Area (shoelace) in XY.
        double sum = 0d;
        for (var i = 0; i + 1 < n; i++)
            sum += (double)points[i].X * points[i + 1].Y - (double)points[i + 1].X * points[i].Y;
        areaSqm = (float)Math.Abs(sum * 0.5d);
        if (areaSqm <= 1f)
            return false;

        // PCA axis in XY from covariance.
        var mean = GetMeanXY(points, pcaCount);
        GetCovarianceXY(points, pcaCount, mean, out var cxx, out var cxy, out var cyy);
        if (!float.IsFinite(cxx) || !float.IsFinite(cxy) || !float.IsFinite(cyy))
            return false;

        // principal eigenvector for 2x2 covariance
        var trace = cxx + cyy;
        var det = cxx * cyy - cxy * cxy;
        var disc = trace * trace - 4f * det;
        if (disc < 0f)
            disc = 0f;
        var s = MathF.Sqrt(disc);
        var lambda1 = 0.5f * (trace + s);
        var vx = cxy;
        var vy = lambda1 - cxx;
        if (MathF.Abs(vx) + MathF.Abs(vy) < 1e-12f)
        {
            vx = 1f;
            vy = 0f;
        }
        var v = Vector2.Normalize(new Vector2(vx, vy));
        principalAxisUnit = v;

        // Project points onto axis and its perpendicular to estimate length/width.
        var perp = new Vector2(-v.Y, v.X);
        var minT = float.PositiveInfinity;
        var maxT = float.NegativeInfinity;
        var minP = float.PositiveInfinity;
        var maxP = float.NegativeInfinity;
        for (var i = 0; i < points.Count; i++)
        {
            var d = new Vector2(points[i].X - mean.X, points[i].Y - mean.Y);
            var t = Vector2.Dot(d, v);
            var p = Vector2.Dot(d, perp);
            if (t < minT) minT = t;
            if (t > maxT) maxT = t;
            if (p < minP) minP = p;
            if (p > maxP) maxP = p;
        }

        lengthMeters = Math.Max(0f, maxT - minT);
        var fullWidth = Math.Max(0f, maxP - minP);
        maxHalfWidthMeters = fullWidth * 0.5f;
        meanFullWidthMeters = areaSqm / Math.Max(1e-3f, lengthMeters);
        aspect = lengthMeters / Math.Max(1e-3f, meanFullWidthMeters);
        return true;
    }

    private static Vector2 GetMeanXY(List<Vector3> points, int n)
    {
        double sx = 0d;
        double sy = 0d;
        for (var i = 0; i < n; i++)
        {
            sx += points[i].X;
            sy += points[i].Y;
        }

        var inv = 1f / n;
        return new Vector2((float)(sx * inv), (float)(sy * inv));
    }

    private static void GetCovarianceXY(List<Vector3> points, int n, Vector2 mean, out float cxx, out float cxy, out float cyy)
    {
        double sxx = 0d;
        double sxy = 0d;
        double syy = 0d;
        for (var i = 0; i < n; i++)
        {
            var dx = points[i].X - mean.X;
            var dy = points[i].Y - mean.Y;
            sxx += dx * dx;
            sxy += dx * dy;
            syy += dy * dy;
        }

        var inv = 1f / Math.Max(1, n);
        cxx = (float)(sxx * inv);
        cxy = (float)(sxy * inv);
        cyy = (float)(syy * inv);
    }
}
