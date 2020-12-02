using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSChangeDoodadPhasePacket : GamePacket
    {
        public CSChangeDoodadPhasePacket() : base(CSOffsets.CSChangeDoodadPhasePacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var objId = stream.ReadBc();
            var FuncGroupId = stream.ReadUInt32();
            var TimeLeft = stream.ReadUInt32();
            var puzzleGroup = stream.ReadUInt32();
            
            _log.Warn("CSChangeDoodadPhase, ObjId: {0}, Id: {1}, {2}, {3}", objId, FuncGroupId, TimeLeft, puzzleGroup);
        }
    }
}
