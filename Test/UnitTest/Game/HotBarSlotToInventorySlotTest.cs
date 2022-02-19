using Game.PlayerInventory.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.UnitTest.Game
{
    [TestClass]
    public class HotBarSlotToInventorySlotTest
    {
        [TestMethod]
        public void Test()
        {
            Test(0, 36);
            Test(1, 37);
            Test(8, 44);
        }
        public void Test(int hotBarSlot, int inventorySlot)
        {
            Assert.AreEqual(inventorySlot, PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot));
        }
    }
}