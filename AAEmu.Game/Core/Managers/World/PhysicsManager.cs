using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Static;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.Units.slaves;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Physics;
using AAEmu.Game.Physics.Forces;
using AAEmu.Game.Physics.Util;
using AAEmu.Game.Utils;

using Jitter2.Dynamics;
using Jitter2.LinearMath;

using NLog;

using InstanceWorld = AAEmu.Game.Models.Game.World.World;

namespace AAEmu.Game.Core.Managers.World
{
    // ReSharper disable HollowTypeName
    public class PhysicsManager : IDisposable
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
        public InstanceWorld SimulationWorld { get; set; }

        private const float DefaultWaterLevel = 100f;
        private const int MaxPhysicsSteps = 4;

        private float TargetPhysicsTps { get; set; } = 100f;
        internal Thread _thread;

        internal Jitter2.World _physWorld;
        internal Buoyancy _buoyancy;
        public bool ThreadRunning { get; set; }

        // --- Добавляем tracker ---
        private readonly LimitedCallTracker _finalizeTransformTracker = new(5);

        private readonly Dictionary<uint, ShipController> _shipControllers = new();

        private readonly ConcurrentQueue<Action> _pendingActions = new();
        private readonly Lock _worldLock = new();
        private readonly List<RigidBody> _bodies = [];

        public void Initialize()
        {
            _physWorld = new Jitter2.World();
            _physWorld.Gravity = new JVector(0, -9.81f, 0);

            _buoyancy = new Buoyancy(_physWorld);
            _buoyancy.FluidBox = new JBoundingBox(
                new JVector(0, 0, 0), // Bottom
                new JVector(SimulationWorld.CellX * WorldManager.CELL_SIZE, SimulationWorld.OceanLevel, SimulationWorld.CellY * WorldManager.CELL_SIZE) // Surface
            );
            _buoyancy.UseOwnFluidArea(CustomWater);

            Logger.Info($"PhysicsManager {SimulationWorld.Name} initialized.");
        }

        public void InitializeTerrain()
        {
            // Add terrain shape based on height map
            //if (SimulationWorld.Name != "main_world") { return; }
            //try
            //{
            //    var hmap = WorldManager.Instance.GetWorld(0).HeightMaps;
            //    var heightMaxCoefficient = WorldManager.Instance.GetWorld(0).HeightMaxCoefficient;
            //    var dx = hmap.GetLength(0);
            //    var dz = hmap.GetLength(1);
            //    var hmapTerrain = new float[dx, dz];
            //    for (var x = 0; x < dx; x++)
            //        for (var y = 0; y < dz; y++)
            //            hmapTerrain[x, y] = (float)(hmap[x, y] / heightMaxCoefficient);

            //    var heightmap = new Heightmap(hmapTerrain);
            //    var tester = new HeightmapTester(heightmap);
            //    _physWorld.BroadPhaseFilter = new HeightmapDetection(_physWorld, tester);
            //    _physWorld.DynamicTree.AddProxy(tester, false);
            //}
            //catch (Exception e)
            //{
            //    Logger.Error(e);
            //}

            Logger.Info($"PhysicsManager {SimulationWorld.Name} initialized Terrain.");
        }

        public void StartPhysics()
        {
            ThreadRunning = true;
            _thread = new Thread(PhysicsThread) { Name = "Physics-" + (SimulationWorld?.Name ?? "???") };
            _thread.Start();
        }

