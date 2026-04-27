using System;
using System.Collections.Generic;
using System.Reflection;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Buffs;
using AAEmu.Game.Models.Game.Skills.Buffs.Triggers;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using Xunit;

namespace AAEmu.UnitTests.Game.Models.Game.Units;

public class BuffsStackingTests
{
    [Fact]
    public void AddBuff_ExtendStackRule_AddsNewDurationToRemainingTime()
    {
        EnsureSkillManagerInitialized();

        var owner = new TestBaseUnit();
        var template = new BuffTemplate
        {
            Id = 0,
            Duration = 100_000,
            MaxStack = 1,
            StackRule = BuffStackRule.Extend
        };

        owner.Buffs.AddBuff(CreateBuff(owner, template));
        var firstBuff = owner.Buffs.GetEffectByIndex(1);
        Assert.NotNull(firstBuff);

        owner.Buffs.AddBuff(CreateBuff(owner, template));

        var extendedBuff = owner.Buffs.GetEffectByIndex(1);
        Assert.Same(firstBuff, extendedBuff);
        Assert.InRange(extendedBuff.Duration, 190_000, 200_000);
        Assert.Equal(100_000, extendedBuff.BaseDuration); // BaseDuration остаётся базовой
        Assert.Equal(2, owner.BroadcastCount);
    }

    private static void EnsureSkillManagerInitialized()
    {
        var buffTagsField = typeof(SkillManager).GetField("_buffTags", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(buffTagsField);
        buffTagsField.SetValue(SkillManager.Instance, new Dictionary<uint, List<uint>> { [0] = new List<uint>() });

        var buffTriggersField = typeof(SkillManager).GetField("_buffTriggers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(buffTriggersField);
        buffTriggersField.SetValue(SkillManager.Instance, new Dictionary<uint, List<BuffTriggerTemplate>>());
    }

    private static AAEmu.Game.Models.Game.Skills.Buff CreateBuff(BaseUnit owner, BuffTemplate template)
    {
        return new AAEmu.Game.Models.Game.Skills.Buff(
            owner,
            owner,
            new SkillCasterUnit(owner.ObjId),
            template,
            null,
            DateTime.UtcNow);
    }

    private sealed class TestBaseUnit : BaseUnit
    {
        public int BroadcastCount { get; private set; }

        public override void BroadcastPacket(GamePacket packet, bool self)
        {
            BroadcastCount++;
        }
    }
}
