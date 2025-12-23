using System;
using AAEmu.Commons.IO;
using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Managers;
using Microsoft.Extensions.Configuration;
using System.IO;
using AAEmu.Game.Models;
using Xunit;
using System.Threading.Tasks;
using AAEmu.Game.Utils.DB;
using System.Collections.Generic;

namespace AAEmu.IntegrationTests.Core.Manager;

public class QuestManagerTests
{
    private static bool _managersLoaded = false;

    private static void LoadManagers()
    {
        if (_managersLoaded)
            return;

        // Create in-memory configuration for testing
        var testConfig = new Dictionary<string, string>
        {
            {"Connections:MySQLProvider:Host", "localhost"},
            {"Connections:MySQLProvider:Port", "3306"},
            {"Connections:MySQLProvider:User", "root"},
            {"Connections:MySQLProvider:Password", ""},
            {"Connections:MySQLProvider:Database", "aaemu_test"},
            {"Connections:MySQLProvider:ConvertZeroDateTime", "true"},
            {"Connections:MySQLProvider:AllowZeroDateTime", "true"},
            {"Connections:MySQLProvider:ConnectionTimeout", "30"},
            {"Connections:MySQLProvider:DefaultCommandTimeout", "120"},
            {"Connections:MySQLProvider:UseAffectedRows", "true"},
            {"Connections:MySQLProvider:AutoEnlist", "false"}
        };

        var configurationBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(testConfig);
            
        configurationBuilder.AddUserSecrets<QuestManager>();
        var configurationBuilderResult = configurationBuilder.Build();
        
        // Initialize configuration
        var config = new AppConfiguration();
        configurationBuilderResult.Bind(config);
        
        // Set the instance
        var property = typeof(AppConfiguration).GetProperty("Instance", 
            System.Reflection.BindingFlags.Public | 
            System.Reflection.BindingFlags.Static | 
            System.Reflection.BindingFlags.SetProperty);
            
        property?.SetValue(null, config);

        MySQL.SetConfiguration(config.Connections.MySQLProvider);

        try
        {
            // Loads all quests from DB
            TaskIdManager.Instance.Initialize();
            TaskManager.Instance.Initialize();
            ZoneManager.Instance.Load();
            QuestManager.Instance.Load();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing managers: {ex.Message}");
            throw;
        }
        
        _managersLoaded = true;
    }

    public QuestManagerTests()
    {
        LoadManagers();
    }

    [Fact]
    public Task GetQuestIdFromStarterItem_ShouldReturnSameResultAsOriginal()
    {
        Dictionary<uint, uint> expectedResults = new();
        var query = @"
SELECT qc2.id as questId, qacai.item_id as itemId
FROM 
	quest_act_con_accept_items qacai
	INNER JOIN quest_acts qa 
		ON qacai.id = qa.act_detail_id and qa.act_detail_type = 'QuestActConAcceptItem'
	INNER JOIN quest_components qc 
		ON qc.id = qa.quest_component_id 
	INNER JOIN quest_contexts qc2 
		ON qc2.id = qc.quest_context_id ";

        using (var connection = SQLite.CreateConnection())
        {
            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.Prepare();

            using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
            {
                while (reader.Read())
                {
                    var questId = reader.GetUInt32("questId");
                    var itemId = reader.GetUInt32("itemId");

                    expectedResults.Add(itemId, questId);
                }
            }
        }

        foreach (var (itemId, questId) in expectedResults)
        {
            var resultOld = QuestManager.Instance.GetQuestIdFromStarterItem(itemId);
            Assert.Equal(questId, resultOld);

            var resultNew = QuestManager.Instance.GetQuestIdFromStarterItemNew(itemId);
            Assert.Equal(questId, resultNew);
        }

        return Task.CompletedTask;
    }
}
