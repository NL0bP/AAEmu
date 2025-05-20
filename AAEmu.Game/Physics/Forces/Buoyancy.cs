using System;
using System.Collections.Generic;

using Jitter2;
using Jitter2.Collision;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.LinearMath;

namespace AAEmu.Game.Physics.Forces;

/// <summary>
/// Simple Helper that adds buoyancy forces to a body if it is within
/// the FluidVolume. The volume is represented by a axis aligned bounding box or by
/// the user.
/// </summary>
public class Buoyancy : ForceGenerator
{

    /// <summary>
    /// Returns true if the given point is within the area.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>True if the given point is within the area.</returns>
    public delegate bool DefineFluidArea(ref JVector point);

    private readonly Dictionary<Shape, JVector[]> _samples = [];
    private readonly List<RigidBody> _bodies = [];

    /// <summary>
    /// The axis aligned bounding box representing the fluid.
    /// </summary>
    public JBBox FluidBox { get; set; }

    /// <summary>
    /// Densitity of the fluid. Default is 2.0.
    /// </summary>
    public float Density { get; set; }

    /// <summary>
    /// Damping applied to the body if it is in contact with the fluid.
    /// Default is 0.1.
    /// </summary>
    public float Damping { get; set; }

    /// <summary>
    /// Flow direction and magnitude.
    /// </summary>
    public JVector Flow { get; set; }

    private DefineFluidArea _fluidArea;

    /// <summary>
    /// Creates a new instance of the FluidVolume class.
    /// </summary>
    /// <param name="world">The world.</param>
    public Buoyancy(World world) : base(world)
    {
        Density = 2.0f;
        Damping = 0.1f;
        Flow = JVector.Zero;
    }

    /// <summary>
    /// Removes bodies from the fluid.
    /// </summary>
    /// <param name="body"></param>
    public void Remove(RigidBody body)
    {
        var flag = false;

        foreach (var b in _bodies)
        {
            if (body.Shapes[0] == b.Shapes[0])
            {
                flag = true;
                break;
            }
        }

        _bodies.Remove(body);
        if (!flag) _samples.Remove(body.Shapes[0]);
    }

    /// <summary>
    /// Removes all bodies from the fluid.
    /// </summary>
    public void Clear()
    {
        _bodies.Clear();
        _samples.Clear();
    }

    /// <summary>
    /// If you don't want to use the default axis aligned bounding box as
    /// fluid area representation you can define your own area using the FluidAreaDelegate.
    /// </summary>
    /// <param name="fluidArea">A delegate specifing the fluid area. Set to null if you
    /// want to use the default box.</param>
    public void UseOwnFluidArea(DefineFluidArea fluidArea)
    {
        _fluidArea = fluidArea;
    }

    /// <summary>
    /// Adds a body to the fluid. Only bodies which where added
    /// to the fluidvolume gets affected by buoyancy forces.
    /// </summary>
    /// <param name="body">The body which should be added.</param>
    /// <param name="subdivisions">The object is subdivided in smaller objects
    /// for which buoyancy force is calculated. The more subdivisons the better
    /// the results. Note that the total number of subdivisions is subdivisions³.</param>
    public void Add(RigidBody body, int subdivisions)
    {
        List<JVector> massPoints = [];

        var diff = body.Shapes[0].WorldBoundingBox.Max - body.Shapes[0].WorldBoundingBox.Min;

        if (MathHelper.CloseToZero(diff))
            throw new InvalidOperationException("BoundingBox volume of the shape is zero.");

        for (var i = 0; i < subdivisions; i++)
        {
            for (var e = 0; e < subdivisions; e++)
            {
                for (var k = 0; k < subdivisions; k++)
                {
                    JVector testVector;
                    testVector.X = body.Shapes[0].WorldBoundingBox.Min.X + (diff.X / (subdivisions - 1)) * i;
                    testVector.Y = body.Shapes[0].WorldBoundingBox.Min.Y + (diff.Y / (subdivisions - 1)) * e;
                    testVector.Z = body.Shapes[0].WorldBoundingBox.Min.Z + (diff.Z / (subdivisions - 1)) * k;

                    if (NarrowPhase.PointTest(body.Shapes[0], in testVector))
                    {
                        massPoints.Add(testVector);
                    }
                }
            }
        }

        _samples.Add(body.Shapes[0], massPoints.ToArray());
        _bodies.Add(body);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="timeStep"></param>
    public override void PreStep(float timeStep)
    {
        var damping = (float)Math.Pow(Damping, timeStep);

        foreach (var body in _bodies)
        {
            if ((FluidBox.Contains(body.Shapes[0].WorldBoundingBox) != JBBox.ContainmentType.Disjoint) || (_fluidArea != null))
            {
                var positions = _samples[body.Shapes[0]];

                var frac = 0.0f;

                for (var i = 0; i < positions.Length; i++)
                {
                    var currentCoord = JVector.Transform(positions[i], body.Orientation);
                    currentCoord = JVector.Add(currentCoord, body.Position);

                    bool containsCoord;

                    if (_fluidArea == null) containsCoord = FluidBox.Contains(in currentCoord) != JBBox.ContainmentType.Disjoint;
                    else containsCoord = _fluidArea(ref currentCoord);

                    if (containsCoord)
                    {
                        body.AddForce((1.0f / positions.Length) * body.Mass * Flow);
                        body.Shapes[0].CalculateMassInertia(out _, out _, out var shapeMass);
                        body.AddForce(-(1.0f / positions.Length) * shapeMass * Density * world.Gravity, currentCoord);
                        frac += 1.0f / positions.Length;
                    }
                }

                body.AngularVelocity *= damping;
                body.Velocity *= damping;
            }
        }
    }
}
