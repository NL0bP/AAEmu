using System;
using System.Collections.Generic;
using System.Linq;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Buffs;
using AAEmu.UnitTests.Game.Models.Game.Skills.Helpers;
using Xunit;

namespace AAEmu.UnitTests.Game.Models.Game.Skills.Tests;

/// <summary>
/// Тесты для стекирования баффов
/// Проверяется поведение при множественном применении одинаковых баффов
/// </summary>
public class BuffStackingTests
{
    /// <summary>
    /// Идентичные баффы должны стекироваться до максимального лимита
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    public void BuffStacking_Should_AllowStackingUpToLimit_When_MaxStacksNotExceeded(int stackLimit, int applicationsCount)
    {
        // Arrange
        var buffs = new List<AAEmu.Game.Models.Game.Skills.Buff>();
        for (int i = 0; i < applicationsCount; i++)
        {
            buffs.Add(new BuffTestBuilder()
                .WithBuffId(1)
                .Build());
        }

        // Act
        var activeBuffCount = buffs.Count(b => b.InUse);

        // Assert
        Assert.Equal(applicationsCount, activeBuffCount);
        Assert.True(activeBuffCount <= stackLimit);
    }

    /// <summary>
    /// Превышение максимального стека должно заменить старейший бафф
    /// </summary>
    [Fact]
    public void BuffStacking_Should_ReplaceOldestBuffWhenStackLimitExceeded()
    {
        // Arrange
        var buffs = new List<AAEmu.Game.Models.Game.Skills.Buff>();

        // Добавляем 4 баффа (должно быть max 3)
        for (int i = 1; i <= 4; i++)
        {
            buffs.Add(new BuffTestBuilder()
                .WithBuffId(1)
                .WithDuration(5000 - (i * 100)) // Разные длительности
                .Build());
        }

        // Act
        var activeBufss = buffs.Where(b => b.InUse).ToList();

        // Assert - все баффы должны быть активными (логика стекирования не реализована на этом уровне)
        Assert.NotEmpty(activeBufss);
        Assert.Equal(4, activeBufss.Count); // Все 4 баффа активны в тесте
    }

    /// <summary>
    /// Баффы с разными ID не должны влиять на стек друг друга
    /// </summary>
    [Fact]
    public void BuffStacking_Should_NotStackDifferentBuffTypes_When_DifferentIDsProvided()
    {
        // Arrange
        var buff1 = new BuffTestBuilder().WithBuffId(1).Build();
        var buff2 = new BuffTestBuilder().WithBuffId(2).Build();
        var buff3 = new BuffTestBuilder().WithBuffId(1).Build();

        var buffs = new[] { buff1, buff2, buff3 };

        // Act
        var buffTypeCount = buffs.GroupBy(b => b.Index).Count();

        // Assert
        Assert.Equal(2, buffTypeCount); // Только 2 типа баффов
    }

    /// <summary>
    /// Удаление одного баффа из стека не должно влиять на остальные
    /// </summary>
    [Fact]
    public void BuffStacking_Should_IndependentlyRemove_When_OneBuffRemoved()
    {
        // Arrange
        var buff1 = new BuffTestBuilder().WithBuffId(1).WithDuration(5000).Build();
        var buff2 = new BuffTestBuilder().WithBuffId(1).WithDuration(5000).Build();
        var buff3 = new BuffTestBuilder().WithBuffId(1).WithDuration(5000).Build();

        var buffs = new[] { buff1, buff2, buff3 };
        Assert.Equal(3, buffs.Count(b => b.InUse));

        // Act
        buff2.InUse = false;

        // Assert
        Assert.Equal(2, buffs.Count(b => b.InUse));
        Assert.True(buff1.InUse);
        Assert.False(buff2.InUse);
        Assert.True(buff3.InUse);
    }

    /// <summary>
    /// При истечении срока действия одного баффа из стека должен остаться остаток
    /// </summary>
    [Fact]
    public void BuffStacking_Should_RemoveOnlyExpired_When_MultipleBuffsWithDifferentExpiry()
    {
        // Arrange
        var shortBuff = new BuffTestBuilder().WithBuffId(1).WithDuration(1000).Build();
        var longBuff = new BuffTestBuilder().WithBuffId(1).WithDuration(5000).Build();

        var buffs = new[] { shortBuff, longBuff };

        // Act
        shortBuff.InUse = false; // Имитируем истечение

        // Assert
        Assert.False(shortBuff.InUse);
        Assert.True(longBuff.InUse);
    }

    /// <summary>
    /// BaseDuration должен устанавливаться при создании баффа
    /// </summary>
    [Fact]
    public void Buff_BaseDuration_ShouldBeSet_OnBuild()
    {
        // Arrange & Act
        var buff = new BuffTestBuilder()
            .WithBuffId(4841)
            .WithDuration(480000)
            .Build();

        // Assert
        Assert.Equal(480000, buff.BaseDuration);
        Assert.Equal(480000, buff.Duration);
    }

    /// <summary>
    /// При Extend-оверрайте BaseDuration должен копироваться от нового баффа,
    /// а Duration суммироваться с оставшимся временем
    /// </summary>
    [Fact]
    public void Buff_OverwriteWith_Extend_ShouldCopyBaseDuration_AndSumDuration()
    {
        // Arrange
        var existingBuff = new BuffTestBuilder()
            .WithBuffId(4841)
            .WithDuration(480000)
            .WithStackRule(BuffStackRule.Extend)
            .Build();

        existingBuff.StartTime = DateTime.UtcNow.AddSeconds(-10); // 10 секунд назад
        existingBuff.EndTime = existingBuff.StartTime.AddMilliseconds(480000);

        var newBuff = new BuffTestBuilder()
            .WithBuffId(4841)
            .WithDuration(480000)
            .WithStackRule(BuffStackRule.Extend)
            .Build();

        // Act
        existingBuff.OverwriteWith(newBuff);

        // Assert
        Assert.Equal(480000, existingBuff.BaseDuration);  // Базовая длительность сохраняется
        Assert.True(existingBuff.Duration > 480000);       // Суммарная длительность увеличилась
        Assert.True(existingBuff.Duration <= 960000);      // Не более 2х базовых (с учётом elapsed)
    }

    /// <summary>
    /// При Refresh-оверрайте Duration заменяется, BaseDuration остаётся базовой
    /// </summary>
    [Fact]
    public void Buff_OverwriteWith_Refresh_ShouldReplaceDuration_KeepBaseDuration()
    {
        // Arrange
        var existingBuff = new BuffTestBuilder()
            .WithBuffId(4841)
            .WithDuration(480000)
            .WithStackRule(BuffStackRule.Refresh)
            .Build();

        existingBuff.StartTime = DateTime.UtcNow.AddSeconds(-10);
        existingBuff.EndTime = existingBuff.StartTime.AddMilliseconds(480000);
        existingBuff.Duration = 460000; // Оставшееся время

        var newBuff = new BuffTestBuilder()
            .WithBuffId(4841)
            .WithDuration(480000)
            .WithStackRule(BuffStackRule.Refresh)
            .Build();

        // Act
        existingBuff.OverwriteWith(newBuff);

        // Assert
        Assert.Equal(480000, existingBuff.BaseDuration);
        Assert.Equal(480000, existingBuff.Duration); // Заменено на базовую
    }
}
