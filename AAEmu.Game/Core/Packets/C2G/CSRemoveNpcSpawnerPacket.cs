using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSRemoveNpcSpawnerPacket : GamePacket
    {
        public CSRemoveNpcSpawnerPacket() : base(CSOffsets.CSRemoveNpcSpawnerPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var spawnerId = stream.ReadUInt32();
        }
    }
}
