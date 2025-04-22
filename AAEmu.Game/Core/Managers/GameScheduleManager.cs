using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Schedules;

using NCrontab;

using NLog;

using static System.String;

using DayOfWeek = AAEmu.Game.Models.Game.Schedules.DayOfWeek;

namespace AAEmu.Game.Core.Managers;

public class GameScheduleManager : Singleton<GameScheduleManager>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private bool _loaded = false;
    private Dictionary<int, GameSchedules> _gameSchedules; // GameScheduleId, GameSchedules
    private Dictionary<int, GameScheduleSpawners> _gameScheduleSpawners;
    private Dictionary<int, List<int>> _gameScheduleSpawnerIds;
    private Dictionary<int, GameScheduleDoodads> _gameScheduleDoodads;
    private Dictionary<int, List<int>> _gameScheduleDoodadIds;
    private Dictionary<int, GameScheduleQuests> _gameScheduleQuests;
    private Dictionary<uint, ScheduleItems> _scheduleItems;
    private List<int> GameScheduleId { get; set; }

    public void Load()
    {
        if (_loaded)
            return;

        Logger.Info("Loading schedules...");

        SchedulesGameData.Instance.PostLoad();

        LoadGameScheduleSpawnersData(); // добавил разделение spawnerId для Npc & Doodads

        Logger.Info("Loaded schedules");

        _loaded = true;
    }

    public void LoadGameSchedules(Dictionary<int, GameSchedules> gameSchedules)
    {
        //_gameSchedules = new Dictionary<int, GameSchedules>();
        //foreach (var gs in gameSchedules)
        //{
        //    _gameSchedules.TryAdd(gs.Key, gs.Value);
        //}
        _gameSchedules = gameSchedules;
    }

    public void LoadGameScheduleSpawners(Dictionary<int, GameScheduleSpawners> gameScheduleSpawners)
    {
        _gameScheduleSpawners = gameScheduleSpawners;
    }

    public void LoadGameScheduleDoodads(Dictionary<int, GameScheduleDoodads> gameScheduleDoodads)
    {
        _gameScheduleDoodads = gameScheduleDoodads;
    }

    public void LoadGameScheduleQuests(Dictionary<int, GameScheduleQuests> gameScheduleQuests)
    {
        _gameScheduleQuests = gameScheduleQuests;
    }

    public void LoadScheduleItems(Dictionary<uint, ScheduleItems> scheduleItems)
    {
        _scheduleItems = scheduleItems;
    }

    public bool CheckSpawnerInScheduleSpawners(int spawnerId)
    {
        return _gameScheduleSpawnerIds.ContainsKey(spawnerId);
    }

    public bool CheckDoodadInScheduleSpawners(int doodadId)
    {
        return _gameScheduleDoodadIds.ContainsKey(doodadId);
    }

    //public bool CheckDoodadInScheduleSpawners(int spawnerId)
    //{
    //    return _gameScheduleDoodads.ContainsKey(spawnerId);
    //}

    public bool CheckSpawnerInGameSchedules(int spawnerId)
    {
        var res = CheckSpawnerScheduler(spawnerId);
        return res;
    }

    public bool CheckDoodadInGameSchedules(uint doodadId)
    {
        var res = CheckDoodadScheduler((int)doodadId);
        return res;
    }

    private bool CheckSpawnerScheduler(int spawnerId)
    {
        var res = false;
        foreach (var gameScheduleId in _gameScheduleSpawnerIds[spawnerId])
        {
            if (_gameSchedules.TryGetValue(gameScheduleId, out var gs))
            {
                res = true;
            }
        }

        return res;
    }

    private bool CheckDoodadScheduler(int doodadId)
    {
        var res = false;
        foreach (var gameScheduleId in _gameScheduleDoodadIds[doodadId])
        {
            if (_gameSchedules.TryGetValue(gameScheduleId, out var gs))
            {
                res = true;
            }
        }

        return res;
    }

    public enum PeriodStatus
    {
        NotFound,   // If doodadId or spawnerId not found
        NotStarted, // The period has not started
        InProgress, // Period in progress
        Ended       // The period has ended
    }

    /// <summary>
    /// Returns enum that shows the overall period status for all GameSchedules associated with spawnerId.
    /// </summary>
    /// <param name="spawnerId"></param>
    /// <returns></returns>
    public PeriodStatus GetPeriodStatusNpc(int spawnerId)
    {
        if (!_gameScheduleSpawnerIds.TryGetValue(spawnerId, out var ids))
            return PeriodStatus.NotFound; // If spawnerId is not found

        return CheckPeriodStatus(ids);
    }

    /// <summary>
    /// Returns an enum that shows the overall period status for all GameSchedules associated with the specified doodadId.
    /// If the doodadId is not found, returns <see cref="PeriodStatus.NotFound"/>.
    /// </summary>
    /// <param name="doodadId">The ID of the doodad to check.</param>
    /// <returns>The overall period status.</returns>
    public PeriodStatus GetPeriodStatus(int doodadId)
    {
        if (!_gameScheduleDoodadIds.TryGetValue(doodadId, out var ids))
            return PeriodStatus.NotFound; // If doodadId is not found

        return CheckPeriodStatus(ids);
    }

    /// <summary>
    /// Checks the period status for a list of game schedule IDs.
    /// </summary>
    /// <param name="ids">The list of game schedule IDs to check.</param>
    /// <returns>The overall period status.</returns>
    private PeriodStatus CheckPeriodStatus(List<int> ids)
    {
        var hasNotStarted = true;  // Assume that no period has started
        var hasInProgress = false; // Assume that no period is in progress
        var hasEnded = false;      // Assume that no period has ended

        foreach (var gameScheduleId in ids)
        {
            if (_gameSchedules.TryGetValue(gameScheduleId, out var gs))
            {
                var (started, ended) = CheckData(gs);

                if (started && !ended)
                {
                    hasInProgress = true;  // The period has started, but has not yet ended
                    hasNotStarted = false; // At least one period has started, so "hasn't started" = false
                }
                else if (ended)
                {
                    hasEnded = true;       // At least one period has ended
                    hasNotStarted = false; // At least one period has ended, so "hasn't started" = false
                }
            }
        }

        // Determine the final status
        if (hasInProgress)
            return PeriodStatus.InProgress;
        if (hasEnded)
            return PeriodStatus.Ended;
        return PeriodStatus.NotStarted;
    }

    /// <summary>
    /// Каждый кортеж будет содержать информацию о том,
    /// начался и завершился ли период для каждого GameSchedules,
    /// связанного с doodadId
    /// </summary>
    /// <param name="doodadId"></param>
    /// <returns></returns>
    public List<(bool, bool)> PeriodHasAlreadyBegunDoodad(int doodadId)
    {
        var results = new List<(bool, bool)>();

        if (!_gameScheduleDoodadIds.TryGetValue(doodadId, out var ids))
        {
            return results; // Возвращаем пустой список, если doodadId не найден
        }

        foreach (var gameScheduleId in ids)
        {
            if (_gameSchedules.TryGetValue(gameScheduleId, out var gs))
            {
                var (hasStarted, hasEnded) = CheckData(gs);
                results.Add((hasStarted, hasEnded));
            }
        }

        return results;
    }

    //public bool PeriodHasAlreadyBegunNpc(int spawnerId)
    //{
    //    var res = new List<bool>();
    //    foreach (var gameScheduleId in _gameScheduleSpawnerIds[spawnerId])
    //    {
    //        if (_gameSchedules.TryGetValue(gameScheduleId, out var gs))
    //        {
    //            res.Add(CheckData(gs));
    //        }
    //    }

    //    return res.Contains(true);
    //}

    //private List<bool> CheckScheduler()
    //{
    //    var res = new List<bool>();
    //    foreach (var gameScheduleId in GameScheduleId)
    //    {
    //        if (_gameSchedules.TryGetValue(gameScheduleId, out var gs))
    //        {
    //            res.Add(CheckData(gs));
    //        }
    //    }

    //    return res;
    //}

    public ScheduleItems GetScheduleItem(uint id)
    {
        _scheduleItems.TryGetValue(id, out var value);
        return value;
    }

    public string GetCronRemainingTime(int spawnerId, bool start = true)
    {
        var cronExpression = Empty;
        if (!_gameScheduleSpawnerIds.TryGetValue(spawnerId, out var ids))
        {
            return cronExpression;
        }

        foreach (var gameScheduleId in ids)
        {
            if (!_gameSchedules.TryGetValue(gameScheduleId, out var gameSchedules)) { continue; }
            cronExpression = GetCronExpression(gameSchedules, start);
        }

        return cronExpression;
    }

    public string GetDoodadCronRemainingTime(int doodadId, bool start = true)
    {
        try
        {
            var cronExpression = Empty;
            if (!_gameScheduleDoodadIds.TryGetValue(doodadId, out var ids))
            {
                return cronExpression;
            }

            foreach (var gameScheduleId in ids)
            {
                if (!_gameSchedules.TryGetValue(gameScheduleId, out var gameSchedules)) { continue; }
                cronExpression = GetCronExpression(gameSchedules, start);
            }

            return cronExpression;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in GetDoodadCronRemainingTime: {ex.Message}");
            return Empty;
        }
    }

    public List<string> GetListDoodadCronRemainingTime(int doodadId, bool start = true)
    {
        var cronExpressions = new List<string>();
        if (!_gameScheduleDoodadIds.TryGetValue(doodadId, out var ids))
        {
            return cronExpressions;
        }

        foreach (var gameScheduleId in ids)
        {
            if (!_gameSchedules.TryGetValue(gameScheduleId, out var gameSchedules)) { continue; }
            cronExpressions.Add(GetCronExpression(gameSchedules, start));
        }

        return cronExpressions;
    }

    public TimeSpan GetRemainingTime(int spawnerId, bool start = true)
    {
        if (!_gameScheduleSpawnerIds.ContainsKey(spawnerId))
        {
            return TimeSpan.Zero;
        }

        var remainingTime = TimeSpan.MaxValue;

        foreach (var gameScheduleId in _gameScheduleSpawnerIds[spawnerId])
        {
            if (!_gameSchedules.TryGetValue(gameScheduleId, out var gameSchedules)) { continue; }

            var timeSpan = start ? GetRemainingTimeStart(gameSchedules) : GetRemainingTimeEnd(gameSchedules);
            if (timeSpan <= remainingTime)
            {
                remainingTime = timeSpan;
            }
        }

        return remainingTime;
    }

    public bool HasGameScheduleSpawnersData(uint spawnerTemplateId)
    {
        return _gameScheduleSpawners.Values.Any(gss => gss.SpawnerId == spawnerTemplateId);
    }

    private void LoadGameScheduleSpawnersData()
    {
        // Spawners
        _gameScheduleSpawnerIds = new Dictionary<int, List<int>>();
        foreach (var gss in _gameScheduleSpawners.Values)
        {
            if (!_gameScheduleSpawnerIds.ContainsKey(gss.SpawnerId))
            {
                _gameScheduleSpawnerIds.Add(gss.SpawnerId, new List<int> { gss.GameScheduleId });
            }
            else
            {
                _gameScheduleSpawnerIds[gss.SpawnerId].Add(gss.GameScheduleId);
            }
        }

        // Doodads
        _gameScheduleDoodadIds = new Dictionary<int, List<int>>();
        foreach (var gsd in _gameScheduleDoodads.Values)
        {
            if (!_gameScheduleDoodadIds.ContainsKey(gsd.DoodadId))
            {
                _gameScheduleDoodadIds.Add(gsd.DoodadId, new List<int> { gsd.GameScheduleId });
            }
            else
            {
                _gameScheduleDoodadIds[gsd.DoodadId].Add(gsd.GameScheduleId);
            }
        }
        //TODO: quests data
    }

    public bool GetGameScheduleDoodadsData(uint doodadId)
    {
        GameScheduleId = new List<int>();
        foreach (var gsd in _gameScheduleDoodads.Values)
        {
            if (gsd.DoodadId != doodadId) { continue; }
            GameScheduleId.Add(gsd.GameScheduleId);
        }
        return GameScheduleId.Count != 0;
    }

    public bool GetGameScheduleQuestsData(uint questId)
    {
        GameScheduleId = new List<int>();
        foreach (var gsq in _gameScheduleQuests.Values)
        {
            if (gsq.QuestId != questId) { continue; }
            GameScheduleId.Add(gsq.GameScheduleId);
        }
        return GameScheduleId.Count != 0;
    }

    private static (bool hasStarted, bool hasEnded) CheckData(GameSchedules value)
    {
        var now = DateTime.UtcNow;
        var currentTime = now.TimeOfDay;
        var currentDate = now.Date;

        // Преобразуем стандартный DayOfWeek в ваш кастомный DayOfWeek
        var currentDayOfWeek = (DayOfWeek)((int)now.DayOfWeek + 1);

        // Проверка на нулевые дату и месяц
        var startDate = value is { StYear: > 0, StMonth: > 0, StDay: > 0 }
            ? new DateTime(value.StYear, value.StMonth, value.StDay)
            : DateTime.MinValue;

        var endDate = value is { EdYear: > 0, EdMonth: > 0, EdDay: > 0 }
            ? new DateTime(value.EdYear, value.EdMonth, value.EdDay)
            : DateTime.MaxValue;

        var startTime = new TimeSpan(value.StartTime, value.StartTimeMin, 0);
        var endTime = new TimeSpan(value.EndTime, value.EndTimeMin, 0);

        var hasStarted = false;
        var hasEnded = false;

        // Проверка на попадание в период по дате и времени
        if ((startDate == DateTime.MinValue || currentDate > startDate || (currentDate == startDate && currentTime >= startTime)) &&
            (endDate == DateTime.MaxValue || currentDate < endDate || (currentDate == endDate && currentTime <= endTime)))
        {
            // Проверка на попадание в период по дню недели
            if (currentDayOfWeek == value.DayOfWeekId || value.DayOfWeekId == DayOfWeek.Invalid)
            {
                hasStarted = true;
            }
        }

        // Проверка на окончание периода
        if (endDate != DateTime.MaxValue && (currentDate > endDate || (currentDate == endDate && currentTime >= endTime)))
        {
            hasEnded = true;
        }

        return (hasStarted, hasEnded);
    }

    private static (bool hasStarted, bool hasEnded) CheckData0(GameSchedules value)
    {
        var now = DateTime.UtcNow;
        var currentTime = now.TimeOfDay;
        var currentDate = now.Date;

        var startDate = new DateTime(value.StYear, value.StMonth, value.StDay);
        var endDate = new DateTime(value.EdYear, value.EdMonth, value.EdDay);
        var startTime = new TimeSpan(value.StartTime, value.StartTimeMin, 0);
        var endTime = new TimeSpan(value.EndTime, value.EndTimeMin, 0);

        var hasStarted = false;
        var hasEnded = false;

        // Проверка, начался ли период
        if (currentDate > startDate || (currentDate == startDate && currentTime >= startTime))
        {
            hasStarted = true;
        }

        // Проверка, завершился ли период
        if (currentDate > endDate || (currentDate == endDate && currentTime >= endTime))
        {
            hasEnded = true;
        }

        return (hasStarted, hasEnded);
    }

    private static bool CheckData2(GameSchedules value)
    {
        var now = DateTime.UtcNow;
        var currentTime = now.TimeOfDay;
        var currentDate = now.Date;

        var startDate = new DateTime(value.StYear, value.StMonth, value.StDay);
        var endDate = new DateTime(value.EdYear, value.EdMonth, value.EdDay);
        var startTime = new TimeSpan(value.StartTime, value.StartTimeMin, 0);
        var endTime = new TimeSpan(value.EndTime, value.EndTimeMin, 0);

        if (value.DayOfWeekId == DayOfWeek.Invalid)
        {
            return CheckCommonConditions();
        }

        if ((int)now.DayOfWeek + 1 == (int)value.DayOfWeekId)
        {
            return CheckCommonConditions();
        }

        return false;

        // Локальная функция для проверки общих условий
        bool CheckCommonConditions()
        {
            if (value is { EndTime: 0, StMonth: 0, StDay: 0, StHour: 0 })
            {
                return true;
            }

            if (currentDate >= startDate && currentDate <= endDate)
            {
                if (value.EndTime == 0)
                {
                    return true;
                }

                if (currentDate == startDate && currentTime >= startTime)
                {
                    return true;
                }

                if (currentDate == endDate && currentTime <= endTime)
                {
                    return true;
                }

                if (currentDate > startDate && currentDate < endDate)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static TimeSpan GetRemainingTimeStart(GameSchedules value)
    {
        var cronExpression = GetCronExpression(value, true);
        var schedule = CrontabSchedule.Parse(cronExpression, TaskManager.s_crontabScheduleParseOptions);
        return schedule.GetNextOccurrence(DateTime.UtcNow) - DateTime.UtcNow;
    }

    public static TimeSpan GetRemainingTimeEnd(GameSchedules value)
    {
        var cronExpression = GetCronExpression(value, false);
        var schedule = CrontabSchedule.Parse(cronExpression, TaskManager.s_crontabScheduleParseOptions);
        return schedule.GetNextOccurrence(DateTime.UtcNow) - DateTime.UtcNow;
    }

    private static string GetCronExpression(GameSchedules value, bool start = true)
    {
        // Validate input
        if (value.StMonth < 1 || value.StMonth > 12 || value.StDay < 1 || value.StDay > 31 ||
            value.StHour < 0 || value.StHour > 23 || value.StMin < 0 || value.StMin > 59)
        {
            throw new ArgumentException("Invalid GameSchedules data for cron expression.");
        }

        // Determine time and date components
        var hour = start ? value.StHour : value.EdHour;
        var minute = start ? value.StMin : value.EdMin;
        var day = start ? value.StDay : value.EdDay;
        var month = start ? value.StMonth : value.EdMonth;
        var dayOfWeek = value.DayOfWeekId == DayOfWeek.Invalid ? "*" : ((int)value.DayOfWeekId).ToString();

        // Build cron expression
        var cronExpression = BuildCronExpression(0, minute, hour, day, month, dayOfWeek);

        // Replace unsupported characters
        cronExpression = cronExpression.Replace("?", "*");

        Logger.Debug($"Generated cron expression: {cronExpression}");
        return cronExpression;
    }

    // Helper method to build cron expressions
    private static string BuildCronExpression(int seconds, int minute, int hour, int day, int month, string dayOfWeek)
    {
        return $"{seconds} {minute} {hour} {day} {month} {dayOfWeek}";
    }

    private static string GetCronExpression1(GameSchedules value, bool start = true)
    {
        /*
            Cron-выражение состоит из 6 или 7 полей:
            1. Секунды (0-59)
            2. Минуты (0-59)
            3. Часы (0-23)
            4. День месяца (1-31)
            5. Месяц (1-12)
            6. День недели (0-7, где 0 и 7 — воскресенье)
            7. Год (опционально, не поддерживается в стандартных cron)
        */

        // Преобразуем DayOfWeek в формат cron (0-7, где 0 и 7 — воскресенье)
        var dayOfWeek = value.DayOfWeekId switch
        {
            DayOfWeek.Sunday => 0,
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            _ => (int)DayOfWeek.Invalid // Используем Invalid для обозначения "не указано"
        };

        // Получаем значения времени и даты
        var stMonth = value.StMonth;
        var stDay = value.StDay;
        var stHour = start ? value.StHour : value.EdHour;
        var stMinute = start ? value.StMin : value.EdMin;

        var edMonth = value.EdMonth;
        var edDay = value.EdDay;
        var edHour = value.EdHour;
        var edMinute = value.EdMin;

        var startTime = value.StartTime;
        var startTimeMin = value.StartTimeMin;
        var endTime = value.EndTime;
        var endTimeMin = value.EndTimeMin;

        string cronExpression;

        if (value.DayOfWeekId == DayOfWeek.Invalid)
        {
            switch (start)
            {
                case true:
                    {
                        switch (value)
                        {
                            case { StartTime: 0, EndTime: 0, StMonth: 0, StDay: 0, StHour: 0 }:
                                {
                                    cronExpression = BuildCronExpression(0, 0, 0, stDay, stMonth, (int)DayOfWeek.Invalid);
                                    break;
                                }
                            case { EndTime: > 0, StMonth: 0, StDay: 0, StHour: 0 }:
                            case { EndTime: > 0, StMonth: > 0, StDay: > 0 }:
                                {
                                    cronExpression = BuildCronExpression(0, startTimeMin, startTime, stDay, stMonth, (int)DayOfWeek.Invalid);
                                    break;
                                }
                            //case { StartTime: 0, EndTime: 0, StMonth: 0, StDay: 0 }:
                            //case { StartTime: 0, EndTime: 0, StMonth: > 0, StDay: > 0 }:
                            //    {
                            //        cronExpression = BuildCronExpression(0, stMinute, stHour, stDay, stMonth, (int)DayOfWeek.Invalid);
                            //        break;
                            //    }
                            default:
                                {
                                    cronExpression = BuildCronExpression(0, stMinute, stHour, stDay, stMonth, (int)DayOfWeek.Invalid);
                                    break;
                                }
                        }

                        break;
                    }
                default:
                    {
                        switch (value)
                        {
                            case { StartTime: 0, EndTime: 0, EdMonth: 0, EdDay: 0, EdHour: 0 }:
                                {
                                    cronExpression = BuildCronExpression(0, 0, 0, edDay, edMonth, (int)DayOfWeek.Invalid);
                                    break;
                                }
                            case { EndTime: > 0, EdMonth: 0, EdDay: 0, EdHour: 0 }:
                            case { EndTime: > 0, EdMonth: > 0, EdDay: > 0 }:
                                {
                                    cronExpression = BuildCronExpression(0, endTimeMin, endTime, edDay, edMonth, (int)DayOfWeek.Invalid);
                                    break;
                                }
                            //case { StartTime: 0, EndTime: 0, EdMonth: 0, EdDay: 0 }:
                            //case { StartTime: 0, EndTime: 0, EdMonth: > 0, EdDay: > 0 }:
                            //    {
                            //        cronExpression = BuildCronExpression(0, edMinute, edHour, edDay, edMonth, (int)DayOfWeek.Invalid);
                            //        break;
                            //    }
                            default:
                                {
                                    cronExpression = BuildCronExpression(0, edMinute, edHour, edDay, edMonth, (int)DayOfWeek.Invalid);
                                    break;
                                }
                        }

                        break;
                    }
            }
        }
        else
        {
            switch (start)
            {
                case true:
                    {
                        switch (value)
                        {
                            case { StartTime: 0, EndTime: 0, StMonth: 0, StDay: 0, StHour: 0 }:
                                {
                                    cronExpression = BuildCronExpression(0, 0, 0, stDay, stMonth, dayOfWeek);
                                    break;
                                }
                            case { EndTime: > 0, StMonth: 0, StDay: 0, StHour: 0 }:
                            case { EndTime: > 0, StMonth: > 0, StDay: > 0 }:
                                {
                                    cronExpression = BuildCronExpression(0, startTimeMin, startTime, stDay, stMonth, dayOfWeek);
                                    break;
                                }
                            //case { StartTime: 0, EndTime: 0, StMonth: 0, StDay: 0 }:
                            //case { StartTime: 0, EndTime: 0, StMonth: > 0, StDay: > 0 }:
                            //    {
                            //        cronExpression = BuildCronExpression(stMinute, stHour, stDay, stMonth, dayOfWeek);
                            //        break;
                            //    }
                            default:
                                {
                                    cronExpression = BuildCronExpression(0, stMinute, stHour, stDay, stMonth, dayOfWeek);
                                    break;
                                }
                        }

                        break;
                    }
                default:
                    {
                        switch (value)
                        {
                            case { StartTime: 0, EndTime: 0, EdMonth: 0, EdDay: 0, EdHour: 0 }:
                                {
                                    cronExpression = BuildCronExpression(0, 0, 0, edDay, edMonth, dayOfWeek);
                                    break;
                                }
                            case { EndTime: > 0, EdMonth: 0, EdDay: 0, EdHour: 0 }:
                            case { EndTime: > 0, EdMonth: > 0, EdDay: > 0 }:
                                {
                                    cronExpression = BuildCronExpression(0, endTimeMin, endTime, edDay, edMonth, dayOfWeek);
                                    break;
                                }
                            //case { StartTime: 0, EndTime: 0, EdMonth: 0, EdDay: 0 }:
                            //case { StartTime: 0, EndTime: 0, EdMonth: > 0, EdDay: > 0 }:
                            //    {
                            //        cronExpression = BuildCronExpression(edMinute, edHour, edDay, edMonth, dayOfWeek);
                            //        break;
                            //    }
                            default:
                                {
                                    cronExpression = BuildCronExpression(0, edMinute, edHour, edDay, edMonth, dayOfWeek);
                                    break;
                                }
                        }

                        break;
                    }
            }
        }

        cronExpression = cronExpression.Replace("?", "*"); // Crontab doesn't support ?, so we replace it with */1 instead

        return cronExpression;

        // Локальная функция для формирования cron-выражения
        string BuildCronExpression(int seconds, int minute, int hour, int day, int month, int dayOfWeekCron)
        {
            return dayOfWeek switch
            {
                8 => $"{seconds} {minute} {hour} {day} {month} *",
                _ => $"{seconds} {minute} {hour} {day} {month} {dayOfWeekCron}"
            };
        }
    }

    private static string GetCronExpression0(GameSchedules value, bool start = true)
    {
        /*
           1. Seconds / Секунды
           2. Minutes / Минуты
           3. Hours / Часы
           4. Day of the month / День месяца
           5. Month / Месяц
           6. Day of the week / День недели
           7. Year (optional field) / Год (необязательное поле) // Not supported by Crontab
        */

        var dayOfWeek = value.DayOfWeekId switch
        {
            DayOfWeek.Sunday => 0,
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            _ => 8
        };

        //var stYear = value.StYear;
        var stMonth = value.StMonth;
        var stDay = value.StDay;
        var stHour = value.StHour;
        var stMinute = value.StMin;
        var startTime = value.StartTime;
        var startTimeMin = value.StartTimeMin;

        //var edYear = value.EdYear;
        var edMonth = value.EdMonth;
        var edDay = value.EdDay;
        var edHour = value.EdHour;
        var edMinute = value.EdMin;
        var endTime = value.EndTime;
        var endTimeMin = value.EndTimeMin;

        var cronExpression = Empty;

        if (start)
        {
            if (value.DayOfWeekId == DayOfWeek.Invalid)
            {
                if (value is { StartTime: 0, EndTime: 0, StMonth: 0, StDay: 0, StHour: 0 })
                {
                    cronExpression = "0 0 0 ? * *";  // *"; // verified
                }
                if (value is { EndTime: > 0, StMonth: 0, StDay: 0, StHour: 0 })
                {
                    cronExpression = $"0 {startTimeMin} {startTime} ? * *"; // *"; // not verified
                }
                if (value is { EndTime: > 0, StMonth: > 0, StDay: > 0 })
                {
                    cronExpression = $"0 {startTimeMin} {startTime} {stDay} {stMonth} ?"; // *"; // not verified
                }
                if (value is { StartTime: 0, EndTime: 0, StMonth: 0, StDay: 0 })
                {
                    cronExpression = $"0 {stMinute} {stHour} ? * *"; // *"; // verified
                }
                if (value is { StartTime: 0, EndTime: 0, StMonth: > 0, StDay: > 0 })
                {
                    cronExpression = $"0 {stMinute} {stHour} {stDay} {stMonth} ?"; // *"; // verified
                }
                //cronExpression = $"0 {stMinute} {stHour} {stDay} {stMonth} ?"; // *";
            }
            else
            {
                if (value is { StartTime: 0, EndTime: 0, StMonth: 0, StDay: 0, StHour: 0 })
                {
                    cronExpression = $"0 0 0 ? * {dayOfWeek}"; // *"; // not verified
                }
                if (value is { EndTime: > 0, StMonth: 0, StDay: 0, StHour: 0 })
                {
                    cronExpression = $"0 {startTimeMin} {startTime} ? * {dayOfWeek}"; // *"; // verified
                }
                if (value is { EndTime: > 0, StMonth: > 0, StDay: > 0 })
                {
                    cronExpression = $"0 {startTimeMin} {startTime} {stDay} {stMonth} {dayOfWeek}"; // *"; // not verified
                }
                if (value is { StartTime: 0, EndTime: 0, StMonth: 0, StDay: 0 })
                {
                    cronExpression = $"0 {stMinute} {stHour} ? * {dayOfWeek}"; // *"; // not verified
                }
                if (value is { StartTime: 0, EndTime: 0, StMonth: > 0, StDay: > 0 })
                {
                    cronExpression = $"0 {stMinute} {stHour} {edDay} {edMonth} {dayOfWeek}"; // *"; // not verified
                }
                //cronExpression = $"0 {stMinute} {stHour} {stDay} {stMonth} {dayOfWeek}";
            }
        }
        else
        {
            if (value.DayOfWeekId == DayOfWeek.Invalid)
            {
                if (value is { StartTime: 0, EndTime: 0, EdMonth: 0, EdDay: 0, EdHour: 0 })
                {
                    cronExpression = "0 0 0 ? * *"; // *"; // not verified
                }
                if (value is { EndTime: > 0, EdMonth: 0, EdDay: 0, EdHour: 0 })
                {
                    cronExpression = $"0 {endTimeMin} {endTime} ? * *"; // *"; // not verified
                }
                if (value is { EndTime: > 0, EdMonth: > 0, EdDay: > 0 })
                {
                    cronExpression = $"0 {endTimeMin} {endTime} {edDay} {edMonth} ?"; // *"; // not verified
                }
                if (value is { StartTime: 0, EndTime: 0, EdMonth: 0, EdDay: 0 })
                {
                    cronExpression = $"0 {edMinute} {edHour} ? * *"; // *"; // not verified
                }
                if (value is { StartTime: 0, EndTime: 0, EdMonth: > 0, EdDay: > 0 })
                {
                    cronExpression = $"0 {edMinute} {edHour} {edDay} {edMonth} ?"; // *"; // not verified
                }
            }
            else
            {
                if (value is { StartTime: 0, EndTime: 0, EdMonth: 0, EdDay: 0, EdHour: 0 })
                {
                    cronExpression = $"0 0 0 ? * {dayOfWeek}"; // *"; // not verified
                }
                if (value is { EndTime: > 0, EdMonth: 0, EdDay: 0, EdHour: 0 })
                {
                    cronExpression = $"0 {endTimeMin} {endTime} ? * {dayOfWeek}"; // *"; // not verified
                }
                if (value is { EndTime: > 0, EdMonth: > 0, EdDay: > 0 })
                {
                    cronExpression = $"0 {endTimeMin} {endTime} {edDay} {edMonth} ?"; // *"; // not verified
                }
                if (value is { StartTime: 0, EndTime: 0, EdMonth: 0, EdDay: 0 })
                {
                    cronExpression = $"0 {edMinute} {edHour} ? * {dayOfWeek}"; // *"; // not verified
                }
                if (value is { StartTime: 0, EndTime: 0, EdMonth: > 0, EdDay: > 0 })
                {
                    cronExpression = $"0 {edMinute} {edHour} {edDay} {edMonth} {dayOfWeek}"; // *"; // not verified
                }
            }
            //cronExpression = start ?
            //    $"0 {stMinute} {stHour} {stDay} {stMonth} {dayOfWeek}"
            //    :
            //    $"0 {edMinute} {edHour} {edDay} {edMonth} ?";
        }

        cronExpression = cronExpression.Replace("?", "*/1"); // Crontab doesn't support ?, so we replace it with */1 instead

        return cronExpression;
    }

    public List<uint> GetMatchingPeriods2()
    {
        var matchingPeriods = new List<uint>();
        var now = DateTime.Now;

        foreach (var period in _scheduleItems.Values)
        {
            var startDate = new DateTime((int)period.StYear, (int)period.StMonth, (int)period.StDay, (int)period.StHour, (int)period.StMin, 0);
            var endDate = new DateTime((int)period.EdYear, (int)period.EdMonth, (int)period.EdDay, (int)period.EdHour, (int)period.EdMin, 59);

            if (now >= startDate && now <= endDate)
            {
                matchingPeriods.Add(period.Id);
            }
        }

        return matchingPeriods;
    }

    public List<uint> GetMatchingPeriods()
    {
        var matchingPeriods = new List<uint>();
        var now = DateTime.UtcNow;

        foreach (var period in _scheduleItems.Values)
        {
            var startDate = new DateTime(
                period.StYear == 0 ? now.Year : (int)period.StYear,
                period.StMonth == 0 ? now.Month : (int)period.StMonth,
                period.StDay == 0 ? now.Day : (int)period.StDay,
                (int)period.StHour,
                (int)period.StMin,
                0
            );

            var endDate = new DateTime(
                period.EdYear == 0 ? now.Year : (int)period.EdYear,
                period.EdMonth == 0 ? now.Month : (int)period.EdMonth,
                period.EdDay == 0 ? now.Day : (int)period.EdDay,
                (int)period.EdHour,
                (int)period.EdMin,
                59
            );

            if (now >= startDate && now <= endDate && period.ActiveTake)
            {
                matchingPeriods.Add(period.Id);
            }
        }

        return matchingPeriods;
    }
}
