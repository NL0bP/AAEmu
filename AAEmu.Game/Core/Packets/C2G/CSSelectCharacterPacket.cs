using System.Linq;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Route;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSSelectCharacterPacket() : GamePacket(CSOffsets.CSSelectCharacterPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSSelectCharacter...");
        var characterId = stream.ReadUInt32();
        var gm = stream.ReadBoolean();
        stream.ReadByte();

        if (Connection.Characters.TryGetValue(characterId, out var character))
        {
            // Despawn any old pets this character might have even before loading it
            //var character = Connection.Characters[characterId];
            character.Load();
            character.Connection = Connection;
            var houses = Connection.Houses.Values.Where(x => x.OwnerId == character.Id).ToList();
            MateManager.Instance.RemoveAndDespawnAllActiveOwnedMates(character);

            Connection.ActiveChar = character;
            if (Character.UsedCharacterObjIds.TryGetValue(character.Id, out var oldObjId))
            {
                character.ObjId = oldObjId;
            }
            else
            {
                character.ObjId = ObjectIdManager.Instance.GetNextId();
                Character.UsedCharacterObjIds.TryAdd(character.Id, character.ObjId);
            }

            var mySlave = SlaveManager.Instance.GetSlaveByOwnerObjId(character.ObjId);
            if (mySlave != null)
            {
                Logger.Warn($"{character.Name}: Interrupting the transport shutdown task");
                mySlave.CancelTokenSource.Cancel();
                // TODO найти, как восстанавливать контроль
                Unit.DespawSlave(character); // despawn because we lost control over them
            }
            var myMates = MateManager.Instance.GetActiveMates(character.ObjId);
            if (myMates != null)
            {
                Unit.DespawnMate(character); // despawn because we lost control over them
            }

            character.Simulation = new Simulation(character);

            // начинаем слать пакеты

            // TODO подобрать правильное место для пакета

            character.Attendances.ResetIfNewMonth();

            if (character.Attendances.Records?.Count == 0)
            {
                character.Attendances.SendEmptyAttendances();
            }
            else
            {
                character.Attendances.Send();
            }

            character.SendPacket(new SCResidentInfoListPacket(ResidentManager.Instance.GetInfo()));
            character.SendPacket(new SCCharacterStatePacket(character));
            character.Inventory.Send();
            character.SendPacket(new SCCharacterGamePointsPacket(character));
            // move to CSSpawnCharacter
            //Connection.SendPacket(new SCActionSlotsPacket(character.Slots));
            // added in 5.0.7.0
            character.SendPacket(new SCIncreasedFavoritePortalLimitPacket(0));
            //character.Portals.SendIndunZone();
            character.SendPacket(new SCNpcFriendshipListPacket());

            character.Quests.Send();
            character.Quests.SendCompleted();

            character.Actability.Send();
            character.Mails.SendUnreadMailCount();
            // removed in 5.0.7.0
            character.Appellations.Send();
            character.Portals.Send();

            character.Friends.Send();
            character.Blocked.Send();
            // added in 5.0.7.0
            character.SendPacket(new SCWorldRestrictOwnerChangePacket(false));

            foreach (var house in houses)
            {
                character.SendPacket(new SCHouseStatePacket(house));
            }

            foreach (var conflict in ZoneManager.Instance.GetConflicts())
            {
                character.SendPacket(new SCConflictZoneStatePacket(conflict.ZoneGroupId, conflict.CurrentZoneState, conflict.NextStateTime));
            }

            FactionManager.Instance.SendFactions(character);
            ExpeditionManager.Instance.SendExpeditions(character);
            ExpeditionManager.SendMyExpeditionInfo(character);
            FactionManager.Instance.SendRelations(character);

            character.SendOption(4);
            character.SendOption(5);
            character.SendOption(6);

            //character.Buffs.AddBuff((uint)BuffConstants.LoggedOn, character);
            //var template = CharacterManager.Instance.GetTemplate(character.Race, character.Gender);
            //foreach (var buff in template.Buffs)
            //{
            //    var buffTemplate = SkillManager.Instance.GetBuffTemplate(buff);
            //    var casterObj = new SkillCasterUnit(character.ObjId);
            //    character.Buffs.AddBuff(new Buff(character, character, casterObj, buffTemplate, null, DateTime.UtcNow) { Passive = true });
            //}
            //character.Breath = character.LungCapacity;
            //// TODO: Fix the patron and auction house license buff issue
            //character.Buffs.AddBuff((uint)SkillConstants.Patron, character);
            //character.Buffs.AddBuff((uint)SkillConstants.AuctionLicense, character);

            character.UpdateGearBonuses(null, null);
            character.RestoreSavedHpMp();

            character.OnZoneChange(0, character.Transform.ZoneId);

            var scheduleItems = AccountManager.Instance.GetDivineClock(character.AccountId);
            if (scheduleItems is not null)
            {
                character.ScheduleItems = scheduleItems; // updated
            }
        }
        else
        {
            // TODO ...
        }
    }
}
