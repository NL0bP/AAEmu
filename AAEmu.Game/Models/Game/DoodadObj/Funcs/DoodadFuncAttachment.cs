using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs
{
    public class DoodadFuncAttachment : DoodadFuncTemplate
    {
        public AttachPointKind AttachPointId { get; set; }
        public int Space { get; set; }
        public byte BondKindId { get; set; }
        public byte AnimActionId { get; set; }

        public override void Use(Unit caster, Doodad owner, uint skillId, int nextPhase = 0)
        {
            _log.Debug("DoodadFuncAttachment");
            if (caster is Character character)
            {
                if (BondKindId > 1)
                {
                    character.Bonding = new BondDoodad(owner, AttachPointId, BondKindId, Space, AnimActionId);
                    character.BroadcastPacket(new SCBondDoodadPacket(caster.ObjId, character.Bonding), true);
                }
                // Ships
                else
                {
                    SlaveManager.Instance.BindSlave(character, owner.ParentObjId, AttachPointId, AttachUnitReason.NewMaster);
                }
            }
        }
    }
}
