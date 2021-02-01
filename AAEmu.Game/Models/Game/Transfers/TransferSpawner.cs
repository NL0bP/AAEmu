using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Transfers.Paths;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Route;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Utils;

using NLog;

namespace AAEmu.Game.Models.Game.Transfers
{
    public class TransferSpawner : Spawner<Transfer>
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private readonly List<Transfer> _spawned;
        private Transfer _lastSpawn;
        private int _scheduledCount;
        public int _spawnCount;
        public float RotationX;
        public float RotationY;
        public float RotationZ;

        public uint Count { get; set; }

        public TransferSpawner()
        {
            _spawned = new List<Transfer>();
            Count = 1;
        }

        public List<Transfer> SpawnAll()
        {
            var list = new List<Transfer>();
            for (var num = _scheduledCount; num < Count; num++)
            {
                var transfer = Spawn(0);
                if (transfer != null)
                {
                    list.Add(transfer);
                }
            }

            return list;
        }

        public override Transfer Spawn(uint objId)
        {
            var transfer = TransferManager.Instance.Create(objId, UnitId, this);
            if (transfer == null)
            {
                _log.Warn("TransfersPath {0}, from spawn not exist at db", UnitId);
                return null;
            }

            transfer.Spawner = this;
            transfer.Position = Position.Clone();
            transfer.GameTime = DateTime.Now;
            transfer.SpawnTime = DateTime.Now;
            if (transfer.Position == null)
            {
                _log.Error("Can't spawn transfer {1} from spawn {0}", Id, UnitId);
                return null;
            }

            // использование путей из логов с помощью файла transfer_paths.json
            //if (transfer.TemplateId != 46 && transfer.TemplateId != 4 && transfer.TemplateId != 122)
            //{
            //    //if (
            //    //transfer.TemplateId == 52
            //    //|| transfer.TemplateId == 8
            //    //|| transfer.TemplateId == 10
            //    //|| transfer.TemplateId == 49
            //    //|| transfer.TemplateId == 50
            //    //|| transfer.TemplateId == 52
            //    //|| transfer.TemplateId == 65
            //    //|| transfer.TemplateId == 103
            //    //|| transfer.TemplateId == 110
            //    //|| transfer.TemplateId == 114
            //    //|| transfer.TemplateId == 120
            //    //|| transfer.TemplateId == 130
            //    //|| transfer.TemplateId == 131
            //    //|| transfer.TemplateId == 132
            //    //|| transfer.TemplateId == 133
            //    //|| transfer.TemplateId == 134
            //    //    )
            //    {
            //        if (!transfer.IsInPatrol)
            //        {
            //            var path = new Simulation(transfer, transfer.Template.PathSmoothing);
            //            // организуем последовательность "Дорог" для следования "Транспорта"
            //            //for (var i = 0; i < TransfersPath.Paths.Count; i++)
            //            //{
            //            var idxSteering = 0;
            //            //var idxPathPointIndex = 0;
            //            var lpp = new List<TransfersPathPoint>();
            //            foreach (var p in TransfersPath.Paths)
            //            {
            //                if (p.Type != transfer.TemplateId) { continue; }

            //                foreach (var pp in p.Pos)
            //                {
            //                    if (pp.Steering == idxSteering)
            //                    {
            //                        lpp.Add(pp);
            //                    }
            //                    else
            //                    {
            //                        path.Routes2.TryAdd(idxSteering, lpp);
            //                        idxSteering = pp.Steering;
            //                        lpp = new List<TransfersPathPoint>();
            //                    }
            //                }
            //                path.Routes2.TryAdd(idxSteering, lpp);
            //                break;
            //            }
            //            //}
            //            var go = true;
            //            if (path.Routes2.Count == 0)
            //            {
            //                go = false;
            //            }
            //            else
            //            {
            //                if (path.Routes2.Any(route => route.Value.Count == 0))
            //                {
            //                    go = false;
            //                }
            //            }
            //            //if (path.Routes2.Count != 0)
            //            if(go)
            //            {

            //                path.LoadTransferPathFromRoutes2(0); // начнем с самого начала
            //                                                     //_log.Warn("TransfersPath #" + transfer.TemplateId);
            //                                                     //_log.Warn("First spawn myX=" + transfer.Position.X + " myY=" + transfer.Position.Y + " myZ=" + transfer.Position.Z + " rotZ=" + transfer.Rot.Z + " rotationZ=" + transfer.Position.RotationZ);
            //                transfer.IsInPatrol = true; // so as not to run the route a second time

            //                transfer.Steering = 0;
            //                transfer.PathPointIndex = 0;

            //                // попробуем заспавнить в последней точке пути (она как раз напротив стоянки)
            //                // попробуем смотреть на следующую точку

            //                var point = path.Routes2[0][0];
            //                var point2 = path.Routes2[0][1];

            //                var vPosition = new Vector3(point.X, point.Y, point.Z);
            //                var vTarget = new Vector3(point2.X, point2.Y, point2.Z);
            //                path.Angle = MathUtil.CalculateDirection(vPosition, vTarget);
            //                transfer.Position.RotationZ = MathUtil.ConvertDegreeToDirection(MathUtil.RadianToDegree(path.Angle));
            //                transfer.RotationZ = (short)point.RotationZ;

            //                transfer.Position.WorldId = 1;
            //                transfer.Position.ZoneId = transfer.Template.TransferRoads[0].ZoneId;
            //                transfer.Position.ZoneId = WorldManager.Instance.GetZoneId(transfer.Position.WorldId, point.X, point.Y);
            //                transfer.Position.X = point.X;
            //                transfer.Position.Y = point.Y;
            //                transfer.Position.Z = point.Z;

            //                transfer.WorldPos = new WorldPos(Helpers.ConvertLongY(point.X), Helpers.ConvertLongY(point.Y), point.Z);
            //                _log.Warn("TransfersPath #" + transfer.TemplateId);
            //                _log.Warn("New spawn X={0}", transfer.Position.X);
            //                _log.Warn("New spawn Y={0}", transfer.Position.Y);
            //                _log.Warn("New spawn Z={0}", transfer.Position.Z);
            //                _log.Warn("transfer.RotationZ={0}, rotZ={1}, zoneId={2}", transfer.RotationZ, transfer.Position.RotationZ, transfer.Position.ZoneId);

            //                path.InPatrol = false;

            //                path.GoToPath2(transfer, true);
            //            }
            //            else
            //            {
            //                _log.Warn("PathName: " + transfer.Template.TransferPaths[0].PathName + " not found!");
            //            }
            //        }

            //    }
            //}

            // использование путей из клиента
            if (transfer.TemplateId != 46 && transfer.TemplateId != 4 && transfer.TemplateId != 122)
            {
                //if (transfer.TemplateId == 64)
                {
                    if (!transfer.IsInPatrol)
                    {
                        var path = new Simulation(transfer, transfer.Template.PathSmoothing);
                        // организуем последовательность "Дорог" для следования "Транспорта"
                        for (var i = 0; i < transfer.Template.TransferRoads.Count; i++)
                        {
                            path.Routes.TryAdd(i, transfer.Template.TransferRoads[i].Pos);
                        }
                        path.LoadTransferPathFromRoutes(0); // начнем с самого начала

                        if (path.Routes[0] != null)
                        {
                            //_log.Warn("TransfersPath #" + transfer.TemplateId);
                            //_log.Warn("First spawn myX=" + transfer.Position.X + " myY=" + transfer.Position.Y + " myZ=" + transfer.Position.Z + " rotZ=" + transfer.Rot.Z + " rotationZ=" + transfer.Position.RotationZ);
                            transfer.IsInPatrol = true; // so as not to run the route a second time

                            transfer.Steering = 0;
                            transfer.PathPointIndex = 0;

                            // попробуем заспавнить в последней точке пути (она как раз напротив стоянки)
                            // попробуем смотреть на следующую точку
                            //var point = path.Routes[path.Routes.Count - 1][path.Routes[path.Routes.Count - 1].Count - 1];
                            //var point = path.Routes[0][0];

                            // попробуем заспавнить в первой точке пути
                            // попробуем смотреть на следующую точку
                            var point = path.Routes[0][0];
                            var point2 = path.Routes[0][1];

                            var vPosition = new Vector3(point.X, point.Y, point.Z);
                            var vTarget = new Vector3(point2.X, point2.Y, point2.Z);
                            path.Angle = MathUtil.CalculateDirection(vPosition, vTarget);

                            transfer.Position.RotationZ = MathUtil.ConvertDegreeToDirection(MathUtil.RadianToDegree(path.Angle));

                            var quat = Quaternion.CreateFromYawPitchRoll((float)path.Angle, 0.0f, 0.0f);
                            transfer.Rot = new Quaternion(quat.X, quat.Z, quat.Y, quat.W);

                            transfer.Position.WorldId = 1;
                            transfer.Position.ZoneId = transfer.Template.TransferRoads[0].ZoneId;
                            transfer.Position.ZoneId = WorldManager.Instance.GetZoneId(transfer.Position.WorldId, point.X, point.Y);
                            transfer.Position.X = point.X;
                            transfer.Position.Y = point.Y;
                            transfer.Position.Z = point.Z;

                            transfer.WorldPos = new WorldPos(Helpers.ConvertLongX(point.X), Helpers.ConvertLongY(point.Y), point.Z);
                            _log.Warn("TransfersPath #" + transfer.TemplateId);
                            _log.Warn("New spawn X={0}", transfer.Position.X);
                            _log.Warn("New spawn Y={0}", transfer.Position.Y);
                            _log.Warn("New spawn Z={0}", transfer.Position.Z);
                            _log.Warn("transfer.Rot={0}, rotZ={1}, zoneId={2}", transfer.Rot, transfer.Position.RotationZ, transfer.Position.ZoneId);

                            path.InPatrol = false;

                            path.GoToPath(transfer, true);
                        }
                        else
                        {
                            _log.Warn("PathName: " + transfer.Template.TransferPaths[0].PathName + " not found!");
                        }
                    }

                }
            }


            #region transfer_paths_original.json
            // нужно для создания файла transfer_paths_original.json
            //if (transfer.TemplateId != 46 && transfer.TemplateId != 4 && transfer.TemplateId != 122)
            //{

            //    TransfersPath.types.Add(transfer.ObjId, transfer.TemplateId);

            //    var path = new Simulation(transfer, transfer.Template.PathSmoothing);
            //    // организуем последовательность "Дорог" для следования "Транспорта"
            //    var steering = 0;
            //    var patPointIndex = 0;

            //    for (var i = 0; i < transfer.Template.TransferRoads.Count; i++)
            //    {

            //        foreach (var p in transfer.Template.TransferRoads[i].Pos)
            //        {
            //            AddPathAndUpdatePos(transfer.ObjId, p, transfer.TemplateId, steering, patPointIndex);
            //            //AddOrUpdatePos(transfers[transfer.ObjId].Pos, Transfers[i].Pos[i] , p);
            //            patPointIndex++;
            //        }

            //        steering++;
            //        patPointIndex = 0;
            //    }

            //}
            #endregion transfer_paths_original.json

            transfer.Spawn();
            _lastSpawn = transfer;
            _spawned.Add(transfer);
            _scheduledCount--;
            _spawnCount++;

            return transfer;
        }

