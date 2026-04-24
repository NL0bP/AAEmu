using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncCofferPerm : DoodadFuncTemplate
{
    /// <summary>
    /// Executes the use of the DoodadFuncCofferPerm.
    /// </summary>
    /// <param name="caster">The unit using the doodad.</param>
    /// <param name="owner">The doodad being used.</param>
    /// <param name="skillId">The ID of the skill being used.</param>
    /// <param name="nextPhase">The next phase of the action (default is 0).</param>
    public override void Use(BaseUnit caster, Doodad owner, uint skillId, int nextPhase = 0)
    {
        if (caster == null)
        {
            Logger.Error("Caster is null. Cannot use DoodadFuncCofferPerm.");
            return;
        }

        if (owner == null)
        {
            Logger.Error("Owner is null. Cannot use DoodadFuncCofferPerm.");
            return;
        }

        Logger.Debug($"DoodadFuncCofferPerm used by {caster.Name} on {owner.Name} with SkillId: {skillId}, NextPhase: {nextPhase}");

        // Здесь вы можете добавить логику для использования функциональности дудада
    }
}
