using System;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Skills.Plots;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Utils;
using NLog;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects
{
    public class Projectile : ISpecialEffect
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();
        private PlotObject _casterPlotObj;
        private PlotObject _targetPlotObj;
        
        public void Execute(Unit caster,
            SkillCaster casterObj,
            BaseUnit target,
            SkillCastTarget targetObj,
            CastAction castObj,
            Skill skill,
            SkillObject skillObject,
            DateTime time,
            int rotationZ, int value2, int distance, int value4)
        {
            // TODO ...
            _log.Warn("rotationZ {0}, value2 {1}, distance {2}, value4 {3}", rotationZ, value2, distance, value4);

            #region PlotEvent
            // TODO перечислять скиллы?
            switch (caster.SkillId)
            {
                case 11268:
                    // 11268   Stoning Throw a stone in a straight line.
                    UnitPos(caster, rotationZ, distance);
                    break;
                case 12856:
                    // 12856	Throw Red Fireworks	Shoot fireworks.
                case 12847:
                    // 12847	Throw Green Fireworks	Shoot fireworks.
                case 12863:
                    // 12863	Throw Blue Fireworks	Shoot fireworks.
                    PosPos(caster, rotationZ, distance);
                    break;
                default:
                    PosPos(caster, rotationZ, distance);
                    break;
            }

            if (!(casterObj is SkillItem itm))
                return;

            var character = (Character)caster;
            //var itemId = item.ItemId;
            //var item = character.Inventory.GetItem(itm.ItemId);
            character.Inventory.RemoveItem(itm.ItemTemplateId, 1, ItemTaskType.Destroy);

            var time2 = (ushort)(caster.Step.Flag != 0 ? caster.Step.Delay / 10 + 10 : 0);
            var objId = caster.Step.Casting || caster.Step.Channeling ? caster.ObjId : 0;
            caster.Step.Flag = 6;
            caster.BroadcastPacket(new SCPlotEventPacket(caster.TlId, caster.Step.Event.Id, caster.SkillId, _casterPlotObj, _targetPlotObj, objId, time2, caster.Step.Flag, itm.ItemId, 0), true);
            #endregion

        }

        public void UnitPos(Unit caster, int rotationZ, int distance)
        {
            // Бросок горизонтально
            _casterPlotObj = new PlotObject(caster);
            var endLine = caster.Position.Clone();
            var (newX2, newY2) = MathUtil.AddDistanceToFront(distance, endLine.X, endLine.Y, endLine.RotationZ); //TODO value3 throw distance L meters
            endLine.X = newX2;
            endLine.Y = newY2;
            endLine.Z = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(caster.Position.ZoneId, endLine.X, endLine.Y) : caster.Position.Z;
            //endLine.RotationZ = (sbyte)value1;
            _targetPlotObj = new PlotObject(endLine);
        }
        public void PosPos(Unit caster, int rotationZ, int distance)
        {
            // Бросок вверх
            //var startLine = caster.Position.Clone();  // in 5+
            //_casterPlotObj = new PlotObject(startLine);

            _casterPlotObj = new PlotObject(caster);    // in 3+

            var endLine = caster.Position.Clone();
            var (newX2, newY2) = MathUtil.AddDistanceToFront(distance, endLine.X, endLine.Y, endLine.RotationZ); //TODO value3 throw distance L meters
            endLine.X = newX2;
            endLine.Y = newY2;
            endLine.Z += distance;
            //endLine.RotationZ = (sbyte)rotationZ;
            _targetPlotObj = new PlotObject(endLine);
        }
    }
}
