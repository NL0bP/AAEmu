using System;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;

using NLog;

namespace AAEmu.Game.Scripts.Commands
{
    internal class ReloadConfigs : ICommand
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public void OnLoad()
        {
            string[] name = { "reloadconfig", "reload_configs", "reload_configurations" };
            CommandManager.Instance.Register(name, this);
        }

        public string GetCommandLineHelp()
        {
            return "";
        }

        public string GetCommandHelpText()
        {
            return "Reloads the ConfigurationManager";
        }
        public void Execute(Character character, string[] args)
        {
            try
            {
                ConfigurationManager.Instance.Load();
                character.SendMessage("Configuration reloaded");
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
                character.SendMessage("Configurations failed reloading - check error output");
            }
        }
    }
}
