using AAEmu.Game.Models.Game.AI_old.Abstracts;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.World;

/*
   Author:Sagara, NLObP
*/
namespace AAEmu.Game.Models.Game.AI_old
{
    public sealed class TransferAi : ACreatureAi
    {
        public TransferAi(GameObject owner, float visibleRange) : base(owner, visibleRange)
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
            //        var chr = (Character)someone;
            //        var transfer = (TransfersPath)Owner;
            //        transfer.RemoveVisibleObject(chr);
            //        break;
            //}
        }

        protected override void SomeoneSeeMe(GameObject someone)
        {
            //switch (someone.UnitType)
            //{
            //    case BaseUnitType.Character:
            //        var chr = (Character)someone;
            //        var transfer = (TransfersPath)Owner;
            //        transfer.AddVisibleObject(chr);
            //        break;
            //}
        }

        protected override void SomeoneUnseeMee(GameObject someone)
        {
            //switch (someone.UnitType)
            //{
            //    case BaseUnitType.Character:
            //        var chr = (Character)someone;
            //        var transfer = (TransfersPath)Owner;
            //        transfer.RemoveVisibleObject(chr);
            //        break;
            //}
        }

        protected override void SomeoneThatIamSeeWasMoved(GameObject someone, MovementAction action)
        {
            //switch (someone.UnitType)
            //{
            //    case BaseUnitType.Character:
            //        var chr = (Character)someone;
            //        var transfer = (TransfersPath)Owner;
            //        transfer.AddVisibleObject(chr);
            //        break;
            //}
        }

        protected override void SomeoneThatSeeMeWasMoved(GameObject someone, MovementAction action)
        {
            //switch (someone.UnitType)
            //{
            //    case BaseUnitType.Character:
            //        var chr = (Character)someone;
            //        var transfer = (TransfersPath)Owner;
            //        //TransferManager.Instance.Spawn(chr, transfer);
            //        transfer.AddVisibleObject(chr);
            //        break;
            //}
        }
    }
}