        /// <summary>
        /// нужно для создания файла transfer_paths_original.json

        /// </summary>
        /// <param name="objid"></param>
        /// <param name="position2"></param>
        /// <param name="type"></param>
        /// <param name="steering"></param>
        /// <param name="pathPointIndex"></param>
        public static void AddPathAndUpdatePos(uint objid, Point position2, uint type, int steering, int pathPointIndex)
        {
            var path = new TransfersPath
            {
                ObjId = objid,
                Name = "",
                Type = type
            };
            //try
            //{
            //    path.Type = types[objid]; // get templateId
            //}
            //catch (Exception e)
            //{
            //    return;

            //}

            var position = new TransfersPathPoint();
            if (TransfersPath.paths.TryGetValue(objid, out var val))
            {
                // value exists!
                position.X = position2.X;
                position.Y = position2.Y;
                position.Z = position2.Z;
                position.Steering = steering;
                position.PathPointIndex = pathPointIndex;
                path.AddOrUpdatePos1(val.Pos, position);
                TransfersPath.paths[objid] = val;
            }
            else
            {
                // add the value
                position.X = position2.X;
                position.Y = position2.Y;
                position.Z = position2.Z;
                position.Steering = steering;
                position.PathPointIndex = pathPointIndex;
                path.Pos.Add(position);
                TransfersPath.paths.Add(objid, path);
            }
        }

