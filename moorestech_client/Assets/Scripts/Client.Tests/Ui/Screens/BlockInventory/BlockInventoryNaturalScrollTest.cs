using System.Linq;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 自然高のスクロール器と高さ確定の本文（Web .naturalBodyScroll・.fillPanelHeight）の L1。寸法は 1280×720 の舞台で帯 482
    /// L1 of the natural-height scroller and the fixed-height body (Web .naturalBodyScroll, .fillPanelHeight); the 1280x720 stage gives a 482 band
    /// </summary>
    public class BlockInventoryNaturalScrollTest : BlockInventoryScreenTestHost
    {
        private const string NaturalScrollName = "block-inventory-natural-scroll";
        private const float Band = 482f;

        [Test]
        public void 高さ確定の本文はパネルの本文いっぱいに伸びる()
        {
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest()), new FakeLargePanelRule(true));
            simulate.FrameUpdate();
            var stack = shell.View.Q(className: SectionStackBody.FillPanelHeightClassName);
            Assert.IsNotNull(stack);
            var panelBody = shell.Panel().contentContainer;
            Assert.AreEqual(panelBody.contentRect.height, stack.layout.height, 0.01f);
        }

        [Test]
        public void 小型では本文が自然高のスクロール器の中にあり負マージンと逃げが8()
        {
            var shell = Open(1);
            var scroll = shell.View.Q<GameScrollView>(NaturalScrollName);
            Assert.IsNotNull(scroll);
            Assert.IsNotNull(scroll.contentContainer.Q("section-stack"));
            Assert.AreEqual(-8f, scroll.resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(-8f, scroll.resolvedStyle.marginLeft, 0.01f);
            Assert.AreEqual(8f, scroll.contentContainer.resolvedStyle.paddingTop, 0.01f);
            Assert.AreEqual(8f, scroll.contentContainer.resolvedStyle.paddingLeft, 0.01f);
        }

        [Test]
        public void 大型では本文がスクロール器を通らず直下にある()
        {
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.Chest()), new FakeLargePanelRule(true));
            simulate.FrameUpdate();
            Assert.IsNull(shell.View.Q(NaturalScrollName));
            Assert.AreEqual("section-stack", shell.View.Q("block-inventory-body")[0].name);
        }

        [Test]
        public void 帯を超える中身はスクロール器の中で流れる()
        {
            // 120 枠は 12 行で 650.8px あり帯を超える（60 枠は外殻込み 373.6px で帯に収まるので使わない）
            // 120 slots make 12 rows of 650.8px, past the band (60 slots fit at 373.6px with the shell, so they are not used)
            var shell = Open(120);
            var scroll = shell.View.Q<GameScrollView>(NaturalScrollName);
            Assert.AreEqual(Band, shell.Panel().layout.height, 0.01f);
            Assert.Greater(scroll.GetContentHeight(), scroll.GetViewportHeight());
        }

        [Test]
        public void 短い本文は内容の高さでスクロールしない()
        {
            var shell = Open(10);
            var scroll = shell.View.Q<GameScrollView>(NaturalScrollName);
            Assert.Less(shell.Panel().layout.height, Band);
            Assert.LessOrEqual(scroll.GetContentHeight(), scroll.GetViewportHeight() + 0.01f);
        }

        private BlockInventoryTestShell Open(int slotCount)
        {
            var slots = Enumerable.Repeat(new SlotItem(new ItemId(1), 1), slotCount).ToArray();
            var shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.ChestWithSlots(slots)));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return shell;
        }
    }
}
