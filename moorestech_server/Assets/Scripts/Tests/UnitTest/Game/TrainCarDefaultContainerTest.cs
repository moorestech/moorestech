using System;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Mooresmaster.Model.TrainModule;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    // マスタ指定のdefaultContainerTypeに応じてTrainCarへ既定コンテナが装着されることを検証する
    // Verifies that TrainCar receives the default container specified by master's defaultContainerType.
    public class TrainCarDefaultContainerTest
    {
        [Test]
        public void AttachDefaultContainer_None_LeavesContainerNull()
        {
            // TrainCarコンストラクタはServerContext経由でITrainUpdateEventを取得するため最小環境を生成する
            // TrainCar's constructor resolves ITrainUpdateEvent via ServerContext, so build a minimal environment.
            TrainTestHelper.CreateEnvironment();

            var element = CreateMaster("None", inventorySlots: 5, fluidCapacity: 0f);
            var car = new TrainCar(element, isFacingForward: true);

            car.AttachDefaultContainerFromMaster();

            Assert.IsNull(car.Container, "Noneの場合はコンテナが装着されないべき / Container must remain null for None");
        }

        [Test]
        public void AttachDefaultContainer_Item_AttachesItemTrainCarContainer()
        {
            TrainTestHelper.CreateEnvironment();

            const int slotCount = 7;
            var element = CreateMaster("Item", inventorySlots: slotCount, fluidCapacity: 0f);
            var car = new TrainCar(element, isFacingForward: true);

            car.AttachDefaultContainerFromMaster();

            Assert.IsInstanceOf<ItemTrainCarContainer>(car.Container, "Itemの場合はItemTrainCarContainerが装着されるべき / Item must yield ItemTrainCarContainer");
            var itemContainer = (ItemTrainCarContainer)car.Container;
            Assert.AreEqual(slotCount, itemContainer.GetSlotSize(), "inventorySlotsと一致するスロット数で生成されるべき / Slot count must match inventorySlots");
        }

        [Test]
        public void AttachDefaultContainer_Fluid_AttachesFluidTrainCarContainerWithCapacity()
        {
            TrainTestHelper.CreateEnvironment();

            const float capacity = 1250f;
            var element = CreateMaster("Fluid", inventorySlots: 0, fluidCapacity: capacity);
            var car = new TrainCar(element, isFacingForward: true);

            car.AttachDefaultContainerFromMaster();

            Assert.IsInstanceOf<FluidTrainCarContainer>(car.Container, "Fluidの場合はFluidTrainCarContainerが装着されるべき / Fluid must yield FluidTrainCarContainer");
            var fluidContainer = (FluidTrainCarContainer)car.Container;
            Assert.AreEqual(capacity, fluidContainer.Container.Capacity, "fluidCapacityと一致する容量で生成されるべき / Capacity must match fluidCapacity");
        }

        private static TrainCarMasterElement CreateMaster(string defaultContainerType, int inventorySlots, float fluidCapacity)
        {
            // テスト用に最小限の値だけを指定し、他は安全な既定値で埋める
            // Fill in minimal values for tests; other fields use safe defaults.
            return new TrainCarMasterElement(0, Guid.NewGuid(), Guid.Empty, null, 100, 0, inventorySlots, 1, defaultContainerType, fluidCapacity, null, null);
        }
    }
}
