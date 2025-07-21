using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Chat;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;

public class TalkBehavior : BaseCombatBehavior
{
    public override void Enter()
    {
    }

    private readonly Dictionary<uint, DateTime> _greeted = new();

    public override void Tick(TimeSpan delta)
    {
        if (!Validate() || !Throttle()) return;

        var characters = WorldManager.GetAround<Character>(Ai.Owner, 5f, true);

        foreach (var character in characters)
        {
            if (!_greeted.TryGetValue(character.ObjId, out var greetTime) || DateTime.UtcNow - greetTime >= TimeSpan.FromMinutes(5))
            {
                //var message = $"Salute! |cFFFFFFFF{character.Name}|!";
                var message = $"Salute! {character.Name}!";
                Logger.Warn(message);
                character.BroadcastPacket(new SCNpcChatMessagePacket(ChatType.White, Ai.Owner, character,  0, 0, message), true);
                character.SendMessage(ChatType.System, message);

                if (Ai.Owner.Template.NpcNicknameId == 22) // Vehicle Conductor
                {
                    using var pos = Ai.Owner.Transform.CloneDetached();
                    pos.Local.AddDistanceToFront(1.5f);
                    var defaultYaw = (float)MathUtil.CalculateAngleFrom(pos, Ai.Owner.Transform);
                    var doodadSpawner = new DoodadSpawner { Id = 0, UnitId = 6129, Position = pos.CloneAsSpawnPosition() }; // 6129	Подзорная труба
                    doodadSpawner.Position.Yaw = defaultYaw;
                    doodadSpawner.Position.Pitch = 0;
                    doodadSpawner.Position.Roll = 0;
                    _ = doodadSpawner.Spawn(0, 0, Ai.Owner.ObjId);

                }
                _greeted[character.ObjId] = DateTime.UtcNow;
            }
        }

        // Remove entries that are out of range and older than 5 minutes
        var toRemove = _greeted
            .Where(kv => characters.All(c => c.ObjId != kv.Key) &&
                         DateTime.UtcNow - kv.Value >= TimeSpan.FromMinutes(5))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in toRemove)
            _greeted.Remove(id);
    }
    
    public override void Exit()
    {
        //Logger.Warn($"Bye! Bye!");
    }
}
