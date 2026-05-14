using Core.Master;
using Game.Context;
using Game.Train.Unit.Containers;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainCarInventoryTest
    {
        [Test]
        public void EnumerateInventory_ReturnsAllSlotStacks()
        {
            TrainTestHelper.CreateEnvironment();

            var (_, container) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 3, 10, true);

            var filledStack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 2);
            container.SetItem(1, filledStack);

            var inventorySnapshot = container.InventoryItems;

            Assert.AreEqual(3, inventorySnapshot.Count, "Expected InventoryItems to return all inventory slots.");
            Assert.AreEqual(ItemMaster.EmptyItemId, inventorySnapshot[0].Id, "First slot should remain empty.");
            Assert.AreEqual(filledStack, inventorySnapshot[1], "Slot stack should match the assigned stack.");
            Assert.AreEqual(ItemMaster.EmptyItemId, inventorySnapshot[2].Id, "Third slot should remain empty.");
        }

        [Test]
        public void InventoryChecksReflectEnumeratedStacks()
        {
            TrainTestHelper.CreateEnvironment();

            var (trainCar, itemContainer) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 1, 10, true);

            Assert.IsTrue(trainCar.IsInventoryEmpty(), "New train car inventory should start empty.");
            Assert.IsFalse(trainCar.IsInventoryFull(), "New train car inventory should not be full.");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            var fullStack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack);
            itemContainer.SetItem(0, fullStack);

            Assert.IsTrue(trainCar.IsInventoryFull(), "Inventory should report full after filling the slot to max stack.");
            Assert.IsFalse(trainCar.IsInventoryEmpty(), "Inventory should not report empty when a slot is filled.");
        }
    }
}
