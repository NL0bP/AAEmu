using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSRequestUIDataPacket : GamePacket
    {
        public CSRequestUIDataPacket() : base(CSOffsets.CSRequestUIDataPacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var uiDataKey = stream.ReadString();
            var id = stream.ReadUInt32();
            var name = stream.ReadString();

            if (Connection.Characters.ContainsKey(id))
            {
                Connection.SendPacket(new SCResponseUIDataPacket(id, name, uiDataKey, Connection.Characters[id].GetOption(uiDataKey)));
            }
        }
    }
}
