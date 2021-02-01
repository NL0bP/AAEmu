using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCOneUnitMovementPacket : GamePacket // TODO ... SCMoveTypesPacket
    {
        private readonly uint _id;
        private readonly MoveType _type;

        public SCOneUnitMovementPacket(uint id, MoveType type) : base(SCOffsets.SCOneUnitMovementPacket, 5)
        {
            _id = id;
            _type = type;

            // ---- test Ai_old ----
            var unit = WorldManager.Instance.GetUnit(id);
            if (!(unit is Npc) && !(unit is Transfer) && !(unit is Gimmick)) { return; }

            var movementAction = new MovementAction(
                new Point(type.X, type.Y, type.Z, Helpers.ConvertRadianToSbyteDirection(type.Rot.X), Helpers.ConvertRadianToSbyteDirection(type.Rot.Y), Helpers.ConvertRadianToSbyteDirection(type.Rot.Z)),
                new Point(0, 0, 0),
                (sbyte)type.Rot.Z,
                3,
                MoveTypeEnum.Unit
            );
            unit.VisibleAi.OwnerMoved(movementAction);
            // ---- test Ai_old ----
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.WriteBc(_id);
            stream.Write((byte)_type.Type);
            stream.Write(_type);
            return stream;
        }
    }
}
