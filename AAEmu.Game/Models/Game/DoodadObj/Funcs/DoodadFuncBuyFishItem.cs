using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncBuyFishItem : DoodadPhaseFuncTemplate
{
    // doodad_phase_funcs
    public uint DoodadFuncBuyFishId { get; set; }
    public uint ItemId { get; set; }
    public override bool Use(BaseUnit caster, Doodad owner, ref Doodad.PhaseRollContext ctx)
    {
        Logger.Trace("DoodadFuncBuyFishItem");
        return false;
    }
}
