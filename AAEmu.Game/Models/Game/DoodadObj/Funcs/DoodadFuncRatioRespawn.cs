using System;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncRatioRespawn : DoodadPhaseFuncTemplate
{
    public int Ratio { get; set; }
    public uint SpawnDoodadId { get; set; }

    public override bool Use(BaseUnit caster, Doodad owner, ref Doodad.PhaseRollContext ctx)
    {
        Logger.Trace("DoodadFuncRatioRespawn : Ratio {0}, SpawnDoodadId {1}", Ratio, SpawnDoodadId);

        // защита от мусора
        int ratio = Math.Max(0, Ratio);

        ctx.Cumulative += ratio;

        if (ctx.Roll <= ctx.Cumulative && (owner.Spawner?.Id ?? 0) > 0)
        {
            owner.Spawner.RespawnDoodadTemplateId = SpawnDoodadId;
            return true; // Interrupt the PhaseFunc as new doodad is spawned
        }

        return false;
    }
}
