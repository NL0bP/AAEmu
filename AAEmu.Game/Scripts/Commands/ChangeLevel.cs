using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Scripts.Commands
{
    public class ChangeLevel : ICommand
    {
        public void OnLoad()
        {
            CommandManager.Instance.Register("change_level", this);
        }

        public string GetCommandLineHelp()
        {
            return "<level>";
        }

        public string GetCommandHelpText()
        {
            return "Adds experience points needed to reach target <level> (allowed range is 1-100)\n" +
                "Do note that going above the intended max level might break skills.";
        }

        public void Execute(Character character, string[] args)
        {
            if (args.Length == 0)
            {
                character.SendMessage("[ChangeLevel] /change_level <level>");
                //character.SendMessage("[ChangeLevel] level: 1-100");
                return;
            }

            var level = byte.Parse(args[0]);
            if (level <= 0 && level > 100)
            {
                character.SendMessage("[ChangeLevel] level: 1-100");
                return;
            }

            var exp = ExperienceManager.Instance.GetExpForLevel(level);
            if (exp <= 0)
                return;
            var diffExp = exp - character.Experience;
            if (diffExp > 0)
                character.AddExp(diffExp, true);
            else
            {
                character.Level = level;
                character.Experience = exp;

                character.Hp = character.MaxHp;
                character.Mp = character.MaxMp;

                character.SendPacket(new SCLevelChangedPacket(character.ObjId, level));
            }
        }
    }
}
