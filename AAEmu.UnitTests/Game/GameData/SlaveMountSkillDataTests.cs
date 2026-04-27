using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AAEmu.UnitTests.Game.GameData;

public class SlaveMountSkillDataTests
{
    private const uint FarmHaulerFuelSkillId = 20291;
    private const uint FarmHaulerFuelBuffId = 4841;
    private const uint RedFarmHaulerFuelSkillId = 27032;
    private const uint RedFarmHaulerFuelBuffId = 8335;
    private const uint RedFarmHaulerAdditiveSkillId = 35166;
    private const uint RedFarmHaulerAdditiveBuffId = 20584;
    private const uint RowingDeviceSkillId = 27446;
    private const uint RowingDeviceBuffId = 11388;

    [Theory]
    [InlineData(FarmHaulerFuelSkillId, FarmHaulerFuelBuffId, 3)]
    [InlineData(RedFarmHaulerFuelSkillId, RedFarmHaulerFuelBuffId, 3)]
    [InlineData(RedFarmHaulerAdditiveSkillId, RedFarmHaulerAdditiveBuffId, 3)]
    [InlineData(RowingDeviceSkillId, RowingDeviceBuffId, 1)]
    public void TransportMovementSkill_HasExpectedBuffEffect(uint skillId, uint buffId, int applicationMethodId)
    {
        using var connection = OpenCompactDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COUNT(*)
                              FROM skill_effects se
                              JOIN effects e ON e.id = se.effect_id
                              JOIN buff_effects be ON be.id = e.actual_id
                              JOIN buffs b ON b.id = be.buff_id
                              WHERE se.skill_id = $skillId
                                AND e.actual_type = 'BuffEffect'
                                AND be.buff_id = $buffId
                                AND b.slave_applicable = 1
                                AND se.application_method_id = $applicationMethodId
                                AND se.chance = 100
                                AND be.chance = 100;
                              """;
        command.Parameters.AddWithValue("$skillId", skillId);
        command.Parameters.AddWithValue("$buffId", buffId);
        command.Parameters.AddWithValue("$applicationMethodId", applicationMethodId);

        var count = Convert.ToInt32(command.ExecuteScalar());

        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(FarmHaulerFuelSkillId)]
    [InlineData(RedFarmHaulerFuelSkillId)]
    [InlineData(RedFarmHaulerAdditiveSkillId)]
    [InlineData(RowingDeviceSkillId)]
    public void TransportMovementSkill_IsMountedSkillForAtLeastOneSlave(uint skillId)
    {
        using var connection = OpenCompactDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COUNT(DISTINCT sms.slave_id)
                              FROM mount_skills ms
                              JOIN slave_mount_skills sms ON sms.mount_skill_id = ms.id
                              WHERE ms.skill_id = $skillId;
                              """;
        command.Parameters.AddWithValue("$skillId", skillId);

        var count = Convert.ToInt32(command.ExecuteScalar());

        Assert.True(count > 0);
    }

    [Fact]
    public void HaulerFuelMountSkills_AllHaveBuffEffects()
    {
        using var connection = OpenCompactDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT DISTINCT sk.id
                              FROM mount_skills ms
                              JOIN slave_mount_skills sms ON sms.mount_skill_id = ms.id
                              JOIN skills sk ON sk.id = ms.skill_id
                              WHERE sk.desc LIKE '%달구지에 친환경 연료%'
                                AND NOT EXISTS (
                                    SELECT 1
                                    FROM skill_effects se
                                    JOIN effects e ON e.id = se.effect_id
                                    WHERE se.skill_id = sk.id
                                      AND e.actual_type = 'BuffEffect'
                                )
                              ORDER BY sk.id;
                              """;

        var missingSkillIds = new List<uint>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            missingSkillIds.Add(Convert.ToUInt32(reader.GetInt64(0)));

        Assert.Empty(missingSkillIds);
    }

    private static SqliteConnection OpenCompactDatabase()
    {
        var dataSource = ResolveCompactDatabasePath();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static string ResolveCompactDatabasePath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var repoPath = Path.Combine(current.FullName, "AAEmu.Game", "Data", "compact.sqlite3");
            if (File.Exists(repoPath))
                return repoPath;

            current = current.Parent;
        }

        var outputPath = Path.Combine(AppContext.BaseDirectory, "Data", "compact.sqlite3");
        if (File.Exists(outputPath))
            return outputPath;

        throw new FileNotFoundException("Could not locate Data/compact.sqlite3 for skill data tests.", outputPath);
    }
}
