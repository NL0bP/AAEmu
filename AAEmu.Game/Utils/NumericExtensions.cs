using System;
using System.Numerics;

using Jitter2.LinearMath;

namespace AAEmu.Game.Utils;

public static class NumericExtensions
{
    public static double DegToRad(this double val)
    {
        return (Math.PI / 180f) * val;
    }

    public static float DegToRad(this float val)
    {
        return (MathF.PI / 180f) * val;
    }

    public static double RadToDeg(this double val)
    {
        return val / Math.PI * 180f;
    }

    public static float RadToDeg(this float val)
    {
        return val / MathF.PI * 180f;
    }

    /// <summary>
    /// Converts JVector to System.Numerics.Vector3 (XZY → XYZ)
    /// </summary>
    public static Vector3 ToVector(this JVector val)
    {
        return new Vector3(val.X, val.Z, val.Y);
    }

    /// <summary>
    /// Converts Vector3 to JVector (XYZ → XZY)
    /// </summary>
    public static JVector ToJVector(this Vector3 val)
    {
        return new JVector(val.X, val.Z, val.Y);
    }
    public static JVector ToJVectorFix(this JVector val)
    {
        return new JVector(val.X, 0f, val.Z);
    }
    public static JVector ToVectorFix(this Vector3 val)
    {
        return new JVector(val.X, 0f, val.Z);
    }

    /// <summary>
    /// Converts a world position into an X Y index for cells
    /// </summary>
    /// <param name="pos"></param>
    /// <returns>(cellX, cellY)</returns>
    public static (int, int) ToCellIndex(this Vector3 pos)
    {
        return ((int)Math.Floor(pos.X / 1024), (int)Math.Floor(pos.Y / 1024));
    }

    /// <summary>
    /// Converts a world position into an X Y index for chunks (paths)
    /// </summary>
    /// <param name="pos"></param>
    /// <returns>(pathsX, pathsY)</returns>
    public static (int, int) ToPathsIndex(this Vector3 pos)
    {
        return ((int)Math.Floor(pos.X / 256), (int)Math.Floor(pos.Y / 256));
    }
}
