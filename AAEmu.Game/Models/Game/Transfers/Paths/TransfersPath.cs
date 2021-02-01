using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;

using NLog;

namespace AAEmu.Game.Models.Game.Transfers.Paths
{
    [Serializable]
    public sealed class TransfersPath : IComparable<TransfersPath>, IComparable
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        // это нужно записывать в json
        public uint ObjId { get; set; }     // здесь будет objId
        public string Name { get; set; }
        public uint Type { get; set; }   // TemplateId -> owner_id != 0, указывает на участок начала пути
        public int CellX { get; set; }
        public int CellY { get; set; }
        public List<TransfersPathPoint> Pos { get; set; }
        // =================================
        //                       objId, TemplateId
        public static Dictionary<uint, uint> types = new Dictionary<uint, uint>();
        //                         objId, 
        public static Dictionary<uint, TransfersPath> paths = new Dictionary<uint, TransfersPath>();
        public static List<TransfersPath> Paths { get { return paths.Values.ToList(); } }

        public TransfersPath()
        {
            Name = String.Empty;
            Pos = new List<TransfersPathPoint>();
        }

        /// <summary>
        /// Получить спарсенные из логов пути для транспорта
        /// </summary>
        /// <returns></returns>
        public static void GetPaths()
        {
            Clear();
            var contents = FileManager.GetFileContents($"{FileManager.AppPath}Data/Worlds/main_world/transfer_paths.json");
            if (string.IsNullOrWhiteSpace(contents))
            {
                _log.Warn($"File {FileManager.AppPath}Data/Worlds/main_world/transfer_paths.json doesn't exists or is empty.");
            }
            else
            {
                if (JsonHelper.TryDeserializeObject(contents, out List<TransfersPath> spawners, out _))
                {
                    foreach (var spawner in spawners)
                    {
                        AddTransfer(spawner);
                    }
                }
                else
                {
                    throw new Exception(
                        $"SpawnManager: Parse {FileManager.AppPath}Data/Worlds/main_world/transfer_paths.json file");
                }
            }
        }

        public static void AddTransfer(uint objid, TransfersPathPoint position)
        {
            var transfer = new TransfersPath
            {
                ObjId = objid,
                Name = ""
            };
            try
            {
                transfer.Type = types[objid]; // get templateId
            }
            catch (Exception e)
            {
                return;

            }

            if (paths.TryGetValue(objid, out var val))
            {
                // value exists!
                //val.Pos.Add(position);
                transfer.AddOrUpdatePos(val.Pos, position);
                paths[objid] = val;
            }
            else
            {
                // add the value
                transfer.Pos.Add(position);
                paths.Add(objid, transfer);
            }
        }

