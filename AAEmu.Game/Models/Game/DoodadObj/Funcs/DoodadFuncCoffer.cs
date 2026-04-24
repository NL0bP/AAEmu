using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncCoffer : DoodadPhaseFuncTemplate
{
    // doodad_phase_funcs
    // Максимальная вместимость Coffer
    public int Capacity { get; set; }

    public override bool Use(BaseUnit caster, Doodad owner, ref Doodad.PhaseRollContext ctx)
    {
        Logger.Debug("Entering DoodadFuncCoffer");

        // Устанавливаем, что Coffer не переходит в следующую фазу
        owner.ToNextPhase = false;

        // Проверяем, является ли кастер персонажем и owner это Coffer
        if (caster is Character character && owner is DoodadCoffer coffer)
        {
            // Если Coffer уже открыт данным персонажем, закрываем его
            if (coffer.OpenedBy?.Id == character.Id)
            {
                DoodadManager.CloseCofferDoodad(character, owner.ObjId);
                Logger.Debug($"Coffer {owner.ObjId} closed by {character.Name}.");
            }
            else
            {
                // Иначе открываем Coffer
                DoodadManager.OpenCofferDoodad(character, owner.ObjId);
                Logger.Debug($"Coffer {owner.ObjId} opened by {character.Name}.");
            }
        }
        else
        {
            Logger.Warn("Invalid caster or owner type in DoodadFuncCoffer.");
        }

        return false; // Возвращаем false, если действие не было успешным
    }
}
