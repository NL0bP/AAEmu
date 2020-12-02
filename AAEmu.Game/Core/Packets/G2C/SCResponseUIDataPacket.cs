using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCResponseUIDataPacket : GamePacket
    {
        private readonly uint _characterId;
        private readonly string _name;
        private readonly string _uiDataKey;
        private readonly string _uiData;

        public SCResponseUIDataPacket(uint characterId, string name, string uiDataKey, string uiData) : base(SCOffsets.SCResponseUIDataPacket, 1)
        {
            _characterId = characterId;
            _name= name;
            _uiDataKey = uiDataKey;
            _uiData = uiData;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_characterId);       // type owner
            stream.Write(_name);              // name
            stream.Write(_uiDataKey);         // uiDataKey
            stream.Write(_uiData);            // uiData
            stream.Write(_uiData.Length + 1); // size

            return stream;
        }
    }
}
