using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class SaveGeodate : ICommand
{
    public string[] CommandNames { get; set; } = ["save_geodate", "savegeodate", "sg", "save"];
    protected string Title { get; set; }

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "[true || false]";
    }

    public string GetCommandHelpText()
    {
        return "Enables or disables save_geodate mode";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var isSaving = false;
        if (args.Length > 0)
        {
            if (!bool.TryParse(args[0], out isSaving))
            {
                CommandManager.SendErrorText(this, messageOutput, $"<save_geodate mode> bool parse error!");
                return;
            }
        }
        character.SetSaveGeoDataMode(isSaving);
        character.SendDebugMessage($"Set save_geodate mode: |cFFFFFFFF{isSaving}|r.");
    }
}
