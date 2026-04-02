using System;
using System.Linq;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

public class MoveToGround : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.MoveToGround;

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
            Logger.Debug("Special effects: MoveToGround value1 {0}, value2 {1}, value3 {2}, value4 {3}", value1, value2, value3, value4);
        }

        if (target?.Transform == null)
            return;

        var destination = target.Transform.World.Position;

        switch (caster)
        {
            case Character character:
            {
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

                character.SendPacket(new SCUnitBlinkPacket(character.ObjId, 0f, 0f, destination.X, destination.Y, destination.Z));
                break;
            }
            case Npc npc:
                npc.SetPosition(destination.X, destination.Y, destination.Z,
                    npc.Transform.World.Rotation.X, npc.Transform.World.Rotation.Y, npc.Transform.World.Rotation.Z);
                break;
        }
    }
}
