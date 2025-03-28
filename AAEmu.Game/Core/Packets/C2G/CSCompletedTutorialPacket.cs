using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Items.Actions;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSCompletedTutorialPacket() : GamePacket(CSOffsets.CSCompletedTutorialPacket, 5)
{
    public override void Read(PacketStream stream)
    {
        var id = stream.ReadUInt32();

        Logger.Debug($"SaveTutorial, Id: {id}");

        Connection.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.QuestComplete, [], [], 3));
        Connection.SendPacket(new SCTutorialCompletedPacket(id));
    }
}
