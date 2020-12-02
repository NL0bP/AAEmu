using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSEditorGameModePacket : GamePacket
    {
        public CSEditorGameModePacket() : base(CSOffsets.CSEditorGameModePacket, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var on = stream.ReadBoolean();

            var (x, y, z) = stream.ReadWorldPosition();
            //var x = Helpers.ConvertLongX(stream.ReadInt64());
            //var y = Helpers.ConvertLongY(stream.ReadInt64());
            //var z = stream.ReadSingle();

            var ori = stream.ReadQuaternionWithScalarSingle();

            _log.Debug("EditorGameMode, On: {0}", on);
        }
    }
}
