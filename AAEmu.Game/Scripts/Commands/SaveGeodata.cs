using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class SaveGeodata : ICommand
{
    public string[] CommandNames { get; set; } = ["save_geodata", "sg"];
    protected string Title { get; set; }

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "[true || false || change]";
    }

    public string GetCommandHelpText()
    {
        return "Performs geodata write/read to the npc_spawn.json file when the parameter is equal to change";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var isSaving = false;
        if (args.Length > 0)
        {
            if (args[0].ToLower() == "change")
            {
                // Летящие персонажи по команде /fly не отслеживаются
                var isFlying = Fly.GetCacheState(character.Id); // We cache the playerId, not the ObjectId
                // Позволяем команде "/save change" сохранять высоту персонажа, как высоту выбранного в цель Npc
                if (!isFlying)
                {
                    Character.TrackCharacterCoordinates(character);
                }

                return;
            }

            if (!bool.TryParse(args[0], out isSaving))
            {
                CommandManager.SendErrorText(this, messageOutput, $"<save_geodata mode> bool parse error!");
                return;
            }
        }

        // Команда "/save true" включает режим сохранения геоданных
        character.SetSaveGeoDataMode(isSaving);
        character.SendDebugMessage($"Set save_geodata mode: |cFFFFFFFF{isSaving}|r.");
    }
}
