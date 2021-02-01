using System;
using System.Numerics;

using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Models.Game.Units.Movements
{
    public class TransferData : MoveType
    {
        public Vector3 AngVel { get; set; }
        // ---
        public float AngVelX { get; set; }
        public float AngVelY { get; set; }
        public float AngVelZ { get; set; }
        public short VelX { get; set; }
        public short VelY { get; set; }
        public short VelZ { get; set; }
        // ---
        public int Steering { get; set; }
        public int PathPointIndex { get; set; }
        public float Speed { get; set; }
        public bool Reverse { get; set; }

        // ---
        public float RotationDegrees { get; set; }
        public float RotSpeed { get; set; }  // ?
        public sbyte Throttle { get; set; } // ?
        // ---
        public new short RotationX { get; set; }
        public new short RotationY { get; set; }
        public new short RotationZ { get; set; }
        // ---

        public TransferData()
        {
            WorldPos = new WorldPos(Helpers.ConvertLongX(X), Helpers.ConvertLongY(Y), Z);
        }

        public void UseTransferBase(Transfer transfer)
        {
            //// Пробую сделать движение транспорта из спарсенных с лога точек пути
            //X = transfer.Position.X;
            //Y = transfer.Position.Y;
            //Z = transfer.Position.Z;
            ////WorldPos.X = transfer.WorldPos.X;
            ////WorldPos.Y = transfer.WorldPos.Y;
            ////WorldPos.Z = transfer.WorldPos.Z;
            //WorldPos = transfer.WorldPos;
            //Rot = transfer.Rot;
            //RotationX = transfer.RotationX;
            //RotationY = transfer.RotationY;
            //RotationZ = transfer.RotationZ;
            //RotSpeed = transfer.RotSpeed;
            //RotationDegrees = transfer.RotationDegrees;
            ////Velocity = transfer.Velocity;
            //VelX = transfer.VelX;
            //VelY = transfer.VelY;
            //VelZ = transfer.VelZ;
            ////AngVel = transfer.AngVel;
            //AngVelX = transfer.AngVelX;
            //AngVelY = transfer.AngVelY;
            //AngVelZ = transfer.AngVelZ;
            //Steering = transfer.Steering;
            //Throttle = transfer.Throttle;
            //PathPointIndex = transfer.PathPointIndex;
            //Speed = transfer.Speed;
            //Reverse = transfer.Reverse;
            //Time = (uint)(DateTime.Now - transfer.SpawnTime).TotalMilliseconds;

            // TODO
            X = transfer.Position.X;
            Y = transfer.Position.Y;
            Z = transfer.Position.Z;
            //WorldPos.X = transfer.WorldPos.X;
            //WorldPos.Y = transfer.WorldPos.Y;
            //WorldPos.Z = transfer.WorldPos.Z;
            WorldPos = transfer.WorldPos;
            Rot = transfer.Rot;
            //RotationX = transfer.RotationX;
            //RotationY = transfer.RotationY;
            //RotationZ = transfer.RotationZ;
            RotSpeed = transfer.RotSpeed;
            RotationDegrees = transfer.RotationDegrees;
            Velocity = transfer.Velocity;
            //VelX = transfer.VelX;
            //VelY = transfer.VelY;
            //VelZ = transfer.VelZ;
            AngVel = transfer.AngVel;
            //AngVelX = transfer.AngVelX;
            //AngVelY = transfer.AngVelY;
            //AngVelZ = transfer.AngVelZ;
            Steering = transfer.Steering;
            Throttle = transfer.Throttle;
            PathPointIndex = transfer.PathPointIndex;
            Speed = transfer.Speed;
            Reverse = transfer.Reverse;
            Time = (uint)(DateTime.Now - transfer.SpawnTime).TotalMilliseconds;
        }

        public override void Read(PacketStream stream)
        {
            base.Read(stream);
            (X, Y, Z) = stream.ReadPositionBc();
            //var (x, y, z) = stream.ReadWorldPosition();
            WorldPos = new WorldPos(Helpers.ConvertLongX(X), Helpers.ConvertLongY(Y), Z);

            //VelX = stream.ReadInt16();
            //VelY = stream.ReadInt16();
            //VelZ = stream.ReadInt16();
            //var tempX = VelX * 0.000030518509f * 30.0f;
            //var tempY = VelY * 0.000030518509f * 30.0f;
            //var tempZ = VelZ * 0.000030518509f * 30.0f;
            //Velocity = new Vector3(tempX, tempY, tempZ);

            var tempVelocity = stream.ReadVector3Short();
            Velocity = new Vector3(tempVelocity.X * 30f, tempVelocity.Y * 30f, tempVelocity.Z * 30f);
            VelX = (short)Velocity.X;
            VelY = (short)Velocity.Y;
            VelZ = (short)Velocity.Z;

            //RotationX = stream.ReadInt16();
            //RotationY = stream.ReadInt16();
            //RotationZ = stream.ReadInt16();
            ////-----------------
            //var x = Convert.ToSingle(RotationX * 0.000030518509f);
            //var y = Convert.ToSingle(RotationY * 0.000030518509f);
            //var z = Convert.ToSingle(RotationZ * 0.000030518509f);
            //x *= (float)Math.PI * 2; // переводим в радианы
            //y *= (float)Math.PI * 2;
            //z *= (float)Math.PI * 2;
            //var halfAngle = z * 0.5f;
            //var w = (float)Math.Cos(halfAngle);
            //x = (float)(Math.Sin(halfAngle) * x);
            //y = (float)(Math.Sin(halfAngle) * y);
            //z = (float)(Math.Sin(halfAngle) * z);
            //Rot = new Quaternion(x, y, z, w);
            ////-----------------
            Rot = stream.ReadQuaternionShort();
            //RotationX = 0;
            //RotationY = 0;
            //RotationZ = Helpers.ConvertRadianToShortDirection(Rot.Z);
            //-----------------

            //AngVelX = stream.ReadSingle();
            //AngVelY = stream.ReadSingle();
            //AngVelZ = stream.ReadSingle();
            AngVel = stream.ReadVector3Single();
            AngVelX = AngVel.X;
            AngVelY = AngVel.Y;
            AngVelZ = AngVel.Z;

            Steering = stream.ReadInt32();
            PathPointIndex = stream.ReadInt32();
            Speed = stream.ReadSingle();
            Reverse = stream.ReadBoolean();
        }

        //public override PacketStream Write(PacketStream stream)
        //{
        //    base.Write(stream);
        //    stream.WritePositionBc(X, Y, Z);
        //    //stream.WriteWorldPosition(WorldPos.X, WorldPos.Y, WorldPos.Z);

        //    stream.Write(VelX);
        //    stream.Write(VelY);
        //    stream.Write(VelZ);
        //    //Velocity = new Vector3(VelX, VelY, VelZ);
        //    //var tempVelocity = new Vector3(Velocity.X / 30f, Velocity.Y / 30f, Velocity.Z / 30f);
        //    //stream.WriteVector3Short(tempVelocity);

        //    stream.Write(RotationX);
        //    stream.Write(RotationY);
        //    stream.Write(RotationZ);
        //    //var angle = Helpers.ConvertDirectionToRadian(RotationZ);
        //    //Rot = new Quaternion(0f, 0f, angle, 1f);
        //    //stream.WriteQuaternionShort(Rot);

        //    //stream.Write(AngVelX);
        //    //stream.Write(AngVelY);
        //    //stream.Write(AngVelZ);
        //    AngVel = new Vector3(AngVelX, AngVelY, AngVelZ);
        //    stream.WriteVector3Single(AngVel);

        //    stream.Write(Steering);
        //    stream.Write(PathPointIndex);
        //    stream.Write(Speed);
        //    stream.Write(Reverse);

        //    return stream;
        //}
        public override PacketStream Write(PacketStream stream)
        {
            base.Write(stream);
            stream.WritePositionBc(X, Y, Z);
            //stream.WriteWorldPosition(WorldPos.X, WorldPos.Y, WorldPos.Z);

            //stream.Write(VelX);
            //stream.Write(VelY);
            //stream.Write(VelZ);
            //var tempVelocity = new Vector3(Velocity.X / 30f, Velocity.Y / 30f, Velocity.Z / 30f);
            stream.WriteVector3Short(new Vector3(Velocity.X * 0.033333f, Velocity.Y * 0.033333f, Velocity.Z * 0.033333f));

            //stream.Write(RotationX);
            //stream.Write(RotationY);
            //stream.Write(RotationZ);
            stream.WriteQuaternionShort(Rot);

            //stream.Write(AngVelX);
            //stream.Write(AngVelY);
            //stream.Write(AngVelZ);
            stream.WriteVector3Single(AngVel);
            stream.Write(Steering);
            stream.Write(PathPointIndex);
            stream.Write(Speed);
            stream.Write(Reverse);

            return stream;
        }
    }
}
