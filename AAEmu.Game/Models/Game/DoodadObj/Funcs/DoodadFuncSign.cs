using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncSign : DoodadPhaseFuncTemplate
{
    public string Name { get; set; }
    public int PickNum { get; set; }

    public override bool Use(BaseUnit caster, Doodad owner, ref Doodad.PhaseRollContext ctx)
    {
        Logger.Trace("DoodadFuncSign");
        return false;
    }
}
