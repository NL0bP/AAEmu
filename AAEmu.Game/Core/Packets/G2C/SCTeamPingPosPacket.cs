using System.Collections.Generic;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Team;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCTeamPingPosPacket : GamePacket
    {
        //private readonly bool _hasPing;
        //private readonly byte _flag;
        //private readonly List<Point> _position;
        //private readonly List<Point> _position2;
        //private readonly long[] _pisc1;
        //private readonly long[] _pisc2;
        //private readonly byte _lineCount;
        private readonly PingPosition _pingPosition;

        public SCTeamPingPosPacket(PingPosition pingPosition) : base(SCOffsets.SCTeamPingPosPacket, 5)
        {
            _pingPosition = pingPosition;

            //_hasPing = hasPing;
            //_flag = flag;
            //_position = position;
            //_position2 = position2;
            //_pisc1 = pisc1;
            //_pisc2 = pisc2;
            //_lineCount = lineCount;
        }

        public override PacketStream Write(PacketStream stream)
        {
            _pingPosition.Write(stream);
            return stream;

            //stream.Write(_hasPing);
            //stream.Write(_flag);
            //foreach (var pos in _position) // max 6
            //{
            //    stream.WritePositionBc(pos.X, pos.Y, pos.Z);
            //}

            //stream.WritePisc(_pisc1[0], _pisc1[1], _pisc1[2], _pisc1[3]);
            //stream.WritePisc(_pisc2[0], _pisc2[1]);

            //stream.Write(_lineCount);
            //if (_position2 != null)
            //{
            //    foreach (var pos in _position2) // max 6
            //    {
            //        stream.WritePositionBc(pos.X, pos.Y, pos.Z);
            //    }
            //}
            //return stream;
        }
    }
}
