using System;
using System.Threading.Tasks;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using Moq;
using Xunit;

namespace AAEmu.UnitTests.Game.Models.Game.Buff
{
    public class TestBuffTemplate : BuffTemplate
    {
        public int GetDuration(ushort level) => 1000;

        public new double GetTick() => 200;

        public virtual void Start(BaseUnit caster, BaseUnit owner, AAEmu.Game.Models.Game.Skills.Buff buff)
        {
            // Реализация для тестов
        }

        public virtual void TimeToTimeApply(BaseUnit caster, BaseUnit owner, AAEmu.Game.Models.Game.Skills.Buff buff)
        {
            // Реализация для тестов
        }

        public virtual void Dispel(BaseUnit caster, BaseUnit owner, AAEmu.Game.Models.Game.Skills.Buff buff, bool replaced = false)
        {
            // Реализация для тестов
        }
    }

    public class BuffTests
    {
        private AAEmu.Game.Models.Game.Skills.Buff CreateBuff(out TestBuffTemplate template, DateTime? startTime = null)
        {
            template = new TestBuffTemplate();

            var owner = new Mock<BaseUnit>();
            var caster = new Mock<Unit>();
            var skillCaster = new Mock<SkillCaster>();
            var skill = new Mock<Skill>();

            return new AAEmu.Game.Models.Game.Skills.Buff(owner.Object, caster.Object, skillCaster.Object, template, skill.Object, startTime ?? DateTime.MinValue);
        }

        [Fact]
        public async Task ConsumeCharge_ShouldReduceChargeAndExitWhenZero()
        {
            // Arrange
            var buff = CreateBuff(out var template);
            buff.Charge = 50;

            // Act
            var remainder = buff.ConsumeCharge(20);

            // Assert
            Assert.Equal(30, buff.Charge);
            Assert.Equal(0, remainder);

            // Act 2: полностью поглощаем оставшийся заряд
            remainder = buff.ConsumeCharge(30);

            // Так как для завершения эффекта могут потребоваться обработка задач, даём немного времени
            await Task.Delay(50);

            // Assert 2
            Assert.Equal(0, buff.Charge);
            Assert.True(buff.IsEnded(), "Бафф должен быть завершен, когда заряд равен 0");
        }

        [Fact]
        public void OverwriteWith_ShouldUpdatePropertiesAndReinitializeTiming()
        {
            // Arrange
            var buff = CreateBuff(out var template);
            buff.Charge = 50;
            buff.Duration = 1000;
            buff.StartTime = DateTime.UtcNow.AddMilliseconds(-500);

            // Создаём новый бафф для перезаписи
            var newBuff = CreateBuff(out var newTemplate);
            newBuff.Charge = 100;
            newBuff.Duration = 2000;

            // Act: запоминаем оставшееся время и перезаписываем
            var remainingBefore = buff.GetTimeLeft();
            buff.OverwriteWith(newBuff);

            // Assert
            Assert.Equal(100, buff.Charge);
            // При условии refresh (по умолчанию) длительность обновляется новым значением
            Assert.Equal(newBuff.Duration, buff.Duration);
            Assert.True(buff.GetTimeLeft() <= buff.Duration, "Остаток времени должен соответствовать новой длительности");
        }

        [Fact]
        public void Exit_ShouldNotИзменятьСостояние_ЕслиУжеFinished()
        {
            // Arrange
            var buff = CreateBuff(out var template);
            buff.State = EffectState.Finished;

            // Act
            buff.Exit();

            // Assert
            Assert.Equal(EffectState.Finished, buff.State);
        }
    }
}
