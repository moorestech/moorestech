using Client.Ui.State.Inventory.Grab;
using Client.Ui.State.Items;
using Client.Ui.State.Routing;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Inventory.Grab
{
    /// <summary>
    /// 掴み絵は掴みを扱う画面で何かを持っているときだけ出る（Web useGrabInteractive と grab.count > 0）
    /// The grab picture shows only on grab-capable screens while holding something (Web useGrabInteractive and grab.count > 0)
    /// </summary>
    public class GrabOverlayViewModelFactoryTest
    {
        private static readonly ItemId Wood = new(1);

        [TestCase(UiScreen.PlayerInventory, true)]
        [TestCase(UiScreen.SubInventory, true)]
        [TestCase(UiScreen.ResearchTree, true)]
        [TestCase(UiScreen.None, false)]
        [TestCase(UiScreen.BuildMenu, false)]
        [TestCase(UiScreen.PauseMenu, false)]
        public void 掴みを扱う画面で持っているときだけ出る(UiScreen screen, bool expected)
        {
            var model = GrabOverlayViewModelFactory.Create(screen, new SlotItem(Wood, 2));

            Assert.AreEqual(expected ? GrabOverlayViewModel.Holding(Wood, 2) : GrabOverlayViewModel.Hidden, model);
        }

        [Test]
        public void 何も持っていなければ持ち物の画面でも出ない()
        {
            Assert.AreEqual(GrabOverlayViewModel.Hidden, GrabOverlayViewModelFactory.Create(UiScreen.PlayerInventory, new SlotItem(ItemMaster.EmptyItemId, 0)));
        }

        [Test]
        public void 個数が違えば別の表示として扱う()
        {
            // ReactiveProperty の同値の間引きで個数の変化を落とさない
            // ReactiveProperty's equality filter must not drop a count change
            Assert.AreNotEqual(GrabOverlayViewModel.Holding(Wood, 2), GrabOverlayViewModel.Holding(Wood, 1));
        }
    }
}
