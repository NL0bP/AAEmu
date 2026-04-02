using System;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.StaticValues;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.Skills.SkillControllers;

public class WanderingSkillController : SkillController
{
    private readonly int _tickMs;
    private DateTime _nextMoveAt;

    public WanderingSkillController(SkillControllerTemplate template, BaseUnit owner, BaseUnit target)
    {
        Template = template;
        Owner = owner as Unit;
        Target = target as Unit;
        _tickMs = Math.Max(template.Value[0], 300);
    }

    public override void Execute()
    {
        base.Execute();
        if (Owner == null)
        {
            End();
            return;
        }

        _nextMoveAt = DateTime.UtcNow;
        TickManager.Instance.OnTick.Subscribe(Tick, TimeSpan.FromMilliseconds(100));
    }

    public override void End()
    {
        if (State == SCState.Ended)
            return;

        TickManager.Instance.OnTick.UnSubscribe(Tick);
        base.End();
    }

    private void Tick(TimeSpan delta)
    {
        if (Owner == null || Owner.IsDead)
        {
            End();
            return;
        }

        if (DateTime.UtcNow < _nextMoveAt)
            return;

        _nextMoveAt = DateTime.UtcNow.AddMilliseconds(_tickMs);

        var angle = (float)(Rand.NextDouble() * Math.PI * 2);
        var distance = 1.5f + (float)Rand.NextDouble();
        var (newX, newY) = MathUtil.AddDistanceToFront(distance, Owner.Transform.Local.Position.X, Owner.Transform.Local.Position.Y, angle);
        var newZ = WorldManager.Instance.GetHeight(Owner.Transform.ZoneId, newX, newY);
        if (newZ == 0)
            newZ = Owner.Transform.Local.Position.Z;

        var oldPosition = Owner.Transform.Local.ClonePosition();
        Owner.Transform.Local.SetPosition(newX, newY, newZ);
        Owner.Transform.Local.SetRotationDegree(0f, 0f, angle.RadToDeg() - 90f);
        var (rx, ry, rz) = Owner.Transform.Local.ToRollPitchYawSBytesMovement();

        var moveType = (UnitMoveType)MoveType.GetType(MoveTypeEnum.Unit);
        moveType.X = newX;
        moveType.Y = newY;
        moveType.Z = newZ;
        moveType.RotationX = rx;
        moveType.RotationY = ry;
        moveType.RotationZ = rz;
        moveType.ActorFlags = 4;
        moveType.Flags = MoveTypeFlags.Moving;
        moveType.DeltaMovement = [0, 127, 0];
        moveType.Stance = 0;
        moveType.Alertness = MoveTypeAlertness.Combat;
        moveType.Time = (uint)(DateTime.UtcNow - DateTime.UtcNow.Date).TotalMilliseconds;

        Owner.CheckMovedPosition(oldPosition);
        Owner.BroadcastPacket(new SCOneUnitMovementPacket(Owner.ObjId, moveType), false);
    }
}
