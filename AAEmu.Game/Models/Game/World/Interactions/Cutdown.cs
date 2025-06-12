using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.World.Interactions;


public class Cutdown : IWorldInteraction
{
    public void Execute(BaseUnit caster, SkillCaster casterType, BaseUnit target, SkillCastTarget targetType,
        uint skillId, uint doodadId, DoodadFuncTemplate objectFunc = null)
    {
        if (target is Doodad doodad)
        {
            doodad.Use(caster, skillId);
            if (doodad.TemplateId is not (7420 or 8312 or 8046 or 8047 or 8048 or 8049 or 8050 or 8273 or 8274 or 8275)) // ID=7420 Cornucopia Tree, ID=8312 Majestic Tree, ID=8046..8050, 8273..8275 Woodlots
            {
                caster.BroadcastPacket(new SCVegetationCutdowningPacket(caster.ObjId, doodad.ObjId), true);
            }
        }
    }
}
