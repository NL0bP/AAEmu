using System;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Buffs;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using Moq;

namespace AAEmu.UnitTests.Game.Models.Game.Skills.Helpers;

/// <summary>
/// Builder для создания тестовых баффов с предопределенными значениями
/// Использует паттерн Builder для гибкости конфигурации
/// </summary>
public class BuffTestBuilder
{
    private uint _buffId = 1;
    private int _duration = 5000; // 5 секунд по умолчанию
    private double _tick = 0;
    private BuffTemplate _template;
    private Unit _caster;
    private Unit _owner;
    private int _maxStacks = 1;
    private BuffStackRule _stackRule = BuffStackRule.Refresh;

    public BuffTestBuilder WithBuffId(uint id)
    {
        _buffId = id;
        return this;
    }

    public BuffTestBuilder WithDuration(int durationMs)
    {
        if (durationMs < 0)
            throw new ArgumentException("Duration cannot be negative", nameof(durationMs));
        _duration = durationMs;
        return this;
    }

    public BuffTestBuilder WithTick(double tick)
    {
        if (tick < 0)
            throw new ArgumentException("Tick cannot be negative", nameof(tick));
        _tick = tick;
        return this;
    }

    public BuffTestBuilder WithTemplate(BuffTemplate template)
    {
        _template = template ?? throw new ArgumentNullException(nameof(template));
        return this;
    }

    public BuffTestBuilder WithCaster(Unit caster)
    {
        _caster = caster ?? throw new ArgumentNullException(nameof(caster));
        return this;
    }

    public BuffTestBuilder WithOwner(Unit owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        return this;
    }

    public BuffTestBuilder AsStackable(int maxStacks = 5)
    {
        if (maxStacks < 1)
            throw new ArgumentException("MaxStacks must be at least 1", nameof(maxStacks));
        _maxStacks = maxStacks;
        return this;
    }

    public BuffTestBuilder WithStackRule(BuffStackRule stackRule)
    {
        _stackRule = stackRule;
        return this;
    }

    public AAEmu.Game.Models.Game.Skills.Buff Build()
    {
        var owner = _owner ?? CreateDefaultUnit("Owner");
        var caster = _caster ?? CreateDefaultUnit("Caster");
        var template = _template ?? CreateDefaultTemplate();
        var skillCaster = new Mock<SkillCaster>().Object;

        // Создаем бафф с требуемыми параметрами конструктора
        var buff = new AAEmu.Game.Models.Game.Skills.Buff(
            owner,
            caster as IBaseUnit,
            skillCaster,
            template,
            null,  // Skill - может быть null для тестов
            DateTime.UtcNow);

        buff.Duration = _duration;
        buff.BaseDuration = _duration;
        buff.Index = _buffId;
        buff.InUse = true;  // Активируем бафф по умолчанию

        return buff;
    }

    private BuffTemplate CreateDefaultTemplate()
    {
        return new BuffTemplate
        {
            Id = _buffId,
            Duration = _duration,
            StackRule = _stackRule
        };
    }

    private Unit CreateDefaultUnit(string name)
    {
        // Создаем простой mock без setup для non-overridable properties
        var mockUnit = new Mock<Unit>(MockBehavior.Loose);
        return mockUnit.Object;
    }
}
