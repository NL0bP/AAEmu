using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;

using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items.Actions;

using NLog;

namespace AAEmu.Game.Models.Game.Attendance
{
    public class CharacterAttendance
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        public List<AttendanceRecord> Records { get; private set; }
        public ulong AccountId { get; private set; }
        private Character Owner { get; set; }

        private const int MaxRewards = 28;
        private const int MaxDaysInMonth = 31;

        public CharacterAttendance(Character owner)
        {
            Owner = owner;
            AccountId = owner.AccountId;
            Load(MySQL.CreateConnection());
            if (Records == null || Records.Count == 0)
            {
                InitializeEmptyRecords();
            }
        }

        private void InitializeEmptyRecords()
        {
            Records = [];
            for (var i = 0; i < MaxDaysInMonth; i++)
            {
                Records.Add(new AttendanceRecord());
            }
        }

        public void RegisterDailyAttendance()
        {
            var now = DateTime.UtcNow;
            var dayIndex = now.Day - 1;

            if (!IsValidDayIndex(dayIndex) || AlreadyReceivedToday(dayIndex))
            {
                Logger.Info($"Attendance: {Owner.Name}:{Owner.Id} already picked up the gift on day {now.Day}.");
                return;
            }

            var attendedCount = GetAttendedDaysCount();
            if (attendedCount >= MaxRewards)
            {
                Logger.Info($"Attendance limit reached for {Owner.Name}:{Owner.Id}.");
                return;
            }

            Records[dayIndex].MarkAsAttended(now);

            var newCount = attendedCount + 1;

            var (itemId, itemCount) = AttendanceGameData.Instance.GetReward(now.Year, now.Month, newCount);
            if (itemId > 0 && itemCount > 0)
            {
                Owner.Inventory.Bag.AcquireDefaultItem(ItemTaskType.TakeScheduleItem, itemId, itemCount);
            }

            var (extraItemId, extraItemCount) = AttendanceGameData.Instance.GetAdditionalReward(now.Year, now.Month, newCount);
            if (extraItemId > 0 && extraItemCount > 0)
            {
                Owner.Inventory.Bag.AcquireDefaultItem(ItemTaskType.TakeScheduleItem, extraItemId, extraItemCount);
            }

            Owner.SendPacket(new SCDbAttendanceTimePacket(true, now));
            Logger.Info($"Attendance: {Owner.Name}:{Owner.Id} received reward #{newCount} on day {now.Day}.");
        }

        public void Add(Character character)
        {
            if (character.AccountId != AccountId)
                return;

            ResetIfNewMonth();
            RegisterDailyAttendance();
        }

        public void Send()
        {
            //var daysInMonth = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
            var recordsToSend = new List<AttendanceRecord>();

            for (var i = 0; i < MaxDaysInMonth; i++)
            {
                if (i < Records.Count)
                    recordsToSend.Add(Records[i]);
                else
                    recordsToSend.Add(new AttendanceRecord());
            }

            Owner.SendPacket(new SCAccountAttendancePacket(recordsToSend));
        }

        public void SendEmptyAttendances()
        {
            //var daysInMonth = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
            var emptyRecords = new List<AttendanceRecord>();
            for (var i = 0; i < MaxDaysInMonth; i++)
            {
                emptyRecords.Add(new AttendanceRecord());
            }

            Owner.SendPacket(new SCAccountAttendancePacket(emptyRecords));
        }

        private bool IsValidDayIndex(int index)
        {
            return index is >= 0 and < MaxDaysInMonth;
        }

        private bool AlreadyReceivedToday(int dayIndex)
        {
            return Records[dayIndex].Accept;
        }

        private int GetAttendedDaysCount()
        {
            return Records.Count(r => r.Accept);
        }

        public void ResetIfNewMonth()
        {
            try
            {
                if (Records == null)
                {
                    InitializeEmptyRecords();
                    Logger.Info($"Attendance records initialized for account {AccountId} (records was null).");
                    return;
                }

                // Проверяем, есть ли вообще НЕпустые записи (с датой > 0001-01-01)
                var hasAnyNonEmptyRecords = Records.Any(x => x.AccountAttendance > DateTime.MinValue);

                // Если все записи "пустые" (0001-01-01), то инициализировать не нужно
                if (!hasAnyNonEmptyRecords)
                {
                    Logger.Info($"No reset needed for account {AccountId} (all records are empty).");
                    return;
                }

                // Получаем последнюю подтвержденную запись (с Accept = true) с датой > MinValue
                var lastValidAttendance = Records
                    .Where(x => x.Accept && x.AccountAttendance > DateTime.MinValue)
                    .OrderByDescending(x => x.AccountAttendance)
                    .FirstOrDefault();

                var now = DateTime.UtcNow;

                // Если нет ни одной валидной записи или последняя запись не в текущем месяце/году
                if (lastValidAttendance == null ||
                    lastValidAttendance.AccountAttendance.Month != now.Month ||
                    lastValidAttendance.AccountAttendance.Year != now.Year)
                {
                    // Проверяем, есть ли сегодняшняя запись
                    var hasTodayRecord = Records.Any(x =>
                        x.AccountAttendance.Date == now.Date &&
                        x.Accept);

                    if (!hasTodayRecord)
                    {
                        InitializeEmptyRecords();
                        Logger.Info($"Attendance reset for account {AccountId} (new month/year detected). " +
                                 $"Last valid record: {lastValidAttendance?.AccountAttendance.ToString("yyyy-MM-dd") ?? "none"}, " +
                                 $"Current time: {now:yyyy-MM-dd}");
                    }
                    else
                    {
                        Logger.Info($"Attendance not reset for account {AccountId} (today's record exists).");
                    }
                }
                else
                {
                    Logger.Debug($"No need to reset attendance for account {AccountId} " +
                               $"(last record is from current month: {lastValidAttendance.AccountAttendance:yyyy-MM-dd})");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error resetting attendance for account {AccountId}: {ex.Message}", ex);
                throw;
            }
        }

        public void Load(MySqlConnection connection)
        {
            Records = [];

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM attendances WHERE `owner` = @owner";
            command.Parameters.AddWithValue("@owner", AccountId);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var record = new AttendanceRecord
                {
                    AccountAttendance = reader.GetDateTime("account_attendance"),
                    Accept = reader.GetBoolean("accept")
                };
                Records.Add(record);
            }

            while (Records.Count < MaxDaysInMonth)
            {
                Records.Add(new AttendanceRecord());
            }
        }

        public void Save(MySqlConnection connection, MySqlTransaction transaction)
        {
            //var daysInMonth = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);

            for (var i = 0; i < MaxDaysInMonth; i++)
            {
                var record = Records[i];

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "REPLACE INTO attendances(`id`, `owner`, `account_attendance`, `accept`) VALUES (@id, @owner, @account_attendance, @accept)";
                command.Parameters.AddWithValue("@id", i);
                command.Parameters.AddWithValue("@owner", AccountId);
                command.Parameters.AddWithValue("@account_attendance", record.AccountAttendance);
                command.Parameters.AddWithValue("@accept", record.Accept);

                command.ExecuteNonQuery();
            }
        }
    }
}
