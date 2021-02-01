using System;
using System.Collections.Generic;
using System.Numerics;

using AAEmu.Game.Models.Game.Units.Movements;

namespace AAEmu.Game.Models.Game.Transfers.Paths
{
    [Serializable]
    public class NpcsPathPoint : IComparable<NpcsPathPoint>, IComparable
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public uint SkillId { get; set; }   // здесь будет SkillId

        public short VelX { get; set; }
        public short VelY { get; set; }
        public short VelZ { get; set; }

        public sbyte RotationX { get; set; }
        public sbyte RotationY { get; set; }
        public sbyte RotationZ { get; set; }

        public sbyte ActorDeltaMovementX { get; set; }
        public sbyte ActorDeltaMovementY { get; set; }
        public sbyte ActorDeltaMovementZ { get; set; }

        public EStance ActorStance { get; set; }
        public AiAlertness ActorAlertness { get; set; }
        public ActorMoveType ActorFlags { get; set; }
        public byte Flags { get; set; }

        private const float _tolerance = 1.0f;

        public NpcsPathPoint()
        {
        }

        public NpcsPathPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public NpcsPathPoint(float x, float y, float z, sbyte rotationZ)
        {
            X = x;
            Y = y;
            Z = z;
            RotationZ = rotationZ;
        }

        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case Vector2 vector2:
                    {
                        var temp = vector2;
                        return (Math.Abs(temp.X - X) < _tolerance && Math.Abs(temp.Y - Y) < _tolerance);
                    }
                case Vector3 vector3:
                    {
                        var temp = vector3;
                        return (Math.Abs(temp.X - X) < _tolerance && Math.Abs(temp.Y - Y) < _tolerance && Math.Abs(temp.Z - Z) < _tolerance);
                    }
                case NpcsPathPoint other:
                    return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
                //return this.Steering.Equals(other.Steering) && this.PathPointIndex.Equals(other.PathPointIndex);

                default:
                    return false;
            }
        }

        public bool Equals(NpcsPathPoint other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && RotationZ == 0;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Z.GetHashCode();
                //hashCode = (hashCode * 397) ^ this.RotationZ.GetHashCode();
                return hashCode;
                //            var hashCode = this.Steering.GetHashCode();
                //hashCode = (hashCode * 397) ^ this.PathPointIndex.GetHashCode();
                //return hashCode;
            }
        }

        //public int CompareTo(NpcPathPoint other)
        //{
        //	if (ReferenceEquals(this, other)) return 0;
        //	if (ReferenceEquals(null, other)) return 1;
        //	var steeringComparison = this.Steering.CompareTo(other.Steering);
        //	if (steeringComparison != 0) return steeringComparison;
        //	return this.PathPointIndex.CompareTo(other.PathPointIndex);
        //}

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

            return obj is NpcsPathPoint other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(NpcsPathPoint)}");
        }

        public static bool operator <(NpcsPathPoint left, NpcsPathPoint right)
        {
            return Comparer<NpcsPathPoint>.Default.Compare(left, right) < 0;
        }

        public static bool operator >(NpcsPathPoint left, NpcsPathPoint right)
        {
            return Comparer<NpcsPathPoint>.Default.Compare(left, right) > 0;
        }

        public static bool operator <=(NpcsPathPoint left, NpcsPathPoint right)
        {
            return Comparer<NpcsPathPoint>.Default.Compare(left, right) <= 0;
        }

        public static bool operator >=(NpcsPathPoint left, NpcsPathPoint right)
        {
            return Comparer<NpcsPathPoint>.Default.Compare(left, right) >= 0;
        }

        public int CompareTo(NpcsPathPoint other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            var xComparison = X.CompareTo(other.X);
            if (xComparison != 0)
            {
                return xComparison;
            }

            var yComparison = Y.CompareTo(other.Y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return Z.CompareTo(other.Z);
        }
    }
}
