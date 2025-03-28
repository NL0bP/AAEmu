using System;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Network.Connections;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Char.Static;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Observers;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSBroadcastVisualOption_0_Packet() : GamePacket(CSOffsets.CSBroadcastVisualOption_0_Packet, 5)
{
    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in CSBroadcastVisualOption_0_...");
        Connection.State = GameState.World;
        var character = Connection.ActiveChar;

        Connection.ActiveChar.VisualOptions = new CharacterVisualOptions();
        Connection.ActiveChar.VisualOptions.Read(stream);

        Connection.ActiveChar.Buffs.AddBuff((uint)BuffConstants.LoggedOn, Connection.ActiveChar);
        var template = CharacterManager.Instance.GetTemplate(character.Race, character.Gender);
        foreach (var buff in template.Buffs)
        {
            var buffTemplate = SkillManager.Instance.GetBuffTemplate(buff);
            var casterObj = new SkillCasterUnit(character.ObjId);
            character.Buffs.AddBuff(new Buff(character, character, casterObj, buffTemplate, null, DateTime.UtcNow) { Passive = true });
        }
        character.Breath = character.LungCapacity;
        // TODO: Fix the patron and auction house license buff issue
        Connection.ActiveChar.Buffs.AddBuff((uint)SkillConstants.PatronStatus, Connection.ActiveChar);
        //Connection.ActiveChar.Buffs.AddBuff((uint)SkillConstants.PatronPlus, Connection.ActiveChar);
        //Connection.ActiveChar.Buffs.AddBuff((uint)SkillConstants.Patron, Connection.ActiveChar);
        //Connection.ActiveChar.Buffs.AddBuff((uint)SkillConstants.AuctionLicense, Connection.ActiveChar);

        Connection.SendPacket(new SCUnitStatePacket(Connection.ActiveChar));
        Connection.SendPacket(new SCCooldownsPacket(Connection.ActiveChar));
        Connection.SendPacket(new SCListSkillActiveTypsPacket([]));

        Connection.ActiveChar.PushSubscriber(TimeManager.Instance.Subscribe(Connection, new TimeOfDayObserver(Connection.ActiveChar)));
        //Connection.SendPacket(new SCDetailedTimeOfDayPacket(12f));

        Connection.ActiveChar.SendPacket(new SCDailyCountPacket(0, 0, 5));
        Connection.ActiveChar.SendPacket(new SCDailyResetPacket(DailyResetKind.Instance));
        Connection.ActiveChar.SendPacket(new SCDailyResetPacket(DailyResetKind.AbilitySetFreeActivationCount));

        Connection.SendPacket(new SCActionSlotsPacket(Connection.ActiveChar.Slots));

        Connection.ActiveChar.BroadcastPacket(new SCReputationChangedPacket(DateTime.UtcNow, false), true);
        // TODO здесь шлем пустой таск с номером 143 (пока не знаю точно, что это)
        Connection.ActiveChar.BroadcastPacket(new SCItemTaskSuccessPacket((ItemTaskType)143, [], []), true);
        Connection.ActiveChar.BroadcastPacket(new SCAbilitySetAllInfoPacket(), true);

        Connection.ActiveChar.BroadcastPacket(new SCUnitVisualOptionsPacket(Connection.ActiveChar.ObjId, Connection.ActiveChar.VisualOptions), true);

        Logger.Info("CSBroadcastVisualOption_0");
    }
}
