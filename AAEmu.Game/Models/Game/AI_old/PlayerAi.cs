using AAEmu.Game.Models.Game.AI_old.Abstracts;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.World;

/*
   Author:Sagara, NLObP
*/
namespace AAEmu.Game.Models.Game.AI_old
{
    public sealed class PlayerAi : ACreatureAi
    {
        public PlayerAi(GameObject owner, float visibleRange) : base(owner, visibleRange)
        {
        }

        protected override void IamSeeSomeone(GameObject someone)
        {
        }

        protected override void IamUnseeSomeone(GameObject someone)
        {
            //switch (someone.UnitType)
            //{
            //    case BaseUnitType.Character:
            //        break;
            //    case BaseUnitType.Npc:
            //        var npc = (Npc)someone;
            //        var character = (Character)Owner;
            //        if (!npc.IsInBattle && npc.Hp > 0 && npc.Patrol != null)
            //        {
            //            // NPC stand still
            //            //npc.Patrol = null;
            //            npc.Patrol?.Stop(npc);
            //            npc.RemoveVisibleObject(character);
            //        }
            //        break;
            //    case BaseUnitType.Slave:
            //        break;
            //    case BaseUnitType.Housing:
            //        break;
            //    case BaseUnitType.Transfer:
            //        break;
            //    case BaseUnitType.Mate:
            //        break;
            //    case BaseUnitType.Shipyard:
            //        break;
            //    default:
            //        throw new ArgumentOutOfRangeException();
            //}
        }

        protected override void SomeoneSeeMe(GameObject someone)
        {
        }

        protected override void SomeoneUnseeMee(GameObject someone)
        {
        }

        protected override void SomeoneThatIamSeeWasMoved(GameObject someone, MovementAction action)
        {
        }

        protected override void SomeoneThatSeeMeWasMoved(GameObject someone, MovementAction action)
        {
        }
    }
}
