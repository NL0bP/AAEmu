using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSInteractGimmickPacket : GamePacket
    {
        public CSInteractGimmickPacket() : base(CSOffsets.CSInteractGimmickPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var bc = stream.ReadBc();

            _log.Debug("CSInteractGimmickPacket");
        }
    }
}
