using System;
using System.Linq;
using Jitter2.Collision;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.LinearMath;

namespace AAEmu.Game.Physics;

public class Heightmap(float[,] heights)
{
    public int Width => heights.GetLength(0);
    public int Height => heights.GetLength(1);
    public float MinHeight { get; } = heights.Cast<float>().Min();
    public float MaxHeight { get; } = heights.Cast<float>().Max();

    public float GetHeight(int x, int z) => heights[x, z];

    public JBoundingBox GetBoundingBox()
    {
        var min = new JVector(0, MinHeight, 0);
        var max = new JVector(Width - 1, MaxHeight, Height - 1);
        return new JBoundingBox(min, max);
    }
}

public class HeightmapTester(Heightmap heightmap) : IDynamicTreeProxy, IRayCastable
{
    public int SetIndex { get; set; } = -1;
    public int NodePtr { get; set; }
    public Heightmap Heightmap { get; } = heightmap;

    public JVector Velocity => JVector.Zero;
    public JBoundingBox WorldBoundingBox { get; } = heightmap.GetBoundingBox();

    private void RayCastTriangle(in JVector origin, in JVector direction,
        in JVector a, in JVector b, in JVector c, out JVector normal, out float lambda)
    {
        var u = b - a;
        var v = c - a;

        normal = v % u;
        var it = 1.0f / normal.LengthSquared();
        var denominator = JVector.Dot(direction, normal);

        if (Math.Abs(denominator) < 1e-06f)
        {
            // triangle and ray are parallel
            lambda = float.MaxValue;
            normal = JVector.Zero;
            return;
        }

        lambda = JVector.Dot(a - origin, normal);
        if (lambda > 0.0f)
        {
            // ray is pointing away from the triangle
            lambda = float.MaxValue;
            normal = JVector.Zero;
            return;
        }

        lambda /= denominator;

        // point where the ray intersects the plane of the triangle.
        var hitPoint = origin + lambda * direction;
        var at = a - hitPoint;

        JVector.Cross(u, at, out var tmp);
        var gamma = JVector.Dot(tmp, normal) * it;
        JVector.Cross(at, v, out tmp);
        var beta = JVector.Dot(tmp, normal) * it;
        var alpha = 1.0f - gamma - beta;

        if (!(alpha > 0 && beta > 0 && gamma > 0))
        {
            // point is outside the triangle
            normal = JVector.Zero;
            lambda = float.MaxValue;
            return;
        }

        normal *= MathF.Sqrt(it);
    }

    public bool RayCast(in JVector origin, in JVector direction, out JVector normal, out float lambda)
    {
        const float maxDistance = 100.0f;

        var dirX = direction.X;
        var dirZ = direction.Z;

        var len2 = dirX * dirX + dirZ * dirZ;
        var ilen = 1.0f / MathF.Sqrt(len2);

        dirX *= ilen;
        dirZ *= ilen;

        var x = (int)Math.Floor(origin.X);
        var z = (int)Math.Floor(origin.Z);

        var stepX = dirX > 0 ? 1 : -1;
        var stepZ = dirZ > 0 ? 1 : -1;

        var nextX = dirX > 0 ? (x + 1) - origin.X : origin.X - x;
        var nextZ = dirZ > 0 ? (z + 1) - origin.Z : origin.Z - z;

        var tMaxX = dirX != 0 ? nextX / Math.Abs(dirX) : float.PositiveInfinity;
        var tMaxZ = dirZ != 0 ? nextZ / Math.Abs(dirZ) : float.PositiveInfinity;

        var tDeltaX = direction.X != 0 ? 1f / Math.Abs(dirX) : float.PositiveInfinity;
        var tDeltaZ = direction.Z != 0 ? 1f / Math.Abs(dirZ) : float.PositiveInfinity;

        var t = 0f;

        while (t <= maxDistance)
        {
            // check if we are out of bounds
            if (x < 0 || x + 1 >= Heightmap.Width || z < 0 || z + 1 >= Heightmap.Height)
                goto continue_walk;

            // check this quad!

            var a = new JVector(x + 0, Heightmap.GetHeight(x + 0, z + 0), z + 0);
            var b = new JVector(x + 1, Heightmap.GetHeight(x + 1, z + 0), z + 0);
            var c = new JVector(x + 1, Heightmap.GetHeight(x + 1, z + 1), z + 1);
            var d = new JVector(x + 0, Heightmap.GetHeight(x + 0, z + 1), z + 1);

            RayCastTriangle(origin, direction, a, b, c, out var normal0, out var lambda0);
            RayCastTriangle(origin, direction, a, c, d, out var normal1, out var lambda1);

            if (lambda0 < float.MaxValue || lambda1 < float.MaxValue)
            {
                if (lambda0 <= lambda1)
                {
                    normal = normal0;
                    lambda = lambda0;
                }
                else
                {
                    normal = normal1;
                    lambda = lambda1;
                }

                return true;
            }

continue_walk:

            if (tMaxX < tMaxZ)
            {
                x += stepX;
                t = tMaxX;
                tMaxX += tDeltaX;
            }
            else
            {
                z += stepZ;
                t = tMaxZ;
                tMaxZ += tDeltaZ;
            }
        }

        normal = JVector.Zero; lambda = 0.0f;
        return false;
    }
}

