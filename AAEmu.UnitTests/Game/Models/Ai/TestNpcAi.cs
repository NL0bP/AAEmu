using System.Collections.Generic;
using System.Diagnostics;

using AAEmu.Game.Models.Game.AI.Enums;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.AI.v2.Params;
using AAEmu.Game.Models.Game.NPChar;

using Xunit;

namespace AAEmu.UnitTests.Game.Models.Ai
{
    // Минимальная тестовая реализация NpcAi для тестирования EnqueueAiCommands.
    public class TestNpcAi : NpcAi
    {
        protected override void Build()
        {
            // Для тестирования достаточно пустой реализации.
        }
    }

    // Минимальная реализация AiCommands для тестирования.
    public class TestAiCommands : AiCommands
    {
        // Можно добавить конструктор или инициализацию, если потребуется.
    }

    public class TestNpc : Npc
    {
    }
    public class NpcAiTests
    {
        [Fact]
        public void EnqueueAiCommands_PerformanceTest()
        {
            // Подготовка тестового экземпляра AI.
            var npcAi = new TestNpcAi { Owner = new TestNpc() };
            
            // Создание набора команд для теста.
            var commandsList = new List<AiCommands>();
            const int commandsCount = 10000; // Можно изменить для нагрузки.
            for (var i = 0; i < commandsCount; i++)
            {
                commandsList.Add(new TestAiCommands
                {
                    Id = (uint)i,
                    CmdSetId = 1,
                    CmdId = (AiCommandCategory)1, // Пример значения
                    Param1 = (uint)i,
                    Param2 = "Test"
                });
            }

            // Очистка очереди для точного замера.
            npcAi.AiCommandsQueue.Clear();

            // Измеряем время выполнения EnqueueAiCommands.
            var sw = Stopwatch.StartNew();
            npcAi.EnqueueAiCommands(commandsList, addOnly: false);
            sw.Stop();
            
            // Выводим время в миллисекундах.
            var elapsedMilliseconds = sw.ElapsedMilliseconds;
            // Можно добавить проверку, что время выполнения не превышает ожидаемый порог.
            // Для примера вывод результата через Assert.
            Assert.True(elapsedMilliseconds < 100, $"EnqueueAiCommands занял слишком много времени: {elapsedMilliseconds} мс");

            // Дополнительно можно проверить, что все команды были добавлены.
            Assert.Equal(commandsCount, npcAi.AiCommandsQueue.Count);
        }
    }
}
