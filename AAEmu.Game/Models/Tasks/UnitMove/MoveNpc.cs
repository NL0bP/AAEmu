using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Transfers.Paths;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Route;

namespace AAEmu.Game.Models.Tasks.UnitMove
{
    public class MoveNpc : Task
    {
        private readonly SimulationNpc _patrol;
        private readonly Unit _unit;
        private readonly float _targetX;
        private readonly float _targetY;
        private readonly float _targetZ;
        private readonly NpcsPathPoint _pp;

        /// <summary>
        /// Move task
        /// </summary>
        /// <param name="patrol"></param>
        /// <param name="unit"></param>
        /// <param name="TargetX"></param>
        /// <param name="TargetY"></param>
        /// <param name="TargetZ"></param>
        /// <param name="pp"></param>
        public MoveNpc(SimulationNpc patrol, Unit unit, float TargetX, float TargetY, float TargetZ, NpcsPathPoint pp = null)
        {
            _patrol = patrol;
            _targetX = TargetX;
            _targetY = TargetY;
            _targetZ = TargetZ;
            _unit = unit;
            _pp = pp;
        }

        /// <summary>
        /// Perform tasks
        /// </summary>
        public override void Execute()
        {
            switch (_unit)
            {
                case Npc npc:
                    _patrol?.MoveToPathNpc(_patrol, npc, _targetX, _targetY, _targetZ, _pp);
                    break;
            }
        }
    }
}
