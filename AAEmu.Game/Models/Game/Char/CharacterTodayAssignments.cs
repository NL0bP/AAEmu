using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.TodayAssignments;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Units;

using MySql.Data.MySqlClient;

using NLog;

namespace AAEmu.Game.Models.Game.Char;

public class CharacterTodayAssignments
{
    public enum TodayAssignmentFactionSide
    {
        Neutral = 0,
        West = 1,
        East = 2
    }

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly HashSet<uint> WestPatrolQuestIds =
    [
        6863, 6864, 6865,
        6866, 6867, 6868,
        6872, 6873, 6874,
        6875, 6876, 6877,
        6881, 6882, 6883,
        6887, 6888, 6889,
        6893, 6894, 6895,
        6905, 6906, 6907,
        6911, 6912, 6913
    ];

    private static readonly HashSet<uint> EastPatrolQuestIds =
    [
        6869, 6870, 6871,
        6878, 6879, 6880,
        6884, 6885, 6886,
        6890, 6891, 6892,
        6896, 6897, 6898,
        6908, 6909, 6910,
        6914, 6915, 6916
    ];

    private static readonly HashSet<uint> WestHighLevelQuestIds =
    [
        7419, 7420, 7421, 7422, 7423, 7424, 7425, 7426, 7427, 7428, 7429,
        7430, 7431, 7432, 7433, 7434, 7435, 7436, 7437, 7438, 7439, 7440,
        8447, 8448, 8449, 8450
    ];

    private static readonly HashSet<uint> EastHighLevelQuestIds =
    [
        7441, 7442, 7443, 7444, 7445, 7446, 7447, 7448, 7449, 7450, 7451,
        7452, 7453, 7454, 7455, 7456, 7457, 7458, 7459, 7460, 7461, 7462,
        7463, 7464, 8451, 8452, 8453, 8454, 8455, 8456, 8457, 8458
    ];

    private readonly Dictionary<int, CharacterTodayAssignmentState> _states = [];
    private bool _suppressQuestStatePackets;

    public CharacterTodayAssignments(Character owner)
    {
        Owner = owner;
        Owner.Events.OnQuestStepChanged += OnQuestStepChanged;
        Owner.Events.OnQuestComplete += OnQuestComplete;
    }

    public Character Owner { get; }

    private TodayAssignmentGameData GameData => TodayAssignmentGameData.Instance;

    public void Load(MySqlConnection connection)
    {
        _states.Clear();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT real_step, step_id, group_id, quest_context_id, quest_id, quest_data, quest_status, status, updated_at FROM character_today_assignments WHERE owner = @owner";
        command.Parameters.AddWithValue("@owner", Owner.Id);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var questDataOrdinal = reader.GetOrdinal("quest_data");
            var state = new CharacterTodayAssignmentState
            {
                RealStep = reader.GetInt32("real_step"),
                StepId = reader.GetInt32("step_id"),
                GroupId = reader.GetInt32("group_id"),
                QuestContextId = reader.GetInt32("quest_context_id"),
                QuestId = reader.GetInt64("quest_id"),
                QuestData = reader.IsDBNull(questDataOrdinal) ? null : (byte[])reader.GetValue(questDataOrdinal),
                QuestStatus = (QuestStatus)reader.GetByte("quest_status"),
                Status = (TodayAssignmentData)reader.GetByte("status"),
                UpdatedAt = reader.GetDateTime("updated_at")
            };
            _states[state.RealStep] = state;
        }

        Logger.Info("Today assignments loaded: player={0} ({1}), count={2}", Owner.Name, Owner.Id, _states.Count);

