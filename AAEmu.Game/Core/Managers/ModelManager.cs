using System.Collections.Generic;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Utils.DB;

namespace AAEmu.Game.Core.Managers
{
    namespace AAEmu.Game.Core.Managers
    {
        public class ModelManager : Singleton<ModelManager>
        {

            private Dictionary<string, Dictionary<uint, Model>> _models;
            private Dictionary<uint, ModelType> _modelTypes;
            private Dictionary<uint, GameStance> _gameStances;
            private bool _loaded = false;

            // Getters
            public ModelType GetModelType(uint modelId)
            {
                if (_modelTypes.TryGetValue(modelId, out var res))
                    return res;
                return null;
            }

            public ActorModel GetActorModel(uint modelId)
            {
                if (!_modelTypes.ContainsKey(modelId))
                    return null;

                var modelType = _modelTypes[modelId];

                if (!_models.ContainsKey(modelType.SubType) || !_models[modelType.SubType].ContainsKey(modelType.SubId))
                    return null;

                var model = _models[modelType.SubType][modelType.SubId];
                if (model is ActorModel actorModel)
                    return actorModel;
                return null;
            }

            public ShipModel GetShipModel(uint modelId)
            {
                if (!_modelTypes.ContainsKey(modelId))
                    return null;

                var modelType = _modelTypes[modelId];

                if (!_models.ContainsKey(modelType.SubType) || !_models[modelType.SubType].ContainsKey(modelType.SubId))
                    return null;

                var model = _models[modelType.SubType][modelType.SubId];
                if (model is ShipModel shipModel)
                    return shipModel;
                return null;
            }

            public VehicleModel GetVehicleModels(uint modelId)
            {
                if (!_modelTypes.ContainsKey(modelId))
                    return null;

                var modelType = _modelTypes[modelId];

                if (!_models.ContainsKey(modelType.SubType) || !_models[modelType.SubType].ContainsKey(modelType.SubId))
                    return null;

                var model = _models[modelType.SubType][modelType.SubId];
                if (model is VehicleModel vehicleModel)
                    return vehicleModel;
                return null;
            }

            public bool IsFlyOrSwim(uint modelId)
            {
                if (!_modelTypes.ContainsKey(modelId))
                    return false;
                var modelType = _modelTypes[modelId];
                if (!_models.ContainsKey(modelType.SubType) || !_models[modelType.SubType].ContainsKey(modelType.SubId))
                    return false;
                var model = _models[modelType.SubType][modelType.SubId];
                return model is ActorModel { MovementId: 2 };
            }

