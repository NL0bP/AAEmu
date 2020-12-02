using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSItemUccPacket : GamePacket
    {
        public CSItemUccPacket() : base(CSOffsets.CSItemUccPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var templateId = stream.ReadUInt32(); // type templateId
            var num = stream.ReadInt32();          // num
            for (var i = 0; i < num; i++)
            {
                var itemId = stream.ReadInt64();  // itemId
            }

            _log.Warn("CSItemUccPacket, Id: {0}, Num: {1}", templateId, num);
        }
    }
}
