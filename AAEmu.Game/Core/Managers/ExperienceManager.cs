using System.Collections.Generic;

using AAEmu.Commons.Utils;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Utils.DB;

using NLog;

namespace AAEmu.Game.Core.Managers;

public class ExperienceManager : Singleton<ExperienceManager>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private Dictionary<byte, ExperienceLevelTemplate> _levels;
    private Dictionary<byte, ExperienceHeirLevelTemplate> _heirLevels;

    // TODO: Put this in the configuration files
    public static byte MaxPlayerLevel => 55;
    public static byte MaxPlayerHeirLevel => 34;
    public static byte MaxMateLevel => 50;

    #region Level
    public int GetExpForLevel(byte level, bool mate = false)
    {
        var levelTemplate = _levels.GetValueOrDefault(level);
        return mate ? levelTemplate?.TotalMateExp ?? 0 : levelTemplate?.TotalExp ?? 0;
    }

    public byte GetLevelFromExp(int exp, bool mate = false)
    {
        // Loop the levels to find the level for a given exp value
        foreach (var (level, levelTemplate) in _levels)
        {
            if (exp < (mate ? levelTemplate.TotalMateExp : levelTemplate.TotalExp))
                return (byte)(level-1);
        }
        return 0;
    }

    public int GetExpNeededToGivenLevel(int currentExp, byte targetLevel, bool mate = false)
    {
        var targetExp = GetExpForLevel(targetLevel, mate);
        var diff = targetExp - currentExp;
        return (diff <= 0) ? 0 : diff;
    }

    public int GetSkillPointsForLevel(byte level)
    {
        return _levels.GetValueOrDefault(level)?.SkillPoints ?? 0;
    }

    public int GetReqItemCountForLevel(byte level)
    {
        return _levels.GetValueOrDefault(level)?.ReqItemCount ?? 0;
    }

    public int GetReqItemIdForLevel(byte level)
    {
        return _levels.GetValueOrDefault(level)?.ReqItemId ?? 0;
    }
    #endregion Level

    #region HeirLevel
    public uint GetExpForHeirLevel(byte level)
    {
        var levelTemplate = _heirLevels.GetValueOrDefault(level);
        return levelTemplate?.ReqTotalExp ?? 0;
    }

    public byte GetHeirLevelFromExp(uint exp)
    {
        // Loop the levels to find the level for a given exp value
        foreach (var (level, levelTemplate) in _heirLevels)
        {
            if (exp < levelTemplate.ReqTotalExp)
                return (byte)(level);
        }
        return 0;
    }
    
    public uint GetExpNeededToGivenHeirLevel(uint currentExp, byte targetLevel)
    {
        var targetExp = GetExpForHeirLevel(targetLevel);
        var diff = targetExp - currentExp;
        return diff <= 0 ? 0 : diff;
    }

    public int GetReqItemCountForHeirLevel(byte level)
    {
        return _heirLevels.GetValueOrDefault(level)?.ReqItemCount ?? 0;
    }

    public int GetReqItemIdForHeirLevel(byte level)
    {
        return _heirLevels.GetValueOrDefault(level)?.ReqItemId ?? 0;
    }

    public int GetStepForHeirLevel(byte level)
    {
        return _heirLevels.GetValueOrDefault(level)?.Step ?? 0;
    }
    #endregion HeirLevel

    public void Load()
    {
        _levels = new Dictionary<byte, ExperienceLevelTemplate>();
        _heirLevels = new Dictionary<byte, ExperienceHeirLevelTemplate>();
        using var connection = SQLite.CreateConnection();
        Logger.Info("Loading experience data...");
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM levels";
        command.Prepare();
        using (var sqliteDataReader = command.ExecuteReader())
        using (var reader = new SQLiteWrapperReader(sqliteDataReader))
        {
            while (reader.Read())
            {
                var level = new ExperienceLevelTemplate();
                level.Level = reader.GetByte("id");
                level.ExpeditionExp = reader.GetInt32("expedition_exp");
                level.ReqItemCount = reader.GetInt32("req_item_count");
                level.ReqItemId = reader.GetInt32("req_item_id");
                level.SkillPoints = reader.GetInt32("skill_points");
                level.TotalExp = reader.GetInt32("total_exp");
                level.TotalMateExp = reader.GetInt32("total_mate_exp");

                _levels.Add(level.Level, level);
            }
        }

        Logger.Info("Experience data loaded");

        command.CommandText = "SELECT * FROM heir_levels";
        command.Prepare();
        using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
        {
            while (reader.Read())
            {
                var heirLevel = new ExperienceHeirLevelTemplate();
                heirLevel.Level = reader.GetByte("level");
                heirLevel.ReqItemCount = reader.GetInt32("req_item_count");
                heirLevel.ReqItemId = reader.GetInt32("req_item_id");
                heirLevel.ReqTotalExp = reader.GetUInt32("req_total_exp");
                heirLevel.Step = reader.GetInt32("step");

                _heirLevels.TryAdd(heirLevel.Level, heirLevel);
            }
        }

        Logger.Info("Experience data loaded");
    }
}
