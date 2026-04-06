using System.Collections.Generic;

using AAEmu.Game.Core.Packets.G2C;

using MySql.Data.MySqlClient;

using NLog;

namespace AAEmu.Game.Models.Game.Char;

/// <summary>
/// Tracks skills unlocked via ChangeSkillActiveType (emotes, dances, etc. learned from items).
/// Persists per character.
/// activeType: 1=all, 2=female, 3=male — NOT related to AbilityType enum (that's character classes).
/// </summary>
public class CharacterSkillActiveTypes(Character owner)
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // key=skillId, value=activeType (1/2/3)
    private readonly Dictionary<uint, byte> _activeTypes = new();

    public void Load(MySqlConnection connection)
    {
        _activeTypes.Clear();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT `skill_id`, `active_type` FROM `skill_active_types` WHERE `owner` = @owner";
        command.Parameters.AddWithValue("@owner", owner.Id);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            _activeTypes[reader.GetUInt32("skill_id")] = reader.GetByte("active_type");
    }

    public void Save(MySqlConnection connection, MySqlTransaction transaction)
    {
        foreach (var pair in _activeTypes)
        {
            using var command = connection.CreateCommand();
            command.Connection = connection;
            command.Transaction = transaction;
            command.CommandText = "REPLACE INTO `skill_active_types` (`owner`, `skill_id`, `active_type`) VALUES (@owner, @skill_id, @active_type)";
            command.Parameters.AddWithValue("@owner", owner.Id);
            command.Parameters.AddWithValue("@skill_id", pair.Key);
            command.Parameters.AddWithValue("@active_type", pair.Value);
            command.ExecuteNonQuery();
        }
    }

    public void Unlock(uint skillId, byte activeType)
    {
        _activeTypes[skillId] = activeType;
        SendUpdate(skillId, activeType);
    }

    public void SendOnLogin()
    {
        foreach (var pair in _activeTypes)
            SendUpdate(pair.Key, pair.Value);
    }

    private void SendUpdate(uint skillId, byte activeType)
    {
        owner.SendPacket(new SCUpdateSkillActiveTypePacket(0, skillId, activeType));
        Logger.Debug("SkillActiveType update sent for {0}: skill {1} -> {2}", owner.Name, skillId, activeType);
    }
}