        /// <summary>
        /// нужно для создания файла transfer_paths_original.json
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="position"></param>
        /// <param name="position2"></param>
        public void AddOrUpdatePos(List<TransfersPathPoint> pos, TransfersPathPoint position, Point position2)
        {
            var valueExists = false;
            foreach (var p in pos.Where(p =>
                    p.Steering.Equals(position.Steering) &&
                    p.PathPointIndex.Equals(position.PathPointIndex)
                )
            )
            {
                valueExists = true;
                // yay, value exists!
                // закомменитовать если нужна первая точка
                // раскомментировать если нужна последняя точка
                p.X = position2.X;
                p.Y = position2.Y;
                p.Z = position2.Z;
                //p.VelX = position2.VelX;
                //p.VelY = position2.VelY;
                //p.VelZ = position2.VelZ;
                //                p.RotationX = position2.RotationY;
                //                p.RotationY = position2.RotationY;
                //                p.RotationZ = position2.RotationZ;
                //p.AngVelX = position2.AngVelX;
                //p.AngVelY = position2.AngVelY;
                //p.AngVelZ = position2.AngVelZ;
                //p.Steering = position2.Steering;
                //p.PathPointIndex = position2.PathPointIndex;
                //p.Speed = position2.Speed;
                //p.Reverse = position2.Reverse;
                // комментировать/раскомментировать до сюда
            }

            if (!valueExists)
            {
                // darn, lets add the value			
                position = new TransfersPathPoint
                {
                    X = position2.X,
                    Y = position2.Y,
                    Z = position2.Z
                };

                pos.Add(position);
            }
        }

        public override void Despawn(Transfer transfer)
        {
            transfer.Delete();
            if (transfer.Respawn == DateTime.MinValue)
            {
                _spawned.Remove(transfer);
                ObjectIdManager.Instance.ReleaseId(transfer.ObjId);
                _spawnCount--;
            }

            if (_lastSpawn == null || _lastSpawn.ObjId == transfer.ObjId)
            {
                _lastSpawn = _spawned.Count != 0 ? _spawned[_spawned.Count - 1] : null;
            }
        }
    }
}
