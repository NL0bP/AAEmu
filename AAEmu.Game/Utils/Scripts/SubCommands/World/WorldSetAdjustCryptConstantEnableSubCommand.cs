using System.Collections.Generic;
using System.Drawing;

using AAEmu.Commons.Cryptography;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Utils.Scripts.SubCommands.World;

public class WorldSetAdjustCryptConstantEnableSubCommand : SubCommandBase
{
    public WorldSetAdjustCryptConstantEnableSubCommand()
    {
        Title = "[World Set AdjustCryptConstantEnable]";
        Description = "Setting the AdjustCryptConstantEnable";
        CallPrefix = $"{CommandManager.CommandPrefix}adjustcrypt";
        AddParameter(new StringSubCommandParameter("AdjustCryptConstantEnable", "AdjustCryptConstantEnable", true));
    }
    public override void Execute(ICharacter character, string triggerArgument, IDictionary<string, ParameterValue> parameters, IMessageOutput messageOutput)
    {
        string adjustCryptConstantEnable = parameters["AdjustCryptConstantEnable"];
        if (adjustCryptConstantEnable is "")
        {
            SendColorMessage(messageOutput, Color.Coral, $"AdjustCryptConstantEnable must be an 'true' or 'false'");
            return;
        }

        EncryptionManager.AdjustCryptConstantEnable = adjustCryptConstantEnable == "true";

        SendMessage(messageOutput, $"Set AdjustCryptConstantEnable: {adjustCryptConstantEnable}");
        Logger.Warn($"{Title}: {adjustCryptConstantEnable}");
    }
}
