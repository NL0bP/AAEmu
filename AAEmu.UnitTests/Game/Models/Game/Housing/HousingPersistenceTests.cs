using System.Collections.Generic;
using System.Reflection;

using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Models;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Static;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Housing;
using AAEmu.Game.Models.Game.World.Transform;

using Xunit;

namespace AAEmu.UnitTests.Game.Models.Game.Housing
{
    public class HousingPersistenceTests
    {
        private const uint BindingDoodadTemplateId = 900000001;

        public HousingPersistenceTests()
        {
            AppConfiguration.Instance.World ??= new WorldConfig();
            EnsureDoodadTemplate(BindingDoodadTemplateId);
        }

        [Fact]
        public void CurrentStep_WhenLoadedFromDatabase_ShouldCreateNonPersistentBindingDoodads()
        {
            var house = CreateHouse(isLoadingFromDatabase: true);

            house.CurrentStep = -1;

            Assert.Single(house.AttachedDoodads);
            Assert.False(house.AttachedDoodads[0].IsPersistent);
        }

        [Fact]
        public void CurrentStep_WhenNotLoadedFromDatabase_ShouldCreatePersistentBindingDoodads()
        {
            var house = CreateHouse(isLoadingFromDatabase: false);

            house.CurrentStep = -1;

            Assert.Single(house.AttachedDoodads);
            Assert.True(house.AttachedDoodads[0].IsPersistent);
        }

        [Fact]
        public void ReplaceAttachedDoodadByAttachPoint_ShouldRemoveLoadedPlaceholderAndKeepPersistedState()
        {
            var house = CreateHouse(isLoadingFromDatabase: true);
            house.CurrentStep = -1;
            var placeholder = house.AttachedDoodads[0];

            var persisted = DoodadManager.Instance.Create(0, BindingDoodadTemplateId, null, true);
            persisted.IsPersistent = true;
            persisted.DbId = 987;
            persisted.FuncGroupId = 654321;
            persisted.AttachPoint = AttachPointKind.Driver;
            persisted.OwnerType = DoodadOwnerType.Housing;
            persisted.OwnerDbId = house.Id;

            var replaced = house.ReplaceAttachedDoodadByAttachPoint(persisted);

            Assert.True(replaced);
            Assert.Single(house.AttachedDoodads);
            Assert.Same(persisted, house.AttachedDoodads[0]);
            Assert.NotSame(placeholder, house.AttachedDoodads[0]);
            Assert.Equal(654321u, persisted.FuncGroupId);
            Assert.Same(house, persisted.ParentObj);
            Assert.Equal(house.ObjId, persisted.ParentObjId);
            Assert.Same(house.Transform, persisted.Transform.Parent);
        }

        private static House CreateHouse(bool isLoadingFromDatabase)
        {
            var house = new House
            {
                Id = 123,
                OwnerId = 456,
                IsLoadingFromDatabase = isLoadingFromDatabase,
                Template = new HousingTemplate
                {
                    MainModelId = 789,
                    HousingBindingDoodad =
                    [
                        new HousingBindingDoodad
                        {
                            AttachPointId = AttachPointKind.Driver,
                            DoodadId = BindingDoodadTemplateId,
                            Position = new WorldSpawnPosition()
                        }
                    ]
                }
            };

            house.Transform.Local.SetPosition(100f, 200f, 50f);
            return house;
        }

        private static void EnsureDoodadTemplate(uint templateId)
        {
            EnsureDoodadManagerField("_funcsByGroups", new Dictionary<uint, List<DoodadFunc>>());
            EnsureDoodadManagerField("_phaseFuncs", new Dictionary<uint, List<DoodadPhaseFunc>>());

            var field = typeof(DoodadManager).GetField("_templates", BindingFlags.Instance | BindingFlags.NonPublic);
            var templates = (Dictionary<uint, DoodadTemplate>)field.GetValue(DoodadManager.Instance);
            if (templates == null)
            {
                templates = [];
                field.SetValue(DoodadManager.Instance, templates);
            }

            templates[templateId] = new DoodadTemplate
            {
                Id = templateId
            };
        }

        private static void EnsureDoodadManagerField<T>(string fieldName, T value)
        {
            var field = typeof(DoodadManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field.GetValue(DoodadManager.Instance) == null)
                field.SetValue(DoodadManager.Instance, value);
        }
    }
}
