using System.Collections.Generic;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Json;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSSetPingPosPacket : GamePacket
    {
        public CSSetPingPosPacket() : base(CSOffsets.CSSetPingPosPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {

            //List <Point> position = new List<Point>();
            //List<Point> position2 = new List<Point>();

            //var teamId = stream.ReadUInt32(); // teamId
            //var hasPing = stream.ReadBoolean();   // setPingType
            //var flag = stream.ReadByte();     // flag, added in 3.0.3.0

            ////stream.ReadUInt16();
            ////stream.ReadUInt16();

            //for (var i = 0; i < 6; i++) // added in 3.0.3.0
            //{
            //    var (x, y, z) = stream.ReadPositionBc(); // posXYZ
            //    position[i] = new Point(x, y, z);
            //}

            //var pisc1 = stream.ReadPisc(4); // added in 3.0.3.0
            //var pisc2 = stream.ReadPisc(2); // added in 3.0.3.0

            //var lineCount = stream.ReadByte();   // lineCount, added in 3.0.3.0
            //for (var i = 0; i < lineCount; i++)
            //{
            //    var (x, y, z) = stream.ReadPositionBc(); // posXYZ
            //    position2[i] = new Point(x, y, z);
            //}

            //var insId = stream.ReadUInt32(); // remove in 3.0.3.0

            // _log.Warn("SetPingPos, teamId {0}, hasPing {1}, insId {2}", teamId, hasPing, insId);
            //var owner = Connection.ActiveChar;
            //owner.LocalPingPosition = position[(int)teamId];

            var owner = Connection.ActiveChar;
            var teamId = stream.ReadUInt32(); // teamId
            owner.LocalPingPosition.Read(stream);
            if (teamId > 0)
            {
                TeamManager.Instance.SetPingPos(owner, teamId, owner.LocalPingPosition);
            }
            else
            {
                owner.SendPacket(new SCTeamPingPosPacket(owner.LocalPingPosition));
            }
        }
    }
}
