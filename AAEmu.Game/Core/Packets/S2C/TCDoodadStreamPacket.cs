using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Stream;
using AAEmu.Game.Models.Game.DoodadObj;

namespace AAEmu.Game.Core.Packets.S2C;

public class TCDoodadStreamPacket(int id, int next, Doodad[] doodads) : StreamPacket(TCOffsets.TCDoodadStreamPacket)
{
    public override PacketLogLevel LogLevel => PacketLogLevel.Trace; // DIAG-3503: was Trace, for doodad stream diagnostics

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(id);             // id
        stream.Write(next);           // next
        stream.Write(doodads.Length); // count
        foreach (var doodad in doodads)
        {
            stream.WriteBc(doodad.ObjId);    // bc
            stream.Write(doodad.TemplateId); // type
            stream.WritePosition(doodad.Transform.World.Position.X, doodad.Transform.World.Position.Y, doodad.Transform.World.Position.Z);
            var (roll, pitch, yaw) = doodad.Transform.World.ToRollPitchYawShorts();
            stream.Write(roll);  // rotx
            stream.Write(pitch); // roty
            stream.Write(yaw);   // rotz
            stream.Write(doodad.Scale);
            stream.Write(doodad.FuncGroupId); // doodad_func_groups Id
        }

        return stream;
    }

    public override string Verbose()
    {
        // DIAG-3503: list streamed doodad objIds/funcGroups
        var sb = new System.Text.StringBuilder();
        foreach (var d in doodads)
            sb.Append($" o[{d.ObjId}]t{d.TemplateId}g{d.FuncGroupId}");
        return sb.ToString();
    }
}
