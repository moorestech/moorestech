using System.Linq;
using Core.Master;
using Game.Context;
using Game.Train.Train;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    public class TrainCarInventoryTest
    {
        [Test]
        public void EnumerateInventory_ReturnsSlotIndicesAndStacks()
        {
            TrainTestHelper.CreateEnvironment();

            var trainCar = new TrainCar(tractionForce: 0, inventorySlots: 3, length: 10);

            var filledStack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 2);
            trainCar.SetItem(1, filledStack);

            var inventorySnapshot = trainCar.EnumerateInventory().ToList();

            Assert.AreEqual(3, inventorySnapshot.Count, "Expected EnumerateInventory to return all inventory slots.");
            Assert.AreEqual(0, inventorySnapshot[0].slot, "First slot index should be 0.");
            Assert.AreEqual(1, inventorySnapshot[1].slot, "Second slot index should be 1.");
            Assert.AreEqual(2, inventorySnapshot[2].slot, "Third slot index should be 2.");
            Assert.AreEqual(filledStack, inventorySnapshot[1].item, "Enumerated stack should match the stack assigned to the slot.");
        }

        [Test]
        public void InventoryChecksReflectEnumeratedStacks()
        {
            TrainTestHelper.CreateEnvironment();

            var trainCar = new TrainCar(tractionForce: 0, inventorySlots: 1, length: 10);

            Assert.IsTrue(trainCar.IsInventoryEmpty(), "New train car inventory should start empty.");
            Assert.IsFalse(trainCar.IsInventoryFull(), "New train car inventory should not be full.");

            var maxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId1).MaxStack;
            var fullStack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, maxStack);
            trainCar.SetItem(0, fullStack);

            Assert.IsTrue(trainCar.IsInventoryFull(), "Inventory should report full after filling the slot to max stack.");
            Assert.IsFalse(trainCar.IsInventoryEmpty(), "Inventory should not report empty when a slot is filled.");
        }
    }
}