            public void Load()
            {
                if (_loaded)
                    return;

                _models = new Dictionary<string, Dictionary<uint, Model>>
                {
                    {"ActorModel", new Dictionary<uint, Model>()},
                    {"VehicleModel", new Dictionary<uint, Model>()},
                    {"PrefabModel", new Dictionary<uint, Model>()},
                    {"ShipModel", new Dictionary<uint, Model>()}
                };

                _modelTypes = new Dictionary<uint, ModelType>();
                _gameStances = new Dictionary<uint, GameStance>();

                using (var connection = SQLite.CreateConnection())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM actor_models";
                        command.Prepare();
                        using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                        {
                            while (reader.Read())
                            {
                                var model = new ActorModel()
                                {
                                    Id = reader.GetUInt32("id"),
                                    Radius = reader.GetFloat("radius"),
                                    Height = reader.GetFloat("height"),
                                    MovementId = reader.GetInt32("movement_id")
                                };

                                _models["ActorModel"].TryAdd(model.Id, model);
                            }
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM ship_models";
                        command.Prepare();
                        using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                        {
                            while (reader.Read())
                            {
                                var model = new ShipModel();
                                model.Id = reader.GetUInt32("id");
                                model.Accel = reader.GetFloat("accel");
                                model.AccelExponent = reader.GetFloat("accel_exponent");
                                model.CharAnimSteerBackwardId = reader.GetInt32("char_anim_steer_backward_id");
                                model.CharAnimSteerForwardId = reader.GetInt32("char_anim_steer_forward_id");
                                model.CollisionBoxCenterX = reader.GetFloat("collision_box_center_x");
                                model.CollisionBoxCenterY = reader.GetFloat("collision_box_center_y");
                                model.CollisionBoxCenterZ = reader.GetFloat("collision_box_center_z");
                                model.CollisionBoxOffsetX = reader.GetFloat("collision_box_offset_x");
                                model.CollisionBoxOffsetY = reader.GetFloat("collision_box_offset_y");
                                model.CollisionBoxOffsetZ = reader.GetFloat("collision_box_offset_z");
                                model.CollisionBoxScaleX = reader.GetFloat("collision_box_scale_x");
                                model.CollisionBoxScaleY = reader.GetFloat("collision_box_scale_y");
                                model.CollisionBoxScaleZ = reader.GetFloat("collision_box_scale_z");
                                model.CollisionBoxSizeX = reader.GetFloat("collision_box_size_x");
                                model.CollisionBoxSizeY = reader.GetFloat("collision_box_size_y");
                                model.CollisionBoxSizeZ = reader.GetFloat("collision_box_size_z");
                                model.CollisionSphereRadius = reader.GetFloat("collision_sphere_radius");
                                model.Damaged25 = reader.GetString("damaged25");
                                model.Damaged50 = reader.GetString("damaged50");
                                model.Damaged75 = reader.GetString("damaged75");
                                model.Dead = reader.GetString("dead");
                                model.HaltRate = reader.GetFloat("halt_rate");
                                model.ImpactMass = reader.GetFloat("impact_mass");
                                model.KeelHeight = reader.GetFloat("keel_height");
                                model.KeelLength = reader.GetFloat("keel_length");
                                model.KeelOffsetZ = reader.GetFloat("keel_offset_z");
                                model.Mass = reader.GetFloat("mass");
                                model.MassBoxSizeX = reader.GetFloat("mass_box_size_x");
                                model.MassBoxSizeY = reader.GetFloat("mass_box_size_y");
                                model.MassBoxSizeZ = reader.GetFloat("mass_box_size_z");
                                model.MassCenterX = reader.GetFloat("mass_center_x");
                                model.MassCenterY = reader.GetFloat("mass_center_y");
                                model.MassCenterZ = reader.GetFloat("mass_center_z");
                                model.MaxRpmSec = reader.GetFloat("max_rpm_sec");
                                model.MinRpmSec = reader.GetFloat("min_rpm_sec");
                                model.Normal = reader.GetString("normal");
                                model.PassengerBoxOffsetX = reader.GetFloat("passenger_box_offset_x");
                                model.PassengerBoxOffsetY = reader.GetFloat("passenger_box_offset_y");
                                model.PassengerBoxOffsetZ = reader.GetFloat("passenger_box_offset_z");
                                model.PassengerBoxScaleX = reader.GetFloat("passenger_box_scale_x");
                                model.PassengerBoxScaleY = reader.GetFloat("passenger_box_scale_y");
                                model.PassengerBoxScaleZ = reader.GetFloat("passenger_box_scale_z");
                                model.ReverseAccel = reader.GetFloat("reverse_accel");
                                model.ReverseVelocity = reader.GetFloat("reverse_velocity");
                                model.SteerVel = reader.GetFloat("steer_vel");
                                model.SubmergeAscendSpeed = reader.GetFloat("submerge_ascend_speed");
                                model.SubmergeDescendSpeed = reader.GetFloat("submerge_descend_speed");
                                model.TubeLength = reader.GetFloat("tube_length");
                                model.TubeOffsetZ = reader.GetFloat("tube_offset_z");
                                model.TubeRadius = reader.GetFloat("tube_radius");
                                model.TurnAccel = reader.GetFloat("turn_accel");
                                model.Velocity = reader.GetFloat("velocity");
                                model.WaterDamping = reader.GetFloat("water_damping");
                                model.WaterDensity = reader.GetFloat("water_density");
                                model.WaterResistance = reader.GetFloat("water_resistance");

                                _models["ShipModel"].TryAdd(model.Id, model);
                            }
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM vehicle_models";
                        command.Prepare();
                        using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                        {
                            while (reader.Read())
                            {
                                var model = new VehicleModel()
                                {
                                    Id = reader.GetUInt32("id"),
                                    LinInertia = reader.GetFloat("lin_inertia"),
                                    LinDeaccelInertia = reader.GetFloat("lin_deaccel_inertia"),
                                    RotInertia = reader.GetFloat("rot_inertia"),
                                    RotDeaccelInertia = reader.GetFloat("rot_deaccel_inertia"),
                                    Velocity = reader.GetFloat("velocity"),
                                    AngVel = reader.GetFloat("angVel"),
                                    CanFly = reader.GetFloat("can_fly"),
                                    WheeledVehicleMass = reader.GetFloat("wheeled_vehicle_mass"),
                                    WheeledVehiclePower = reader.GetFloat("wheeled_vehicle_power"),
                                    WheeledVehicleBrakeTorque = reader.GetFloat("wheeled_vehicle_brake_torque"),
                                    WheeledVehicleMaxGear = reader.GetUInt32("wheeled_vehicle_max_gear"),
                                    WheeledVehicleGearSpeedRatioReverse = reader.GetFloat("wheeled_vehicle_gear_speed_ratio_reverse"),
                                    WheeledVehicleGearSpeedRatio1 = reader.GetFloat("wheeled_vehicle_gear_speed_ratio_1"),
                                    WheeledVehicleGearSpeedRatio2 = reader.GetFloat("wheeled_vehicle_gear_speed_ratio_2"),
                                    WheeledVehicleGearSpeedRatio3 = reader.GetFloat("wheeled_vehicle_gear_speed_ratio_3"),
                                    WheeledVehicleSuspStroke = reader.GetFloat("wheeled_vehicle_susp_stroke"),
                                    WheeledVehicleDrive = reader.GetFloat("wheeled_vehicle_drive"),
                                    WheeledVehicleFrontOptimalSa = reader.GetFloat("wheeled_vehicle_front_optimal_sa"),
                                    WheeledVehicleRearOptimalSa = reader.GetFloat("wheeled_vehicle_rear_optimal_sa")
                                };

                                _models["VehicleModel"].TryAdd(model.Id, model);
                            }
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM models";
                        command.Prepare();
                        using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                        {
                            while (reader.Read())
                            {
                                var model = new ModelType()
                                {
                                    Id = reader.GetUInt32("id"),
                                    SubId = reader.GetUInt32("sub_id"),
                                    SubType = reader.GetString("sub_type")
                                };

                                _modelTypes.TryAdd(model.Id, model);
                            }
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM game_stances";
                        command.Prepare();
                        using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                        {
                            while (reader.Read())
                            {
                                var stance = new GameStance()
                                {
                                    Id = reader.GetUInt32("id"),
                                    ActorModelId = reader.GetUInt32("actor_model_id"),
                                    StanceId = (GameStanceType)(reader.GetByte("stance_id")-1), // This seems to be +1 in the DB compared to the packets
                                    Name = reader.GetString("name"),
                                    AiMoveSpeedRun = reader.GetFloat("ai_move_speed_run"),
                                    AiMoveSpeedSlow = reader.GetFloat("ai_move_speed_slow"),
                                    AiMoveSpeedSprint = reader.GetFloat("ai_move_speed_sprint"),
                                    AiMoveSpeedWalk = reader.GetFloat("ai_move_speed_walk"),
                                    HeightCollider = reader.GetFloat("height_collider"),
                                    HeightPivot = reader.GetFloat("height_pivot"),
                                    MaxSpeed = reader.GetFloat("max_speed"),
                                    ModelOffset = new Vector3(reader.GetFloat("model_offset_x"), reader.GetFloat("model_offset_y"), reader.GetFloat("model_offset_z")),
                                    NormalSpeed = reader.GetFloat("normal_speed"),
                                    Size = new Vector3(reader.GetFloat("size_x"), reader.GetFloat("size_y"), reader.GetFloat("size_z")),
                                    UseCapsule = reader.GetBoolean("use_capsule", true),
                                    ViewOffset = new Vector3(reader.GetFloat("view_offset_x"), reader.GetFloat("view_offset_y"), reader.GetFloat("view_offset_z")),
                                };

                                _gameStances.TryAdd(stance.Id, stance);
                                if (_models["ActorModel"].TryGetValue(stance.ActorModelId, out var m))
                                {
                                    var actorModel = m as ActorModel;
                                    if (actorModel != null)
                                        actorModel.Stances.Add(stance.StanceId, stance);
                                }
                            }
                        }
                    }

                }

                _loaded = true;
            }
        }
    }
}
