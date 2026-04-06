using AAEmu.Game.Core.Managers;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Utils.Scripts;
using System;
using AAEmu.Game.Models.Game.Skills.Templates;

namespace AAEmu.Game.Scripts.Commands;

public class Run : ICommand
{
    public string[] CommandNames { get; set; } = new string[] { "run" };

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "(target)";
    }

    public string GetCommandHelpText()
    {
        return "Toggles run buff (id=3105) on the target character. If the buff is active, it removes it; otherwise, it applies it.";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var targetPlayer = character;

        var firstArg = 0;
        if (args.Length > 0)
        {
            targetPlayer = WorldManager.GetTargetOrSelf(character, args[0], out firstArg);
        }

        uint buffId = 3105; // Бег buff id

        var buffTemplate = SkillManager.Instance.GetBuffTemplate(buffId);
        if (buffTemplate == null)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Unknown buffId {buffId}");
            return;
        }

        bool hasBuff = targetPlayer.Buffs.CheckBuff(buffId);

        if (hasBuff)
        {
            // Снимаем бафф
            targetPlayer.Buffs.RemoveBuff(buffId);
            targetPlayer.SendDebugMessage($"Run buff removed.");
        }
        else
        {
            // Накладываем бафф с увеличенным временем действия
            var casterObj = new SkillCasterUnit(character.ObjId);
            var targetObj = SkillCastTarget.GetByType(SkillCastTargetType.Unit);
            targetObj.ObjId = targetPlayer.ObjId;

            // Создаем копию шаблона с измененным временем
            var customBuffTemplate = new BuffTemplate();
            foreach (var prop in typeof(BuffTemplate).GetProperties())
            {
                if (prop.CanWrite)
                    prop.SetValue(customBuffTemplate, prop.GetValue(buffTemplate));
            }
            customBuffTemplate.Duration = 60000; // 1 минута

            var newBuff = new Buff(targetPlayer, character, casterObj, customBuffTemplate, null, DateTime.UtcNow);
            targetPlayer.Buffs.AddBuff(newBuff);
            targetPlayer.SendDebugMessage($"Run buff applied.");
        }
    }
}
