using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSRequestPlantingPacket : GamePacket
    {
        public CSRequestPlantingPacket() : base(CSOffsets.CSRequestPlantingPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
            Logger.Debug("Entering in CSRequestPlantingPacket...");

            var objId = stream.ReadBc();
            var idx = stream.ReadInt32();

            var caster = Connection.ActiveChar;
            var doodad = WorldManager.Instance.GetDoodad(objId);

            var phaseFuncs = DoodadManager.Instance.GetPhaseFunc(doodad.FuncGroupId);
            if (phaseFuncs.Count == 0)
                return; // No phase functions for FuncGroupId

            doodad.DoPhaseFunc(caster, phaseFuncs[idx]);

            Logger.Debug($"CSRequestPlantingPacket, caster Name {caster.Name}, doodad objId: {objId}, idx: {idx}");
        }
    }
}
