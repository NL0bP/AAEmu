using System;
using System.Collections.Generic;
using System.Numerics;

namespace AAEmu.Game.Models.Game.Transfers.Paths
{
    [Serializable]
    public class TransfersPathPoint : IComparable<TransfersPathPoint>, IComparable
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public short VelX { get; set; }
        public short VelY { get; set; }
        public short VelZ { get; set; }

        public short RotationX { get; set; }
        public short RotationY { get; set; }
        public short RotationZ { get; set; }

        public float AngVelX { get; set; }
        public float AngVelY { get; set; }
        public float AngVelZ { get; set; }

        public int Steering { get; set; }
        public int PathPointIndex { get; set; }
        public float Speed { get; set; }
        public byte Reverse { get; set; }

        private const float tolerance = 1.0f;

        public TransfersPathPoint()
        {
        }

        public TransfersPathPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public TransfersPathPoint(float x, float y, float z, short rotationZ)
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
                        return (Math.Abs(temp.X - X) < tolerance && Math.Abs(temp.Y - Y) < tolerance);
                    }
                case Vector3 vector3:
                    {
                        var temp = vector3;
                        return (Math.Abs(temp.X - X) < tolerance && Math.Abs(temp.Y - Y) < tolerance && Math.Abs(temp.Z - Z) < tolerance);
                    }
                case TransfersPathPoint other:
                    //return this.X.Equals(other.X) && this.Y.Equals(other.Y) && this.Z.Equals(other.Z) && this.RotationZ == 0;
                    return Steering.Equals(other.Steering) && PathPointIndex.Equals(other.PathPointIndex);

                default:
                    return false;
            }
        }

        public bool Equals(TransfersPathPoint other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && RotationZ == 0;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                //var hashCode = this.X.GetHashCode();
                //hashCode = (hashCode * 397) ^ this.Y.GetHashCode();
                //hashCode = (hashCode * 397) ^ this.Z.GetHashCode();
                //hashCode = (hashCode * 397) ^ this.RotationZ.GetHashCode();
                //return hashCode;
                var hashCode = Steering.GetHashCode();
                hashCode = (hashCode * 397) ^ PathPointIndex.GetHashCode();
                return hashCode;
            }
        }

        public int CompareTo(TransfersPathPoint other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            var steeringComparison = Steering.CompareTo(other.Steering);
            if (steeringComparison != 0)
            {
                return steeringComparison;
            }

            return PathPointIndex.CompareTo(other.PathPointIndex);
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

            return obj is TransfersPathPoint other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(TransfersPathPoint)}");
        }

        public static bool operator <(TransfersPathPoint left, TransfersPathPoint right)
        {
            return Comparer<TransfersPathPoint>.Default.Compare(left, right) < 0;
        }

        public static bool operator >(TransfersPathPoint left, TransfersPathPoint right)
        {
            return Comparer<TransfersPathPoint>.Default.Compare(left, right) > 0;
        }

        public static bool operator <=(TransfersPathPoint left, TransfersPathPoint right)
        {
            return Comparer<TransfersPathPoint>.Default.Compare(left, right) <= 0;
        }

        public static bool operator >=(TransfersPathPoint left, TransfersPathPoint right)
        {
            return Comparer<TransfersPathPoint>.Default.Compare(left, right) >= 0;
        }
    }
}