        public void AddOrUpdatePos(List<TransfersPathPoint> pos, TransfersPathPoint position)
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
                p.X = position.X;
                p.Y = position.Y;
                p.Z = position.Y;
                p.VelX = position.VelX;
                p.VelY = position.VelY;
                p.VelZ = position.VelZ;
                p.RotationX = position.RotationY;
                p.RotationY = position.RotationY;
                p.RotationZ = position.RotationZ;
                p.AngVelX = position.AngVelX;
                p.AngVelY = position.AngVelY;
                p.AngVelZ = position.AngVelZ;
                p.Steering = position.Steering;
                p.PathPointIndex = position.PathPointIndex;
                p.Speed = position.Speed;
                p.Reverse = position.Reverse;
            }

            if (!valueExists)
            {
                // darn, lets add the value			
                pos.Add(position);
            }
        }
        public void AddOrUpdatePos0(List<TransfersPathPoint> pos, TransfersPathPoint position)
        {
            foreach (var p in pos)
            {
                if (p.X.Equals(position.X) && p.Y.Equals(position.Y) && p.Z.Equals(position.Z)
                    //&& p.Speed.Equals(position.Speed)
                    && p.VelX.Equals(position.VelX) && p.VelY.Equals(position.VelY) && p.VelZ.Equals(position.VelZ)
                //&& p.RotationZ.Equals(position.RotationZ)
                )
                {
                    // yay, value exists!
                    p.X = position.X;
                    p.Y = position.Y;
                    p.Z = position.Y;
                    p.VelX = position.VelX;
                    p.VelY = position.VelY;
                    p.VelZ = position.VelZ;
                    p.RotationX = position.RotationY;
                    p.RotationY = position.RotationY;
                    p.RotationZ = position.RotationZ;
                    p.AngVelX = position.AngVelX;
                    p.AngVelY = position.AngVelY;
                    p.AngVelZ = position.AngVelZ;
                    p.Steering = position.Steering;
                    p.PathPointIndex = position.PathPointIndex;
                    p.Speed = position.Speed;
                    p.Reverse = position.Reverse;
                    break;
                }
                // darn, lets add the value
                pos.Add(position);
                break;
            }
        }
        public void AddOrUpdatePos1(List<TransfersPathPoint> pos, TransfersPathPoint position)
        {
            foreach (var p in pos)
            {
                if (p.X.Equals(position.X) && p.Y.Equals(position.Y) && p.Z.Equals(position.Z)
                )
                {
                    // yay, value exists!
                    p.X = position.X;
                    p.Y = position.Y;
                    p.Z = position.Y;
                    p.VelX = position.VelX;
                    p.VelY = position.VelY;
                    p.VelZ = position.VelZ;
                    p.RotationX = position.RotationY;
                    p.RotationY = position.RotationY;
                    p.RotationZ = position.RotationZ;
                    p.AngVelX = position.AngVelX;
                    p.AngVelY = position.AngVelY;
                    p.AngVelZ = position.AngVelZ;
                    p.Steering = position.Steering;
                    p.PathPointIndex = position.PathPointIndex;
                    p.Speed = position.Speed;
                    p.Reverse = position.Reverse;
                    break;
                }
                // darn, lets add the value
                pos.Add(position);
                break;
            }
        }

        public static void AddOrUpdate(uint key, uint newValue)
        {
            if (types.TryGetValue(key, out var val))
            {
                // yay, value exists!
                types[key] = val + newValue;
            }
            else
            {
                // darn, lets add the value
                types.Add(key, newValue);
            }
        }

        public static void AddTransfer(TransfersPath spawner)
        {
            if (!paths.ContainsKey(spawner.ObjId))
            {
                paths.Add(spawner.ObjId, spawner);
            }
            // сортируем по Steering и PathPointIndex
            spawner.Pos = spawner.Pos.OrderBy(x => x.Steering).ThenBy(x => x.PathPointIndex).ToList();
        }

        public static void AddOrUpdate(Dictionary<uint, uint> dic, uint key, uint newValue)
        {
            if (dic.TryGetValue(key, out var val))
            {
                // yay, value exists!
                dic[key] = val + newValue;
            }
            else
            {
                // darn, lets add the value
                dic.Add(key, newValue);
            }
        }

        public static void AddTransferEquals(uint objid, uint tid, TransfersPathPoint position)
        {
            foreach (var npc in paths)
            {
                //if (npc.Value.Position.Equals(position))
                //if (npc.Value.Position != null && npc.Value != null && position != null && npc.Value.Position.Equals(new Vector2 { X = position.X, Y = position.Y }))
                //{
                //	//npc.Value.Title = title;
                //	npc.Value.Position = position;
                //	break;
                //}
            }
        }

        public static void Clear()
        {
            paths.Clear();
        }

        public int CompareTo(TransfersPath other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            return Type.CompareTo(other.Type);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return 1;
            }

            if (ReferenceEquals(this, obj))
            {
                return 0;
            }

            return obj is TransfersPath other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(TransfersPath)}");
        }

        public static bool operator <(TransfersPath left, TransfersPath right)
        {
            return Comparer<TransfersPath>.Default.Compare(left, right) < 0;
        }

        public static bool operator >(TransfersPath left, TransfersPath right)
        {
            return Comparer<TransfersPath>.Default.Compare(left, right) > 0;
        }

        public static bool operator <=(TransfersPath left, TransfersPath right)
        {
            return Comparer<TransfersPath>.Default.Compare(left, right) <= 0;
        }

        public static bool operator >=(TransfersPath left, TransfersPath right)
        {
            return Comparer<TransfersPath>.Default.Compare(left, right) >= 0;
        }
    }
}
