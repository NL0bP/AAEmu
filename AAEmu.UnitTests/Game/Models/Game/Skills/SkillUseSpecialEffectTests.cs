using System.Reflection;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

using Xunit;

namespace AAEmu.UnitTests.Game.Models.Game.Skills;

public class SkillUseSpecialEffectTests
{
    [Fact]
    public void TriggeredSkillWithTargetOffsetCreatesPositionTargetInFrontOfCaster()
    {
        var caster = CreateUnit(100, 100f, 200f, 5f, 0f);
        var target = CreateUnit(200, 0f, 0f, 0f, 0f);
        var template = new SkillTemplate
        {
            TargetOffsetDistance = 13.5f,
            TargetOffsetAngle = 0f
        };

        var result = CreateTargetForTriggeredSkill(caster, target, template);

        var positionTarget = Assert.IsType<SkillCastPositionTarget>(result);
        Assert.Equal(SkillCastTargetType.Position, positionTarget.Type);
        Assert.Equal(0u, positionTarget.ObjId1);
        Assert.Equal(100f, positionTarget.PosX, 3);
        Assert.Equal(213.5f, positionTarget.PosY, 3);
        Assert.Equal(5f, positionTarget.PosZ, 3);
        Assert.Equal(0f, positionTarget.PosRot, 3);
    }

    [Fact]
    public void TriggeredSkillWithTargetOffsetAppliesTemplateOffsetAngle()
    {
        var caster = CreateUnit(100, 100f, 200f, 5f, 0f);
        var target = CreateUnit(200, 0f, 0f, 0f, 0f);
        var template = new SkillTemplate
        {
            TargetOffsetDistance = 13.5f,
            TargetOffsetAngle = 90f
        };

        var result = CreateTargetForTriggeredSkill(caster, target, template);

        var positionTarget = Assert.IsType<SkillCastPositionTarget>(result);
        Assert.Equal(86.5f, positionTarget.PosX, 3);
        Assert.Equal(200f, positionTarget.PosY, 3);
    }

    [Fact]
    public void TriggeredSkillWithoutTargetOffsetKeepsUnitTarget()
    {
        var caster = CreateUnit(100, 100f, 200f, 5f, 0f);
        var target = CreateUnit(200, 0f, 0f, 0f, 0f);
        var template = new SkillTemplate
        {
            TargetOffsetDistance = 0f
        };

        var result = CreateTargetForTriggeredSkill(caster, target, template);

        var unitTarget = Assert.IsType<SkillCastUnitTarget>(result);
        Assert.Equal(target.ObjId, unitTarget.ObjId);
    }

    [Fact]
    public void FiredPacketTargetKeepsPositionTarget()
    {
        var caster = CreateUnit(100, 100f, 200f, 5f, 0f);
        var target = new SkillCastPositionTarget
        {
            Type = SkillCastTargetType.Position,
            PosX = 100f,
            PosY = 213.5f,
            PosZ = 5f,
            PosRot = 0f
        };

        var result = CreateFiredTarget(caster, target);

        Assert.Same(target, result);
    }

    [Fact]
    public void FiredPacketTargetConvertsDoodadTargetToCasterUnitTarget()
    {
        var caster = CreateUnit(100, 100f, 200f, 5f, 0f);
        var target = new SkillCastDoodadTarget
        {
            Type = SkillCastTargetType.Doodad,
            ObjId = 200
        };

        var result = CreateFiredTarget(caster, target);

        var unitTarget = Assert.IsType<SkillCastUnitTarget>(result);
        Assert.Equal(caster.ObjId, unitTarget.ObjId);
    }

    [Fact]
    public void MountedTriggeredSkillFiredPacketUsesZeroDelayFields()
    {
        var caster = CreateUnit(100, 100f, 200f, 5f, 0f);
        var skill = new Skill(new SkillTemplate
        {
            Id = 15601,
            SourceMount = true,
            ChannelingTime = 0
        });
        var packet = new SCSkillFiredPacket(
            skill.Id,
            12,
            new SkillCasterUnit(caster.ObjId),
            new SkillCastPositionTarget
            {
                Type = SkillCastTargetType.Position,
                PosX = 100f,
                PosY = 213.5f,
                PosZ = 5f,
                PosRot = 0f
            },
            skill,
            new SkillObject(),
            caster);
        var stream = new PacketStream();

        packet.Write(stream);
        stream.Pos = 0;

        stream.ReadUInt16(); // TlId
        Assert.Equal((byte)SkillCasterType.Unit, stream.ReadByte());
        Assert.Equal(caster.ObjId, stream.ReadBc());
        stream.ReadByte();   // SkillCastTargetType.Position
        stream.ReadInt64();  // x
        stream.ReadInt64();  // y
        stream.ReadSingle(); // z
        stream.ReadSingle(); // rot
        stream.ReadBc();     // ObjId1
        stream.ReadBc();     // ObjId2
        stream.ReadBc();     // ObjId3
        stream.ReadByte();   // SkillObjectType.None

        Assert.Equal(0, stream.ReadInt16());
        Assert.Equal(0, stream.ReadInt16());
    }

    [Fact]
    public void MountedRepeatSkillFiredPacketSerializesRepeatFlag()
    {
        var caster = CreateUnit(100, 100f, 200f, 5f, 0f);
        var skill = new Skill(new SkillTemplate
        {
            Id = 27202,
            SourceMount = true,
            ChannelingTime = 0
        });
        var packet = new SCSkillFiredPacket(
            skill.Id,
            12,
            new SkillCasterUnit(caster.ObjId),
            new SkillCastUnitTarget(caster.ObjId),
            skill,
            new SkillObject(),
            caster)
        {
            FiredFlag = 2
        };
        var stream = new PacketStream();

        packet.Write(stream);
        stream.Pos = 0;

        stream.ReadUInt16(); // TlId
        stream.ReadByte();   // SkillCasterType.Unit
        stream.ReadBc();     // caster objId
        stream.ReadByte();   // SkillCastTargetType.Unit
        stream.ReadBc();     // target objId
        stream.ReadByte();   // SkillObjectType.None
        stream.ReadInt16();  // delay
        stream.ReadInt16();  // channeling
        stream.ReadByte();   // ExtraDataFlags.None
        stream.ReadPisc(2);  // skill id, fire anim

        Assert.Equal(2, stream.ReadByte());
    }

    private static SkillCastTarget CreateTargetForTriggeredSkill(BaseUnit caster, BaseUnit target, SkillTemplate template)
    {
        var method = typeof(SkillUse).GetMethod("CreateTargetForTriggeredSkill", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return Assert.IsAssignableFrom<SkillCastTarget>(method.Invoke(null, [caster, target, template]));
    }

    private static SkillCastTarget CreateFiredTarget(BaseUnit caster, SkillCastTarget target)
    {
        var method = typeof(Skill).GetMethod("CreateFiredTarget", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return Assert.IsAssignableFrom<SkillCastTarget>(method.Invoke(null, [caster, target]));
    }

    private static BaseUnit CreateUnit(uint objId, float x, float y, float z, float rotZ)
    {
        var unit = new BaseUnit
        {
            ObjId = objId
        };
        unit.Transform.Local.SetPosition(x, y, z, 0f, 0f, rotZ);
        return unit;
    }
}
