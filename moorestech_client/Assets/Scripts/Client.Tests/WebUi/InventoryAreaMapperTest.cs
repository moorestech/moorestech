using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Game.Actions;
using NUnit.Framework;

namespace Client.Tests.WebUi
{
    public class InventoryAreaMapperTest
    {
        [TestCase("main", 0, LocalMoveInventoryType.MainOrSub, 0)]
        [TestCase("main", 35, LocalMoveInventoryType.MainOrSub, 35)]
        [TestCase("hotbar", 0, LocalMoveInventoryType.MainOrSub, 36)]
        [TestCase("hotbar", 8, LocalMoveInventoryType.MainOrSub, 44)]
        [TestCase("grab", 0, LocalMoveInventoryType.Grab, 0)]
        public void ValidAreaSlotMapsToLocalSlot(string area, int slot, LocalMoveInventoryType expectedType, int expectedSlot)
        {
            var ok = InventoryAreaMapper.TryGetLocalSlot(area, slot, out var type, out var localSlot);
            Assert.IsTrue(ok);
            Assert.AreEqual(expectedType, type);
            Assert.AreEqual(expectedSlot, localSlot);
        }

        [TestCase("main", -1)]
        [TestCase("main", 36)]
        [TestCase("hotbar", -1)]
        [TestCase("hotbar", 9)]
        [TestCase("sub", 0)]
        [TestCase(null, 0)]
        public void InvalidAreaSlotReturnsFalse(string area, int slot)
        {
            var ok = InventoryAreaMapper.TryGetLocalSlot(area, slot, out _, out _);
            Assert.IsFalse(ok);
        }
    }
}