public struct CollisionTriangle : ISupportMappable
{
    public JVector A, B, C;

    public void SupportMap(in JVector direction, out JVector result)
    {
        var min = JVector.Dot(A, direction);
        var dot = JVector.Dot(B, direction);

        result = A;
        if (dot > min)
        {
            min = dot;
            result = B;
        }

        dot = JVector.Dot(C, direction);
        if (dot > min)
        {
            result = C;
        }
    }

    public void GetCenter(out JVector point)
    {
        point = (1.0f / 3.0f) * (A + B + C);
    }
}

public class HeightmapDetection : IBroadPhaseFilter
{
    private readonly Jitter2.World _world;
    private readonly HeightmapTester _shape;
    private readonly Heightmap _heightmap;
    private readonly ulong _minIndex;

    public HeightmapDetection(Jitter2.World world, HeightmapTester shape)
    {
        _shape = shape;
        _world = world;
        _heightmap = shape.Heightmap;

        (_minIndex, _) = Jitter2.World.RequestId(_heightmap.Width * _heightmap.Height * 2);
    }

    public bool Filter(IDynamicTreeProxy shapeA, IDynamicTreeProxy shapeB)
    {
        if (shapeA != _shape && shapeB != _shape) return true;

        var collider = shapeA == _shape ? shapeB : shapeA;

        if (collider is not RigidBodyShape rbs || rbs.RigidBody.Data.MotionType != MotionType.Dynamic) return false;

        ref var body = ref rbs.RigidBody!.Data;

        var min = collider.WorldBoundingBox.Min;
        var max = collider.WorldBoundingBox.Max;

        var minX = Math.Max(0, (int)min.X);
        var minZ = Math.Max(0, (int)min.Z);
        var maxX = Math.Min(_heightmap.Width - 1, (int)max.X + 1);
        var maxZ = Math.Min(_heightmap.Height - 1, (int)max.Z + 1);

        for (var x = minX; x < maxX; x++)
        {
            for (var z = minZ; z < maxZ; z++)
            {
                // First triangle of the quad

                var index = 2 * (ulong)(x * _heightmap.Width + z);

                CollisionTriangle triangle;

                triangle.A = new JVector(x + 0, _heightmap.GetHeight(x + 0, z + 0), z + 0);
                triangle.B = new JVector(x + 1, _heightmap.GetHeight(x + 1, z + 0), z + 0);
                triangle.C = new JVector(x + 1, _heightmap.GetHeight(x + 1, z + 1), z + 1);

                var normal = JVector.Normalize((triangle.C - triangle.A) % (triangle.B - triangle.A));

                var hit = NarrowPhase.MprEpa(triangle, rbs, body.Orientation, body.Position, out var pointA, out var pointB, out _, out var penetration);

                if (hit)
                {
                    _world.RegisterContact(rbs.ShapeId, _minIndex + index, _world.NullBody, rbs.RigidBody, pointA, pointB, normal, (ContactData.SolveMode)penetration);
                }

                // Second triangle of the quad

                index += 1;
                triangle.A = new JVector(x + 0, _heightmap.GetHeight(x + 0, z + 0), z + 0);
                triangle.B = new JVector(x + 1, _heightmap.GetHeight(x + 1, z + 1), z + 1);
                triangle.C = new JVector(x + 0, _heightmap.GetHeight(x + 0, z + 1), z + 1);

                normal = JVector.Normalize((triangle.C - triangle.A) % (triangle.B - triangle.A));

                hit = NarrowPhase.MprEpa(triangle, rbs, body.Orientation, body.Position, out pointA, out pointB, out _, out penetration);

                if (hit)
                {
                    _world.RegisterContact(rbs.ShapeId, _minIndex + index, _world.NullBody, rbs.RigidBody, pointA, pointB, normal, (ContactData.SolveMode)penetration);
                }
            }
        }

        return false;
    }
}
