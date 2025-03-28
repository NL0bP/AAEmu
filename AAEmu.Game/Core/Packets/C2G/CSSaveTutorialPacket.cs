using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSSaveTutorialPacket() : GamePacket(CSOffsets.CSSaveTutorialPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        Logger.Debug("Entering in SaveTutorial...");
        var id = stream.ReadUInt32();

        Logger.Debug($"SaveTutorial, Id: {id}");

        //var completedQuestBlock = Connection.ActiveChar.Quests.SetCompletedQuestFlag(id, true);
        //var body = new byte[8];
        //completedQuestBlock.Body.CopyTo(body, 0);

        //Connection.SendPacket(new SCTutorialSavedPacket(id, body));
        Connection.SendPacket(new SCGlobalGameStatusAckPacket());
    }
}
