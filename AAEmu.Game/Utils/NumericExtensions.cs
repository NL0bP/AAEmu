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
}
