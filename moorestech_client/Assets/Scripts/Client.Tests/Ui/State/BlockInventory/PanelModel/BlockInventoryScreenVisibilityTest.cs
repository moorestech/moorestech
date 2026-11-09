using Client.Game.InGame.UI.UIState;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// ブロックインベントリの画面の出し分けは画面ルータとパネルの閉だけで決まる（L0）
    /// Block-inventory screen visibility depends only on the screen router and the closed panel (L0)
    /// </summary>
    public class BlockInventoryScreenVisibilityTest
    {
        [Test]
        public void SubInventoryでブロックが開いていれば出す()
        {
            Assert.IsTrue(BlockInventoryScreenVisibility.IsShown(new UiRoutingSnapshot(UiScreen.SubInventory), BlockInventoryTestPanels.OfType("Chest")));
        }

        [Test]
        public void SubInventoryでもパネルが閉なら出さない()
        {
            Assert.IsFalse(BlockInventoryScreenVisibility.IsShown(new UiRoutingSnapshot(UiScreen.SubInventory), BlockInventoryPanelState.Closed));
        }

        [Test]
        public void GameScreenではブロックが開いた値が残っていても出さない()
        {
            Assert.IsFalse(BlockInventoryScreenVisibility.IsShown(new UiRoutingSnapshot(UiScreenRouting.Resolve(UIStateEnum.GameScreen, null)), BlockInventoryTestPanels.OfType("Chest")));
        }
    }
}
