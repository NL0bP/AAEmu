using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.DoodadObj;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCDoodadsCreatedPacket(Doodad[] doodads) : GamePacket(SCOffsets.SCDoodadsCreatedPacket, 5)
{
    public const int MaxCountPerPacket = 30; // Suggested Maximum Size

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write((byte)doodads.Length);
        foreach (var doodad in doodads)
            doodad.Write(stream);

        return stream;
    }

    public override string Verbose()
    {
        // DIAG-3503: list doodad objIds/funcGroups to diagnose client-side interaction issues
        var sb = new System.Text.StringBuilder();
        foreach (var d in doodads)
            sb.Append($" o[{d.ObjId}]t{d.TemplateId}g{d.FuncGroupId}");
        return sb.ToString();
    }
}