        private void PhysicsThread()
        {
            try
            {
                Logger.Debug($"PhysicsThread Start: {Thread.CurrentThread.Name}");

                var lastTick = TimeSpan.FromMilliseconds(Environment.TickCount64);
                var fixedStep = TimeSpan.FromSeconds(1f / TargetPhysicsTps);
                var accumulatedTime = TimeSpan.Zero;

                while (ThreadRunning)
                {
                    Thread.Sleep(fixedStep);

                    // 1. Process pending add/remove actions
                    while (_pendingActions.TryDequeue(out var action)) { action(); }

                    var currentTick = TimeSpan.FromMilliseconds(Environment.TickCount64);
                    var timeSinceLastTick = currentTick - lastTick;
                    accumulatedTime += timeSinceLastTick;
                    var steps = 0;

                    List<(RigidBody body, JVector vel, bool moving)> snapshot = [];

                    lock (_worldLock)
                    {

                        // 2. Take snapshot of bodies for state synchronization
                        foreach (var body in _bodies)
                        {
                            if (body == null) { continue; }

                            var vel = body.Velocity;
                            var moving = vel.LengthSquared() > 0.001f;
                            snapshot.Add((body, vel, moving));
                        }

                        // 3. Step the physics world
                        // Potentially step multiple times to catch up if we were running behind.
                        while (accumulatedTime > fixedStep)
                        {
                            _physWorld.Step((float)fixedStep.TotalSeconds, false);
                            accumulatedTime -= fixedStep;
                            if (++steps >= MaxPhysicsSteps) { break; }
                        }

                        lastTick = currentTick;

                        // 4. Sync positions and broadcast outside lock
                        foreach (var (body, velocity, isMoving) in snapshot)
                        {
                            if (body.Tag is Npc npc)
                            {
                                // Update transform
                                //UpdateNpcTransform(npc, velocity, isMoving);

                                // Update avoidance controller
                                //npc.AvoidanceController.Update(0.01f);
                            }

                            if (body.Tag is not Slave slave) { continue; }

                            try
                            {
                                if (slave.Transform.WorldId != SimulationWorld.Id)
                                    continue;

                                // Skip simulation if still summoning
                                if (slave.SpawnTime.AddSeconds(slave.Template.PortalTime) > DateTime.UtcNow)
                                    continue;

                                if (slave.Hp <= 0)
                                {
                                    body.SetActivationState(false);
                                    continue;
                                }

                                // Skip simulation if no rigidbody applied to slave
                                if (!body.IsActive)
                                    continue;

                                var underPos = slave.Transform.World.Position + Vector3.UnitZ * -2f;
                                if (SimulationWorld.Water.IsWater(underPos, out var flowDirection))
                                {
                                    if (flowDirection.Length() > 0f)
                                    {
                                        // We are in moving water, apply force
                                        var multiplier = slave.RigidBody.Mass * 3.15f;
                                        slave.RigidBody.AddForce(new JVector(flowDirection.X * multiplier, flowDirection.Z * multiplier, flowDirection.Y * multiplier));
                                        // slaveRigidBody.LinearVelocity += new JVector(flowDirection.X * 0.1f,flowDirection.Z * 0.1f, flowDirection.Y * 0.1f);
                                    }
                                }

                                SyncTransformWithRigidBody(slave);
                                BoatPhysicsTick(slave, body, slave.ShipController.ShipModel);
                                if (this.CheckInterval(150))
                                    SendUpdatedMovementData(slave, body);
                            }
                            catch (Exception slaveException)
                            {
                                // Put a separate catch here to catch individual errors without it breaking all the physics in this world 
                                Logger.Error($"PhysicsThread Error on Slave {slave.Id} {slave.Name} ({slave.ObjId}): {slaveException.Message}\n{slaveException.StackTrace}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"PhysicsThread Error: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                Logger.Debug($"PhysicsThread End: {Thread.CurrentThread.Name}");
            }
        }

        private void SyncTransformWithRigidBody(Slave slave)
        {
            var slaveRigidBody = slave.RigidBody;
            var xDelta = slaveRigidBody.Position.X - slave.Transform.World.Position.X;
            var yDelta = slaveRigidBody.Position.Z - slave.Transform.World.Position.Y;
            var zDelta = slaveRigidBody.Position.Y - slave.Transform.World.Position.Z;
            //if (zDelta < -3)
            //{
            //    slaveRigidBody.Position = slaveRigidBody.Position with { Y = slave.Transform.World.Position.Z };
            //    zDelta = 0;
            //    Logger.Info($"SyncTransformWithRigidBody {slave.Name} -> {SimulationWorld.Name}, _waterLevel={DefaultWaterLevel}, OceanLevel={SimulationWorld.OceanLevel}, slave.Position.Z={slave.Transform.World.Position.Z}");
            //}

            slave.Transform.Local.Translate(xDelta, yDelta, zDelta);
            var rotation = slaveRigidBody.Orientation;
            slave.Transform.Local.ApplyFromQuaternion(rotation.X, rotation.Z, rotation.Y, rotation.W);
        }

        public void AddShip(Slave slave)
        {
            var shipModel = ModelManager.Instance.GetShipModel(slave.ModelId);
            if (shipModel == null || shipModel.Mass <= 0)
            {
                Logger.Error($"Invalid ship model for slave {slave.Name}");
                return;
            }

            var pos = new JVector(slave.Transform.World.Position.X, slave.Transform.World.Position.Z, slave.Transform.World.Position.Y);
            var rot = JQuaternion.CreateRotationY(slave.Transform.World.Rotation.Z);
            //                                       Width                   Length                  Height
            //var dimensions = new JVector(shipModel.MassBoxSizeX, shipModel.MassBoxSizeY, shipModel.MassBoxSizeZ);
            var ctrl = new ShipController(world: _physWorld, shipModel: shipModel, waterLevel: DefaultWaterLevel);

            ctrl.Build(initialPosition: pos, initialOrientation: rot);

            _shipControllers[slave.Id] = ctrl;
            slave.RigidBody = ctrl.Hull;
            slave.RigidBody.Tag = slave;
            slave.ShipController = ctrl;

            EnqueueAddBody(slave.RigidBody);
            _buoyancy.AddForRectangularParallelepiped(slave.RigidBody, 3);

            // Reset FinalizeTransform counter on spawn
            _finalizeTransformTracker.Reset(slave.Id);

            Logger.Debug($"AddShip {slave.Name} -> {SimulationWorld.Name}");
        }

        public void RemoveShip(Slave slave)
        {
            if (slave.RigidBody == null) return;

            var rigidBody = slave.RigidBody;
            rigidBody.SetActivationState(false);
            EnqueueRemoveBody(rigidBody);
            _physWorld.Remove(rigidBody);
            _buoyancy.Remove(rigidBody);
            _shipControllers.Remove(slave.Id);
            slave.ShipController = null;
            slave.RigidBody = null;

            // Cleanup FinalizeTransform counter
            _finalizeTransformTracker.Remove(slave.Id);

            Logger.Debug($"RemoveShip {slave.Name} <- {SimulationWorld.Name}");
        }

        private void BoatPhysicsTick(Slave slave, RigidBody rigidBody, ShipModel shipModel)
        {
            slave.ShipController.UpdateControls(slave, rigidBody, shipModel);
            ApplyCollisions(slave, rigidBody, shipModel);
        }

        private void SendUpdatedMovementData(Slave slave, RigidBody rigidBody)
        {
            var moveType = (ShipMoveType)MoveType.GetType(MoveTypeEnum.Ship);
            moveType.UseSlaveBase(slave);

            // Get current rotation of the ship
            var rpy = PhysicsUtil.GetYawPitchRollFromMatrix(JMatrix.CreateFromQuaternion(rigidBody.Orientation));
            // Insert new Rotation data into MoveType
            var (rotZ, rotY, rotX) = MathUtil.GetSlaveRotationFromDegrees(rpy.Item1, rpy.Item2, rpy.Item3);
            moveType.RotationX = rotX;
            moveType.RotationY = rotY;
            moveType.RotationZ = rotZ;

            // Fill in the Velocity Data into the MoveType
            //moveType.Velocity = new Vector3(rigidBody.Velocity.X, rigidBody.Velocity.Z, rigidBody.Velocity.Y);
            moveType.AngVelX = rigidBody.AngularVelocity.X;
            moveType.AngVelY = rigidBody.AngularVelocity.Z;
            moveType.AngVelZ = rigidBody.AngularVelocity.Y;

            // Seems display the correct speed this way, but what happens if you go over the bounds ?
            moveType.VelX = (short)(rigidBody.Velocity.X * 1024);
            moveType.VelY = (short)(rigidBody.Velocity.Z * 1024);
            moveType.VelZ = (short)(rigidBody.Velocity.Y * 1024);

            // Do not allow the body to flip
            //slave.RigidBody.Orientation = Quaternion.CreateFromYawPitchRoll(rpy.Item1, 0, rpy.Item3); // TODO: Fix me with proper physics

            // Apply new Location/Rotation to GameObject
            slave.Transform.Local.SetPosition(rigidBody.Position.X, rigidBody.Position.Z, rigidBody.Position.Y);
            slave.Transform.Local.ApplyFromQuaternion(rigidBody.Orientation);

            // Send the packet
            //slave.BroadcastPacket(new SCOneUnitMovementPacket(slave.ObjId, moveType), false);
            var movements = new (uint, MoveType)[] { (slave.ObjId, moveType) };
            slave.BroadcastPacket(new SCUnitMovementsPacket(movements), false);

            // Call FinalizeTransform only first 5 times per Slave
            if (_finalizeTransformTracker.TryCall(slave.Id))
            {
                // Update all to main Slave and it's children
                slave.Transform.FinalizeTransform();
            }
        }

        private bool ApplyCollisions(Slave slave, RigidBody rigidBody, ShipModel shipModel)
        {
            var floor = WorldManager.Instance.GetHeight(slave.Transform);
            var boatBottom = rigidBody.Position.Y;
            //Logger.Debug($"Slave: {slave.Name}, floor: {floor:F1}, boatBottom: {boatBottom:F1}, boxSize: {boxSize}");

            if (SimulationWorld?.OceanLevel < floor)
            {
                var penetration = floor - boatBottom;
                rigidBody.Position += new JVector(0, penetration, 0);
                var collisionForce = new JVector(0, shipModel.Mass * 9.81f, 0);
                rigidBody.AddForce(collisionForce);

                // Gradually reduce speed
                var collisionDamping = 0.9f;
                rigidBody.Velocity *= collisionDamping;
                rigidBody.AngularVelocity *= collisionDamping;

                //if (this.CheckInterval(1500))
                //    Logger.Debug($"Collision detected. Boat adjusted position: {rigidBody.Position}");

                var damageAmount = (floor - SimulationWorld.OceanLevel) * 100f;
                if (damageAmount < 1f)
                    damageAmount = 1f;

                if (damageAmount > 0)
                {
                    if (slave.Summoner.Buffs.CheckBuff((uint)BuffConstants.PeaceZone))
                        damageAmount = 1;

                    slave.DoFloorCollisionDamage((int)damageAmount, false, KillReason.Collide);
                    if (this.CheckInterval(1500))
                    {
                        slave.Summoner.BroadcastPacket(new SCEnvDamagePacket(EnvSource.Collision, slave.ObjId, (uint)damageAmount, 0, rigidBody.Position.ToVector(), damageAmount * 0.1f), true);
                        Logger.Debug($"Slave: {slave.ObjId}, speed: {slave.Speed}, rotSpeed: {slave.RotSpeed}, floor: {floor}, Z: {slave.Transform.World.Position.Z}, damage: {damageAmount}");
                    }
                }

                return true; // Collision detected and handled
            }

            return false; // No collision detected
        }

        public void Stop()
        {
            ThreadRunning = false;
        }

        public void Dispose() => _physWorld?.Dispose();

        public bool CustomWater(ref JVector area)
        {
            return SimulationWorld?.IsWater(new Vector3(area.X, area.Z, area.Y), out _) ?? area.Y <= (SimulationWorld?.OceanLevel ?? DefaultWaterLevel);
        }

        /// <summary>
        /// Enqueues an NPC body to be added in the next physics step.
        /// </summary>
        public void EnqueueAddBody(RigidBody body)
        {
            if (body == null) return;
            _pendingActions.Enqueue(() =>
            {
                _bodies.Add(body);
            });
        }

        /// <summary>
        /// Enqueues an NPC body to be removed in the next physics step.
        /// </summary>
        public void EnqueueRemoveBody(RigidBody body)
        {
            if (body == null) return;
            _pendingActions.Enqueue(() =>
            {
                _bodies.Remove(body);
            });
        }

        public static float GetRollAngle(JMatrix orientation)
        {
            var yawPitchRoll = GetYawPitchRollFromJMatrix(orientation);
            return yawPitchRoll.Item2; // Roll angle in radians
        }

        public static (float, float, float) GetYawPitchRollFromJMatrix(JMatrix mat)
        {
            return MathUtil.GetYawPitchRollFromQuat(JMatrixToQuaternion(mat));
        }

        public static Quaternion JMatrixToQuaternion(JMatrix matrix)
        {
            var jq = JQuaternion.CreateFromMatrix(matrix);

            return new Quaternion() { X = jq.X, Y = jq.Y, Z = jq.Z, W = jq.W };
        }

    }
}

// --- Новый helper-класс ---
public class LimitedCallTracker
{
    private readonly Dictionary<uint, int> _callCounts = new();
    private readonly int _maxCalls;

    public LimitedCallTracker(int maxCalls = 5)
    {
        _maxCalls = maxCalls;
    }

    // Returns true if call is allowed (increments counter)
    public bool TryCall(uint id)
    {
        var count = _callCounts.GetValueOrDefault(id, 0);

        if (count < _maxCalls)
        {
            _callCounts[id] = count + 1;
            return true;
        }

        return false;
    }

    // Reset counter for object (e.g. on spawn)
    public void Reset(uint id)
    {
        _callCounts[id] = 0;
    }

    // Remove counter for object (e.g. on despawn)
    public void Remove(uint id)
    {
        _callCounts.Remove(id);
    }
}

