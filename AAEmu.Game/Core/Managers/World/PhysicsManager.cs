using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.SkillControllers;
using AAEmu.Game.Models.Game.Slaves;
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

        // Ship interaction resolvers
        private readonly ShipShoreInteraction _shipShore = new();
        private readonly ShipShipInteraction _shipShip = new();
        private readonly ShipCliffInteraction _shipCliff = new();
        private readonly ShipDoodadInteraction _shipDoodad = new();
        private readonly ShipStaticBarrierInteraction _shipBarrier = new();

        public void Initialize()
        {
            if (SimulationWorld == null)
            {
                Logger.Error("SimulationWorld is null, cannot initialize PhysicsManager");
                return;
            }

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
                            try
                            {
                                // Проверяем, что физический мир существует перед выполнением шага
                                if (_physWorld != null)
                                {
                                    _physWorld.Step((float)fixedStep.TotalSeconds, false);
                                }
                            }
                            catch (AccessViolationException ex)
                            {
                                Logger.Error(ex, "AccessViolationException при выполнении шага физической симуляции. Попытка восстановления...");
                                // В случае ошибки, можно попробовать пересоздать физический мир или пропустить этот шаг
                                break; // Прерываем цикл, чтобы избежать повторного возникновения ошибки
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, "Произошла ошибка при выполнении шага физической симуляции");
                                break; // Прерываем цикл при любой другой ошибке
                            }

                            accumulatedTime -= fixedStep;
                            if (++steps >= MaxPhysicsSteps) { break; }
                        }

                        lastTick = currentTick;

                        // Total delta for this frame (clamped to max steps)
                        var physicsTotalDelta = TimeSpan.FromSeconds(steps * fixedStep.TotalSeconds);

                        // 4. Sync positions and broadcast outside lock
                        foreach (var (body, velocity, isMoving) in snapshot)
                        {
                            if (body.Tag is Npc npc)
                            {
                                // Update transform
                                //UpdateNpcTransform(npc, velocity, isMoving);
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

                                // Cache water surface, floor, and flow for this tick
                                slave.CreateWaterAndLandSurfaceCache();

                                var underPos = slave.Transform.World.Position + Vector3.UnitZ * -2f;
                                if (SimulationWorld.Water.IsWater(underPos, out var flowDirection) && flowDirection.LengthSquared() > 1e-10f)
                                {
                                    slave.CachedWaterFlow = flowDirection;
                                    var multiplier = slave.RigidBody.Mass * 3.15f;
                                    slave.RigidBody.AddForce(new JVector(flowDirection.X * multiplier, flowDirection.Z * multiplier, flowDirection.Y * multiplier));
                                }

                                SyncTransformWithRigidBody(slave);
                                BoatPhysicsTick(slave, physicsTotalDelta);
                                slave.ShipController?.ApplyForceAndTorque(slave, physicsTotalDelta);
                                SendUpdatedMovementData(slave, body, physicsTotalDelta);
                            }
                            catch (Exception slaveException)
                            {
                                Logger.Error($"PhysicsThread Error on Slave {slave.Id} {slave.Name} ({slave.ObjId}): {slaveException.Message}\n{slaveException.StackTrace}");
                            }
                        }

                        // Collect active ships for interaction resolution
                        var shipsThisTick = new List<Slave>();
                        foreach (var body2 in _bodies)
                        {
                            if (body2?.Tag is Slave s && s.Hp > 0 && body2.IsActive && s.Transform.WorldId == SimulationWorld.Id)
                                shipsThisTick.Add(s);
                        }

                        // Ship-to-ship, ship-to-shore, ship-to-cliff interactions
                        if (shipsThisTick.Count > 0)
                        {
                            _shipShore.ResolveAll(SimulationWorld, shipsThisTick, physicsTotalDelta);
                            _shipShip.ResolveAllPairs(shipsThisTick, physicsTotalDelta);
                            _shipCliff.ResolveAll(SimulationWorld, shipsThisTick, physicsTotalDelta);
                            _shipDoodad.ResolveAll(SimulationWorld, shipsThisTick, physicsTotalDelta);
                            _shipBarrier.ResolveAll(SimulationWorld, shipsThisTick, physicsTotalDelta);

                            // Tick damage accumulators
                            foreach (var ship in shipsThisTick)
                            {
                                ship.TickBeachedHullDamage(physicsTotalDelta);
                                ship.TickStaticObstacleHullDamage(physicsTotalDelta);
                                ship.StaticObstacleHullDamageContactActive = false;
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
            var ctrl = new ShipController(world: _physWorld, shipModel: shipModel);

            ctrl.Build(initialPosition: pos, initialOrientation: rot);

            _shipControllers[slave.Id] = ctrl;
            slave.RigidBody = ctrl.Hull;
            slave.RigidBody.Tag = slave;
            slave.ShipController = ctrl;

            // During PortalTime the physics thread skips ship processing (including transform sync),
            // so ensure the initial server-side Transform matches the physics spawn position.
            SyncTransformWithRigidBody(slave);
            slave.Transform.FinalizeTransform();
            ctrl.Replication.Reset();
            slave.WavePitchPhase = 0f;
            slave.ShipHullCollisionDamageCooldownByOtherShipId.Clear();
            slave.StaticObstacleHullDamageContactActive = false;
            slave.StaticObstacleHullDamageSecondsAccumulator = 0f;
            slave.StaticObstacleHullDamageNoContactSeconds = 0f;

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

            try
            {
                if (_physWorld != null && rigidBody != null)
                {
                    _physWorld.Remove(rigidBody);
                }
            }
            catch (AccessViolationException ex)
            {
                Logger.Error(ex, "AccessViolationException при удалении тела из физического мира");
            }

            try
            {
                if (_buoyancy != null && rigidBody != null)
                {
                    _buoyancy.Remove(rigidBody);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ошибка при удалении тела из системы буйности");
            }

            _shipControllers.Remove(slave.Id);
            slave.ShipController = null;
            slave.RigidBody = null;

            // Cleanup FinalizeTransform counter
            _finalizeTransformTracker.Remove(slave.Id);

            Logger.Debug($"RemoveShip {slave.Name} <- {SimulationWorld.Name}");
        }

        private void BoatPhysicsTick(Slave slave, TimeSpan deltaTime)
        {
            var shipModel = slave.ShipController?.ShipModel;
            if (shipModel == null) return;

            _shipShore.ApplyOnLandPhysics(slave, deltaTime);

            // Check if the ship has a driver
            var hasDriver = slave.AttachedCharacters.ContainsKey(AttachPointKind.Driver);
            if (hasDriver)
            {
                // Smooth toward client input in float space, then round — avoids sbyte stair-stepping on rudder animation.
                const float SmoothingFactor = 0.12f;
                slave.ThrottleSmoothed += (slave.ThrottleRequest - slave.ThrottleSmoothed) * SmoothingFactor;
                slave.SteeringSmoothed += (slave.SteeringRequest - slave.SteeringSmoothed) * SmoothingFactor;
                slave.Throttle = (sbyte)Math.Clamp((int)Math.Round(slave.ThrottleSmoothed), -128, 127);
                slave.Steering = (sbyte)Math.Clamp((int)Math.Round(slave.SteeringSmoothed), -128, 127);
            }
            else
            {
                // If there is no driver, we reset the control
                slave.ThrottleRequest = 0;
                slave.SteeringRequest = 0;
                slave.Throttle = 0;
                slave.Steering = 0;
                slave.ThrottleSmoothed = 0f;
                slave.SteeringSmoothed = 0f;
            }
        }

        private void SendUpdatedMovementData(Slave slave, RigidBody rigidBody, TimeSpan deltaTime)
        {
            var moveType = (ShipMoveType)MoveType.GetType(MoveTypeEnum.Ship);
            moveType.UseSlaveBase(slave);

            // Get current rotation of the ship
            var rpy = PhysicsUtil.GetYawPitchRollFromMatrix(JMatrix.CreateFromQuaternion(rigidBody.Orientation));

            // Visual-only bank (ship leans into turns). Applied to replicated rotation, not physics.
            var maxBankDeg = ComputeVisualMaxBankDegFromShipModel(slave.ShipController?.ShipModel, slave.Scale);
            const float bankResponse = 7.5f;
            var dt = Math.Max(0.0001f, (float)deltaTime.TotalSeconds);
            var maxBankRad = maxBankDeg.DegToRad();
            var yawRate = rigidBody.AngularVelocity.Y;
            var horizSpeed = MathF.Sqrt(
                rigidBody.Velocity.X * rigidBody.Velocity.X +
                rigidBody.Velocity.Z * rigidBody.Velocity.Z);
            var speedFactor = Math.Clamp(horizSpeed / 2.5f, 0f, 1f);
            var targetBank = Math.Clamp(-yawRate * 0.9f, -maxBankRad, maxBankRad) * speedFactor;
            var a = 1f - MathF.Exp(-bankResponse * dt);
            slave.BankAngle += (targetBank - slave.BankAngle) * a;

            _shipShore.UpdateVisualGroundPitch(slave, rigidBody, deltaTime);

            var wavePitchRad = ComputeVisualWavePitchOnWater(slave, rigidBody, dt);

            // Replication smoothing for clients
            const float repLambdaHorizFree = 22f;
            const float repLambdaHorizContact = 11f;
            const float repLambdaVertFree = 8f;
            const float repLambdaVertContact = 4f;
            const float repLambdaBankFree = 4f;
            const float repLambdaBankContact = 2f;
            var rep = slave.ShipController!.Replication;
            var repLambdaH = rep.ContactHoldTicks > 0 ? repLambdaHorizContact : repLambdaHorizFree;
            var repLambdaV = rep.ContactHoldTicks > 0 ? repLambdaVertContact : repLambdaVertFree;
            var repLambdaB = rep.ContactHoldTicks > 0 ? repLambdaBankContact : repLambdaBankFree;
            var repAlphaH = 1f - MathF.Exp(-repLambdaH * dt);
            var repAlphaV = 1f - MathF.Exp(-repLambdaV * dt);
            var repAlphaB = 1f - MathF.Exp(-repLambdaB * dt);

            var tgtX = rigidBody.Position.X;
            var tgtY = rigidBody.Position.Z;
            var tgtZ = rigidBody.Position.Y;
            var tgtVx = rigidBody.Velocity.X;
            var tgtVy = rigidBody.Velocity.Z;
            var tgtVz = rigidBody.Velocity.Y;

            if (!rep.Seeded)
            {
                rep.PosX = tgtX;
                rep.PosY = tgtY;
                rep.PosZ = tgtZ;
                rep.VelPx = tgtVx;
                rep.VelPy = tgtVy;
                rep.VelPz = tgtVz;
                rep.BankSmoothed = slave.BankAngle;
                rep.GroundPitchSmoothed = slave.GroundPitchAngle;
                rep.Seeded = true;
            }
            else
            {
                rep.PosX += (tgtX - rep.PosX) * repAlphaH;
                rep.PosY += (tgtY - rep.PosY) * repAlphaH;
                rep.PosZ += (tgtZ - rep.PosZ) * repAlphaV;
                rep.VelPx += (tgtVx - rep.VelPx) * repAlphaH;
                rep.VelPy += (tgtVy - rep.VelPy) * repAlphaH;
                rep.VelPz += (tgtVz - rep.VelPz) * repAlphaV;
                rep.BankSmoothed += (slave.BankAngle - rep.BankSmoothed) * repAlphaB;
                rep.GroundPitchSmoothed += (slave.GroundPitchAngle - rep.GroundPitchSmoothed) * repAlphaV;
            }

            var bankedRpy = (rpy.Item1, rpy.Item2 + rep.BankSmoothed, rpy.Item3 + rep.GroundPitchSmoothed + wavePitchRad);

            var (rotZ, rotY, rotX) = MathUtil.GetSlaveRotationFromDegrees(bankedRpy.Item1, bankedRpy.Item2, bankedRpy.Item3);
            moveType.RotationX = rotX;
            moveType.RotationY = rotY;
            moveType.RotationZ = rotZ;

            moveType.X = rep.PosX;
            moveType.Y = rep.PosY;
            moveType.Z = rep.PosZ;

            moveType.AngVelX = rigidBody.AngularVelocity.X;
            moveType.AngVelY = rigidBody.AngularVelocity.Z;
            moveType.AngVelZ = rigidBody.AngularVelocity.Y;

            const int velMultiplier = 2048;
            moveType.VelX = (short)(rep.VelPx * velMultiplier);
            moveType.VelY = (short)(rep.VelPy * velMultiplier);
            moveType.VelZ = (short)(rep.VelPz * velMultiplier);

            // Apply new Location/Rotation to GameObject
            slave.Transform.Local.SetPosition(rigidBody.Position.X, rigidBody.Position.Z, rigidBody.Position.Y);
            slave.Transform.Local.ApplyFromQuaternion(rigidBody.Orientation);
            slave.Transform.Local.SetRotation(
                slave.Transform.Local.Rotation.X,
                slave.Transform.Local.Rotation.Y + rep.BankSmoothed,
                slave.Transform.Local.Rotation.Z + rep.GroundPitchSmoothed + wavePitchRad);

            // Send the packet
            slave.BroadcastPacket(new SCOneUnitMovementPacket(slave.ObjId, moveType), false);

            // Update all to main Slave and it's children
            slave.Transform.FinalizeTransform();

            if (rep.ContactHoldTicks > 0)
                rep.ContactHoldTicks--;
        }

        /// <summary>
        /// Waterline length = max(X,Y), beam = min(X,Y), height = Z, mass from ship_models.
        /// </summary>
        private static bool TryGetShipMassBoxWaterlineExtents(ShipModel model, float scale,
            out float length, out float beam, out float height, out float mass)
        {
            if (model == null)
            {
                length = beam = height = mass = 0f;
                return false;
            }

            var s = MathF.Max(scale, 0.01f);
            var hx = model.MassBoxSizeX * s;
            var hy = model.MassBoxSizeY * s;
            length = MathF.Max(MathF.Max(hx, hy), 0.25f);
            beam = MathF.Max(MathF.Min(hx, hy), 0.25f);
            height = MathF.Max(model.MassBoxSizeZ * s, 0.15f);
            mass = MathF.Max(model.Mass, 10f);
            return true;
        }

        private static void GetVisualWavePitchModelFactors(ShipModel model, float scale, out float maxAmpRad, out float omega)
        {
            const float baseDeg = 3f;
            const float baseHz = 0.06f;
            if (!TryGetShipMassBoxWaterlineExtents(model, scale, out var length, out _, out _, out var mass))
            {
                maxAmpRad = baseDeg.DegToRad();
                omega = 2f * MathF.PI * baseHz;
                return;
            }

            const float refLength = 14f;
            const float refMass = 85000f;

            var lenRatio = Math.Clamp(refLength / length, 0.35f, 2.5f);
            var massRatio = Math.Clamp(MathF.Sqrt(refMass / mass), 0.45f, 2.2f);
            var ampMul = MathF.Pow(lenRatio, 0.38f) * MathF.Pow(massRatio, 0.28f);
            var maxDeg = Math.Clamp(baseDeg * ampMul, 1.1f, 5.5f);
            maxAmpRad = maxDeg.DegToRad();

            var freqMul = MathF.Pow(Math.Clamp(length / refLength, 0.5f, 2.2f), -0.18f);
            var hz = Math.Clamp(baseHz * freqMul, 0.042f, 0.078f);
            omega = 2f * MathF.PI * hz;
        }

        /// <summary>
        /// Visual-only pitch oscillation on open water. Does not affect rigid body.
        /// </summary>
        private static float ComputeVisualWavePitchOnWater(Slave slave, RigidBody rigidBody, float dt)
        {
            var grounded = slave.CachedFloorLevel > slave.CachedWaterSurface || slave.GroundContactLatched;
            if (grounded)
                return 0f;

            var submerged = MathF.Max(0f, slave.CachedWaterSurface - rigidBody.Position.Y);
            const float submergedForFullAmp = 0.32f;
            var depthMul = Math.Clamp(submerged / submergedForFullAmp, 0f, 1f);
            if (depthMul <= 0f)
                return 0f;

            GetVisualWavePitchModelFactors(slave.ShipController?.ShipModel, slave.Scale, out var maxAmpRad, out var omega);
            slave.WavePitchPhase += omega * dt;
            if (slave.WavePitchPhase > MathF.PI * 4000f)
                slave.WavePitchPhase -= MathF.PI * 4000f;

            var phaseOff = (slave.ObjId & 511) * 0.211f;
            return MathF.Sin(slave.WavePitchPhase + phaseOff) * maxAmpRad * depthMul;
        }

        /// <summary>
        /// Max visual bank (degrees) for turn lean from ship_models mass box and mass.
        /// </summary>
        private static float ComputeVisualMaxBankDegFromShipModel(ShipModel model, float scale)
        {
            if (!TryGetShipMassBoxWaterlineExtents(model, scale, out var length, out var beam, out var height, out var mass))
                return 8f;

            const float refLength = 14f;
            const float refBeam = 1.5f;
            const float refHeight = 16f;
            const float refMass = 85000f;
            const float baseDeg = 9f;

            var lengthFactor = MathF.Pow(Math.Clamp(length / refLength, 0.35f, 2.8f), 0.22f);
            var beamFactor = MathF.Pow(Math.Clamp(refBeam / beam, 0.65f, 1.6f), 0.28f);
            var massFactor = MathF.Pow(Math.Clamp(refMass / mass, 0.2f, 4f), 0.18f);
            var heightFactor = MathF.Pow(Math.Clamp(refHeight / height, 0.5f, 2f), 0.12f);

            var deg = baseDeg * lengthFactor * beamFactor * massFactor * heightFactor;
            return Math.Clamp(deg, 5f, 14f);
        }

        public void Stop()
        {
            ThreadRunning = false;

            // Дожидаемся завершения потока физики, если он существует
            if (_thread != null && _thread.IsAlive)
            {
                try
                {
                    // Даем потоку немного времени на завершение
                    if (!_thread.Join(TimeSpan.FromSeconds(5)))
                    {
                        Logger.Warn("Физический поток не завершился вовремя, прерывание...");
                        // В .NET Core/Framework не рекомендуется использовать Abort(),
                        // но в крайних случаях это может помочь избежать зависания
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Ошибка при ожидании завершения физического потока");
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _physWorld?.Dispose();
            }
            catch (AccessViolationException ex)
            {
                Logger.Error(ex, "AccessViolationException при освобождении физического мира");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Ошибка при освобождении физического мира");
            }
        }

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

