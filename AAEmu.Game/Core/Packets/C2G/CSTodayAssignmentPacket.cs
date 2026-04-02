using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.TodayAssignments;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSTodayAssignmentPacket : GamePacket
{
    public CSTodayAssignmentPacket() : base(CSOffsets.CSTodayAssignmentPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        var realStep = stream.ReadInt32(); // realStep
        var request = (TodayAssignmentData)stream.ReadByte();  // request
        Logger.Debug($"CSTodayAssignmentPacket, realStep: {realStep}, request: {request}");

        var character = Connection.ActiveChar;
        if (character?.TodayAssignments == null)
        {
            return;
        }

        character.TodayAssignments.HandleRequest(realStep, request);
    }
}
