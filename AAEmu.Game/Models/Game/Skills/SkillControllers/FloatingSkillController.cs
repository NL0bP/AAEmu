using System;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.StaticValues;

namespace AAEmu.Game.Models.Game.Skills.SkillControllers;

public class FloatingSkillController : SkillController
{
    private readonly float _liftHeight;
    private readonly int _durationMs;
    private float _startHeight;
    private float _targetHeight;
    private DateTime _endAt;

    public FloatingSkillController(SkillControllerTemplate template, BaseUnit owner, BaseUnit target)
    {
        Template = template;
        Owner = owner as Unit;
        Target = target as Unit;
        _durationMs = Math.Max(template.Value[0], 1000);
        // Floating controllers from data are tuned too low for visible launch in our movement model,
        // so keep the same source value but raise it conservatively.
        _liftHeight = Math.Max(template.Value[1] / 600f, 0.75f);
    }

    public override void Execute()
    {
        base.Execute();
        if (Owner == null)
        {
            End();
            return;
        }

        _startHeight = Owner.Transform.Local.Position.Z;
        _targetHeight = _startHeight + _liftHeight;
        _endAt = DateTime.UtcNow.AddMilliseconds(_durationMs);
        TickManager.Instance.OnTick.Subscribe(Tick, TimeSpan.FromMilliseconds(100));
    }

    public override void End()
    {
        if (State == SCState.Ended)
            return;

        if (Owner != null && !Owner.IsDead)
        {
            var groundHeight = WorldManager.Instance.GetHeight(Owner.Transform.ZoneId,
                Owner.Transform.Local.Position.X,
                Owner.Transform.Local.Position.Y);
            if (groundHeight != 0)
                SetHeightAndBroadcast(groundHeight);
            else
                SetHeightAndBroadcast(_startHeight);
        }

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

        if (DateTime.UtcNow >= _endAt)
        {
            End();
            return;
        }

        var currentZ = Owner.Transform.Local.Position.Z;
        if (Math.Abs(_targetHeight - currentZ) > 0.01f)
            SetHeightAndBroadcast(_targetHeight);
    }

    private void SetHeightAndBroadcast(float newZ)
    {
        var oldPosition = Owner.Transform.Local.ClonePosition();
        Owner.Transform.Local.SetHeight(newZ);

        var moveType = (UnitMoveType)MoveType.GetType(MoveTypeEnum.Unit);
        moveType.X = Owner.Transform.Local.Position.X;
        moveType.Y = Owner.Transform.Local.Position.Y;
        moveType.Z = Owner.Transform.Local.Position.Z;
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
