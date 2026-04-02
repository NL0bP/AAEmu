using System;
using System.Linq;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

public class TeleportToUnit : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.TeleportToUnit;

    public override void Execute(BaseUnit caster,
        SkillCaster casterObj,
        BaseUnit target,
        SkillCastTarget targetObj,
        CastAction castObj,
        Skill skill,
        SkillObject skillObject,
        DateTime time,
        int value1,
        int value2,
        int value3,
        int value4, int value5, int value6, int value7)
    {
        if (caster is Character)
        {
            Logger.Debug("Special effects: TeleportToUnit value1 {0}, value2 {1}, value3 {2}, value4 {3}", value1, value2, value3, value4);
        }

        if (target == null)
        {
            return;
        }

        // value1/value2 define distance in millimeters from target.
        // value3/value4 define rotation relative to target orientation in degrees.
        var distance = Rand.Next(value1, value2);
        var worldDistance = distance / 1000f;
        var rotationDegrees = Rand.Next(value3, value4);

        var targetPosition = target.Transform.World.Position;
        var targetYawDegrees = target.Transform.World.ToRollPitchYawDegrees().Z;
        var (endX, endY) = MathUtil.AddDistanceToFrontDeg(
            worldDistance,
            targetPosition.X,
            targetPosition.Y,
            targetYawDegrees + 90f + rotationDegrees);

        Logger.Debug(
            "TeleportToUnit: caster={0}:{1}, target={2}:{3}, targetPos=({4:0.###},{5:0.###},{6:0.###}), targetYaw={7:0.###}, distance={8}, rotation={9}, endPos=({10:0.###},{11:0.###},{12:0.###})",
            caster.Name, caster.ObjId,
            target.Name, target.ObjId,
            targetPosition.X, targetPosition.Y, targetPosition.Z,
            targetYawDegrees,
            worldDistance, rotationDegrees,
            endX, endY, targetPosition.Z);

        switch (caster)
        {
            case Character character:
                if (character.IsRiding)
                {
                    var mates = MateManager.Instance.GetActiveMates(character.ObjId);
                    if (mates != null)
                    {
                        foreach (var mate in mates.Where(mate => mate is { MateType: MateType.Ride }))
                        {
                            MateManager.Instance.UnMountMate(character.Connection, mate.TlId, AttachPointKind.Driver, AttachUnitReason.None);
                        }
                    }
                }

                character.SetPosition(endX, endY, targetPosition.Z,
                    character.Transform.World.Rotation.X,
                    character.Transform.World.Rotation.Y,
                    character.Transform.World.Rotation.Z);
                character.SendPacket(new SCUnitBlinkPacket(caster.ObjId, 0f, 0f, endX, endY, targetPosition.Z));
                break;
            case Npc npc:
                npc.SetPosition(endX, endY, targetPosition.Z,
                    npc.Transform.World.Rotation.X,
                    npc.Transform.World.Rotation.Y,
                    npc.Transform.World.Rotation.Z);
                break;
        }
    }
}