        foreach (var state in _states.Values)
        {
            AdoptOrRestoreQuestState(state);
            AdvanceStatePastCompletedChain(state);
            RefreshStatus(state);
        }
    }

    public void Save(MySqlConnection connection, MySqlTransaction transaction)
    {
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Connection = connection;
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM character_today_assignments WHERE owner = @owner";
            deleteCommand.Parameters.AddWithValue("@owner", Owner.Id);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var state in _states.Values)
        {
            SyncQuestState(state);

            using var command = connection.CreateCommand();
            command.Connection = connection;
            command.Transaction = transaction;
            command.CommandText =
                "REPLACE INTO character_today_assignments(`owner`,`real_step`,`step_id`,`group_id`,`quest_context_id`,`quest_id`,`quest_data`,`quest_status`,`status`,`updated_at`) " +
                "VALUES (@owner,@real_step,@step_id,@group_id,@quest_context_id,@quest_id,@quest_data,@quest_status,@status,@updated_at)";
            command.Parameters.AddWithValue("@owner", Owner.Id);
            command.Parameters.AddWithValue("@real_step", state.RealStep);
            command.Parameters.AddWithValue("@step_id", state.StepId);
            command.Parameters.AddWithValue("@group_id", state.GroupId);
            command.Parameters.AddWithValue("@quest_context_id", state.QuestContextId);
            command.Parameters.AddWithValue("@quest_id", state.QuestId);
            command.Parameters.AddWithValue("@quest_data", state.QuestData ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@quest_status", (byte)state.QuestStatus);
            command.Parameters.AddWithValue("@status", (byte)state.Status);
            command.Parameters.AddWithValue("@updated_at", state.UpdatedAt == default ? DateTime.UtcNow : state.UpdatedAt);
            command.ExecuteNonQuery();
        }
    }

    public void CheckDailyResetAtLogin()
    {
        var isOld = (DateTime.UtcNow.Date - Owner.LeaveTime.Date) >= TimeSpan.FromDays(1);
        if (isOld)
        {
            Reset(false);
        }
    }

    public void Send()
    {
        if (_states.Count == 0)
        {
            return;
        }

        foreach (var state in _states.Values.OrderBy(x => x.RealStep))
        {
            RefreshStatus(state);
            SyncQuestState(state);
            state.UpdatedAt = DateTime.UtcNow;
            ReplayQuestStartSync(state);
            Owner.SendPacket(new SCTodayAssignmentChangedPacket(state.StepId, state.GroupId, state.QuestContextId, state.Status, true));
        }
    }

    public void Reset(bool sendPackets)
    {
        if (_states.Count == 0)
        {
            return;
        }

        foreach (var state in _states.Values.ToList())
        {
            if (Owner.Quests.HasQuest((uint)state.QuestContextId))
            {
                Owner.Quests.DropQuest((uint)state.QuestContextId, sendPackets);
            }

            ClearQuestState(state);
        }

        _states.Clear();
    }

    private void OnQuestStepChanged(object sender, OnQuestStepChangedArgs args)
    {
        if (_suppressQuestStatePackets || _states.Count == 0)
        {
            return;
        }

        if (!TryGetStateByQuestId(args.QuestId, out var state))
        {
            return;
        }

        if (state.Status == TodayAssignmentData.Done)
        {
            return;
        }

        RefreshStatus(state);
        SyncQuestState(state);
        state.UpdatedAt = DateTime.UtcNow;
        Owner.SendPacket(new SCTodayAssignmentChangedPacket(state.StepId, state.GroupId, state.QuestContextId, state.Status, false));
    }

    private void OnQuestComplete(object sender, OnQuestCompleteArgs args)
    {
        if (_states.Count == 0)
        {
            return;
        }

        if (!TryGetStateByQuestId(args.QuestId, out var state))
        {
            return;
        }

        if (TryAdvanceAssignmentChain(state))
        {
            return;
        }

        state.Status = TodayAssignmentData.Done;
        ClearQuestState(state);
        state.UpdatedAt = DateTime.UtcNow;
        Owner.SendPacket(new SCTodayAssignmentChangedPacket(state.StepId, state.GroupId, state.QuestContextId, state.Status, false));
    }

    public void HandleRequest(int realStep, TodayAssignmentData request)
    {
        var selection = EnsureSelection(realStep);
        if (selection == null)
        {
            Owner.SendPacket(new SCTodayAssignmentChangedPacket(0, 0, 0, TodayAssignmentData.Locked, false));
            Logger.Warn("No eligible today assignment found for {0} ({1}), realStep {2}", Owner.Name, Owner.Id, realStep);
            return;
        }

        RefreshStatus(selection);
        SyncQuestState(selection);
        Logger.Info("Today assignment handle request: player={0} ({1}), realStep={2}, request={3}, stepId={4}, groupId={5}, questContextId={6}, status={7}, hasQuest={8}, hasCompleted={9}",
            Owner.Name, Owner.Id, realStep, request, selection.StepId, selection.GroupId, selection.QuestContextId, selection.Status,
            Owner.Quests.HasQuest((uint)selection.QuestContextId), Owner.Quests.HasQuestCompleted((uint)selection.QuestContextId));

        switch (request)
        {
            case TodayAssignmentData.Locked:
                break;
            case TodayAssignmentData.Ready:
                RefreshStatus(selection);
                SyncQuestState(selection);
                selection.UpdatedAt = DateTime.UtcNow;
                Owner.SendPacket(new SCTodayAssignmentChangedPacket(selection.StepId, selection.GroupId, selection.QuestContextId, selection.Status, false));
                break;
            case TodayAssignmentData.Progress:
                if (selection.Status != TodayAssignmentData.Done && !Owner.Quests.HasQuest((uint)selection.QuestContextId))
                {
                    Logger.Info("Today assignment start attempt: player={0} ({1}), realStep={2}, stepId={3}, groupId={4}, questContextId={5}",
                        Owner.Name, Owner.Id, realStep, selection.StepId, selection.GroupId, selection.QuestContextId);

                    _suppressQuestStatePackets = true;
                    bool started;
                    try
                    {
                        started = Owner.Quests.AddQuest((uint)selection.QuestContextId);
                        if (started)
                        {
                            PromoteQuestToProgressState(selection.QuestContextId);
                        }
                    }
                    finally
                    {
                        _suppressQuestStatePackets = false;
                    }

                    Logger.Info("Today assignment start result: player={0} ({1}), questContextId={2}, started={3}, hasQuest={4}, hasCompleted={5}",
                        Owner.Name, Owner.Id, selection.QuestContextId, started, Owner.Quests.HasQuest((uint)selection.QuestContextId),
                        Owner.Quests.HasQuestCompleted((uint)selection.QuestContextId));
                }
                else
                {
                    Logger.Info("Today assignment start skipped: player={0} ({1}), realStep={2}, questContextId={3}, status={4}, hasQuest={5}, hasCompleted={6}",
                        Owner.Name, Owner.Id, realStep, selection.QuestContextId, selection.Status,
                        Owner.Quests.HasQuest((uint)selection.QuestContextId), Owner.Quests.HasQuestCompleted((uint)selection.QuestContextId));
                }

                RefreshStatus(selection);
                SyncQuestState(selection);
                if (Owner.Quests.HasQuest((uint)selection.QuestContextId))
                {
                    selection.Status = TodayAssignmentData.Progress;
                }

                selection.UpdatedAt = DateTime.UtcNow;
                Owner.SendPacket(new SCTodayAssignmentChangedPacket(selection.StepId, selection.GroupId, selection.QuestContextId, selection.Status, false));
                break;
            case TodayAssignmentData.Done:
                RefreshStatus(selection);
                SyncQuestState(selection);
                selection.UpdatedAt = DateTime.UtcNow;
                Owner.SendPacket(new SCTodayAssignmentChangedPacket(selection.StepId, selection.GroupId, selection.QuestContextId, selection.Status, false));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request, null);
        }
    }

    private CharacterTodayAssignmentState EnsureSelection(int realStep)
    {
        if (_states.TryGetValue(realStep, out var existing))
        {
            if (GameData.IsAssignmentStillValid(Owner, existing))
            {
                existing.StepId = GameData.GetTodayQuestStepId(realStep);
                return existing;
            }

            DropExistingQuestState(existing, false);
        }

        var resolved = GameData.SelectAssignmentForCharacter(Owner, realStep);
        if (resolved == null)
        {
        return null;
        }

        var state = existing ?? new CharacterTodayAssignmentState { RealStep = realStep };
        state.StepId = resolved.StepId;
        state.GroupId = resolved.GroupId;
        state.QuestContextId = resolved.QuestContextId;
        state.QuestId = 0;
        state.QuestData = null;
        state.QuestStatus = QuestStatus.Invalid;
        state.Status = TodayAssignmentData.Ready;
        state.UpdatedAt = DateTime.UtcNow;
        _states[realStep] = state;
        return state;
    }

    private void RefreshStatus(CharacterTodayAssignmentState state)
    {
        if (state.Status == TodayAssignmentData.Done)
        {
            ClearQuestState(state);
            state.Status = TodayAssignmentData.Done;
            return;
        }

        if (Owner.Quests.HasQuest((uint)state.QuestContextId))
        {
            state.Status = TodayAssignmentData.Progress;
            return;
        }

        state.Status = TodayAssignmentData.Ready;
    }

    public bool OwnsQuest(uint questContextId)
    {
        return _states.Values.Any(x => x.QuestContextId == questContextId);
    }

    public IReadOnlyList<(int RealStep, int GroupId, int QuestContextId)> GetAssignedSelections()
    {
        return _states.Values
            .Select(state => (state.RealStep, state.GroupId, state.QuestContextId))
            .ToList();
    }

    private bool TryGetStateByQuestId(uint questContextId, out CharacterTodayAssignmentState state)
    {
        state = _states.Values.FirstOrDefault(x => x.QuestContextId == questContextId);
        return state != null;
    }

    private void AdoptOrRestoreQuestState(CharacterTodayAssignmentState state)
    {
        if (Owner.Quests.TryGetActiveQuest((uint)state.QuestContextId, out var existingQuest))
        {
            state.QuestId = existingQuest.Id;
            state.QuestData = existingQuest.WriteData();
            state.QuestStatus = existingQuest.Status;
            Owner.Quests.MarkQuestRemovedFromPersistence((uint)state.QuestContextId);
            Logger.Info("Today assignment adopted legacy quest: player={0} ({1}), realStep={2}, questContextId={3}, questId={4}",
                Owner.Name, Owner.Id, state.RealStep, state.QuestContextId, state.QuestId);
            return;
        }

        if (state.Status != TodayAssignmentData.Progress || state.QuestData == null || state.QuestData.Length == 0)
        {
            return;
        }

        var template = QuestManager.Instance.GetTemplate((uint)state.QuestContextId);
        if (template == null)
        {
            Logger.Warn("Today assignment restore skipped because quest template is missing: player={0} ({1}), realStep={2}, questContextId={3}",
                Owner.Name, Owner.Id, state.RealStep, state.QuestContextId);
            ClearQuestState(state);
            return;
        }

        if (state.QuestId <= 0)
        {
            state.QuestId = QuestIdManager.Instance.GetNextId();
            Logger.Warn("Today assignment restore allocated missing runtime quest id: player={0} ({1}), realStep={2}, questContextId={3}, questId={4}",
                Owner.Name, Owner.Id, state.RealStep, state.QuestContextId, state.QuestId);
        }

        var quest = new Quest(template, Owner)
        {
            Id = state.QuestId,
            TemplateId = (uint)state.QuestContextId,
            Status = state.QuestStatus
        };
        quest.ReadData(state.QuestData);
        quest.Status = state.QuestStatus;
        Owner.Quests.AttachQuest(quest);
        Owner.Quests.MarkQuestRemovedFromPersistence((uint)state.QuestContextId);
        state.NeedsClientQuestStartSync = true;
        Logger.Info("Today assignment restored runtime quest: player={0} ({1}), realStep={2}, questContextId={3}, questId={4}",
            Owner.Name, Owner.Id, state.RealStep, state.QuestContextId, state.QuestId);
    }

    private void SyncQuestState(CharacterTodayAssignmentState state)
    {
        if (Owner.Quests.TryGetActiveQuest((uint)state.QuestContextId, out var quest))
        {
            state.QuestId = quest.Id;
            state.QuestData = quest.WriteData();
            state.QuestStatus = quest.Status;
            Owner.Quests.MarkQuestRemovedFromPersistence((uint)state.QuestContextId);
            return;
        }

        if (state.Status != TodayAssignmentData.Progress)
        {
            ClearQuestState(state);
        }
    }

    private void DropExistingQuestState(CharacterTodayAssignmentState state, bool sendPackets)
    {
        if (Owner.Quests.HasQuest((uint)state.QuestContextId))
        {
            Owner.Quests.DropQuest((uint)state.QuestContextId, sendPackets);
        }

        ClearQuestState(state);
    }

    private static void ClearQuestState(CharacterTodayAssignmentState state)
    {
        state.QuestId = 0;
        state.QuestData = null;
        state.QuestStatus = QuestStatus.Invalid;
        state.NeedsClientQuestStartSync = false;
    }

    private void AdvanceStatePastCompletedChain(CharacterTodayAssignmentState state)
    {
        while (!Owner.Quests.HasQuest((uint)state.QuestContextId) && Owner.Quests.HasQuestCompleted((uint)state.QuestContextId))
        {
            var nextQuestContextId = GameData.GetNextQuestContextId(Owner, state.GroupId, state.QuestContextId);
            if (nextQuestContextId <= 0)
            {
                break;
            }

            state.QuestContextId = nextQuestContextId;
            ClearQuestState(state);
            state.Status = TodayAssignmentData.Ready;
            state.UpdatedAt = DateTime.UtcNow;
        }
    }

    private bool TryAdvanceAssignmentChain(CharacterTodayAssignmentState state)
    {
        var nextQuestContextId = GameData.GetNextQuestContextId(Owner, state.GroupId, state.QuestContextId);
        if (nextQuestContextId <= 0)
        {
            return false;
        }

        state.QuestContextId = nextQuestContextId;
        ClearQuestState(state);
        state.Status = TodayAssignmentData.Ready;
        state.UpdatedAt = DateTime.UtcNow;

        _suppressQuestStatePackets = true;
        try
        {
            Owner.Quests.AddQuest((uint)state.QuestContextId);
            PromoteQuestToProgressState(state.QuestContextId);
        }
        finally
        {
            _suppressQuestStatePackets = false;
        }

        RefreshStatus(state);
        SyncQuestState(state);
        Logger.Info("Today assignment advanced to next quest in chain: player={0} ({1}), realStep={2}, groupId={3}, questContextId={4}, status={5}",
            Owner.Name, Owner.Id, state.RealStep, state.GroupId, state.QuestContextId, state.Status);
        Owner.SendPacket(new SCTodayAssignmentChangedPacket(state.StepId, state.GroupId, state.QuestContextId, state.Status, false));
        return true;
    }

    private void PromoteQuestToProgressState(int questContextId)
    {
        if (!Owner.Quests.TryGetActiveQuest((uint)questContextId, out var quest))
        {
            return;
        }

        var guard = 0;
        while (quest.Step != QuestComponentKind.Progress && guard < 8)
        {
            guard++;
            var advanced = quest.RunCurrentStep();
            if (!advanced && quest.Step == QuestComponentKind.Progress)
            {
                break;
            }
        }

        NormalizeQuestForClientSync(quest);
    }

    private void ReplayQuestStartSync(CharacterTodayAssignmentState state)
    {
        if (!state.NeedsClientQuestStartSync)
        {
            return;
        }

        if (!Owner.Quests.TryGetActiveQuest((uint)state.QuestContextId, out var quest))
        {
            return;
        }

        NormalizeQuestForClientSync(quest);

        var startComponentId = GetFirstComponentId(quest.Template, QuestComponentKind.Start);
        Quest startedQuest;
        _suppressQuestStatePackets = true;
        try
        {
            startedQuest = CreateStartedQuestView(quest, startComponentId);
        }
        finally
        {
            _suppressQuestStatePackets = false;
        }

        Owner.SendPacket(new SCQuestContextStartedPacket(startedQuest, startComponentId));
        Owner.SendPacket(new SCQuestContextUpdatedPacket(quest, quest.ComponentId));
        state.NeedsClientQuestStartSync = false;
        Logger.Info("Today assignment replayed quest start sync: player={0} ({1}), realStep={2}, questContextId={3}, questId={4}, componentId={5}, step={6}, status={7}",
            Owner.Name, Owner.Id, state.RealStep, state.QuestContextId, quest.Id, quest.ComponentId, quest.Step, quest.Status);
    }

    private static void NormalizeQuestForClientSync(Quest quest)
    {
        if (quest.Status == QuestStatus.Invalid)
        {
            quest.Status = quest.Step switch
            {
                QuestComponentKind.Progress => QuestStatus.Progress,
                QuestComponentKind.Ready => QuestStatus.Ready,
                QuestComponentKind.Reward => QuestStatus.Completed,
                _ => QuestStatus.Progress
            };
        }

        if (quest.ComponentId == 0)
        {
            quest.ComponentId = GetFirstComponentId(quest.Template, quest.Step);
            if (quest.ComponentId == 0 && quest.Step == QuestComponentKind.Progress)
            {
                quest.ComponentId = GetFirstComponentId(quest.Template, QuestComponentKind.Start);
            }
        }
    }

    private static Quest CreateStartedQuestView(Quest sourceQuest, uint startComponentId)
    {
        var startedQuest = new Quest(sourceQuest.Template, sourceQuest.Owner)
        {
            Id = sourceQuest.Id,
            TemplateId = sourceQuest.TemplateId,
            Status = QuestStatus.Progress,
            Condition = sourceQuest.Condition,
            QuestAcceptorType = sourceQuest.QuestAcceptorType,
            AcceptorId = sourceQuest.AcceptorId,
            DoodadId = sourceQuest.DoodadId,
            Time = sourceQuest.Time,
            SelectedRewardIndex = sourceQuest.SelectedRewardIndex,
            ReadyToReportNpc = sourceQuest.ReadyToReportNpc,
            AllowItemRewards = sourceQuest.AllowItemRewards,
            QuestRewardRatio = sourceQuest.QuestRewardRatio,
            IsCheckSet = sourceQuest.IsCheckSet,
            ComponentId = startComponentId
        };

        startedQuest.Objectives = (int[])sourceQuest.Objectives.Clone();
        startedQuest.ProgressStepResults = sourceQuest.ProgressStepResults.ToList();
        startedQuest.Step = QuestComponentKind.Start;

        return startedQuest;
    }

    private static uint GetFirstComponentId(IQuestTemplate template, QuestComponentKind step)
    {
        return template.GetComponents(step).FirstOrDefault()?.Id ?? 0;
    }

    public static bool IsWestRace(Race race)
    {
        return race is Race.Nuian or Race.Elf or Race.Dwarf;
    }

    public static bool IsEastRace(Race race)
    {
        return race is Race.Hariharan or Race.Ferre or Race.Warborn;
    }

    public static TodayAssignmentFactionSide GetFactionSide(Character character)
    {
        if (IsWestRace(character.Race))
        {
            return TodayAssignmentFactionSide.West;
        }

        if (IsEastRace(character.Race))
        {
            return TodayAssignmentFactionSide.East;
        }

        return TodayAssignmentFactionSide.Neutral;
    }

    public static TodayAssignmentFactionSide GetFactionSideForQuests(IEnumerable<uint> questContextIds)
    {
        var hasWest = false;
        var hasEast = false;

        foreach (var questContextId in questContextIds)
        {
            if (WestPatrolQuestIds.Contains(questContextId) || WestHighLevelQuestIds.Contains(questContextId))
            {
                hasWest = true;
            }

            if (EastPatrolQuestIds.Contains(questContextId) || EastHighLevelQuestIds.Contains(questContextId))
            {
                hasEast = true;
            }
        }

        if (hasWest && !hasEast)
        {
            return TodayAssignmentFactionSide.West;
        }

        if (hasEast && !hasWest)
        {
            return TodayAssignmentFactionSide.East;
        }

        return TodayAssignmentFactionSide.Neutral;
    }

    public static TodayAssignmentFactionSide GetFactionSideForQuest(uint questContextId)
    {
        if (WestPatrolQuestIds.Contains(questContextId) || WestHighLevelQuestIds.Contains(questContextId))
        {
            return TodayAssignmentFactionSide.West;
        }

        if (EastPatrolQuestIds.Contains(questContextId) || EastHighLevelQuestIds.Contains(questContextId))
        {
            return TodayAssignmentFactionSide.East;
        }

        return TodayAssignmentFactionSide.Neutral;
    }
}
