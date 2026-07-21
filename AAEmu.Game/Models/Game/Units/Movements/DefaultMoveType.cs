using AAEmu.Commons.Network;

namespace AAEmu.Game.Models.Game.Units.Movements;

public class DefaultMoveType : MoveType
{
    // Client reads/writes default rotation as Int16, not sbyte.
    public new short RotationX { get; set; }
    public new short RotationY { get; set; }
    public new short RotationZ { get; set; }

    public override void Read(PacketStream stream)
    {
        base.Read(stream);
        (X, Y, Z) = stream.ReadPosition();
        VelX = stream.ReadInt16();
        VelY = stream.ReadInt16();
        VelZ = stream.ReadInt16();
        RotationX = stream.ReadInt16();
        RotationY = stream.ReadInt16();
        RotationZ = stream.ReadInt16();
    }

    public override PacketStream Write(PacketStream stream)
    {
        base.Write(stream);
        stream.WritePosition(X, Y, Z);
        stream.Write(VelX);
        stream.Write(VelY);
        stream.Write(VelZ);
        stream.Write(RotationX);
        stream.Write(RotationY);
        stream.Write(RotationZ);
        return stream;
    }
}
