using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Achievement;
using AAEmu.Game.Models.Game.Achievement.Enums;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Mails;
using AAEmu.Game.Models.Game.Mails.Static;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;

using MySql.Data.MySqlClient;

using NLog;

namespace AAEmu.Game.Models.Game.Char;

public class CharacterAchievements
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private const long MoneyPerGold = 10000;

    private readonly Dictionary<uint, ulong> _recordAmounts = [];
    private readonly Dictionary<uint, AchievementInfo> _achievementInfos = [];

    public CharacterAchievements(Character owner)
    {
        Owner = owner;
    }

    public Character Owner { get; }

    private AchievementGameData GameData => AchievementGameData.Instance;

    public void Load(MySqlConnection connection)
    {
        try
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT record_id, amount FROM character_achievement_records WHERE owner = @owner";
                command.Parameters.AddWithValue("@owner", Owner.Id);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    _recordAmounts[reader.GetUInt32("record_id")] = reader.GetUInt64("amount");
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, amount, completed_at FROM character_achievements WHERE owner = @owner";
                command.Parameters.AddWithValue("@owner", Owner.Id);
                using var reader = command.ExecuteReader();
                var completedAtOrdinal = reader.GetOrdinal("completed_at");
                while (reader.Read())
                {
                    _achievementInfos[reader.GetUInt32("id")] = new AchievementInfo
                    {
                        Id = reader.GetUInt32("id"),
                        Amount = reader.GetUInt32("amount"),
                        Complete = reader.IsDBNull(completedAtOrdinal) ? DateTime.MinValue : reader.GetDateTime("completed_at")
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to load achievements for character {0}", Owner.Id);
        }

        BootstrapSnapshotRecords();
        EvaluateAll(false);
    }

    public void Save(MySqlConnection connection, MySqlTransaction transaction)
    {
        foreach (var (recordId, amount) in _recordAmounts)
        {
            using var command = connection.CreateCommand();
            command.Connection = connection;
            command.Transaction = transaction;
            command.CommandText = "REPLACE INTO character_achievement_records(`owner`,`record_id`,`amount`) VALUES (@owner,@record_id,@amount)";
            command.Parameters.AddWithValue("@owner", Owner.Id);
            command.Parameters.AddWithValue("@record_id", recordId);
            command.Parameters.AddWithValue("@amount", amount);
            command.ExecuteNonQuery();
        }

        foreach (var info in _achievementInfos.Values)
        {
            using var command = connection.CreateCommand();
            command.Connection = connection;
            command.Transaction = transaction;
            command.CommandText = "REPLACE INTO character_achievements(`owner`,`id`,`amount`,`completed_at`) VALUES (@owner,@id,@amount,@completed_at)";
            command.Parameters.AddWithValue("@owner", Owner.Id);
            command.Parameters.AddWithValue("@id", info.Id);
            command.Parameters.AddWithValue("@amount", info.Amount);
            command.Parameters.AddWithValue("@completed_at", info.Complete == DateTime.MinValue ? DBNull.Value : info.Complete);
            command.ExecuteNonQuery();
        }
    }

    public void Send()
    {
        var infos = _achievementInfos.Values
            .Where(x => x.Amount > 0 || x.Complete > DateTime.MinValue)
            .OrderBy(x => x.Id)
            .ToList();

        for (var i = 0; i < infos.Count; i += 50)
        {
            Owner.SendPacket(new SCAchievementsPacket(infos.Skip(i).Take(50).ToList()));
        }
    }

    public void BootstrapSnapshotRecords()
    {
        TrackSnapshot(CharRecordKind.CharLevel, 0, 0, Owner.Level, null, false);
        TrackSnapshot(CharRecordKind.AncestralLevel, 0, 0, Owner.HeirLevel, null, false);
        TrackSnapshot(CharRecordKind.MyGold, 0, 0, ConvertMoneyToGoldAmount(Owner.Money), null, false);
        BootstrapMateSnapshotRecords();
        BootstrapEquipmentSnapshotRecords();

        foreach (var ability in Owner.Abilities?.Values ?? [])
        {
            var level = Owner.Abilities.GetAbilityLevel(ability.Id);
            TrackSnapshot(CharRecordKind.AbilityLevel, (uint)ability.Id, 0, level, null, false);
            TrackSnapshot(CharRecordKind.AbyssalSkillsetLevel, 1, (uint)ability.Id, level, null, false);
        }

        foreach (var actability in Owner.Actability?.Actabilities ?? [])
        {
            TrackSnapshot(CharRecordKind.GetActability, actability.Key, 0, (uint)Math.Max(0, actability.Value.Point), null, false);
        }
    }

    public void TrackQuestCompleted(uint questId)
    {
        TrackRecordProgress(CharRecordKind.CompleteQuestType, questId);
        var template = QuestManager.Instance.GetTemplate(questId);
        if (template != null)
        {
            TrackRecordProgress(CharRecordKind.CompleteQuestCategory, template.CategoryId);
        }
    }

    public void TrackAbilityLevel(AbilityType abilityType, byte level)
    {
        TrackSnapshot(CharRecordKind.AbilityLevel, (uint)abilityType, 0, level);
        TrackSnapshot(CharRecordKind.AbyssalSkillsetLevel, 1, (uint)abilityType, level);
    }

    public void TrackAncestralLevel(byte level)
    {
        TrackSnapshot(CharRecordKind.AncestralLevel, 0, 0, level);
    }

    public void TrackActability(uint actabilityId, int points)
    {
        TrackSnapshot(CharRecordKind.GetActability, actabilityId, 0, (uint)Math.Max(0, points));
    }

    public void TrackMoneySnapshot()
    {
        TrackSnapshot(CharRecordKind.MyGold, 0, 0, ConvertMoneyToGoldAmount(Owner.Money));
    }

    public void TrackGoldSpent(long amount)
    {
        if (amount <= 0)
        {
            return;
        }

        var goldAmount = ConvertMoneyToGoldAmount(amount);
        if (goldAmount == 0)
        {
            return;
        }

        TrackRecordProgress(CharRecordKind.SpendGold, amount: goldAmount);
    }

    public void TrackHonorPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        TrackRecordProgress(CharRecordKind.GetHonorPoint, amount: (uint)amount);
    }

    public void TrackJuryPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        TrackRecordProgress(CharRecordKind.GetJuryPoint, amount: (uint)amount);
    }

    public void TrackLifePoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        TrackRecordProgress(CharRecordKind.GetLifePoint, amount: (uint)amount);
    }

    public void TrackFamilyEnrollment()
    {
        TrackRecordProgress(CharRecordKind.EnrollFamily);
    }

    public void TrackExpeditionEnrollment()
    {
        TrackRecordProgress(CharRecordKind.EnrollGuild);
    }

    public void TrackTeamEnrollment(bool isParty)
    {
        TrackRecordProgress(isParty ? CharRecordKind.EnrollParty : CharRecordKind.EnrollRaidGroup);
    }

    public void TrackDuelWin()
    {
        TrackRecordProgress(CharRecordKind.WinDuel);
    }

    public void TrackPvpKill(bool sameMotherFaction)
    {
        TrackRecordProgress(sameMotherFaction ? CharRecordKind.PvpKillAlly : CharRecordKind.PvpKillEnemy);
    }

    public void TrackPvpDeath(bool sameMotherFaction)
    {
        TrackRecordProgress(sameMotherFaction ? CharRecordKind.PvpDeathAlly : CharRecordKind.PvpDeathEnemy);
        TrackRecordProgress(CharRecordKind.DeadByPvp);
    }

    public void TrackNpcDeath()
    {
        TrackRecordProgress(CharRecordKind.DeadByNpc);
    }

    public void TrackSlaveKill(uint templateId = 0)
    {
        TrackRecordProgress(CharRecordKind.KillSlave, templateId);
    }

    public void TrackMateLevel(uint mateTemplateId, byte level)
    {
        TrackSnapshot(CharRecordKind.PetLevel, mateTemplateId, 0, level);
    }

    public void TrackNpcEmotion(uint npcTemplateId, uint emotionId)
    {
        TrackRecordProgress(CharRecordKind.NpcEmotion, npcTemplateId, emotionId);
    }

    public void TrackHiramGrade(uint itemTemplateId, byte grade, BaseUnit target = null, bool sendPackets = true)
    {
        var changedRecordIds = new HashSet<uint>();
        foreach (var record in GameData.GetCharRecordsByKind(CharRecordKind.HiramGrade))
        {
            if (record.Value1 != itemTemplateId || record.Value2 > grade)
            {
                continue;
            }

            var currentAmount = _recordAmounts.GetValueOrDefault(record.Id);
            if (currentAmount >= 1)
            {
                continue;
            }

            _recordAmounts[record.Id] = 1;
            changedRecordIds.Add(record.Id);
        }

        if (changedRecordIds.Count == 0)
        {
            return;
        }

        EvaluateChangedRecords(changedRecordIds, target, sendPackets);
    }

    public void TrackRecordProgress(CharRecordKind kind, uint value1 = 0, uint value2 = 0, uint amount = 1, BaseUnit target = null, bool sendPackets = true)
    {
        if (amount == 0)
        {
            return;
        }

        var changedRecordIds = new HashSet<uint>();
        foreach (var record in GameData.GetCharRecordsByKind(kind))
        {
            if (!IsRecordMatch(record, value1, value2))
            {
                continue;
            }

            var currentAmount = _recordAmounts.GetValueOrDefault(record.Id);
            var newAmount = currentAmount + amount;
            if (newAmount == currentAmount)
            {
                continue;
            }

            _recordAmounts[record.Id] = newAmount;
            changedRecordIds.Add(record.Id);
        }

        if (changedRecordIds.Count == 0)
        {
            return;
        }

        if (kind == CharRecordKind.UseItem)
        {
            foreach (var recordId in changedRecordIds)
            {
                Logger.Debug("Achievement UseItem progress: char={0} template={1} record={2} amount={3}", Owner.Id, value1, recordId, _recordAmounts.GetValueOrDefault(recordId));
            }
        }

        EvaluateChangedRecords(changedRecordIds, target, sendPackets);
    }

    public void TrackSnapshot(CharRecordKind kind, uint value1, uint value2, uint currentValue, BaseUnit target = null, bool sendPackets = true)
    {
        var changedRecordIds = new HashSet<uint>();
        foreach (var record in GameData.GetCharRecordsByKind(kind))
        {
            if (!IsRecordMatch(record, value1, value2))
            {
                continue;
            }

            var currentAmount = _recordAmounts.GetValueOrDefault(record.Id);
            if (currentAmount >= currentValue)
            {
                continue;
            }

            _recordAmounts[record.Id] = currentValue;
            changedRecordIds.Add(record.Id);
        }

        if (changedRecordIds.Count == 0)
        {
            return;
        }

        EvaluateChangedRecords(changedRecordIds, target, sendPackets);
    }

    private void EvaluateAll(bool sendPackets)
    {
        foreach (var achievement in GameData.Achievements.Values.OrderBy(x => x.Id))
        {
            EvaluateAchievement(achievement.Id, null, sendPackets);
        }
    }

    private void EvaluateChangedRecords(IEnumerable<uint> recordIds, BaseUnit target, bool sendPackets)
    {
        var achievementIds = new HashSet<uint>();
        foreach (var recordId in recordIds)
        {
            foreach (var objective in GameData.GetAchievementObjectivesByRecordId(recordId))
            {
                achievementIds.Add(objective.AchievementId);
            }
        }

        if (achievementIds.Contains(2008))
        {
            Logger.Debug("Achievement reevaluation queued: char={0} achievements=[{1}] sendPackets={2}", Owner.Id, string.Join(",", achievementIds.OrderBy(x => x)), sendPackets);
        }

        foreach (var achievementId in achievementIds)
        {
            EvaluateAchievement(achievementId, target, sendPackets);
        }
    }

    private void EvaluateAchievement(uint achievementId, BaseUnit target, bool sendPackets)
    {
        var achievement = GameData.GetAchievement(achievementId);
        if (achievement == null)
        {
            return;
        }

        var objectives = GameData.GetAchievementObjectives(achievementId);
        if (objectives.Count == 0)
        {
            return;
        }

        var newAmount = GetAchievementAmount(achievement, objectives, target);
        var isCompleted = CanCompleteAchievement(achievement, objectives, target);

        if (!_achievementInfos.TryGetValue(achievementId, out var info))
        {
            info = new AchievementInfo { Id = achievementId, Amount = 0, Complete = DateTime.MinValue };
            _achievementInfos.Add(achievementId, info);
        }

        if (achievementId == 2008)
        {
            Logger.Debug("Achievement 2008 evaluate: char={0} recordAmount={1} storedAmount={2} newAmount={3} completed={4} reqsMet={5}",
                Owner.Id,
                _recordAmounts.GetValueOrDefault(objectives[0].RecordId),
                info.Amount,
                newAmount,
                info.Complete > DateTime.MinValue,
                isCompleted);
        }

        if (info.Amount != newAmount)
        {
            info.Amount = newAmount;
            if (sendPackets)
            {
                if (achievementId == 2008)
                {
                    Logger.Debug("Achievement 2008 changed packet: char={0} amount={1}", Owner.Id, newAmount);
                }

                Owner.SendPacket(new SCAchievementChangedPacket(achievementId, (int)newAmount));
            }
        }

        if (info.Complete > DateTime.MinValue || !isCompleted)
        {
            return;
        }

        info.Complete = DateTime.UtcNow;
        if (sendPackets)
        {
            Owner.SendPacket(new SCAchievementCompletedPacket(achievementId));
        }

        GrantRewards(achievement, sendPackets);
        TrackSnapshot(CharRecordKind.CompleteAchievement, achievementId, 0, 1, target, sendPackets);
    }

    private uint GetAchievementAmount(Achievements achievement, IReadOnlyList<AchievementObjectives> objectives, BaseUnit target)
    {
        if (TryGetAggregatedAmount(achievement, objectives, out var aggregatedAmount))
        {
            return aggregatedAmount;
        }

        if (objectives.Count == 1)
        {
            var requiredAmount = GetObjectiveRequiredAmount(achievement);
            var amount = _recordAmounts.GetValueOrDefault(objectives[0].RecordId);
            return requiredAmount == 0 ? (uint)Math.Min(amount, uint.MaxValue) : (uint)Math.Min(amount, requiredAmount);
        }

        uint completedObjectives = 0;
        foreach (var objective in objectives)
        {
            if (IsObjectiveComplete(achievement, objective, target))
            {
                completedObjectives++;
            }
        }

        if (IsAllObjectivesCollectionAchievement(achievement, objectives))
        {
            return completedObjectives;
        }

        var required = GetObjectiveRequiredAmount(achievement);
        return required == 0 ? completedObjectives : Math.Min(completedObjectives, required);
    }

    private bool CanCompleteAchievement(Achievements achievement, IReadOnlyList<AchievementObjectives> objectives, BaseUnit target)
    {
        if (achievement.ParentAchievementId > 0 &&
            !IsMetaAchievementParentOf(achievement.ParentAchievementId, achievement.Id) &&
            !IsAchievementCompleted(achievement.ParentAchievementId))
        {
            return false;
        }

        foreach (var requirement in GameData.GetAchievementRequirements(achievement.Id))
        {
            if (!IsAchievementCompleted(requirement.CompletedAchievementId))
            {
                return false;
            }
        }

        if (TryGetAggregatedAmount(achievement, objectives, out var aggregatedAmount))
        {
            return aggregatedAmount >= GetObjectiveRequiredAmount(achievement);
        }

        if (achievement.CompleteOr)
        {
            if (IsAllObjectivesCollectionAchievement(achievement, objectives))
            {
                return objectives.All(objective => IsObjectiveComplete(achievement, objective, target));
            }

            var requiredObjectives = GetObjectiveRequiredAmount(achievement);
            if (requiredObjectives > 1)
            {
                uint completedObjectives = 0;
                foreach (var objective in objectives)
                {
                    if (IsObjectiveComplete(achievement, objective, target))
                    {
                        completedObjectives++;
                        if (completedObjectives >= requiredObjectives)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            return objectives.Any(objective => IsObjectiveComplete(achievement, objective, target));
        }

        return objectives.All(objective => IsObjectiveComplete(achievement, objective, target));
    }

    private bool IsObjectiveComplete(Achievements achievement, AchievementObjectives objective, BaseUnit target)
    {
        if (!PassObjectiveRequirements(objective, target))
        {
            return false;
        }

        var amount = _recordAmounts.GetValueOrDefault(objective.RecordId);
        var required = GetObjectiveRequiredAmount(achievement);
        return required == 0 ? amount > 0 : amount >= required;
    }

    private bool PassObjectiveRequirements(AchievementObjectives objective, BaseUnit target)
    {
        var requirements = UnitRequirementsGameData.Instance.GetAchievementObjectiveRequirements(objective.Id);
        if (requirements.Count == 0)
        {
            return true;
        }

        var res = !objective.OrUnitReqs;
        foreach (var req in requirements)
        {
            var valid = req.Validate(Owner, target ?? Owner).ResultKey == SkillResultKeys.ok;
            if (objective.OrUnitReqs)
            {
                if (valid)
                {
                    return true;
                }

                continue;
            }

            res &= valid;
            if (!res)
            {
                return false;
            }
        }

        return res;
    }

    private static uint GetObjectiveRequiredAmount(Achievements achievement)
    {
        return achievement.CompleteNum > 0 ? achievement.CompleteNum : 1;
    }

    private static bool IsAllObjectivesCollectionAchievement(Achievements achievement, IReadOnlyList<AchievementObjectives> objectives)
    {
        return achievement.CompleteOr && achievement.CompleteNum == 0 && objectives.Count > 1;
    }

    private bool IsMetaAchievementParentOf(uint parentAchievementId, uint childAchievementId)
    {
        var parentObjectives = GameData.GetAchievementObjectives(parentAchievementId);
        if (parentObjectives.Count == 0)
        {
            return false;
        }

        foreach (var objective in parentObjectives)
        {
            var record = GameData.GetCharRecord(objective.RecordId);
            if (record?.KindId != CharRecordKind.CompleteAchievement)
            {
                return false;
            }

            if (record.Value1 == childAchievementId)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetAggregatedAmount(Achievements achievement, IReadOnlyList<AchievementObjectives> objectives, out uint amount)
    {
        amount = 0;
        if (objectives.Count <= 1)
        {
            return false;
        }

        if (achievement.CompleteOr || achievement.CompleteNum == 0)
        {
            return false;
        }

        var firstRecord = GameData.GetCharRecord(objectives[0].RecordId);
        if (firstRecord == null || !CanAggregateObjectivesByKind(firstRecord.KindId))
        {
            return false;
        }

        ulong totalAmount = 0;
        foreach (var objective in objectives)
        {
            if (!PassObjectiveRequirements(objective, Owner))
            {
                continue;
            }

            var record = GameData.GetCharRecord(objective.RecordId);
            if (record == null || record.KindId != firstRecord.KindId)
            {
                return false;
            }

            totalAmount += _recordAmounts.GetValueOrDefault(objective.RecordId);
        }

        var required = GetObjectiveRequiredAmount(achievement);
        amount = required == 0
            ? (uint)Math.Min(totalAmount, uint.MaxValue)
            : (uint)Math.Min(totalAmount, required);
        return true;
    }

    private static bool CanAggregateObjectivesByKind(CharRecordKind kind)
    {
        return kind == CharRecordKind.UseItem ||
               kind == CharRecordKind.GetItemType ||
               kind == CharRecordKind.KillNpc ||
               kind == CharRecordKind.CompleteQuestType ||
               kind == CharRecordKind.MakeItemType ||
               kind == CharRecordKind.SellItem ||
               kind == CharRecordKind.UseSkill;
    }

    private bool IsAchievementCompleted(uint achievementId)
    {
        return _achievementInfos.TryGetValue(achievementId, out var info) && info.Complete > DateTime.MinValue;
    }

    private static bool IsRecordMatch(CharRecords record, uint value1, uint value2)
    {
        return IsValueMatch(record.Value1, value1) && IsValueMatch(record.Value2, value2);
    }

    private static bool IsValueMatch(uint expected, uint actual)
    {
        return expected == 0 || expected == uint.MaxValue || expected == actual;
    }

    private static uint ConvertMoneyToGoldAmount(long amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        return (uint)Math.Min(amount / MoneyPerGold, uint.MaxValue);
    }

    private void BootstrapMateSnapshotRecords()
    {
        var containers = Owner.Inventory?._itemContainers;
        if (containers == null)
        {
            return;
        }

        foreach (var container in containers.Values)
        {
            foreach (var item in container.Items)
            {
                if (item is not SummonMate summonMate || item.Template is not SummonMateTemplate template)
                {
                    continue;
                }

                TrackSnapshot(CharRecordKind.PetLevel, template.NpcId, 0, summonMate.DetailLevel, null, false);
            }
        }
    }

    private void BootstrapEquipmentSnapshotRecords()
    {
        var containers = Owner.Inventory?._itemContainers;
        if (containers == null)
        {
            return;
        }

        foreach (var container in containers.Values)
        {
            foreach (var item in container.Items)
            {
                if (item is not EquipItem)
                {
                    continue;
                }

                TrackHiramGrade(item.TemplateId, item.Grade, null, false);
            }
        }
    }

    private void GrantRewards(Achievements achievement, bool sendPackets)
    {
        if (achievement.AppellationId > 0 && !Owner.Appellations.Appellations.Contains(achievement.AppellationId))
        {
            Owner.Appellations.Add(achievement.AppellationId);
        }

        if (achievement.ItemId == 0 || achievement.ItemNum == 0)
        {
            return;
        }

        var byMail = false;
        if (!Owner.Inventory.Bag.AcquireDefaultItem(ItemTaskType.AchievementSupplyItems, achievement.ItemId, (int)achievement.ItemNum, -1, Owner.Id))
        {
            byMail = SendRewardByMail(achievement);
        }

        if (sendPackets)
        {
            Owner.SendPacket(new SCAchievementItemSentPacket(achievement.Id, byMail));
        }
    }

    private bool SendRewardByMail(Achievements achievement)
    {
        if (!Owner.Inventory.MailAttachments.AcquireDefaultItemEx(ItemTaskType.Invalid, achievement.ItemId, (int)achievement.ItemNum, -1, out var newItems, out _, Owner.Id))
        {
            return false;
        }

        var mail = new MailPlayerToPlayer(Owner, Owner.Name)
        {
            MailType = MailType.SysExpress,
            Title = "Achievement Reward"
        };
        mail.Header.SenderId = (uint)SystemMailSenderKind.None;
        mail.Header.SenderName = ".achievement";
        mail.Body.Text = $"Reward for achievement {achievement.Id}.";

        foreach (var item in newItems)
        {
            mail.Body.Attachments.Add(item);
        }

        mail.Send();
        return true;
    }
}
