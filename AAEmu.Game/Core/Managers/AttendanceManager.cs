using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.World;

using NLog;

namespace AAEmu.Game.Core.Managers
{
    public class AttendanceManager : Singleton<AttendanceManager>
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        public void Add(uint characterId)
        {
            var character = WorldManager.Instance.GetCharacterById(characterId);
            if (character == null)
            {
                Logger.Warn($"AttendanceManager: Character with ID {characterId} not found.");
                return;
            }

            character.Attendances?.Add(character);
        }

        public void Send(uint characterId)
        {
            var character = WorldManager.Instance.GetCharacterById(characterId);
            if (character == null)
            {
                Logger.Warn($"AttendanceManager: Character with ID {characterId} not found.");
                return;
            }

            character.Attendances?.Send();
        }

        public void SendEmpty(uint characterId)
        {
            var character = WorldManager.Instance.GetCharacterById(characterId);
            if (character == null)
            {
                Logger.Warn($"AttendanceManager: Character with ID {characterId} not found.");
                return;
            }

            character.Attendances?.SendEmptyAttendances();
        }
    }
}
