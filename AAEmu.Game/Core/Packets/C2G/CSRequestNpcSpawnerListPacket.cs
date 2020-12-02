using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Core.Packets.Proxy;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSRequestNpcSpawnerListPacket : GamePacket
    {
        public CSRequestNpcSpawnerListPacket() : base(CSOffsets.CSRequestNpcSpawnerListPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            // Empty struct

            _log.Debug("CSRequestNpcSpawnerListPacket.");
        }
    }
}
