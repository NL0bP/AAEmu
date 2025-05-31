#nullable enable

using System;

using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Physics.Util;
using AAEmu.Game.Utils;

using Jitter2;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.LinearMath;

namespace AAEmu.Game.Physics;

public class ShipController
{
    private readonly World _world;

    public RigidBody Hull { get; private set; } = null!;

    internal ShipModel ShipModel { get; init; }

    private readonly float _waterLevel;
    
    public ShipController(World world, ShipModel shipModel, float waterLevel = 100f)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _waterLevel = waterLevel;
        ShipModel = shipModel ?? throw new ArgumentNullException(nameof(shipModel));
    }

    ~ShipController()
    {
        try
        {
            Hull?.World.Remove(Hull);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to remove hull RigidBody from Physics world: {e}");
        }
    }

    /// <summary>
    /// Создает корпус корабля.
    /// </summary>
    public void Build(JVector initialPosition, JQuaternion initialOrientation)
    {
        // New object
        Hull = _world.CreateRigidBody();
        // Set starting position and rotation
        Hull.Position = initialPosition;
        Hull.Orientation = initialOrientation;
        // Ship shape
        var shipBoxShape = new BoxShape(ShipModel.MassBoxSizeY, ShipModel.MassBoxSizeZ, ShipModel.MassBoxSizeX);
        // Center offset
        var shipCenterPoint = new TransformedShape(shipBoxShape, new JVector(ShipModel.MassCenterX, ShipModel.MassCenterZ, ShipModel.MassCenterY));
        // Add shape
        Hull.AddShape(shipCenterPoint);
        // Set Mass
        Hull.SetMassInertia(ShipModel.Mass);
        Hull.DeactivationTime = TimeSpan.MaxValue;
        Hull.IsStatic = false;
        Hull.SetActivationState(true);
    }

    /// <summary>
    /// Обновляет управление кораблем. Вызывать перед каждым шагом физики.
    /// </summary>
    public void UpdateControls(Slave slave, RigidBody rigidBody, ShipModel shipModel)
    {
        if (slave?.RigidBody is null)
            return;

        // Check if the ship has a driver
        var hasDriver = slave.AttachedCharacters.ContainsKey(AttachPointKind.Driver);
        if (hasDriver)
        {
            // If there is a driver, we update the control
            // Smooth throttle and steering inputs
            const float SmoothingFactor = 0.1f;
            slave.Throttle = (sbyte)(slave.Throttle + (slave.ThrottleRequest - slave.Throttle) * SmoothingFactor);
            slave.Steering = (sbyte)(slave.Steering + (slave.SteeringRequest - slave.Steering) * SmoothingFactor);
        }
        else
        {
            // If there is no driver, we reset the control
            slave.ThrottleRequest = 0;
            slave.SteeringRequest = 0;
            slave.Throttle = 0;
            slave.Steering = 0;
        }

        // Provide minimum speed of 1 when Throttle is used
        if (slave is { Throttle: > 0, Speed: < 1f })
            slave.Speed = 1f;

        if (slave is { Throttle: < 0, Speed: > -1f })
            slave.Speed = -1f;

        var throttleNorm = slave.Throttle * 0.00787401575f; // sbyte -> float
        var steeringNorml = slave.Steering * 0.00787401575f; // sbyte -> float

        // Calculate speed
        slave.Speed += throttleNorm * (shipModel.Accel / 10f);

        // Clamp speed between min and max Velocity
        var maxForward = shipModel.Velocity;
        var maxBackward = -shipModel.ReverseVelocity;
        slave.Speed = Math.Clamp(slave.Speed, maxBackward, maxForward);

        // Calculate rotation speed
        var turnSpeed = slave.TurnSpeed == 0 ? 10f : slave.TurnSpeed;
        slave.RotSpeed += steeringNorml * (turnSpeed / 100f) * (shipModel.TurnAccel / 360f);

        // Clamp to Steer Velocity
        var steerMax = (shipModel.Velocity * 2).DegToRad();
        slave.RotSpeed = Math.Clamp(slave.RotSpeed, -steerMax, steerMax);

        // Slow down turning if no steering active
        const float AngularDamping = 0.9f; // Damping of angular velocity
        if (slave.Steering == 0)
            slave.RotSpeed *= AngularDamping;

        // this needs to be fixed : ships need to apply a static drag, and slowly ship away at the speed instead of doing it like this
        if (slave.Throttle == 0)
        {
            slave.Speed -= slave.Speed / (100 * 3f);
            if (Math.Abs(slave.Speed) < 1)
            {
                slave.Speed = 0;
                slave.RigidBody.Velocity = JVector.Zero;
            }
        }

        // Get current rotation of the ship
        var rpy = PhysicsUtil.GetYawPitchRollFromMatrix(JMatrix.CreateFromQuaternion(rigidBody.Orientation));
        var slaveRotRad = rpy.Item1 + 1.57f; // 90 degrees in radians

        var forceThrottle = slave.Speed; // * slave.MoveSpeedMul; // Not sure if correct, but it feels correct

        // Apply directional force
        rigidBody.Velocity = new JVector(forceThrottle * MathF.Cos(slaveRotRad), 0.0f, forceThrottle * MathF.Sin(slaveRotRad));

        var steer = slave.RotSpeed * -1;

        // Make sure the steering is reversed when going backwards.
        if (forceThrottle < 0)
            steer *= -1;

        rigidBody.AngularVelocity = new JVector(0, steer, 0);

        //Logger.Debug($"Slave: {slave.Name}, Throttle: {throttleFloatVal:F1} ({slave.ThrottleRequest}), Steering {steeringFloatVal:F1} ({slave.SteeringRequest}), speed: {slave.Speed}, rotSpeed: {slave.RotSpeed}");
    }
}
