using System.Drawing;
using System.Linq;

using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Chat;
using AAEmu.Game.Models.Game.Mails;
using AAEmu.Game.Models.Game.NPChar;

namespace AAEmu.Game.Models.Game.Char;

public partial class Character
{

    private const float NearbyNpcSearchRadius = 15f;
    private int Count = 0;

    /// <summary>
    /// Запись высоты персонажа в таблицу высот.
    /// </summary>
    /// <param name="character"></param>
    internal static void TrackCharacterCoordinates(Character character)
    {
        if (character == null)
            return;
        if (AppConfiguration.Instance.World.SaveGeoDataMode == false)
            return;
        if (character.CurrentTarget == null || character.CurrentTarget == character || character.CurrentTarget is Character)
            return;

        var pos = character.Transform.World.Position;
        var npcs = WorldManager.GetAround<Npc>(character, NearbyNpcSearchRadius);
        if (!npcs.Any())
            return;

        foreach (var npc in npcs)
        {
            if (character.CurrentTarget is not Npc n || n.ObjId != npc.ObjId)
                continue;

            var cacheKey = Npc.GetCacheKey(pos.X, pos.Y, character.Transform.ZoneId);

            var candidate = npc.AdjustNpcFloor(pos.Z);

            Npc.UpdateHeightMapInDatabase(character.Transform.ZoneId, cacheKey.GridX, cacheKey.GridY, candidate);
            Npc.HeightCacheAddOrUpdate(pos.Z, cacheKey);

            npc.Transform.Local.Position = npc.Transform.Local.Position with { Z = candidate };
            npc.Transform.Local.SetPosition(npc.Transform.Local.Position.X, npc.Transform.Local.Position.Y, candidate);

            character.SendMessage(ChatType.System, "Записываем геоданные! Не забудьте отключить запись!", Color.White);
            //character.SendMessage(ChatType.System, "Let's record the geo-data! Don't forget to turn it off!", Color.White);
        }

        character.Count++;
        if (character.Count < 30)
            return;

        character.Count = 0;
        AppConfiguration.Instance.World.SaveGeoDataMode = false; // отключаем запись
        character.SendMessage(ChatType.System, "Запись геоданных прекращена по истечении таймаута!", Color.Aqua);
        //character.SendMessage(ChatType.System, "The recording of the geodata is stopped after the time out!", Color.Aqua);
    }

}
