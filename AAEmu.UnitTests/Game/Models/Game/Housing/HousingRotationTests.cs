using System;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Housing;
using Xunit;

namespace AAEmu.UnitTests.Game.Models.Game.Housing
{
    public class HousingRotationTests
    {
        private const float DegToRad = MathF.PI / 180f;

        [Fact]
        public void HouseRotation_WithoutRotation_Should_KeepDoodadPosition()
        {
            // Arrange
            var house = new House();
            house.Transform.Local.SetPosition(100, 100, 50);
            house.Transform.Local.SetRotation(0, 0, 0);

            var doodad = new Doodad();
            doodad.Transform.Parent = house.Transform;
            doodad.Transform.Local.SetPosition(10, 10, 5);

            // Act & Assert: без вращения мировые координаты = дом + локальные
            var worldPos = doodad.Transform.World.Position;
            Assert.Equal(110.0f, worldPos.X, 1);
            Assert.Equal(110.0f, worldPos.Y, 1);
            Assert.Equal(55.0f, worldPos.Z, 1);
        }

        [Fact]
        public void HouseRotation_With90Degrees_Should_UpdateDoodadWorldPosition()
        {
            // Arrange
            var house = new House();
            house.Transform.Local.SetPosition(100, 100, 50);
            house.Transform.Local.SetRotation(0, 0, 0);

            var doodad = new Doodad();
            doodad.Transform.Parent = house.Transform;
            // Предмет справа от дома (по оси X)
            doodad.Transform.Local.SetPosition(10, 0, 5);

            // Act: вращаем дом на 90 градусов
            house.Transform.Local.SetRotation(0, 0, 90f * DegToRad);

            // Assert: после вращения на 90° по Z, (10, 0) локально 
            // должно стать (0, 10) в мировых координатах (относительно дома)
            // Общие мировые: (100 + 0, 100 + 10, 50 + 5) = (100, 110, 55)
            var worldPos = doodad.Transform.World.Position;
            Assert.Equal(100.0f, worldPos.X, 1);
            Assert.Equal(110.0f, worldPos.Y, 1);
            Assert.Equal(55.0f, worldPos.Z, 1);
        }

        [Fact]
        public void HouseRotation_With180Degrees_Should_UpdateDoodadWorldPosition()
        {
            // Arrange
            var house = new House();
            house.Transform.Local.SetPosition(100, 100, 50);
            house.Transform.Local.SetRotation(0, 0, 0);

            var doodad = new Doodad();
            doodad.Transform.Parent = house.Transform;
            doodad.Transform.Local.SetPosition(10, 10, 5);

            // Act: вращаем на 180 градусов
            house.Transform.Local.SetRotation(0, 0, 180f * DegToRad);

            // Assert: (10, 10) -> (-10, -10) относительно дома
            // Мировые: (100 - 10, 100 - 10, 50 + 5) = (90, 90, 55)
            var worldPos = doodad.Transform.World.Position;
            Assert.Equal(90.0f, worldPos.X, 1);
            Assert.Equal(90.0f, worldPos.Y, 1);
            Assert.Equal(55.0f, worldPos.Z, 1);
        }

        [Fact]
        public void HouseRotation_MultipleDoodads_Should_UpdateAll()
        {
            // Arrange
            var house = new House();
            house.Transform.Local.SetPosition(0, 0, 0);
            house.Transform.Local.SetRotation(0, 0, 0);

            var doodad1 = new Doodad();
            doodad1.Transform.Parent = house.Transform;
            doodad1.Transform.Local.SetPosition(10, 0, 0);

            var doodad2 = new Doodad();
            doodad2.Transform.Parent = house.Transform;
            doodad2.Transform.Local.SetPosition(0, 10, 0);

            // Act: вращаем на 90 градусов
            house.Transform.Local.SetRotation(0, 0, 90f * DegToRad);

            // Assert
            var pos1 = doodad1.Transform.World.Position;
            var pos2 = doodad2.Transform.World.Position;

            // doodad1: (10, 0) -> (0, 10)
            Assert.Equal(0.0f, pos1.X, 1);
            Assert.Equal(10.0f, pos1.Y, 1);

            // doodad2: (0, 10) -> (-10, 0)
            Assert.Equal(-10.0f, pos2.X, 1);
            Assert.Equal(0.0f, pos2.Y, 1);
        }

        [Fact]
        public void HouseRotation_HeightChange_Should_MoveDoodadWithHouse()
        {
            // Arrange
            var house = new House();
            house.Transform.Local.SetPosition(100, 100, 50);
            house.Transform.Local.SetRotation(0, 0, 0);

            var doodad = new Doodad();
            doodad.Transform.Parent = house.Transform;
            doodad.Transform.Local.SetPosition(10, 10, 5);

            // Act: меняем высоту дома
            house.Transform.Local.SetPosition(100, 100, 60);

            // Assert: предмет должен подняться вместе с домом
            var worldPos = doodad.Transform.World.Position;
            Assert.Equal(110.0f, worldPos.X, 1);
            Assert.Equal(110.0f, worldPos.Y, 1);
            Assert.Equal(65.0f, worldPos.Z, 1); // 60 + 5
        }

        [Fact]
        public void HouseRotation_RotationShouldNotAffectDoodadRotationIfNotSet()
        {
            // Arrange
            var house = new House();
            house.Transform.Local.SetRotation(0, 0, 90f * DegToRad);

            var doodad = new Doodad();
            doodad.Transform.Parent = house.Transform;
            // У предмета свое локальное вращение (0)
            doodad.Transform.Local.SetRotation(0, 0, 0);

            // Assert: мировое вращение предмета должно учитывать вращение родителя
            var worldRot = doodad.Transform.World.Rotation;
            // Ожидаем, что мировое вращение будет 90 градусов (в радианах)
            Assert.Equal(90f * DegToRad, worldRot.Z, 2);
        }

        [Fact]
        public void HouseRotation_With270Degrees_Should_UpdateDoodadWorldPosition()
        {
            // Arrange
            var house = new House();
            house.Transform.Local.SetPosition(0, 0, 0);
            house.Transform.Local.SetRotation(0, 0, 0);

            var doodad = new Doodad();
            doodad.Transform.Parent = house.Transform;
            doodad.Transform.Local.SetPosition(10, 0, 0);

            // Act: вращаем на 270 градусов (или -90)
            house.Transform.Local.SetRotation(0, 0, 270f * DegToRad);

            // Assert: (10, 0) при 270° -> (0, -10)
            var worldPos = doodad.Transform.World.Position;
            Assert.Equal(0.0f, worldPos.X, 1);
            Assert.Equal(-10.0f, worldPos.Y, 1);
        }
    }
}
