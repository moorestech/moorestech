using Game.PlayerInventory.Interface;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class HotBarSlotToInventorySlotTest
    {
        [Test]
        public void DynamicSizeHotBarSlotTest()
        {
            // 45スロットは従来通り36-44
            // 45 slots keep the classic 36-44
            Assert.AreEqual(36, PlayerInventoryConst.HotBarSlotToInventorySlot(0, 45));
            Assert.AreEqual(44, PlayerInventoryConst.HotBarSlotToInventorySlot(8, 45));

            // 54スロット時は45〜53がホットバー
            // With 54 slots the hotbar shifts to 45..53
            Assert.AreEqual(45, PlayerInventoryConst.HotBarSlotToInventorySlot(0, 54));
            Assert.AreEqual(53, PlayerInventoryConst.HotBarSlotToInventorySlot(8, 54));

            Assert.IsTrue(PlayerInventoryConst.IsHotBarSlot(36, 45));
            Assert.IsFalse(PlayerInventoryConst.IsHotBarSlot(35, 45));
            Assert.IsFalse(PlayerInventoryConst.IsHotBarSlot(36, 54));
            Assert.AreEqual(new[] { 36, 37, 38, 39, 40, 41, 42, 43, 44 }, PlayerInventoryConst.GetHotBarSlots(45));
        }
    }
}