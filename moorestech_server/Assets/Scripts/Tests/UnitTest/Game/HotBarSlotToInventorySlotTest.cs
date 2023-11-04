
using Game.PlayerInventory.Interface;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class HotBarSlotToInventorySlotTest
    {
        [TestCase(0, 36)]
        [TestCase(1, 37)]
        [TestCase(8, 44)]
        public void Test(int hotBarSlot, int inventorySlot)
        {
            Assert.AreEqual(inventorySlot, PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot));
        }
    }
}