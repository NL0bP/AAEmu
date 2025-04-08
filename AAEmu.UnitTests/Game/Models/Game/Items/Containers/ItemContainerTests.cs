using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Containers;
using AAEmu.Game.Models.Game.Items.Templates;

using Xunit;

namespace AAEmu.UnitTests.Game.Models.Game.Items.Containers
{
    public class ItemContainerTests
    {
        [Fact]
        public void AddOrMoveExistingItem_ShouldNotDuplicateItems()
        {
            // Arrange
            var container = new ItemContainer(1, SlotType.Bag, true, null);
            var itemTemplate = new ItemTemplate { MaxCount = 10 };
            var item1 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 0, Count = 5 };
            var item2 = new Item { Template = itemTemplate, TemplateId = 101, Slot = 1, Count = 3 };

            container.Items = [item1, item2];

            var newItem = new Item { Template = itemTemplate, TemplateId = 100, Count = 2 };

            // Act
            var result = container.AddOrMoveExistingItem(ItemTaskType.Invalid, newItem, 0);

            // Assert
            Assert.True(result);
            Assert.Equal(7, container.GetItemBySlot(0).Count);
            Assert.Null(container.GetItemBySlot(2));
        }

        [Fact]
        public void AddOrMoveExistingItem2_ShouldNotDuplicateItems()
        {
            // Arrange
            var container = new ItemContainer(1, SlotType.Bag, true, null);
            var itemTemplate = new ItemTemplate { MaxCount = 10 };
            var item1 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 0, Count = 5 };
            var item2 = new Item { Template = itemTemplate, TemplateId = 101, Slot = 1, Count = 3 };

            container.Items = [item1, item2];

            var newItem = new Item { Template = itemTemplate, TemplateId = 100, Count = 2 };

            // Act
            var result = container.AddOrMoveExistingItem2(ItemTaskType.Invalid, newItem, 0);

            // Assert
            Assert.True(result);
            Assert.Equal(7, container.GetItemBySlot(0).Count);
            Assert.Null(container.GetItemBySlot(2));
        }

        [Fact]
        public void AddOrMoveExistingItem_ShouldNotAddToFullContainer()
        {
            // Arrange
            var container = new ItemContainer(1, SlotType.Bag, true, null) { ContainerSize = 2 };
            var itemTemplate = new ItemTemplate { MaxCount = 10 };
            var item1 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 0, Count = 10 };
            var item2 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 1, Count = 10 };

            container.Items = [item1, item2];

            var newItem = new Item { Template = itemTemplate, TemplateId = 100, Count = 1 };

            // Act
            var result = container.AddOrMoveExistingItem(ItemTaskType.Invalid, newItem, -1);

            // Assert
            Assert.False(result);
            Assert.Equal(10, container.GetItemBySlot(0).Count);
            Assert.Equal(10, container.GetItemBySlot(1).Count);
        }

        [Fact]
        public void AddOrMoveExistingItem_ShouldNotCombineDifferentTemplateId()
        {
            // Arrange
            var container = new ItemContainer(1, SlotType.Bag, true, null);
            var itemTemplate1 = new ItemTemplate { MaxCount = 10 };
            var itemTemplate2 = new ItemTemplate { MaxCount = 10 };
            var item1 = new Item { Template = itemTemplate1, TemplateId = 100, Slot = 0, Count = 5 };
            var item2 = new Item { Template = itemTemplate2, TemplateId = 101, Slot = 1, Count = 3 };

            container.Items = [item1, item2];

            var newItem = new Item { Template = itemTemplate2, TemplateId = 101, Count = 2 };

            // Act
            var result = container.AddOrMoveExistingItem(ItemTaskType.Invalid, newItem, 0);

            // Assert
            Assert.True(result);
            Assert.Equal(5, container.GetItemBySlot(0).Count);
            Assert.Equal(3, container.GetItemBySlot(1).Count);
            Assert.Equal(2, container.GetItemBySlot(2).Count);
        }

        [Fact]
        public void AddOrMoveExistingItem_ShouldNotCombineDifferentGrade()
        {
            // Arrange
            var container = new ItemContainer(1, SlotType.Bag, true, null);
            var itemTemplate = new ItemTemplate { MaxCount = 10 };
            var item1 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 0, Count = 5, Grade = 1 };
            var item2 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 1, Count = 3, Grade = 2 };

            container.Items = [item1, item2];

            var newItem = new Item { Template = itemTemplate, TemplateId = 100, Count = 2, Grade = 2 };

            // Act
            var result = container.AddOrMoveExistingItem(ItemTaskType.Invalid, newItem, 0);

            // Assert
            Assert.True(result);
            Assert.Equal(5, container.GetItemBySlot(0).Count);
            Assert.Equal(3, container.GetItemBySlot(1).Count);
            Assert.Equal(2, container.GetItemBySlot(2).Count);
        }

        [Fact]
        public void AddOrMoveExistingItem_ShouldCombineSameGrade()
        {
            // Arrange
            var container = new ItemContainer(1, SlotType.Bag, true, null);
            var itemTemplate = new ItemTemplate { MaxCount = 10 };
            var item1 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 0, Count = 5, Grade = 1 };
            var item2 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 1, Count = 3, Grade = 2 };

            container.Items = [item1, item2];

            var newItem = new Item { Template = itemTemplate, TemplateId = 100, Count = 2, Grade = 2 };

            // Act
            var result = container.AddOrMoveExistingItem(ItemTaskType.Invalid, newItem, 1);

            // Assert
            Assert.True(result);
            Assert.Equal(5, container.GetItemBySlot(0).Count);
            Assert.Equal(5, container.GetItemBySlot(1).Count);
        }

        [Fact]
        public void AddOrMoveExistingItem_ShouldAddToFirstAvailableSlot()
        {
            // Arrange
            var container = new ItemContainer(1, SlotType.Bag, true, null) { ContainerSize = 3 };
            var itemTemplate = new ItemTemplate { MaxCount = 10 };
            var item1 = new Item { Template = itemTemplate, TemplateId = 100, Slot = 0, Count = 5 };

            container.Items = [item1];

            var newItem = new Item { Template = itemTemplate, TemplateId = 100, Count = 2 };

            // Act
            var result = container.AddOrMoveExistingItem(ItemTaskType.Invalid, newItem, -1);

            // Assert
            Assert.True(result);
            Assert.Equal(5, container.GetItemBySlot(0).Count);
            Assert.Equal(2, container.GetItemBySlot(1).Count);
            Assert.Null(container.GetItemBySlot(3));
        }
    }
}
