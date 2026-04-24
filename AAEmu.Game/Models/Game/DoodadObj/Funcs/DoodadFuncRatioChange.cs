using System;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncRatioChange : DoodadPhaseFuncTemplate
{
    // doodad_phase_funcs
    public int Ratio { get; set; }
    public int NextPhase { get; set; }

    public override bool Use(BaseUnit caster, Doodad owner, ref Doodad.PhaseRollContext ctx)
    {
        // защита от мусора
        int ratio = Math.Max(0, Ratio);

        ctx.Cumulative += ratio;

        if (ctx.Roll <= ctx.Cumulative)
        {
            if (caster is Character)
                Logger.Debug($"DoodadFuncRatioChange: Ratio={Ratio}, Cumulative={ctx.Cumulative}, Roll={ctx.Roll}, NextPhase={NextPhase}");
            else
                Logger.Trace($"DoodadFuncRatioChange: Ratio={Ratio}, Cumulative={ctx.Cumulative}, Roll={ctx.Roll}, NextPhase={NextPhase}");

            owner.OverridePhase = NextPhase;
            return true; // it is necessary to interrupt the phase functions and switch to NextPhase
        }

        return false; // let's continue with the phase functions
    }
}

