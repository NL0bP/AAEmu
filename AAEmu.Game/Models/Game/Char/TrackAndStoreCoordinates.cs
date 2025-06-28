using System.Drawing;
using System.Numerics;

using AAEmu.Game.Models.Game.Chat;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.Char;

public partial class Character
{

    private const float NearbyNpcSearchRadius = 4f;

    /// <summary>
    /// Запись высоты персонажа как высоту Npc с записью в файл npc_spawn.json
    /// </summary>
    /// <param name="character"></param>
    internal static void TrackCharacterCoordinates(Character character)
    {
        if (character.CurrentTarget is not Npc npc)
            return;

        var pos = character.Transform.World.Position;
        var npcPos = npc.Transform.World.Position;

        // Check if the NPC is within a reasonable distance
        var distance = MathUtil.CalculateDistance(pos, npcPos);
        if (distance >= NearbyNpcSearchRadius)
            return;

        if (Npc.UpdateSpawnFileHeight(npc.TemplateId, npcPos.X, npcPos.Y, pos.Z))
            character.SendMessage(ChatType.System, $"Height adjusted: npc={npc.TemplateId}:{npc.ObjId}, old={npcPos.Z}, new={pos.Z}!", Color.Aquamarine);
        else
            character.SendMessage(ChatType.System, $"Height not adjusted: npc={npc.TemplateId}:{npc.ObjId} not found!", Color.Coral);

        // Update NPC position
        npc.Transform.Local.Position = npc.Transform.Local.Position with { Z = pos.Z };
        npc.Transform.Local.SetPosition(npc.Transform.Local.Position.X, npc.Transform.Local.Position.Y, pos.Z);
    }

}
