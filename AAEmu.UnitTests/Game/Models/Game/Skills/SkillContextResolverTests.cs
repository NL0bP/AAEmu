using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Resolvers;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.slaves;
using AAEmu.UnitTests.Utils.Mocks;

using Xunit;

namespace AAEmu.UnitTests.Game.Models.Game.Skills;

public class SkillContextResolverTests
{
    [Fact]
    public void SourceMountSkillUsesAttachedUnitAsCasterAndTarget()
    {
        var player = CreateMountedPlayer(100, out var slave);
        var template = CreateTemplate(sourceMount: true, SkillTargetType.Self, SkillTargetSelection.Source);
        SkillCaster caster = new SkillCasterMount(999);
        SkillCastTarget target = new SkillCastUnitTarget(777);

        var effectiveCaster = SkillContextResolver.AuthoritativeResolve(player, template, ref caster, ref target);

        Assert.Same(slave, effectiveCaster);
        Assert.Equal(slave.ObjId, caster.ObjId);
        Assert.Equal(slave.ObjId, target.ObjId);
    }

    [Fact]
    public void NormalSelfSkillWhileMountedKeepsPlayerAsCasterAndTarget()
    {
        var player = CreateMountedPlayer(100, out _);
        var template = CreateTemplate(sourceMount: false, SkillTargetType.Self, SkillTargetSelection.Source);
        SkillCaster caster = new SkillCasterUnit(player.ObjId);
        SkillCastTarget target = new SkillCastUnitTarget(777);

        var effectiveCaster = SkillContextResolver.AuthoritativeResolve(player, template, ref caster, ref target);

        Assert.Same(player, effectiveCaster);
        Assert.Equal(player.ObjId, caster.ObjId);
        Assert.Equal(player.ObjId, target.ObjId);
    }

    [Fact]
    public void NormalSelfSkillWhileMountedIgnoresClientSuppliedMountCaster()
    {
        var player = CreateMountedPlayer(100, out var slave);
        var template = CreateTemplate(sourceMount: false, SkillTargetType.Self, SkillTargetSelection.Source);
        SkillCaster caster = new SkillCasterUnit(slave.ObjId);
        SkillCastTarget target = new SkillCastUnitTarget(slave.ObjId);

        var effectiveCaster = SkillContextResolver.AuthoritativeResolve(player, template, ref caster, ref target);

        Assert.Same(player, effectiveCaster);
        Assert.Equal(player.ObjId, caster.ObjId);
        Assert.Equal(player.ObjId, target.ObjId);
    }

    [Fact]
    public void MountedSkillIgnoresClientSuppliedForeignCaster()
    {
        var player = CreateMountedPlayer(100, out var slave);
        var template = CreateTemplate(sourceMount: true, SkillTargetType.Self, SkillTargetSelection.Source);
        SkillCaster caster = new SkillCasterMount(555);
        SkillCastTarget target = new SkillCastUnitTarget(555);

        SkillContextResolver.AuthoritativeResolve(player, template, ref caster, ref target);

        Assert.Equal(slave.ObjId, caster.ObjId);
        Assert.Equal(slave.ObjId, target.ObjId);
    }

    [Fact]
    public void ParentTargetUsesRootAttachedUnit()
    {
        var player = new CharacterMock { ObjId = 100, AttachedPoint = AttachPointKind.Driver };
        var root = new Slave { ObjId = 200, OwnerObjId = player.ObjId, Summoner = player };
        var child = new Slave { ObjId = 201, OwnerObjId = player.ObjId, Summoner = player };
        child.Transform.Parent = root.Transform;
        player.Transform.Parent = child.Transform;

        var template = CreateTemplate(sourceMount: true, SkillTargetType.Parent, SkillTargetSelection.Target);
        SkillCaster caster = new SkillCasterMount(child.ObjId);
        SkillCastTarget target = new SkillCastUnitTarget(player.ObjId);

        var effectiveCaster = SkillContextResolver.AuthoritativeResolve(player, template, ref caster, ref target);

        Assert.Same(child, effectiveCaster);
        Assert.Equal(child.ObjId, caster.ObjId);
        Assert.Equal(root.ObjId, target.ObjId);
    }

    [Fact]
    public void RiderSourceSkillUsesPlayerAsTarget()
    {
        var player = CreateMountedPlayer(100, out var slave);
        var template = CreateTemplate(sourceMount: false, SkillTargetType.Self, SkillTargetSelection.Source);
        SkillCastTarget originalTarget = new SkillCastUnitTarget(slave.ObjId);

        var riderTarget = SkillContextResolver.BuildRiderTarget(template, player, originalTarget);

        Assert.Equal(player.ObjId, riderTarget.ObjId);
    }

    [Fact]
    public void ControlledChecksAcceptAttachedSlaveAndRejectUnrelatedMate()
    {
        var player = CreateMountedPlayer(100, out var slave);
        var unrelatedMate = new Mate { ObjId = 300, OwnerObjId = 999 };

        Assert.True(SkillContextResolver.IsControlledBy(player, slave));
        Assert.False(SkillContextResolver.IsControlledBy(player, unrelatedMate));
    }

    private static CharacterMock CreateMountedPlayer(uint playerObjId, out Slave slave)
    {
        var player = new CharacterMock { ObjId = playerObjId, AttachedPoint = AttachPointKind.Driver };
        slave = new Slave { ObjId = 200, OwnerObjId = player.ObjId, Summoner = player };
        slave.AttachedCharacters[AttachPointKind.Driver] = player;
        player.Transform.Parent = slave.Transform;
        return player;
    }

    private static SkillTemplate CreateTemplate(bool sourceMount, SkillTargetType targetType, SkillTargetSelection targetSelection)
    {
        return new SkillTemplate
        {
            SourceMount = sourceMount,
            TargetType = targetType,
            TargetSelection = targetSelection
        };
    }
}
