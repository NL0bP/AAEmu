using System.Drawing;
using System.Linq;

using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Chat;
using AAEmu.Game.Models.Game.NPChar;

namespace AAEmu.Game.Models.Game.Char;

public partial class Character
{

    private const float NearbyNpcSearchRadius = 15f;
    private int Count = 0;
    private static bool IsChanged = false;

    /// <summary>
    /// Запись высоты персонажа в таблицу высот.
    /// </summary>
    /// <param name="character"></param>
    internal static void TrackCharacterCoordinates(Character character)
    {
        if (character == null)
            return;
        if (AppConfiguration.Instance.World.SaveGeoDataMode == false)
        {
            IsChanged = false;
            return;
        }

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

            var npcPos = npc.Transform.World.Position;
            var cacheKey = Npc.GetCacheKey(pos.X, pos.Y, character.Transform.ZoneId, character.Transform.WorldId);

            var candidate = npc.AdjustNpcFloor(pos.Z);

            // Update database and cache
            Npc.UpdateHeightMapInDatabase(character.Transform.ZoneId, cacheKey.GridX, cacheKey.GridY, candidate);
            Npc.HeightCacheAddOrUpdate(pos.Z, cacheKey);

            // Update spawn file
            if (!IsChanged)
            {
                character.SendMessage(ChatType.System, "Записываем геоданные! Не забудьте отключить запись!", Color.White);
                //character.SendMessage(ChatType.System, "Let's record the geo-data! Don't forget to turn it off!", Color.White);

                if (Npc.UpdateSpawnHeight(SpawnFile, npc.TemplateId, npcPos.X, npcPos.Y, candidate))
                {
                    IsChanged = true;
                    character.SendMessage(ChatType.System, $"Высота скорректирована: npc={npc.TemplateId}:{npc.ObjId}, старая={npcPos.Z}, новая={candidate}!", Color.Aquamarine);
                }
                else
                {
                    character.SendMessage(ChatType.System, $"Высота не скорректирована: npc={npc.TemplateId}:{npc.ObjId} не найден!", Color.Coral);
                }
            }

            // Update NPC position
            npc.Transform.Local.Position = npc.Transform.Local.Position with { Z = candidate };
            npc.Transform.Local.SetPosition(npc.Transform.Local.Position.X, npc.Transform.Local.Position.Y, candidate);
        }

        character.Count++;
        if (character.Count < 30)
            return;

        character.Count = 0;
        character.SetSaveGeoDataMode(false); // отключаем запись
        IsChanged = false;
        character.SendMessage(ChatType.System, "Запись геоданных прекращена по истечении таймаута!", Color.Aqua);
        //character.SendMessage(ChatType.System, "The recording of the geodata is stopped after the time out!", Color.Aqua);
    }

}
