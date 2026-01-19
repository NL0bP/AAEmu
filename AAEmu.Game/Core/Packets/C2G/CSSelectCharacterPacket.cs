using System;
using System.Linq;

using AAEmu.Commons.Network;
using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Route;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSSelectCharacterPacket : GamePacket
{
    public CSSelectCharacterPacket() : base(CSOffsets.CSSelectCharacterPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        var characterId = stream.ReadUInt32();
        var gm = stream.ReadBoolean();
        stream.ReadByte();

        if (Connection.Characters.TryGetValue(characterId, out var character))
        {
            using var dbConn = MySQL.CreateConnection();

            // 1. Загружаем базовые данные персонажа
            character.Load();

            // 2. Загружаем скиллы и пассивки (это ключевой момент!)
            character.Skills.Load(dbConn);

            // Лог для проверки — сколько реально загрузилось
            Logger.Info($"[EnterWorld] {character.Name} (Id {character.Id}): " +
                        $"активных скиллов: {character.Skills.Skills.Count}, " +
                        $"пассивных баффов: {character.Skills.PassiveBuffs.Count}");

            character.Connection = Connection;

            var houses = Connection.Houses.Values.Where(x => x.OwnerId == character.Id).ToList();
            MateManager.Instance.RemoveAndDespawnAllActiveOwnedMates(character);

            Connection.ActiveChar = character;

            if (Character.UsedCharacterObjIds.TryGetValue(character.Id, out var oldObjId))
            {
                Connection.ActiveChar.ObjId = oldObjId;
            }
            else
            {
                Connection.ActiveChar.ObjId = ObjectIdManager.Instance.GetNextId();
                Character.UsedCharacterObjIds.TryAdd(character.Id, character.ObjId);
            }

            var mySlave = SlaveManager.Instance.GetSlaveByOwnerObjId(Connection.ActiveChar.ObjId);
            if (mySlave != null)
            {
                Logger.Warn($"{Connection.ActiveChar.Name}: Interrupting the transport shutdown task");
                mySlave.CancelTokenSource.Cancel();
                Unit.DespawSlave(Connection.ActiveChar);
            }

            var myMates = MateManager.Instance.GetActiveMates(Connection.ActiveChar.ObjId);
            if (myMates != null)
            {
                Unit.DespawnMate(Connection.ActiveChar);
            }

            Connection.ActiveChar.Simulation = new Simulation(character);

            // Ручные баффы (Patron, Auction и т.д.)
            Connection.ActiveChar.Buffs.AddBuff((uint)SkillConstants.Patron, Connection.ActiveChar);
            Connection.ActiveChar.Buffs.AddBuff((uint)SkillConstants.AuctionLicense, Connection.ActiveChar);

            // Отправка всех основных пакетов
            Connection.SendPacket(new SCResidentInfoListPacket(ResidentManager.Instance.GetInfo()));
            Connection.SendPacket(new SCCharacterStatePacket(character));
            Connection.SendPacket(new SCCharacterGamePointsPacket(character));
            Connection.ActiveChar.Inventory.Send();
            Connection.SendPacket(new SCActionSlotsPacket(Connection.ActiveChar.Slots));

            Connection.ActiveChar.Quests.Send();
            Connection.ActiveChar.Quests.SendCompleted();

            Connection.ActiveChar.Actability.Send();
            Connection.ActiveChar.Mails.SendUnreadMailCount();
            Connection.ActiveChar.Appellations.Send();
            Connection.ActiveChar.Portals.Send();
            Connection.ActiveChar.Friends.Send();
            Connection.ActiveChar.Blocked.Send();

            foreach (var house in houses)
            {
                Connection.SendPacket(new SCMyHousePacket(house));
            }

            foreach (var conflict in ZoneManager.Instance.GetConflicts())
            {
                Connection.SendPacket(new SCConflictZoneStatePacket(conflict.ZoneGroupId, conflict.CurrentZoneState, conflict.NextStateTime));
            }

            FactionManager.Instance.SendFactions(Connection.ActiveChar);
            ExpeditionManager.Instance.SendExpeditions(Connection.ActiveChar);
            ExpeditionManager.SendMyExpeditionInfo(Connection.ActiveChar);
            FactionManager.Instance.SendRelations(Connection.ActiveChar);

            Connection.ActiveChar.SendOption(1);
            Connection.ActiveChar.SendOption(2);
            Connection.ActiveChar.SendOption(5);

            Connection.ActiveChar.Buffs.AddBuff((uint)BuffConstants.LoggedOn, Connection.ActiveChar);

            // Баффы от расы/пола
            var template = CharacterManager.Instance.GetTemplate(character.Race, character.Gender);
            foreach (var buff in template.Buffs)
            {
                var buffTemplate = SkillManager.Instance.GetBuffTemplate(buff);
                var casterObj = new SkillCasterUnit(character.ObjId);
                character.Buffs.AddBuff(new Buff(character, character, casterObj, buffTemplate, null, DateTime.UtcNow) { Passive = true });
            }

            character.Breath = character.LungCapacity;

            Connection.ActiveChar.OnZoneChange(0, Connection.ActiveChar.Transform.ZoneId);

            // ────────────────────────────────────────────────────────────────
            // ФИНАЛЬНАЯ ЧАСТЬ: Применяем и отправляем пассивные скиллы клиенту
            // ────────────────────────────────────────────────────────────────

            // Применяем все пассивки (на случай, если Apply не вызвался в Load)
            foreach (var passive in character.Skills.PassiveBuffs.Values.ToList())
            {
                passive.Apply(character);
            }

            // Повторно отправляем пакеты Learned для каждой пассивки
            // (это заставляет клиент отобразить их в пассивном табе после релога)
            foreach (var passive in character.Skills.PassiveBuffs.Values.ToList())
            {
                Connection.SendPacket(new SCBuffLearnedPacket(character.ObjId, passive.Id));
            }

            // Если активные скиллы тоже иногда не видны — раскомментируй:
            // Connection.SendPacket(new SCSkillListPacket(character));

            // Пересчитываем статы после применения всех пассивок
            //character.RecalculateBonuses();   // или UpdateAllStats(), BroadcastStats() — как у вас называется
        }
        else
        {
            // TODO: обработка ошибки — персонаж не найден
            Logger.Warn($"Character ID {characterId} not found for connection");
        }
    }
}
